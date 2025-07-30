using System;
using System.Collections.Generic;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class EnumTests
    {
        private static VerticalDirection[] VerticalDirectionEnumValues = Enum.GetValues<VerticalDirection>();
        private static HorizontalDirection[] HorizontalDirectionEnumValues = Enum.GetValues<HorizontalDirection>();

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

        public enum VerticalDirection
        {
            Down = -2,
            Flat = 0,
            Up = 2,
        }

        public enum HorizontalDirection
        {
            Left = -2,
            Flat = 0,
            Right = 2,
        }

        [Test]
        public void CSharpEnumsBehaveAsEnumsInPython()
        {
            using var _ = Py.GIL();
            var module = PyModule.FromString("CSharpEnumsBehaveAsEnumsInPython", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def enum_is_right_type(enum_value={nameof(EnumTests)}.{nameof(VerticalDirection)}.{nameof(VerticalDirection.Up)}):
    return isinstance(enum_value, {nameof(EnumTests)}.{nameof(VerticalDirection)})
");

            Assert.IsTrue(module.InvokeMethod("enum_is_right_type").As<bool>());

            // Also test passing the enum value from C# to Python
            using var pyEnumValue = VerticalDirection.Up.ToPython();
            Assert.IsTrue(module.InvokeMethod("enum_is_right_type", pyEnumValue).As<bool>());
        }

        private PyModule GetTestOperatorsModule(string @operator, VerticalDirection operand1, double operand2)
        {
            var operand1Str = $"{nameof(EnumTests)}.{nameof(VerticalDirection)}.{operand1}";
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

        [TestCase("*", VerticalDirection.Down, 2, -4, -4)]
        [TestCase("/", VerticalDirection.Down, 2, -1, -1)]
        [TestCase("+", VerticalDirection.Down, 2, 0, 0)]
        [TestCase("-", VerticalDirection.Down, 2, -4, 4)]
        [TestCase("*", VerticalDirection.Flat, 2, 0, 0)]
        [TestCase("/", VerticalDirection.Flat, 2, 0, 0)]
        [TestCase("+", VerticalDirection.Flat, 2, 2, 2)]
        [TestCase("-", VerticalDirection.Flat, 2, -2, 2)]
        [TestCase("*", VerticalDirection.Up, 2, 4, 4)]
        [TestCase("/", VerticalDirection.Up, 2, 1, 1)]
        [TestCase("+", VerticalDirection.Up, 2, 4, 4)]
        [TestCase("-", VerticalDirection.Up, 2, 0, 0)]
        [TestCase("*", VerticalDirection.Down, -2, 4, 4)]
        [TestCase("/", VerticalDirection.Down, -2, 1, 1)]
        [TestCase("+", VerticalDirection.Down, -2, -4, -4)]
        [TestCase("-", VerticalDirection.Down, -2, 0, 0)]
        [TestCase("*", VerticalDirection.Flat, -2, 0, 0)]
        [TestCase("/", VerticalDirection.Flat, -2, 0, 0)]
        [TestCase("+", VerticalDirection.Flat, -2, -2, -2)]
        [TestCase("-", VerticalDirection.Flat, -2, 2, -2)]
        [TestCase("*", VerticalDirection.Up, -2, -4, -4)]
        [TestCase("/", VerticalDirection.Up, -2, -1, -1)]
        [TestCase("+", VerticalDirection.Up, -2, 0, 0)]
        [TestCase("-", VerticalDirection.Up, -2, 4, -4)]
        public void ArithmeticOperatorsWorkWithoutExplicitCast(string @operator, VerticalDirection operand1, double operand2, double expectedResult, double invertedOperationExpectedResult)
        {
            using var _ = Py.GIL();
            var module = GetTestOperatorsModule(@operator, operand1, operand2);

            Assert.AreEqual(expectedResult, module.InvokeMethod("operation1").As<double>());

            if (Convert.ToInt64(operand1) != 0 || @operator != "/")
            {
                Assert.AreEqual(invertedOperationExpectedResult, module.InvokeMethod("operation2").As<double>());
            }
        }

        [TestCase("==", VerticalDirection.Down, -2, true)]
        [TestCase("==", VerticalDirection.Down, 0, false)]
        [TestCase("==", VerticalDirection.Down, 2, false)]
        [TestCase("==", VerticalDirection.Flat, -2, false)]
        [TestCase("==", VerticalDirection.Flat, 0, true)]
        [TestCase("==", VerticalDirection.Flat, 2, false)]
        [TestCase("==", VerticalDirection.Up, -2, false)]
        [TestCase("==", VerticalDirection.Up, 0, false)]
        [TestCase("==", VerticalDirection.Up, 2, true)]
        [TestCase("!=", VerticalDirection.Down, -2, false)]
        [TestCase("!=", VerticalDirection.Down, 0, true)]
        [TestCase("!=", VerticalDirection.Down, 2, true)]
        [TestCase("!=", VerticalDirection.Flat, -2, true)]
        [TestCase("!=", VerticalDirection.Flat, 0, false)]
        [TestCase("!=", VerticalDirection.Flat, 2, true)]
        [TestCase("!=", VerticalDirection.Up, -2, true)]
        [TestCase("!=", VerticalDirection.Up, 0, true)]
        [TestCase("!=", VerticalDirection.Up, 2, false)]
        [TestCase("<", VerticalDirection.Down, -3, false)]
        [TestCase("<", VerticalDirection.Down, -2, false)]
        [TestCase("<", VerticalDirection.Down, 0, true)]
        [TestCase("<", VerticalDirection.Down, 2, true)]
        [TestCase("<", VerticalDirection.Flat, -2, false)]
        [TestCase("<", VerticalDirection.Flat, 0, false)]
        [TestCase("<", VerticalDirection.Flat, 2, true)]
        [TestCase("<", VerticalDirection.Up, -2, false)]
        [TestCase("<", VerticalDirection.Up, 0, false)]
        [TestCase("<", VerticalDirection.Up, 2, false)]
        [TestCase("<", VerticalDirection.Up, 3, true)]
        [TestCase("<=", VerticalDirection.Down, -3, false)]
        [TestCase("<=", VerticalDirection.Down, -2, true)]
        [TestCase("<=", VerticalDirection.Down, 0, true)]
        [TestCase("<=", VerticalDirection.Down, 2, true)]
        [TestCase("<=", VerticalDirection.Flat, -2, false)]
        [TestCase("<=", VerticalDirection.Flat, 0, true)]
        [TestCase("<=", VerticalDirection.Flat, 2, true)]
        [TestCase("<=", VerticalDirection.Up, -2, false)]
        [TestCase("<=", VerticalDirection.Up, 0, false)]
        [TestCase("<=", VerticalDirection.Up, 2, true)]
        [TestCase("<=", VerticalDirection.Up, 3, true)]
        [TestCase(">", VerticalDirection.Down, -3, true)]
        [TestCase(">", VerticalDirection.Down, -2, false)]
        [TestCase(">", VerticalDirection.Down, 0, false)]
        [TestCase(">", VerticalDirection.Down, 2, false)]
        [TestCase(">", VerticalDirection.Flat, -2, true)]
        [TestCase(">", VerticalDirection.Flat, 0, false)]
        [TestCase(">", VerticalDirection.Flat, 2, false)]
        [TestCase(">", VerticalDirection.Up, -2, true)]
        [TestCase(">", VerticalDirection.Up, 0, true)]
        [TestCase(">", VerticalDirection.Up, 2, false)]
        [TestCase(">", VerticalDirection.Up, 3, false)]
        [TestCase(">=", VerticalDirection.Down, -3, true)]
        [TestCase(">=", VerticalDirection.Down, -2, true)]
        [TestCase(">=", VerticalDirection.Down, 0, false)]
        [TestCase(">=", VerticalDirection.Down, 2, false)]
        [TestCase(">=", VerticalDirection.Flat, -2, true)]
        [TestCase(">=", VerticalDirection.Flat, 0, true)]
        [TestCase(">=", VerticalDirection.Flat, 2, false)]
        [TestCase(">=", VerticalDirection.Up, -2, true)]
        [TestCase(">=", VerticalDirection.Up, 0, true)]
        [TestCase(">=", VerticalDirection.Up, 2, true)]
        [TestCase(">=", VerticalDirection.Up, 3, false)]
        public void IntComparisonOperatorsWorkWithoutExplicitCast(string @operator, VerticalDirection operand1, int operand2, bool expectedResult)
        {
            using var _ = Py.GIL();
            var module = GetTestOperatorsModule(@operator, operand1, operand2);

            Assert.AreEqual(expectedResult, module.InvokeMethod("operation1").As<bool>());

            var invertedOperationExpectedResult = (@operator.StartsWith('<') || @operator.StartsWith('>')) && Convert.ToInt64(operand1) != operand2
                ? !expectedResult
                : expectedResult;
            Assert.AreEqual(invertedOperationExpectedResult, module.InvokeMethod("operation2").As<bool>());
        }

        [TestCase("==", VerticalDirection.Down, -2.0, true)]
        [TestCase("==", VerticalDirection.Down, -2.00001, false)]
        [TestCase("==", VerticalDirection.Down, -1.99999, false)]
        [TestCase("==", VerticalDirection.Down, 0.0, false)]
        [TestCase("==", VerticalDirection.Down, 2.0, false)]
        [TestCase("==", VerticalDirection.Flat, -2.0, false)]
        [TestCase("==", VerticalDirection.Flat, 0.0, true)]
        [TestCase("==", VerticalDirection.Flat, 0.00001, false)]
        [TestCase("==", VerticalDirection.Flat, -0.00001, false)]
        [TestCase("==", VerticalDirection.Flat, 2.0, false)]
        [TestCase("==", VerticalDirection.Up, -2.0, false)]
        [TestCase("==", VerticalDirection.Up, 0.0, false)]
        [TestCase("==", VerticalDirection.Up, 2.0, true)]
        [TestCase("==", VerticalDirection.Up, 2.00001, false)]
        [TestCase("==", VerticalDirection.Up, 1.99999, false)]
        [TestCase("!=", VerticalDirection.Down, -2.0, false)]
        [TestCase("!=", VerticalDirection.Down, -2.00001, true)]
        [TestCase("!=", VerticalDirection.Down, -1.99999, true)]
        [TestCase("!=", VerticalDirection.Down, 0.0, true)]
        [TestCase("!=", VerticalDirection.Down, 2.0, true)]
        [TestCase("!=", VerticalDirection.Flat, -2.0, true)]
        [TestCase("!=", VerticalDirection.Flat, 0.0, false)]
        [TestCase("!=", VerticalDirection.Flat, 0.00001, true)]
        [TestCase("!=", VerticalDirection.Flat, -0.00001, true)]
        [TestCase("!=", VerticalDirection.Flat, 2.0, true)]
        [TestCase("!=", VerticalDirection.Up, -2.0, true)]
        [TestCase("!=", VerticalDirection.Up, 0.0, true)]
        [TestCase("!=", VerticalDirection.Up, 2.0, false)]
        [TestCase("!=", VerticalDirection.Up, 2.00001, true)]
        [TestCase("!=", VerticalDirection.Up, 1.99999, true)]
        [TestCase("<", VerticalDirection.Down, -3.0, false)]
        [TestCase("<", VerticalDirection.Down, -2.00001, false)]
        [TestCase("<", VerticalDirection.Down, -2.0, false)]
        [TestCase("<", VerticalDirection.Down, -1.99999, true)]
        [TestCase("<", VerticalDirection.Down, 0.0, true)]
        [TestCase("<", VerticalDirection.Down, 2.0, true)]
        [TestCase("<", VerticalDirection.Flat, -2.0, false)]
        [TestCase("<", VerticalDirection.Flat, -0.00001, false)]
        [TestCase("<", VerticalDirection.Flat, 0.0, false)]
        [TestCase("<", VerticalDirection.Flat, 0.00001, true)]
        [TestCase("<", VerticalDirection.Flat, 2.0, true)]
        [TestCase("<", VerticalDirection.Up, -2.0, false)]
        [TestCase("<", VerticalDirection.Up, 0.0, false)]
        [TestCase("<", VerticalDirection.Up, 1.99999, false)]
        [TestCase("<", VerticalDirection.Up, 2.0, false)]
        [TestCase("<", VerticalDirection.Up, 2.00001, true)]
        [TestCase("<", VerticalDirection.Up, 3.0, true)]
        [TestCase("<=", VerticalDirection.Down, -3.0, false)]
        [TestCase("<=", VerticalDirection.Down, -2.00001, false)]
        [TestCase("<=", VerticalDirection.Down, -2.0, true)]
        [TestCase("<=", VerticalDirection.Down, -1.99999, true)]
        [TestCase("<=", VerticalDirection.Down, 0.0, true)]
        [TestCase("<=", VerticalDirection.Down, 2.0, true)]
        [TestCase("<=", VerticalDirection.Flat, -2.0, false)]
        [TestCase("<=", VerticalDirection.Flat, -0.00001, false)]
        [TestCase("<=", VerticalDirection.Flat, 0.0, true)]
        [TestCase("<=", VerticalDirection.Flat, 0.00001, true)]
        [TestCase("<=", VerticalDirection.Flat, 2.0, true)]
        [TestCase("<=", VerticalDirection.Up, -2.0, false)]
        [TestCase("<=", VerticalDirection.Up, 0.0, false)]
        [TestCase("<=", VerticalDirection.Up, 1.99999, false)]
        [TestCase("<=", VerticalDirection.Up, 2.0, true)]
        [TestCase("<=", VerticalDirection.Up, 2.00001, true)]
        [TestCase("<=", VerticalDirection.Up, 3.0, true)]
        [TestCase(">", VerticalDirection.Down, -3.0, true)]
        [TestCase(">", VerticalDirection.Down, -2.00001, true)]
        [TestCase(">", VerticalDirection.Down, -2.0, false)]
        [TestCase(">", VerticalDirection.Down, -1.99999, false)]
        [TestCase(">", VerticalDirection.Down, 0.0, false)]
        [TestCase(">", VerticalDirection.Down, 2.0, false)]
        [TestCase(">", VerticalDirection.Flat, -2.0, true)]
        [TestCase(">", VerticalDirection.Flat, -0.00001, true)]
        [TestCase(">", VerticalDirection.Flat, 0.0, false)]
        [TestCase(">", VerticalDirection.Flat, 0.00001, false)]
        [TestCase(">", VerticalDirection.Flat, 2.0, false)]
        [TestCase(">", VerticalDirection.Up, -2.0, true)]
        [TestCase(">", VerticalDirection.Up, 0.0, true)]
        [TestCase(">", VerticalDirection.Up, 1.99999, true)]
        [TestCase(">", VerticalDirection.Up, 2.0, false)]
        [TestCase(">", VerticalDirection.Up, 2.00001, false)]
        [TestCase(">", VerticalDirection.Up, 3.0, false)]
        [TestCase(">=", VerticalDirection.Down, -3.0, true)]
        [TestCase(">=", VerticalDirection.Down, -2.00001, true)]
        [TestCase(">=", VerticalDirection.Down, -2.0, true)]
        [TestCase(">=", VerticalDirection.Down, -1.99999, false)]
        [TestCase(">=", VerticalDirection.Down, 0.0, false)]
        [TestCase(">=", VerticalDirection.Down, 2.0, false)]
        [TestCase(">=", VerticalDirection.Flat, -2.0, true)]
        [TestCase(">=", VerticalDirection.Flat, -0.00001, true)]
        [TestCase(">=", VerticalDirection.Flat, 0.0, true)]
        [TestCase(">=", VerticalDirection.Flat, 0.00001, false)]
        [TestCase(">=", VerticalDirection.Flat, 2.0, false)]
        [TestCase(">=", VerticalDirection.Up, -2.0, true)]
        [TestCase(">=", VerticalDirection.Up, 0.0, true)]
        [TestCase(">=", VerticalDirection.Up, 1.99999, true)]
        [TestCase(">=", VerticalDirection.Up, 2.0, true)]
        [TestCase(">=", VerticalDirection.Up, 2.00001, false)]
        [TestCase(">=", VerticalDirection.Up, 3.0, false)]
        public void FloatComparisonOperatorsWorkWithoutExplicitCast(string @operator, VerticalDirection operand1, double operand2, bool expectedResult)
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

                foreach (var enumValue in VerticalDirectionEnumValues)
                {
                    foreach (var enumValue2 in VerticalDirectionEnumValues)
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
        public void SameEnumTypeComparisonOperatorsWorkWithoutExplicitCast(string @operator, VerticalDirection operand1, VerticalDirection operand2, bool expectedResult)
        {
            using var _ = Py.GIL();
            var module = PyModule.FromString("SameEnumTypeComparisonOperatorsWorkWithoutExplicitCast", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def operation():
    return {nameof(EnumTests)}.{nameof(VerticalDirection)}.{operand1} {@operator} {nameof(EnumTests)}.{nameof(VerticalDirection)}.{operand2}
");

            Assert.AreEqual(expectedResult, module.InvokeMethod("operation").As<bool>());
        }

        [TestCase("==", VerticalDirection.Down, "Down", true)]
        [TestCase("==", VerticalDirection.Down, "Flat", false)]
        [TestCase("==", VerticalDirection.Down, "Up", false)]
        [TestCase("==", VerticalDirection.Flat, "Down", false)]
        [TestCase("==", VerticalDirection.Flat, "Flat", true)]
        [TestCase("==", VerticalDirection.Flat, "Up", false)]
        [TestCase("==", VerticalDirection.Up, "Down", false)]
        [TestCase("==", VerticalDirection.Up, "Flat", false)]
        [TestCase("==", VerticalDirection.Up, "Up", true)]
        [TestCase("!=", VerticalDirection.Down, "Down", false)]
        [TestCase("!=", VerticalDirection.Down, "Flat", true)]
        [TestCase("!=", VerticalDirection.Down, "Up", true)]
        [TestCase("!=", VerticalDirection.Flat, "Down", true)]
        [TestCase("!=", VerticalDirection.Flat, "Flat", false)]
        [TestCase("!=", VerticalDirection.Flat, "Up", true)]
        [TestCase("!=", VerticalDirection.Up, "Down", true)]
        [TestCase("!=", VerticalDirection.Up, "Flat", true)]
        [TestCase("!=", VerticalDirection.Up, "Up", false)]
        public void EnumComparisonOperatorsWorkWithString(string @operator, VerticalDirection operand1, string operand2, bool expectedResult)
        {
            using var _ = Py.GIL();
            var module = PyModule.FromString("EnumComparisonOperatorsWorkWithString", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def operation1():
    return {nameof(EnumTests)}.{nameof(VerticalDirection)}.{operand1} {@operator} ""{operand2}""

def operation2():
    return ""{operand2}"" {@operator} {nameof(EnumTests)}.{nameof(VerticalDirection)}.{operand1}
");

            Assert.AreEqual(expectedResult, module.InvokeMethod("operation1").As<bool>());
            Assert.AreEqual(expectedResult, module.InvokeMethod("operation2").As<bool>());
        }

        public static IEnumerable<TestCaseData> OtherEnumsComparisonOperatorsTestCases
        {
            get
            {
                var operators = new[] { "==", "!=", "<", "<=", ">", ">=" };

                foreach (var enumValue in VerticalDirectionEnumValues)
                {
                    foreach (var enum2Value in HorizontalDirectionEnumValues)
                    {
                        var intEnumValue = Convert.ToInt64(enumValue);
                        var intEnum2Value = Convert.ToInt64(enum2Value);

                        yield return new TestCaseData("==", enumValue, enum2Value, intEnumValue == intEnum2Value, intEnum2Value == intEnumValue);
                        yield return new TestCaseData("!=", enumValue, enum2Value, intEnumValue != intEnum2Value, intEnum2Value != intEnumValue);
                        yield return new TestCaseData("<", enumValue, enum2Value, intEnumValue < intEnum2Value, intEnum2Value < intEnumValue);
                        yield return new TestCaseData("<=", enumValue, enum2Value, intEnumValue <= intEnum2Value, intEnum2Value <= intEnumValue);
                        yield return new TestCaseData(">", enumValue, enum2Value, intEnumValue > intEnum2Value, intEnum2Value > intEnumValue);
                        yield return new TestCaseData(">=", enumValue, enum2Value, intEnumValue >= intEnum2Value, intEnum2Value >= intEnumValue);
                    }
                }
            }
        }

        [TestCaseSource(nameof(OtherEnumsComparisonOperatorsTestCases))]
        public void OtherEnumsComparisonOperatorsWorkWithoutExplicitCast(string @operator, VerticalDirection operand1, HorizontalDirection operand2, bool expectedResult, bool invertedOperationExpectedResult)
        {
            using var _ = Py.GIL();
            var module = PyModule.FromString("OtherEnumsComparisonOperatorsWorkWithoutExplicitCast", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def operation1():
    return {nameof(EnumTests)}.{nameof(VerticalDirection)}.{operand1} {@operator} {nameof(EnumTests)}.{nameof(HorizontalDirection)}.{operand2}

def operation2():
    return {nameof(EnumTests)}.{nameof(HorizontalDirection)}.{operand2} {@operator} {nameof(EnumTests)}.{nameof(VerticalDirection)}.{operand1}
");

            Assert.AreEqual(expectedResult, module.InvokeMethod("operation1").As<bool>());
            Assert.AreEqual(invertedOperationExpectedResult, module.InvokeMethod("operation2").As<bool>());
        }

        private static IEnumerable<TestCaseData> IdentityComparisonTestCases
        {
            get
            {
                foreach (var enumValue1 in VerticalDirectionEnumValues)
                {
                    foreach (var enumValue2 in VerticalDirectionEnumValues)
                    {
                        if (enumValue2 != enumValue1)
                        {
                            yield return new TestCaseData(enumValue1, enumValue2);
                        }
                    }
                }
            }
        }

        [TestCaseSource(nameof(IdentityComparisonTestCases))]
        public void CSharpEnumsAreSingletonsInPthonAndIdentityComparisonWorks(VerticalDirection enumValue1, VerticalDirection enumValue2)
        {
            var enumValue1Str = $"{nameof(EnumTests)}.{nameof(VerticalDirection)}.{enumValue1}";
            var enumValue2Str = $"{nameof(EnumTests)}.{nameof(VerticalDirection)}.{enumValue2}";

            using var _ = Py.GIL();
            var module = PyModule.FromString("CSharpEnumsAreSingletonsInPthonAndIdentityComparisonWorks", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def are_same1():
    return {enumValue1Str} is {enumValue1Str}

def are_same2():
    enum_value = {enumValue1Str}
    return enum_value is {enumValue1Str}

def are_same3():
    enum_value = {enumValue1Str}
    return {enumValue1Str} is enum_value

def are_same4():
    enum_value1 = {enumValue1Str}
    enum_value2 = {enumValue1Str}
    return enum_value1 is enum_value2

def are_not_same1():
    return {enumValue1Str} is not {enumValue2Str}

def are_not_same2():
    enum_value = {enumValue1Str}
    return enum_value is not {enumValue2Str}

def are_not_same3():
    enum_value = {enumValue2Str}
    return {enumValue1Str} is not enum_value

def are_not_same4():
    enum_value1 = {enumValue1Str}
    enum_value2 = {enumValue2Str}
    return enum_value1 is not enum_value2


");

            Assert.IsTrue(module.InvokeMethod("are_same1").As<bool>());
            Assert.IsTrue(module.InvokeMethod("are_same2").As<bool>());
            Assert.IsTrue(module.InvokeMethod("are_same3").As<bool>());
            Assert.IsTrue(module.InvokeMethod("are_same4").As<bool>());

            Assert.IsTrue(module.InvokeMethod("are_not_same1").As<bool>());
            Assert.IsTrue(module.InvokeMethod("are_not_same2").As<bool>());
            Assert.IsTrue(module.InvokeMethod("are_not_same3").As<bool>());
            Assert.IsTrue(module.InvokeMethod("are_not_same4").As<bool>());
        }

        [Test]
        public void IdentityComparisonBetweenDifferentEnumTypesIsNeverTrue(
            [ValueSource(nameof(VerticalDirectionEnumValues))] VerticalDirection enumValue1,
            [ValueSource(nameof(HorizontalDirectionEnumValues))] HorizontalDirection enumValue2)
        {
            var enumValue1Str = $"{nameof(EnumTests)}.{nameof(VerticalDirection)}.{enumValue1}";
            var enumValue2Str = $"{nameof(EnumTests)}.{nameof(HorizontalDirection)}.{enumValue2}";

            using var _ = Py.GIL();
            var module = PyModule.FromString("IdentityComparisonBetweenDifferentEnumTypesIsNeverTrue", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

enum_value1 = {enumValue1Str}
enum_value2 = {enumValue2Str}

def are_same1():
    return {enumValue1Str} is {enumValue2Str}

def are_same2():
    return enum_value1 is {enumValue2Str}

def are_same3():
    return {enumValue2Str} is enum_value1

def are_same4():
    return enum_value2 is {enumValue1Str}

def are_same5():
    return {enumValue1Str} is enum_value2

def are_same6():
    return enum_value1 is enum_value2

def are_same7():
    return enum_value2 is enum_value1
");

            Assert.IsFalse(module.InvokeMethod("are_same1").As<bool>());
            Assert.IsFalse(module.InvokeMethod("are_same2").As<bool>());
            Assert.IsFalse(module.InvokeMethod("are_same3").As<bool>());
            Assert.IsFalse(module.InvokeMethod("are_same4").As<bool>());
            Assert.IsFalse(module.InvokeMethod("are_same5").As<bool>());
            Assert.IsFalse(module.InvokeMethod("are_same6").As<bool>());
            Assert.IsFalse(module.InvokeMethod("are_same7").As<bool>());
        }
    }
}
