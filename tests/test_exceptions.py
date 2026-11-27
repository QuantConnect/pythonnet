# -*- coding: utf-8 -*-

"""Test exception support."""

import os
import subprocess
import sys

import System
import pytest
import pickle

# begin code from https://utcc.utoronto.ca/~cks/space/blog/python/GetAllObjects
import gc
# Recursively expand slist's objects
# into olist, using seen to track
# already processed objects.

def _getr(slist, olist, seen):
    for e in slist:
      if id(e) in seen:
        continue
      seen[id(e)] = None
      olist.append(e)
      tl = gc.get_referents(e)
      if tl:
        _getr(tl, olist, seen)

# The public function.
def get_all_objects():
    gcl = gc.get_objects()
    olist = []
    seen = {}
    # Just in case:
    seen[id(gcl)] = None
    seen[id(olist)] = None
    seen[id(seen)] = None
    # _getr does the real work.
    _getr(gcl, olist, seen)
    return olist
# end code from https://utcc.utoronto.ca/~cks/space/blog/python/GetAllObjects

def leak_check(func):
    def do_leak_check():
        func()
        gc.collect()
        exc = {x for x in get_all_objects() if isinstance(x, Exception) and not isinstance(x, pytest.PytestDeprecationWarning)}
        print(len(exc))
        if len(exc):
            for x in exc:
                print('-------')
                print(repr(x))
                print(gc.get_referrers(x))
                print(len(gc.get_referrers(x)))
            assert False
    gc.collect()
    return do_leak_check

def test_unified_exception_semantics():
    """Test unified exception semantics."""
    e = System.Exception('Something bad happened')
    assert isinstance(e, Exception)
    assert isinstance(e, System.Exception)


def test_standard_exception_attributes():
    """Test accessing standard exception attributes."""
    from System import OverflowException
    from Python.Test import ExceptionTest

    e = ExceptionTest.GetExplicitException()
    assert isinstance(e, OverflowException)

    assert e.Message == 'error'

    e.Source = 'Test Suite'
    assert e.Source == 'Test Suite'

    v = e.ToString()
    assert len(v) > 0


def test_extended_exception_attributes():
    """Test accessing extended exception attributes."""
    from Python.Test import ExceptionTest, ExtendedException
    from System import OverflowException

    e = ExceptionTest.GetExtendedException()
    assert isinstance(e, ExtendedException)
    assert isinstance(e, OverflowException)
    assert isinstance(e, System.Exception)

    assert e.Message == 'error'

    e.Source = 'Test Suite'
    assert e.Source == 'Test Suite'

    v = e.ToString()
    assert len(v) > 0

    assert e.ExtraProperty == 'extra'
    e.ExtraProperty = 'changed'
    assert e.ExtraProperty == 'changed'

    assert e.GetExtraInfo() == 'changed'


def test_raise_class_exception():
    """Test class exception propagation."""
    from System import NullReferenceException

    with pytest.raises(NullReferenceException) as cm:
        raise NullReferenceException

    exc = cm.value
    assert isinstance(exc, NullReferenceException)


def test_exc_info():
    """Test class exception propagation.
    Behavior of exc_info changed in Py3. Refactoring its test"""
    from System import NullReferenceException
    try:
        raise NullReferenceException("message")
    except Exception as exc:
        type_, value, tb = sys.exc_info()
        assert type_ is NullReferenceException
        assert value.Message == "message"
        assert exc.Message == "message"
        # FIXME: Lower-case message isn't implemented
        # self.assertTrue(exc.message == "message")
        assert value is exc


def test_raise_class_exception_with_value():
    """Test class exception propagation with associated value."""
    from System import NullReferenceException

    with pytest.raises(NullReferenceException) as cm:
        raise NullReferenceException('Aiiieee!')

    exc = cm.value
    assert isinstance(exc, NullReferenceException)
    assert exc.Message == 'Aiiieee!'


def test_raise_instance_exception():
    """Test instance exception propagation."""
    from System import NullReferenceException

    with pytest.raises(NullReferenceException) as cm:
        raise NullReferenceException()

    exc = cm.value
    assert isinstance(exc, NullReferenceException)
    assert len(exc.Message) > 0


def test_raise_instance_exception_with_args():
    """Test instance exception propagation with args."""
    from System import NullReferenceException

    with pytest.raises(NullReferenceException) as cm:
        raise NullReferenceException("Aiiieee!")

    exc = cm.value
    assert isinstance(exc, NullReferenceException)
    assert exc.Message == 'Aiiieee!'

