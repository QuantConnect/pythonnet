using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Python.Runtime.Native;

#pragma warning disable CS0618 // Type or member is obsolete. OK for internal use
using static Python.Runtime.PythonDerivedType;
#pragma warning restore CS0618 // Type or member is obsolete

namespace Python.Runtime
{
    /// <summary>
    /// Managed class that provides the implementation for reflected types.
    /// Managed classes and value types are represented in Python by actual
    /// Python type objects. Each of those type objects is associated with
    /// an instance of ClassObject, which provides its implementation.
    /// </summary>
    /// <remarks>
    /// interface used to identify which C# types were dynamically created as python subclasses
    /// </remarks>
    public interface IPythonDerivedType
    {
    }

    [Serializable]
    internal class ClassDerivedObject : ClassObject
    {
        private static Dictionary<string, AssemblyBuilder> assemblyBuilders;
        private static Dictionary<Tuple<string, string>, ModuleBuilder> moduleBuilders;

        /// <summary>
        /// Python objects whose .NET object holds one reference to them, taken in <see cref="tp_finalize"/>
        /// </summary>
        private static readonly HashSet<IntPtr> KeptByManagedObject = new();
        /// <summary>
        /// Python objects that took back the reference their .NET object kept, in <see cref="ToPython"/>,
        /// while .NET may already be finalizing that .NET object
        /// </summary>
        private static readonly HashSet<IntPtr> TakenBackByPython = new();
        /// <summary>
        /// Python objects whose .NET object was finalized while it held the reference taken in
        /// <see cref="tp_finalize"/>, and that weren't passed to .NET since: the next time the GC finds
        /// such an object unreachable, nothing else references it and the GC frees the cycle
        /// </summary>
        private static readonly HashSet<IntPtr> ReturnedToPython = new();
        /// <summary>
        /// Guards <see cref="KeptByManagedObject"/>, <see cref="TakenBackByPython"/> and
        /// <see cref="ReturnedToPython"/>, which the .NET finalizer thread updates in
        /// <see cref="PythonDerivedType.PyFinalize"/>
        /// </summary>
        private static readonly object KeptLock = new();
        /// <summary>
        /// Nothing else references the wrapper of a .NET object its Python object holds weakly:
        /// this keeps it alive as long as the .NET object
        /// </summary>
        private static readonly ConditionalWeakTable<object, CLRObject> WeaklyHeldWrappers = new();

        static ClassDerivedObject()
        {
            assemblyBuilders = new Dictionary<string, AssemblyBuilder>();
            moduleBuilders = new Dictionary<Tuple<string, string>, ModuleBuilder>();
        }

        public static void Reset()
        {
            assemblyBuilders = new Dictionary<string, AssemblyBuilder>();
            moduleBuilders = new Dictionary<Tuple<string, string>, ModuleBuilder>();
            lock (KeptLock)
            {
                KeptByManagedObject.Clear();
                TakenBackByPython.Clear();
                ReturnedToPython.Clear();
            }
            WeaklyHeldWrappers.Clear();
            finalizedFlagOffset = GetFinalizedFlagOffset();
        }

        internal ClassDerivedObject(Type tp) : base(tp)
        {
        }

        protected override NewReference NewObjectToPython(object obj, BorrowedReference tp)
        {
            var self = base.NewObjectToPython(obj, tp);

            SetPyObj((IPythonDerivedType)obj, self.Borrow());

            // Decrement the python object's reference count.
            // This doesn't actually destroy the object, it just sets the reference to this object
            // to be a weak reference and it will be destroyed when the C# object is destroyed.
            Runtime.XDecref(self.Steal());

            return Converter.ToPython(obj, type.Value);
        }

        protected override void SetTypeNewSlot(BorrowedReference pyType, SlotsHolder slotsHolder)
        {
            // Python derived types rely on base tp_new and overridden __init__
        }

        public new static void tp_dealloc(NewReference ob)
        {
            var self = (CLRObject?)GetManagedObject(ob.Borrow());

            // don't let the python GC destroy this object
            Runtime.PyObject_GC_UnTrack(ob.Borrow());
            lock (KeptLock)
            {
                ReturnedToPython.Remove(ob.DangerousGetAddress());
            }

            // self may be null after Shutdown begun
            if (self is not null)
            {
                // The python should now have a ref count of 0, but we don't actually want to
                // deallocate the object until the C# object that references it is destroyed.
                // So we don't call PyObject_GC_Del here and instead we set the python
                // reference to a weak reference so that the C# object can be collected.
                ReplaceGCHandle(ob.Borrow(), self, GCHandleType.Weak);
            }
        }

