namespace Python.EmbeddingTest
{
    using System;
    using System.Threading;
    using NUnit.Framework;
    using Python.Runtime;

    public class TestGILState
    {
        /// <summary>
        /// Ensure, that calling <see cref="Py.GILState.Dispose"/> multiple times is safe
        /// </summary>
        [Test]
        public void CanDisposeMultipleTimes()
        {
            using (var gilState = Py.GIL())
            {
                for(int i = 0; i < 50; i++)
                    gilState.Dispose();
            }
        }

        /// <summary>
        /// The thread's PyThreadState must survive between GIL scopes. Native extensions
        /// built with pybind11 (e.g. matplotlib >= 3.10 _path/ft2font) cache the pointer
        /// per OS thread and crash with an access violation if a later scope runs after
        /// the thread state was deleted. threading.local values live in the thread state
        /// dictionary, so they survive a second scope only if the thread state did.
        /// </summary>
        [Test]
        public void ThreadStateIsPreservedBetweenGILScopes()
        {
            var result = 0;
            Exception error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    PyModule scope;
                    using (Py.GIL())
                    {
                        scope = Py.CreateScope();
                        scope.Exec("import threading\nlocal = threading.local()\nlocal.value = 42");
                    }
                    using (Py.GIL())
                    using (scope)
                    {
                        using var value = scope.Eval("getattr(local, 'value', -1)");
                        result = value.As<int>();
                    }
                }
                catch (Exception e)
                {
                    error = e;
                }
            });
            // the fixture initializes the engine with the GIL held on this thread:
            // release it so the worker thread can acquire it
            var ts = PythonEngine.BeginAllowThreads();
            try
            {
                thread.Start();
                thread.Join();
            }
            finally
            {
                PythonEngine.EndAllowThreads(ts);
            }

            Assert.IsNull(error);
            Assert.AreEqual(42, result);
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
    }
}
