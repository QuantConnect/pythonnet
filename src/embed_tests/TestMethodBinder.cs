using System;
using System.Linq;
using Python.Runtime;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

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
        self.NumericalArgumentMethod(TestMethodBinder.Numpy.float64(0.1))
    def ListKeyValuePairTest(self):
        self.ListKeyValuePair([{'key': 1}])
        self.ListKeyValuePair([])
    def EnumerableKeyValuePairTest(self):
        self.EnumerableKeyValuePair([{'key': 1}])
        self.EnumerableKeyValuePair([])
    def MethodWithParamsTest(self):
        self.MethodWithParams(1, 'pepe')";

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
        public void ImplicitConversionErrorHandling()
        {
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
            CSharpModel.ProvidedArgument = 0;

            module.NumericalArgumentMethodInteger();
            Assert.AreEqual(typeof(int), CSharpModel.ProvidedArgument.GetType());
            Assert.AreEqual(1, CSharpModel.ProvidedArgument);

            // python float type has double precision
            module.NumericalArgumentMethodDouble();
            Assert.AreEqual(typeof(double), CSharpModel.ProvidedArgument.GetType());
            Assert.AreEqual(0.1d, CSharpModel.ProvidedArgument);

            module.NumericalArgumentMethodNumpyFloat();
            Assert.AreEqual(typeof(double), CSharpModel.ProvidedArgument.GetType());
            Assert.AreEqual(0.1d, CSharpModel.ProvidedArgument);

            module.NumericalArgumentMethodNumpy64Float();
            Assert.AreEqual(typeof(decimal), CSharpModel.ProvidedArgument.GetType());
            Assert.AreEqual(0.1, CSharpModel.ProvidedArgument);
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

        [Test]
        public void NumpyDateTime64()
        {
            var number = 10;
            var numpyDateTime = Numpy.datetime64("2011-02");

            object result;
            var converted = Converter.ToManaged(numpyDateTime.Handle, typeof(DateTime), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(new DateTime(2011, 02, 1), result);
        }

        [Test]
        public void ListKeyValuePair()
        {
            Assert.DoesNotThrow(() => module.ListKeyValuePairTest());
        }

        [Test]
        public void EnumerableKeyValuePair()
        {
            Assert.DoesNotThrow(() => module.EnumerableKeyValuePairTest());
        }

        [Test]
        public void MethodWithParamsPerformance()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (var i = 0; i < 100000; i++)
            {
                module.MethodWithParamsTest();
            }
            stopwatch.Stop();

            Console.WriteLine($"Took: {stopwatch.ElapsedMilliseconds}");
        }

        [Test]
        public void NumericalArgumentMethodNumpy64FloatPerformance()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (var i = 0; i < 100000; i++)
            {
                module.NumericalArgumentMethodNumpy64Float();
            }
            stopwatch.Stop();

            Console.WriteLine($"Took: {stopwatch.ElapsedMilliseconds}");
        }

        [Test]
        public void MethodWithParamsTest()
        {
            Assert.DoesNotThrow(() => module.MethodWithParamsTest());
        }

        [Test]
        public void TestNonStaticGenericMethodBinding()
        {
            // Test matching generic on instance functions
            // i.e. function signature is <T>(Generic<T> var1)

            // Run in C#
            var class1 = new TestGenericClass1();
            var class2 = new TestGenericClass2();

            class1.TestNonStaticGenericMethod(class1);
            class2.TestNonStaticGenericMethod(class2);

            Assert.AreEqual(1, class1.Value);
            Assert.AreEqual(1, class2.Value);

            // Run in Python
            Assert.DoesNotThrow(() => PythonEngine.ModuleFromString("test", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *
class1 = TestMethodBinder.TestGenericClass1()
class2 = TestMethodBinder.TestGenericClass2()

class1.TestNonStaticGenericMethod(class1)
class2.TestNonStaticGenericMethod(class2)

if class1.Value != 1 or class2.Value != 1:
    raise AssertionError('Values were not updated')
"));
        }

        [Test]
        public void TestGenericMethodBinding()
        {
            // Test matching generic
            // i.e. function signature is <T>(Generic<T> var1)

            // Run in C#
            var class1 = new TestGenericClass1();
            var class2 = new TestGenericClass2();

            TestGenericMethod(class1);
            TestGenericMethod(class2);

            Assert.AreEqual(1, class1.Value);
            Assert.AreEqual(1, class2.Value);

            // Run in Python
            Assert.DoesNotThrow(() => PythonEngine.ModuleFromString("test", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *
class1 = TestMethodBinder.TestGenericClass1()
class2 = TestMethodBinder.TestGenericClass2()

TestMethodBinder.TestGenericMethod(class1)
TestMethodBinder.TestGenericMethod(class2)

if class1.Value != 1 or class2.Value != 1:
    raise AssertionError('Values were not updated')
"));
        }

        [Test]
        public void TestMultipleGenericMethodBinding()
        {
            // Test matching multiple generics
            // i.e. function signature is <T,K>(Generic<T,K> var1)

            // Run in C#
            var class1 = new TestMultipleGenericClass1();
            var class2 = new TestMultipleGenericClass2();

            TestMultipleGenericMethod(class1);
            TestMultipleGenericMethod(class2);

            Assert.AreEqual(1, class1.Value);
            Assert.AreEqual(1, class2.Value);

            // Run in Python
            Assert.DoesNotThrow(() => PythonEngine.ModuleFromString("test", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *
class1 = TestMethodBinder.TestMultipleGenericClass1()
class2 = TestMethodBinder.TestMultipleGenericClass2()

TestMethodBinder.TestMultipleGenericMethod(class1)
TestMethodBinder.TestMultipleGenericMethod(class2)

if class1.Value != 1 or class2.Value != 1:
    raise AssertionError('Values were not updated')
"));
        }

        [Test]
        public void TestMultipleGenericParamMethodBinding()
        {
            // Test multiple param generics matching
            // i.e. function signature is <T,K>(Generic1<T> var1, Generic<T,K> var2)

            // Run in C#
            var class1a = new TestGenericClass1();
            var class1b = new TestMultipleGenericClass1();

            TestMultipleGenericParamsMethod(class1a, class1b);

            Assert.AreEqual(1, class1a.Value);
            Assert.AreEqual(1, class1a.Value);


            var class2a = new TestGenericClass2();
            var class2b = new TestMultipleGenericClass2();

            TestMultipleGenericParamsMethod(class2a, class2b);

            Assert.AreEqual(1, class2a.Value);
            Assert.AreEqual(1, class2b.Value);

            // Run in Python
            Assert.DoesNotThrow(() => PythonEngine.ModuleFromString("test", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *
class1a = TestMethodBinder.TestGenericClass1()
class1b = TestMethodBinder.TestMultipleGenericClass1()

TestMethodBinder.TestMultipleGenericParamsMethod(class1a, class1b)

if class1a.Value != 1 or class1b.Value != 1:
    raise AssertionError('Values were not updated')

class2a = TestMethodBinder.TestGenericClass2()
class2b = TestMethodBinder.TestMultipleGenericClass2()

TestMethodBinder.TestMultipleGenericParamsMethod(class2a, class2b)

if class2a.Value != 1 or class2b.Value != 1:
    raise AssertionError('Values were not updated')
"));
        }

        [Test]
        public void TestMultipleGenericParamMethodBinding_MixedOrder()
        {
            // Test matching multiple param generics with mixed order
            // i.e. function signature is <T,K>(Generic1<K> var1, Generic<T,K> var2)

            // Run in C#
            var class1a = new TestGenericClass2();
            var class1b = new TestMultipleGenericClass1();

            TestMultipleGenericParamsMethod2(class1a, class1b);

            Assert.AreEqual(1, class1a.Value);
            Assert.AreEqual(1, class1a.Value);

            var class2a = new TestGenericClass1();
            var class2b = new TestMultipleGenericClass2();

            TestMultipleGenericParamsMethod2(class2a, class2b);

            Assert.AreEqual(1, class2a.Value);
            Assert.AreEqual(1, class2b.Value);

            // Run in Python
            Assert.DoesNotThrow(() => PythonEngine.ModuleFromString("test", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *
class1a = TestMethodBinder.TestGenericClass2()
class1b = TestMethodBinder.TestMultipleGenericClass1()

TestMethodBinder.TestMultipleGenericParamsMethod2(class1a, class1b)

if class1a.Value != 1 or class1b.Value != 1:
    raise AssertionError('Values were not updated')

class2a = TestMethodBinder.TestGenericClass1()
class2b = TestMethodBinder.TestMultipleGenericClass2()

TestMethodBinder.TestMultipleGenericParamsMethod2(class2a, class2b)

if class2a.Value != 1 or class2b.Value != 1:
    raise AssertionError('Values were not updated')
"));
        }

        [Test]
        public void TestPyClassGenericBinding()
        {
            // Overriding our generics in Python we should still match with the generic method
            Assert.DoesNotThrow(() => PythonEngine.ModuleFromString("test", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *

class PyGenericClass(TestMethodBinder.TestGenericClass1):
    pass

class PyMultipleGenericClass(TestMethodBinder.TestMultipleGenericClass1):
    pass

singleGenericClass = PyGenericClass()
multiGenericClass = PyMultipleGenericClass()

TestMethodBinder.TestGenericMethod(singleGenericClass)
TestMethodBinder.TestMultipleGenericMethod(multiGenericClass)
TestMethodBinder.TestMultipleGenericParamsMethod(singleGenericClass, multiGenericClass)

if singleGenericClass.Value != 1 or multiGenericClass.Value != 1:
    raise AssertionError('Values were not updated')
"));
        }

        [Test]
        public void TestNonGenericIsUsedWhenAvailable()
        {
            // Run in C#
            var class1 = new TestGenericClass3();
            TestGenericMethod(class1);
            Assert.AreEqual(10, class1.Value);


            // When available, should select non-generic method over generic method
            Assert.DoesNotThrow(() => PythonEngine.ModuleFromString("test", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *

class1 = TestMethodBinder.TestGenericClass3()

TestMethodBinder.TestGenericMethod(class1)

if class1.Value != 10:
    raise AssertionError('Value was not updated')
"));
        }

        [Test]
        public void TestGenericBindingSpeed()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < 10000; i++)
            {
                TestMultipleGenericParamMethodBinding();
            }
            stopwatch.Stop();

            Console.WriteLine($"Took: {stopwatch.ElapsedMilliseconds} ms");
        }

        public class CSharpModel
        {
            public static dynamic ProvidedArgument;
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
                ProvidedArgument = value;
            }
            public void NumericalArgumentMethod(float value)
            {
                ProvidedArgument = value;
            }
            public void NumericalArgumentMethod(double value)
            {
                ProvidedArgument = value;
            }
            public void NumericalArgumentMethod(decimal value)
            {
                ProvidedArgument = value;
            }
            public void EnumerableKeyValuePair(IEnumerable<KeyValuePair<string, decimal>> value)
            {
                ProvidedArgument = value;
            }
            public void ListKeyValuePair(List<KeyValuePair<string, decimal>> value)
            {
                ProvidedArgument = value;
            }

            public void MethodWithParams(decimal value, params string[] argument)
            {

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

        public class GenericClassBase<J>
            where J : class
        {
            public int Value = 0;

            public void TestNonStaticGenericMethod<T>(GenericClassBase<T> test)
                where T : class
            {
                test.Value = 1;
            }
        }

        public static void TestGenericMethod<T>(GenericClassBase<T> test)
            where T : class
        {
            test.Value = 1;
        }

        // Used in test to verify non-generic is bound and used when generic option is also available
        public static void TestGenericMethod(TestGenericClass3 class3)
        {
            class3.Value = 10;
        }

        public class ReferenceClass1
        { }

        public class ReferenceClass2
        { }

        public class TestGenericClass1 : GenericClassBase<ReferenceClass1>
        { }

        public class TestGenericClass2 : GenericClassBase<ReferenceClass2>
        { }

        public class TestGenericClass3 : GenericClassBase<ReferenceClass2>
        { }

        public class MultipleGenericClassBase<T, K>
            where T : class
            where K : class
        {
            public int Value = 0;
        }

        public static void TestMultipleGenericMethod<T, K>(MultipleGenericClassBase<T, K> test)
            where T : class
            where K : class
        {
            test.Value = 1;
        }

        public class TestMultipleGenericClass1 : MultipleGenericClassBase<ReferenceClass1, ReferenceClass2>
        { }

        public class TestMultipleGenericClass2 : MultipleGenericClassBase<ReferenceClass2, ReferenceClass1>
        { }

        public class TestMultipleGenericClass3 : MultipleGenericClassBase<ReferenceClass1, ReferenceClass1>
        { }

        public class TestMultipleGenericClass4 : MultipleGenericClassBase<ReferenceClass2, ReferenceClass2>
        { }

        public static void TestMultipleGenericParamsMethod<T, K>(GenericClassBase<T> singleGeneric, MultipleGenericClassBase<T, K> doubleGeneric)
            where T : class
            where K : class
        {
            singleGeneric.Value = 1;
            doubleGeneric.Value = 1;
        }

        public static void TestMultipleGenericParamsMethod2<T, K>(GenericClassBase<K> singleGeneric, MultipleGenericClassBase<T, K> doubleGeneric)
            where T : class
            where K : class
        {
            singleGeneric.Value = 1;
            doubleGeneric.Value = 1;
        }

    }
}