        /// <summary>
        /// Called by the Python cyclic GC on an object it found unreachable from Python, before it clears
        /// the cycle. .NET may still reference the object, which Python can't see, so instead of letting
        /// the cycle be cleared, the .NET object keeps one reference to the Python object, and the Python
        /// object stops keeping the .NET object alive, as <see cref="tp_dealloc"/> does. Python gets that
        /// reference back in <see cref="ToPython"/>, and the .NET object releases it when it's finalized.
        /// </summary>
        public static void tp_finalize(BorrowedReference ob)
        {
            // The GC marks an object finalized before it calls tp_finalize and doesn't call it again
            // once marked. Otherwise, it's a direct __del__ call from Python, which still references the object.
            if ((FinalizedFlagWord(ob) & FinalizedFlag) == 0)
            {
                return;
            }

            // .NET already let go of the object while Python still referenced it: the GC frees the cycle
            lock (KeptLock)
            {
                if (ReturnedToPython.Remove(ob.DangerousGetAddress()))
                {
                    return;
                }
            }

            var self = (CLRObject?)GetManagedObject(ob);
            // null once the .NET object is gone: the GC frees the cycle
            if (self is null)
            {
                return;
            }

            // the next time Python lets go of the object in a cycle, the GC has to call tp_finalize again
            FinalizedFlagWord(ob) &= ~FinalizedFlag;
            Runtime.XIncref(ob);
            lock (KeptLock)
            {
                KeptByManagedObject.Add(ob.DangerousGetAddress());
            }
            // Another object's __del__ may give Python the object back before the cycle is cleared.
            // Tracking resurrection keeps the handle valid until Finalize(IntPtr, object, bool) takes
            // the .NET object back after its finalizer ran.
            ReplaceGCHandle(ob, self, GCHandleType.WeakTrackResurrection);
        }

        /// <summary>
        /// Called by the .NET finalizer of a Python derived type. Returns true when the .NET object
        /// held the Python object since <see cref="tp_finalize"/>, or Python took it back while .NET
        /// may have been finalizing it: Python may still reference the object, so its .NET finalization
        /// waits until <see cref="PythonDerivedType.Finalize(IntPtr, object, bool)"/> handles it.
        /// </summary>
        internal static bool DeferFinalization(IntPtr ob, out bool keptReference)
        {
            lock (KeptLock)
            {
                keptReference = KeptByManagedObject.Remove(ob);
                if (keptReference)
                {
                    ReturnedToPython.Add(ob);
                }
                return TakenBackByPython.Remove(ob) || keptReference;
            }
        }

        /// <summary>
        /// Called when .NET finalized the .NET object of a Python object Python still references:
        /// through the cycle, or because Python got the object back without .NET. The Python object
        /// keeps the .NET object alive again, as for any object Python holds.
        /// </summary>
        internal static void ReturnToPython(BorrowedReference ob, object managed)
        {
            if (WeaklyHeldWrappers.TryGetValue(managed, out var self))
            {
                ReplaceGCHandle(ob, self, GCHandleType.Normal);
            }
        }

        /// <summary>
        /// The GC's finalized flag: bit 0 of _gc_prev since Python 3.8, of gc_refs before
        /// </summary>
        private const nint FinalizedFlag = 1;

        /// <summary>
        /// Offset from the object to the GC header word that holds <see cref="FinalizedFlag"/>
        /// </summary>
        private static nint finalizedFlagOffset;

        private static unsafe ref nint FinalizedFlagWord(BorrowedReference ob)
            => ref *(nint*)((byte*)ob.DangerousGetAddress() + finalizedFlagOffset);

        private static nint GetFinalizedFlagOffset()
        {
            if (Runtime.PyVersion >= new Version(3, 8))
            {
                // _gc_prev, the last word of the GC header that precedes the object
                return -IntPtr.Size;
            }

            // Python 3.7: gc_refs, the third word of a GC header padded to the platform's
            // long double alignment. sys.getsizeof adds that header to __sizeof__.
            using var list = Runtime.PyList_New(0);
            using var sizeOf = Runtime.PyObject_GetAttrString(list.Borrow(), "__sizeof__");
            using var noArgs = Runtime.PyTuple_New(0);
            using var size = Runtime.PyObject_CallObject(sizeOf.BorrowOrThrow(), noArgs.Borrow());
            using var getSizeOfArgs = Runtime.PyTuple_New(1);
            Runtime.PyTuple_SetItem(getSizeOfArgs.Borrow(), 0, list.Borrow());
            using var sizeWithGCHeader = Runtime.PyObject_CallObject(
                Runtime.PySys_GetObject("getsizeof"), getSizeOfArgs.Borrow());
            nint gcHeaderSize = Runtime.PyLong_AsSignedSize_t(sizeWithGCHeader.BorrowOrThrow())
                - Runtime.PyLong_AsSignedSize_t(size.BorrowOrThrow());
            return -gcHeaderSize + 2 * IntPtr.Size;
        }

        private static void ReplaceGCHandle(BorrowedReference ob, CLRObject self, GCHandleType type)
        {
            if (type != GCHandleType.Normal)
            {
                WeaklyHeldWrappers.AddOrUpdate(self.inst, self);
            }
            else
            {
                WeaklyHeldWrappers.Remove(self.inst);
            }
            GCHandle oldHandle = GetGCHandle(ob);
            SetGCHandle(ob, GCHandle.Alloc(self, type));
            oldHandle.Free();
        }

        /// <summary>
        /// No-op clear. Real cleanup happens in <seealso cref="Finalize(IntPtr)"/>
        /// </summary>
        public new static int tp_clear(BorrowedReference ob) => 0;

