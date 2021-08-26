using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Python.Runtime
{
    /// <summary>
    /// A MethodBinder encapsulates information about a (possibly overloaded)
    /// managed method, and is responsible for selecting the right method given
    /// a set of Python arguments. This is also used as a base class for the
    /// ConstructorBinder, a minor variation used to invoke constructors.
    /// </summary>
    [Serializable]
    internal class MethodBinder
    {
        private List<MethodInformation> list;
        public const bool DefaultAllowThreads = true;
        public bool allow_threads = DefaultAllowThreads;
        public bool init = false;

        internal MethodBinder()
        {
            list = new List<MethodInformation>();
        }

        internal MethodBinder(MethodInfo mi)
        {
            list = new List<MethodInformation> { new MethodInformation(mi, mi.GetParameters()) };
        }

        public int Count
        {
            get { return list.Count; }
        }

        internal void AddMethod(MethodBase m)
        {
            // we added a new method so we have to re sort the method list
            init = false;
            list.Add(new MethodInformation(m, m.GetParameters()));
        }

        /// <summary>
        /// Given a sequence of MethodInfo and a sequence of types, return the
        /// MethodInfo that matches the signature represented by those types.
        /// </summary>
        internal static MethodInfo MatchSignature(MethodInfo[] mi, Type[] tp)
        {
            if (tp == null)
            {
                return null;
            }
            int count = tp.Length;
            for (var i = 0; i < mi.Length; i++)
            {
                var t = mi[i];
                ParameterInfo[] pi = t.GetParameters();
                if (pi.Length != count)
                {
                    continue;
                }
                for (var n = 0; n < pi.Length; n++)
                {
                    if (tp[n] != pi[n].ParameterType)
                    {
                        break;
                    }
                    if (n == pi.Length - 1)
                    {
                        return t;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Given a sequence of MethodInfo and a sequence of type parameters,
        /// return the MethodInfo that represents the matching closed generic.
        /// </summary>
        internal static MethodInfo MatchParameters(MethodInfo[] mi, Type[] tp)
        {
            if (tp == null)
            {
                return null;
            }
            int count = tp.Length;
            for (var i = 0; i < mi.Length; i++)
            {
                var t = mi[i];
                if (!t.IsGenericMethodDefinition)
                {
                    continue;
                }
                Type[] args = t.GetGenericArguments();
                if (args.Length != count)
                {
                    continue;
                }
                try
                {
                    // MakeGenericMethod can throw ArgumentException if the type parameters do not obey the constraints.
                    MethodInfo method = t.MakeGenericMethod(tp);
                    Exceptions.Clear();
                    return method;
                }
                catch (ArgumentException e)
                {
                    Exceptions.SetError(e);
                    // The error will remain set until cleared by a successful match.
                }
            }
            return null;
        }


        /// <summary>
        /// Given a sequence of MethodInfo and two sequences of type parameters,
        /// return the MethodInfo that matches the signature and the closed generic.
        /// </summary>
        internal static MethodInfo MatchSignatureAndParameters(MethodInfo[] mi, Type[] genericTp, Type[] sigTp)
        {
            if (genericTp == null || sigTp == null)
            {
                return null;
            }
            int genericCount = genericTp.Length;
            int signatureCount = sigTp.Length;
            for (var i = 0; i < mi.Length; i++)
            {
                var t = mi[i];
                if (!t.IsGenericMethodDefinition)
                {
                    continue;
                }
                Type[] genericArgs = t.GetGenericArguments();
                if (genericArgs.Length != genericCount)
                {
                    continue;
                }
                ParameterInfo[] pi = t.GetParameters();
                if (pi.Length != signatureCount)
                {
                    continue;
                }
                for (var n = 0; n < pi.Length; n++)
                {
                    if (sigTp[n] != pi[n].ParameterType)
                    {
                        break;
                    }
                    if (n == pi.Length - 1)
                    {
                        MethodInfo match = t;
                        if (match.IsGenericMethodDefinition)
                        {
                            // FIXME: typeArgs not used
                            Type[] typeArgs = match.GetGenericArguments();
                            return match.MakeGenericMethod(genericTp);
                        }
                        return match;
                    }
                }
            }
            return null;
        }


        /// <summary>
        /// Return the array of MethodInfo for this method. The result array
        /// is arranged in order of precedence (done lazily to avoid doing it
        /// at all for methods that are never called).
        /// </summary>
        internal List<MethodInformation> GetMethods()
        {
            if (!init)
            {
                // I'm sure this could be made more efficient.
                list.Sort(new MethodSorter());
                init = true;
            }
            return list;
        }

        /// <summary>
        /// Precedence algorithm largely lifted from Jython - the concerns are
        /// generally the same so we'll start with this and tweak as necessary.
        /// </summary>
        /// <remarks>
        /// Based from Jython `org.python.core.ReflectedArgs.precedence`
        /// See: https://github.com/jythontools/jython/blob/master/src/org/python/core/ReflectedArgs.java#L192
        /// </remarks>
        private static int GetPrecedence(MethodInformation methodInformation)
        {
            ParameterInfo[] pi = methodInformation.ParameterInfo;
            var mi = methodInformation.MethodBase;
            int val = mi.IsStatic ? 3000 : 0;
            int num = pi.Length;

            val += mi.IsGenericMethod ? 1 : 0;
            for (var i = 0; i < num; i++)
            {
                val += ArgPrecedence(pi[i].ParameterType, methodInformation);
            }

            var info = mi as MethodInfo;
            if (info != null)
            {
                val += ArgPrecedence(info.ReturnType, methodInformation);
                val += mi.DeclaringType == mi.ReflectedType ? 0 : 3000;
            }

            return val;
        }

        /// <summary>
        /// Return a precedence value for a particular Type object.
        /// </summary>
        internal static int ArgPrecedence(Type t, MethodInformation mi)
        {
            Type objectType = typeof(object);
            if (t == objectType)
            {
                return 3000;
            }

            if (t.IsAssignableFrom(typeof(PyObject)) && !OperatorMethod.IsOperatorMethod(mi.MethodBase))
            {
                return -1;
            }

            TypeCode tc = Type.GetTypeCode(t);
            // TODO: Clean up
            switch (tc)
            {
                case TypeCode.Object:
                    return 1;

                // we place higher precision methods at the top
                case TypeCode.Decimal:
                    return 2;
                case TypeCode.Double:
                    return 3;
                case TypeCode.Single:
                    return 4;

                case TypeCode.Int64:
                    return 21;
                case TypeCode.Int32:
                    return 22;
                case TypeCode.Int16:
                    return 23;
                case TypeCode.UInt64:
                    return 24;
                case TypeCode.UInt32:
                    return 25;
                case TypeCode.UInt16:
                    return 26;
                case TypeCode.Char:
                    return 27;
                case TypeCode.Byte:
                    return 28;
                case TypeCode.SByte:
                    return 29;

                case TypeCode.String:
                    return 30;

                case TypeCode.Boolean:
                    return 40;
            }

            if (t.IsArray)
            {
                Type e = t.GetElementType();
                if (e == objectType)
                {
                    return 2500;
                }
                return 100 + ArgPrecedence(e, mi);
            }

            return 2000;
        }

        /// <summary>
        /// Bind the given Python instance and arguments to a particular method
        /// overload and return a structure that contains the converted Python
        /// instance, converted arguments and the correct method to call.
        /// </summary>
        internal Binding Bind(IntPtr inst, IntPtr args, IntPtr kw)
        {
            return Bind(inst, args, kw, null, null);
        }

        internal Binding Bind(IntPtr inst, IntPtr args, IntPtr kw, MethodBase info)
        {
            return Bind(inst, args, kw, info, null);
        }

        internal Binding Bind(IntPtr inst, IntPtr args, IntPtr kw, MethodBase info, MethodInfo[] methodinfo)
        {
            // Relevant function variables used post conversion
            var isGeneric = false;
            Binding bindingUsingImplicitConversion = null;

            // If we have KWArgs create dictionary and collect them
            Dictionary<string, IntPtr> kwArgDict = null;
            if (kw != IntPtr.Zero)
            {
                var pyKwArgsCount = (int)Runtime.PyDict_Size(kw);
                kwArgDict = new Dictionary<string, IntPtr>(pyKwArgsCount);
                IntPtr keylist = Runtime.PyDict_Keys(kw);
                IntPtr valueList = Runtime.PyDict_Values(kw);
                for (int i = 0; i < pyKwArgsCount; ++i)
                {
                    var keyStr = Runtime.GetManagedString(Runtime.PyList_GetItem(new BorrowedReference(keylist), i));
                    kwArgDict[keyStr] = Runtime.PyList_GetItem(new BorrowedReference(valueList), i).DangerousGetAddress();
                }
                Runtime.XDecref(keylist);
                Runtime.XDecref(valueList);
            }

            // Fetch our methods we are going to attempt to match and bind too.
            var methods = info == null ? GetMethods()
                : new List<MethodInformation>(1) { new MethodInformation(info, info.GetParameters()) };

            for (var i = 0; i < methods.Count; i++)
            {
                var methodInformation = methods[i];
                // Relevant method variables
                var mi = methodInformation.MethodBase;
                var pi = methodInformation.ParameterInfo;

                isGeneric = mi.IsGenericMethod;
                int pyArgCount = (int)Runtime.PyTuple_Size(args);


                // Special case for operators
                bool isOperator = OperatorMethod.IsOperatorMethod(mi);
                // Binary operator methods will have 2 CLR args but only one Python arg
                // (unary operators will have 1 less each), since Python operator methods are bound.
                isOperator = isOperator && pyArgCount == pi.Length - 1;
                bool isReverse = isOperator && OperatorMethod.IsReverse((MethodInfo)mi);  // Only cast if isOperator.
                if (isReverse && OperatorMethod.IsComparisonOp((MethodInfo)mi))
                    continue;  // Comparison operators in Python have no reverse mode.
                // Preprocessing pi to remove either the first or second argument.
                if (isOperator && !isReverse)
                {
                    // The first Python arg is the right operand, while the bound instance is the left.
                    // We need to skip the first (left operand) CLR argument.
                    pi = pi.Skip(1).ToArray();
                }
                else if (isOperator && isReverse)
                {
                    // The first Python arg is the left operand.
                    // We need to take the first CLR argument.
                    pi = pi.Take(1).ToArray();
                }

                // Must be done after IsOperator section
                int clrArgCount = pi.Length;

                if (CheckMethodArgumentsMatch(clrArgCount,
                    pyArgCount,
                    kwArgDict,
                    pi,
                    out bool paramsArray,
                    out ArrayList defaultArgList))
                {
                    var outs = 0;
                    var margs = new object[clrArgCount];

                    int paramsArrayIndex = paramsArray ? pi.Length - 1 : -1; // -1 indicates no paramsArray
                    var usedImplicitConversion = false;

                    // Conversion loop for each parameter
                    for (int paramIndex = 0; paramIndex < clrArgCount; paramIndex++)
                    {
                        IntPtr op = IntPtr.Zero;            // Python object to be converted; not yet set
                        var parameter = pi[paramIndex];     // Clr parameter we are targeting
                        object arg;                         // Python -> Clr argument

                        // Check our KWargs for this parameter
                        bool hasNamedParam = kwArgDict == null ? false : kwArgDict.TryGetValue(parameter.Name, out op);
                        bool isNewReference = false;

                        // Check if we are going to use default
                        if (paramIndex >= pyArgCount && !(hasNamedParam || (paramsArray && paramIndex == paramsArrayIndex)))
                        {
                            if (defaultArgList != null)
                            {
                                margs[paramIndex] = defaultArgList[paramIndex - pyArgCount];
                            }

                            continue;
                        }

                        // At this point, if op is IntPtr.Zero we don't have a KWArg and are not using default
                        if (op == IntPtr.Zero)
                        {
                            // If we have reached the paramIndex
                            if (paramsArrayIndex == paramIndex)
                            {
                                op = HandleParamsArray(args, paramsArrayIndex, pyArgCount, out isNewReference);
                            }
                            else
                            {
                                op = Runtime.PyTuple_GetItem(args, paramIndex);
                            }
                        }

                        // this logic below handles cases when multiple overloading methods
                        // are ambiguous, hence comparison between Python and CLR types
                        // is necessary
                        Type clrtype = null;
                        IntPtr pyoptype;
                        if (methods.Count > 1)
                        {
                            pyoptype = IntPtr.Zero;
                            pyoptype = Runtime.PyObject_Type(op);
                            Exceptions.Clear();
                            if (pyoptype != IntPtr.Zero)
                            {
                                clrtype = Converter.GetTypeByAlias(pyoptype);
                            }
                            Runtime.XDecref(pyoptype);
                        }


                        if (clrtype != null)
                        {
                            var typematch = false;

                            if ((parameter.ParameterType != typeof(object)) && (parameter.ParameterType != clrtype))
                            {
                                IntPtr pytype = Converter.GetPythonTypeByAlias(parameter.ParameterType);
                                pyoptype = Runtime.PyObject_Type(op);
                                Exceptions.Clear();
                                if (pyoptype != IntPtr.Zero)
                                {
                                    if (pytype != pyoptype)
                                    {
                                        typematch = false;
                                    }
                                    else
                                    {
                                        typematch = true;
                                        clrtype = parameter.ParameterType;
                                    }
                                }
                                if (!typematch)
                                {
                                    // this takes care of nullables
                                    var underlyingType = Nullable.GetUnderlyingType(parameter.ParameterType);
                                    if (underlyingType == null)
                                    {
                                        underlyingType = parameter.ParameterType;
                                    }
                                    // this takes care of enum values
                                    TypeCode argtypecode = Type.GetTypeCode(underlyingType);
                                    TypeCode paramtypecode = Type.GetTypeCode(clrtype);
                                    if (argtypecode == paramtypecode)
                                    {
                                        typematch = true;
                                        clrtype = parameter.ParameterType;
                                    }
                                    // lets just keep the first binding using implicit conversion
                                    // this is to respect method order/precedence
                                    else if (bindingUsingImplicitConversion == null)
                                    {
                                        // accepts non-decimal numbers in decimal parameters
                                        if (underlyingType == typeof(decimal))
                                        {
                                            clrtype = parameter.ParameterType;
                                            usedImplicitConversion |= typematch = Converter.ToManaged(op, clrtype, out arg, false);
                                        }
                                        if (!typematch)
                                        {
                                            // this takes care of implicit conversions
                                            var opImplicit = parameter.ParameterType.GetMethod("op_Implicit", new[] { clrtype });
                                            if (opImplicit != null)
                                            {
                                                usedImplicitConversion |= typematch = opImplicit.ReturnType == parameter.ParameterType;
                                                clrtype = parameter.ParameterType;
                                            }
                                        }
                                    }
                                }
                                Runtime.XDecref(pyoptype);
                                if (!typematch)
                                {
                                    margs = null;
                                    break;
                                }
                            }
                            else
                            {
                                clrtype = parameter.ParameterType;
                            }
                        }
                        else
                        {
                            clrtype = parameter.ParameterType;
                        }

                        if (parameter.IsOut || clrtype.IsByRef)
                        {
                            outs++;
                        }

                        if (!Converter.ToManaged(op, clrtype, out arg, false))
                        {
                            margs = null;
                            break;
                        }

                        if (isNewReference)
                        {
                            // TODO: is this a bug? Should this happen even if the conversion fails?
                            // GetSlice() creates a new reference but GetItem()
                            // returns only a borrow reference.
                            Runtime.XDecref(op);
                        }

                        margs[paramIndex] = arg;

                    }

                    if (margs == null)
                    {
                        continue;
                    }

                    if (isOperator)
                    {
                        if (inst != IntPtr.Zero)
                        {
                            if (ManagedType.GetManagedObject(inst) is CLRObject co)
                            {
                                bool isUnary = pyArgCount == 0;
                                // Postprocessing to extend margs.
                                var margsTemp = isUnary ? new object[1] : new object[2];
                                // If reverse, the bound instance is the right operand.
                                int boundOperandIndex = isReverse ? 1 : 0;
                                // If reverse, the passed instance is the left operand.
                                int passedOperandIndex = isReverse ? 0 : 1;
                                margsTemp[boundOperandIndex] = co.inst;
                                if (!isUnary)
                                {
                                    margsTemp[passedOperandIndex] = margs[0];
                                }
                                margs = margsTemp;
                            }
                            else continue;
                        }
                    }

                    object target = null;
                    if (!mi.IsStatic && inst != IntPtr.Zero)
                    {
                        //CLRObject co = (CLRObject)ManagedType.GetManagedObject(inst);
                        // InvalidCastException: Unable to cast object of type
                        // 'Python.Runtime.ClassObject' to type 'Python.Runtime.CLRObject'
                        var co = ManagedType.GetManagedObject(inst) as CLRObject;

                        // Sanity check: this ensures a graceful exit if someone does
                        // something intentionally wrong like call a non-static method
                        // on the class rather than on an instance of the class.
                        // XXX maybe better to do this before all the other rigmarole.
                        if (co == null)
                        {
                            return null;
                        }
                        target = co.inst;
                    }

                    var binding = new Binding(mi, target, margs, outs);
                    if (usedImplicitConversion)
                    {
                        // in this case we will not return the binding yet in case there is a match
                        // which does not use implicit conversions, which will return directly
                        bindingUsingImplicitConversion = binding;
                    }
                    else
                    {
                        return binding;
                    }
                }
            }

            // if we generated a binding using implicit conversion return it
            if (bindingUsingImplicitConversion != null)
            {
                return bindingUsingImplicitConversion;
            }

            // We weren't able to find a matching method but at least one
            // is a generic method and info is null. That happens when a generic
            // method was not called using the [] syntax. Let's introspect the
            // type of the arguments and use it to construct the correct method.
            if (isGeneric && info == null && methodinfo != null)
            {
                Type[] types = Runtime.PythonArgsToTypeArray(args, true);
                MethodInfo mi = MatchParameters(methodinfo, types);
                return Bind(inst, args, kw, mi, null);
            }
            return null;
        }

        static IntPtr HandleParamsArray(IntPtr args, int arrayStart, int pyArgCount, out bool isNewReference)
        {
            isNewReference = false;
            IntPtr op;
            // for a params method, we may have a sequence or single/multiple items
            // here we look to see if the item at the paramIndex is there or not
            // and then if it is a sequence itself.
            if ((pyArgCount - arrayStart) == 1)
            {
                // Ee only have one argument left, so we need to check to see if it is a sequence or a single item
                // We also need to check if this object is possibly a wrapped C# Enumerable Object
                IntPtr item = Runtime.PyTuple_GetItem(args, arrayStart);
                if (!Runtime.PyString_Check(item) && (Runtime.PySequence_Check(item) || (ManagedType.GetManagedObject(item) as CLRObject)?.inst is IEnumerable))
                {
                    // it's a sequence (and not a string), so we use it as the op
                    op = item;
                }
                else
                {
                    // Its a single value that needs to be converted into the params array
                    isNewReference = true;
                    op = Runtime.PyTuple_GetSlice(args, arrayStart, pyArgCount);
                }
            }
            else
            {
                // Its a set of individual values, so we grab them as a tuple to be converted into the params array
                isNewReference = true;
                op = Runtime.PyTuple_GetSlice(args, arrayStart, pyArgCount);
            }
            return op;
        }

        /// <summary>
        /// This helper method will perform an initial check to determine if we found a matching
        /// method based on its parameters count and type <see cref="Bind(IntPtr,IntPtr,IntPtr,MethodBase,MethodInfo[])"/>
        /// </summary>
        private bool CheckMethodArgumentsMatch(int clrArgCount,
            int pyArgCount,
            Dictionary<string, IntPtr> kwargDict,
            ParameterInfo[] parameterInfo,
            out bool paramsArray,
            out ArrayList defaultArgList)
        {
            var match = false;

            // Prepare our outputs
            defaultArgList = null;
            paramsArray = parameterInfo.Length > 0 && Attribute.IsDefined(parameterInfo[parameterInfo.Length - 1], typeof(ParamArrayAttribute));

            // First if we have anys kwargs, look at the function for matching args
            if (kwargDict != null && kwargDict.Count > 0)
            {
                // If the method doesn't have all of these kw args, it is not a match
                // Otherwise just continue on to see if it is a match
                if (!kwargDict.All(x => parameterInfo.Any(pi => x.Key == pi.Name)))
                {
                    return false;
                }
            }

            // If they have the exact same amount of args they do match
            // Must check kwargs because it contains additional args
            if (pyArgCount == clrArgCount && (kwargDict == null || kwargDict.Count == 0))
            {
                match = true;
            }
            else if (pyArgCount < clrArgCount)
            {
                // every parameter past 'pyArgCount' must have either
                // a corresponding keyword argument or a default parameter
                match = true;
                defaultArgList = new ArrayList();
                for (var v = pyArgCount; v < clrArgCount && match; v++)
                {
                    if (kwargDict != null && kwargDict.ContainsKey(parameterInfo[v].Name))
                    {
                        // we have a keyword argument for this parameter,
                        // no need to check for a default parameter, but put a null
                        // placeholder in defaultArgList
                        defaultArgList.Add(null);
                    }
                    else if (parameterInfo[v].IsOptional)
                    {
                        // IsOptional will be true if the parameter has a default value,
                        // or if the parameter has the [Optional] attribute specified.
                        if (parameterInfo[v].HasDefaultValue)
                        {
                            defaultArgList.Add(parameterInfo[v].DefaultValue);
                        }
                        else
                        {
                            // [OptionalAttribute] was specified for the parameter.
                            // See https://stackoverflow.com/questions/3416216/optionalattribute-parameters-default-value
                            // for rules on determining the value to pass to the parameter
                            var type = parameterInfo[v].ParameterType;
                            if (type == typeof(object))
                                defaultArgList.Add(Type.Missing);
                            else if (type.IsValueType)
                                defaultArgList.Add(Activator.CreateInstance(type));
                            else
                                defaultArgList.Add(null);
                        }
                    }
                    else if (!paramsArray)
                    {
                        // If there is no KWArg or Default value, then this isn't a match
                        match = false;
                    }
                }
            }
            else if (pyArgCount > clrArgCount && clrArgCount > 0 && paramsArray)
            {
                // This is a `foo(params object[] bar)` style method
                // We will handle the params later
                match = true;
            }
            return match;
        }

        internal virtual IntPtr Invoke(IntPtr inst, IntPtr args, IntPtr kw)
        {
            return Invoke(inst, args, kw, null, null);
        }

        internal virtual IntPtr Invoke(IntPtr inst, IntPtr args, IntPtr kw, MethodBase info)
        {
            return Invoke(inst, args, kw, info, null);
        }

        internal virtual IntPtr Invoke(IntPtr inst, IntPtr args, IntPtr kw, MethodBase info, MethodInfo[] methodinfo)
        {
            Binding binding = Bind(inst, args, kw, info, methodinfo);
            object result;
            IntPtr ts = IntPtr.Zero;

            if (binding == null)
            {
                // If we already have an exception pending, don't create a new one
                if(!Exceptions.ErrorOccurred()){
                    var value = new StringBuilder("No method matches given arguments");
                    if (methodinfo != null && methodinfo.Length > 0)
                    {
                        value.Append($" for {methodinfo[0].Name}");
                    }
                    else if (list.Count > 0)
                    {
                        value.Append($" for {list[0].MethodBase.Name}");
                    }

                    value.Append(": ");
                    AppendArgumentTypes(to: value, args);
                    Exceptions.RaiseTypeError(value.ToString());
                }

                return IntPtr.Zero;
            }

            if (allow_threads)
            {
                ts = PythonEngine.BeginAllowThreads();
            }

            try
            {
                result = binding.info.Invoke(binding.inst, BindingFlags.Default, null, binding.args, null);
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                if (allow_threads)
                {
                    PythonEngine.EndAllowThreads(ts);
                }
                Exceptions.SetError(e);
                return IntPtr.Zero;
            }

            if (allow_threads)
            {
                PythonEngine.EndAllowThreads(ts);
            }

            // If there are out parameters, we return a tuple containing
            // the result followed by the out parameters. If there is only
            // one out parameter and the return type of the method is void,
            // we return the out parameter as the result to Python (for
            // code compatibility with ironpython).

            var mi = (MethodInfo)binding.info;

            if (binding.outs > 0)
            {
                ParameterInfo[] pi = mi.GetParameters();
                int c = pi.Length;
                var n = 0;

                IntPtr t = Runtime.PyTuple_New(binding.outs + 1);
                IntPtr v = Converter.ToPython(result, mi.ReturnType);
                Runtime.PyTuple_SetItem(t, n, v);
                n++;

                for (var i = 0; i < c; i++)
                {
                    Type pt = pi[i].ParameterType;
                    if (pi[i].IsOut || pt.IsByRef)
                    {
                        v = Converter.ToPython(binding.args[i], pt);
                        Runtime.PyTuple_SetItem(t, n, v);
                        n++;
                    }
                }

                if (binding.outs == 1 && mi.ReturnType == typeof(void))
                {
                    v = Runtime.PyTuple_GetItem(t, 1);
                    Runtime.XIncref(v);
                    Runtime.XDecref(t);
                    return v;
                }

                return t;
            }

            return Converter.ToPython(result, mi.ReturnType);
        }

        /// <summary>
        /// Utility class to store the information about a <see cref="MethodBase"/>
        /// </summary>
        [Serializable]
        internal class MethodInformation
        {
            public MethodBase MethodBase { get; }

            public ParameterInfo[] ParameterInfo { get; }

            public MethodInformation(MethodBase methodBase, ParameterInfo[] parameterInfo)
            {
                MethodBase = methodBase;
                ParameterInfo = parameterInfo;
            }

            public override string ToString()
            {
                return MethodBase.ToString();
            }
        }

        /// <summary>
        /// Utility class to sort method info by parameter type precedence.
        /// </summary>
        private class MethodSorter : IComparer<MethodInformation>
        {
            public int Compare(MethodInformation x, MethodInformation y)
            {
                int p1 = GetPrecedence(x);
                int p2 = GetPrecedence(y);
                if (p1 < p2)
                {
                    return -1;
                }
                if (p1 > p2)
                {
                    return 1;
                }
                return 0;
            }
        }
        protected static void AppendArgumentTypes(StringBuilder to, IntPtr args)
        {
            long argCount = Runtime.PyTuple_Size(args);
            to.Append("(");
            for (long argIndex = 0; argIndex < argCount; argIndex++)
            {
                var arg = Runtime.PyTuple_GetItem(args, argIndex);
                if (arg != IntPtr.Zero)
                {
                    var type = Runtime.PyObject_Type(arg);
                    if (type != IntPtr.Zero)
                    {
                        try
                        {
                            var description = Runtime.PyObject_Unicode(type);
                            if (description != IntPtr.Zero)
                            {
                                to.Append(Runtime.GetManagedString(description));
                                Runtime.XDecref(description);
                            }
                        }
                        finally
                        {
                            Runtime.XDecref(type);
                        }
                    }
                }

                if (argIndex + 1 < argCount)
                    to.Append(", ");
            }
            to.Append(')');
        }
    }


    /// <summary>
    /// A Binding is a utility instance that bundles together a MethodInfo
    /// representing a method to call, a (possibly null) target instance for
    /// the call, and the arguments for the call (all as managed values).
    /// </summary>
    internal class Binding
    {
        public MethodBase info;
        public object[] args;
        public object inst;
        public int outs;

        internal Binding(MethodBase info, object inst, object[] args, int outs)
        {
            this.info = info;
            this.inst = inst;
            this.args = args;
            this.outs = outs;
        }
    }
}
