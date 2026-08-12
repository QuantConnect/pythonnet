using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Python.Runtime
{
    public static class Util
    {
        internal const string UnstableApiMessage =
            "This API is unstable, and might be changed or removed in the next minor release";
        internal const string MinimalPythonVersionRequired =
            "Only Python 3.6 or newer is supported";
        internal const string InternalUseOnly =
            "This API is for internal use only";

        internal const string UseOverloadWithReferenceTypes =
            "This API is unsafe, and will be removed in the future. Use overloads working with *Reference types";
        internal const string UseNone =
            $"null is not supported in this context. Use {nameof(PyObject)}.{nameof(PyObject.None)}";

        internal const string BadStr = "bad __str__";


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadInt32(BorrowedReference ob, int offset)
        {
            Debug.Assert(offset >= 0);
            return Marshal.ReadInt32(ob.DangerousGetAddress(), offset);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ReadInt64(BorrowedReference ob, int offset)
        {
            Debug.Assert(offset >= 0);
            return Marshal.ReadInt64(ob.DangerousGetAddress(), offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static T* ReadPtr<T>(BorrowedReference ob, int offset)
            where T : unmanaged
        {
            Debug.Assert(offset >= 0);
            IntPtr ptr = Marshal.ReadIntPtr(ob.DangerousGetAddress(), offset);
            return (T*)ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static IntPtr ReadIntPtr(BorrowedReference ob, int offset)
        {
            Debug.Assert(offset >= 0);
            return Marshal.ReadIntPtr(ob.DangerousGetAddress(), offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static BorrowedReference ReadRef(BorrowedReference @ref, int offset)
        {
            Debug.Assert(offset >= 0);
            return new BorrowedReference(ReadIntPtr(@ref, offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt32(BorrowedReference ob, int offset, int value)
        {
            Debug.Assert(offset >= 0);
            Marshal.WriteInt32(ob.DangerousGetAddress(), offset, value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt64(BorrowedReference ob, int offset, long value)
        {
            Debug.Assert(offset >= 0);
            Marshal.WriteInt64(ob.DangerousGetAddress(), offset, value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static void WriteIntPtr(BorrowedReference ob, int offset, IntPtr value)
        {
            Debug.Assert(offset >= 0);
            Marshal.WriteIntPtr(ob.DangerousGetAddress(), offset, value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static void WriteRef(BorrowedReference ob, int offset, in StolenReference @ref)
        {
            Debug.Assert(offset >= 0);
            Marshal.WriteIntPtr(ob.DangerousGetAddress(), offset, @ref.DangerousGetAddress());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static void WriteNullableRef(BorrowedReference ob, int offset, in StolenReference @ref)
        {
            Debug.Assert(offset >= 0);
            Marshal.WriteIntPtr(ob.DangerousGetAddress(), offset, @ref.DangerousGetAddressOrNull());
        }


        internal static Int64 ReadCLong(BorrowedReference tp, int offset)
        {
            // On Windows, a C long is always 32 bits.
            if (Runtime.IsWindows || Runtime.Is32Bit)
            {
                return ReadInt32(tp, offset);
            }
            else
            {
                return ReadInt64(tp, offset);
            }
        }

        internal static void WriteCLong(BorrowedReference type, int offset, Int64 value)
        {
            if (Runtime.IsWindows || Runtime.Is32Bit)
            {
                WriteInt32(type, offset, (Int32)(value & 0xffffffffL));
            }
            else
            {
                WriteInt64(type, offset, value);
            }
        }

        /// <summary>
        /// Gets substring after last occurrence of <paramref name="symbol"/>
        /// </summary>
        internal static string? AfterLast(this string str, char symbol)
        {
            if (str is null)
                throw new ArgumentNullException(nameof(str));

            int last = str.LastIndexOf(symbol);
            return last >= 0 ? str.Substring(last + 1) : null;
        }

        internal static string ReadStringResource(this System.Reflection.Assembly assembly, string resourceName)
        {
            if (assembly is null) throw new ArgumentNullException(nameof(assembly));
            if (string.IsNullOrEmpty(resourceName)) throw new ArgumentNullException(nameof(resourceName));

            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static int HexToInt(char hex) => hex switch
        {
            >= '0' and <= '9' => hex - '0',
            >= 'a' and <= 'f' => hex - 'a' + 10,
            _ => throw new ArgumentOutOfRangeException(nameof(hex)),
        };

        public static IEnumerator<T> GetEnumerator<T>(this IEnumerator<T> enumerator) => enumerator;

        public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
            where T : class
        {
            foreach (var item in source)
            {
                if (item is not null) yield return item;
            }
        }

        /// <summary>
        /// Converts the specified name to snake case.
        /// </summary>
        /// <remarks>
        /// Reference: https://github.com/efcore/EFCore.NamingConventions/blob/main/EFCore.NamingConventions/Internal/SnakeCaseNameRewriter.cs
        /// </remarks>
        public static string ToSnakeCase(this string name, bool constant = false)
        {
            var builder = new StringBuilder(name.Length + Math.Min(2, name.Length / 5));
            var previousCategory = default(UnicodeCategory?);

            for (var currentIndex = 0; currentIndex < name.Length; currentIndex++)
            {
                var currentChar = name[currentIndex];
                if (currentChar == '_')
                {
                    builder.Append('_');
                    previousCategory = null;
                    continue;
                }

                var currentCategory = char.GetUnicodeCategory(currentChar);
                switch (currentCategory)
                {
                    case UnicodeCategory.UppercaseLetter:
                    case UnicodeCategory.TitlecaseLetter:
                        if (previousCategory == UnicodeCategory.SpaceSeparator ||
                            previousCategory == UnicodeCategory.LowercaseLetter ||
                            previousCategory == UnicodeCategory.DecimalDigitNumber &&
                            currentIndex + 1 < name.Length ||
                            previousCategory != UnicodeCategory.DecimalDigitNumber &&
                            previousCategory != null &&
                            currentIndex > 0 &&
                            currentIndex + 1 < name.Length &&
                            char.IsLower(name[currentIndex + 1]))
                        {
                            builder.Append('_');
                        }
                        if (!constant)
                        {
                            currentChar = char.ToLower(currentChar, CultureInfo.InvariantCulture);
                        }
                        break;

                    case UnicodeCategory.LowercaseLetter:
                        if (previousCategory == UnicodeCategory.SpaceSeparator ||
                            // Underscore before this character if previous is a digit and followed by more than one lowercase letter
                            previousCategory == UnicodeCategory.DecimalDigitNumber &&
                            currentIndex + 1 < name.Length &&
                            char.IsLetter(name[currentIndex + 1]))
                        {
                            builder.Append('_');
                        }
                        if (constant)
                        {
                            currentChar = char.ToUpper(currentChar, CultureInfo.InvariantCulture);
                        }
                        break;

                    case UnicodeCategory.DecimalDigitNumber:
                        if (previousCategory != null &&
                            previousCategory != UnicodeCategory.DecimalDigitNumber &&
                            previousCategory != UnicodeCategory.SpaceSeparator)
                        {
                            builder.Append('_');
                        }
                        break;

                    default:
                        if (previousCategory != null)
                        {
                            previousCategory = UnicodeCategory.SpaceSeparator;
                        }
                        continue;
                }

                builder.Append(currentChar);
                previousCategory = currentCategory;
            }

            return builder.ToString();
        }

        /// <summary>
        /// Converts the specified field name to snake case.
        /// const and static readonly fields are considered as constants and are converted to uppercase.
        /// </summary>
        public static string ToSnakeCase(this FieldInfo fieldInfo)
        {
            return fieldInfo.Name.ToSnakeCase(fieldInfo.IsLiteral || fieldInfo.IsStaticReadonly());
        }

        /// <summary>
        /// Converts the specified property name to snake case.
        /// Static properties without a setter are considered as constants and are converted to uppercase.
        /// </summary>
        public static string ToSnakeCase(this PropertyInfo propertyInfo)
        {
            return propertyInfo.Name.ToSnakeCase(propertyInfo.IsStaticReadonly());
        }

        /// <summary>
        /// Determines whether the specified field is static readonly.
        /// </summary>
        public static bool IsStaticReadonly(this FieldInfo fieldInfo)
        {
            return fieldInfo.IsStatic && fieldInfo.IsInitOnly;
        }

        /// <summary>
        /// Determines whether the specified property is static readonly.
        /// </summary>
        public static bool IsStaticReadonly(this PropertyInfo propertyInfo)
        {
            return propertyInfo.CanRead && !propertyInfo.CanWrite &&
                (propertyInfo.GetGetMethod()?.IsStatic ?? propertyInfo.GetGetMethod(nonPublic: true)?.IsStatic ?? false);
        }

        /// <summary>
        /// Determines whether the specified field is static readonly and callable (Action, Func)
        /// </summary>
        public static bool IsStaticReadonlyCallable(this FieldInfo fieldInfo)
        {
            return fieldInfo.IsStaticReadonly() && fieldInfo.FieldType.IsDelegate();
        }

        /// <summary>
        /// Determines whether the specified property is static readonly and callable (Action, Func)
        /// </summary>
        public static bool IsStaticReadonlyCallable(this PropertyInfo propertyInfo)
        {
            return propertyInfo.IsStaticReadonly() && propertyInfo.PropertyType.IsDelegate();
        }

        /// <summary>
        /// Determines whether the specified type is a delegate.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDelegate(this Type type)
        {
            return type.IsSubclassOf(typeof(Delegate));
        }

        /// <summary>
        /// Determines whether the specified type is a CLR integer type (signed or unsigned).
        /// Enums report an integral <see cref="TypeCode"/> too, so callers that want to
        /// exclude them must check <see cref="Type.IsEnum"/> separately.
        /// </summary>
        public static bool IsInteger(this Type type)
        {
            return Type.GetTypeCode(type).IsInteger();
        }

        /// <summary>
        /// Determines whether the specified type code is a CLR integer type (signed or unsigned).
        /// Enums report an integral <see cref="TypeCode"/> too, so callers that want to
        /// exclude them must check <see cref="Type.IsEnum"/> separately.
        /// </summary>
        public static bool IsInteger(this TypeCode typeCode)
        {
            switch (typeCode)
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        // Case-insensitive Levenshtein distance.
        internal static int LevenshteinDistance(string a, string b)
        {
            a = a.ToLowerInvariant();
            b = b.ToLowerInvariant();
            var n = a.Length;
            var m = b.Length;
            if (n == 0) return m;
            if (m == 0) return n;

            var prev = new int[m + 1];
            var curr = new int[m + 1];
            for (var j = 0; j <= m; j++) prev[j] = j;

            for (var i = 1; i <= n; i++)
            {
                curr[0] = i;
                for (var j = 1; j <= m; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                (prev, curr) = (curr, prev);
            }
            return prev[m];
        }

        /// <summary>
        /// Case-insensitive Jaro-Winkler similarity in [0, 1] (Jaro boosted by shared prefix).
        /// </summary>
        internal static double JaroWinklerSimilarity(string a, string b)
        {
            const double PrefixScale = 0.1;
            const int MaxPrefixLength = 4;

            a = a.ToLowerInvariant();
            b = b.ToLowerInvariant();

            var jaro = JaroSimilarity(a, b);

            var prefix = 0;
            var maxPrefix = Math.Min(MaxPrefixLength, Math.Min(a.Length, b.Length));
            while (prefix < maxPrefix && a[prefix] == b[prefix])
            {
                prefix++;
            }

            return jaro + prefix * PrefixScale * (1 - jaro);
        }

        private static double JaroSimilarity(string a, string b)
        {
            if (a == b)
            {
                return 1;
            }

            var n = a.Length;
            var m = b.Length;
            if (n == 0 || m == 0)
            {
                return 0;
            }

            var window = Math.Max(0, Math.Max(n, m) / 2 - 1);
            var aMatched = new bool[n];
            var bMatched = new bool[m];

            var matches = 0;
            for (var i = 0; i < n; i++)
            {
                var lo = Math.Max(0, i - window);
                var hi = Math.Min(m, i + window + 1);
                for (var j = lo; j < hi; j++)
                {
                    if (!bMatched[j] && a[i] == b[j])
                    {
                        aMatched[i] = bMatched[j] = true;
                        matches++;
                        break;
                    }
                }
            }

            if (matches == 0)
            {
                return 0;
            }

            var transpositions = 0;
            var k = 0;
            for (var i = 0; i < n; i++)
            {
                if (!aMatched[i])
                {
                    continue;
                }
                while (!bMatched[k])
                {
                    k++;
                }
                if (a[i] != b[k])
                {
                    transpositions++;
                }
                k++;
            }
            transpositions /= 2;

            return ((double)matches / n + (double)matches / m
                + (double)(matches - transpositions) / matches) / 3;
        }
    }
}