        /// <summary>
        /// Called from Converter.ToPython for types that are python subclasses of managed types.
        /// The referenced python object is returned instead of a new wrapper.
        /// </summary>
        internal static NewReference ToPython(IPythonDerivedType obj)
        {
            // derived types have a __pyobj__ field that gets set to the python
            // object in the overridden constructor
            BorrowedReference self;
            try
            {
                self = GetPyObj(obj).CheckRun();
            } catch (RuntimeShutdownException e)
            {
                Exceptions.SetError(e);
                return default;
            }

            bool kept;
            lock (KeptLock)
            {
                ReturnedToPython.Remove(self.DangerousGetAddress());
                kept = KeptByManagedObject.Remove(self.DangerousGetAddress());
                if (kept)
                {
                    TakenBackByPython.Add(self.DangerousGetAddress());
                }
            }
            if (kept)
            {
                // Python takes over the reference the .NET object kept in tp_finalize
                ReplaceGCHandle(self, (CLRObject)GetManagedObject(self)!, GCHandleType.Normal);
                return NewReference.DangerousFromPointer(self.DangerousGetAddress());
            }

            var result = new NewReference(self);

            // when the C# constructor creates the python object it starts as a weak
            // reference with a reference count of 0. Now we're passing this object
            // to Python the reference count needs to be incremented and the reference
            // needs to be replaced with a strong reference to stop the C# object being
            // collected while Python still has a reference to it.
            if (Runtime.Refcount(self) == 1)
            {
                Runtime._Py_NewReference(self);
                ReplaceGCHandle(self, (CLRObject)GetManagedObject(self)!, GCHandleType.Normal);

                // now the object has a python reference it's safe for the python GC to track it
                Runtime.PyObject_GC_Track(self);
            }

            return result;
        }

