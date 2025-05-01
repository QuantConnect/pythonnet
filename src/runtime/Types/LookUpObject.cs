using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type for managed objects that support look up (dictionaries),
    /// that is, they implement ContainsKey().
    /// This type is essentially the same as a ClassObject, except that it provides
    /// sequence semantics to support natural dictionary usage (__contains__ and __len__)
    /// from Python.
    /// </summary>
    internal class LookUpObject : ClassObject
    {
        [NonSerialized]
        private static Dictionary<Tuple<Type, string>, MethodInfo> methodsByType = new Dictionary<Tuple<Type, string>, MethodInfo>();
        private static List<(string, int)> requiredMethods = new (){ ("Count", 0), ("ContainsKey", 1) };

        private static MethodInfo GetRequiredMethod(MethodInfo[] methods, string methodName, int parametersCount)
        {
            return methods.FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == parametersCount);
        }

        internal static bool VerifyMethodRequirements(Type type)
        {
            var methods = type.GetMethods();

            foreach (var (requiredMethod, parametersCount) in requiredMethods)
            {
                var method = GetRequiredMethod(methods, requiredMethod, parametersCount);
                if (method == null)
                {
                    var getterName = $"get_{requiredMethod}";
                    method = GetRequiredMethod(methods, getterName, parametersCount);
                    if (method == null)
                    {
                        return false;
                    }
                }

                var key = Tuple.Create(type, requiredMethod);
                methodsByType.Add(key, method);
            }

            return true;
        }

        internal LookUpObject(Type tp) : base(tp)
        {
        }

        /// <summary>
        /// Implements __len__ for dictionary types.
        /// </summary>
        public static int mp_length(BorrowedReference ob)
        {
            return LookUpObjectExtensions.Length(ob, methodsByType);
        }

        /// <summary>
        /// Implements __contains__ for dictionary types.
        /// </summary>
        public static int sq_contains(BorrowedReference ob, BorrowedReference v)
        {
            return LookUpObjectExtensions.Contains(ob, v, methodsByType);
        }
    }

    internal static class LookUpObjectExtensions
    {
        internal static bool IsLookUp(this Type type)
        {
            return LookUpObject.VerifyMethodRequirements(type);
        }

        /// <summary>
        /// Implements __len__ for dictionary types.
        /// </summary>
        internal static int Length(BorrowedReference ob, Dictionary<Tuple<Type, string>, MethodInfo> methodsByType)
        {
            var obj = (CLRObject)ManagedType.GetManagedObject(ob);
            var self = obj.inst;

            var key = Tuple.Create(self.GetType(), "Count");
            var methodInfo = methodsByType[key];

            return (int)methodInfo.Invoke(self, null);
        }

        /// <summary>
        /// Implements __contains__ for dictionary types.
        /// </summary>
        internal static int Contains(BorrowedReference ob, BorrowedReference v, Dictionary<Tuple<Type, string>, MethodInfo> methodsByType)
        {
            var obj = (CLRObject)ManagedType.GetManagedObject(ob);
            var self = obj.inst;

            var key = Tuple.Create(self.GetType(), "ContainsKey");
            var methodInfo = methodsByType[key];

            var parameters = methodInfo.GetParameters();
            object arg;
            if (!Converter.ToManaged(v, parameters[0].ParameterType, out arg, false))
            {
                Exceptions.SetError(Exceptions.TypeError,
                    $"invalid parameter type for sq_contains: should be {Converter.GetTypeByAlias(v)}, found {parameters[0].ParameterType}");
            }

            // If the argument is None, we return false. Python allows using None as key,
            // but C# doesn't and will throw, so we shortcut here
            if (arg == null)
            {
                return 0;
            }

            return (bool)methodInfo.Invoke(self, new[] { arg }) ? 1 : 0;
        }
    }
}
