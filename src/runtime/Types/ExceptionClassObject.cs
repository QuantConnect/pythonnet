using System;
using System.Reflection;

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
        try
        {
            string name = e.GetType().Name;
            string message = e.Message;
            if (!string.IsNullOrEmpty(message))
            {
                return Runtime.PyString_FromString(String.Format("{0}('{1}')", name, message));
            }
            return Runtime.PyString_FromString(String.Format("{0}()", name));
        }
        catch (Exception error)
        {
            SetError(error);
            return default;
        }
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

        // a None returned by a Python override of ToString reads as an empty message
        try
        {
            string message = e.ToString() ?? string.Empty;
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
        catch (Exception error)
        {
            SetError(error);
            return default;
        }
    }

    /// <summary>
    /// Raises to Python the error of an exception member, as it is: a Python override of the member
    /// throws its PythonException directly, whose InnerException is its cause, while a member the
    /// Python subclass inherits runs through reflection, which wraps what it throws once per
    /// generated level it goes through
    /// </summary>
    static void SetError(Exception error)
    {
        while (error is TargetInvocationException { InnerException: { } inner })
        {
            error = inner;
        }
        Exceptions.SetError(error);
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
            SetError(error);
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