        /// <summary>
        /// Creates a new managed type derived from a base type with any virtual
        /// methods overridden to call out to python if the associated python
        /// object has overridden the method.
        /// </summary>
        internal static Type CreateDerivedType(string name,
            Type baseType,
            BorrowedReference py_dict,
            string? namespaceStr,
            string? assemblyName,
            string moduleName = "Python.Runtime.Dynamic.dll")
        {
            // TODO: clean up
            if (null != namespaceStr)
            {
                name = namespaceStr + "." + name;
            }

            if (null == assemblyName)
            {
                assemblyName = "Python.Runtime.Dynamic";
            }

            ModuleBuilder moduleBuilder = GetModuleBuilder(assemblyName, moduleName);

            Type baseClass = baseType;
            var interfaces = new List<Type> { typeof(IPythonDerivedType) };

            // if the base type is an interface then use System.Object as the base class
            // and add the base type to the list of interfaces this new class will implement.
            if (baseType.IsInterface)
            {
                interfaces.Add(baseType);
                baseClass = typeof(object);
            }

            TypeBuilder typeBuilder = moduleBuilder.DefineType(name,
                TypeAttributes.Public | TypeAttributes.Class,
                baseClass,
                interfaces.ToArray());

            // add a field for storing the python object pointer
            // FIXME: fb not used
            FieldBuilder fb = typeBuilder.DefineField(PyObjName,
#pragma warning disable CS0618 // Type or member is obsolete. OK for internal use.
                                typeof(UnsafeReferenceWithRun),
#pragma warning restore CS0618 // Type or member is obsolete
                                FieldAttributes.Private);

            // override any constructors
            ConstructorInfo[] constructors = baseClass.GetConstructors();
            foreach (ConstructorInfo ctor in constructors)
            {
                AddConstructor(ctor, baseType, typeBuilder);
            }

            // Override any properties explicitly overridden in python
            var pyProperties = new HashSet<string>();
            if (py_dict != null && Runtime.PyDict_Check(py_dict))
            {
                using var dict = new PyDict(py_dict);
                using (PyIterable keys = dict.Keys())
                {
                    foreach (PyObject pyKey in keys)
                    {
                        using (PyObject value = dict[pyKey])
                        {
                            if (value.HasAttr("_clr_property_type_"))
                            {
                                string propertyName = pyKey.ToString()!;
                                pyProperties.Add(propertyName);

                                // Add the property to the type
                                AddPythonProperty(propertyName, value, typeBuilder);
                            }
                        }
                    }
                }
            }

            // override any virtual methods not already overridden by the properties above
            MethodInfo[] methods = baseType.GetMethods();
            var virtualMethods = new HashSet<string>();
            foreach (MethodInfo method in methods)
            {
                if (!method.Attributes.HasFlag(MethodAttributes.Virtual) |
                    method.Attributes.HasFlag(MethodAttributes.Final))
                {
                    continue;
                }

                // skip if this property has already been overridden
                if ((method.Name.StartsWith("get_") || method.Name.StartsWith("set_"))
                    && pyProperties.Contains(method.Name.Substring(4)))
                {
                    continue;
                }

                // keep track of the virtual methods redirected to the python instance
                virtualMethods.Add(method.Name);

                // override the virtual method to call out to the python method, if there is one.
                AddVirtualMethod(method, baseType, typeBuilder);
            }

            // Add any additional methods and properties explicitly exposed from Python.
            if (py_dict != null && Runtime.PyDict_Check(py_dict))
            {
                using var dict = new PyDict(py_dict);
                using (PyIterable keys = dict.Keys())
                {
                    foreach (PyObject pyKey in keys)
                    {
                        using (PyObject value = dict[pyKey])
                        {
                            if (value.HasAttr("_clr_return_type_") && value.HasAttr("_clr_arg_types_"))
                            {
                                string methodName = pyKey.ToString()!;

                                // if this method has already been redirected to the python method skip it
                                if (virtualMethods.Contains(methodName))
                                {
                                    continue;
                                }

                                // Add the method to the type
                                AddPythonMethod(methodName, value, typeBuilder);
                            }
                        }
                    }
                }
            }

            // add the destructor so the python object created in the constructor gets destroyed,
            // then runs the base type's finalizer, unless PyFinalize defers it
            MethodBuilder methodBuilder = typeBuilder.DefineMethod("Finalize",
                MethodAttributes.Family |
                MethodAttributes.Virtual |
                MethodAttributes.HideBySig,
                CallingConventions.Standard,
                typeof(void),
                Type.EmptyTypes);
            ILGenerator il = methodBuilder.GetILGenerator();
            Label deferred = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);
#pragma warning disable CS0618 // PythonDerivedType is for internal use only
            il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod(nameof(PyFinalize)));
#pragma warning restore CS0618 // PythonDerivedType is for internal use only
            il.Emit(OpCodes.Brfalse_S, deferred);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseClass.GetMethod("Finalize", BindingFlags.NonPublic | BindingFlags.Instance));
            il.MarkLabel(deferred);
            il.Emit(OpCodes.Ret);

            Type type = typeBuilder.CreateType();

            // scan the assembly so the newly added class can be imported
            Assembly assembly = Assembly.GetAssembly(type);
            AssemblyManager.ScanAssembly(assembly);

            // FIXME: assemblyBuilder not used
            AssemblyBuilder assemblyBuilder = assemblyBuilders[assemblyName];

            return type;
        }

        /// <summary>
        /// Add a constructor override that calls the python ctor after calling the base type constructor.
        /// </summary>
        /// <param name="ctor">constructor to be called before calling the python ctor</param>
        /// <param name="baseType">Python callable object</param>
        /// <param name="typeBuilder">TypeBuilder for the new type the ctor is to be added to</param>
        private static void AddConstructor(ConstructorInfo ctor, Type baseType, TypeBuilder typeBuilder)
        {
            ParameterInfo[] parameters = ctor.GetParameters();
            Type[] parameterTypes = (from param in parameters select param.ParameterType).ToArray();

            // create a method for calling the original constructor
            string baseCtorName = "_" + baseType.Name + "__cinit__";
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(baseCtorName,
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig,
                typeof(void),
                parameterTypes);

            // emit the assembly for calling the original method using call instead of callvirt
            ILGenerator il = methodBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            for (var i = 0; i < parameters.Length; ++i)
            {
                il.Emit(OpCodes.Ldarg, i + 1);
            }
            il.Emit(OpCodes.Call, ctor);
            il.Emit(OpCodes.Ret);

            // override the original method with a new one that dispatches to python
            ConstructorBuilder cb = typeBuilder.DefineConstructor(MethodAttributes.Public |
                                                                  MethodAttributes.ReuseSlot |
                                                                  MethodAttributes.HideBySig,
                ctor.CallingConvention,
                parameterTypes);
            il = cb.GetILGenerator();
            il.DeclareLocal(typeof(object[]));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, baseCtorName);
            il.Emit(OpCodes.Ldc_I4, parameters.Length);
            il.Emit(OpCodes.Newarr, typeof(object));
            il.Emit(OpCodes.Stloc_0);
            for (var i = 0; i < parameters.Length; ++i)
            {
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg, i + 1);
                if (parameterTypes[i].IsValueType)
                {
                    il.Emit(OpCodes.Box, parameterTypes[i]);
                }
                il.Emit(OpCodes.Stelem, typeof(object));
            }
            il.Emit(OpCodes.Ldloc_0);
#pragma warning disable CS0618 // PythonDerivedType is for internal use only
            il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod(nameof(InvokeCtor)));
