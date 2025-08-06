using System;
using System.Linq;
using Python.Runtime;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;

namespace Python.EmbeddingTest
{
    public class TestMethodBinder
    {
        private static dynamic module;
        private static string testModule = @"
from datetime import *
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
    def MethodTimeSpanTest(self):
        TestMethodBinder.CSharpModel.MethodDateTimeAndTimeSpan(self, timedelta(days = 1), TestMethodBinder.SomeEnu.A, pinocho = 0)
        TestMethodBinder.CSharpModel.MethodDateTimeAndTimeSpan(self, date(1, 1, 1), TestMethodBinder.SomeEnu.A, pinocho = 0)
        TestMethodBinder.CSharpModel.MethodDateTimeAndTimeSpan(self, datetime(1, 1, 1, 1, 1, 1), TestMethodBinder.SomeEnu.A, pinocho = 0)
    def NumericalArgumentMethodInteger(self):
        self.NumericalArgumentMethod(1)
    def NumericalArgumentMethodDouble(self):
        self.NumericalArgumentMethod(0.1)
    def NumericalArgumentMethodNumpy64Float(self):
        self.NumericalArgumentMethod(TestMethodBinder.Numpy.float64(0.1))
    def ListKeyValuePairTest(self):
        self.ListKeyValuePair([{'key': 1}])
        self.ListKeyValuePair([])
    def EnumerableKeyValuePairTest(self):
        self.EnumerableKeyValuePair([{'key': 1}])
        self.EnumerableKeyValuePair([])
    def MethodWithParamsTest(self):
        self.MethodWithParams(1, 'pepe')

    def TestList(self):
        model = TestMethodBinder.CSharpModel()
        model.List([TestMethodBinder.CSharpModel])
    def TestListReadOnlyCollection(self):
        model = TestMethodBinder.CSharpModel()
        model.ListReadOnlyCollection([TestMethodBinder.CSharpModel])
    def TestEnumerable(self):
        model = TestMethodBinder.CSharpModel()
        model.ListEnumerable([TestMethodBinder.CSharpModel])";

        public static dynamic Numpy;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            PythonEngine.Initialize();
            using var _ = Py.GIL();

            try
            {
                Numpy = Py.Import("numpy");
            }
            catch (PythonException)
            {
            }

            module = PyModule.FromString("module", testModule).GetAttr("PythonModel").Invoke();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        [SetUp]
        public void SetUp()
        {
            CSharpModel.LastDelegateCalled = null;
            CSharpModel.LastFuncCalled = null;
            CSharpModel.MethodCalled = null;
            CSharpModel.ProvidedArgument = null;
        }

        [Test]
        public void MethodCalledList()
        {
            using (Py.GIL())
                module.TestList();
            Assert.AreEqual("List(List<Type> collection)", CSharpModel.MethodCalled);
        }

        [Test]
        public void MethodCalledReadOnlyCollection()
        {
            using (Py.GIL())
                module.TestListReadOnlyCollection();
            Assert.AreEqual("List(IReadOnlyCollection<Type> collection)", CSharpModel.MethodCalled);
        }

        [Test]
        public void MethodCalledEnumerable()
        {
            using (Py.GIL())
                module.TestEnumerable();
            Assert.AreEqual("List(IEnumerable<Type> collection)", CSharpModel.MethodCalled);
        }

        [Test]
        public void ListToEnumerableExpectingMethod()
        {
            using (Py.GIL())
                Assert.DoesNotThrow(() => module.TestF());
        }

        [Test]
        public void ListToListExpectingMethod()
        {
            using (Py.GIL())
                Assert.DoesNotThrow(() => module.TestG());
        }

        [Test]
        public void ImplicitConversionToString()
        {
            using (Py.GIL())
            {
                var data = (string)module.TestA();
                // we assert implicit conversion took place
                Assert.AreEqual("OnlyString impl: implicit to string", data);
            }
        }

        [Test]
        public void ImplicitConversionToClass()
        {
            using (Py.GIL())
            {
                var data = (string)module.TestB();
                // we assert implicit conversion took place
                Assert.AreEqual("OnlyClass impl", data);
            }
        }

        // Reproduces a bug in which program explodes when implicit conversion fails
        // in Linux
        [Test]
        public void ImplicitConversionErrorHandling()
        {
            using (Py.GIL())
            {
                var errorCaught = false;
                try
                {
                    var data = (string)module.TestH();
                }
                catch (Exception e)
                {
                    errorCaught = true;
                    Assert.AreEqual("Failed to implicitly convert Python.EmbeddingTest.TestMethodBinder+ErroredImplicitConversion to System.String", e.Message);
                }

                Assert.IsTrue(errorCaught);
            }
        }

        [Test]
        public void WillAvoidUsingImplicitConversionIfPossible_String()
        {
            using (Py.GIL())
            {
                var data = (string)module.TestC();
                // we assert no implicit conversion took place
                Assert.AreEqual("string impl: input string", data);
            }
        }

        [Test]
        public void WillAvoidUsingImplicitConversionIfPossible_Class()
        {
            using (Py.GIL())
            {
                var data = (string)module.TestD();

                // we assert no implicit conversion took place
                Assert.AreEqual("TestImplicitConversion impl", data);
            }
        }

        [Test]
        public void ArrayLength()
        {
            using (Py.GIL())
            {
                var array = new[] { "pepe", "pinocho" };
                var data = (bool)module.TestE(array);

                // Assert it is true
                Assert.AreEqual(true, data);
            }
        }

