using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    /// <summary>
    /// Passing a Python float where a .NET integer is expected.
    ///
    /// A float that holds an integral value (e.g. 5.0) is accepted and converted;
    /// a non-integral float (e.g. 5.5) is rejected rather than silently truncated.
    /// This must hold regardless of whether the target method/constructor has a
    /// single signature or several overloads (the latter reproduces Lean's
    /// RangeConsolidator(period), which has two int-first constructor overloads).
    /// </summary>
    public class TestFloatToIntConversion
    {
        private PyModule _module;

        private const string TestModule = @"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import IntTaker, OverloadedIntTaker

def single_ctor(value):
    return IntTaker(value).Value

def single_method(value):
    return IntTaker(0).Echo(value)

def overloaded_ctor(value):
    return OverloadedIntTaker(value).Value

def overloaded_method(value):
    return OverloadedIntTaker(0).Echo(value)

def single_named(value):
    return IntTaker(0).ComputeValue(value)

def overloaded_named(value):
    return OverloadedIntTaker(0).ComputeRange(value)

def single_params(value):
    return IntTaker(0).ComputeScaled(value)
";

        [OneTimeSetUp]
        public void Setup()
        {
            PythonEngine.Initialize();
            _module = PyModule.FromString("float_to_int_module", TestModule);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _module.Dispose();
            PythonEngine.Shutdown();
        }

        private int Call(string func, double value)
        {
            using (Py.GIL())
            using (var arg = value.ToPython())
            {
                return _module.InvokeMethod(func, arg).As<int>();
            }
        }

        // An integral-valued float is accepted and converted, single or overloaded.
        [TestCase("single_ctor")]
        [TestCase("single_method")]
        [TestCase("overloaded_ctor")]
        [TestCase("overloaded_method")]
        public void IntegralFloat_IsAccepted(string func)
        {
            Assert.AreEqual(5, Call(func, 5.0));
        }

        // A non-integral float is rejected (no silent truncation) for every target.
        [TestCase("single_ctor")]
        [TestCase("single_method")]
        [TestCase("overloaded_ctor")]
        [TestCase("overloaded_method")]
        public void NonIntegralFloat_IsRejected(string func)
        {
            var ex = Assert.Throws<PythonException>(() => Call(func, 5.5));
            Assert.AreEqual("TypeError", ex.Type.Name);
        }

        // When no overload matches, the error should hint the expected signature(s).
        [Test]
        public void ErrorMessage_SingleOverload_ShowsExpectedSignature()
        {
            var ex = Assert.Throws<PythonException>(() => Call("single_ctor", 5.5));
            StringAssert.Contains("The expected signature is:", ex.Message);
            StringAssert.Contains("value: int", ex.Message);
        }

        [Test]
        public void ErrorMessage_MultipleOverloads_ListsCandidates()
        {
            var ex = Assert.Throws<PythonException>(() => Call("overloaded_ctor", 5.5));
            // The int overload is surfaced, hinting an integer was expected. The
            // PyObject overload is skipped (it carries no type information), which
            // leaves a single hinted signature here.
            StringAssert.Contains("The expected signature is:", ex.Message);
            StringAssert.Contains("range: int", ex.Message);
            StringAssert.DoesNotContain("volume_selector", ex.Message);
        }

        // The hinted signatures use the snake_case name Python callers use, not the
        // original C# name.
        [Test]
        public void ErrorMessage_SingleOverload_UsesSnakeCaseMethodName()
        {
            var ex = Assert.Throws<PythonException>(() => Call("single_named", 5.5));
            StringAssert.Contains("compute_value(", ex.Message);
            StringAssert.DoesNotContain("ComputeValue", ex.Message);
        }

        [Test]
        public void ErrorMessage_MultipleOverloads_UseSnakeCaseMethodName()
        {
            var ex = Assert.Throws<PythonException>(() => Call("overloaded_named", 5.5));
            StringAssert.Contains("compute_range(", ex.Message);
            StringAssert.DoesNotContain("ComputeRange", ex.Message);
        }

        // The hinted signatures also snake_case the parameter names.
        [Test]
        public void ErrorMessage_SignatureParameters_AreSnakeCase()
        {
            var ex = Assert.Throws<PythonException>(() => Call("single_params", 5.5));
            StringAssert.Contains("scale_factor", ex.Message);
            StringAssert.DoesNotContain("scaleFactor", ex.Message);
        }
    }

    public class IntTaker
    {
        public int Value { get; }

        public IntTaker(int value)
        {
            Value = value;
        }

        public int Echo(int value) => value;

        public int ComputeValue(int value) => value;

        public int ComputeScaled(int scaleFactor) => scaleFactor;
    }

    /// <summary>
    /// Mimics Lean's RangeConsolidator: two overloads that both take an int first
    /// parameter, differing only in the (defaulted) later parameters. This forces the
    /// binder through its overload-disambiguation path.
    /// </summary>
    public class OverloadedIntTaker
    {
        public int Value { get; }

        public OverloadedIntTaker(int range, System.Func<int, int> selector = null)
        {
            Value = range;
        }

        public OverloadedIntTaker(int range, PyObject selector, PyObject volumeSelector = null)
        {
            Value = range;
        }

        public int Echo(int value, System.Func<int, int> selector = null) => value;

        public int Echo(int value, PyObject selector, PyObject other = null) => value;

        public int ComputeRange(int value, System.Func<int, int> selector = null) => value;

        public int ComputeRange(int value, PyObject selector, PyObject other = null) => value;
    }
}