def test_managed_exception_propagation():
    """Test propagation of exceptions raised in managed code."""
    from System import Decimal, DivideByZeroException

    with pytest.raises(DivideByZeroException):
        Decimal.Divide(1, 0)

def test_managed_exception_conversion():
    """Test conversion of managed exceptions."""
    from System import OverflowException
    from Python.Test import ExceptionTest

    e = ExceptionTest.GetBaseException()
    assert isinstance(e, System.Exception)

    e = ExceptionTest.GetExplicitException()
    assert isinstance(e, OverflowException)
    assert isinstance(e, System.Exception)

    e = ExceptionTest.GetWidenedException()
    assert isinstance(e, OverflowException)
    assert isinstance(e, System.Exception)

    v = ExceptionTest.SetBaseException(System.Exception('error'))
    assert v

    v = ExceptionTest.SetExplicitException(OverflowException('error'))
    assert v

    v = ExceptionTest.SetWidenedException(OverflowException('error'))
    assert v


def test_catch_exception_from_managed_method():
    """Test catching an exception from a managed method."""
    from Python.Test import ExceptionTest
    from System import OverflowException

    with pytest.raises(OverflowException) as cm:
        ExceptionTest().ThrowException()

    e = cm.value
    assert isinstance(e, OverflowException)


def test_catch_exception_from_managed_property():
    """Test catching an exception from a managed property."""
    from Python.Test import ExceptionTest
    from System import OverflowException

    with pytest.raises(OverflowException) as cm:
        _ = ExceptionTest().ThrowProperty

    e = cm.value
    assert isinstance(e, OverflowException)

    with pytest.raises(OverflowException) as cm:
        ExceptionTest().ThrowProperty = 1

    e = cm.value
    assert isinstance(e, OverflowException)


def test_catch_exception_managed_class():
    """Test catching the managed class of an exception."""
    from System import OverflowException

    with pytest.raises(OverflowException):
        raise OverflowException('overflow')


def test_catch_exception_python_class():
    """Test catching the python class of an exception."""
    from System import OverflowException

    with pytest.raises(Exception):
        raise OverflowException('overflow')


def test_catch_exception_base_class():
    """Test catching the base of an exception."""
    from System import OverflowException, ArithmeticException

    with pytest.raises(ArithmeticException):
        raise OverflowException('overflow')


def test_catch_exception_nested_base_class():
    """Test catching the nested base of an exception."""
    from System import OverflowException, SystemException

    with pytest.raises(SystemException):
        raise OverflowException('overflow')


def test_catch_exception_with_assignment():
    """Test catching an exception with assignment."""
    from System import OverflowException

    with pytest.raises(OverflowException) as cm:
        raise OverflowException('overflow')

    e = cm.value
    assert isinstance(e, OverflowException)


def test_catch_exception_unqualified():
    """Test catching an unqualified exception."""
    from System import OverflowException

    try:
        raise OverflowException('overflow')
    except:
        pass
    else:
        self.fail("failed to catch unqualified exception")


def test_catch_baseexception():
    """Test catching an unqualified exception with BaseException."""
    from System import OverflowException

    with pytest.raises(BaseException):
        raise OverflowException('overflow')


def test_apparent_module_of_exception():
    """Test the apparent module of an exception."""
    from System import OverflowException

    assert System.Exception.__module__ == 'System'
    assert OverflowException.__module__ == 'System'


def test_str_of_exception():
    """Test the str() representation of an exception."""
    from System import NullReferenceException, Convert, FormatException

    e = NullReferenceException('')
    assert str(e) == ''

    e = NullReferenceException('Something bad happened')
    assert str(e).startswith('Something bad happened')

    with pytest.raises(FormatException) as cm:
        Convert.ToDateTime('this will fail')


def test_python_compat_of_managed_exceptions():
    """Test managed exceptions compatible with Python's implementation"""
    from System import OverflowException
    msg = "Simple message"

    e = OverflowException(msg)
    assert str(e) == msg

    assert e.args == (msg,)
    assert isinstance(e.args, tuple)
    strexp = "OverflowException('Simple message"
    assert repr(e)[:len(strexp)] == strexp