        [Test]
        public void MethodDateTimeAndTimeSpan()
        {
            using (Py.GIL())
                Assert.DoesNotThrow(() => module.MethodTimeSpanTest());
        }

        [Test]
        public void NumericalArgumentMethod()
        {
            using (Py.GIL())
            {
                CSharpModel.ProvidedArgument = 0;

                module.NumericalArgumentMethodInteger();
                Assert.AreEqual(typeof(int), CSharpModel.ProvidedArgument.GetType());
                Assert.AreEqual(1, CSharpModel.ProvidedArgument);

                // python float type has double precision
                module.NumericalArgumentMethodDouble();
                Assert.AreEqual(typeof(double), CSharpModel.ProvidedArgument.GetType());
                Assert.AreEqual(0.1d, CSharpModel.ProvidedArgument);

                module.NumericalArgumentMethodNumpy64Float();
                Assert.AreEqual(typeof(decimal), CSharpModel.ProvidedArgument.GetType());
                Assert.AreEqual(0.1, CSharpModel.ProvidedArgument);
            }
        }

        [Test]
        // TODO: see GH issue https://github.com/pythonnet/pythonnet/issues/1532 re importing numpy after an engine restart fails
        // so moving example test here so we import numpy once
        public void TestReadme()
        {
            using (Py.GIL())
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
        }

        [Test]
        public void NumpyDateTime64()
        {
            using (Py.GIL())
            {
                var number = 10;
                var numpyDateTime = Numpy.datetime64("2011-02");

                object result;
                var converted = Converter.ToManaged(numpyDateTime, typeof(DateTime), out result, false);

                Assert.IsTrue(converted);
                Assert.AreEqual(new DateTime(2011, 02, 1), result);
            }
        }

        [Test]
        public void ListKeyValuePair()
        {
            using (Py.GIL())
                Assert.DoesNotThrow(() => module.ListKeyValuePairTest());
        }

        [Test]
        public void EnumerableKeyValuePair()
        {
            using (Py.GIL())
                Assert.DoesNotThrow(() => module.EnumerableKeyValuePairTest());
        }

        [Test]
        public void MethodWithParamsPerformance()
        {
            using (Py.GIL())
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
        }

        [Test]
        public void NumericalArgumentMethodNumpy64FloatPerformance()
        {
            using (Py.GIL())
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
        }

        [Test]
        public void MethodWithParamsTest()
        {
            using (Py.GIL())
                Assert.DoesNotThrow(() => module.MethodWithParamsTest());
        }

