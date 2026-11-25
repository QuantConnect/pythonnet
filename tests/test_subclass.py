# -*- coding: utf-8 -*-
# FIXME: This test module randomly passes/fails even if all tests are skipped.
# Something fishy is going on with the Test fixtures. Behavior seen on CI on
# both Linux and Windows
# TODO: Remove delay of class creations. Adding SetUp/TearDown may help

"""Test sub-classing managed types"""

import sys
import System
import pytest
from Python.Test import (IInterfaceTest, SubClassTest, EventArgsTest,
                         FunctionsTest)
from System.Collections.Generic import List


def interface_test_class_fixture(subnamespace):
    """Delay creation of class until test starts."""

    class InterfaceTestClass(IInterfaceTest):
        """class that implements the test interface"""
        __namespace__ = "Python.Test." + subnamespace

        def foo(self):
            return "InterfaceTestClass"

        def bar(self, x, i):
            return "/".join([x] * i)

    return InterfaceTestClass


def derived_class_fixture(subnamespace):
    """Delay creation of class until test starts."""

    class DerivedClass(SubClassTest):
        """class that derives from a class deriving from IInterfaceTest"""
        __namespace__ = "Python.Test." + subnamespace

        def foo(self):
            return "DerivedClass"

        def base_foo(self):
            return SubClassTest.foo(self)

        def super_foo(self):
            return super(DerivedClass, self).foo()

        def bar(self, x, i):
            return "_".join([x] * i)

        def return_list(self):
            l = List[str]()
            l.Add("A")
            l.Add("B")
            l.Add("C")
            return l

    return DerivedClass

def broken_derived_class_fixture(subnamespace):
    """Delay creation of class until test starts."""

    class DerivedClass(SubClassTest):
        """class that derives from a class deriving from IInterfaceTest"""
        __namespace__ = 3

    return DerivedClass

def derived_event_test_class_fixture(subnamespace):
    """Delay creation of class until test starts."""

    class DerivedEventTest(IInterfaceTest):
        """class that implements IInterfaceTest.TestEvent"""
        __namespace__ = "Python.Test." + subnamespace

        def __init__(self):
            self.event_handlers = []

        # event handling
        def add_TestEvent(self, handler):
            self.event_handlers.append(handler)

        def remove_TestEvent(self, handler):
            self.event_handlers.remove(handler)

        def OnTestEvent(self, value):
            args = EventArgsTest(value)
            for handler in self.event_handlers:
                handler(self, args)

    return DerivedEventTest


def test_base_class():
    """Test base class managed type"""
    ob = SubClassTest()
    assert ob.foo() == "foo"
    assert FunctionsTest.test_foo(ob) == "foo"
    assert ob.bar("bar", 2) == "bar"
    assert FunctionsTest.test_bar(ob, "bar", 2) == "bar"
    assert ob.not_overriden() == "not_overriden"
    assert list(ob.return_list()) == ["a", "b", "c"]
    assert list(SubClassTest.test_list(ob)) == ["a", "b", "c"]


def test_interface():
    """Test python classes can derive from C# interfaces"""
    InterfaceTestClass = interface_test_class_fixture(test_interface.__name__)
    ob = InterfaceTestClass()
    assert ob.foo() == "InterfaceTestClass"
    assert FunctionsTest.test_foo(ob) == "InterfaceTestClass"
    assert ob.bar("bar", 2) == "bar/bar"
    assert FunctionsTest.test_bar(ob, "bar", 2) == "bar/bar"

    x = FunctionsTest.pass_through(ob)
    assert id(x) == id(ob)


def test_derived_class():
    """Test python class derived from managed type"""
    DerivedClass = derived_class_fixture(test_derived_class.__name__)
    ob = DerivedClass()
    assert ob.foo() == "DerivedClass"
    assert ob.base_foo() == "foo"
    assert ob.super_foo() == "foo"
    assert FunctionsTest.test_foo(ob) == "DerivedClass"
    assert ob.bar("bar", 2) == "bar_bar"
    assert FunctionsTest.test_bar(ob, "bar", 2) == "bar_bar"
    assert ob.not_overriden() == "not_overriden"
    assert list(ob.return_list()) == ["A", "B", "C"]
    assert list(SubClassTest.test_list(ob)) == ["A", "B", "C"]

    x = FunctionsTest.pass_through(ob)
    assert id(x) == id(ob)

