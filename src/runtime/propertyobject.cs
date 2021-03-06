using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Permissions;

using Fasterflect;

namespace Python.Runtime
{
    using MaybeMethodInfo = MaybeMethodBase<MethodInfo>;
    /// <summary>
    /// Implements a Python descriptor type that manages CLR properties.
    /// </summary>
    [Serializable]
    internal class PropertyObject : ExtensionType
    {
        private MaybeMemberInfo<PropertyInfo> info;
        private MaybeMethodInfo getter;
        private MaybeMethodInfo setter;

        private MemberGetter _memberGetter;
        private Type _memberGetterType;

        private MemberSetter _memberSetter;
        private Type _memberSetterType;

        private bool _isValueType;
        private Type _isValueTypeType;

        [StrongNameIdentityPermission(SecurityAction.Assert)]
        public PropertyObject(PropertyInfo md)
        {
            getter = md.GetGetMethod(true);
            setter = md.GetSetMethod(true);
            info = md;
        }


        /// <summary>
        /// Descriptor __get__ implementation. This method returns the
        /// value of the property on the given object. The returned value
        /// is converted to an appropriately typed Python object.
        /// </summary>
        public static IntPtr tp_descr_get(IntPtr ds, IntPtr ob, IntPtr tp)
        {
            var self = (PropertyObject)GetManagedObject(ds);
            if (!self.info.Valid)
            {
                return Exceptions.RaiseTypeError(self.info.DeletedMessage);
            }
            var info = self.info.Value;
            MethodInfo getter = self.getter.UnsafeValue;
            object result;


            if (getter == null)
            {
                return Exceptions.RaiseTypeError("property cannot be read");
            }

            if (ob == IntPtr.Zero || ob == Runtime.PyNone)
            {
                if (!getter.IsStatic)
                {
                    Exceptions.SetError(Exceptions.TypeError,
                        "instance property must be accessed through a class instance");
                    return IntPtr.Zero;
                }

                try
                {
                    result = self.GetMemberGetter(info.DeclaringType)(info.DeclaringType);
                    return Converter.ToPython(result, info.PropertyType);
                }
                catch (Exception e)
                {
                    return Exceptions.RaiseTypeError(e.Message);
                }
            }

            var co = GetManagedObject(ob) as CLRObject;
            if (co == null)
            {
                return Exceptions.RaiseTypeError("invalid target");
            }

            try
            {
                var type = co.inst.GetType();
                result = self.GetMemberGetter(type)(self.IsValueType(type) ? co.inst.WrapIfValueType() : co.inst);
                return Converter.ToPython(result, info.PropertyType);
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return IntPtr.Zero;
            }
        }


        /// <summary>
        /// Descriptor __set__ implementation. This method sets the value of
        /// a property based on the given Python value. The Python value must
        /// be convertible to the type of the property.
        /// </summary>
        public new static int tp_descr_set(IntPtr ds, IntPtr ob, IntPtr val)
        {
            var self = (PropertyObject)GetManagedObject(ds);
            if (!self.info.Valid)
            {
                Exceptions.RaiseTypeError(self.info.DeletedMessage);
                return -1;
            }
            var info = self.info.Value;

            MethodInfo setter = self.setter.UnsafeValue;
            object newval;

            if (val == IntPtr.Zero)
            {
                Exceptions.RaiseTypeError("cannot delete property");
                return -1;
            }

            if (setter == null)
            {
                Exceptions.RaiseTypeError("property is read-only");
                return -1;
            }


            if (!Converter.ToManaged(val, info.PropertyType, out newval, true))
            {
                return -1;
            }

            bool is_static = setter.IsStatic;

            if (ob == IntPtr.Zero || ob == Runtime.PyNone)
            {
                if (!is_static)
                {
                    Exceptions.RaiseTypeError("instance property must be set on an instance");
                    return -1;
                }
            }

            try
            {
                if (!is_static)
                {
                    var co = GetManagedObject(ob) as CLRObject;
                    if (co == null)
                    {
                        Exceptions.RaiseTypeError("invalid target");
                        return -1;
                    }

                    var type = co.inst.GetType();
                    self.GetMemberSetter(type)(self.IsValueType(type) ? co.inst.WrapIfValueType() : co.inst, newval);
                }
                else
                {
                    self.GetMemberSetter(info.DeclaringType)(info.DeclaringType, newval);
                }
                return 0;
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return -1;
            }
        }


        /// <summary>
        /// Descriptor __repr__ implementation.
        /// </summary>
        public static IntPtr tp_repr(IntPtr ob)
        {
            var self = (PropertyObject)GetManagedObject(ob);
            return Runtime.PyString_FromString($"<property '{self.info}'>");
        }

        private MemberGetter GetMemberGetter(Type type)
        {
            if (type != _memberGetterType)
            {
                _memberGetter = FasterflectManager.GetPropertyGetter(type, info.Value.Name);
                _memberGetterType = type;
            }

            return _memberGetter;
        }

        private MemberSetter GetMemberSetter(Type type)
        {
            if (type != _memberSetterType)
            {
                _memberSetter = FasterflectManager.GetPropertySetter(type, info.Value.Name);
                _memberSetterType = type;
            }

            return _memberSetter;
        }

        private bool IsValueType(Type type)
        {
            if (type != _isValueTypeType)
            {
                _isValueType = FasterflectManager.IsValueType(type);
                _isValueTypeType = type;
            }

            return _isValueType;
        }
    }
}