def test_exception_is_instance_of_system_object():
    """Test behavior of isinstance(<managed exception>, System.Object)."""
    # This is an anti-test, in that this is a caveat of the current
    # implementation. Because exceptions are not allowed to be new-style
    # classes, we wrap managed exceptions in a general-purpose old-style
    # class that delegates to the wrapped object. This makes _almost_
    # everything work as expected, except that an isinstance check against
    # System.Object will fail for a managed exception (because a new
    # style class cannot appear in the __bases__ of an old-style class
    # without causing a crash in the CPython interpreter). This test is
    # here mainly to remind me to update the caveat in the documentation
    # one day when when exceptions can be new-style classes.

    # This behavior is now over-shadowed by the implementation of
    # __instancecheck__ (i.e., overloading isinstance), so for all Python
    # version >= 2.6 we expect isinstance(<managed exception>, Object) to
    # be true, even though it does not really subclass Object.
    from System import OverflowException, Object

    o = OverflowException('error')

    if sys.version_info >= (2, 6):
        assert isinstance(o, Object)
    else:
        assert not isinstance(o, Object)


def test_pickling_exceptions():
    exc = System.Exception("test")
    dumped = pickle.dumps(exc)
    loaded = pickle.loads(dumped)

    assert exc.args == loaded.args


def test_chained_exceptions():
    from Python.Test import ExceptionTest

    with pytest.raises(Exception) as cm:
        ExceptionTest.ThrowChainedExceptions()

    exc = cm.value

    msgs = ("Outer exception",
            "Inner exception",
            "Innermost exception",)
    for msg in msgs:
        assert exc.Message == msg
        assert exc.__cause__ == exc.InnerException
        exc = exc.__cause__

def test_iteration_exception():
    from Python.Test import ExceptionTest
    from System import OverflowException

    exception = OverflowException("error")

    val = ExceptionTest.ThrowExceptionInIterator(exception).__iter__()
    assert next(val) == 1
    assert next(val) == 2
    with pytest.raises(OverflowException) as cm:
        next(val)

    exc = cm.value

    assert exc == exception

    # after exception is thrown iterator is no longer valid
    with pytest.raises(StopIteration):
        next(val)


def test_iteration_innerexception():
    from Python.Test import ExceptionTest
    from System import OverflowException

    exception = System.Exception("message", OverflowException("error"))

    val = ExceptionTest.ThrowExceptionInIterator(exception).__iter__()
    assert next(val) == 1
    assert next(val) == 2
    with pytest.raises(OverflowException) as cm:
        next(val)

    exc = cm.value

    assert exc == exception.InnerException

    # after exception is thrown iterator is no longer valid
    with pytest.raises(StopIteration):
        next(val)

def leak_test(func):
    def do_test_leak():
        # PyTest leaks things, gather the current state
        orig_exc = {x for x in get_all_objects() if isinstance(x, Exception)}
        func()
        exc = {x for x in get_all_objects() if isinstance(x, Exception)}
        possibly_leaked = exc - orig_exc
        assert not possibly_leaked

    return do_test_leak

@leak_test
def test_dont_leak_exceptions_simple():
    from Python.Test import ExceptionTest

    try:
        ExceptionTest.DoThrowSimple()
    except System.ArgumentException:
        print('type error, as expected')

@leak_test
def test_dont_leak_exceptions_inner():
    from Python.Test import ExceptionTest
    try:
        ExceptionTest.DoThrowWithInner()
    except TypeError:
        print('type error, as expected')
    except System.ArgumentException:
        print('type error, also expected')


_NAMESPACED_EXCEPTION_SCRIPT_SETUP = """
import os, sys
bin_path = sys.argv[1]
sys.path.append(bin_path)
from clr_loader import get_coreclr
from pythonnet import set_runtime
set_runtime(get_coreclr(runtime_config=os.path.join(bin_path, "Python.Test.runtimeconfig.json")))
import clr
clr.AddReference("Python.Test")
import gc
from Python.Test import ExtendedException
from System import GC, Object
"""


def _run_namespaced_exception_script(script):
    """Runs the script in a subprocess, so a native crash fails the test instead of the test run"""
    from Python.Test import ExtendedException

    bin_path = os.path.dirname(ExtendedException().GetType().Assembly.Location)
    result = subprocess.run([sys.executable, "-c", _NAMESPACED_EXCEPTION_SCRIPT_SETUP + script, bin_path],
                            capture_output=True, text=True, timeout=300)

    assert result.returncode == 0, result.stderr
    return result.stdout.splitlines()


