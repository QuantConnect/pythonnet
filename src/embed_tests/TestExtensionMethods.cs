using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
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

    public class DisposableExtendedModel : ExtendedModel, IDisposable
    {
        public void Dispose()
        {
        }
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

        public static int CountModels(this List<ExtendedModel> models) => models.Count;

        public static string Names(this IEnumerable<ExtendedModel> models) => string.Join(",", models.Select(model => model.Name));

        // targets made only of framework types are not bound, they would bloat every class sharing them
        public static int Total(this List<int> values) => values.Sum();

        public static double SumAll(this double[] values) => values.Sum();

        public static string Tag(this object value) => "tag";

        public static int CountItems(this IEnumerable values) => values.Cast<object>().Count();

        public static bool Release(this IDisposable disposable) => true;

        public static T Identity<T>(this T value) => value;

        public static string Twice(this string value) => value + value;

        // hidden from IntelliSense, so not advertised on the instance either
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static string Hidden(this ExtendedModel model) => "hidden";

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static string Advanced(this ExtendedModel model) => "advanced";
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class HiddenExtendedModelExtensions
    {
        public static string HiddenByClass(this ExtendedModel model) => "hidden by class";
    }

    public class TestExtensionMethods
    {
        private static dynamic module;

        private const string TestModule = @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from System import Array
from System.Collections.Generic import List
from System.Text import StringBuilder
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
    models = List[ExtendedModel]()
    models.Add(ExtendedModel())
    models.Add(ExtendedModel())
    return models.count_models(), models.names()

def framework_targets():
    values = List[int]()
    values.Add(1)
    values.Add(2)
    array = Array[float]([1.5, 2.5])
    bound = (hasattr(values, 'total'), hasattr(array, 'sum_all'), hasattr(ExtendedModel(), 'tag'),
        hasattr(StringBuilder(), 'tag'), hasattr(values, 'count_items'), hasattr(DisposableExtendedModel(), 'release'))
    static = (ExtendedModelExtensions.total(values), ExtendedModelExtensions.sum_all(array),
        ExtendedModelExtensions.tag(ExtendedModel()), ExtendedModelExtensions.count_items(values),
        ExtendedModelExtensions.release(DisposableExtendedModel()))
    return bound, static

def open_generic_not_bound():
    return hasattr(ExtendedModel(), 'identity')

def editor_browsable():
    model = ExtendedModel()
    bound = hasattr(model, 'hidden'), hasattr(model, 'hidden_by_class'), model.advanced()
    static = ExtendedModelExtensions.hidden(model), HiddenExtendedModelExtensions.hidden_by_class(model)
    return bound, static

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
        public void ClosedGenericTargetOverAUserTypeIsBound()
        {
            using var _ = Py.GIL();
            var result = module.closed_generic();
            Assert.AreEqual(2, (int)result[0]);
            Assert.AreEqual("model,model", (string)result[1]);
        }

        [Test]
        public void FrameworkOnlyTargetsKeepTheStaticFormOnly()
        {
            using var _ = Py.GIL();
            var result = module.framework_targets();
            var bound = result[0];
            for (var i = 0; i < 6; i++)
            {
                Assert.IsFalse((bool)bound[i], $"index {i}");
            }

            var staticForms = result[1];
            Assert.AreEqual(3, (int)staticForms[0]);
            Assert.AreEqual(4.0, (double)staticForms[1]);
            Assert.AreEqual("tag", (string)staticForms[2]);
            Assert.AreEqual(2, (int)staticForms[3]);
            Assert.IsTrue((bool)staticForms[4]);
        }

        [Test]
        public void OpenGenericTargetIsNotBound()
        {
            using var _ = Py.GIL();
            Assert.IsFalse((bool)module.open_generic_not_bound());
        }

        [Test]
        public void EditorBrowsableNeverKeepsTheStaticFormOnly()
        {
            using var _ = Py.GIL();
            var result = module.editor_browsable();
            Assert.IsFalse((bool)result[0][0]);
            Assert.IsFalse((bool)result[0][1]);
            Assert.AreEqual("advanced", (string)result[0][2]);
            Assert.AreEqual("hidden", (string)result[1][0]);
            Assert.AreEqual("hidden by class", (string)result[1][1]);
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
