using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

using Python.Runtime.Slots;

namespace Python.Runtime
{
    /// <summary>
    /// Base class for Python types that reflect managed types / classes.
    /// Concrete subclasses include ClassObject and DelegateObject. This
    /// class provides common attributes and common machinery for doing
    /// class initialization (initialization of the class __dict__). The
    /// concrete subclasses provide slot implementations appropriate for
    /// each variety of reflected type.
    /// </summary>
    [Serializable]
    internal class ClassBase : ManagedType, IDeserializationCallback
    {
        [NonSerialized]
        internal List<string> dotNetMembers = new();
        internal Indexer? indexer;
        internal readonly Dictionary<int, MethodObject> richcompare = new();
        internal MaybeType type;

        internal ClassBase(Type tp)
        {
            if (tp is null) throw new ArgumentNullException(nameof(type));

            indexer = null;
            type = tp;
        }

        internal virtual bool CanSubclass()
        {
            return !type.Value.IsEnum;
        }

        public readonly static Dictionary<string, int> CilToPyOpMap = new Dictionary<string, int>
        {
            ["op_Equality"] = Runtime.Py_EQ,
            ["op_Inequality"] = Runtime.Py_NE,
            ["op_LessThanOrEqual"] = Runtime.Py_LE,
            ["op_GreaterThanOrEqual"] = Runtime.Py_GE,
            ["op_LessThan"] = Runtime.Py_LT,
            ["op_GreaterThan"] = Runtime.Py_GT,
        };

        /// <summary>
        /// Default implementation of [] semantics for reflected types.
        /// </summary>
        public virtual NewReference type_subscript(BorrowedReference idx)
        {
            Type[]? types = Runtime.PythonArgsToTypeArray(idx);
            if (types == null)
            {
                return Exceptions.RaiseTypeError("type(s) expected");
            }

            if (!type.Valid)
            {
                return Exceptions.RaiseTypeError(type.DeletedMessage);
            }

            Type? target = GenericUtil.GenericForType(type.Value, types.Length);

            if (target != null)
            {
                Type t;
                try
                {
                    // MakeGenericType can throw ArgumentException
                    t = target.MakeGenericType(types);
                }
                catch (ArgumentException e)
                {
                    return Exceptions.RaiseTypeError(e.Message);
                }
                var c = ClassManager.GetClass(t);
                return new NewReference(c);
            }

            return Exceptions.RaiseTypeError($"{type.Value.Namespace}.{type.Name} does not accept {types.Length} generic parameters");
        }

        /// <summary>
        /// Standard comparison implementation for instances of reflected types.
        /// </summary>
        public static NewReference tp_richcompare(BorrowedReference ob, BorrowedReference other, int op)
        {
            CLRObject co1;
            object co2Inst;
            BorrowedReference tp = Runtime.PyObject_TYPE(ob);
            var cls = (ClassBase)GetManagedObject(tp)!;
            // C# operator methods take precedence over IComparable.
            // We first check if there's a comparison operator by looking up the richcompare table,
            // otherwise fallback to checking if an IComparable interface is handled.
            if (cls.richcompare.TryGetValue(op, out var methodObject))
            {
                // Wrap the `other` argument of a binary comparison operator in a PyTuple.
                using var args = Runtime.PyTuple_New(1);
                Runtime.PyTuple_SetItem(args.Borrow(), 0, other);
                return methodObject.Invoke(ob, args.Borrow(), null);
            }

            switch (op)
            {
                case Runtime.Py_EQ:
                case Runtime.Py_NE:
                    BorrowedReference pytrue = Runtime.PyTrue;
                    BorrowedReference pyfalse = Runtime.PyFalse;

                    // swap true and false for NE
                    if (op != Runtime.Py_EQ)
                    {
                        pytrue = Runtime.PyFalse;
                        pyfalse = Runtime.PyTrue;
                    }

                    if (ob == other)
                    {
                        return new NewReference(pytrue);
                    }

                    if (!TryGetSecondCompareOperandInstance(ob, other, out co1, out co2Inst))
                    {
                        return new NewReference(pyfalse);
                    }

                    if (Equals(co1.inst, co2Inst))
                    {
                        return new NewReference(pytrue);
                    }

                    return new NewReference(pyfalse);
                case Runtime.Py_LT:
                case Runtime.Py_LE:
                case Runtime.Py_GT:
                case Runtime.Py_GE:
                    if (!TryGetSecondCompareOperandInstance(ob, other, out co1, out co2Inst))
                    {
                        return Exceptions.RaiseTypeError("Cannot get managed object");
                    }

                    var co1Comp = co1.inst as IComparable;
                    if (co1Comp == null)
                    {
                        Type co1Type = co1.GetType();
                        return Exceptions.RaiseTypeError($"Cannot convert object of type {co1Type} to IComparable");
                    }
                    try
                    {
                        int cmp = co1Comp.CompareTo(co2Inst);
                        return new NewReference(GetComparisonResult(op, cmp));
                    }
                    catch (ArgumentException e)
                    {
                        return Exceptions.RaiseTypeError(e.Message);
                    }
                default:
                    return new NewReference(Runtime.PyNotImplemented);
            }
        }

