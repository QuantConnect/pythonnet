using System;
using System.Linq;
using System.Linq.Expressions;
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

        internal static MethodInfo[] GetMethods(Type type)
        {
            return typeof(ImplicitNumericOps<>).MakeGenericType(type).GetMethods(OpsHelper.BindingFlags);
        }
    }

    [Ops]
    internal static class ImplicitNumericOps<T>
    {
        private static readonly Func<T, double> ToDouble = BuildConversion();

#pragma warning disable IDE1006 // Naming Styles - must match Python
        [ForbidPythonThreads]
        public static double __float__(T value) => ToDouble(value);

        [ForbidPythonThreads]
        public static long __int__(T value) => (long)Math.Truncate(ToDouble(value));
#pragma warning restore IDE1006

        public static double op_UnaryNegation(T value) => -ToDouble(value);

        public static double op_Addition(T a, T b) => ToDouble(a) + ToDouble(b);
        public static double op_Addition(T a, double b) => ToDouble(a) + b;
        public static double op_Addition(double a, T b) => a + ToDouble(b);

        public static double op_Subtraction(T a, T b) => ToDouble(a) - ToDouble(b);
        public static double op_Subtraction(T a, double b) => ToDouble(a) - b;
        public static double op_Subtraction(double a, T b) => a - ToDouble(b);

        public static double op_Multiply(T a, T b) => ToDouble(a) * ToDouble(b);
        public static double op_Multiply(T a, double b) => ToDouble(a) * b;
        public static double op_Multiply(double a, T b) => a * ToDouble(b);

        public static double op_Division(T a, T b) => ToDouble(a) / ToDouble(b);
        public static double op_Division(T a, double b) => ToDouble(a) / b;
        public static double op_Division(double a, T b) => a / ToDouble(b);

        // Python reflects a reversed comparison onto the other operand, so only the forward overloads are needed
        public static bool op_LessThan(T a, T b) => ToDouble(a) < ToDouble(b);
        public static bool op_LessThan(T a, double b) => ToDouble(a) < b;

        public static bool op_GreaterThan(T a, T b) => ToDouble(a) > ToDouble(b);
        public static bool op_GreaterThan(T a, double b) => ToDouble(a) > b;

        public static bool op_LessThanOrEqual(T a, T b) => ToDouble(a) <= ToDouble(b);
        public static bool op_LessThanOrEqual(T a, double b) => ToDouble(a) <= b;

        public static bool op_GreaterThanOrEqual(T a, T b) => ToDouble(a) >= ToDouble(b);
        public static bool op_GreaterThanOrEqual(T a, double b) => ToDouble(a) >= b;

        private static Func<T, double> BuildConversion()
        {
            var conversion = ImplicitNumericOps.GetConversion(typeof(T))
                ?? throw new InvalidOperationException($"{typeof(T)} has no implicit numeric conversion");
            var value = Expression.Parameter(typeof(T), "value");
            var body = Expression.Convert(Expression.Call(conversion, value), typeof(double));
            return Expression.Lambda<Func<T, double>>(body, value).Compile();
        }
    }
}