        [Test]
        public void TestNonStaticGenericMethodBinding()
        {
            using (Py.GIL())
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
                Assert.DoesNotThrow(() => PyModule.FromString("test", @"
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
        }

        [Test]
        public void TestGenericMethodBinding()
        {
            using (Py.GIL())
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
                Assert.DoesNotThrow(() => PyModule.FromString("test", @"
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
        }

        [Test]
        public void TestMultipleGenericMethodBinding()
        {
            using (Py.GIL())
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
                Assert.DoesNotThrow(() => PyModule.FromString("test", @"
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
        }

        [Test]
        public void TestMultipleGenericParamMethodBinding()
        {
            using (Py.GIL())
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
                Assert.DoesNotThrow(() => PyModule.FromString("test", @"
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
        }

        [Test]
        public void TestMultipleGenericParamMethodBinding_MixedOrder()
        {
            using (Py.GIL())
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
                Assert.DoesNotThrow(() => PyModule.FromString("test", @"
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
        }

        [Test]
        public void TestPyClassGenericBinding()
        {
            using (Py.GIL())
                // Overriding our generics in Python we should still match with the generic method
                Assert.DoesNotThrow(() => PyModule.FromString("test", @"
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
            using (Py.GIL())
            {// Run in C#
                var class1 = new TestGenericClass3();
                TestGenericMethod(class1);
                Assert.AreEqual(10, class1.Value);


                // When available, should select non-generic method over generic method
                Assert.DoesNotThrow(() => PyModule.FromString("test", @"
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
        }

        [Test]
        public void TestMatchTypedGenericOverload()
        {
            using (Py.GIL())
            {// Test to ensure we can match a typed generic overload
                // even when there are other matches that would apply.
                var class1 = new TestGenericClass4();
                TestGenericMethod(class1);
                Assert.AreEqual(15, class1.Value);

                Assert.DoesNotThrow(() => PyModule.FromString("test", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *

class1 = TestMethodBinder.TestGenericClass4()

TestMethodBinder.TestGenericMethod(class1)

if class1.Value != 15:
    raise AssertionError('Value was not updated')
"));
            }
        }

        [Test]
        public void TestGenericBindingSpeed()
        {
            using (Py.GIL())
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
        }

        [Test]
        public void TestGenericTypeMatchingWithConvertedPyType()
        {
            // This test ensures that we can still match and bind a generic method when we
            // have a converted pytype in the args (py timedelta -> C# TimeSpan)

            using (Py.GIL())
                Assert.DoesNotThrow(() => PyModule.FromString("test", @"
from datetime import timedelta
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *
class1 = TestMethodBinder.TestGenericClass1()

span = timedelta(hours=5)

TestMethodBinder.TestGenericMethod(class1, span)

if class1.Value != 5:
    raise AssertionError('Values were not updated properly')
"));
        }

        [Test]
        public void TestGenericTypeMatchingWithDefaultArgs()
        {
            // This test ensures that we can still match and bind a generic method when we have default args

            using (Py.GIL())
                Assert.DoesNotThrow(() => PyModule.FromString("test", @"
from datetime import timedelta
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *
class1 = TestMethodBinder.TestGenericClass1()

TestMethodBinder.TestGenericMethodWithDefault(class1)

if class1.Value != 25:
    raise AssertionError(f'Value was not 25, was {class1.Value}')

TestMethodBinder.TestGenericMethodWithDefault(class1, 50)

if class1.Value != 50:
    raise AssertionError('Value was not 50, was {class1.Value}')
"));
        }

        [Test]
        public void TestGenericTypeMatchingWithNullDefaultArgs()
        {
            // This test ensures that we can still match and bind a generic method when we have \
            // null default args, important because caching by arg types occurs

            using (Py.GIL())
                Assert.DoesNotThrow(() => PyModule.FromString("test", @"
from datetime import timedelta
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *
class1 = TestMethodBinder.TestGenericClass1()

TestMethodBinder.TestGenericMethodWithNullDefault(class1)

if class1.Value != 10:
    raise AssertionError(f'Value was not 25, was {class1.Value}')

TestMethodBinder.TestGenericMethodWithNullDefault(class1, class1)

if class1.Value != 20:
    raise AssertionError('Value was not 50, was {class1.Value}')
"));
        }

        [Test]
        public void TestMatchPyDateToDateTime()
        {
            using (Py.GIL())
                // This test ensures that we match py datetime.date object to C# DateTime object
                Assert.DoesNotThrow(() => PyModule.FromString("test", @"
from datetime import *
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *

test = date(year=2011, month=5, day=1)
result = TestMethodBinder.GetMonth(test)

if result != 5:
    raise AssertionError('Failed to return expected value 1')
"));
        }

        public class OverloadsTestClass
        {

            public string Method1(string positionalArg, decimal namedArg1 = 1.2m, int namedArg2 = 123)
            {
                Console.WriteLine("1");
                return "Method1 Overload 1";
            }

            public string Method1(decimal namedArg1 = 1.2m, int namedArg2 = 123)
            {
                Console.WriteLine("2");
                return "Method1 Overload 2";
            }

            // ----

            public string Method2(string arg1, int arg2, decimal arg3, decimal kwarg1 = 1.1m, bool kwarg2 = false, string kwarg3 = "")
            {
                return "Method2 Overload 1";
            }

            public string Method2(string arg1, int arg2, decimal kwarg1 = 1.1m, bool kwarg2 = false, string kwarg3 = "")
            {
                return "Method2 Overload 2";
            }

            // ----

            public string Method3(string arg1, int arg2, float arg3, float kwarg1 = 1.1f, bool kwarg2 = false, string kwarg3 = "")
            {
                return "Method3 Overload 1";
            }

            public string Method3(string arg1, int arg2, float kwarg1 = 1.1f, bool kwarg2 = false, string kwarg3 = "")
            {
                return "Method3 Overload 2";
            }

            // ----

            public string ImplicitConversionSameArgumentCount(string symbol, int quantity, float trailingAmount, bool trailingAsPercentage, string tag = "")
            {
                return "ImplicitConversionSameArgumentCount 1";
            }

            public string ImplicitConversionSameArgumentCount(string symbol, decimal quantity, decimal trailingAmount, bool trailingAsPercentage, string tag = "")
            {
                return "ImplicitConversionSameArgumentCount 2";
            }

            public string ImplicitConversionSameArgumentCount2(string symbol, int quantity, float trailingAmount, bool trailingAsPercentage, string tag = "")
            {
                return "ImplicitConversionSameArgumentCount2 1";
            }

            public string ImplicitConversionSameArgumentCount2(string symbol, float quantity, float trailingAmount, bool trailingAsPercentage, string tag = "")
            {
                return "ImplicitConversionSameArgumentCount2 2";
            }

            public string ImplicitConversionSameArgumentCount2(string symbol, decimal quantity, float trailingAmount, bool trailingAsPercentage, string tag = "")
            {
                return "ImplicitConversionSameArgumentCount2 2";
            }

            // ----

            public string VariableArgumentsMethod(params CSharpModel[] paramsParams)
            {
                return "VariableArgumentsMethod(CSharpModel[])";
            }

            public string VariableArgumentsMethod(params PyObject[] paramsParams)
            {
                return "VariableArgumentsMethod(PyObject[])";
            }

            // ----

            public string MethodWithEnumParam(SomeEnu enumValue, string symbol)
            {
                return $"MethodWithEnumParam With Enum";
            }

            public string MethodWithEnumParam(PyObject pyObject, string symbol)
            {
                return $"MethodWithEnumParam With PyObject";
            }

            // ----

            public string ConstructorMessage { get; set; }

            public OverloadsTestClass(params CSharpModel[] paramsParams)
            {
                ConstructorMessage = "OverloadsTestClass(CSharpModel[])";
            }

            public OverloadsTestClass(params PyObject[] paramsParams)
            {
                ConstructorMessage = "OverloadsTestClass(PyObject[])";
            }

            public OverloadsTestClass()
            {
            }
        }

        [TestCase("Method1('abc', namedArg1=10, namedArg2=321)", "Method1 Overload 1")]
        [TestCase("Method1('abc', namedArg1=12.34, namedArg2=321)", "Method1 Overload 1")]
        [TestCase("Method2(\"SPY\", 10, 123, kwarg1=1, kwarg2=True)", "Method2 Overload 1")]
        [TestCase("Method2(\"SPY\", 10, 123.34, kwarg1=1.23, kwarg2=True)", "Method2 Overload 1")]
        [TestCase("Method3(\"SPY\", 10, 123.34, kwarg1=1.23, kwarg2=True)", "Method3 Overload 1")]
        public void SelectsRightOverloadWithNamedParameters(string methodCallCode, string expectedResult)
        {
            using var _ = Py.GIL();

            dynamic module = PyModule.FromString("SelectsRightOverloadWithNamedParameters", @$"

def call_method(instance):
    return instance.{methodCallCode}
");

            var instance = new OverloadsTestClass();
            var result = module.call_method(instance).As<string>();

            Assert.AreEqual(expectedResult, result);
        }

        [TestCase("ImplicitConversionSameArgumentCount", "10", "ImplicitConversionSameArgumentCount 1")]
        [TestCase("ImplicitConversionSameArgumentCount", "10.1", "ImplicitConversionSameArgumentCount 2")]
        [TestCase("ImplicitConversionSameArgumentCount2", "10", "ImplicitConversionSameArgumentCount2 1")]
        [TestCase("ImplicitConversionSameArgumentCount2", "10.1", "ImplicitConversionSameArgumentCount2 2")]
        public void DisambiguatesOverloadWithSameArgumentCountAndImplicitConversion(string methodName, string quantity, string expectedResult)
        {
            using var _ = Py.GIL();

            dynamic module = PyModule.FromString("DisambiguatesOverloadWithSameArgumentCountAndImplicitConversion", @$"
def call_method(instance):
    return instance.{methodName}(""SPY"", {quantity}, 123.4, trailingAsPercentage=True)
");

            var instance = new OverloadsTestClass();
            var result = module.call_method(instance).As<string>();

            Assert.AreEqual(expectedResult, result);
        }

        public class CSharpClass
        {
            public string CalledMethodMessage { get; private set; }

            public void Method()
            {
                CalledMethodMessage = "Overload 1";
            }

            public void Method(string stringArgument, decimal decimalArgument = 1.2m)
            {
                CalledMethodMessage = "Overload 2";
            }

            public void Method(PyObject typeArgument, decimal decimalArgument = 1.2m)
            {
                CalledMethodMessage = "Overload 3";
            }
        }

        [Test]
        public void CallsCorrectOverloadWithoutErrors()
        {
            using var _ = Py.GIL();

            var module = PyModule.FromString("CallsCorrectOverloadWithoutErrors", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *

class PythonModel(TestMethodBinder.CSharpModel):
    pass

def call_method(instance):
    instance.Method(PythonModel, decimalArgument=1.234)
");

            var instance = new CSharpClass();
            using var pyInstance = instance.ToPython();

            Assert.DoesNotThrow(() =>
            {
                module.GetAttr("call_method").Invoke(pyInstance);
            });

            Assert.AreEqual("Overload 3", instance.CalledMethodMessage);

            Assert.IsFalse(Exceptions.ErrorOccurred());
        }

        public class CSharpClass2
        {
            public string CalledMethodMessage { get; private set; } = string.Empty;

            public void Clear()
            {
                CalledMethodMessage = string.Empty;
            }

            public void Method()
            {
                CalledMethodMessage = "Overload 1";
            }

            public void Method(CSharpClass csharpClassArgument, decimal decimalArgument = 1.2m, PyObject pyObjectKwArgument = null)
            {
                CalledMethodMessage = "Overload 2";
            }

            public void Method(PyObject pyObjectArgument, decimal decimalArgument = 1.2m, object objectArgument = null)
            {
                CalledMethodMessage = "Overload 3";
            }

            // This must be matched when passing just a single argument and it's a PyObject,
            // event though the PyObject kwarg in the second overload has more precedence.
            // But since it will not be passed, this overload must be called.
            public void Method(PyObject pyObjectArgument, decimal decimalArgument = 1.2m, int intArgument = 0)
            {
                CalledMethodMessage = "Overload 4";
            }
        }

        [Test]
        public void PyObjectArgsHavePrecedenceOverOtherTypes()
        {
            using var _ = Py.GIL();

            var instance = new CSharpClass2();
            using var pyInstance = instance.ToPython();
            using var pyArg = new CSharpClass().ToPython();

            Assert.DoesNotThrow(() =>
            {
                // We are passing a PyObject and not using the named arguments,
                // that overload must be called without converting the PyObject to CSharpClass
                pyInstance.InvokeMethod("Method", pyArg);
            });

            Assert.AreEqual("Overload 4", instance.CalledMethodMessage);
            Assert.IsFalse(Exceptions.ErrorOccurred());
            instance.Clear();

            // With the first named argument
            Assert.DoesNotThrow(() =>
            {
                using var kwargs = Py.kw("decimalArgument", 1.234m);
                pyInstance.InvokeMethod("Method", new[] { pyArg }, kwargs);
            });

            Assert.AreEqual("Overload 4", instance.CalledMethodMessage);
            Assert.IsFalse(Exceptions.ErrorOccurred());
            instance.Clear();

            // Snake case version
            Assert.DoesNotThrow(() =>
            {
                using var kwargs = Py.kw("decimal_argument", 1.234m);
                pyInstance.InvokeMethod("method", new[] { pyArg }, kwargs);
            });

            Assert.AreEqual("Overload 4", instance.CalledMethodMessage);
            Assert.IsFalse(Exceptions.ErrorOccurred());
        }

        [Test]
        public void OtherTypesHavePrecedenceOverPyObjectArgsIfMoreArgsAreMatched()
        {
            using var _ = Py.GIL();

            var instance = new CSharpClass2();
            using var pyInstance = instance.ToPython();
            using var pyArg = new CSharpClass().ToPython();

            Assert.DoesNotThrow(() =>
            {
                using var kwargs = Py.kw("pyObjectKwArgument", new CSharpClass2());
                pyInstance.InvokeMethod("Method", new[] { pyArg }, kwargs);
            });

            Assert.AreEqual("Overload 2", instance.CalledMethodMessage);
            Assert.IsFalse(Exceptions.ErrorOccurred());
            instance.Clear();

            Assert.DoesNotThrow(() =>
            {
                using var kwargs = Py.kw("py_object_kw_argument", new CSharpClass2());
                pyInstance.InvokeMethod("method", new[] { pyArg }, kwargs);
            });

            Assert.AreEqual("Overload 2", instance.CalledMethodMessage);
            Assert.IsFalse(Exceptions.ErrorOccurred());
            instance.Clear();

            Assert.DoesNotThrow(() =>
            {
                using var kwargs = Py.kw("objectArgument", "somestring");
                pyInstance.InvokeMethod("Method", new[] { pyArg }, kwargs);
            });

            Assert.AreEqual("Overload 3", instance.CalledMethodMessage);
            Assert.IsFalse(Exceptions.ErrorOccurred());
            instance.Clear();

            Assert.DoesNotThrow(() =>
            {
                using var kwargs = Py.kw("object_argument", "somestring");
                pyInstance.InvokeMethod("method", new[] { pyArg }, kwargs);
            });

            Assert.AreEqual("Overload 3", instance.CalledMethodMessage);
            Assert.IsFalse(Exceptions.ErrorOccurred());
            instance.Clear();
        }

        [Test]
        public void BindsConstructorToSnakeCasedArgumentsVersion([Values] bool useCamelCase, [Values] bool passOptionalArgument)
        {
            using var _ = Py.GIL();

            var argument1Name = useCamelCase ? "someArgument" : "some_argument";
            var argument2Name = useCamelCase ? "anotherArgument" : "another_argument";
            var argument2Code = passOptionalArgument ? $", {argument2Name}=\"another argument value\"" : "";

            var module = PyModule.FromString("BindsConstructorToSnakeCasedArgumentsVersion", @$"
from clr import AddReference
AddReference(""System"")
from Python.EmbeddingTest import *

def create_instance():
    return TestMethodBinder.CSharpModel({argument1Name}=1{argument2Code})
");
            var exception = Assert.Throws<ClrBubbledException>(() => module.GetAttr("create_instance").Invoke());
            var sourceException = exception.InnerException;
            Assert.IsInstanceOf<NotImplementedException>(sourceException);

            var expectedMessage = passOptionalArgument
                ? "Constructor with arguments: someArgument=1. anotherArgument=\"another argument value\""
                : "Constructor with arguments: someArgument=1. anotherArgument=\"another argument default value\"";
            Assert.AreEqual(expectedMessage, sourceException.Message);
        }

        [Test]
        public void PyObjectArrayHasPrecedenceOverOtherTypeArrays()
        {
            using var _ = Py.GIL();

            var module = PyModule.FromString("PyObjectArrayHasPrecedenceOverOtherTypeArrays", @$"
from clr import AddReference
AddReference(""System"")
from Python.EmbeddingTest import *

class PythonModel(TestMethodBinder.CSharpModel):
    pass

def call_method():
    return TestMethodBinder.OverloadsTestClass().VariableArgumentsMethod(PythonModel(), PythonModel())
");

            var result = module.GetAttr("call_method").Invoke().As<string>();
            Assert.AreEqual("VariableArgumentsMethod(PyObject[])", result);
        }

        [Test]
        public void PyObjectArrayHasPrecedenceOverOtherTypeArraysInConstructors()
        {
            using var _ = Py.GIL();

            var module = PyModule.FromString("PyObjectArrayHasPrecedenceOverOtherTypeArrays", @$"
from clr import AddReference
AddReference(""System"")
from Python.EmbeddingTest import *

class PythonModel(TestMethodBinder.CSharpModel):
    pass

def get_instance():
    return TestMethodBinder.OverloadsTestClass(PythonModel(), PythonModel())
");

            var instance = module.GetAttr("get_instance").Invoke();
            Assert.AreEqual("OverloadsTestClass(PyObject[])", instance.GetAttr("ConstructorMessage").As<string>());
        }

        [Test]
        public void EnumHasPrecedenceOverPyObject()
        {
            using var _ = Py.GIL();

            var module = PyModule.FromString("EnumHasPrecedenceOverPyObject", @$"
from clr import AddReference
AddReference(""System"")
from Python.EmbeddingTest import *

class PythonModel(TestMethodBinder.CSharpModel):
    pass

def call_method():
    return TestMethodBinder.OverloadsTestClass().MethodWithEnumParam(TestMethodBinder.SomeEnu.A, ""Some string"")
");

            var result = module.GetAttr("call_method").Invoke();
            Assert.AreEqual("MethodWithEnumParam With Enum", result.As<string>());
        }

        [TestCase("call_method_with_func1", "MethodWithFunc1", "func1")]
        [TestCase("call_method_with_func2", "MethodWithFunc2", "func2")]
        [TestCase("call_method_with_func3", "MethodWithFunc3", "func3")]
        [TestCase("call_method_with_func1_lambda", "MethodWithFunc1", "func1")]
        [TestCase("call_method_with_func2_lambda", "MethodWithFunc2", "func2")]
        [TestCase("call_method_with_func3_lambda", "MethodWithFunc3", "func3")]
        public void BindsPythonToCSharpFuncDelegates(string pythonFuncToCall, string expectedCSharpMethodCalled, string expectedPythonFuncCalled)
        {
            using var _ = Py.GIL();

            var module = PyModule.FromString("BindsPythonToCSharpFuncDelegates", @$"
from clr import AddReference
AddReference(""System"")
from Python.EmbeddingTest import *

from System import Func

class PythonModel:
    last_delegate_called = None

def func1():
    PythonModel.last_delegate_called = 'func1'
    return TestMethodBinder.CSharpModel();

def func2(model):
    if model is None or not isinstance(model, TestMethodBinder.CSharpModel):
        raise TypeError(""model must be of type CSharpModel"")
    PythonModel.last_delegate_called = 'func2'
    return model

def func3(model1, model2):
    if model1 is None or model2 is None or not isinstance(model1, TestMethodBinder.CSharpModel) or not isinstance(model2, TestMethodBinder.CSharpModel):
        raise TypeError(""model1 and model2 must be of type CSharpModel"")
    PythonModel.last_delegate_called = 'func3'
    return model1

def call_method_with_func1():
    return TestMethodBinder.CSharpModel.MethodWithFunc1(func1)

def call_method_with_func2():
    return TestMethodBinder.CSharpModel.MethodWithFunc2(func2)

def call_method_with_func3():
    return TestMethodBinder.CSharpModel.MethodWithFunc3(func3)

def call_method_with_func1_lambda():
    return TestMethodBinder.CSharpModel.MethodWithFunc1(lambda: func1())

def call_method_with_func2_lambda():
    return TestMethodBinder.CSharpModel.MethodWithFunc2(lambda model: func2(model))

def call_method_with_func3_lambda():
    return TestMethodBinder.CSharpModel.MethodWithFunc3(lambda model1, model2: func3(model1, model2))
");

            CSharpModel managedResult = null;
            Assert.DoesNotThrow(() =>
            {
                using var result = module.GetAttr(pythonFuncToCall).Invoke();
                managedResult = result.As<CSharpModel>();
            });

            Assert.IsNotNull(managedResult);
            Assert.AreEqual(expectedCSharpMethodCalled, CSharpModel.LastDelegateCalled);

            using var pythonModel = module.GetAttr("PythonModel");
            using var lastDelegateCalled = pythonModel.GetAttr("last_delegate_called");
            Assert.AreEqual(expectedPythonFuncCalled, lastDelegateCalled.As<string>());
        }

        [TestCase("call_method_with_action1", "MethodWithAction1", "action1")]
        [TestCase("call_method_with_action2", "MethodWithAction2", "action2")]
        [TestCase("call_method_with_action3", "MethodWithAction3", "action3")]
        [TestCase("call_method_with_action1_lambda", "MethodWithAction1", "action1")]
        [TestCase("call_method_with_action2_lambda", "MethodWithAction2", "action2")]
        [TestCase("call_method_with_action3_lambda", "MethodWithAction3", "action3")]
        public void BindsPythonToCSharpActionDelegates(string pythonFuncToCall, string expectedCSharpMethodCalled, string expectedPythonFuncCalled)
        {
            using var _ = Py.GIL();

            var module = PyModule.FromString("BindsPythonToCSharpActionDelegates", @$"
from clr import AddReference
AddReference(""System"")
from Python.EmbeddingTest import *

from System import Func

class PythonModel:
    last_delegate_called = None

def action1():
    PythonModel.last_delegate_called = 'action1'
    pass

def action2(model):
    if model is None or not isinstance(model, TestMethodBinder.CSharpModel):
        raise TypeError(""model must be of type CSharpModel"")
    PythonModel.last_delegate_called = 'action2'
    pass

def action3(model1, model2):
    if model1 is None or model2 is None or not isinstance(model1, TestMethodBinder.CSharpModel) or not isinstance(model2, TestMethodBinder.CSharpModel):
        raise TypeError(""model1 and model2 must be of type CSharpModel"")
    PythonModel.last_delegate_called = 'action3'
    pass

def call_method_with_action1():
    return TestMethodBinder.CSharpModel.MethodWithAction1(action1)

def call_method_with_action2():
    return TestMethodBinder.CSharpModel.MethodWithAction2(action2)

def call_method_with_action3():
    return TestMethodBinder.CSharpModel.MethodWithAction3(action3)

def call_method_with_action1_lambda():
    return TestMethodBinder.CSharpModel.MethodWithAction1(lambda: action1())

def call_method_with_action2_lambda():
    return TestMethodBinder.CSharpModel.MethodWithAction2(lambda model: action2(model))

def call_method_with_action3_lambda():
    return TestMethodBinder.CSharpModel.MethodWithAction3(lambda model1, model2: action3(model1, model2))
");

            Assert.DoesNotThrow(() =>
            {
                using var result = module.GetAttr(pythonFuncToCall).Invoke();
            });

            Assert.AreEqual(expectedCSharpMethodCalled, CSharpModel.LastDelegateCalled);

            using var pythonModel = module.GetAttr("PythonModel");
            using var lastDelegateCalled = pythonModel.GetAttr("last_delegate_called");
            Assert.AreEqual(expectedPythonFuncCalled, lastDelegateCalled.As<string>());
        }

        [TestCase("call_method_with_func1", "MethodWithFunc1", "TestFunc1")]
        [TestCase("call_method_with_func2", "MethodWithFunc2", "TestFunc2")]
        [TestCase("call_method_with_func3", "MethodWithFunc3", "TestFunc3")]
        public void BindsCSharpFuncFromPythonToCSharpFuncDelegates(string pythonFuncToCall, string expectedMethodCalled, string expectedInnerMethodCalled)
        {
            using var _ = Py.GIL();

            var module = PyModule.FromString("BindsCSharpFuncFromPythonToCSharpFuncDelegates", @$"
from clr import AddReference
AddReference(""System"")
from Python.EmbeddingTest import *

def call_method_with_func1():
    return TestMethodBinder.CSharpModel.MethodWithFunc1(TestMethodBinder.CSharpModel.TestFunc1)

def call_method_with_func2():
    return TestMethodBinder.CSharpModel.MethodWithFunc2(TestMethodBinder.CSharpModel.TestFunc2)

def call_method_with_func3():
    return TestMethodBinder.CSharpModel.MethodWithFunc3(TestMethodBinder.CSharpModel.TestFunc3)
");

            CSharpModel managedResult = null;
            Assert.DoesNotThrow(() =>
            {
                using var result = module.GetAttr(pythonFuncToCall).Invoke();
                managedResult = result.As<CSharpModel>();
            });
            Assert.IsNotNull(managedResult);
            Assert.AreEqual(expectedMethodCalled, CSharpModel.LastDelegateCalled);
            Assert.AreEqual(expectedInnerMethodCalled, CSharpModel.LastFuncCalled);
        }

        [TestCase("call_method_with_action1", "MethodWithAction1", "TestAction1")]
        [TestCase("call_method_with_action2", "MethodWithAction2", "TestAction2")]
        [TestCase("call_method_with_action3", "MethodWithAction3", "TestAction3")]
        public void BindsCSharpActionFromPythonToCSharpActionDelegates(string pythonFuncToCall, string expectedMethodCalled, string expectedInnerMethodCalled)
        {
            using var _ = Py.GIL();

            var module = PyModule.FromString("BindsCSharpActionFromPythonToCSharpActionDelegates", @$"
from clr import AddReference
AddReference(""System"")
from Python.EmbeddingTest import *

def call_method_with_action1():
    return TestMethodBinder.CSharpModel.MethodWithAction1(TestMethodBinder.CSharpModel.TestAction1)

def call_method_with_action2():
    return TestMethodBinder.CSharpModel.MethodWithAction2(TestMethodBinder.CSharpModel.TestAction2)

def call_method_with_action3():
    return TestMethodBinder.CSharpModel.MethodWithAction3(TestMethodBinder.CSharpModel.TestAction3)
");

            Assert.DoesNotThrow(() =>
            {
                using var result = module.GetAttr(pythonFuncToCall).Invoke();
            });
            Assert.AreEqual(expectedMethodCalled, CSharpModel.LastDelegateCalled);
            Assert.AreEqual(expectedInnerMethodCalled, CSharpModel.LastFuncCalled);
        }

        [Test]
        public void NumericArgumentsTakePrecedenceOverEnums()
        {
            using var _ = Py.GIL();

            var module = PyModule.FromString("NumericArgumentsTakePrecedenceOverEnums", @$"
from clr import AddReference
AddReference(""System"")
from Python.EmbeddingTest import *
from System import DayOfWeek

def call_method_with_int():
    TestMethodBinder.CSharpModel().NumericalArgumentMethod(1)

def call_method_with_float():
    TestMethodBinder.CSharpModel().NumericalArgumentMethod(0.1)

def call_method_with_numpy_float():
    TestMethodBinder.CSharpModel().NumericalArgumentMethod(TestMethodBinder.Numpy.float64(0.1))

def call_method_with_enum():
    TestMethodBinder.CSharpModel().NumericalArgumentMethod(DayOfWeek.MONDAY)
");

            module.GetAttr("call_method_with_int").Invoke();
            Assert.AreEqual(typeof(int), CSharpModel.ProvidedArgument.GetType());
            Assert.AreEqual(1, CSharpModel.ProvidedArgument);

            module.GetAttr("call_method_with_float").Invoke();
            Assert.AreEqual(typeof(double), CSharpModel.ProvidedArgument.GetType());
            Assert.AreEqual(0.1d, CSharpModel.ProvidedArgument);

            module.GetAttr("call_method_with_numpy_float").Invoke();
            Assert.AreEqual(typeof(decimal), CSharpModel.ProvidedArgument.GetType());
            Assert.AreEqual(0.1m, CSharpModel.ProvidedArgument);

            module.GetAttr("call_method_with_enum").Invoke();
            Assert.AreEqual(typeof(DayOfWeek), CSharpModel.ProvidedArgument.GetType());
            Assert.AreEqual(DayOfWeek.Monday, CSharpModel.ProvidedArgument);
        }

        // Used to test that we match this function with Py DateTime & Date Objects
        public static int GetMonth(DateTime test)
        {
            return test.Month;
        }

        public class CSharpModel
        {
            public static string MethodCalled { get; set; }
            public static dynamic ProvidedArgument;
            public List<TestImplicitConversion> SomeList { get; set; }

            public CSharpModel()
            {
                SomeList = new List<TestImplicitConversion>
                {
                    new TestImplicitConversion()
                };
            }

            public CSharpModel(int someArgument, string anotherArgument = "another argument default value")
            {
                throw new NotImplementedException($"Constructor with arguments: someArgument={someArgument}. anotherArgument=\"{anotherArgument}\"");
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
            public void NumericalArgumentMethod(DayOfWeek value)
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

            public void ListReadOnlyCollection(IReadOnlyCollection<Type> collection)
            {
                MethodCalled = "List(IReadOnlyCollection<Type> collection)";
            }
            public void List(List<Type> collection)
            {
                MethodCalled = "List(List<Type> collection)";
            }
            public void ListEnumerable(IEnumerable<Type> collection)
            {
                MethodCalled = "List(IEnumerable<Type> collection)";
            }

            private static void AssertErrorNotOccurred()
            {
                using (Py.GIL())
                {
                    if (Exceptions.ErrorOccurred())
                    {
                        throw new Exception("Error occurred");
                    }
                }
            }

            public static void MethodDateTimeAndTimeSpan(CSharpModel pepe, SomeEnu @someEnu, int integer, double? jose = null, double? pinocho = null)
            {
                AssertErrorNotOccurred();
            }
            public static void MethodDateTimeAndTimeSpan(CSharpModel pepe, DateTime dateTime, SomeEnu someEnu, double? jose = null, double? pinocho = null)
            {
                AssertErrorNotOccurred();
            }
            public static void MethodDateTimeAndTimeSpan(CSharpModel pepe, TimeSpan timeSpan, SomeEnu someEnu, double? jose = null, double? pinocho = null)
            {
                AssertErrorNotOccurred();
            }
            public static void MethodDateTimeAndTimeSpan(CSharpModel pepe, Func<DateTime, DateTime> func, SomeEnu someEnu, double? jose = null, double? pinocho = null)
            {
                AssertErrorNotOccurred();
            }

            public static string LastDelegateCalled { get; set; }
            public static string LastFuncCalled { get; set; }

            public static CSharpModel MethodWithFunc1(Func<CSharpModel> func)
            {
                AssertErrorNotOccurred();
                LastDelegateCalled = "MethodWithFunc1";
                return func();
            }

            public static CSharpModel MethodWithFunc2(Func<CSharpModel, CSharpModel> func)
            {
                AssertErrorNotOccurred();
                LastDelegateCalled = "MethodWithFunc2";
                return func(new CSharpModel());
            }

            public static CSharpModel MethodWithFunc3(Func<CSharpModel, CSharpModel, CSharpModel> func)
            {
                AssertErrorNotOccurred();
                LastDelegateCalled = "MethodWithFunc3";
                return func(new CSharpModel(), new CSharpModel());
            }

            public static void MethodWithAction1(Action action)
            {
                AssertErrorNotOccurred();
                LastDelegateCalled = "MethodWithAction1";
                action();
            }

            public static void MethodWithAction2(Action<CSharpModel> action)
            {
                AssertErrorNotOccurred();
                LastDelegateCalled = "MethodWithAction2";
                action(new CSharpModel());
            }

            public static void MethodWithAction3(Action<CSharpModel, CSharpModel> action)
            {
                AssertErrorNotOccurred();
                LastDelegateCalled = "MethodWithAction3";
                action(new CSharpModel(), new CSharpModel());
            }

            public static CSharpModel TestFunc1()
            {
                LastFuncCalled = "TestFunc1";
                return new CSharpModel();
            }

            public static CSharpModel TestFunc2(CSharpModel model)
            {
                if (model == null)
                {
                    throw new ArgumentNullException(nameof(model));
                }
                LastFuncCalled = "TestFunc2";
                return model;
            }

            public static CSharpModel TestFunc3(CSharpModel model1, CSharpModel model2)
            {
                if (model1 == null || model2 == null)
                {
                    throw new ArgumentNullException(model1 == null ? nameof(model1) : nameof(model2));
                }
                LastFuncCalled = "TestFunc3";
                return model1;
            }

            public static void TestAction1()
            {
                LastFuncCalled = "TestAction1";
            }

            public static void TestAction2(CSharpModel model)
            {
                if (model == null)
                {
                    throw new ArgumentNullException(nameof(model));
                }
                LastFuncCalled = "TestAction2";
            }

            public static void TestAction3(CSharpModel model1, CSharpModel model2)
            {
                if (model1 == null || model2 == null)
                {
                    throw new ArgumentNullException(model1 == null ? nameof(model1) : nameof(model2));
                }
                LastFuncCalled = "TestAction3";
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

        // Used to test that when a generic option is available but the parameter is already typed it doesn't
        // match to the wrong one. This is an example of a typed generic parameter
        public static void TestGenericMethod(GenericClassBase<ReferenceClass3> test)
        {
            test.Value = 15;
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

        // Used in test to verify generic binding when converted PyTypes are involved (timedelta -> TimeSpan)
        public static void TestGenericMethod<T>(GenericClassBase<T> test, TimeSpan span)
        where T : class
        {
            test.Value = span.Hours;
        }

        // Used in test to verify generic binding when defaults are used
        public static void TestGenericMethodWithDefault<T>(GenericClassBase<T> test, int value = 25)
        where T : class
        {
            test.Value = value;
        }

        // Used in test to verify generic binding when null defaults are used
        public static void TestGenericMethodWithNullDefault<T>(GenericClassBase<T> test, Object testObj = null)
        where T : class
        {
            if (testObj == null)
            {
                test.Value = 10;
            }
            else
            {
                test.Value = 20;
            }
        }

        public class ReferenceClass1
        { }

        public class ReferenceClass2
        { }

        public class ReferenceClass3
        { }

        public class TestGenericClass1 : GenericClassBase<ReferenceClass1>
        { }

        public class TestGenericClass2 : GenericClassBase<ReferenceClass2>
        { }

        public class TestGenericClass3 : GenericClassBase<ReferenceClass2>
        { }

        public class TestGenericClass4 : GenericClassBase<ReferenceClass3>
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

        public enum SomeEnu
        {
            A = 1,
            B = 2,
        }
    }
}