def test_broken_derived_class():
    """Test python class derived from managed type with invalid namespace"""
    with pytest.raises(TypeError):
        DerivedClass = broken_derived_class_fixture(test_derived_class.__name__)
        ob = DerivedClass()

def test_derived_traceback():
    """Test python exception traceback in class derived from managed base"""
    class DerivedClass(SubClassTest):
        __namespace__ = "Python.Test.traceback"

        def foo(self):
            print (xyzname)
            return None

    import sys,traceback
    ob = DerivedClass()

    # direct call
    try:
        ob.foo()
        assert False
    except:
        e = sys.exc_info()
    assert "xyzname" in str(e[1])
    location = traceback.extract_tb(e[2])[-1]
    assert location[2] == "foo"

    # call through managed code
    try:
        FunctionsTest.test_foo(ob)
        assert False
    except:
        e = sys.exc_info()
    assert "xyzname" in str(e[1])
    location = traceback.extract_tb(e[2])[-1]
    assert location[2] == "foo"


def test_create_instance():
    """Test derived instances can be created from managed code"""
    DerivedClass = derived_class_fixture(test_create_instance.__name__)
    ob = FunctionsTest.create_instance(DerivedClass)
    assert ob.foo() == "DerivedClass"
    assert FunctionsTest.test_foo(ob) == "DerivedClass"
    assert ob.bar("bar", 2) == "bar_bar"
    assert FunctionsTest.test_bar(ob, "bar", 2) == "bar_bar"
    assert ob.not_overriden() == "not_overriden"

    x = FunctionsTest.pass_through(ob)
    assert id(x) == id(ob)

    InterfaceTestClass = interface_test_class_fixture(test_create_instance.__name__)
    ob2 = FunctionsTest.create_instance(InterfaceTestClass)
    assert ob2.foo() == "InterfaceTestClass"
    assert FunctionsTest.test_foo(ob2) == "InterfaceTestClass"
    assert ob2.bar("bar", 2) == "bar/bar"
    assert FunctionsTest.test_bar(ob2, "bar", 2) == "bar/bar"

    y = FunctionsTest.pass_through(ob2)
    assert id(y) == id(ob2)


def test_namespace_subclass_of_namespace_subclass():
    """Test managed code reaches the Python overrides a namespaced subclass inherits from a namespaced parent"""
    subnamespace = test_namespace_subclass_of_namespace_subclass.__name__
    namespace = "Python.Test." + subnamespace
    DerivedClass = derived_class_fixture(subnamespace)

    class ChildClass(DerivedClass):
        __namespace__ = namespace

    class GrandChildClass(ChildClass):
        __namespace__ = namespace

        def bar(self, x, i):
            return "GrandChildClass"

    for ob in (ChildClass(), GrandChildClass(), FunctionsTest.create_instance(GrandChildClass)):
        assert ob.foo() == "DerivedClass"
        assert FunctionsTest.test_foo(ob) == "DerivedClass"
        assert ob.not_overriden() == "not_overriden"
        assert list(SubClassTest.test_list(ob)) == ["A", "B", "C"]

    assert FunctionsTest.test_bar(ChildClass(), "bar", 2) == "bar_bar"
    assert FunctionsTest.test_bar(GrandChildClass(), "bar", 2) == "GrandChildClass"

    # interface methods have no managed implementation to fall back to
    InterfaceTestClass = interface_test_class_fixture(subnamespace)

    class InterfaceChildClass(InterfaceTestClass):
        __namespace__ = namespace

    ob = InterfaceChildClass()
    assert ob.foo() == "InterfaceTestClass"
    assert FunctionsTest.test_foo(ob) == "InterfaceTestClass"
    assert FunctionsTest.test_bar(ob, "bar", 2) == "bar/bar"

    # overloads the generated type doesn't override don't hide the parent's override either
    class EqualsParentClass(System.Object):
        __namespace__ = namespace

        def Equals(self, other):
            return True

    class EqualsChildClass(EqualsParentClass):
        __namespace__ = namespace

    other = System.Object()
    ob = EqualsChildClass()
    assert ob.Equals(other)
    assert System.Object.Equals(ob, other)
    assert ob.GetType().GetMethod("Equals", [System.Object]).Invoke(ob, [other])

    # members the child declares for .NET keep their snake_case aliases over the parent's Python ones
    import clr

    class AliasParentClass(SubClassTest):
        __namespace__ = namespace

        def calculate(self):
            return 1

        @property
        def number(self):
            return 2

    class AliasChildClass(AliasParentClass):
        __namespace__ = namespace

        @clr.clrmethod(int, [])
        def Calculate(self):
            return 3

        @clr.clrproperty(int)
        def Number(self):
            return 4

    ob = AliasChildClass()
    assert ob.calculate() == 3
    assert ob.Calculate() == 3
    assert ob.number == 4
    assert ob.Number == 4

    # nor the accessors of a property the child redeclares
    class PropertyParentClass(System.Object):
        __namespace__ = namespace

        def get_Number(self):
            return 1

        def set_Number(self, value):
            self.value = value + 1

        Number = clr.clrproperty(int, get_Number, set_Number)

    class PropertyChildClass(PropertyParentClass):
        __namespace__ = namespace

        def get_Number(self):
            return 3

        def set_Number(self, value):
            self.value = value + 3

        Number = clr.clrproperty(int, get_Number, set_Number)

    class PropertyDecoratedChildClass(PropertyParentClass):
        __namespace__ = namespace

        @clr.clrproperty(int)
        def Number(self):
            return 3

        @Number.setter
        def Number(self, value):
            self.value = value + 3

    for cls in (PropertyChildClass, PropertyDecoratedChildClass):
        ob = cls()
        prop = ob.GetType().GetProperty("Number")
        assert ob.Number == 3
        assert ob.get_Number() == 3
        assert prop.GetValue(ob) == 3
        ob.set_Number(10)
        assert ob.value == 13
        prop.SetValue(ob, 20)
        assert ob.value == 23

    # the managed constructor still runs once
    from Python.Test import CountedConstructorTest

    class CountedParentClass(CountedConstructorTest):
        __namespace__ = namespace

        def __init__(self):
            super().__init__()

    class CountedChildClass(CountedParentClass):
        __namespace__ = namespace

    CountedConstructorTest.Calls = 0
    CountedChildClass()
    assert CountedConstructorTest.Calls == 1