        /// <summary>
        /// Get the result of a comparison operation based on the operator and the comparison result.
        /// </summary>
        /// <remarks>
        /// This method is used to determine the result of a comparison operation, excluding equality and inequality.
        /// </remarks>
        protected static BorrowedReference GetComparisonResult(int op, int comparisonResult)
        {
            BorrowedReference pyCmp;
            if (comparisonResult < 0)
            {
                if (op == Runtime.Py_LT || op == Runtime.Py_LE)
                {
                    pyCmp = Runtime.PyTrue;
                }
                else
                {
                    pyCmp = Runtime.PyFalse;
                }
            }
            else if (comparisonResult == 0)
            {
                if (op == Runtime.Py_LE || op == Runtime.Py_GE)
                {
                    pyCmp = Runtime.PyTrue;
                }
                else
                {
                    pyCmp = Runtime.PyFalse;
                }
            }
            else
            {
                if (op == Runtime.Py_GE || op == Runtime.Py_GT)
                {
                    pyCmp = Runtime.PyTrue;
                }
                else
                {
                    pyCmp = Runtime.PyFalse;
                }
            }

            return pyCmp;
        }

        protected static bool TryGetSecondCompareOperandInstance(BorrowedReference left, BorrowedReference right, out CLRObject co1, out object co2Inst)
        {
            co2Inst = null;

            co1 = (CLRObject)GetManagedObject(left)!;
            if (co1 == null)
            {
                return false;
            }

            var co2 = GetManagedObject(right) as CLRObject;

            // The object comparing against is not a managed object. It could still be a Python object
            // that can be compared against (e.g. comparing against a Python string)
            if (co2 == null)
            {
                if (right != null)
                {
                    using var pyCo2 = new PyObject(right);
                    if (Converter.ToManagedValue(pyCo2, typeof(object), out var result, false))
                    {
                        co2Inst = result;
                        return true;
                    }
                }
                return false;
            }

            co2Inst = co2.inst;
            return true;
        }

        /// <summary>
        /// Standard iteration support for instances of reflected types. This
        /// allows natural iteration over objects that either are IEnumerable
        /// or themselves support IEnumerator directly.
        /// </summary>
        static NewReference tp_iter_impl(BorrowedReference ob)
        {
            var co = GetManagedObject(ob) as CLRObject;
            if (co == null)
            {
                return Exceptions.RaiseTypeError("invalid object");
            }

            var e = co.inst as IEnumerable;
            IEnumerator? o;
            if (e != null)
            {
                o = e.GetEnumerator();
            }
            else
            {
                o = co.inst as IEnumerator;

                if (o == null)
                {
                    return Exceptions.RaiseTypeError("iteration over non-sequence");
                }
            }

            var elemType = typeof(object);
            var iterType = co.inst.GetType();
            foreach(var ifc in iterType.GetInterfaces())
            {
                if (ifc.IsGenericType)
                {
                    var genTypeDef = ifc.GetGenericTypeDefinition();
                    if (genTypeDef == typeof(IEnumerable<>) || genTypeDef == typeof(IEnumerator<>))
                    {
                        elemType = ifc.GetGenericArguments()[0];
                        break;
                    }
                }
            }

            return new Iterator(o, elemType).Alloc();
        }