#pragma warning restore CS0618 // PythonDerivedType is for internal use only
            il.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Add a virtual method override that checks for an override on the python instance
        /// and calls it, otherwise fall back to the base class method.
        /// </summary>
        /// <param name="method">virtual method to be overridden</param>
        /// <param name="baseType">Python callable object</param>
        /// <param name="typeBuilder">TypeBuilder for the new type the method is to be added to</param>
        private static void AddVirtualMethod(MethodInfo method, Type baseType, TypeBuilder typeBuilder)
        {
            ParameterInfo[] parameters = method.GetParameters();
            Type[] parameterTypes = (from param in parameters select param.ParameterType).ToArray();

            // If the method isn't abstract create a method for calling the original method
            string? baseMethodName = null;
            if (!method.IsAbstract)
            {
                baseMethodName = "_" + baseType.Name + "__" + method.Name;
                MethodBuilder baseMethodBuilder = typeBuilder.DefineMethod(baseMethodName,
                    MethodAttributes.Public |
                    MethodAttributes.Final |
                    MethodAttributes.HideBySig,
                    method.ReturnType,
                    parameterTypes);

                // emit the assembly for calling the original method using call instead of callvirt
                ILGenerator baseIl = baseMethodBuilder.GetILGenerator();
                baseIl.Emit(OpCodes.Ldarg_0);
                for (var i = 0; i < parameters.Length; ++i)
                {
                    baseIl.Emit(OpCodes.Ldarg, i + 1);
                }
                baseIl.Emit(OpCodes.Call, method);
                baseIl.Emit(OpCodes.Ret);
            }

            // override the original method with a new one that dispatches to python
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(method.Name,
                MethodAttributes.Public |
                MethodAttributes.ReuseSlot |
                MethodAttributes.Virtual |
                MethodAttributes.HideBySig,
                method.CallingConvention,
                method.ReturnType,
                parameterTypes);
            ILGenerator il = methodBuilder.GetILGenerator();
            il.DeclareLocal(typeof(object[]));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, method.Name);

            // don't fall back to the base type's method if it's abstract
            if (null != baseMethodName)
            {
                il.Emit(OpCodes.Ldstr, baseMethodName);
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            il.Emit(OpCodes.Ldc_I4, parameters.Length);
            il.Emit(OpCodes.Newarr, typeof(object));
            il.Emit(OpCodes.Stloc_0);
            for (var i = 0; i < parameters.Length; ++i)
            {
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg, i + 1);
                var type = parameterTypes[i];
                if (type.IsByRef)
                {
                    type = type.GetElementType();
                    il.Emit(OpCodes.Ldobj, type);
                }
                if (type.IsValueType)
                {
                    il.Emit(OpCodes.Box, type);
                }
                il.Emit(OpCodes.Stelem, typeof(object));
            }
            il.Emit(OpCodes.Ldloc_0);

            il.Emit(OpCodes.Ldtoken, method);
#pragma warning disable CS0618 // PythonDerivedType is for internal use only
            if (method.ReturnType == typeof(void))
            {
                il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod(nameof(InvokeMethodVoid)));
            }
            else
            {
                il.Emit(OpCodes.Call,
                    typeof(PythonDerivedType).GetMethod(nameof(InvokeMethod)).MakeGenericMethod(method.ReturnType));
            }
#pragma warning restore CS0618 // PythonDerivedType is for internal use only
            CodeGenerator.GenerateMarshalByRefsBack(il, parameterTypes);
            il.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Python method may have the following function attributes set to control how they're exposed:
        /// - _clr_return_type_    - method return type (required)
        /// - _clr_arg_types_      - list of method argument types (required)
        /// - _clr_method_name_    - method name, if different from the python method name (optional)
        /// </summary>
        /// <param name="methodName">Method name to add to the type</param>
        /// <param name="func">Python callable object</param>
        /// <param name="typeBuilder">TypeBuilder for the new type the method/property is to be added to</param>
        private static void AddPythonMethod(string methodName, PyObject func, TypeBuilder typeBuilder)
        {
            const string methodNameAttribute = "_clr_method_name_";
            if (func.HasAttr(methodNameAttribute))
            {
                using PyObject pyMethodName = func.GetAttr(methodNameAttribute);
                methodName = pyMethodName.As<string>() ?? throw new ArgumentNullException(methodNameAttribute);
            }

            using (PyObject pyReturnType = func.GetAttr("_clr_return_type_"))
            using (var pyArgTypes = PyIter.GetIter(func.GetAttr("_clr_arg_types_")))
            {
                var returnType = pyReturnType.AsManagedObject(typeof(Type)) as Type;
                if (returnType == null)
                {
                    returnType = typeof(void);
                }

                var argTypes = new List<Type>();
                foreach (PyObject pyArgType in pyArgTypes)
                {
                    var argType = pyArgType.AsManagedObject(typeof(Type)) as Type;
                    if (argType == null)
                    {
                        throw new ArgumentException("_clr_arg_types_ must be a list or tuple of CLR types");
                    }
                    argTypes.Add(argType);
                }

                // add the method to call back into python
                MethodAttributes methodAttribs = MethodAttributes.Public |
                                                 MethodAttributes.Virtual |
                                                 MethodAttributes.ReuseSlot |
                                                 MethodAttributes.HideBySig;

                MethodBuilder methodBuilder = typeBuilder.DefineMethod(methodName,
                    methodAttribs,
                    returnType,
                    argTypes.ToArray());

                ILGenerator il = methodBuilder.GetILGenerator();

                il.DeclareLocal(typeof(object[]));
                il.DeclareLocal(typeof(RuntimeMethodHandle));

                // this
                il.Emit(OpCodes.Ldarg_0);

                // Python method to call
                il.Emit(OpCodes.Ldstr, methodName);

                // original method name
                il.Emit(OpCodes.Ldnull); // don't fall back to the base type's method

                // create args array
                il.Emit(OpCodes.Ldc_I4, argTypes.Count);
                il.Emit(OpCodes.Newarr, typeof(object));
                il.Emit(OpCodes.Stloc_0);

                // fill args array
                for (var i = 0; i < argTypes.Count; ++i)
                {
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldc_I4, i);
                    il.Emit(OpCodes.Ldarg, i + 1);
                    var type = argTypes[i];
                    if (type.IsByRef)
                    {
                        type = type.GetElementType();
                        il.Emit(OpCodes.Ldobj, type);
                    }
                    if (type.IsValueType)
                    {
                        il.Emit(OpCodes.Box, type);
                    }
                    il.Emit(OpCodes.Stelem, typeof(object));
                }

                // args array
                il.Emit(OpCodes.Ldloc_0);

                // method handle for the base method is null
                il.Emit(OpCodes.Ldloca_S, 1);
                il.Emit(OpCodes.Initobj, typeof(RuntimeMethodHandle));
                il.Emit(OpCodes.Ldloc_1);
#pragma warning disable CS0618 // PythonDerivedType is for internal use only

                // invoke the method
                if (returnType == typeof(void))
                {
                    il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod(nameof(InvokeMethodVoid)));
                }
                else
                {
                    il.Emit(OpCodes.Call,
                        typeof(PythonDerivedType).GetMethod(nameof(InvokeMethod)).MakeGenericMethod(returnType));
                }

                CodeGenerator.GenerateMarshalByRefsBack(il, argTypes);

