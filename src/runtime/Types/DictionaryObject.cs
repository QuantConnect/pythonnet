using System;
using System.Collections;
using System.Linq;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type for managed IDictionary (dictionaries).
    /// This type is essentially the same as a ClassObject, except that it provides
    /// sequence semantics to support natural dictionary usage (__contains__ and __len__)
    /// from Python.
    /// </summary>
    internal class DictionaryObject : KeyValuePairEnumerableObject
    {
        internal DictionaryObject(Type tp) : base(tp)
        {
        }
    }

    public static class DictionaryObjectExtension
    {
        public static bool IsDictionary(this Type type)
        {
            var iDictionaryType = typeof(IDictionary);
            return type.GetInterfaces().Contains(iDictionaryType) && DictionaryObject.VerifyMethodRequirements(type);
        }
    }
}