def test_events():
    class EventHandler(object):
        def handler(self, x, args):
            self.value = args.value

    event_handler = EventHandler()

    x = SubClassTest()
    x.TestEvent += event_handler.handler
    assert FunctionsTest.test_event(x, 1) == 1
    assert event_handler.value == 1

    InterfaceTestClass = interface_test_class_fixture(test_events.__name__)
    i = InterfaceTestClass()
    with pytest.raises(System.NotImplementedException):
        FunctionsTest.test_event(i, 2)

    DerivedEventTest = derived_event_test_class_fixture(test_events.__name__)
    d = DerivedEventTest()
    d.add_TestEvent(event_handler.handler)
    assert FunctionsTest.test_event(d, 3) == 3
    assert event_handler.value == 3
    assert len(d.event_handlers) == 1


def test_isinstance_check():
    a = [str(x) for x in range(0, 1000)]
    b = [System.String(x) for x in a]

    for x in a:
        assert not isinstance(x, System.Object)
        assert not isinstance(x, System.String)

    for x in b:
        assert isinstance(x, System.Object)
        assert isinstance(x, System.String)

def test_namespace_and_init():
    calls = []
    class TestX(System.Object):
        __namespace__ = "test_clr_subclass_with_init_args"
        def __init__(self, *args, **kwargs):
            calls.append((args, kwargs))
    t = TestX(1,2,3,foo="bar")
    assert len(calls) == 1
    assert calls[0][0] == (1,2,3)
    assert calls[0][1] == {"foo":"bar"}

def test_namespace_and_argless_init():
    calls = []
    class TestX(System.Object):
        __namespace__ = "test_clr_subclass_without_init_args"
        def __init__(self):
            calls.append(True)
    t = TestX()
    assert len(calls) == 1
    assert calls[0] == True


def test_namespace_and_no_init():
    class TestX(System.Object):
        __namespace__ = "test_clr_subclass_without_init"
        q = 1
    t = TestX()
    assert t.q == 1

