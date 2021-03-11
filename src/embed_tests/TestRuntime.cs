using System;
using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;
using Python.Runtime.Platform;

namespace Python.EmbeddingTest
{
    public class TestRuntime
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            // We needs to ensure that no any engines are running.
            if (PythonEngine.IsInitialized)
            {
                PythonEngine.Shutdown();
            }
        }

        [Test]
        public static void Py_IsInitializedValue()
        {
            if (Runtime.Runtime.Py_IsInitialized() == 1)
            {
                Runtime.Runtime.PyGILState_Ensure();
            }
            Runtime.Runtime.Py_Finalize();
            Assert.AreEqual(0, Runtime.Runtime.Py_IsInitialized());
            Runtime.Runtime.Py_Initialize();
            Assert.AreEqual(1, Runtime.Runtime.Py_IsInitialized());
            Runtime.Runtime.Py_Finalize();
            Assert.AreEqual(0, Runtime.Runtime.Py_IsInitialized());
        }

        [Test]
        public static void RefCountTest()
        {
            Runtime.Runtime.Py_Initialize();
            IntPtr op = Runtime.Runtime.PyUnicode_FromString("FooBar");

            // New object RefCount should be one
            Assert.AreEqual(1, Runtime.Runtime.Refcount(op));

            // Checking refcount didn't change refcount
            Assert.AreEqual(1, Runtime.Runtime.Refcount(op));

            // New reference doesn't increase refcount
            IntPtr p = op;
            Assert.AreEqual(1, Runtime.Runtime.Refcount(p));

            // Py_IncRef/Py_DecRef increase and decrease RefCount
            Runtime.Runtime.Py_IncRef(op);
            Assert.AreEqual(2, Runtime.Runtime.Refcount(op));
            Runtime.Runtime.Py_DecRef(op);
            Assert.AreEqual(1, Runtime.Runtime.Refcount(op));

            // XIncref/XDecref increase and decrease RefCount
            Runtime.Runtime.XIncref(op);
            Assert.AreEqual(2, Runtime.Runtime.Refcount(op));
            Runtime.Runtime.XDecref(op);
            Assert.AreEqual(1, Runtime.Runtime.Refcount(op));

            Runtime.Runtime.Py_Finalize();
        }

        [Test]
        public static void PyCheck_Iter_PyObject_IsIterable_Test()
        {
            Runtime.Runtime.Py_Initialize();

            // Tests that a python list is an iterable, but not an iterator
            var pyList = Runtime.Runtime.PyList_New(0);
            Assert.IsFalse(Runtime.Runtime.PyIter_Check(pyList));
            Assert.IsTrue(Runtime.Runtime.PyObject_IsIterable(pyList));

            // Tests that a python list iterator is both an iterable and an iterator
            var pyListIter = Runtime.Runtime.PyObject_GetIter(pyList);
            Assert.IsTrue(Runtime.Runtime.PyObject_IsIterable(pyListIter));
            Assert.IsTrue(Runtime.Runtime.PyIter_Check(pyListIter));

            // Tests that a python float is neither an iterable nor an iterator
            var pyFloat = Runtime.Runtime.PyFloat_FromDouble(2.73);
            Assert.IsFalse(Runtime.Runtime.PyObject_IsIterable(pyFloat));
            Assert.IsFalse(Runtime.Runtime.PyIter_Check(pyFloat));

            Runtime.Runtime.Py_Finalize();
        }

        [Test]
        public static void PyCheck_Iter_PyObject_IsIterable_ThreadingLock_Test()
        {
            Runtime.Runtime.Py_Initialize();

            // Create an instance of threading.Lock, which is one of the very few types that does not have the
            // TypeFlags.HaveIter set in Python 2. This tests a different code path in PyObject_IsIterable and PyIter_Check.
            var threading = Runtime.Runtime.PyImport_ImportModule("threading");
            Exceptions.ErrorCheck(threading);
            var threadingDict = Runtime.Runtime.PyModule_GetDict(new BorrowedReference(threading));
            Exceptions.ErrorCheck(threadingDict);
            var lockType = Runtime.Runtime.PyDict_GetItemString(threadingDict, "Lock");
            if (lockType.IsNull)
                throw new KeyNotFoundException("class 'Lock' was not found in 'threading'");

            var args = Runtime.Runtime.PyTuple_New(0);
            var lockInstance = Runtime.Runtime.PyObject_CallObject(lockType.DangerousGetAddress(), args);
            Runtime.Runtime.XDecref(args);
            Exceptions.ErrorCheck(lockInstance);

            Assert.IsFalse(Runtime.Runtime.PyObject_IsIterable(lockInstance));
            Assert.IsFalse(Runtime.Runtime.PyIter_Check(lockInstance));

            Runtime.Runtime.Py_Finalize();
        }


        private static string testModule = @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *
class PythonModule():
    def TestA(self):
        insight = TestRuntime.Insight()
        TestRuntime.EmitInsights(TestRuntime.Group(insight))
    def TestB(self):
        insight = TestRuntime.Insight()
        return TestRuntime.Group(insight)";

        [Test]
        public void QCTestA()
        {
            PythonEngine.Initialize();
            dynamic module = PythonEngine.ModuleFromString("module", testModule).GetAttr("PythonModule").Invoke();
            module.TestA();
            PythonEngine.Shutdown();
        }

        [Test]
        public void QCTestB()
        {
            PythonEngine.Initialize();
            dynamic module = PythonEngine.ModuleFromString("module", testModule).GetAttr("PythonModule").Invoke();
            dynamic ob = module.TestB();
            PythonEngine.Shutdown();
        }


        /// <summary>
        /// Manually emit insights from an algorithm.
        /// This is typically invoked before calls to submit orders in algorithms written against
        /// QCAlgorithm that have been ported into the algorithm framework.
        /// </summary>
        /// <param name="insights">The array of insights to be emitted</param>
        public void EmitInsights(params Insight[] insights)
        {
            foreach (var insight in insights)
            {
                Console.WriteLine(insight.info);
            }
        }

        /// <summary>
        /// Creates a new, unique group id and sets it on each insight
        /// </summary>
        /// <param name="insights">The insights to be grouped</param>
        public static Insight[] Group(params Insight[] insights)
        {
            if (insights == null)
            {
                throw new ArgumentNullException(nameof(insights));
            }

            return insights;
        }

        public class Insight
        {
            public string info;

            public Insight()
            {
                info = "yes";
            }
        }
    }
}
