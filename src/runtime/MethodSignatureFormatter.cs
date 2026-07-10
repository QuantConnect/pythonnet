using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Python.Runtime
{
    /// <summary>
    /// Formats method and constructor signatures the way Python callers see them
    /// (snake_case names, friendly type names). Used to hint the available overloads
    /// in error messages when a call cannot be matched to any of them.
    /// </summary>
    public static class MethodSignatureFormatter
    {
        /// <summary>
        /// Formats the signatures of the candidate overloads as an error message hint,
        /// so the caller can see what the method expects, e.g.
        /// "The following overloads are available:" followed by one signature per line.
        /// Returns an empty string if there are no signatures to show.
        /// </summary>
        /// <param name="methods">The candidate overloads</param>
        /// <param name="maxShown">The maximum number of signatures to include</param>
        /// <param name="displayName">Optional name to display for the methods, e.g. the type
        /// name for constructors instead of the special <c>.ctor</c> token</param>
        public static string FormatOverloads(IEnumerable<MethodBase> methods, int maxShown = 10, string displayName = null)
        {
            if (methods == null)
            {
                return string.Empty;
            }

            // Building this only runs on error paths; never let it throw and mask
            // the original failure.
            try
            {
                // Distinct signatures, preserving order. Snake-cased duplicates and
                // repeated overloads collapse into a single entry.
                var signatures = new List<string>();
                var seen = new HashSet<string>();
                foreach (var method in methods)
                {
                    if (method == null)
                    {
                        continue;
                    }
                    var signature = FormatSignature(method, displayName);
                    if (seen.Add(signature))
                    {
                        signatures.Add(signature);
                    }
                }

                if (signatures.Count == 0)
                {
                    return string.Empty;
                }

                var to = new StringBuilder(signatures.Count == 1
                    ? "The expected signature is:"
                    : "The following overloads are available:");
                for (var i = 0; i < signatures.Count && i < maxShown; i++)
                {
                    to.Append("\n  ").Append(signatures[i]);
                }
                if (signatures.Count > maxShown)
                {
                    to.Append($"\n  ... and {signatures.Count - maxShown} more");
                }
                return to.ToString();
            }
            catch
            {
                // Best-effort hint only.
                return string.Empty;
            }
        }

        /// <summary>
        /// Formats a method/constructor as a readable signature using the snake_case
        /// name Python callers use, e.g.
        /// <c>range_consolidator(Int32 range, Func[IBaseData, Decimal] selector = None)</c>.
        /// The constructor's special <c>.ctor</c> token is left as-is unless
        /// <paramref name="displayName"/> is provided.
        /// </summary>
        public static string FormatSignature(MethodBase method, string displayName = null)
        {
            var to = new StringBuilder();
            to.Append(displayName ?? SnakeCaseName(method)).Append('(');
            var parameters = method.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    to.Append(", ");
                }
                var parameter = parameters[i];
                if (parameter.IsDefined(typeof(ParamArrayAttribute), false))
                {
                    to.Append("params ");
                }
                to.Append(FormatType(parameter.ParameterType)).Append(' ').Append(parameter.Name.ToSnakeCase());
                if (parameter.IsOptional)
                {
                    to.Append(" = ").Append(FormatDefaultValue(parameter.DefaultValue));
                }
            }
            to.Append(')');
            return to.ToString();
        }

        /// <summary>
        /// The snake_case name a Python caller uses for the given method. Constructors
        /// keep their special <c>.ctor</c> token (a Python caller invokes the type).
        /// </summary>
        internal static string SnakeCaseName(MethodBase method)
        {
            return method.IsConstructor ? method.Name : method.Name.ToSnakeCase();
        }

        /// <summary>
        /// Produces a concise, readable name for a CLR type, unwrapping by-ref and
        /// nullable types and rendering generics as <c>Name[Arg1, Arg2]</c>.
        /// </summary>
        private static string FormatType(Type type)
        {
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                return FormatType(underlying) + "?";
            }

            if (type.IsGenericType)
            {
                var name = type.Name;
                var tick = name.IndexOf('`');
                if (tick >= 0)
                {
                    name = name.Substring(0, tick);
                }
                var args = type.GetGenericArguments().Select(FormatType);
                return $"{name}[{string.Join(", ", args)}]";
            }

            return type.Name;
        }

        private static string FormatDefaultValue(object value)
        {
            if (value == null || value is DBNull)
            {
                return "None";
            }
            if (value is string s)
            {
                return $"\"{s}\"";
            }
            return value.ToString();
        }
    }
}