_NAMESPACED_EXCEPTION_SCRIPT = """
from System import Exception as ClrException
from System.Collections.Generic import List

class WithoutInit(ExtendedException):
    __namespace__ = "Python.Test.NamespacedExceptions"

class InitWithoutSuper(ExtendedException):
    __namespace__ = "Python.Test.NamespacedExceptions"
    def __init__(self):
        pass
    def ToString(self):
        return "overridden"

class InitWithSuper(ExtendedException):
    __namespace__ = "Python.Test.NamespacedExceptions"
    def __init__(self):
        super().__init__("message")

def check(cls):
    error = cls()
    held = List[Object]()
    held.Add(error)
    try:
        raise error
    except ExtendedException as caught:
        raised = caught is error
    outer = ClrException("outer", error)
    print(cls.__name__, error.extra, held[0] is error, raised, str(error), error.args, outer.__cause__ is error)

check(WithoutInit)
check(InitWithoutSuper)
check(InitWithSuper)
gc.collect()
for _ in range(2):
    GC.Collect()
    GC.WaitForPendingFinalizers()
print("collected")
"""


@pytest.mark.skipif(os.environ.get("PYTHONNET_RUNTIME") != "netcore", reason="loads the netcore runtime")
def test_namespaced_exception_subclass_construction():
    """A Python subclass of a .NET exception declaring __namespace__ runs its base
    constructor, keeps its Python object for .NET and its overrides, still reads
    as an exception, and is itself the __cause__ of an exception it is the inner one of"""
    output = _run_namespaced_exception_script(_NAMESPACED_EXCEPTION_SCRIPT)

    overflow = "Arithmetic operation resulted in an overflow."
    assert output[-4:] == [
        f"WithoutInit extra True True {overflow} ('{overflow}',) True",
        f"InitWithoutSuper extra True True overridden ('{overflow}',) True",
        "InitWithSuper extra True True message ('message',) True",
        "collected",
    ]


_NAMESPACED_ROUND_TRIP_SCRIPT = """
from System import Exception as ClrException
from System.Collections.Generic import List
from System import String
from System.Threading.Tasks import Task

class Plain(Object):
    __namespace__ = "Python.Test.RoundTrips"
    def __init__(self):
        self.tag = "plain"

class Inner(ExtendedException):
    __namespace__ = "Python.Test.RoundTrips"
    def __init__(self):
        super().__init__("inner")
        self.tag = "inner"

class ManagedPlain(Object):
    __namespace__ = "Python.Test.RoundTrips"
    def ToString(self):
        return self.GetType().Name

class ManagedMessage(ExtendedException):
    __namespace__ = "Python.Test.RoundTrips"
    def __init__(self):
        super().__init__("unused")
    def get_Message(self):
        return self.GetType().Name

def collect():
    gc.collect()
    GC.Collect()
    GC.WaitForPendingFinalizers()

held = List[Object]()
held.Add(Plain())
held.Add(Inner())
task = Task.FromException(Inner())
managed = List[Object]()
managed.Add(ManagedPlain())
managed_task = Task.FromException(ManagedMessage())
cyclic = Inner()
cyclic.link = cyclic
cyclic_task = Task.FromException(cyclic)
del cyclic

from System.Runtime.Serialization import ISerializable

class Hashed(ExtendedException):
    __namespace__ = "Python.Test.RoundTrips"
    armed = False
    def __init__(self):
        pass
    def GetHashCode(self):
        return 7
    def get_Message(self):
        if Hashed.armed:
            raise ValueError("armed")
        return "hashed"

hashed = Hashed()
hashed_raw = ISerializable(hashed).__raw_implementation__
hashed.link = hashed
del hashed
collect()

# a .NET call into an override doesn't read its other members, which may raise
Hashed.armed = True
print(hash(hashed_raw))

# .NET calls the Python overrides, which read managed members, before Python gets the objects back
print(String.Join(",", managed.ToArray()), managed_task.Exception.__cause__.Message)
print(cyclic_task.Exception.__cause__.args)

plain, inner = held[0], held[1]
print(type(plain).__name__, plain.tag, held[0] is plain)
print(type(inner).__name__, inner.tag, str(inner), held[1] is inner)
cause = task.Exception.__cause__
print(type(cause).__name__, cause.tag, str(cause), cause.args)
del plain, inner, cause, task, managed_task, cyclic_task
collect()
print("collected")
"""