        /// <summary>
        /// Standard __hash__ implementation for instances of reflected types.
        /// </summary>
        public static nint tp_hash(BorrowedReference ob)
        {
            var co = GetManagedObject(ob) as CLRObject;
            if (co == null)
            {
                Exceptions.RaiseTypeError("unhashable type");
                return 0;
            }
            return co.inst.GetHashCode();
        }


        /// <summary>
        /// Standard __str__ implementation for instances of reflected types.
        /// </summary>
        public static NewReference tp_str(BorrowedReference ob)
        {
            var co = GetManagedObject(ob) as CLRObject;
            if (co == null)
            {
                return Exceptions.RaiseTypeError("invalid object");
            }
            try
            {
                return Runtime.PyString_FromString(co.inst.ToString());
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return default;
            }
        }

        public static NewReference tp_repr(BorrowedReference ob)
        {
            var co = GetManagedObject(ob) as CLRObject;
            if (co == null)
            {
                return Exceptions.RaiseTypeError("invalid object");
            }
            try
            {
                //if __repr__ is defined, use it
                var instType = co.inst.GetType();
                System.Reflection.MethodInfo methodInfo = instType.GetMethod("__repr__");
                if (methodInfo != null && methodInfo.IsPublic)
                {
                    var reprString = methodInfo.Invoke(co.inst, null) as string;
                    return reprString is null ? new NewReference(Runtime.PyNone) : Runtime.PyString_FromString(reprString);
                }

                //otherwise use the standard object.__repr__(inst)
                using var args = Runtime.PyTuple_New(1);
                Runtime.PyTuple_SetItem(args.Borrow(), 0, ob);
                using var reprFunc = Runtime.PyObject_GetAttr(Runtime.PyBaseObjectType, PyIdentifier.__repr__);
                return Runtime.PyObject_Call(reprFunc.Borrow(), args.Borrow(), null);
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return default;
            }
        }


        /// <summary>
        /// Standard dealloc implementation for instances of reflected types.
        /// </summary>
        public static void tp_dealloc(NewReference lastRef)
        {
            Runtime.PyObject_GC_UnTrack(lastRef.Borrow());

            CallClear(lastRef.Borrow());

            DecrefTypeAndFree(lastRef.Steal());
        }

        public static int tp_clear(BorrowedReference ob)
        {
            var weakrefs = Runtime.PyObject_GetWeakRefList(ob);
            if (weakrefs != null)
            {
                Runtime.PyObject_ClearWeakRefs(ob);
            }

            TryFreeGCHandle(ob);

            int baseClearResult = BaseUnmanagedClear(ob);
            if (baseClearResult != 0)
            {
                return baseClearResult;
            }

            ClearObjectDict(ob);
            return 0;
        }

        internal static unsafe int BaseUnmanagedClear(BorrowedReference ob)
        {
            var type = Runtime.PyObject_TYPE(ob);
            var unmanagedBase = GetUnmanagedBaseType(type);
            var clearPtr = Util.ReadIntPtr(unmanagedBase, TypeOffset.tp_clear);
            if (clearPtr == IntPtr.Zero)
            {
                return 0;
            }
            var clear = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)clearPtr;

            bool usesSubtypeClear = clearPtr == TypeManager.subtype_clear;
            if (usesSubtypeClear)
            {
                // workaround for https://bugs.python.org/issue45266 (subtype_clear)
                using var dict = Runtime.PyObject_GenericGetDict(ob);
                if (Runtime.PyMapping_HasKey(dict.Borrow(), PyIdentifier.__clear_reentry_guard__) != 0)
                    return 0;
                int res = Runtime.PyDict_SetItem(dict.Borrow(), PyIdentifier.__clear_reentry_guard__, Runtime.None);
                if (res != 0) return res;

                res = clear(ob);
                Runtime.PyDict_DelItem(dict.Borrow(), PyIdentifier.__clear_reentry_guard__);
                return res;
            }
            return clear(ob);
        }

