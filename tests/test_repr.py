# -*- coding: utf-8 -*-

"""Test __repr__ output"""

import System
import pytest
from Python.Test import ReprTest

def test_basic():
    """Test Point class which implements both ToString and __repr__ without inheritance"""
    ob = ReprTest.Point(1,2)
    # point implements ToString() and __repr__()
    assert ob.__repr__() == "Point(1,2)"
    assert str(ob) == "Python.Test.ReprTest+Point: X=1, Y=2"

def test_system_string():
    """Test system string"""
    ob = System.String("hello")
    assert str(ob) == "hello"
    assert ob.__repr__() == "hello"

def test_str_only():
    """Test class implementing ToString() but not __repr__()"""
    ob = ReprTest.Bar()
    assert str(ob) == "I implement ToString() but not __repr__()!"
    assert ob.__repr__() == "I implement ToString() but not __repr__()!"

def test_str_only_nested_in_list():
    """Test containers show the ToString() of elements implementing ToString() but not __repr__()"""
    obs = [ReprTest.Bar(), ReprTest.Bar()]
    assert str(obs) == "[I implement ToString() but not __repr__()!, I implement ToString() but not __repr__()!]"

def test_enum():
    """Test enums show the same Python-facing member name as str()"""
    from System import DayOfWeek
    from System.IO import FileAccess
    assert repr(DayOfWeek.Monday) == str(DayOfWeek.Monday) == "MONDAY"
    assert str([DayOfWeek.Monday, FileAccess.ReadWrite]) == "[MONDAY, READ_WRITE]"

def test_struct_without_tostring_override():
    """Test struct not overriding ToString() keeps the default repr instead of ValueType's type name"""
    from System.Collections.Generic import List
    ob = List[int]().GetEnumerator()
    assert repr(ob).startswith("<System.Collections.Generic.List")
    assert " object at " in repr(ob)

def test_namespace_subclass():
    """Test Python subclasses with a __namespace__ only show a ToString() that is actually overridden"""
    class ReprPlainNs(System.Object):
        __namespace__ = "ReprTestNs"

    class ReprBarNs(ReprTest.Bar):
        __namespace__ = "ReprTestNs"

    class ReprOverrideNs(System.Object):
        __namespace__ = "ReprTestNs"

        def ToString(self):
            return "I implement ToString() in Python!"

    assert " object at " in repr(ReprPlainNs())
    assert repr(ReprBarNs()) == "I implement ToString() but not __repr__()!"
    assert repr(ReprOverrideNs()) == "I implement ToString() in Python!"

def test_no_tostring_override():
    """Test object not overriding ToString() keeps the default repr"""
    ob = System.Object()
    assert repr(ob).startswith("<System.Object object at ")

def test_tostring_returns_null():
    """Test class whose ToString() returns null"""
    ob = ReprTest.NullToString()
    assert "<Python.Test.NullToString object at " in repr(ob)

def test_tostring_throws():
    """Test class whose ToString() throws"""
    ob = ReprTest.ThrowingToString()
    assert "<Python.Test.ThrowingToString object at " in repr(ob)

def test_str_tostring_throws():
    """Test str() raises the exception a .NET ToString() throws, also when a Python subclass inherits it"""
    from System import InvalidOperationException

    class StrThrowingNs(ReprTest.ThrowingToString):
        __namespace__ = "ReprTestNs"

    for ob in (ReprTest.ThrowingToString(), StrThrowingNs()):
        with pytest.raises(InvalidOperationException, match="ToString\\(\\) failed"):
            str(ob)

def test_str_python_tostring_raises():
    """Test str() raises the error of a Python ToString() override as it is, with its cause"""
    from System import InvalidOperationException

    class StrRaisingNs(System.Object):
        __namespace__ = "ReprTestNs"

        def __init__(self, error):
            self.error = error

        def ToString(self):
            raise self.error

    with pytest.raises(ValueError, match="plain") as info:
        str(StrRaisingNs(ValueError("plain")))
    assert info.value.__cause__ is None

    with pytest.raises(InvalidOperationException, match="from .NET"):
        str(StrRaisingNs(InvalidOperationException("from .NET")))

    class StrChainedNs(System.Object):
        __namespace__ = "ReprTestNs"

        def ToString(self):
            raise ValueError("outer") from KeyError("inner")

    with pytest.raises(ValueError, match="outer") as info:
        str(StrChainedNs())
    assert isinstance(info.value.__cause__, KeyError)
    assert info.value.__cause__.args == ("inner",)

def test_hierarchy1():
    """Test inheritance hierarchy with base & middle class implementing ToString"""
    ob1 = ReprTest.BazBase()
    assert str(ob1) == "Base class implementing ToString()!"
    assert ob1.__repr__() == "Base class implementing ToString()!"

    ob2 = ReprTest.BazMiddle()
    assert str(ob2) == "Middle class implementing ToString()!"
    assert ob2.__repr__() == "Middle class implementing ToString()!"

    ob3 = ReprTest.Baz()
    assert str(ob3) == "Middle class implementing ToString()!"
    assert ob3.__repr__() == "Middle class implementing ToString()!"

def bad_tostring():
    """Test ToString that can't be used by str()"""
    ob = ReprTest.Quux()
    assert str(ob) == "Python.Test.ReprTest+Quux"
    assert "<Python.Test.Quux object at " in ob.__repr__()

def bad_repr():
    """Test incorrect implementation of repr"""
    ob1 = ReprTest.QuuzBase()
    assert str(ob1) == "Python.Test.ReprTest+QuuzBase"
    assert "<Python.Test.QuuzBase object at " in ob.__repr__()
    
    ob2 = ReprTest.Quuz()
    assert str(ob2) == "Python.Test.ReprTest+Quuz"
    assert "<Python.Test.Quuz object at " in ob.__repr__()

    ob3 = ReprTest.Corge()
    with pytest.raises(Exception):
        str(ob3)
    with pytest.raises(Exception):
        ob3.__repr__()

    ob4 = ReprTest.Grault()
    with pytest.raises(Exception):
        str(ob4)
    with pytest.raises(Exception):
        ob4.__repr__()
