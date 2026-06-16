using System.Diagnostics;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{

    // As the SetUpFixture, the OneTimeTearDown of this class is executed after
    // all tests have run.
    [SetUpFixture]
    public partial class GlobalTestsSetup
    {
        [OneTimeSetUp]
        public void GlobalSetup()
        {
            // The test host installs a trace listener that turns Debug.Assert/Debug.Fail
            // failures into exceptions (DebugAssertException). The runtime uses Debug.Assert
            // for debug-only sanity checks (e.g. metatype dealloc ordering during shutdown,
            // intern-table state on re-initialization) that are compiled out of release builds.
            // Under the test host these would abort otherwise-passing tests and cascade into
            // unrelated fixtures, so we remove the listeners to restore release-like behavior.
            Trace.Listeners.Clear();

            Finalizer.Instance.ErrorHandler += FinalizerErrorHandler;
        }

        private void FinalizerErrorHandler(object sender, Finalizer.ErrorArgs e)
        {
            if (e.Error is RuntimeShutdownException)
            {
                // allow objects to leak after the python runtime run
                // they were created in is gone
                e.Handled = true;
            }
        }

        [OneTimeTearDown]
        public void FinalCleanup()
        {
            if (PythonEngine.IsInitialized)
            {
                PythonEngine.Shutdown();
            }
        }
    }
}