        protected override Dictionary<string, object?> OnSave(BorrowedReference ob)
        {
            var context = base.OnSave(ob) ?? new();
            context["impl"] = this;
            return context;
        }

        protected override void OnLoad(BorrowedReference ob, Dictionary<string, object?>? context)
        {
            base.OnLoad(ob, context);
            var gcHandle = GCHandle.Alloc(this);
            SetGCHandle(ob, gcHandle);
        }


        /// <summary>
        /// Implements __getitem__ for reflected classes and value types.
        /// </summary>
        static NewReference mp_subscript_impl(BorrowedReference ob, BorrowedReference idx)
        {
            BorrowedReference tp = Runtime.PyObject_TYPE(ob);
            var cls = (ClassBase)GetManagedObject(tp)!;

            if (cls.indexer == null || !cls.indexer.CanGet)
            {
                Exceptions.SetError(Exceptions.TypeError, "unindexable object");
                return default;
            }

            // Arg may be a tuple in the case of an indexer with multiple
            // parameters. If so, use it directly, else make a new tuple
            // with the index arg (method binders expect arg tuples).
            if (!Runtime.PyTuple_Check(idx))
            {
                using var argTuple = Runtime.PyTuple_New(1);
                Runtime.PyTuple_SetItem(argTuple.Borrow(), 0, idx);
                return cls.indexer.GetItem(ob, argTuple.Borrow());
            }
            else
            {
                return cls.indexer.GetItem(ob, idx);
            }
        }


        /// <summary>
        /// Implements __setitem__ for reflected classes and value types.
        /// </summary>
        static int mp_ass_subscript_impl(BorrowedReference ob, BorrowedReference idx, BorrowedReference v)
        {
            BorrowedReference tp = Runtime.PyObject_TYPE(ob);
            var cls = (ClassBase)GetManagedObject(tp)!;

            if (cls.indexer == null || !cls.indexer.CanSet)
            {
                Exceptions.SetError(Exceptions.TypeError, "object doesn't support item assignment");
                return -1;
            }

            // Arg may be a tuple in the case of an indexer with multiple
            // parameters. If so, use it directly, else make a new tuple
            // with the index arg (method binders expect arg tuples).
            NewReference argsTuple = default;

            if (!Runtime.PyTuple_Check(idx))
            {
                argsTuple = Runtime.PyTuple_New(1);
                Runtime.PyTuple_SetItem(argsTuple.Borrow(), 0, idx);
                idx = argsTuple.Borrow();
            }

            // Get the args passed in.
            var i = Runtime.PyTuple_Size(idx);
            using var defaultArgs = cls.indexer.GetDefaultArgs(idx);
            var numOfDefaultArgs = Runtime.PyTuple_Size(defaultArgs.Borrow());
            var temp = i + numOfDefaultArgs;
            using var real = Runtime.PyTuple_New(temp + 1);
            for (var n = 0; n < i; n++)
            {
                BorrowedReference item = Runtime.PyTuple_GetItem(idx, n);
                Runtime.PyTuple_SetItem(real.Borrow(), n, item);
            }

            argsTuple.Dispose();

            // Add Default Args if needed
            for (var n = 0; n < numOfDefaultArgs; n++)
            {
                BorrowedReference item = Runtime.PyTuple_GetItem(defaultArgs.Borrow(), n);
                Runtime.PyTuple_SetItem(real.Borrow(), n + i, item);
            }
            i = temp;

            // Add value to argument list
            Runtime.PyTuple_SetItem(real.Borrow(), i, v);

            cls.indexer.SetItem(ob, real.Borrow());

            if (Exceptions.ErrorOccurred())
            {
                return -1;
            }

            return 0;
        }

        static NewReference tp_call_impl(BorrowedReference ob, BorrowedReference args, BorrowedReference kw)
        {
            BorrowedReference tp = Runtime.PyObject_TYPE(ob);
            var self = (ClassBase)GetManagedObject(tp)!;

            if (!self.type.Valid)
            {
                return Exceptions.RaiseTypeError(self.type.DeletedMessage);
            }

            Type type = self.type.Value;

            var calls = GetCallImplementations(type).ToList();
            Debug.Assert(calls.Count > 0);
            var callBinder = new MethodBinder();
            foreach (MethodInfo call in calls)
            {
                callBinder.AddMethod(call, true);
            }
            return callBinder.Invoke(ob, args, kw);
        }

