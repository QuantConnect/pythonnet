using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    /// <summary>
    /// The bind-failure TypeError must pinpoint the first argument that fails to
    /// match the nearest overload.
    /// </summary>
    public class TestBindFailureDiagnosis
    {
        public class OrdersTarget
        {
            public string PlaceOrder(string symbol, decimal quantity, bool asynchronous = false, string tag = "", int depth = 0) => "decimal";
            public string PlaceOrder(string symbol, int quantity, bool asynchronous = false, string tag = "", int depth = 0) => "int";
        }

        public class SingleOverloadTarget
        {
            public int Compute(int periods) => periods;
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        private static string TypeErrorMessageOf(string call)
        {
            using var _ = Py.GIL();
            var module = PyModule.FromString("TestBindFailureDiagnosis_" + TestContext.CurrentContext.Test.Name, $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")
AddReference(""System"")

from Python.EmbeddingTest import *

def get_error():
    target = TestBindFailureDiagnosis.OrdersTarget()
    single = TestBindFailureDiagnosis.SingleOverloadTarget()
    try:
        {call}
    except TypeError as e:
        return str(e)
    return None
");
            using var result = module.GetAttr("get_error").Invoke();
            Assert.IsFalse(result.IsNone(), "expected the call to raise a TypeError");
            return result.As<string>();
        }

        [Test]
        public void PinpointsFirstMismatchedPositionalArgument()
        {
            var message = TypeErrorMessageOf("target.place_order('SPY', -10, 'exit signal')");

            Assert.That(message, Does.StartWith("No method matches given arguments for place_order: "));
            Assert.That(message, Does.Contain("The following overloads are available:"));
            Assert.That(message, Does.Contain("Argument mismatch: argument 3 ('asynchronous') expected bool, got str."));
        }

        [Test]
        public void PinpointsMismatchedKeywordArgument()
        {
            var message = TypeErrorMessageOf("target.place_order('SPY', 10, tag=5)");

            Assert.That(message, Does.Contain("Argument mismatch: keyword argument 'tag' expected str, got int."));
        }

        [Test]
        public void PinpointsMismatchOnSingleOverloadMethods()
        {
            var message = TypeErrorMessageOf("single.compute('abc')");

            Assert.That(message, Does.Contain("The expected signature is:"));
            Assert.That(message, Does.Contain("Argument mismatch: argument 1 ('periods') expected int, got str."));
        }

        [Test]
        public void SkipsDiagnosisWhenAllGivenArgumentsMatch()
        {
            // Pure arity failure: no mismatched argument to single out.
            var message = TypeErrorMessageOf("single.compute(1, 2)");

            Assert.That(message, Does.Contain("No method matches given arguments for compute"));
            Assert.That(message, Does.Not.Contain("Argument mismatch:"));
        }

        [Test]
        public void DiagnosisSurvivesTheOverloadsHintExtraction()
        {
            // Lean keeps the message from the overloads marker onwards; the diagnosis
            // must be inside that region to reach users.
            var message = TypeErrorMessageOf("target.place_order('SPY', -10, 'exit signal')");

            var hintStart = message.IndexOf("The following overloads are available:");
            Assert.GreaterOrEqual(hintStart, 0);
            var hint = message.Substring(hintStart);
            Assert.That(hint, Does.Contain("Argument mismatch: argument 3 ('asynchronous') expected bool, got str."));
        }
    }
}
