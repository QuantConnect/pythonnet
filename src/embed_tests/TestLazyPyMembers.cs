using System;
using System.Threading;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestLazyPyMembers
    {
        public static class OverloadedMethods
        {
            public static string Call(string value) => "string";
            public static string Call(object value) => "object";
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

        /// <summary>
        /// Resolving PyDecimalType imports _pydecimal, which runs Python code that can hand the GIL
        /// to another thread. A thread that reaches PyDecimalType meanwhile must not block on it
        /// while holding the GIL, or the importing thread can never finish.
        /// </summary>
        [Test]
        public void ConcurrentFirstResolutionOfDecimalTypeDoesNotDeadlock()
        {
            using var scope = Py.CreateScope();
            scope.Exec(@"
import sys
import threading
from Python.EmbeddingTest import TestLazyPyMembers
OverloadedMethods = TestLazyPyMembers.OverloadedMethods

importing = threading.Event()

class SlowDecimalImport:
    def find_spec(self, name, path, target=None):
        if name == '_pydecimal':
            importing.set()
            # keep the import open, with the GIL released, until the other thread reaches PyDecimalType
            threading.Event().wait(1)
        return None

finder = SlowDecimalImport()
sys.modules.pop('_pydecimal', None)
sys.meta_path.insert(0, finder)
");
            Assert.IsFalse(Runtime.Runtime.PyDecimalType.IsValueCreated);

            var results = new string[2];
            var errors = new Exception[2];
            Thread Start(int index, string code) => new Thread(() =>
            {
                try
                {
                    using (Py.GIL())
                    {
                        results[index] = scope.Eval<string>(code);
                    }
                }
                catch (Exception e)
                {
                    errors[index] = e;
                }
            });
            var first = Start(0, "OverloadedMethods.Call([])");
            var second = Start(1, "importing.wait(10) and OverloadedMethods.Call([])");

            // the fixture initializes the engine with the GIL held on this thread:
            // release it so the worker threads can acquire it
            var ts = PythonEngine.BeginAllowThreads();
            try
            {
                second.Start();
                first.Start();
                Assert.IsTrue(first.Join(TimeSpan.FromSeconds(30)), "first thread deadlocked");
                Assert.IsTrue(second.Join(TimeSpan.FromSeconds(30)), "second thread deadlocked");
            }
            finally
            {
                PythonEngine.EndAllowThreads(ts);
            }
            scope.Exec("sys.meta_path.remove(finder)");

            Assert.IsNull(errors[0]);
            Assert.IsNull(errors[1]);
            Assert.AreEqual("object", results[0]);
            Assert.AreEqual("object", results[1]);
        }
    }
}
