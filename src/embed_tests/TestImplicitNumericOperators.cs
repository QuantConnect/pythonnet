using System;
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

    public class ConvertibleCount
    {
        public long Value { get; }

        public ConvertibleCount(long value)
        {
            Value = value;
        }

        public static implicit operator long(ConvertibleCount count) => count.Value;
    }

    public class ConvertibleBase
    {
        // inherited by ConvertibleDerived, it must keep winning over the synthesized one as in C#
        public static string operator +(ConvertibleBase a, ConvertibleBase b) => "base";
    }

    public class ConvertibleDerived : ConvertibleBase
    {
        public decimal Amount { get; }

        public ConvertibleDerived(decimal amount)
        {
            Amount = amount;
        }

        public static implicit operator decimal(ConvertibleDerived derived) => derived.Amount;
    }

    public class PreciseQuantity : IComparable<PreciseQuantity>
    {
        public decimal Value { get; }

        public PreciseQuantity(decimal value)
        {
            Value = value;
        }

        // equal to 1 once converted to double, only the type's own ordering tells them apart
        public static PreciseQuantity AboveOne() => new(1.0000000000000000000000000001m);

        public int CompareTo(PreciseQuantity other) => Value.CompareTo(other.Value);

        public static implicit operator decimal(PreciseQuantity quantity) => quantity.Value;
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

count = ConvertibleCount(2)
big = ConvertibleCount(2**53 + 1)

def integer_arithmetic():
    results = (count + 1, 1 + count, count + count, count - 5, 10 - count, count * 3, 3 * count, -count, count + True)
    return all(type(result) is int for result in results), results

def integer_with_floats():
    results = (count + 0.5, 0.5 + count, count * 1.5, count / 2, count / count, 3 / count)
    return all(type(result) is float for result in results), results

def large_integers():
    exact = 2**53 + 1
    return (int(big) == exact, big > 2**53, big - 1 == 2**53, big * big == exact * exact,
        -big == -exact, big >= exact, big + 2**70 == exact + 2**70)

def int_conversions():
    errors = []
    for value in (float('nan'), float('inf')):
        try:
            int(ConvertibleRatio(value))
        except Exception as e:
            errors.append(type(e).__name__)
    return errors, int(ConvertibleRatio(-1.75)), int(ConvertibleFee(12345678901234567891)) == 12345678901234567891

def division_by_zero():
    errors = []
    for operation in (lambda: fee / 0, lambda: 1 / ConvertibleFee(0), lambda: count / 0.0, lambda: fee / ConvertibleFee(0)):
        try:
            operation()
        except ZeroDivisionError:
            errors.append(True)
    return len(errors)

def other_numeric_operands():
    margin = ConvertibleRatio(0.5)
    return fee + 2**40, count + 2**70, fee + margin, count * margin, count < 2**64

def unsupported_operands():
    messages = []
    for operation in (lambda: count + 'x', lambda: fee * None, lambda: fee < 'x', lambda: NotConvertible() - fee):
        try:
            operation()
            messages.append(None)
        except TypeError as e:
            messages.append(str(e))
    return messages

def inherited_operator_wins():
    derived = ConvertibleDerived(2)
    return derived + derived, derived - ConvertibleDerived(1), float(derived)

def own_ordering_wins():
    one, above = PreciseQuantity(1), PreciseQuantity.AboveOne()
    return above > one, one < above, above >= one, one <= above, above < one, sorted([above, one])[0] is one
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
        public void IntegralConversionsBehaveLikeInts()
        {
            using var _ = Py.GIL();
            var result = module.integer_arithmetic();
            Assert.IsTrue((bool)result[0], "every result must be an int");
            var expected = new long[] { 3, 3, 4, -3, 8, 6, 6, -2, 3 };
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], (long)result[1][i], $"index {i}");
            }
        }

        [Test]
        public void IntegralConversionsGiveFloatsWherePythonWould()
        {
            using var _ = Py.GIL();
            var result = module.integer_with_floats();
            Assert.IsTrue((bool)result[0], "every result must be a float");
            var expected = new[] { 2.5, 2.5, 3.0, 1.0, 1.0, 1.5 };
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], (double)result[1][i], $"index {i}");
            }
        }

        [Test]
        public void LargeIntegralValuesStayExact()
        {
            using var _ = Py.GIL();
            var result = module.large_integers();
            for (var i = 0; i < 7; i++)
            {
                Assert.IsTrue((bool)result[i], $"index {i}");
            }
        }

        [Test]
        public void IntConversionFollowsPython()
        {
            using var _ = Py.GIL();
            var result = module.int_conversions();
            CollectionAssert.AreEqual(new[] { "ValueError", "OverflowError" }, ((PyObject)result[0]).As<string[]>());
            Assert.AreEqual(-1, (int)result[1]);
            Assert.IsTrue((bool)result[2], "decimal conversions must truncate exactly");
        }

        [Test]
        public void DivisionByZeroRaisesLikePython()
        {
            using var _ = Py.GIL();
            Assert.AreEqual(4, (int)module.division_by_zero());
        }

        [Test]
        public void LargeIntsAndOtherNumbersAreOperands()
        {
            using var _ = Py.GIL();
            var result = module.other_numeric_operands();
            Assert.AreEqual(2.0 + Math.Pow(2, 40), (double)result[0]);
            Assert.AreEqual("1180591620717411303426", result[1].ToString());
            // another type with an implicit numeric conversion is read through its __float__
            Assert.AreEqual(2.5, (double)result[2]);
            Assert.AreEqual(1.0, (double)result[3]);
            Assert.IsTrue((bool)result[4]);
        }

        [Test]
        public void UnsupportedOperandsRaisePythonsTypeError()
        {
            using var _ = Py.GIL();
            var messages = ((PyObject)module.unsupported_operands()).As<string[]>();
            StringAssert.Contains("unsupported operand type(s) for +", messages[0]);
            StringAssert.Contains("unsupported operand type(s) for *", messages[1]);
            StringAssert.Contains("'<' not supported", messages[2]);
            StringAssert.Contains("unsupported operand type(s) for -", messages[3]);
        }

        [Test]
        public void InheritedOperatorsKeepWinning()
        {
            using var _ = Py.GIL();
            var result = module.inherited_operator_wins();
            Assert.AreEqual("base", (string)result[0]);
            // subtraction is declared nowhere in the hierarchy, so the synthesized one applies
            Assert.AreEqual(1.0, (double)result[1]);
            Assert.AreEqual(2.0, (double)result[2]);
        }

        [Test]
        public void TheTypesOwnOrderingWinsBetweenItsInstances()
        {
            using var _ = Py.GIL();
            var result = module.own_ordering_wins();
            var expected = new[] { true, true, true, true, false, true };
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], (bool)result[i], $"index {i}");
            }
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
