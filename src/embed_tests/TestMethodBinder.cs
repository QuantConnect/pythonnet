using System;
using System.Linq;
using Python.Runtime;
using NUnit.Framework;
using System.Collections.Generic;

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
        return self.OnlyString(TestMethodBinder.ErroredImplicitConversion())
    def NumericalArgumentMethodInteger(self):
        self.NumericalArgumentMethod(1)
    def NumericalArgumentMethodDouble(self):
        self.NumericalArgumentMethod(0.1)
    def NumericalArgumentMethodNumpyFloat(self):
        self.NumericalArgumentMethod(TestMethodBinder.Numpy.float(0.1))
    def NumericalArgumentMethodNumpy64Float(self):
        self.NumericalArgumentMethod(TestMethodBinder.Numpy.float64(0.1))";

        public static dynamic Numpy;

        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();

            try
            {
                Numpy = Py.Import("numpy");
            }
            catch (PythonException)
            {
            }
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

        [Test]
        public void NumericalArgumentMethod()
        {
            CSharpModel.NumericalValue = 0;

            module.NumericalArgumentMethodInteger();
            Assert.AreEqual(typeof(int), CSharpModel.NumericalValue.GetType());
            Assert.AreEqual(1, CSharpModel.NumericalValue);

            // python float type has double precision
            module.NumericalArgumentMethodDouble();
            Assert.AreEqual(typeof(double), CSharpModel.NumericalValue.GetType());
            Assert.AreEqual(0.1d, CSharpModel.NumericalValue);

            module.NumericalArgumentMethodNumpyFloat();
            Assert.AreEqual(typeof(double), CSharpModel.NumericalValue.GetType());
            Assert.AreEqual(0.1d, CSharpModel.NumericalValue);

            module.NumericalArgumentMethodNumpy64Float();
            Assert.AreEqual(typeof(decimal), CSharpModel.NumericalValue.GetType());
            Assert.AreEqual(0.1, CSharpModel.NumericalValue);
        }

        [Test]
        // TODO: see GH issue https://github.com/pythonnet/pythonnet/issues/1532 re importing numpy after an engine restart fails
        // so moving example test here so we import numpy once
        public void TestReadme()
        {
            Assert.AreEqual("1.0", Numpy.cos(Numpy.pi * 2).ToString());

            dynamic sin = Numpy.sin;
            StringAssert.StartsWith("-0.95892", sin(5).ToString());

            double c = Numpy.cos(5) + sin(5);
            Assert.AreEqual(-0.675262, c, 0.01);

            dynamic a = Numpy.array(new List<float> { 1, 2, 3 });
            Assert.AreEqual("float64", a.dtype.ToString());

            dynamic b = Numpy.array(new List<float> { 6, 5, 4 }, Py.kw("dtype", Numpy.int32));
            Assert.AreEqual("int32", b.dtype.ToString());

            Assert.AreEqual("[ 6. 10. 12.]", (a * b).ToString().Replace("  ", " "));
        }

        public class CSharpModel
        {
            public static dynamic NumericalValue;
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

            public void NumericalArgumentMethod(int value)
            {
                NumericalValue = value;
            }
            public void NumericalArgumentMethod(float value)
            {
                NumericalValue = value;
            }
            public void NumericalArgumentMethod(double value)
            {
                NumericalValue = value;
            }
            public void NumericalArgumentMethod(decimal value)
            {
                NumericalValue = value;
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
