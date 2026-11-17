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
        string exceptionMessage;
        try
        {
            exceptionMessage = e.Message;
        }
        catch (Exception error)
        {
            Exceptions.SetError(error.InnerException ?? error);
            return default;
        }
        string message;
        if (exceptionMessage != String.Empty)
        {
            message = String.Format("{0}('{1}')", name, exceptionMessage);
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

        string message;
        try
        {
            message = e.ToString();
        }
        catch (Exception error)
        {
            Exceptions.SetError(error.InnerException ?? error);
            return default;
        }
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
        => base.Init(obj, args, kw) && SetArgsAndCause(obj);

    /// <summary>
    /// Sets the args and __cause__ of the constructed exception, raising to Python an error
    /// its members throw, such as a Python override of Message
    /// </summary>
    internal static bool SetArgsAndCause(BorrowedReference obj)
    {
        var e = (CLRObject)GetManagedObject(obj)!;
        try
        {
            return Exceptions.SetArgsAndCause(obj, (Exception)e.inst);
        }
        catch (Exception error)
        {
            Exceptions.SetError(error.InnerException ?? error);
            return false;
        }
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

    // ClassDerivedObject's tp_clear leaves the object to the .NET finalizer, but the exception's own fields
    // (args, __cause__, __context__, __traceback__, __dict__) can form cycles that only clearing them breaks.
    // The wrapper and its handle stay, as the .NET object may still use them.
    public new static int tp_clear(BorrowedReference ob) => BaseUnmanagedClear(ob);

    public override bool Init(BorrowedReference obj, BorrowedReference args, BorrowedReference kw)
        => base.Init(obj, args, kw) && ExceptionClassObject.SetArgsAndCause(obj);
}
