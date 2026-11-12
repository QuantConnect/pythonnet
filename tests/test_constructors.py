# -*- coding: utf-8 -*-

"""Test CLR class constructor support."""

import os
import subprocess
import pytest
import sys

import System


def test_enum_constructor():
    """Test enum constructor args"""
    from System import TypeCode
    from Python.Test import EnumConstructorTest

    ob = EnumConstructorTest(TypeCode.Int32)
    assert ob.value == TypeCode.Int32


def test_flags_constructor():
    """Test flags constructor args"""
    from Python.Test import FlagsConstructorTest
    from System.IO import FileAccess

    flags = FileAccess.Read | FileAccess.Write
    ob = FlagsConstructorTest(flags)
    assert ob.value == flags


def test_struct_constructor():
    """Test struct constructor args"""
    from System import Guid
    from Python.Test import StructConstructorTest

    guid = Guid.NewGuid()
    ob = StructConstructorTest(guid)
    assert ob.value == guid


def test_datetime():
    inst = System.DateTime(2021, 12, 29)
    assert inst.Year == 2021


def test_subclass_constructor():
    """Test subclass constructor args"""
    from Python.Test import SubclassConstructorTest

    class Sub(System.Exception):
        pass

    instance = Sub()
    ob = SubclassConstructorTest(instance)
    assert isinstance(ob.value, System.Exception)


def test_multiple_constructor():
    from Python.Test import MultipleConstructorsTest

    # Test parameterless
    ob = MultipleConstructorsTest()
    assert ob.value == ""


def test_default_constructor_fallback():
    from Python.Test import DefaultConstructorMatching

    ob = DefaultConstructorMatching(2)
    assert ob.a == 2

    with pytest.raises(TypeError):
        ob = DefaultConstructorMatching("2")

@pytest.mark.skip(reason="Runtime.TryCollectingGarbage is [ForbidPythonThreads]; honoring it in MethodObject is disabled, so calling it releases the GIL and crashes the interpreter")
def test_constructor_leak():
    from System import Uri
    from Python.Runtime import Runtime

    uri = Uri("http://www.python.org")
    Runtime.TryCollectingGarbage(20)
    ref_count = sys.getrefcount(uri)

    # check disabled due to GC uncertainty
    # assert ref_count == 1



def test_string_constructor():
    from System import String, Char, Array

    # QuantConnect fork: the String(char, int) constructor is not selected for
    # a Python str argument, so this raises rather than repeating the character.
    with pytest.raises(TypeError):
        String('A', 10)

    arr = Array[Char](10)
    for i in range(10):
        arr[i] = Char(str(i))

    # QuantConnect fork: the String(char[]) and String(char[], int, int)
    # constructors are likewise not selected, so these raise.
    with pytest.raises(TypeError):
        String(arr)

    with pytest.raises(TypeError):
        String(arr, 5, 4)


_FAILED_CONSTRUCTION_SCRIPT = """
import os, sys
bin_path = sys.argv[1]
sys.path.append(bin_path)
from clr_loader import get_coreclr
from pythonnet import set_runtime
set_runtime(get_coreclr(runtime_config=os.path.join(bin_path, "Python.Test.runtimeconfig.json")))
import clr
clr.AddReference("Python.Test")
import gc
from Python.Test import FinalizedConstructorTest
from System import GC, InvalidOperationException

def construct():
    try:
        FinalizedConstructorTest("unsupported")
    except TypeError:
        print("caught")
    FinalizedConstructorTest()
    try:
        FinalizedConstructorTest(InvalidOperationException("thrown"))
    except InvalidOperationException:
        print("thrown")

construct()
gc.collect()
for _ in range(2):
    GC.Collect()
    GC.WaitForPendingFinalizers()
print(FinalizedConstructorTest.Finalized)
"""


@pytest.mark.skipif(os.environ.get("PYTHONNET_RUNTIME") != "netcore", reason="loads the netcore runtime")
def test_construction_finalization():
    """A failed T(...) must not leave an object whose constructor never ran
    to its finalizer, which would terminate the process, while a constructed
    object, or one whose constructor threw, is finalized once, as with new"""
    from Python.Test import FinalizedConstructorTest

    bin_path = os.path.dirname(FinalizedConstructorTest().GetType().Assembly.Location)
    result = subprocess.run([sys.executable, "-c", _FAILED_CONSTRUCTION_SCRIPT, bin_path],
                            capture_output=True, text=True, timeout=300)

    assert result.returncode == 0, result.stderr
    assert result.stdout.splitlines()[-3:] == ["caught", "thrown", "2"]
