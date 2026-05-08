using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Python.Runtime.Slots
{
    internal static class MpLengthSlot
    {
        private static Dictionary<Type, MethodInfo> _countGettersCache = new();

        public static bool CanAssign(Type clrType)
        {
            if (typeof(IEnumerable).IsAssignableFrom(clrType) && TryGetCountGetter(clrType, clrType, out _))
            {
                return true;
            }

            var iface = clrType.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>));
            if (iface != null)
            {
                // Get and cache the Count getter for this type and interface
                TryGetCountGetter(clrType, iface, out _);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Implements __len__ for classes that implement ICollection
        /// (this includes any IList implementer or Array subclass)
        /// </summary>
        internal static nint impl(BorrowedReference ob)
        {
            var co = ManagedType.GetManagedObject(ob) as CLRObject;
            if (co == null)
            {
                Exceptions.RaiseTypeError("invalid object");
                return -1;
            }

            // first look for ICollection implementation directly
            if (co.inst is ICollection c)
            {
                return c.Count;
            }

            Type clrType = co.inst.GetType();
            if (TryGetCountGetter(clrType, clrType, out var getter))
            {
                return (int)getter.Invoke(co.inst, null);
            }

            Exceptions.SetError(Exceptions.TypeError, $"object of type '{clrType.Name}' has no len()");
            return -1;
        }

        /// <summary>
        /// Will get the Count getter for the given parent type and cache it for the given clr type.
        /// This allows us to cache the Count getter for the give type when it's defined as a private interface implementation.
        /// </summary>
        private static bool TryGetCountGetter(Type clrType, Type parentType, out MethodInfo getter)
        {
            if (!_countGettersCache.TryGetValue(clrType, out getter))
            {
                var countProperty = parentType.GetProperty("Count");
                if (countProperty != null)
                {
                    _countGettersCache[clrType] = getter = countProperty.GetMethod;
                }
            }

            return getter != null;
        }
    }
}
