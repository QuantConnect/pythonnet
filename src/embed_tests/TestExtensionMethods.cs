using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public enum ExtendedStatus { Open, Closed }

    public interface IExtendedShape
    {
        double Side { get; }
    }

    public class ExtendedModel
    {
        public string Name { get; set; } = "model";

        public string Existing() => "member";
    }

    public class DerivedExtendedModel : ExtendedModel
    {
    }

    public class ExtendedSquare : IExtendedShape
    {
        public double Side => 2;
    }

    public static class ExtendedStatusExtensions
    {
        public static bool IsOpen(this ExtendedStatus status) => status == ExtendedStatus.Open;

        public static string Describe(this ExtendedStatus status, string prefix, int times = 1)
            => string.Concat(Enumerable.Repeat(prefix + status, times));
    }

    public static class ExtendedModelExtensions
    {
        public static string Describe(this ExtendedModel model, string prefix = "") => prefix + model.Name;

        // collides with the real instance method, which must win
        public static string Existing(this ExtendedModel model) => "extension";

        public static double Area(this IExtendedShape shape) => shape.Side * shape.Side;

        public static int Total(this List<int> values) => values.Sum();

        public static T Identity<T>(this T value) => value;

        public static string Twice(this string value) => value + value;
    }

    public class TestExtensionMethods
    {
        private static dynamic module;

        private const string TestModule = @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from System.Collections.Generic import List
from Python.EmbeddingTest import *

def enum_instance():
    return ExtendedStatus.Open.IsOpen(), ExtendedStatus.Open.is_open(), ExtendedStatus.Closed.is_open()

def enum_with_args():
    return ExtendedStatus.Open.describe('s-', times=2)

def enum_static_forms():
    return ExtendedStatusExtensions.is_open(ExtendedStatus.Open), ExtendedStatus.is_open(ExtendedStatus.Closed)

def class_instance():
    return ExtendedModel().describe(), ExtendedModel().describe(prefix='p-')

def derived_inherits():
    return DerivedExtendedModel().describe('d-')

def interface_target():
    return ExtendedSquare().area()

def member_wins():
    return ExtendedModel().existing()

def closed_generic():
    values = List[int]()
    values.Add(1)
    values.Add(2)
    return values.total()

def open_generic_not_bound():
    return hasattr(ExtendedModel(), 'identity')

def python_native_not_bound():
    return hasattr('ab', 'twice'), ExtendedModelExtensions.twice('ab')
";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            PythonEngine.Initialize();
            using var _ = Py.GIL();
            module = PyModule.FromString("extension_methods", TestModule);
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        [Test]
        public void EnumExtensionIsCallableFromTheInstance()
        {
            using var _ = Py.GIL();
            var result = module.enum_instance();
            Assert.IsTrue((bool)result[0]);
            Assert.IsTrue((bool)result[1]);
            Assert.IsFalse((bool)result[2]);
        }

        [Test]
        public void ExtensionArgumentsFollowTheInstance()
        {
            using var _ = Py.GIL();
            Assert.AreEqual("s-Opens-Open", (string)module.enum_with_args());
        }

        [Test]
        public void StaticFormsKeepWorking()
        {
            using var _ = Py.GIL();
            var result = module.enum_static_forms();
            Assert.IsTrue((bool)result[0]);
            Assert.IsFalse((bool)result[1]);
        }

        [Test]
        public void ClassExtensionIsCallableFromTheInstance()
        {
            using var _ = Py.GIL();
            var result = module.class_instance();
            Assert.AreEqual("model", (string)result[0]);
            Assert.AreEqual("p-model", (string)result[1]);
        }

        [Test]
        public void DerivedClassInheritsBaseClassExtensions()
        {
            using var _ = Py.GIL();
            Assert.AreEqual("d-model", (string)module.derived_inherits());
        }

        [Test]
        public void InterfaceExtensionIsBoundToImplementingClass()
        {
            using var _ = Py.GIL();
            Assert.AreEqual(4.0, (double)module.interface_target());
        }

        [Test]
        public void RealMemberWinsOverExtension()
        {
            using var _ = Py.GIL();
            Assert.AreEqual("member", (string)module.member_wins());
        }

        [Test]
        public void ClosedGenericTargetIsBound()
        {
            using var _ = Py.GIL();
            Assert.AreEqual(3, (int)module.closed_generic());
        }

        [Test]
        public void OpenGenericTargetIsNotBound()
        {
            using var _ = Py.GIL();
            Assert.IsFalse((bool)module.open_generic_not_bound());
        }

        [Test]
        public void PythonNativeTargetKeepsTheStaticForm()
        {
            using var _ = Py.GIL();
            var result = module.python_native_not_bound();
            Assert.IsFalse((bool)result[0]);
            Assert.AreEqual("abab", (string)result[1]);
        }
    }
}
