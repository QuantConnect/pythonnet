using System;
using System.Linq.Expressions;
using System.Reflection;
using static Python.Runtime.OpsHelper;

namespace Python.Runtime
{
    static class OpsHelper
    {
        public static BindingFlags BindingFlags => BindingFlags.Public | BindingFlags.Static;

        public static Func<T, T, T> Binary<T>(Func<Expression, Expression, Expression> func)
        {
            var a = Expression.Parameter(typeof(T), "a");
            var b = Expression.Parameter(typeof(T), "b");
            var body = func(a, b);
            var lambda = Expression.Lambda<Func<T, T, T>>(body, a, b);
            return lambda.Compile();
        }

        public static Func<T, T> Unary<T>(Func<Expression, Expression> func)
        {
            var value = Expression.Parameter(typeof(T), "value");
            var body = func(value);
            var lambda = Expression.Lambda<Func<T, T>>(body, value);
            return lambda.Compile();
        }

        public static bool IsOpsHelper(this MethodBase method)
            => method.DeclaringType.GetCustomAttribute<OpsAttribute>() is not null;

        public static Expression EnumUnderlyingValue(Expression enumValue)
            => Expression.Convert(enumValue, enumValue.Type.GetEnumUnderlyingType());
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal class OpsAttribute : Attribute { }

    [Ops]
    internal static class FlagEnumOps<T> where T : Enum
    {
        static readonly Func<T, T, T> and = BinaryOp(Expression.And);
        static readonly Func<T, T, T> or = BinaryOp(Expression.Or);
        static readonly Func<T, T, T> xor = BinaryOp(Expression.ExclusiveOr);

        static readonly Func<T, T> invert = UnaryOp(Expression.OnesComplement);

        public static T op_BitwiseAnd(T a, T b) => and(a, b);
        public static T op_BitwiseOr(T a, T b) => or(a, b);
        public static T op_ExclusiveOr(T a, T b) => xor(a, b);
        public static T op_OnesComplement(T value) => invert(value);

        static Expression FromNumber(Expression number)
            => Expression.Convert(number, typeof(T));

        static Func<T, T, T> BinaryOp(Func<Expression, Expression, BinaryExpression> op)
        {
            return Binary<T>((a, b) =>
            {
                var numericA = EnumUnderlyingValue(a);
                var numericB = EnumUnderlyingValue(b);
                var numericResult = op(numericA, numericB);
                return FromNumber(numericResult);
            });
        }
        static Func<T, T> UnaryOp(Func<Expression, UnaryExpression> op)
        {
            return Unary<T>(value =>
            {
                var numeric = EnumUnderlyingValue(value);
                var numericResult = op(numeric);
                return FromNumber(numericResult);
            });
        }
    }

    [Ops]
    internal static class EnumOps<T> where T : Enum
    {
        private static bool IsUnsigned = typeof(T).GetEnumUnderlyingType() == typeof(UInt64);

        [ForbidPythonThreads]
#pragma warning disable IDE1006 // Naming Styles - must match Python
        public static PyInt __int__(T value)
#pragma warning restore IDE1006 // Naming Styles
            => IsUnsigned
            ? new PyInt(Convert.ToUInt64(value))
            : new PyInt(Convert.ToInt64(value));

        #region Arithmetic operators

        public static double op_Addition(T a, double b)
        {
            if (IsUnsigned)
            {
                return Convert.ToUInt64(a) + b;
            }
            return Convert.ToInt64(a) + b;
        }

        public static double op_Addition(double a, T b)
        {
            return op_Addition(b, a);
        }

        public static double op_Subtraction(T a, double b)
        {
            if (IsUnsigned)
            {
                return Convert.ToUInt64(a) - b;
            }
            return Convert.ToInt64(a) - b;
        }

        public static double op_Subtraction(double a, T b)
        {
            if (IsUnsigned)
            {
                return a - Convert.ToUInt64(b);
            }
            return a - Convert.ToInt64(b);
        }

        public static double op_Multiply(T a, double b)
        {
            if (IsUnsigned)
            {
                return Convert.ToUInt64(a) * b;
            }
            return Convert.ToInt64(a) * b;
        }

        public static double op_Multiply(double a, T b)
        {
            return op_Multiply(b, a);
        }

        public static double op_Division(T a, double b)
        {
            if (IsUnsigned)
            {
                return Convert.ToUInt64(a) / b;
            }
            return Convert.ToInt64(a) / b;
        }

        public static double op_Division(double a, T b)
        {
            if (IsUnsigned)
            {
                return a / Convert.ToUInt64(b);
            }
            return a / Convert.ToInt64(b);
        }

        #endregion