@pytest.mark.skipif(os.environ.get("PYTHONNET_RUNTIME") != "netcore", reason="loads the netcore runtime")
def test_namespaced_subclass_round_trip_after_gc():
    """A __namespace__ subclass that Python dropped, and .NET garbage collected around
    while still holding it, comes back to Python as its own working Python object,
    from a .NET collection and as an exception's __cause__, with its args also after Python's
    GC cleared them in a cycle. .NET calls into its Python overrides can use its managed members,
    and don't read its other members"""
    output = _run_namespaced_exception_script(_NAMESPACED_ROUND_TRIP_SCRIPT)

    assert output[-7:] == [
        "7",
        "ManagedPlain ManagedMessage",
        "('inner',)",
        "Plain plain True",
        "Inner inner inner True",
        "Inner inner inner ('inner',)",
        "collected",
    ]


_NAMESPACED_EXCEPTION_RELEASE_SCRIPT = """
class Probe(Object):
    __namespace__ = "Python.Test.ReleasedExceptions"
    def ToString(self):
        return "probe"

class ParentError(ExtendedException):
    __namespace__ = "Python.Test.ReleasedExceptions"
    def __init__(self):
        pass

class ChildError(ParentError):
    __namespace__ = "Python.Test.ReleasedExceptions"
    def __init__(self):
        pass

class CycleError(ExtendedException):
    __namespace__ = "Python.Test.ReleasedExceptions"
    def __init__(self):
        pass

def release():
    for _ in range(3):
        gc.collect()
        GC.Collect()
        GC.WaitForPendingFinalizers()
        # each call from .NET into the Python override creates Python objects, which
        # periodically release the queued Python objects of finalized .NET objects
        for _ in range(300):
            str(probe)

probe = Probe()

for _ in range(10):
    ChildError()
release()
print("released")

for _ in range(10):
    error = CycleError()
    error.__cause__ = error
    error = CycleError()
    error.__context__ = error
    error = CycleError()
    error.args = (error,)
del error
release()
print(sum(1 for o in gc.get_objects() if type(o) is CycleError))

from System.Runtime.Serialization import ISerializable

class RawError(ExtendedException):
    __namespace__ = "Python.Test.ReleasedExceptions"
    def __init__(self):
        pass

class FailingRawError(ExtendedException):
    __namespace__ = "Python.Test.ReleasedExceptions"
    def __init__(self):
        self.armed = False
    def get_Message(self):
        if self.armed:
            raise ValueError("armed")
        return "message"

raw = RawError()
failing_raw = FailingRawError()
failing_interface = ISerializable(failing_raw)
failing_raw.armed = True
def convert_raw():
    for _ in range(1000):
        ISerializable(raw).__raw_implementation__
        try:
            failing_interface.__raw_implementation__
        except ValueError:
            pass
    release()
    return sys.getallocatedblocks()

convert_raw()
before = convert_raw()
# a raw conversion's own Python object isn't kept for .NET, which only knows the registered one,
# nor is one whose Message override raised
print(convert_raw() - before < 1000)

from Python.Test import AbstractFinalizerCallbackTest, VirtualFinalizerCallbackTest

class AbstractCallback(AbstractFinalizerCallbackTest):
    __namespace__ = "Python.Test.ReleasedExceptions"
    def Callback(self):
        return 42

class AbstractCallbackChild(AbstractCallback):
    __namespace__ = "Python.Test.ReleasedExceptions"
    def Callback(self):
        return 43

class VirtualCallback(VirtualFinalizerCallbackTest):
    __namespace__ = "Python.Test.ReleasedExceptions"
    def Callback(self):
        return 42

class VirtualCallbackChild(VirtualCallback):
    __namespace__ = "Python.Test.ReleasedExceptions"
    def Callback(self):
        return 43

for cls, base in ((AbstractCallback, AbstractFinalizerCallbackTest),
                  (AbstractCallbackChild, AbstractFinalizerCallbackTest),
                  (VirtualCallback, VirtualFinalizerCallbackTest),
                  (VirtualCallbackChild, VirtualFinalizerCallbackTest)):
    base.Result = 0
    cls()
    release()
    print(cls.__name__, base.Result)

"""


