using System;

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
    /// </remarks>
    internal static class AttributeErrorHint
    {
        // The shared __getattr__ function object installed on every eligible type.
        private static PyObject? _getAttr;
        // The managed message builder exposed to Python, kept alive for _getAttr's globals.
        private static PyObject? _messageBuilder;
        // Address of CPython's slot_tp_getattr_hook (extracted from a probe type).
        private static IntPtr _hookSlot;
        // Address of PyObject_GenericGetAttr, used to detect types we may safely redirect.
        private static IntPtr _genericGetAttr;

        private static bool IsReady => _getAttr is not null && _hookSlot != IntPtr.Zero;

        internal static void Initialize()
        {
            try
            {
                _genericGetAttr = Util.ReadIntPtr(Runtime.PyBaseObjectType, TypeOffset.tp_getattro);

                Func<PyObject, string, string> builder = ClassBase.BuildMissingAttributeMessage;
                _messageBuilder = builder.ToPython();

                using var globals = new PyDict();
                Runtime.PyDict_SetItemString(globals.Reference, "__builtins__", Runtime.PyEval_GetBuiltins());
                globals["__clr_attr_msg__"] = _messageBuilder;

                // Define the shared hook, plus a probe class whose tp_getattro is
                // slot_tp_getattr_hook so we can read that function pointer.
                PythonEngine.Exec(
                    "def __clr_getattr__(self, name):\n" +
                    "    raise AttributeError(__clr_attr_msg__(self, name))\n" +
                    "class __clr_getattr_probe__:\n" +
                    "    def __getattr__(self, name):\n" +
                    "        raise AttributeError(name)\n",
                    globals);

                _getAttr = globals["__clr_getattr__"];
                using var probe = globals["__clr_getattr_probe__"];
                _hookSlot = Util.ReadIntPtr(probe.Reference, TypeOffset.tp_getattro);
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

            if (Util.ReadIntPtr(type, TypeOffset.tp_getattro) != _genericGetAttr)
            {
                return;
            }

            if (Runtime.PyObject_SetAttrString(type, "__getattr__", _getAttr!.Reference) != 0)
            {
                Exceptions.Clear();
                return;
            }

            Util.WriteIntPtr(type, TypeOffset.tp_getattro, _hookSlot);
            Runtime.PyType_Modified(type);
        }

        internal static void Shutdown()
        {
            _getAttr?.Dispose();
            _getAttr = null;
            _messageBuilder?.Dispose();
            _messageBuilder = null;
            _hookSlot = IntPtr.Zero;
            _genericGetAttr = IntPtr.Zero;
        }
    }
}