        static IEnumerable<MethodInfo> GetCallImplementations(Type type)
            => type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "__call__");

        public virtual void InitializeSlots(BorrowedReference pyType, SlotsHolder slotsHolder)
        {
            if (!this.type.Valid) return;

            if (GetCallImplementations(this.type.Value).Any())
            {
                TypeManager.InitializeSlotIfEmpty(pyType, TypeOffset.tp_call, new Interop.BBB_N(tp_call_impl), slotsHolder);
            }

            if (indexer is not null)
            {
                if (indexer.CanGet)
                {
                    TypeManager.InitializeSlotIfEmpty(pyType, TypeOffset.mp_subscript, new Interop.BB_N(mp_subscript_impl), slotsHolder);
                }
                if (indexer.CanSet)
                {
                    TypeManager.InitializeSlotIfEmpty(pyType, TypeOffset.mp_ass_subscript, new Interop.BBB_I32(mp_ass_subscript_impl), slotsHolder);
                }
            }

            if (typeof(IEnumerable).IsAssignableFrom(type.Value)
                || typeof(IEnumerator).IsAssignableFrom(type.Value))
            {
                TypeManager.InitializeSlotIfEmpty(pyType, TypeOffset.tp_iter, new Interop.B_N(tp_iter_impl), slotsHolder);
            }

            if (MpLengthSlot.CanAssign(type.Value))
            {
                TypeManager.InitializeSlotIfEmpty(pyType, TypeOffset.mp_length, new Interop.B_P(MpLengthSlot.impl), slotsHolder);
            }
        }

        public virtual bool HasCustomNew() => this.GetType().GetMethod("tp_new") is not null;

        public override bool Init(BorrowedReference obj, BorrowedReference args, BorrowedReference kw)
        {
            if (this.HasCustomNew())
                // initialization must be done in tp_new
                return true;

            return base.Init(obj, args, kw);
        }

        protected virtual void OnDeserialization(object sender)
        {
            this.dotNetMembers = new List<string>();
        }

        void IDeserializationCallback.OnDeserialization(object sender) => this.OnDeserialization(sender);

        /// <summary>
        /// If an <c>AttributeError</c> is currently set as the result of a missing
        /// attribute lookup on a .NET object, rewrites its message to append a list
        /// of similarly-named members of the managed type (a "Did you mean ...?" hint).
        /// This is a no-op when there is no AttributeError set, when the object is not
        /// a CLR object, or when no similarly-named members exist. It only runs on the
        /// exceptional (miss) path, so the reflection cost is not on the hot path.
        /// </summary>
        internal static void AppendAttributeErrorSuggestions(BorrowedReference ob, BorrowedReference key)
        {
            if (!Exceptions.ExceptionMatches(Exceptions.AttributeError))
            {
                return;
            }

            var name = Runtime.GetManagedString(key);
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            var hint = GetSuggestionHint(ob, name);
            if (hint.Length == 0)
            {
                return;
            }

            // Keep the original AttributeError message and append our hint to it.
            Runtime.PyErr_Fetch(out var errType, out var errValue, out var errTraceback);
            try
            {
                var baseMessage = GetErrorMessage(errValue.BorrowNullable(), name);
                Exceptions.SetError(Exceptions.AttributeError, baseMessage + hint);
            }
            finally
            {
                errType.Dispose();
                errValue.Dispose();
                errTraceback.Dispose();
            }
        }

        /// <summary>
        /// Builds the full message for an <c>AttributeError</c> raised for a missing
        /// attribute on a .NET object, including any "Did you mean ...?" hint. Used by
        /// the miss-only <c>__getattr__</c> hook installed on reflected types (see
        /// <see cref="AttributeErrorHint"/>), where the original error has already been
        /// cleared, so the base message is reconstructed here.
        /// </summary>
        internal static string BuildMissingAttributeMessage(PyObject self, string name)
        {
            var typeName = "object";
            try
            {
                using var pyType = self.GetPythonType();
                typeName = pyType.Name;
            }
            catch
            {
                // fall back to the generic type name
            }

            var message = $"'{typeName}' object has no attribute '{name}'";
            try
            {
                return message + GetSuggestionHint(self.Reference, name);
            }
            catch
            {
                // never let suggestion building turn into a different exception
                return message;
            }
        }

        /// <summary>
        /// Returns " Did you mean: 'x', 'y'?" listing similarly-named members of the
        /// managed object, or an empty string when there is nothing to suggest. Dunder
        /// names are skipped: they are probed internally by CPython (e.g. __iter__,
        /// __len__) and are never user-facing typos worth helping with.
        /// </summary>
        private static string GetSuggestionHint(BorrowedReference ob, string name)
        {
            if (string.IsNullOrEmpty(name) || name.StartsWith("__", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (GetManagedObject(ob) is not CLRObject clrObj || clrObj.inst is null)
            {
                return string.Empty;
            }

            var suggestions = GetSimilarMemberNames(clrObj.inst.GetType(), name);
            if (suggestions.Count == 0)
            {
                return string.Empty;
            }

            return " Did you mean: " + string.Join(", ", suggestions.Select(s => $"'{s}'")) + "?";
        }

        private static string GetErrorMessage(BorrowedReference value, string fallbackName)
        {
            if (value != null)
            {
                using var str = Runtime.PyObject_Str(value);
                if (!str.IsNull())
                {
                    var managed = Runtime.GetManagedString(str.Borrow());
                    if (!string.IsNullOrEmpty(managed))
                    {
                        return managed;
                    }
                }
                // PyObject_Str may itself have failed; do not let that error leak out.
                Exceptions.Clear();
            }
            return $"object has no attribute '{fallbackName}'";
        }

        private static List<string> GetSimilarMemberNames(Type type, string name)
        {
            const int MaxSuggestions = 5;
            var threshold = Math.Max(2, name.Length / 3);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var scored = new List<(string Name, int Distance)>();

            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance
                                          | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            foreach (var member in members)
            {
                // Skip property/event accessors, operators and other special-name methods,
                // as well as compiler-generated members; none are accessible by name.
                if (member is MethodBase { IsSpecialName: true })
                {
                    continue;
                }

                if (member.Name.Length == 0 || member.Name[0] == '<')
                {
                    continue;
                }

                // Suggest the snake_case alias, since that is the fork's PEP8-style
                // public API surface (members are exposed in both Pascal and snake case).
                var candidate = ToSnakeCaseMemberName(member);
                if (!seen.Add(candidate))
                {
                    continue;
                }

                var distance = LevenshteinDistance(name, candidate);
                var related = distance <= threshold
                    || candidate.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0;
                if (related)
                {
                    scored.Add((candidate, distance));
                }
            }

            return scored
                .OrderBy(t => t.Distance)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxSuggestions)
                .Select(t => t.Name)
                .ToList();
        }

        private static string ToSnakeCaseMemberName(MemberInfo member)
        {
            // Use the field/property overloads so const and static-readonly members
            // are converted to UPPER_CASE, matching how they are exposed to Python.
            return member switch
            {
                FieldInfo fieldInfo => fieldInfo.ToSnakeCase(),
                PropertyInfo propertyInfo => propertyInfo.ToSnakeCase(),
                _ => member.Name.ToSnakeCase(),
            };
        }

        private static int LevenshteinDistance(string a, string b)
        {
            a = a.ToLowerInvariant();
            b = b.ToLowerInvariant();
            var n = a.Length;
            var m = b.Length;
            if (n == 0) return m;
            if (m == 0) return n;

            var prev = new int[m + 1];
            var curr = new int[m + 1];
            for (var j = 0; j <= m; j++) prev[j] = j;

            for (var i = 1; i <= n; i++)
            {
                curr[0] = i;
                for (var j = 1; j <= m; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                (prev, curr) = (curr, prev);
            }
            return prev[m];
        }
    }
}