@pytest.mark.skipif(os.environ.get("PYTHONNET_RUNTIME") != "netcore", reason="loads the netcore runtime")
def test_namespaced_exception_subclass_release():
    """A __namespace__ exception subclass of another one is released once, exceptions
    referencing themselves through args, __cause__ or __context__ are collected, as are
    the Python objects of raw conversions, also of failed ones, and the finalizer of a .NET base class still reaches the Python overrides of its
    __namespace__ subclasses, before their Python object is released"""
    output = _run_namespaced_exception_script(_NAMESPACED_EXCEPTION_RELEASE_SCRIPT)

    assert output[-7:] == [
        "released",
        "0",
        "True",
        "AbstractCallback 42",
        "AbstractCallbackChild 43",
        "VirtualCallback 42",
        "VirtualCallbackChild 43",
    ]


_NAMESPACED_EXCEPTION_OVERRIDES_SCRIPT = """
class OverridingError(ExtendedException):
    __namespace__ = "Python.Test.OverridingExceptions"
    def __init__(self, failing=False):
        self.failing = failing
        if failing:
            super().__init__()
    def fail(self):
        raise ValueError("outer") from KeyError("inner")
    def get_Message(self):
        if self.failing:
            self.fail()
        return "message"
    def ToString(self):
        if self.failing:
            self.fail()
        return None

error = OverridingError()
print(repr(str(error)), repr(error))
error.failing = True

def check(name, call):
    try:
        call()
    except ValueError as raised:
        print(name, raised, type(raised.__cause__).__name__, raised.__cause__)

check("str", lambda: str(error))
check("repr", lambda: repr(error))
check("init", lambda: OverridingError(failing=True))

class InheritedToStringError(ExtendedException):
    __namespace__ = "Python.Test.OverridingExceptions"
    def __init__(self):
        self.failing = False
    def get_Message(self):
        if self.failing:
            raise ValueError("outer") from KeyError("inner")
        return "message"

inherited = InheritedToStringError()
inherited.failing = True
check("inherited str", lambda: str(inherited))

class TwoLevelInheritedToStringError(InheritedToStringError):
    __namespace__ = "Python.Test.OverridingExceptions"
    def __init__(self):
        self.failing = False
    def get_Message(self):
        if self.failing:
            raise ValueError("outer") from KeyError("inner")
        return "message"

two_level = TwoLevelInheritedToStringError()
two_level.failing = True
check("two-level inherited str", lambda: str(two_level))

class ReadOnceError(ExtendedException):
    __namespace__ = "Python.Test.OverridingExceptions"
    def __init__(self):
        self.reads = 0
        super().__init__()
    def get_Message(self):
        self.reads += 1
        if self.reads > 1:
            raise ValueError("read again")
        return "once"

read_once = ReadOnceError()
print(read_once.args)

class ArgsLookupError(ExtendedException):
    __namespace__ = "Python.Test.OverridingExceptions"
    def __getattribute__(self, name):
        if name == "args":
            raise LookupError("args")
        return object.__getattribute__(self, name)
    def __init__(self):
        super().__init__("constructed")

print(str(ArgsLookupError()))

from System.Runtime.Serialization import ISerializable

class RawFailingError(ExtendedException):
    __namespace__ = "Python.Test.OverridingExceptions"
    def __init__(self):
        self.failing = False
    def get_Message(self):
        if self.failing:
            raise ValueError("outer") from KeyError("inner")
        return "message"

raw_failing = RawFailingError()
interface = ISerializable(raw_failing)
raw_failing.failing = True
check("raw", lambda: interface.__raw_implementation__)
check("cast", lambda: ISerializable(raw_failing))
"""


@pytest.mark.skipif(os.environ.get("PYTHONNET_RUNTIME") != "netcore", reason="loads the netcore runtime")
def test_namespaced_exception_subclass_override_errors():
    """str(), repr() and the constructor of a __namespace__ exception subclass read None
    from a Python ToString override as an empty message, and raise the error of a Python
    ToString or Message override to their caller as it is, with its cause, also when an
    inherited ToString calls the Message override, through one or two generated levels,
    and when a raw conversion or an interface cast reads it.
    The constructor reads the Message override once, and no Python override of args"""
    output = _run_namespaced_exception_script(_NAMESPACED_EXCEPTION_OVERRIDES_SCRIPT)

    assert output[-10:] == [
        "'' OverridingError('message')",
        "str outer KeyError 'inner'",
        "repr outer KeyError 'inner'",
        "init outer KeyError 'inner'",
        "inherited str outer KeyError 'inner'",
        "two-level inherited str outer KeyError 'inner'",
        "('once',)",
        "constructed",
        "raw outer KeyError 'inner'",
        "cast outer KeyError 'inner'",
    ]
