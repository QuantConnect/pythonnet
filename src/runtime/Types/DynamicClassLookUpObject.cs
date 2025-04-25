using System;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type for managed DynamicClass objects that support look up (dictionaries),
    /// that is, they implement ContainsKey().
    /// This type is essentially the same as a ClassObject, except that it provides
    /// sequence semantics to support natural dictionary usage (__contains__ and __len__)
    /// from Python.
    /// </summary>
    internal class DynamicClassLookUpObject : DynamicClassObject
    {
        internal DynamicClassLookUpObject(Type tp) : base(tp)
        {
        }

        /// <summary>
        /// Implements __len__ for dictionary types.
        /// </summary>
        public static int mp_length(BorrowedReference ob)
        {
            return LookUpObject.mp_length(ob);
        }

        /// <summary>
        /// Implements __contains__ for dictionary types.
        /// </summary>
        public static int sq_contains(BorrowedReference ob, BorrowedReference v)
        {
            return LookUpObject.sq_contains(ob, v);
        }
    }
}
