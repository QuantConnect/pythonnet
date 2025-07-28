using System;
using System.Collections.Generic;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class EnumTests
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

        public enum Direction
        {
            Down = -2,
            Flat = 0,
            Up = 2,
        }

        [Test]
        public void CSharpEnumsBehaveAsEnumsInPython()
        {
            using var _ = Py.GIL();
            var module = PyModule.FromString("CSharpEnumsBehaveAsEnumsInPython", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def enum_is_right_type(enum_value=EnumTests.Direction.Up):
    return isinstance(enum_value, EnumTests.Direction)
");

            Assert.IsTrue(module.InvokeMethod("enum_is_right_type").As<bool>());

            // Also test passing the enum value from C# to Python
            using var pyEnumValue = Direction.Up.ToPython();
            Assert.IsTrue(module.InvokeMethod("enum_is_right_type", pyEnumValue).As<bool>());
        }

        private PyModule GetTestOperatorsModule(string @operator, Direction operand1, double operand2)
        {
            var operand1Str = $"{nameof(EnumTests)}.{nameof(Direction)}.{operand1}";
            return PyModule.FromString("GetTestOperatorsModule", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def operation1():
    return {operand1Str} {@operator} {operand2}

def operation2():
    return {operand2} {@operator} {operand1Str}
");
        }

        [TestCase(" *", Direction.Down, 2, -4)]
        [TestCase("/", Direction.Down, 2, -1)]
        [TestCase("+", Direction.Down, 2, 0)]
        [TestCase("-", Direction.Down, 2, -4)]
        [TestCase("*", Direction.Flat, 2, 0)]
        [TestCase("/", Direction.Flat, 2, 0)]
        [TestCase("+", Direction.Flat, 2, 2)]
        [TestCase("-", Direction.Flat, 2, -2)]
        [TestCase("*", Direction.Up, 2, 4)]
        [TestCase("/", Direction.Up, 2, 1)]
        [TestCase("+", Direction.Up, 2, 4)]
        [TestCase("-", Direction.Up, 2, 0)]
        public void ArithmeticOperatorsWorkWithoutExplicitCast(string @operator, Direction operand1, double operand2, double expectedResult)
        {
            using var _ = Py.GIL();
            var module = GetTestOperatorsModule(@operator, operand1, operand2);

            Assert.AreEqual(expectedResult, module.InvokeMethod("operation1").As<double>());
            Assert.AreEqual(expectedResult, module.InvokeMethod("operation2").As<double>());
        }

        [TestCase("==", Direction.Down, -2, true)]
        [TestCase("==", Direction.Down, 0, false)]
        [TestCase("==", Direction.Down, 2, false)]
        [TestCase("==", Direction.Flat, -2, false)]
        [TestCase("==", Direction.Flat, 0, true)]
        [TestCase("==", Direction.Flat, 2, false)]
        [TestCase("==", Direction.Up, -2, false)]
        [TestCase("==", Direction.Up, 0, false)]
        [TestCase("==", Direction.Up, 2, true)]
        [TestCase("!=", Direction.Down, -2, false)]
        [TestCase("!=", Direction.Down, 0, true)]
        [TestCase("!=", Direction.Down, 2, true)]
        [TestCase("!=", Direction.Flat, -2, true)]
        [TestCase("!=", Direction.Flat, 0, false)]
        [TestCase("!=", Direction.Flat, 2, true)]
        [TestCase("!=", Direction.Up, -2, true)]
        [TestCase("!=", Direction.Up, 0, true)]
        [TestCase("!=", Direction.Up, 2, false)]
        [TestCase("<", Direction.Down, -3, false)]
        [TestCase("<", Direction.Down, -2, false)]
        [TestCase("<", Direction.Down, 0, true)]
        [TestCase("<", Direction.Down, 2, true)]
        [TestCase("<", Direction.Flat, -2, false)]
        [TestCase("<", Direction.Flat, 0, false)]
        [TestCase("<", Direction.Flat, 2, true)]
        [TestCase("<", Direction.Up, -2, false)]
        [TestCase("<", Direction.Up, 0, false)]
        [TestCase("<", Direction.Up, 2, false)]
        [TestCase("<", Direction.Up, 3, true)]
        [TestCase("<=", Direction.Down, -3, false)]
        [TestCase("<=", Direction.Down, -2, true)]
        [TestCase("<=", Direction.Down, 0, true)]
        [TestCase("<=", Direction.Down, 2, true)]
        [TestCase("<=", Direction.Flat, -2, false)]
        [TestCase("<=", Direction.Flat, 0, true)]
        [TestCase("<=", Direction.Flat, 2, true)]
        [TestCase("<=", Direction.Up, -2, false)]
        [TestCase("<=", Direction.Up, 0, false)]
        [TestCase("<=", Direction.Up, 2, true)]
        [TestCase("<=", Direction.Up, 3, true)]
        [TestCase(">", Direction.Down, -3, true)]
        [TestCase(">", Direction.Down, -2, false)]
        [TestCase(">", Direction.Down, 0, false)]
        [TestCase(">", Direction.Down, 2, false)]
        [TestCase(">", Direction.Flat, -2, true)]
        [TestCase(">", Direction.Flat, 0, false)]
        [TestCase(">", Direction.Flat, 2, false)]
        [TestCase(">", Direction.Up, -2, true)]
        [TestCase(">", Direction.Up, 0, true)]
        [TestCase(">", Direction.Up, 2, false)]
        [TestCase(">", Direction.Up, 3, false)]
        [TestCase(">=", Direction.Down, -3, true)]
        [TestCase(">=", Direction.Down, -2, true)]
        [TestCase(">=", Direction.Down, 0, false)]
        [TestCase(">=", Direction.Down, 2, false)]
        [TestCase(">=", Direction.Flat, -2, true)]
        [TestCase(">=", Direction.Flat, 0, true)]
        [TestCase(">=", Direction.Flat, 2, false)]
        [TestCase(">=", Direction.Up, -2, true)]
        [TestCase(">=", Direction.Up, 0, true)]
        [TestCase(">=", Direction.Up, 2, true)]
        [TestCase(">=", Direction.Up, 3, false)]
        public void IntComparisonOperatorsWorkWithoutExplicitCast(string @operator, Direction operand1, int operand2, bool expectedResult)
        {
            using var _ = Py.GIL();
            var module = GetTestOperatorsModule(@operator, operand1, operand2);

            Assert.AreEqual(expectedResult, module.InvokeMethod("operation1").As<bool>());

            var invertedOperationExpectedResult = (@operator.StartsWith('<') || @operator.StartsWith('>')) && Convert.ToInt64(operand1) != operand2
                ? !expectedResult
                : expectedResult;
            Assert.AreEqual(invertedOperationExpectedResult, module.InvokeMethod("operation2").As<bool>());
        }

        [TestCase("==", Direction.Down, -2.0, true)]
        [TestCase("==", Direction.Down, -2.00001, false)]
        [TestCase("==", Direction.Down, -1.99999, false)]
        [TestCase("==", Direction.Down, 0.0, false)]
        [TestCase("==", Direction.Down, 2.0, false)]
        [TestCase("==", Direction.Flat, -2.0, false)]
        [TestCase("==", Direction.Flat, 0.0, true)]
        [TestCase("==", Direction.Flat, 0.00001, false)]
        [TestCase("==", Direction.Flat, -0.00001, false)]
        [TestCase("==", Direction.Flat, 2.0, false)]
        [TestCase("==", Direction.Up, -2.0, false)]
        [TestCase("==", Direction.Up, 0.0, false)]
        [TestCase("==", Direction.Up, 2.0, true)]
        [TestCase("==", Direction.Up, 2.00001, false)]
        [TestCase("==", Direction.Up, 1.99999, false)]
        [TestCase("!=", Direction.Down, -2.0, false)]
        [TestCase("!=", Direction.Down, -2.00001, true)]
        [TestCase("!=", Direction.Down, -1.99999, true)]
        [TestCase("!=", Direction.Down, 0.0, true)]
        [TestCase("!=", Direction.Down, 2.0, true)]
        [TestCase("!=", Direction.Flat, -2.0, true)]
        [TestCase("!=", Direction.Flat, 0.0, false)]
        [TestCase("!=", Direction.Flat, 0.00001, true)]
        [TestCase("!=", Direction.Flat, -0.00001, true)]
        [TestCase("!=", Direction.Flat, 2.0, true)]
        [TestCase("!=", Direction.Up, -2.0, true)]
        [TestCase("!=", Direction.Up, 0.0, true)]
        [TestCase("!=", Direction.Up, 2.0, false)]
        [TestCase("!=", Direction.Up, 2.00001, true)]
        [TestCase("!=", Direction.Up, 1.99999, true)]
        [TestCase("<", Direction.Down, -3.0, false)]
        [TestCase("<", Direction.Down, -2.00001, false)]
        [TestCase("<", Direction.Down, -2.0, false)]
        [TestCase("<", Direction.Down, -1.99999, true)]
        [TestCase("<", Direction.Down, 0.0, true)]
        [TestCase("<", Direction.Down, 2.0, true)]
        [TestCase("<", Direction.Flat, -2.0, false)]
        [TestCase("<", Direction.Flat, -0.00001, false)]
        [TestCase("<", Direction.Flat, 0.0, false)]
        [TestCase("<", Direction.Flat, 0.00001, true)]
        [TestCase("<", Direction.Flat, 2.0, true)]
        [TestCase("<", Direction.Up, -2.0, false)]
        [TestCase("<", Direction.Up, 0.0, false)]
        [TestCase("<", Direction.Up, 1.99999, false)]
        [TestCase("<", Direction.Up, 2.0, false)]
        [TestCase("<", Direction.Up, 2.00001, true)]
        [TestCase("<", Direction.Up, 3.0, true)]
        [TestCase("<=", Direction.Down, -3.0, false)]
        [TestCase("<=", Direction.Down, -2.00001, false)]
        [TestCase("<=", Direction.Down, -2.0, true)]
        [TestCase("<=", Direction.Down, -1.99999, true)]
        [TestCase("<=", Direction.Down, 0.0, true)]
        [TestCase("<=", Direction.Down, 2.0, true)]
        [TestCase("<=", Direction.Flat, -2.0, false)]
        [TestCase("<=", Direction.Flat, -0.00001, false)]
        [TestCase("<=", Direction.Flat, 0.0, true)]
        [TestCase("<=", Direction.Flat, 0.00001, true)]
        [TestCase("<=", Direction.Flat, 2.0, true)]
        [TestCase("<=", Direction.Up, -2.0, false)]
        [TestCase("<=", Direction.Up, 0.0, false)]
        [TestCase("<=", Direction.Up, 1.99999, false)]
        [TestCase("<=", Direction.Up, 2.0, true)]
        [TestCase("<=", Direction.Up, 2.00001, true)]
        [TestCase("<=", Direction.Up, 3.0, true)]
        [TestCase(">", Direction.Down, -3.0, true)]
        [TestCase(">", Direction.Down, -2.00001, true)]
        [TestCase(">", Direction.Down, -2.0, false)]
        [TestCase(">", Direction.Down, -1.99999, false)]
        [TestCase(">", Direction.Down, 0.0, false)]
        [TestCase(">", Direction.Down, 2.0, false)]
        [TestCase(">", Direction.Flat, -2.0, true)]
        [TestCase(">", Direction.Flat, -0.00001, true)]
        [TestCase(">", Direction.Flat, 0.0, false)]
        [TestCase(">", Direction.Flat, 0.00001, false)]
        [TestCase(">", Direction.Flat, 2.0, false)]
        [TestCase(">", Direction.Up, -2.0, true)]
        [TestCase(">", Direction.Up, 0.0, true)]
        [TestCase(">", Direction.Up, 1.99999, true)]
        [TestCase(">", Direction.Up, 2.0, false)]
        [TestCase(">", Direction.Up, 2.00001, false)]
        [TestCase(">", Direction.Up, 3.0, false)]
        [TestCase(">=", Direction.Down, -3.0, true)]
        [TestCase(">=", Direction.Down, -2.00001, true)]
        [TestCase(">=", Direction.Down, -2.0, true)]
        [TestCase(">=", Direction.Down, -1.99999, false)]
        [TestCase(">=", Direction.Down, 0.0, false)]
        [TestCase(">=", Direction.Down, 2.0, false)]
        [TestCase(">=", Direction.Flat, -2.0, true)]
        [TestCase(">=", Direction.Flat, -0.00001, true)]
        [TestCase(">=", Direction.Flat, 0.0, true)]
        [TestCase(">=", Direction.Flat, 0.00001, false)]
        [TestCase(">=", Direction.Flat, 2.0, false)]
        [TestCase(">=", Direction.Up, -2.0, true)]
        [TestCase(">=", Direction.Up, 0.0, true)]
        [TestCase(">=", Direction.Up, 1.99999, true)]
        [TestCase(">=", Direction.Up, 2.0, true)]
        [TestCase(">=", Direction.Up, 2.00001, false)]
        [TestCase(">=", Direction.Up, 3.0, false)]
        public void FloatComparisonOperatorsWorkWithoutExplicitCast(string @operator, Direction operand1, double operand2, bool expectedResult)
        {
            using var _ = Py.GIL();
            var module = GetTestOperatorsModule(@operator, operand1, operand2);

            Assert.AreEqual(expectedResult, module.InvokeMethod("operation1").As<bool>());

            var invertedOperationExpectedResult = (@operator.StartsWith('<') || @operator.StartsWith('>')) && Convert.ToInt64(operand1) != operand2
                ? !expectedResult
                : expectedResult;
            Assert.AreEqual(invertedOperationExpectedResult, module.InvokeMethod("operation2").As<bool>());
        }

        public static IEnumerable<TestCaseData> SameEnumTypeComparisonOperatorsTestCases
        {
            get
            {
                var operators = new[] { "==", "!=", "<", "<=", ">", ">=" };
                var enumValues = Enum.GetValues<Direction>();

                foreach (var enumValue in enumValues)
                {
                    foreach (var enumValue2 in enumValues)
                    {
                        yield return new TestCaseData("==", enumValue, enumValue2, enumValue == enumValue2);
                        yield return new TestCaseData("!=", enumValue, enumValue2, enumValue != enumValue2);
                        yield return new TestCaseData("<", enumValue, enumValue2, enumValue < enumValue2);
                        yield return new TestCaseData("<=", enumValue, enumValue2, enumValue <= enumValue2);
                        yield return new TestCaseData(">", enumValue, enumValue2, enumValue > enumValue2);
                        yield return new TestCaseData(">=", enumValue, enumValue2, enumValue >= enumValue2);
                    }
                }
            }
        }

        [TestCaseSource(nameof(SameEnumTypeComparisonOperatorsTestCases))]
        public void SameEnumTypeComparisonOperatorsWorkWithoutExplicitCast(string @operator, Direction operand1, Direction operand2, bool expectedResult)
        {
            using var _ = Py.GIL();
            var module = PyModule.FromString("SameEnumTypeComparisonOperatorsWorkWithoutExplicitCast", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def operation():
    return {nameof(EnumTests)}.{nameof(Direction)}.{operand1} {@operator} {nameof(EnumTests)}.{nameof(Direction)}.{operand2}
");

            Assert.AreEqual(expectedResult, module.InvokeMethod("operation").As<bool>());
        }

        [TestCase("==", Direction.Down, "Down", true)]
        [TestCase("==", Direction.Down, "Flat", false)]
        [TestCase("==", Direction.Down, "Up", false)]
        [TestCase("==", Direction.Flat, "Down", false)]
        [TestCase("==", Direction.Flat, "Flat", true)]
        [TestCase("==", Direction.Flat, "Up", false)]
        [TestCase("==", Direction.Up, "Down", false)]
        [TestCase("==", Direction.Up, "Flat", false)]
        [TestCase("==", Direction.Up, "Up", true)]
        [TestCase("!=", Direction.Down, "Down", false)]
        [TestCase("!=", Direction.Down, "Flat", true)]
        [TestCase("!=", Direction.Down, "Up", true)]
        [TestCase("!=", Direction.Flat, "Down", true)]
        [TestCase("!=", Direction.Flat, "Flat", false)]
        [TestCase("!=", Direction.Flat, "Up", true)]
        [TestCase("!=", Direction.Up, "Down", true)]
        [TestCase("!=", Direction.Up, "Flat", true)]
        [TestCase("!=", Direction.Up, "Up", false)]
        public void EnumComparisonOperatorsWorkWithString(string @operator, Direction operand1, string operand2, bool expectedResult)
        {
            using var _ = Py.GIL();
            var module = PyModule.FromString("EnumComparisonOperatorsWorkWithString", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def operation1():
    return {nameof(EnumTests)}.{nameof(Direction)}.{operand1} {@operator} ""{operand2}""

def operation2():
    return ""{operand2}"" {@operator} {nameof(EnumTests)}.{nameof(Direction)}.{operand1}
");

            Assert.AreEqual(expectedResult, module.InvokeMethod("operation1").As<bool>());
            Assert.AreEqual(expectedResult, module.InvokeMethod("operation2").As<bool>());
        }
    }
}
