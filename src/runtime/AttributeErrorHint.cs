using System;
using System.Reflection;
using System.Runtime.InteropServices;

using Python.Runtime.Native;

namespace Python.Runtime
{
    /// <summary>
    /// Installs a miss-only <c>__getattr__</c> hook on reflected .NET types so that an
    /// <c>AttributeError</c> raised for a missing attribute is enriched with suggestions
    /// of similarly-named members — without adding any cost to the (common) successful
    /// attribute-access path.
    /// </summary>
    /// <remarks>
    /// CPython only invokes <c>__getattr__</c> after the normal attribute lookup fails,
    /// via the native <c>slot_tp_getattr_hook</c>: on a hit it calls the generic getattr
    /// directly (no managed transition); only on a miss does it call our <c>__getattr__</c>.
    /// pythonnet's metatype does not run CPython's slot-fixup machinery when an attribute
    /// is set on a type, so simply adding <c>__getattr__</c> to the type dict would not
    /// rewire the slot — we therefore wire <c>tp_getattro</c> to the hook manually.
    ///
    /// The <c>__getattr__</c> itself is a native method descriptor (PyDescr_NewMethod)
    /// around a managed thunk, NOT a .NET delegate exposed to Python: delegate calls go
    /// through <see cref="MethodBinder"/>, which releases the GIL around the invocation
    /// (allow_threads), so the callback would run CPython C-API calls off-GIL and crash
    /// whenever the <see cref="Finalizer"/> fires mid-callback. The native thunk is
    /// called directly by the interpreter with the GIL held.
    /// </remarks>
    internal static class AttributeErrorHint
    {
        // Unmanaged PyMethodDef backing the shared __getattr__ method descriptors.
        // Descriptors keep a raw pointer to it (d_method) and can outlive engine
        // shutdown bookkeeping, so it is allocated once and kept for the process
        // lifetime (as are the thunks in Interop.allocatedThunks).
        private static IntPtr _methodDef;
        // Keeps the thunk delegate for GetAttrHook alive.
        private static ThunkInfo? _thunk;
        // Address of CPython's slot_tp_getattr_hook (extracted from a probe type).
        private static IntPtr _hookSlot;
        // Address of PyObject_GenericGetAttr, used to detect types we may safely redirect.
        private static IntPtr _genericGetAttr;
        // Address of type_getattro (PyType_Type.tp_getattro). The CLR metatype uses it, so we
        // allow redirecting it too: that is how attribute access on a reflected type object
        // (static members, enum values) gets the miss hook.
        private static IntPtr _typeGetAttro;

        private static bool IsReady => _methodDef != IntPtr.Zero && _hookSlot != IntPtr.Zero;

        internal static void Initialize()
        {
            try
            {
                _genericGetAttr = Util.ReadIntPtr(Runtime.PyBaseObjectType, TypeOffset.tp_getattro);
                _typeGetAttro = Util.ReadIntPtr(Runtime.PyTypeType, TypeOffset.tp_getattro);

                if (_methodDef == IntPtr.Zero)
                {
                    _thunk = Interop.GetThunk(typeof(AttributeErrorHint).GetMethod(
                        nameof(GetAttrHook), BindingFlags.Static | BindingFlags.Public)!);
                    IntPtr methodDef = Marshal.AllocHGlobal(4 * IntPtr.Size);
                    TypeManager.WriteMethodDef(methodDef, "__getattr__", _thunk.Address);
                    _methodDef = methodDef;
                }

                // Define a probe class whose tp_getattro is slot_tp_getattr_hook so we
                // can read that function pointer.
                using var globals = new PyDict();
                Runtime.PyDict_SetItemString(globals.Reference, "__builtins__", Runtime.PyEval_GetBuiltins());
                PythonEngine.Exec(
                    "class __clr_getattr_probe__:\n" +
                    "    def __getattr__(self, name):\n" +
                    "        raise AttributeError(name)\n",
                    globals);

                using var probe = globals["__clr_getattr_probe__"];
                _hookSlot = Util.ReadIntPtr(probe.Reference, TypeOffset.tp_getattro);

                // Install the hook on the CLR metatype so that a miss on a reflected type
                // object's own attribute (a mistyped static member or enum value, e.g.
                // DayOfWeek.Sundey) is enriched the same way instance attribute misses are.
                Install(MetaType.ClrMetaTypeReference);
            }
            catch (Exception e)
            {
                // Degrade gracefully: without the hook, AttributeError messages are simply
                // not enriched. Never let this break interpreter initialization.
                DebugUtil.Print($"AttributeErrorHint.Initialize failed: {e}");
                Shutdown();
            }
        }

        /// <summary>
        /// Wires the miss-only hook onto <paramref name="type"/> if it still uses the
        /// native generic getattr. Types with a custom <c>tp_getattro</c> (dynamic
        /// objects, modules, interfaces, ...) handle misses themselves and are left
        /// untouched; derived types that inherit an already-hooked base are likewise
        /// skipped, since they inherit the behavior through the MRO.
        /// </summary>
        internal static void Install(BorrowedReference type)
        {
            if (!IsReady)
            {
                return;
            }

            var getattro = Util.ReadIntPtr(type, TypeOffset.tp_getattro);
            // Only redirect types that still use one of the standard lookups: instances use the
            // generic getattr, the CLR metatype uses type_getattro. Types with a custom
            // tp_getattro (dynamic objects, modules, interfaces, ...) handle misses themselves
            // and are left untouched.
            if (getattro != _genericGetAttr && getattro != _typeGetAttro)
            {
                return;
            }

            using var descr = Runtime.PyDescr_NewMethod(type, _methodDef);
            if (descr.IsNull())
            {
                Exceptions.Clear();
                return;
            }

            BorrowedReference dict = Util.ReadRef(type, TypeOffset.tp_dict);
            if (Runtime.PyDict_SetItemString(dict, "__getattr__", descr.Borrow()) != 0)
            {
                Exceptions.Clear();
                return;
            }

            Util.WriteIntPtr(type, TypeOffset.tp_getattro, _hookSlot);
            Runtime.PyType_Modified(type);
        }

        /// <summary>
        /// The <c>__getattr__(self, name)</c> implementation (METH_VARARGS). CPython's
        /// <c>slot_tp_getattr_hook</c> only calls it after the normal lookup has failed
        /// and the original AttributeError has been cleared, so the full message is
        /// rebuilt here. Runs as a direct native method call with the GIL held.
        /// </summary>
        public static NewReference GetAttrHook(BorrowedReference ob, BorrowedReference args)
        {
            string? name = null;
            string message;
            try
            {
                if (Runtime.PyTuple_Size(args) == 1)
                {
                    BorrowedReference key = Runtime.PyTuple_GetItem(args, 0);
                    if (Runtime.PyString_Check(key))
                    {
                        name = Runtime.GetManagedString(key);
                    }
                }

                using var self = new PyObject(ob);
                message = ClassBase.BuildMissingAttributeMessage(self, name ?? "?");
            }
            catch
            {
                // Never let message building turn into a different exception.
                message = $"object has no attribute '{name ?? "?"}'";
            }

            Exceptions.SetError(Exceptions.AttributeError, message);
            return default;
        }

        internal static void Shutdown()
        {
            // _methodDef and _thunk are deliberately kept: method descriptors created
            // from them may still be reachable during interpreter teardown, and both
            // are reused by the next Initialize.
            _hookSlot = IntPtr.Zero;
            _genericGetAttr = IntPtr.Zero;
            _typeGetAttro = IntPtr.Zero;
        }
    }
}
