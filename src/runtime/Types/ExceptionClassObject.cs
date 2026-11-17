using System;

namespace Python.Runtime;

/// <summary>
/// Base class for Python types that reflect managed exceptions based on
/// System.Exception
/// </summary>
[Serializable]
internal class ExceptionClassObject : ClassObject
{
    internal ExceptionClassObject(Type tp) : base(tp)
    {
    }

    internal static Exception? ToException(BorrowedReference ob)
    {
        var co = GetManagedObject(ob) as CLRObject;
        return co?.inst as Exception;
    }

    /// <summary>
    /// Exception __repr__ implementation
    /// </summary>
    public new static NewReference tp_repr(BorrowedReference ob)
    {
        Exception? e = ToException(ob);
        if (e == null)
        {
            return Exceptions.RaiseTypeError("invalid object");
        }
        string name = e.GetType().Name;
        string message;
        if (e.Message != String.Empty)
        {
            message = String.Format("{0}('{1}')", name, e.Message);
        }
        else
        {
            message = String.Format("{0}()", name);
        }
        return Runtime.PyString_FromString(message);
    }

    /// <summary>
    /// Exception __str__ implementation
    /// </summary>
    public new static NewReference tp_str(BorrowedReference ob)
    {
        Exception? e = ToException(ob);
        if (e == null)
        {
            return Exceptions.RaiseTypeError("invalid object");
        }

        string message = e.ToString();
        string fullTypeName = e.GetType().FullName;
        string prefix = fullTypeName + ": ";
        if (message.StartsWith(prefix))
        {
            message = message.Substring(prefix.Length);
        }
        else if (message.StartsWith(fullTypeName))
        {
            message = message.Substring(fullTypeName.Length);
        }
        return Runtime.PyString_FromString(message);
    }

    public override bool Init(BorrowedReference obj, BorrowedReference args, BorrowedReference kw)
    {
        if (!base.Init(obj, args, kw)) return false;

        var e = (CLRObject)GetManagedObject(obj)!;

        return Exceptions.SetArgsAndCause(obj, (Exception)e.inst);
    }
}

/// <summary>
/// Implementation for the managed types generated for Python subclasses of exceptions
/// that declare __namespace__: the generated object keeps its Python object as derived
/// types do, while it still reads as an exception from Python.
/// </summary>
[Serializable]
internal class ExceptionClassDerivedObject : ClassDerivedObject
{
    internal ExceptionClassDerivedObject(Type tp) : base(tp)
    {
    }

    // Type slots are looked up on the implementing type's hierarchy, which here is ClassDerivedObject's
    public new static NewReference tp_repr(BorrowedReference ob) => ExceptionClassObject.tp_repr(ob);

    public new static NewReference tp_str(BorrowedReference ob) => ExceptionClassObject.tp_str(ob);

    public override bool Init(BorrowedReference obj, BorrowedReference args, BorrowedReference kw)
    {
        if (!base.Init(obj, args, kw)) return false;

        var e = (CLRObject)GetManagedObject(obj)!;

        return Exceptions.SetArgsAndCause(obj, (Exception)e.inst);
    }
}
