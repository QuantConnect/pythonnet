using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// Locates the implicit numeric conversion of a CLR type. Python operators never consult implicit
    /// conversions, so types declaring one get the number protocol through <see cref="ImplicitNumericOps{T}"/>.
    /// </summary>
    internal static class ImplicitNumericOps
    {
        private static readonly Type[] NumericTypes =
        {
            typeof(decimal), typeof(double), typeof(float), typeof(long), typeof(int), typeof(short), typeof(sbyte),
            typeof(ulong), typeof(uint), typeof(ushort), typeof(byte)
        };

        internal static MethodInfo? GetConversion(Type type)
        {
            if (type.IsEnum || type.IsPrimitive || type == typeof(decimal) || type.ContainsGenericParameters)
            {
                return null;
            }

            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "op_Implicit"
                    && NumericTypes.Contains(method.ReturnType)
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == type);
        }

        internal static bool IsSupported(Type type) => GetConversion(type) != null;

        /// <summary>
        /// Gets the operators to graft onto the type. The ones it declares or inherits keep winning, as in C#
        /// </summary>
        internal static MethodInfo[] GetMethods(Type type)
        {
            var existing = new HashSet<string>(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).Select(method => method.Name));
            return typeof(ImplicitNumericOps<>).MakeGenericType(type).GetMethods(OpsHelper.BindingFlags)
                .Where(method => !existing.Contains(method.Name))
                .ToArray();
        }
    }

    /// <summary>
    /// The number protocol of a type with an implicit numeric conversion, behaving like the Python number the
    /// conversion maps to: an int for integral conversions (exact, results are ints unless Python would give a
    /// float) and a float for floating point and decimal ones. Operands that are not numbers return
    /// NotImplemented, so Python can try the other operand and raise its usual TypeError.
    /// </summary>
    [Ops]
    internal static class ImplicitNumericOps<T>
    {
        private static readonly MethodInfo Conversion = ImplicitNumericOps.GetConversion(typeof(T))
            ?? throw new InvalidOperationException($"{typeof(T)} has no implicit numeric conversion");
        private static readonly bool IntegralConversion = Type.GetTypeCode(Conversion.ReturnType) is not (TypeCode.Double or TypeCode.Single or TypeCode.Decimal);
        private static readonly Func<T, double> ToDouble = BuildConversion<double>();
        // double to BigInteger throws on NaN and infinity, so floating point conversions go through ToDouble
        private static readonly Func<T, BigInteger>? ToInteger = IntegralConversion || Conversion.ReturnType == typeof(decimal)
            ? BuildConversion<BigInteger>()
            : null;
        private static readonly bool Comparable = typeof(IComparable<T>).IsAssignableFrom(typeof(T)) || typeof(IComparable).IsAssignableFrom(typeof(T));

#pragma warning disable IDE1006 // Naming Styles - must match Python
        [ForbidPythonThreads]
        public static double __float__(T value) => ToDouble(value);

        [ForbidPythonThreads]
        public static object __int__(T value)
        {
            using (Py.GIL())
            {
                if (ToInteger != null)
                {
                    return ToPython(ToInteger(value));
                }

                var number = ToDouble(value);
                if (double.IsNaN(number))
                {
                    throw RaisePython(Exceptions.ValueError, "cannot convert float NaN to integer");
                }
                if (double.IsInfinity(number))
                {
                    throw RaisePython(Exceptions.OverflowError, "cannot convert float infinity to integer");
                }
                return ToPython(new BigInteger(number));
            }
        }
#pragma warning restore IDE1006

        public static object op_UnaryNegation(T value)
        {
            using (Py.GIL())
            {
                var number = ToNumber(value);
                return number.IsInteger ? ToPython(-number.Integer) : -number.Float;
            }
        }

        public static object op_Addition(T a, PyObject b) => Arithmetic(a, b, reverse: false, BigInteger.Add, (x, y) => x + y);
        public static object op_Addition(PyObject a, T b) => Arithmetic(b, a, reverse: true, BigInteger.Add, (x, y) => x + y);

        public static object op_Subtraction(T a, PyObject b) => Arithmetic(a, b, reverse: false, BigInteger.Subtract, (x, y) => x - y);
        public static object op_Subtraction(PyObject a, T b) => Arithmetic(b, a, reverse: true, BigInteger.Subtract, (x, y) => x - y);

        public static object op_Multiply(T a, PyObject b) => Arithmetic(a, b, reverse: false, BigInteger.Multiply, (x, y) => x * y);
        public static object op_Multiply(PyObject a, T b) => Arithmetic(b, a, reverse: true, BigInteger.Multiply, (x, y) => x * y);

        // true division: a float even between ints, and like Python it raises ZeroDivisionError instead of returning infinity
        public static object op_Division(T a, PyObject b) => Arithmetic(a, b, reverse: false, null, Divide);
        public static object op_Division(PyObject a, T b) => Arithmetic(b, a, reverse: true, null, Divide);

        // Python reflects a reversed comparison onto the other operand, so only the forward overloads are needed
        public static object op_LessThan(T a, PyObject b) => Compare(a, b, Runtime.Py_LT);
        public static object op_GreaterThan(T a, PyObject b) => Compare(a, b, Runtime.Py_GT);
        public static object op_LessThanOrEqual(T a, PyObject b) => Compare(a, b, Runtime.Py_LE);
        public static object op_GreaterThanOrEqual(T a, PyObject b) => Compare(a, b, Runtime.Py_GE);

        private static object Arithmetic(T self, PyObject? other, bool reverse,
            Func<BigInteger, BigInteger, BigInteger>? integerOperation, Func<double, double, double> floatOperation)
        {
            // the binder released the GIL to call us
            using (Py.GIL())
            {
                if (!TryGetOperand(other, out var right))
                {
                    return Runtime.PyNotImplemented;
                }

                var left = ToNumber(self);
                if (reverse)
                {
                    (left, right) = (right, left);
                }

                if (integerOperation != null && left.IsInteger && right.IsInteger)
                {
                    return ToPython(integerOperation(left.Integer, right.Integer));
                }
                return floatOperation(left.AsDouble, right.AsDouble);
            }
        }

        private static double Divide(double dividend, double divisor)
        {
            if (divisor == 0)
            {
                throw RaisePython(Exceptions.ZeroDivisionError, "division by zero");
            }
            return dividend / divisor;
        }

        private static object Compare(T self, PyObject? other, int op)
        {
            int order;
            using (Py.GIL())
            {
                // the type's own ordering wins between its instances, as it did before the number protocol was added
                if (Comparable && TryGetInstance(other, out var same))
                {
                    order = Comparer<T>.Default.Compare(self, same);
                }
                else
                {
                    if (!TryGetOperand(other, out var right))
                    {
                        return Runtime.PyNotImplemented;
                    }

                    var left = ToNumber(self);
                    if (!left.IsInteger || !right.IsInteger)
                    {
                        // IEEE comparisons, false against NaN as in Python
                        double x = left.AsDouble, y = right.AsDouble;
                        return op switch
                        {
                            Runtime.Py_LT => x < y,
                            Runtime.Py_LE => x <= y,
                            Runtime.Py_GT => x > y,
                            _ => x >= y,
                        };
                    }
                    order = left.Integer.CompareTo(right.Integer);
                }
            }

            return op switch
            {
                Runtime.Py_LT => order < 0,
                Runtime.Py_LE => order <= 0,
                Runtime.Py_GT => order > 0,
                _ => order >= 0,
            };
        }

        private static Number ToNumber(T value) => IntegralConversion ? new Number(ToInteger!(value)) : new Number(ToDouble(value));

        private static bool TryGetInstance(PyObject? value, out T instance)
        {
            if (value != null && ManagedType.GetManagedObject(value.Reference) is CLRObject { inst: T clrInstance })
            {
                instance = clrInstance;
                return true;
            }
            instance = default!;
            return false;
        }

        /// <summary>
        /// Reads a Python operand as a number: instances of <typeparamref name="T"/>, ints (bool included) and
        /// floats, and any other object implementing __index__ or __float__ (numpy scalars, other CLR types with
        /// an implicit numeric conversion). Requires the GIL.
        /// </summary>
        private static bool TryGetOperand(PyObject? value, out Number number)
        {
            number = default;
            if (value == null)
            {
                return false;
            }
            if (TryGetInstance(value, out var instance))
            {
                number = ToNumber(instance);
                return true;
            }

            var reference = value.Reference;
            if (Runtime.PyInt_Check(reference))
            {
                number = new Number(ToBigInteger(reference));
                return true;
            }
            if (Runtime.PyObject_TypeCheck(reference, Runtime.PyFloatType))
            {
                number = new Number(Runtime.PyFloat_AsDouble(reference));
                return true;
            }
            if (Runtime.PyObject_HasAttrString(reference, "__index__") != 0)
            {
                using var integer = Runtime.PyNumber_Long(reference);
                if (!integer.IsNull())
                {
                    number = new Number(ToBigInteger(integer.Borrow()));
                    return true;
                }
            }
            else if (Runtime.PyObject_HasAttrString(reference, "__float__") != 0)
            {
                var real = Runtime.PyFloat_AsDouble(reference);
                if (real != -1 || !Exceptions.ErrorOccurred())
                {
                    number = new Number(real);
                    return true;
                }
            }

            // a failed probe must not leak, the operand is just not a number
            Exceptions.Clear();
            return false;
        }

        private static BigInteger ToBigInteger(BorrowedReference integer)
        {
            var value = Runtime.PyLong_AsLongLong(integer);
            if (value.HasValue)
            {
                return value.Value;
            }

            // beyond long, parse its digits
            Exceptions.Clear();
            using var digits = Runtime.PyObject_Str(integer);
            return BigInteger.Parse(Runtime.GetManagedString(digits.Borrow())!, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts an integer result to a Python int, which is unbounded. Requires the GIL beyond long.
        /// </summary>
        private static object ToPython(BigInteger value)
        {
            if (value >= long.MinValue && value <= long.MaxValue)
            {
                return (long)value;
            }
            return new PyInt(value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Raises a Python exception from a call made by the binder, which restores it to the caller. Requires the GIL.
        /// </summary>
        private static Exception RaisePython(PyObject type, string message)
        {
            Exceptions.SetError(type.Reference, message);
            return PythonException.ThrowLastAsClrException();
        }

        private static Func<T, TResult> BuildConversion<TResult>()
        {
            var value = Expression.Parameter(typeof(T), "value");
            var body = Expression.Convert(Expression.Call(Conversion, value), typeof(TResult));
            return Expression.Lambda<Func<T, TResult>>(body, value).Compile();
        }

        private readonly struct Number
        {
            public readonly bool IsInteger;
            public readonly BigInteger Integer;
            public readonly double Float;

            public Number(BigInteger value)
            {
                IsInteger = true;
                Integer = value;
                Float = 0;
            }

            public Number(double value)
            {
                IsInteger = false;
                Integer = default;
                Float = value;
            }

            public double AsDouble => IsInteger ? (double)Integer : Float;
        }
    }
}