        #region Int comparison operators

        public static bool op_Equality(T a, long b)
        {
            if (IsUnsigned)
            {
                var uvalue = Convert.ToUInt64(a);
                return b >= 0 && ((ulong)b) == uvalue;
            }
            return Convert.ToInt64(a) == b;
        }

        public static bool op_Equality(T a, ulong b)
        {
            if (IsUnsigned)
            {
                var uvalue = Convert.ToUInt64(a);
                return b == uvalue;
            }
            var ivalue = Convert.ToInt64(a);
            return ivalue >= 0 && ((ulong)ivalue) == b;
        }

        public static bool op_Equality(long a, T b)
        {
            return op_Equality(b, a);
        }

        public static bool op_Equality(ulong a, T b)
        {
            return op_Equality(b, a);
        }

        public static bool op_Inequality(T a, long b)
        {
            return !op_Equality(a, b);
        }

        public static bool op_Inequality(T a, ulong b)
        {
            return !op_Equality(a, b);
        }

        public static bool op_Inequality(long a, T b)
        {
            return !op_Equality(b, a);
        }

        public static bool op_Inequality(ulong a, T b)
        {
            return !op_Equality(b, a);
        }

        public static bool op_LessThan(T a, long b)
        {
            if (IsUnsigned)
            {
                var uvalue = Convert.ToUInt64(a);
                return b >= 0 && ((ulong)b) > uvalue;
            }
            return Convert.ToInt64(a) < b;
        }

        public static bool op_LessThan(T a, ulong b)
        {
            if (IsUnsigned)
            {
                var uvalue = Convert.ToUInt64(a);
                return b > uvalue;
            }
            var ivalue = Convert.ToInt64(a);
            return ivalue >= 0 && ((ulong)ivalue) < b;
        }

        public static bool op_LessThan(long a, T b)
        {
            return op_GreaterThan(b, a);
        }

        public static bool op_LessThan(ulong a, T b)
        {
            return op_GreaterThan(b, a);
        }

        public static bool op_GreaterThan(T a, long b)
        {
            if (IsUnsigned)
            {
                var uvalue = Convert.ToUInt64(a);
                return b >= 0 && ((ulong)b) < uvalue;
            }
            return Convert.ToInt64(a) > b;
        }

        public static bool op_GreaterThan(T a, ulong b)
        {
            if (IsUnsigned)
            {
                var uvalue = Convert.ToUInt64(a);
                return b < uvalue;
            }
            var ivalue = Convert.ToInt64(a);
            return ivalue >= 0 && ((ulong)ivalue) > b;
        }

        public static bool op_GreaterThan(long a, T b)
        {
            return op_LessThan(b, a);
        }

        public static bool op_GreaterThan(ulong a, T b)
        {
            return op_LessThan(b, a);
        }

        public static bool op_LessThanOrEqual(T a, long b)
        {
            if (IsUnsigned)
            {
                var uvalue = Convert.ToUInt64(a);
                return b >= 0 && ((ulong)b) >= uvalue;
            }
            return Convert.ToInt64(a) <= b;
        }

        public static bool op_LessThanOrEqual(T a, ulong b)
        {
            if (IsUnsigned)
            {
                var uvalue = Convert.ToUInt64(a);
                return b >= uvalue;
            }
            var ivalue = Convert.ToInt64(a);
            return ivalue >= 0 && ((ulong)ivalue) <= b;
        }

        public static bool op_LessThanOrEqual(long a, T b)
        {
            return op_GreaterThanOrEqual(b, a);
        }

        public static bool op_LessThanOrEqual(ulong a, T b)
        {
            return op_GreaterThanOrEqual(b, a);
        }

        public static bool op_GreaterThanOrEqual(T a, long b)
        {
            if (IsUnsigned)
            {
                var uvalue = Convert.ToUInt64(a);
                return b >= 0 && ((ulong)b) <= uvalue;
            }
            return Convert.ToInt64(a) >= b;
        }

        public static bool op_GreaterThanOrEqual(T a, ulong b)
        {
            if (IsUnsigned)
            {
                var uvalue = Convert.ToUInt64(a);
                return b <= uvalue;
            }
            var ivalue = Convert.ToInt64(a);
            return ivalue >= 0 && ((ulong)ivalue) >= b;
        }

        public static bool op_GreaterThanOrEqual(long a, T b)
        {
            return op_LessThanOrEqual(b, a);
        }

        public static bool op_GreaterThanOrEqual(ulong a, T b)
        {
            return op_LessThanOrEqual(b, a);
        }

        #endregion

        #region Double comparison operators

