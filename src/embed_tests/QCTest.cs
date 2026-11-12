using System;
using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    class QCTests
    {
        private static dynamic pythonSuperInitInt;
        private static dynamic pythonSuperInitDefault;
        private static dynamic pythonSuperInitNone;
        private static dynamic pythonSuperInitNotCallingBase;

        private static dynamic withArgs_PythonSuperInitNotCallingBase;
        private static dynamic withArgs_PythonSuperInitDefault;
        private static dynamic withArgs_PythonSuperInitInt;

        private static dynamic pureCSharpConstruction;
        private static dynamic pureCSharpDefaultConstruction;
        private static dynamic pureCSharpOptionalArgsConstruction;
        private static dynamic pureCSharpOptionalArgsDefaultConstruction;
        private static dynamic pythonSuperInitDefaultAfterOtherConstruction;
        private static dynamic reinitAfterConstruction;
        private static dynamic pythonOptionalArgsSuperInitDefault;
        private static dynamic pythonOptionalArgsSuperInitValue;
        private static dynamic namespacedSuperInitNone;
        private static dynamic namespacedSuperInitDefault;

        private static dynamic containsTest;
        private static dynamic module;
        private static string testModule = @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *
class PythonModule(Algo):
    def TestA(self):
        try:
            self.EmitInsights(Insight.Group(Insight()))
            return True
        except:
            return False

def ContainsTest(key, collection):
    if key in collection.Keys:
        return True
    return False

class WithArgs_PythonSuperInitNotCallingBase(SuperInit):
    def __init__(self, jose):
        return

class WithArgs_PythonSuperInitDefault(SuperInit):
    def __init__(self, jose):
        super().__init__()

class WithArgs_PythonSuperInitInt(SuperInit):
    def __init__(self, jose):
        super().__init__(jose)

class PythonSuperInitNotCallingBase(SuperInit):
    def __init__(self):
        return

class PythonSuperInitDefault(SuperInit):
    def __init__(self):
        super().__init__()

class PythonSuperInitInt(SuperInit):
    def __init__(self):
        super().__init__(1)

class PythonSuperInitNone(SuperInit):
    def jose(self):
        return 1

def PureCSharpConstruction():
    return SuperInit(1)

def PureCSharpDefaultConstruction():
    return SuperInit()

def PureCSharpOptionalArgsConstruction():
    return OptionalArgsInit(5)

def PureCSharpOptionalArgsDefaultConstruction():
    return OptionalArgsInit()

class PythonSuperInitDefaultAfterOtherConstruction(SuperInit):
    def __init__(self):
        self.other = SuperInit()
        super().__init__()

class PythonOptionalArgsSuperInitDefault(OptionalArgsInit):
    def __init__(self):
        super().__init__()

class PythonOptionalArgsSuperInitValue(OptionalArgsInit):
    def __init__(self):
        super().__init__(5)

class NamespacedSuperInitNone(SuperInit):
    __namespace__ = 'Python.EmbeddingTest.QCTests'

class NamespacedSuperInitDefault(SuperInit):
    __namespace__ = 'Python.EmbeddingTest.QCTests'
    def __init__(self):
        super().__init__()

def ReinitAfterConstruction():
    instance = PythonSuperInitNone()
    instance.__init__()
    return instance
";

        [OneTimeSetUp]
        public void Setup()
        {
            PythonEngine.Initialize();
            var pyModule = PyModule.FromString("module", testModule);
            containsTest = pyModule.GetAttr("ContainsTest");
            module = pyModule.GetAttr("PythonModule").Invoke();

            pythonSuperInitInt = pyModule.GetAttr("PythonSuperInitInt");
            pythonSuperInitDefault = pyModule.GetAttr("PythonSuperInitDefault");
            pythonSuperInitNone = pyModule.GetAttr("PythonSuperInitNone");
            pythonSuperInitNotCallingBase = pyModule.GetAttr("PythonSuperInitNotCallingBase");

            withArgs_PythonSuperInitNotCallingBase = pyModule.GetAttr("WithArgs_PythonSuperInitNotCallingBase");
            withArgs_PythonSuperInitDefault = pyModule.GetAttr("WithArgs_PythonSuperInitDefault");
            withArgs_PythonSuperInitInt = pyModule.GetAttr("WithArgs_PythonSuperInitInt");

            pureCSharpConstruction = pyModule.GetAttr("PureCSharpConstruction");
            pureCSharpDefaultConstruction = pyModule.GetAttr("PureCSharpDefaultConstruction");
            pureCSharpOptionalArgsConstruction = pyModule.GetAttr("PureCSharpOptionalArgsConstruction");
            pureCSharpOptionalArgsDefaultConstruction = pyModule.GetAttr("PureCSharpOptionalArgsDefaultConstruction");
            pythonSuperInitDefaultAfterOtherConstruction = pyModule.GetAttr("PythonSuperInitDefaultAfterOtherConstruction");
            reinitAfterConstruction = pyModule.GetAttr("ReinitAfterConstruction");
            pythonOptionalArgsSuperInitDefault = pyModule.GetAttr("PythonOptionalArgsSuperInitDefault");
            pythonOptionalArgsSuperInitValue = pyModule.GetAttr("PythonOptionalArgsSuperInitValue");
            namespacedSuperInitNone = pyModule.GetAttr("NamespacedSuperInitNone");
            namespacedSuperInitDefault = pyModule.GetAttr("NamespacedSuperInitDefault");
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            PythonEngine.Shutdown();
        }

        [Test]
        /// Test case for issue with params
        /// Highlights case where params argument is a CLR object wrapped in Python
        /// https://quantconnect.slack.com/archives/G51920EN4/p1615418516028900
        public void ParamTest()
        {
            using (Py.GIL())
            {
                var output = (bool)module.TestA();
                Assert.IsTrue(output);
            }
        }

        [TestCase("AAPL", false)]
        [TestCase("SPY", true)]
        public void ContainsTest(string key, bool expected)
        {
            var dic = new Dictionary<string, object> { { "SPY", new object() } };
            using (Py.GIL())
            {
                Assert.AreEqual(expected, (bool)containsTest(key, dic));
            }
        }

        [Test]
        public void PureCSharpConstruction()
        {
            using (Py.GIL())
            {
                var instance = pureCSharpConstruction();
                Assert.AreEqual(1, (int)instance.CalledInt);
                Assert.AreEqual(0, (int)instance.CalledDefault);
            }
        }

        [Test]
        public void PureCSharpDefaultConstruction()
        {
            using (Py.GIL())
            {
                var instance = pureCSharpDefaultConstruction();
                Assert.AreEqual(0, (int)instance.CalledInt);
                Assert.AreEqual(1, (int)instance.CalledDefault);
            }
        }

        [Test]
        public void PureCSharpOptionalArgsConstruction()
        {
            using (Py.GIL())
            {
                var instance = pureCSharpOptionalArgsConstruction();
                Assert.AreEqual(1, (int)instance.Calls);
                Assert.AreEqual(5, (int)instance.Value);
            }
        }

        [Test]
        public void PureCSharpOptionalArgsDefaultConstruction()
        {
            using (Py.GIL())
            {
                var instance = pureCSharpOptionalArgsDefaultConstruction();
                Assert.AreEqual(1, (int)instance.Calls);
                Assert.AreEqual(0, (int)instance.Value);
            }
        }

        [Test]
        public void NoArgs_DefaultConstructorAfterOtherConstruction()
        {
            using (Py.GIL())
            {
                var instance = pythonSuperInitDefaultAfterOtherConstruction();
                Assert.AreEqual(0, (int)instance.CalledInt);
                Assert.AreEqual(1, (int)instance.CalledDefault);
                Assert.AreEqual(1, (int)instance.other.CalledDefault);
            }
        }

        [Test]
        public void OptionalArgs_DefaultConstructor()
        {
            using (Py.GIL())
            {
                var instance = pythonOptionalArgsSuperInitDefault();
                Assert.AreEqual(1, (int)instance.Calls);
                Assert.AreEqual(0, (int)instance.Value);
            }
        }

        [Test]
        public void OptionalArgs_ValueConstructor()
        {
            using (Py.GIL())
            {
                var instance = pythonOptionalArgsSuperInitValue();
                // the base constructor runs in tp_new before __init__, then again with the given value
                Assert.AreEqual(2, (int)instance.Calls);
                Assert.AreEqual(5, (int)instance.Value);
            }
        }

        [Test]
        public void Namespaced_NoInit()
        {
            using (Py.GIL())
            {
                var instance = namespacedSuperInitNone();
                Assert.AreEqual(0, (int)instance.CalledInt);
                Assert.AreEqual(1, (int)instance.CalledDefault);
            }
        }

        [Test]
        public void Namespaced_DefaultConstructor()
        {
            using (Py.GIL())
            {
                var instance = namespacedSuperInitDefault();
                Assert.AreEqual(0, (int)instance.CalledInt);
                Assert.AreEqual(1, (int)instance.CalledDefault);
            }
        }

        [Test]
        public void ReinitAfterConstructionRunsConstructorAgain()
        {
            using (Py.GIL())
            {
                var instance = reinitAfterConstruction();
                Assert.AreEqual(0, (int)instance.CalledInt);
                Assert.AreEqual(2, (int)instance.CalledDefault);
            }
        }

        [Test]
        public void WithArgs_NoBaseConstructorCall()
        {
            using (Py.GIL())
            {
                var instance = withArgs_PythonSuperInitNotCallingBase(1);
                Assert.AreEqual(0, (int)instance.CalledInt);
                // we call the constructor always
                Assert.AreEqual(1, (int)instance.CalledDefault);
            }
        }

        [Test]
        public void WithArgs_IntConstructor()
        {
            using (Py.GIL())
            {
                var instance = withArgs_PythonSuperInitInt(1);
                Assert.AreEqual(1, (int)instance.CalledInt);
                Assert.AreEqual(1, (int)instance.CalledDefault);
            }
        }

        [Test]
        public void WithArgs_DefaultConstructor()
        {
            using (Py.GIL())
            {
                var instance = withArgs_PythonSuperInitDefault(1);
                Assert.AreEqual(0, (int)instance.CalledInt);
                Assert.AreEqual(1, (int)instance.CalledDefault);
            }
        }

        [Test]
        public void NoArgs_NoBaseConstructorCall()
        {
            using (Py.GIL())
            {
                var instance = pythonSuperInitNotCallingBase();
                Assert.AreEqual(0, (int)instance.CalledInt);
                // this is true because we call the default constructor always
                Assert.AreEqual(1, (int)instance.CalledDefault);
            }
        }

        [Test]
        public void NoArgs_IntConstructor()
        {
            using (Py.GIL())
            {
                var instance = pythonSuperInitInt();
                Assert.AreEqual(1, (int)instance.CalledInt);
                // this is true because we call the default constructor always
                Assert.AreEqual(1, (int)instance.CalledDefault);
            }
        }

        [Test]
        public void NoArgs_DefaultConstructor()
        {
            using (Py.GIL())
            {
                var instance = pythonSuperInitNone();
                Assert.AreEqual(0, (int)instance.CalledInt);
                Assert.AreEqual(1, (int)instance.CalledDefault);
            }
        }

        [Test]
        public void NoArgs_NoConstructor()
        {
            using (Py.GIL())
            {
                var instance = pythonSuperInitDefault.Invoke();

                Assert.AreEqual(0, (int)instance.CalledInt);
                Assert.AreEqual(1, (int)instance.CalledDefault);
            }
        }
    }

    public class Algo
    {
        /// <param name="insight">The insight to be emitted</s>
        public void EmitInsights(Insight insight)
        {
            EmitInsights(new[] { insight });
        }

        /// <param name="insights">The array of insights to be emitted</param>
        public void EmitInsights(params Insight[] insights)
        {
            foreach (var insight in insights)
            {
                Console.WriteLine(insight.info);
            }
        }

    }

    public class SuperInit
    {
        public int CalledInt { get; private set; }
        public int CalledDefault { get; private set; }
        public SuperInit(int a)
        {
            CalledInt++;
        }
        public SuperInit()
        {
            CalledDefault++;
        }
    }

    public class OptionalArgsInit
    {
        public int Calls { get; private set; }
        public int Value { get; private set; }
        public OptionalArgsInit(int value = 0)
        {
            Calls++;
            Value = value;
        }
    }

    public class Insight
    {
        public string info;
        public Insight()
        {
            info = "pepe";
        }

        /// <param name="insight">The insight to be grouped</param>
        public static IEnumerable<Insight> Group(Insight insight) => Group(new[] { insight });

        /// <param name="insights">The insights to be grouped</param>
        public static IEnumerable<Insight> Group(params Insight[] insights)
        {
            if (insights == null)
            {
                throw new ArgumentNullException(nameof(insights));
            }

            return insights;
        }
    }
}
