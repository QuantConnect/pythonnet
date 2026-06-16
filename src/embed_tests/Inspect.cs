using System;
using System.Collections.Generic;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class Inspect
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
        public void InstancePropertiesVisibleOnClass()
        {
            var uri = new Uri("http://example.org").ToPython();
            var uriClass = uri.GetPythonType();
            // Accessing an instance property through the class object invokes the
            // descriptor protocol, which intentionally raises (an instance property
            // must be accessed through an instance). To inspect the descriptor
            // itself, read it from the type's __dict__, which bypasses __get__.
            using var classDict = uriClass.GetAttr("__dict__");
            var property = classDict.GetItem(nameof(Uri.AbsoluteUri)); var pyProp = (PropertyObject)ManagedType.GetManagedObject(property.Reference);
            Assert.AreEqual(nameof(Uri.AbsoluteUri), pyProp.info.Value.Name);
        }

        [Test]
        public void BoundMethodsAreInspectable()
        {
            using var scope = Py.CreateScope();
            try
            {
                scope.Import("inspect");
            }
            catch (PythonException)
            {
                Assert.Inconclusive("Python build does not include inspect module");
                return;
            }

            var obj = new Class();
            scope.Set(nameof(obj), obj);
            using var spec = scope.Eval($"inspect.getfullargspec({nameof(obj)}.{nameof(Class.Method)})");
        }

        class Class
        {
            public void Method(int a, int b = 10) { }
            public void Method(int a, object b) { }
        }
    }
}