#pragma warning restore CS0618 // PythonDerivedType is for internal use only
                il.Emit(OpCodes.Ret);
            }
        }

        /// <summary>
        /// Python properties may have the following function attributes set to control how they're exposed:
        /// - _clr_property_type_     - property type (required)
        /// </summary>
        /// <param name="propertyName">Property name to add to the type</param>
        /// <param name="func">Python property object</param>
        /// <param name="typeBuilder">TypeBuilder for the new type the method/property is to be added to</param>
        private static void AddPythonProperty(string propertyName, PyObject func, TypeBuilder typeBuilder)
        {
            // add the method to call back into python
            MethodAttributes methodAttribs = MethodAttributes.Public |
                                             MethodAttributes.Virtual |
                                             MethodAttributes.ReuseSlot |
                                             MethodAttributes.HideBySig |
                                             MethodAttributes.SpecialName;

            using (PyObject pyPropertyType = func.GetAttr("_clr_property_type_"))
            {
                var propertyType = pyPropertyType.AsManagedObject(typeof(Type)) as Type;
                if (propertyType == null)
                {
                    throw new ArgumentException("_clr_property_type must be a CLR type");
                }

                PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(propertyName,
                    PropertyAttributes.None,
                    propertyType,
                    null);

                if (func.HasAttr("fget"))
                {
                    using (PyObject pyfget = func.GetAttr("fget"))
                    {
                        if (pyfget.IsTrue())
                        {
                            MethodBuilder methodBuilder = typeBuilder.DefineMethod("get_" + propertyName,
                                methodAttribs,
                                propertyType,
                                null);

                            ILGenerator il = methodBuilder.GetILGenerator();
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldstr, propertyName);
#pragma warning disable CS0618 // PythonDerivedType is for internal use only
                            il.Emit(OpCodes.Call,
                                typeof(PythonDerivedType).GetMethod("InvokeGetProperty").MakeGenericMethod(propertyType));
#pragma warning restore CS0618 // PythonDerivedType is for internal use only
                            il.Emit(OpCodes.Ret);

                            propertyBuilder.SetGetMethod(methodBuilder);
                        }
                    }
                }

                if (func.HasAttr("fset"))
                {
                    using (PyObject pyset = func.GetAttr("fset"))
                    {
                        if (pyset.IsTrue())
                        {
                            MethodBuilder methodBuilder = typeBuilder.DefineMethod("set_" + propertyName,
                                methodAttribs,
                                null,
                                new[] { propertyType });

                            ILGenerator il = methodBuilder.GetILGenerator();
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldstr, propertyName);
                            il.Emit(OpCodes.Ldarg_1);
#pragma warning disable CS0618 // PythonDerivedType is for internal use only
                            il.Emit(OpCodes.Call,
                                typeof(PythonDerivedType).GetMethod("InvokeSetProperty").MakeGenericMethod(propertyType));
#pragma warning restore CS0618 // PythonDerivedType is for internal use only
                            il.Emit(OpCodes.Ret);

                            propertyBuilder.SetSetMethod(methodBuilder);
                        }
                    }
                }
            }
        }

        private static ModuleBuilder GetModuleBuilder(string assemblyName, string moduleName)
        {
            // find or create a dynamic assembly and module
            AppDomain domain = AppDomain.CurrentDomain;
            ModuleBuilder moduleBuilder;

            if (moduleBuilders.ContainsKey(Tuple.Create(assemblyName, moduleName)))
            {
                moduleBuilder = moduleBuilders[Tuple.Create(assemblyName, moduleName)];
            }
            else
            {
                AssemblyBuilder assemblyBuilder;
                if (assemblyBuilders.ContainsKey(assemblyName))
                {
                    assemblyBuilder = assemblyBuilders[assemblyName];
                }
                else
                {
                    assemblyBuilder = domain.DefineDynamicAssembly(new AssemblyName(assemblyName),
                        AssemblyBuilderAccess.Run);
                    assemblyBuilders[assemblyName] = assemblyBuilder;
                }

                moduleBuilder = assemblyBuilder.DefineDynamicModule(moduleName);
                moduleBuilders[Tuple.Create(assemblyName, moduleName)] = moduleBuilder;
            }

            return moduleBuilder;
        }
    }

    /// <summary>
    /// PythonDerivedType contains static methods used by the dynamically created
    /// derived type that allow it to call back into python from overridden virtual
    /// methods, and also handle the construction and destruction of the python
    /// object.
    /// </summary>
    /// <remarks>
    /// This has to be public as it's called from methods on dynamically built classes
    /// potentially in other assemblies.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(Util.InternalUseOnly)]
    public class PythonDerivedType
    {
        internal const string PyObjName = "__pyobj__";
        internal const BindingFlags PyObjFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>
        /// This is the implementation of the overridden methods in the derived
        /// type. It looks for a python method with the same name as the method
        /// on the managed base class and if it exists and isn't the managed
        /// method binding (i.e. it has been overridden in the derived python
        /// class) it calls it, otherwise it calls the base method.
        /// </summary>
        public static T? InvokeMethod<T>(IPythonDerivedType obj, string methodName, string origMethodName,
            object[] args, RuntimeMethodHandle methodHandle)
        {
            var self = GetPyObj(obj);

            if (null != self.Ref)
            {
                var disposeList = new List<PyObject>();
                PyGILState gs = Runtime.PyGILState_Ensure();
                try
                {
                    using var pyself = new PyObject(self.CheckRun());
                    using PyObject method = pyself.GetAttr(methodName, Runtime.None);
                    if (method.Reference != Runtime.PyNone)
                    {
                        // if the method hasn't been overridden then it will be a managed object
                        ManagedType? managedMethod = ManagedType.GetManagedObject(method.Reference);
                        if (null == managedMethod)
                        {
                            var pyargs = new PyObject[args.Length];
                            for (var i = 0; i < args.Length; ++i)
                            {
                                pyargs[i] = Converter.ToPythonImplicit(args[i]).MoveToPyObject();
                                disposeList.Add(pyargs[i]);
                            }

                            PyObject py_result = method.Invoke(pyargs);
                            PyTuple? result_tuple = MarshalByRefsBack(args, methodHandle, py_result, outsOffset: 1);
                            return result_tuple is not null
                                ? result_tuple[0].As<T>()
                                : py_result.As<T>();
                        }
                    }
                }
                finally
                {
                    foreach (PyObject x in disposeList)
                    {
                        x?.Dispose();
                    }
                    Runtime.PyGILState_Release(gs);
                }
            }

            if (origMethodName == null)
            {
                throw new NotImplementedException("Python object does not have a '" + methodName + "' method");
            }

            return (T)obj.GetType().InvokeMember(origMethodName,
                BindingFlags.InvokeMethod,
                null,
                obj,
                args);
        }

        public static void InvokeMethodVoid(IPythonDerivedType obj, string methodName, string origMethodName,
            object?[] args, RuntimeMethodHandle methodHandle)
        {
            var self = GetPyObj(obj);
            if (null != self.Ref)
            {
                var disposeList = new List<PyObject>();
                PyGILState gs = Runtime.PyGILState_Ensure();
                try
                {
                    using var pyself = new PyObject(self.CheckRun());
                    using PyObject method = pyself.GetAttr(methodName, Runtime.None);
                    if (method.Reference != Runtime.None)
                    {
                        // if the method hasn't been overridden then it will be a managed object
                        ManagedType? managedMethod = ManagedType.GetManagedObject(method);
                        if (null == managedMethod)
                        {
                            var pyargs = new PyObject[args.Length];
                            for (var i = 0; i < args.Length; ++i)
                            {
                                pyargs[i] = Converter.ToPythonImplicit(args[i]).MoveToPyObject();
                                disposeList.Add(pyargs[i]);
                            }

                            PyObject py_result = method.Invoke(pyargs);
                            MarshalByRefsBack(args, methodHandle, py_result, outsOffset: 0);
                            return;
                        }
                    }
                }
                finally
                {
                    foreach (PyObject x in disposeList)
                    {
                        x?.Dispose();
                    }
                    Runtime.PyGILState_Release(gs);
                }
            }

            if (origMethodName == null)
            {
                throw new NotImplementedException($"Python object does not have a '{methodName}' method");
            }

            obj.GetType().InvokeMember(origMethodName,
                BindingFlags.InvokeMethod,
                null,
                obj,
                args);
        }

        /// <summary>
        /// If the method has byref arguments, reinterprets Python return value
        /// as a tuple of new values for those arguments, and updates corresponding
        /// elements of <paramref name="args"/> array.
        /// </summary>
        private static PyTuple? MarshalByRefsBack(object?[] args, RuntimeMethodHandle methodHandle, PyObject pyResult, int outsOffset)
        {
            if (methodHandle == default) return null;

            var originalMethod = MethodBase.GetMethodFromHandle(methodHandle);
            var parameters = originalMethod.GetParameters();
            PyTuple? outs = null;
            int byrefIndex = 0;
            for (int i = 0; i < parameters.Length; ++i)
            {
                Type type = parameters[i].ParameterType;
                if (!type.IsByRef)
                {
                    continue;
                }

                type = type.GetElementType();

                if (outs is null)
                {
                    outs = new PyTuple(pyResult);
                    pyResult.Dispose();
                }

                args[i] = outs[byrefIndex + outsOffset].AsManagedObject(type);
                byrefIndex++;
            }
            if (byrefIndex > 0 && outs!.Length() > byrefIndex + outsOffset)
                throw new ArgumentException("Too many output parameters");

            return outs;
        }

        public static T? InvokeGetProperty<T>(IPythonDerivedType obj, string propertyName)
        {
            var self = GetPyObj(obj);

            if (null == self.Ref)
            {
                throw new NullReferenceException("Instance must be specified when getting a property");
            }

            PyGILState gs = Runtime.PyGILState_Ensure();
            try
            {
                using var pyself = new PyObject(self.CheckRun());
                using (PyObject pyvalue = pyself.GetAttr(propertyName))
                {
                    return pyvalue.As<T>();
                }
            }
            finally
            {
                Runtime.PyGILState_Release(gs);
            }
        }

        public static void InvokeSetProperty<T>(IPythonDerivedType obj, string propertyName, T value)
        {
            var self = GetPyObj(obj);

            if (null == self.Ref)
            {
                throw new NullReferenceException("Instance must be specified when setting a property");
            }

            PyGILState gs = Runtime.PyGILState_Ensure();
            try
            {
                using var pyself = new PyObject(self.CheckRun());
                using var pyvalue = Converter.ToPythonImplicit(value).MoveToPyObject();
                pyself.SetAttr(propertyName, pyvalue);
            }
            finally
            {
                Runtime.PyGILState_Release(gs);
            }
        }

        public static void InvokeCtor(IPythonDerivedType obj, string origCtorName, object[] args)
        {
            var selfRef = GetPyObj(obj);
            if (selfRef.Ref == null)
            {
                // this might happen when the object is created from .NET
                using var _ = Py.GIL();
                // In the end we decrement the python object's reference count.
                // This doesn't actually destroy the object, it just sets the reference to this object
                // to be a weak reference and it will be destroyed when the C# object is destroyed.
                using var self = CLRObject.GetReference(obj, obj.GetType());
                SetPyObj(obj, self.Borrow());
            }

            // call the base constructor
            obj.GetType().InvokeMember(origCtorName,
                BindingFlags.InvokeMethod,
                null,
                obj,
                args);
        }

        /// <summary>
        /// Called by the .NET finalizer of a Python derived type
        /// </summary>
        /// <returns>
        /// Whether the base type's finalizer runs now. When Python may still reference the Python object,
        /// .NET finalizes the object again once <see cref="Finalize(IntPtr, object, bool)"/> handled it.
        /// </returns>
        public static bool PyFinalize(IPythonDerivedType obj)
        {
            var self = GetPyObj(obj);
            if (ClassDerivedObject.DeferFinalization(self.RawObj, out bool keptReference))
            {
                Finalizer.Instance.AddDerivedFinalizedObject(ref self.RawObj, self.Run, obj, keptReference);
                return false;
            }

            // the C# object is being destroyed which must mean there are no more
            // references to the Python object as well
            Finalizer.Instance.AddDerivedFinalizedObject(ref self.RawObj, self.Run);
            return true;
        }

        /// <param name="derived">The Python object</param>
        /// <param name="deferred">
        /// The .NET object, when <see cref="PyFinalize"/> deferred its finalization
        /// </param>
        /// <param name="keptReference">
        /// The .NET object holds a reference to the Python object, taken in tp_finalize
        /// </param>
        internal static void Finalize(IntPtr derived, object? deferred = null, bool keptReference = false)
        {
            if (deferred is not null)
            {
                var ob = new BorrowedReference(derived);
                bool referencedByPython = Runtime.Refcount(ob) > (keptReference ? 1 : 0);
                if (keptReference)
                {
                    // when it's the last reference, tp_dealloc waits for the .NET object, as for any object
                    Runtime.XDecref(StolenReference.DangerousFromPointer(derived));
                }
                if (referencedByPython)
                {
                    ClassDerivedObject.ReturnToPython(ob, deferred);
                }
                // finalized once nothing references it, which runs the base type's finalizer
                GC.ReRegisterForFinalize(deferred);
                return;
            }

            var @ref = NewReference.DangerousFromPointer(derived);

            ClassBase.tp_clear(@ref.Borrow());

            var type = Runtime.PyObject_TYPE(@ref.Borrow());

            if (!Runtime.HostedInPython || Runtime.TypeManagerInitialized)
            {
                // rare case when it's needed
                // matches correspdonging PyObject_GC_UnTrack
                // in ClassDerivedObject.tp_dealloc
                Runtime.PyObject_GC_Del(@ref.Steal());

                // must decref our type
                Runtime.XDecref(StolenReference.DangerousFromPointer(type.DangerousGetAddress()));
            }
        }

        internal static FieldInfo? GetPyObjField(Type type) => type.GetField(PyObjName, PyObjFlags);

        internal static UnsafeReferenceWithRun GetPyObj(IPythonDerivedType obj)
        {
            FieldInfo fi = GetPyObjField(obj.GetType())!;
            return (UnsafeReferenceWithRun)fi.GetValue(obj);
        }

        internal static void SetPyObj(IPythonDerivedType obj, BorrowedReference pyObj)
        {
            FieldInfo fi = GetPyObjField(obj.GetType())!;
            fi.SetValue(obj, new UnsafeReferenceWithRun(pyObj));
        }
    }
}
