using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

using static Python.Runtime.PythonException;

namespace Python.Runtime;

[Serializable]
internal sealed class ReflectedClrType : PyType
{
    private ReflectedClrType(StolenReference reference) : base(reference, prevalidated: true) { }
    internal ReflectedClrType(ReflectedClrType original) : base(original, prevalidated: true) { }
    ReflectedClrType(SerializationInfo info, StreamingContext context) : base(info, context) { }

    internal ClassBase Impl => (ClassBase)ManagedType.GetManagedObject(this)!;

    /// <summary>
    /// Get the Python type that reflects the given CLR type.
    /// </summary>
    /// <remarks>
    /// Returned <see cref="ReflectedClrType"/> might be partially initialized.
    /// If you need fully initialized type, use <see cref="GetOrInitialize(ClassBase, Type)"/>
    /// </remarks>
    public static ReflectedClrType GetOrCreate(Type type)
    {
        if (ClassManager.cache.TryGetValue(type, out var pyType))
        {
            return pyType;
        }

        // Ensure, that matching Python type exists first.
        // It is required for self-referential classes
        // (e.g. with members, that refer to the same class)
        pyType = AllocateClass(type);
        ClassManager.cache.Add(type, pyType);

        var impl = ClassManager.CreateClass(type);

        TypeManager.InitializeClassCore(type, pyType, impl);

        ClassManager.InitClassBase(type, impl, pyType);

        // Now we force initialize the Python type object to reflect the given
        // managed type, filling the Python type slots with thunks that
        // point to the managed methods providing the implementation.
        TypeManager.InitializeClass(pyType, impl, type);

        return pyType;
    }

    internal void Restore(Dictionary<string, object?> context)
    {
        var cb = (ClassBase)context["impl"]!;

        Debug.Assert(cb is not null);

        cb!.Load(this, context);

        Restore(cb);
    }

    internal void Restore(ClassBase cb)
    {
        ClassManager.InitClassBase(cb.type.Value, cb, this);

        TypeManager.InitializeClass(this, cb, cb.type.Value);
    }

    internal static NewReference CreateSubclass(ClassBase baseClass,
                                                string name, string? assembly, string? ns,
                                                BorrowedReference dict)
    {
        try
        {
            Type subType = ClassDerivedObject.CreateDerivedType(name,
                baseClass.type.Value,
                dict,
                ns,
                assembly);

            var py_type = GetOrCreate(subType);

            // by default the class dict will have all the C# methods in it, but as this is a
            // derived class we want the python overrides in there instead if they exist.
            var cls_dict = Util.ReadRef(py_type, TypeOffset.tp_dict);
            ThrowIfIsNotZero(Runtime.PyDict_Update(cls_dict, dict));

            // The generated type's overrides of its base's virtual methods would hide the Python overrides of
            // a Python base class (a __namespace__ subclass of a __namespace__ subclass), so let lookups of
            // those reach the Python overrides, as Python's method resolution does.
            var pyBaseType = Util.ReadRef(py_type, TypeOffset.tp_base);
            var baseVirtualMethods = baseClass.type.Value.GetMethods()
                .Where(m => m.IsVirtual && !m.IsFinal)
                .Select(m => m.Name)
                .ToHashSet();
            using var clsDict = new PyDict(cls_dict);
            using var keys = clsDict.Keys();
            foreach (PyObject key in keys)
            {
                using (key)
                {
                    var inherited = Runtime._PyType_Lookup(pyBaseType, key.Reference);
                    if (inherited.IsNull
                        || ManagedType.GetManagedObject(inherited) != null
                        || !Runtime.PyDict_GetItem(dict, key.Reference).IsNull
                        || ManagedType.GetManagedObject(Runtime.PyDict_GetItem(cls_dict, key.Reference)) is not MethodObject method)
                    {
                        continue;
                    }

                    // a generated forwarder, possibly among overloads the generated type doesn't override (which
                    // the parent's Python override replaces too), but not the members the class body declares for .NET
                    var hasForwarder = false;
                    var declaredInBody = false;
                    foreach (var member in method.info)
                    {
                        hasForwarder |= member is MethodInfo { IsVirtual: true, IsFinal: false }
                            && member.DeclaringType == subType
                            && baseVirtualMethods.Contains(member.Name);
                        declaredInBody |= !Runtime.PyDict_GetItemString(dict, member.Name).IsNull;
                    }
                    if (hasForwarder && !declaredInBody)
                    {
                        ThrowIfIsNotZero(Runtime.PyDict_DelItem(cls_dict, key.Reference));
                    }
                }
            }

            // Update the __classcell__ if it exists
            BorrowedReference cell = Runtime.PyDict_GetItemString(cls_dict, "__classcell__");
            if (!cell.IsNull)
            {
                ThrowIfIsNotZero(Runtime.PyCell_Set(cell, py_type));
                ThrowIfIsNotZero(Runtime.PyDict_DelItemString(cls_dict, "__classcell__"));
            }

            return new NewReference(py_type);
        }
        catch (Exception e)
        {
            return Exceptions.RaiseTypeError(e.Message);
        }
    }

    static ReflectedClrType AllocateClass(Type clrType)
    {
        string name = TypeManager.GetPythonTypeName(clrType);

        var type = TypeManager.AllocateTypeObject(name, Runtime.PyCLRMetaType);
        type.Flags = TypeFlags.Default
                        | TypeFlags.HasClrInstance
                        | TypeFlags.HeapType
                        | TypeFlags.BaseType
                        | TypeFlags.HaveGC;

        return new ReflectedClrType(type.Steal());
    }

    public override bool Equals(PyObject? other) => other != null && rawPtr == other.rawPtr;
    public override int GetHashCode() => rawPtr.GetHashCode();
}
