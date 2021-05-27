using System;
using System.Collections.Generic;
using System.Dynamic;

using FastMember;

namespace Python.Runtime
{
    /// <summary>
    /// TypeAccessorManager creates TypeAccessor instances when necessary.
    /// </summary>
    public class TypeAccessorManager
    {
        private static Dictionary<Type, TypeAccessor> Cache = new Dictionary<Type, TypeAccessor>(128);

        /// <summary>
        /// Returns the TypeAccessor of a type, or null if FastMember doesn't work on the type.
        /// </summary>
        /// <param name="type">The type to get the TypeAccessor for</param>
        /// <returns>A TypeAccessor instance that can be used as a faster alternative to reflection, or null if FastMember doesn't work on the type</returns>
        public static TypeAccessor GetTypeAccessor(Type type)
        {
            TypeAccessor typeAccessor;
            if (Cache.TryGetValue(type, out typeAccessor))
            {
                return typeAccessor;
            }

            // TypeAccessor has issues with dynamic types and inner types of generic classes
            // In those cases we fall back to reflection, which is significantly slower but has less limitations
            if (type.DeclaringType?.ContainsGenericParameters != true &&
                !typeof(IDynamicMetaObjectProvider).IsAssignableFrom(type))
            {
                typeAccessor = TypeAccessor.Create(type);
            }

            Cache.Add(type, typeAccessor);
            return typeAccessor;
        }
    }
}