        public static bool op_Equality(T a, double b)
        {
            if (IsUnsigned)
            {
                return Convert.ToUInt64(a) == b;
            }
            return Convert.ToInt64(a) == b;
        }

        public static bool op_Equality(double a, T b)
        {
            return op_Equality(b, a);
        }

        public static bool op_Inequality(T a, double b)
        {
            return !op_Equality(a, b);
        }

        public static bool op_Inequality(double a, T b)
        {
            return !op_Equality(b, a);
        }

        public static bool op_LessThan(T a, double b)
        {
            if (IsUnsigned)
            {
                return Convert.ToUInt64(a) < b;
            }
            return Convert.ToInt64(a) < b;
        }

        public static bool op_LessThan(double a, T b)
        {
            return op_GreaterThan(b, a);
        }

        public static bool op_GreaterThan(T a, double b)
        {
            if (IsUnsigned)
            {
                return Convert.ToUInt64(a) > b;
            }
            return Convert.ToInt64(a) > b;
        }

        public static bool op_GreaterThan(double a, T b)
        {
            return op_LessThan(b, a);
        }

        public static bool op_LessThanOrEqual(T a, double b)
        {
            if (IsUnsigned)
            {
                return Convert.ToUInt64(a) <= b;
            }
            return Convert.ToInt64(a) <= b;
        }

        public static bool op_LessThanOrEqual(double a, T b)
        {
            return op_GreaterThanOrEqual(b, a);
        }

        public static bool op_GreaterThanOrEqual(T a, double b)
        {
            if (IsUnsigned)
            {
                return Convert.ToUInt64(a) >= b;
            }
            return Convert.ToInt64(a) >= b;
        }

        public static bool op_GreaterThanOrEqual(double a, T b)
        {
            return op_LessThanOrEqual(b, a);
        }

        #endregion

        #region String comparison operators
        public static bool op_Equality(T a, string b)
        {
            return a.ToString().Equals(b, StringComparison.InvariantCultureIgnoreCase);
        }
        public static bool op_Equality(string a, T b)
        {
            return op_Equality(b, a);
        }

        public static bool op_Inequality(T a, string b)
        {
            return !op_Equality(a, b);
        }

        public static bool op_Inequality(string a, T b)
        {
            return !op_Equality(b, a);
        }

        #endregion

        #region Enum comparison operators

        public static bool op_Equality(T a, Enum b)
        {
            if (b.GetType().GetEnumUnderlyingType() == typeof(UInt64))
            {
                return op_Equality(a, Convert.ToUInt64(b));
            }
            return op_Equality(a, Convert.ToInt64(b));
        }

        public static bool op_Equality(Enum a, T b)
        {
            return op_Equality(b, a);
        }

        public static bool op_Inequality(T a, Enum b)
        {
            return !op_Equality(a, b);
        }

        public static bool op_Inequality(Enum a, T b)
        {
            return !op_Equality(b, a);
        }

        public static bool op_LessThan(T a, Enum b)
        {
            if (b.GetType().GetEnumUnderlyingType() == typeof(UInt64))
            {
                return op_LessThan(a, Convert.ToUInt64(b));
            }
            return op_LessThan(a, Convert.ToInt64(b));
        }

        public static bool op_LessThan(Enum a, T b)
        {
            return op_GreaterThan(b, a);
        }

        public static bool op_GreaterThan(T a, Enum b)
        {
            if (b.GetType().GetEnumUnderlyingType() == typeof(UInt64))
            {
                return op_GreaterThan(a, Convert.ToUInt64(b));
            }
            return op_GreaterThan(a, Convert.ToInt64(b));
        }

        public static bool op_GreaterThan(Enum a, T b)
        {
            return op_LessThan(b, a);
        }

        public static bool op_LessThanOrEqual(T a, Enum b)
        {
            if (b.GetType().GetEnumUnderlyingType() == typeof(UInt64))
            {
                return op_LessThanOrEqual(a, Convert.ToUInt64(b));
            }
            return op_LessThanOrEqual(a, Convert.ToInt64(b));
        }

        public static bool op_LessThanOrEqual(Enum a, T b)
        {
            return op_GreaterThanOrEqual(b, a);
        }

        public static bool op_GreaterThanOrEqual(T a, Enum b)
        {
            if (b.GetType().GetEnumUnderlyingType() == typeof(UInt64))
            {
                return op_GreaterThanOrEqual(a, Convert.ToUInt64(b));
            }
            return op_GreaterThanOrEqual(a, Convert.ToInt64(b));
        }

        public static bool op_GreaterThanOrEqual(Enum a, T b)
        {
            return op_LessThanOrEqual(b, a);
        }

        #endregion
    }
}
