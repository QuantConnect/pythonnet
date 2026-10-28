using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

using static Python.Runtime.MethodBinder;

namespace Python.Runtime
{
    internal static class OperatorMethod
    {
        /// <summary>
        /// Maps the compiled method name in .NET CIL (e.g. op_Addition) to
        /// the equivalent Python operator (e.g. __add__) as well as the offset
        /// that identifies that operator's slot (e.g. nb_add) in heap space.
        /// </summary>
        public static Dictionary<string, SlotDefinition> OpMethodMap { get; private set; }
        public static Dictionary<string, string> ComparisonOpMap { get; private set; }
        public readonly struct SlotDefinition
        {
            public SlotDefinition(string methodName, int typeOffset)
            {
                MethodName = methodName;
                TypeOffset = typeOffset;
            }
            public string MethodName { get; }
            public int TypeOffset { get; }

        }
        private static HashSet<string> _operatorNames;
        private static PyObject? _opType;

        static OperatorMethod()
        {
            // .NET operator method names are documented at:
            // https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/operator-overloads
            // Python operator methods and slots are documented at:
            // https://docs.python.org/3/c-api/typeobj.html
            // TODO: Rich compare, inplace operator support
            OpMethodMap = new Dictionary<string, SlotDefinition>
            {
                ["op_Addition"] = new SlotDefinition("__add__", TypeOffset.nb_add),
                ["op_Subtraction"] = new SlotDefinition("__sub__", TypeOffset.nb_subtract),
                ["op_Multiply"] = new SlotDefinition("__mul__", TypeOffset.nb_multiply),
                ["op_Division"] = new SlotDefinition("__truediv__", TypeOffset.nb_true_divide),
                ["op_Modulus"] = new SlotDefinition("__mod__", TypeOffset.nb_remainder),
                ["op_BitwiseAnd"] = new SlotDefinition("__and__", TypeOffset.nb_and),
                ["op_BitwiseOr"] = new SlotDefinition("__or__", TypeOffset.nb_or),
                ["op_ExclusiveOr"] = new SlotDefinition("__xor__", TypeOffset.nb_xor),
                ["op_LeftShift"] = new SlotDefinition("__lshift__", TypeOffset.nb_lshift),
                ["op_RightShift"] = new SlotDefinition("__rshift__", TypeOffset.nb_rshift),
                ["op_OnesComplement"] = new SlotDefinition("__invert__", TypeOffset.nb_invert),
                ["op_UnaryNegation"] = new SlotDefinition("__neg__", TypeOffset.nb_negative),
                ["op_UnaryPlus"] = new SlotDefinition("__pos__", TypeOffset.nb_positive),

                ["__int__"] = new SlotDefinition("__int__", TypeOffset.nb_int),
                ["__float__"] = new SlotDefinition("__float__", TypeOffset.nb_float),
            };
            ComparisonOpMap = new Dictionary<string, string>
            {
                ["op_Equality"] = "__eq__",
                ["op_Inequality"] = "__ne__",
                ["op_LessThanOrEqual"] = "__le__",
                ["op_GreaterThanOrEqual"] = "__ge__",
                ["op_LessThan"] = "__lt__",
                ["op_GreaterThan"] = "__gt__",
            };
            _operatorNames = new HashSet<string>(OpMethodMap.Keys.Concat(ComparisonOpMap.Keys));
        }

        public static void Initialize()
        {
            _opType = GetOperatorType();
        }

        public static void Shutdown()
        {
            if (_opType != null)
            {
                _opType.Dispose();
                _opType = null;
            }
        }

        public static bool IsOperatorMethod(MethodBase method)
        {
            if (!method.IsSpecialName && !method.IsOpsHelper())
            {
                return false;
            }
            return _operatorNames.Contains(method.Name);
        }

        public static bool IsComparisonOp(MethodBase method)
        {
            return ComparisonOpMap.ContainsKey(method.Name);
        }

        /// <summary>
        /// For the operator methods of a CLR type, set the special slots of the
        /// corresponding Python type's operator methods.
        /// </summary>
        public static void FixupSlots(BorrowedReference pyType, Type clrType)
        {
            Debug.Assert(_opType != null);

            var operatorCandidates = GetOperatorCandidates(clrType);

            foreach (var method in operatorCandidates)
            {
                // We only want to override slots for operators excluding
                // comparison operators, which are handled by ClassBase.tp_richcompare.
                if (!OpMethodMap.ContainsKey(method.Name))
                {
                    continue;
                }
                int offset = OpMethodMap[method.Name].TypeOffset;
                // Copy the default implementation of e.g. the nb_add slot,
                // which simply calls __add__ on the type.
                IntPtr func = Util.ReadIntPtr(_opType!, offset);
                // Write the slot definition of the target Python type, so
                // that we can later modify __add___ and it will be called
                // when used with a Python operator.
                // https://tenthousandmeters.com/blog/python-behind-the-scenes-6-how-python-object-system-works/
                Util.WriteIntPtr(pyType, offset, func);
            }
        }

