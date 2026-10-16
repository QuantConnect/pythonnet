using NUnit.Framework;

using Python.Runtime;
using Python.Runtime.Codecs;

using System;
using System.Linq;
using System.Reflection;

namespace Python.EmbeddingTest
{
    public class TestOperator
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

        public class OperableObject
        {
            public int Num { get; set; }

            public override int GetHashCode()
            {
                return unchecked(159832395 + Num.GetHashCode());
            }

            public override bool Equals(object obj)
            {
                return obj is OperableObject @object &&
                       Num == @object.Num;
            }

            public OperableObject(int num)
            {
                Num = num;
            }

            public static OperableObject operator ~(OperableObject a)
            {
                return new OperableObject(~a.Num);
            }

            public static OperableObject operator +(OperableObject a)
            {
                return new OperableObject(+a.Num);
            }

            public static OperableObject operator -(OperableObject a)
            {
                return new OperableObject(-a.Num);
            }

            public static OperableObject operator +(int a, OperableObject b)
            {
                return new OperableObject(a + b.Num);
            }
            public static OperableObject operator +(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num + b.Num);
            }
            public static OperableObject operator +(OperableObject a, int b)
            {
                return new OperableObject(a.Num + b);
            }

            public static OperableObject operator -(int a, OperableObject b)
            {
                return new OperableObject(a - b.Num);
            }
            public static OperableObject operator -(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num - b.Num);
            }
            public static OperableObject operator -(OperableObject a, int b)
            {
                return new OperableObject(a.Num - b);
            }

            public static OperableObject operator *(int a, OperableObject b)
            {
                return new OperableObject(a * b.Num);
            }
            public static OperableObject operator *(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num * b.Num);
            }
            public static OperableObject operator *(OperableObject a, int b)
            {
                return new OperableObject(a.Num * b);
            }

            public static OperableObject operator /(int a, OperableObject b)
            {
                return new OperableObject(a / b.Num);
            }
            public static OperableObject operator /(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num / b.Num);
            }
            public static OperableObject operator /(OperableObject a, int b)
            {
                return new OperableObject(a.Num / b);
            }

            public static OperableObject operator %(int a, OperableObject b)
            {
                return new OperableObject(a % b.Num);
            }
            public static OperableObject operator %(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num % b.Num);
            }
            public static OperableObject operator %(OperableObject a, int b)
            {
                return new OperableObject(a.Num % b);
            }

            public static OperableObject operator &(int a, OperableObject b)
            {
                return new OperableObject(a & b.Num);
            }
            public static OperableObject operator &(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num & b.Num);
            }
            public static OperableObject operator &(OperableObject a, int b)
            {
                return new OperableObject(a.Num & b);
            }

            public static OperableObject operator |(int a, OperableObject b)
            {
                return new OperableObject(a | b.Num);
            }
            public static OperableObject operator |(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num | b.Num);
            }
            public static OperableObject operator |(OperableObject a, int b)
            {
                return new OperableObject(a.Num | b);
            }

            public static OperableObject operator ^(int a, OperableObject b)
            {
                return new OperableObject(a ^ b.Num);
            }
            public static OperableObject operator ^(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num ^ b.Num);
            }
            public static OperableObject operator ^(OperableObject a, int b)
            {
                return new OperableObject(a.Num ^ b);
            }

            public static bool operator ==(int a, OperableObject b)
            {
                return (a == b.Num);
            }
            public static bool operator ==(OperableObject a, OperableObject b)
            {
                return (a.Num == b.Num);
            }
            public static bool operator ==(OperableObject a, int b)
            {
                return (a.Num == b);
            }

            public static bool operator !=(int a, OperableObject b)
            {
                return (a != b.Num);
            }
            public static bool operator !=(OperableObject a, OperableObject b)
            {
                return (a.Num != b.Num);
            }
            public static bool operator !=(OperableObject a, int b)
            {
                return (a.Num != b);
            }

            public static bool operator <=(int a, OperableObject b)
            {
                return (a <= b.Num);
            }
            public static bool operator <=(OperableObject a, OperableObject b)
            {
                return (a.Num <= b.Num);
            }
            public static bool operator <=(OperableObject a, int b)
            {
                return (a.Num <= b);
            }

            public static bool operator >=(int a, OperableObject b)
            {
                return (a >= b.Num);
            }
            public static bool operator >=(OperableObject a, OperableObject b)
            {
                return (a.Num >= b.Num);
            }
            public static bool operator >=(OperableObject a, int b)
            {
                return (a.Num >= b);
            }

            public static bool operator >=(OperableObject a, (int, int) b)
            {
                using (Py.GIL())
                {
                    int bNum = b.Item1;
                    return a.Num >= bNum;
                }
            }
            public static bool operator <=(OperableObject a, (int, int) b)
            {
                using (Py.GIL())
                {
                    int bNum = b.Item1;
                    return a.Num <= bNum;
                }
            }

            public static bool operator <(int a, OperableObject b)
            {
                return (a < b.Num);
            }
            public static bool operator <(OperableObject a, OperableObject b)
            {
                return (a.Num < b.Num);
            }
            public static bool operator <(OperableObject a, int b)
            {
                return (a.Num < b);
            }

            public static bool operator >(int a, OperableObject b)
            {
                return (a > b.Num);
            }
            public static bool operator >(OperableObject a, OperableObject b)
            {
                return (a.Num > b.Num);
            }
            public static bool operator >(OperableObject a, int b)
            {
                return (a.Num > b);
            }

            public static OperableObject operator <<(OperableObject a, int offset)
            {
                return new OperableObject(a.Num << offset);
            }

            public static OperableObject operator >>(OperableObject a, int offset)
            {
                return new OperableObject(a.Num >> offset);
            }
        }

        [Test]
        public void SymmetricalOperatorOverloads()
        {
            string name = string.Format("{0}.{1}",
                typeof(OperableObject).DeclaringType.Name,
                typeof(OperableObject).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = cls(-2)
b = cls(10)
c = ~a
assert c.Num == ~a.Num

c = +a
assert c.Num == +a.Num

a = cls(2)
c = -a
assert c.Num == -a.Num

c = a + b
assert c.Num == a.Num + b.Num

c = a - b
assert c.Num == a.Num - b.Num

c = a * b
assert c.Num == a.Num * b.Num

c = a / b
assert c.Num == a.Num // b.Num

c = a % b
assert c.Num == a.Num % b.Num

c = a & b
assert c.Num == a.Num & b.Num

c = a | b
assert c.Num == a.Num | b.Num

c = a ^ b
assert c.Num == a.Num ^ b.Num

c = a == b
assert c == (a.Num == b.Num)

c = a != b
assert c == (a.Num != b.Num)

c = a <= b
assert c == (a.Num <= b.Num)

c = a >= b
assert c == (a.Num >= b.Num)

c = a < b
assert c == (a.Num < b.Num)

c = a > b
assert c == (a.Num > b.Num)
");
        }

        [Test]
        public void EnumOperator()
        {
            PythonEngine.Exec($@"
from System.IO import FileAccess
c = FileAccess.Read | FileAccess.Write");
        }

        [Test]
        public void OperatorInequality()
        {
            string name = string.Format("{0}.{1}",
                typeof(OperableObject).DeclaringType.Name,
                typeof(OperableObject).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            PythonEngine.Exec($@"
from {module} import *
cls = {name}
b = cls(10)
a = cls(2)


c = a <= b
assert c == (a.Num <= b.Num)

c = a >= b
assert c == (a.Num >= b.Num)
");

        }

        [Test]
        public void OperatorOverloadMissingArgument()
        {
            string name = string.Format("{0}.{1}",
                typeof(OperableObject).DeclaringType.Name,
                typeof(OperableObject).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            Assert.Throws<PythonException>(() =>
            PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = cls(2)
b = cls(10)
a.op_Addition()
"));
        }

        [Test]
        public void ForwardOperatorOverloads()
        {
            string name = string.Format("{0}.{1}",
                typeof(OperableObject).DeclaringType.Name,
                typeof(OperableObject).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = cls(2)
b = 10
c = a + b
assert c.Num == a.Num + b

c = a - b
assert c.Num == a.Num - b

c = a * b
assert c.Num == a.Num * b

c = a / b
assert c.Num == a.Num // b

c = a % b
assert c.Num == a.Num % b

c = a & b
assert c.Num == a.Num & b

c = a | b
assert c.Num == a.Num | b

c = a ^ b
assert c.Num == a.Num ^ b

c = a == b
assert c == (a.Num == b)

c = a != b
assert c == (a.Num != b)

c = a <= b
assert c == (a.Num <= b)

c = a >= b
assert c == (a.Num >= b)

c = a < b
assert c == (a.Num < b)

c = a > b
assert c == (a.Num > b)
");
        }

        [Test]
        public void TupleComparisonOperatorOverloads()
        {
                TupleCodec<ValueTuple>.Register();
                string name = string.Format("{0}.{1}",
                typeof(OperableObject).DeclaringType.Name,
                typeof(OperableObject).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;
                PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = cls(2)
b = (1, 2)

c = a >= b
assert c == (a.Num >= b[0])

c = a <= b
assert c == (a.Num <= b[0])

c = b >= a
assert c == (b[0] >= a.Num)

c = b <= a
assert c == (b[0] <= a.Num)
");
        }


        [Test]
        public void ReverseOperatorOverloads()
        {
            string name = string.Format("{0}.{1}",
                typeof(OperableObject).DeclaringType.Name,
                typeof(OperableObject).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = 2
b = cls(10)

c = a + b
assert c.Num == a + b.Num

c = a - b
assert c.Num == a - b.Num

c = a * b
assert c.Num == a * b.Num

c = a / b
assert c.Num == a // b.Num

c = a % b
assert c.Num == a % b.Num

c = a & b
assert c.Num == a & b.Num

c = a | b
assert c.Num == a | b.Num

c = a ^ b
assert c.Num == a ^ b.Num

c = a == b
assert c == (a == b.Num)

c = a != b
assert c == (a != b.Num)

c = a <= b
assert c == (a <= b.Num)

c = a >= b
assert c == (a >= b.Num)

c = a < b
assert c == (a < b.Num)

c = a > b
assert c == (a > b.Num)
");

        }
        [Test]
        public void ShiftOperatorOverloads()
        {
            string name = string.Format("{0}.{1}",
                typeof(OperableObject).DeclaringType.Name,
                typeof(OperableObject).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = cls(2)
b = cls(10)

c = a << b.Num
assert c.Num == a.Num << b.Num

c = a >> b.Num
assert c.Num == a.Num >> b.Num
");
        }

        public class ForwardOnlyOperable
        {
            public static ForwardOnlyOperable operator +(ForwardOnlyOperable a, ForwardOnlyOperable b) => a;
        }

        public class ReverseOnlyOperable
        {
            public static ReverseOnlyOperable operator +(double a, ReverseOnlyOperable b) => b;
        }

        [Flags]
        public enum OperableFlags
        {
            A = 1,
            B = 2
        }

        private const string OperationErrors = @"
def error(operation):
    try:
        operation()
    except Exception as e:
        return type(e).__name__
";

        [Test]
        public void UnsupportedReflectedOperationRaisesTypeError()
        {
            // the reflected operation has no CLR overload, it used to recurse until RecursionError
            using var _ = Py.GIL();
            using var scope = Py.CreateScope();
            scope.Exec(OperationErrors + @"
from Python.EmbeddingTest import TestOperator
x = TestOperator.ForwardOnlyOperable()
supported = error(lambda: x + x), error(lambda: x.__add__(x))
unsupported = error(lambda: 1.0 + x), error(lambda: 1 + x), error(lambda: sum([x])), error(lambda: x + 1.0)
has_reflected = hasattr(x, '__radd__')
");
            Assert.AreEqual(new string[] { null, null }, scope.Get<string[]>("supported"));
            CollectionAssert.AreEqual(new[] { "TypeError", "TypeError", "TypeError", "TypeError" }, scope.Get<string[]>("unsupported"));
            Assert.IsFalse(scope.Get<bool>("has_reflected"));
        }

        [Test]
        public void UnsupportedForwardOperationRaisesTypeError()
        {
            // the forward operation has no CLR overload, it used to recurse until RecursionError
            using var _ = Py.GIL();
            using var scope = Py.CreateScope();
            scope.Exec(OperationErrors + @"
from Python.EmbeddingTest import TestOperator
y = TestOperator.ReverseOnlyOperable()
supported = error(lambda: 1.0 + y)
unsupported = error(lambda: y + 1.0), error(lambda: y + y)
has_forward = hasattr(y, '__add__')
");
            Assert.IsNull(scope.Get<string>("supported"));
            CollectionAssert.AreEqual(new[] { "TypeError", "TypeError" }, scope.Get<string[]>("unsupported"));
            Assert.IsFalse(scope.Get<bool>("has_forward"));
        }

        [Test]
        public void UnsupportedReflectedFlagsOperationRaisesTypeError()
        {
            // the [Flags] bitwise operators are forward only
            using var _ = Py.GIL();
            using var scope = Py.CreateScope();
            scope.Exec(OperationErrors + @"
from Python.EmbeddingTest import TestOperator
flags = TestOperator.OperableFlags.A | TestOperator.OperableFlags.B
supported = error(lambda: flags & TestOperator.OperableFlags.A)
unsupported = error(lambda: 1 & flags), error(lambda: 1 | flags)
");
            Assert.IsNull(scope.Get<string>("supported"));
            CollectionAssert.AreEqual(new[] { "TypeError", "TypeError" }, scope.Get<string[]>("unsupported"));
        }

        [Test]
        public void PythonSubclassCanStillDefineTheReflectedOperation()
        {
            using var _ = Py.GIL();
            using var scope = Py.CreateScope();
            scope.Exec(@"
from Python.EmbeddingTest import TestOperator
class Custom(TestOperator.ForwardOnlyOperable):
    def __radd__(self, other):
        return 'custom'
class Plain(TestOperator.ForwardOnlyOperable):
    pass
custom = 1.0 + Custom()
try:
    1.0 + Plain()
    plain = None
except TypeError:
    plain = 'TypeError'
");
            Assert.AreEqual("custom", scope.Get<string>("custom"));
            Assert.AreEqual("TypeError", scope.Get<string>("plain"));
        }
    }
}