def test_construction_from_clr():
    import clr
    calls = []
    class TestX(System.Object):
        __namespace__ = "test_clr_subclass_init_from_clr"
        @clr.clrmethod(None, [int, str])
        def __init__(self, i, s):
            calls.append((i, s))

    # Construct a TestX from Python
    t = TestX(1, "foo")
    assert len(calls) == 1
    assert calls[0][0] == 1
    assert calls[0][1] == "foo"

    # Reset calls and construct a TestX from CLR
    calls = []
    tp = t.GetType()
    t2 = tp.GetConstructors()[0].Invoke(None)
    assert len(calls) == 0

    # The object has only been constructed, now it needs to be initialized as well
    tp.GetMethod("__init__").Invoke(t2, [1, "foo"])
    assert len(calls) == 1
    assert calls[0][0] == 1
    assert calls[0][1] == "foo"

# regression test for https://github.com/pythonnet/pythonnet/issues/1565
def test_can_be_collected_by_gc():
    from Python.Test import BaseClass

    class Derived(BaseClass):
        __namespace__ = 'test_can_be_collected_by_gc'

    inst = Derived()
    cycle = [inst]
    del inst
    cycle.append(cycle)
    del cycle

    import gc
    gc.collect()


def _release_python_and_net_objects():
    """Collect Python cycles and .NET objects, then free the Python objects whose .NET object was finalized"""
    import gc
    from System import GC

    for _ in range(3):
        gc.collect()
        GC.Collect()
        GC.WaitForPendingFinalizers()
        # every 200th Python object passed to .NET frees the queued ones
        List[System.Object]([object() for _ in range(300)])
    gc.collect()


def test_namespaced_subclass_held_by_net_keeps_its_cycle():
    from System import String

    class CycleHolder(System.Object):
        __namespace__ = "test_namespaced_subclass_held_by_net_keeps_its_cycle"

        def __init__(self, code):
            self.code = code
            self.me = self
            self.items = [self]
            self.items.append(self.items)

        def ToString(self):
            return "holder {}".format(self.code)

    class PythonSubclass(CycleHolder):
        pass

    # each instance references its class, until it's freed
    refcounts = sys.getrefcount(CycleHolder), sys.getrefcount(PythonSubclass)
    held = List[System.Object]()
    held.Add(CycleHolder(7))
    held.Add(PythonSubclass(8))

    for _ in range(2):
        _release_python_and_net_objects()
        # .NET calls the Python override while only .NET holds the object
        assert String.Join[System.Object](",", held) == "holder 7,holder 8"

        holder = held[0]
        assert holder.code == 7
        assert holder.me is holder
        assert holder.items[0] is holder
        assert held[1].code == 8
        del holder

    held.Clear()
    _release_python_and_net_objects()
    assert (sys.getrefcount(CycleHolder), sys.getrefcount(PythonSubclass)) == refcounts


def test_namespaced_subclass_cycle_is_collected():
    class CycleMember(System.Object):
        __namespace__ = "test_namespaced_subclass_cycle_is_collected"

        def __init__(self):
            self.me = self

    refcount = sys.getrefcount(CycleMember)
    for _ in range(100):
        CycleMember()
        first, second = CycleMember(), CycleMember()
        first.other, second.other = second, first
        del first, second

    held = List[System.Object]()
    first, second = CycleMember(), CycleMember()
    first.other, second.other = second, first
    first.code = 1
    held.Add(second)
    del first, second

    _release_python_and_net_objects()
    assert held[0].other.code == 1
    assert held[0].other.other is held[0]
    assert sys.getrefcount(CycleMember) == refcount + 2

    held.Clear()
    _release_python_and_net_objects()
    assert sys.getrefcount(CycleMember) == refcount


def test_namespaced_subclass_direct_del_call_keeps_net_object():
    class DirectDelCall(System.Object):
        __namespace__ = "test_namespaced_subclass_direct_del_call_keeps_net_object"

        def __init__(self):
            self.me = self

    instance = DirectDelCall()
    instance.__del__()
    _release_python_and_net_objects()
    instance.GetHashCode()
    assert instance.me is instance


def _resurrect_through_python(cls, survivors):
    """Drop an instance in a cycle whose other member's __del__ gives Python the instance back"""
    import gc
    from System import GC

    class Resurrector:
        def __init__(self, owner):
            self.owner = owner

        def __del__(self):
            survivors.append(self.owner)

    owner = cls()
    owner.other = Resurrector(owner)
    del owner

    gc.collect()
    GC.Collect()
    GC.WaitForPendingFinalizers()