        /// <summary>
        /// PyType_Ready adds a slot wrapper for the forward and the reflected method of every number slot
        /// <see cref="FixupSlots"/> set (__add__ and __radd__ for nb_add) the type does not define. With no CLR
        /// overload behind them they call the very slot that dispatched to them, so an operation the type does
        /// not support, like 1.0 + x when x only declares x + x, recursed until RecursionError. Removing them
        /// lets the slot return NotImplemented, so Python raises its usual TypeError.
        /// </summary>
        public static void RemoveUnboundSlotWrappers(BorrowedReference pyType, Type clrType)
        {
            // best effort: a wrapper left behind only keeps the previous behavior, so nothing here fails the type creation
            try
            {
                var dict = Util.ReadRef(pyType, TypeOffset.tp_dict);
                var fixedSlots = GetOperatorCandidates(clrType)
                    .Where(method => OpMethodMap.ContainsKey(method.Name))
                    .Select(method => OpMethodMap[method.Name].MethodName)
                    .Distinct();
                foreach (var pyName in fixedSlots)
                {
                    RemoveSlotWrapper(dict, pyName);
                    RemoveSlotWrapper(dict, ReversePyMethodName(pyName));
                }
            }
            catch
            {
                // pass
            }
        }

        private static void RemoveSlotWrapper(BorrowedReference dict, string name)
        {
            var member = Runtime.PyDict_GetItemString(dict, name);
            // only the slot wrappers PyType_Ready added, the CLR operators are managed method objects
            if (!member.IsNull && Runtime.PyObject_TYPE(member) == Runtime.PyWrapperDescriptorType
                && Runtime.PyDict_DelItemString(dict, name) != 0)
            {
                // don't let a failed delete leak, the wrapper just stays
                Exceptions.Clear();
            }
        }

        static IEnumerable<MethodInfo> GetOperatorCandidates(Type clrType)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            if (clrType.IsEnum)
            {
                return typeof(EnumOps<>).MakeGenericType(clrType).GetMethods(flags)
                    .Concat(typeof(FlagEnumOps<>).MakeGenericType(clrType).GetMethods(flags));
            }

            if (ImplicitNumericOps.IsSupported(clrType))
            {
                return clrType.GetMethods(flags).Concat(ImplicitNumericOps.GetMethods(clrType));
            }

            return clrType.GetMethods(flags);
        }

        public static string GetPyMethodName(string clrName)
        {
            if (OpMethodMap.ContainsKey(clrName))
            {
                return OpMethodMap[clrName].MethodName;
            } else
            {
                return ComparisonOpMap[clrName];
            }
        }

        private static string GenerateDummyCode()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("class OperatorMethod(object):");
            foreach (var item in OpMethodMap.Values)
            {
                string def = string.Format("    def {0}(self, other): pass", item.MethodName);
                sb.AppendLine(def);
            }
            return sb.ToString();
        }

        private static PyObject GetOperatorType()
        {
            using (PyDict locals = new PyDict())
            {
                // A hack way for getting typeobject.c::slotdefs
                string code = GenerateDummyCode();
                // The resulting OperatorMethod class is stored in a PyDict.
                PythonEngine.Exec(code, null, locals);
                // Return the class itself, which is a type.
                return locals.GetItem("OperatorMethod");
            }
        }

        public static string ReversePyMethodName(string pyName)
        {
            return pyName.Insert(2, "r");
        }

        /// <summary>
        /// Check if the method is performing a reverse operation.
        /// </summary>
        /// <param name="method">The operator method.</param>
        /// <returns></returns>
        public static bool IsReverse(MethodBase method)
        {
            Type primaryType = method.IsOpsHelper()
                ? method.DeclaringType.GetGenericArguments()[0]
                : method.DeclaringType;
            Type leftOperandType = method.GetParameters()[0].ParameterType;
            return leftOperandType != primaryType;
        }

        public static void FilterMethods(List<MethodInformation> methods, out List<MethodInformation> forwardMethods, out List<MethodInformation> reverseMethods)
        {
            forwardMethods = new List<MethodInformation>();
            reverseMethods = new List<MethodInformation>();
            foreach (var method in methods)
            {
                if (IsReverse(method.MethodBase))
                {
                    reverseMethods.Add(method);
                } else
                {
                    forwardMethods.Add(method);
                }
            }
        }
    }
}
