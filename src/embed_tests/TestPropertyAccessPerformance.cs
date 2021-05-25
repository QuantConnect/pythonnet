using System;
using System.Diagnostics;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    [TestFixture]
    public class TestPropertyAccessPerformance
    {
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

        [Test]
        [TestCase(true, TestName = "CSharpBenchmark")]
        [TestCase(false, TestName = "PythonBenchmark")]
        public void TestPythonPropertyAccess(bool useCSharp)
        {
            IModel model;
            if (useCSharp)
            {
                model = new CSharpModel();
            }
            else
            {
                var pyModel = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class PythonModel(TestPropertyAccessPerformance.IModel):
    __namespace__ = ""Python.EmbeddingTest""

    def __init__(self):
        self._indicator = TestPropertyAccessPerformance.Indicator()

    def InvokeModel(self):
        value = self._indicator.Current.Value
").GetAttr("PythonModel").Invoke();

                model = new ModelPythonWrapper(pyModel);
            }

            // jit
            model.InvokeModel();

            const int iterations = 5000000;
            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                model.InvokeModel();
            }

            stopwatch.Stop();
            var thousandInvocationsPerSecond = iterations / 1000d / stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine(
                $"Elapsed: {stopwatch.Elapsed.TotalMilliseconds}ms for {iterations} iterations. {thousandInvocationsPerSecond} KIPS");
        }

        public interface IModel
        {
            void InvokeModel();
        }

        public class IndicatorValue
        {
            public int Value => 42;
        }

        public class Indicator
        {
            public IndicatorValue Current { get; } = new IndicatorValue();
        }

        public class CSharpModel : IModel
        {
            private readonly Indicator _indicator = new Indicator();

            public virtual void InvokeModel()
            {
                var value = _indicator.Current.Value;
            }
        }

        public class ModelPythonWrapper : IModel
        {
            private readonly dynamic _invokeModel;

            public ModelPythonWrapper(PyObject impl)
            {
                _invokeModel = impl.GetAttr("InvokeModel");
            }

            public virtual void InvokeModel()
            {
                using (Py.GIL())
                {
                    _invokeModel();
                }
            }
        }
    }
}
