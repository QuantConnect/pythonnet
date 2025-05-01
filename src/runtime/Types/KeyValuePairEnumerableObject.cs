using System;
using System.Collections.Generic;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type for managed KeyValuePairEnumerable (dictionaries).
    /// This type is essentially the same as a ClassObject, except that it provides
    /// sequence semantics to support natural dictionary usage (__contains__ and __len__)
    /// from Python.
    /// </summary>
    internal class KeyValuePairEnumerableObject : LookUpObject
    {
        internal KeyValuePairEnumerableObject(Type tp) : base(tp)
        {

        }

        internal override bool CanSubclass() => false;
    }

    public static class KeyValuePairEnumerableObjectExtension
    {
        public static bool IsKeyValuePairEnumerable(this Type type)
        {
            var iEnumerableType = typeof(IEnumerable<>);
            var keyValuePairType = typeof(KeyValuePair<,>);

            var interfaces = type.GetInterfaces();
            foreach (var i in interfaces)
            {
                if (i.IsGenericType &&
                    i.GetGenericTypeDefinition() == iEnumerableType)
                {
                    var arguments = i.GetGenericArguments();
                    if (arguments.Length != 1) continue;

                    var a = arguments[0];
                    if (a.IsGenericType &&
                        a.GetGenericTypeDefinition() == keyValuePairType &&
                        a.GetGenericArguments().Length == 2)
                    {
                        return LookUpObject.VerifyMethodRequirements(type);
                    }
                }
            }

            return false;
        }
    }
}
