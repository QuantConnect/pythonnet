using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class ConvertibleFee
    {
        public decimal Amount { get; }

        public ConvertibleFee(decimal amount)
        {
            Amount = amount;
        }

        public static implicit operator decimal(ConvertibleFee fee) => fee.Amount;
    }

    public class ConvertibleRatio
    {
        public double Value { get; }

        public ConvertibleRatio(double value)
        {
            Value = value;
        }

        public static implicit operator double(ConvertibleRatio ratio) => ratio.Value;
    }

    public class ConvertibleMoney
    {
        public decimal Amount { get; }

        public ConvertibleMoney(decimal amount)
        {
            Amount = amount;
        }

        public static implicit operator decimal(ConvertibleMoney money) => money.Amount;

        // the type's own operator must keep winning over the synthesized one
        public static ConvertibleMoney operator +(ConvertibleMoney a, ConvertibleMoney b) => new(a.Amount + b.Amount);
    }

    public class NotConvertible
    {
    }

    public class TestImplicitNumericOperators
    {
        private static dynamic module;

        private const string TestModule = @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *

fee = ConvertibleFee(2)
other = ConvertibleFee(3)

def arithmetic():
    return (fee + other, fee + 1, 1 + fee, fee - other, fee - 1, 10 - fee,
        fee * other, fee * 2, 2 * fee, other / fee, fee / 2, 10 / fee, -fee)

def aggregates():
    return sum([fee, other]), max(fee, other) is other, sorted([other, fee])[0] is fee

def comparisons():
    return (fee < other, fee > other, fee <= other, fee >= other,
        fee > 0, fee < 2, fee <= 2, fee >= 2, 1 < fee, 2 >= fee)

def conversions():
    return float(fee), int(ConvertibleRatio(1.75)), float(ConvertibleRatio(1.75))

def own_operator_wins():
    total = ConvertibleMoney(2) + ConvertibleMoney(3)
    return type(total).__name__, total.Amount, ConvertibleMoney(2) - ConvertibleMoney(1)

def not_convertible():
    return hasattr(NotConvertible(), '__add__'), hasattr(NotConvertible(), '__float__')
";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            PythonEngine.Initialize();
            using var _ = Py.GIL();
            module = PyModule.FromString("implicit_numeric_operators", TestModule);
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        [Test]
        public void ArithmeticDelegatesToTheImplicitConversion()
        {
            using var _ = Py.GIL();
            var result = module.arithmetic();
            var expected = new[] { 5.0, 3.0, 3.0, -1.0, 1.0, 8.0, 6.0, 4.0, 4.0, 1.5, 1.0, 5.0, -2.0 };
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], (double)result[i], $"index {i}");
            }
        }

        [Test]
        public void AggregatesWork()
        {
            using var _ = Py.GIL();
            var result = module.aggregates();
            Assert.AreEqual(5.0, (double)result[0]);
            Assert.IsTrue((bool)result[1]);
            Assert.IsTrue((bool)result[2]);
        }

        [Test]
        public void ComparisonsDelegateToTheImplicitConversion()
        {
            using var _ = Py.GIL();
            var result = module.comparisons();
            var expected = new[] { true, false, true, false, true, false, true, true, true, true };
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], (bool)result[i], $"index {i}");
            }
        }

        [Test]
        public void FloatAndIntConversions()
        {
            using var _ = Py.GIL();
            var result = module.conversions();
            Assert.AreEqual(2.0, (double)result[0]);
            Assert.AreEqual(1, (int)result[1]);
            Assert.AreEqual(1.75, (double)result[2]);
        }

        [Test]
        public void TheTypesOwnOperatorsKeepWinning()
        {
            using var _ = Py.GIL();
            var result = module.own_operator_wins();
            Assert.AreEqual("ConvertibleMoney", (string)result[0]);
            Assert.AreEqual(5m, (decimal)result[1]);
            // subtraction is not declared by the type, so the synthesized one applies
            Assert.AreEqual(1.0, (double)result[2]);
        }

        [Test]
        public void TypesWithoutAConversionAreUntouched()
        {
            using var _ = Py.GIL();
            var result = module.not_convertible();
            Assert.IsFalse((bool)result[0]);
            Assert.IsFalse((bool)result[1]);
        }
    }
}
