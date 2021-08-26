using System;
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestMethodBinder
    {
        private static dynamic module;
        private static string testModule = @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *
class PythonModel(TestMethodBinder.CSharpModel):
    def TestA(self):
        return self.OnlyString(TestMethodBinder.TestImplicitConversion())
    def TestB(self):
        return self.OnlyClass('input string')
    def TestC(self):
        return self.InvokeModel('input string')
    def TestD(self):
        return self.InvokeModel(TestMethodBinder.TestImplicitConversion())
    def TestE(self, array):
        return array.Length == 2
    def TestF(self):
        model = TestMethodBinder.CSharpModel()
        model.TestEnumerable(model.SomeList)
    def TestG(self):
        model = TestMethodBinder.CSharpModel()
        model.TestList(model.SomeList)
    def TestH(self):
        return self.OnlyString(TestMethodBinder.ErroredImplicitConversion())";


        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
            module = PythonEngine.ModuleFromString("module", testModule).GetAttr("PythonModel").Invoke();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        [Test]
        public void ListToEnumerableExpectingMethod()
        {
            Assert.DoesNotThrow(() => module.TestF());
        }

        [Test]
        public void ListToListExpectingMethod()
        {
            Assert.DoesNotThrow(() => module.TestG());
        }

        [Test]
        public void ImplicitConversionToString()
        {
            var data = (string)module.TestA();
            // we assert implicit conversion took place
            Assert.AreEqual("OnlyString impl: implicit to string", data);
        }

        [Test]
        public void ImplicitConversionToClass()
        {
            var data = (string)module.TestB();
            // we assert implicit conversion took place
            Assert.AreEqual("OnlyClass impl", data);
        }

        // Reproduces a bug in which program explodes when implicit conversion fails
        // in Linux
        [Test]
        public void ImplicitConversionErrorHandling(){
            var errorCaught = false;
            try
            {
                var data = (string)module.TestH();
            }
            catch (Exception e)
            {
                errorCaught = true;
                Assert.AreEqual("TypeError : Failed to implicitly convert Python.EmbeddingTest.TestMethodBinder+ErroredImplicitConversion to System.String", e.Message);
            }

            Assert.IsTrue(errorCaught);
        }

        [Test]
        public void WillAvoidUsingImplicitConversionIfPossible_String()
        {
            var data = (string)module.TestC();
            // we assert no implicit conversion took place
            Assert.AreEqual("string impl: input string", data);
        }

        [Test]
        public void WillAvoidUsingImplicitConversionIfPossible_Class()
        {
            var data = (string)module.TestD();
            // we assert no implicit conversion took place
            Assert.AreEqual("TestImplicitConversion impl", data);
            
        }

        [Test]
        public void ArrayLength()
        {
            var array = new[] { "pepe", "pinocho" };
            var data = (bool)module.TestE(array);

            // Assert it is true
            Assert.AreEqual(true, data);
        }

        public class CSharpModel
        {
            public List<TestImplicitConversion> SomeList { get; set; }

            public CSharpModel()
            {
                SomeList = new List<TestImplicitConversion>
                {
                    new TestImplicitConversion()
                };
            }
            public void TestList(List<TestImplicitConversion> conversions)
            {
                if (!conversions.Any())
                {
                    throw new ArgumentException("We expect at least an instance");
                }
            }

            public void TestEnumerable(IEnumerable<TestImplicitConversion> conversions)
            {
                if (!conversions.Any())
                {
                    throw new ArgumentException("We expect at least an instance");
                }
            }

            public bool SomeMethod()
            {
                return true;
            }

            public virtual string OnlyClass(TestImplicitConversion data)
            {
                return "OnlyClass impl";
            }

            public virtual string OnlyString(string data)
            {
                return "OnlyString impl: " + data;
            }

            public virtual string InvokeModel(string data)
            {
                return "string impl: " + data;
            }

            public virtual string InvokeModel(TestImplicitConversion data)
            {
                return "TestImplicitConversion impl";
            }
        }

        public class TestImplicitConversion
        {
            public static implicit operator string(TestImplicitConversion symbol)
            {
                return "implicit to string";
            }
            public static implicit operator TestImplicitConversion(string symbol)
            {
                return new TestImplicitConversion();
            }
        }

        public class ErroredImplicitConversion
        {
            public static implicit operator string(ErroredImplicitConversion symbol)
            {
                throw new ArgumentException();
            }
            public static implicit operator ErroredImplicitConversion(string symbol)
            {
                throw new ArgumentException();
            }
        }
    }
}