def test_namespaced_subclass_resurrected_by_python_keeps_net_object():
    class Resurrected(System.Object):
        __namespace__ = "test_namespaced_subclass_resurrected_by_python_keeps_net_object"

        def __init__(self):
            self.code = 7

    survivors = []
    _resurrect_through_python(Resurrected, survivors)
    # before the finalized .NET object is handled
    survivors[0].GetHashCode()

    _release_python_and_net_objects()
    assert survivors[0].GetType().Name == "Resurrected"
    assert survivors[0].code == 7


def test_namespaced_subclass_resurrected_by_python_is_collected():
    class Resurrected(System.Object):
        __namespace__ = "test_namespaced_subclass_resurrected_by_python_is_collected"

        def __init__(self):
            self.code = 7

    refcount = sys.getrefcount(Resurrected)
    survivors = []
    _resurrect_through_python(Resurrected, survivors)

    _release_python_and_net_objects()
    assert survivors[0].code == 7
    assert survivors[0].other.owner is survivors[0]

    survivors.clear()
    _release_python_and_net_objects()
    assert sys.getrefcount(Resurrected) == refcount


def test_namespaced_subclass_resurrected_by_python_and_passed_to_net():
    class Resurrected(System.Object):
        __namespace__ = "test_namespaced_subclass_resurrected_by_python_and_passed_to_net"

        def __init__(self):
            self.code = 7

    from Python.Runtime import Finalizer

    refcount = sys.getrefcount(Resurrected)
    survivors = []
    held = List[System.Object]()
    threshold = Finalizer.Instance.Threshold
    # .NET gets the instance back before the finalized .NET object is handled
    Finalizer.Instance.Threshold = 1000000
    try:
        _resurrect_through_python(Resurrected, survivors)
        held.Add(survivors[0])
        alias = held[0]
        assert alias is survivors[0]
        # the next Python object passed to .NET handles the finalized .NET object
        Finalizer.Instance.Threshold = 1
        List[System.Object]([object()])
    finally:
        Finalizer.Instance.Threshold = threshold
    assert alias.code == 7

    # Python lets go first, then .NET
    survivors.clear()
    del alias
    _release_python_and_net_objects()
    assert held[0].code == 7
    assert held[0].GetType().Name == "Resurrected"

    held.Clear()
    _release_python_and_net_objects()
    assert sys.getrefcount(Resurrected) == refcount


def test_namespaced_subclass_taken_back_by_python_then_released():
    class TakenBack(System.Object):
        __namespace__ = "test_namespaced_subclass_taken_back_by_python_then_released"

        def __init__(self):
            self.code = 7
            self.me = self

    refcount = sys.getrefcount(TakenBack)
    held = List[System.Object]()
    held.Add(TakenBack())
    _release_python_and_net_objects()

    instance = held[0]
    assert instance.code == 7
    instance.me = None
    del instance
    held.Clear()
    for _ in range(2):
        _release_python_and_net_objects()
    assert sys.getrefcount(TakenBack) == refcount


@pytest.mark.parametrize("cycle", ["none", "self", "resurrected"])
def test_namespaced_subclass_runs_base_finalizer_once(cycle):
    from Python.Test import FinalizedConstructorTest

    class Finalized(FinalizedConstructorTest):
        __namespace__ = "test_namespaced_subclass_runs_base_finalizer_once_{}".format(cycle)

        def __init__(self):
            self.code = 7

    before = FinalizedConstructorTest.Finalized
    survivors = []
    if cycle == "resurrected":
        _resurrect_through_python(Finalized, survivors)
        _release_python_and_net_objects()
        assert survivors[0].code == 7
        survivors.clear()
    else:
        instance = Finalized()
        if cycle == "self":
            instance.me = instance
        del instance

    for _ in range(3):
        _release_python_and_net_objects()
    assert FinalizedConstructorTest.Finalized - before == 1


@pytest.mark.parametrize("in_cycle", [False, True])
def test_namespaced_subclass_held_by_net_keeps_its_wrapper(in_cycle):
    class HeldByNet(System.Object):
        __namespace__ = "test_namespaced_subclass_held_by_net_keeps_its_wrapper_{}".format(in_cycle)

        def __init__(self, code):
            self.code = code
            if in_cycle:
                self.me = self

    held = List[System.Object]()
    held.Add(HeldByNet(7))
    for _ in range(2):
        _release_python_and_net_objects()

        instance = held[0]
        assert instance.code == 7
        assert held.Contains(instance)
        assert instance.GetType().Name == "HeldByNet"
        del instance
