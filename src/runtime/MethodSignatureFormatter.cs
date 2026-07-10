using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Python.Runtime
{
    /// <summary>
    /// Formats method and constructor signatures the way Python callers see them
    /// (snake_case names, Python type names). Used to hint the available overloads
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
        /// name and the Python types a Python caller uses, e.g.
        /// <c>range_consolidator(int range, Callable[[IBaseData], float] selector = None)</c>.
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
        /// Produces the Python-side name for a CLR type, following the conversions the
        /// runtime performs on arguments: primitives map to their Python equivalents
        /// (str, int, float, bool, datetime, timedelta), Nullable to Optional, delegates
        /// to Callable, list/dictionary shapes to List/Dict and PyObject/object to Any.
        /// CLR types without a Python equivalent keep their name, with generics rendered
        /// as <c>Name[Arg1, Arg2]</c>.
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
                return $"Optional[{FormatType(underlying)}]";
            }

            if (type == typeof(void))
            {
                return "None";
            }
            if (type == typeof(TimeSpan))
            {
                return "timedelta";
            }
            if (type == typeof(object))
            {
                return "Any";
            }
            if (typeof(Type).IsAssignableFrom(type))
            {
                return "type";
            }

            // pythonnet wrapper parameters accept any Python object of the matching shape
            if (type == typeof(PyList))
            {
                return "List[Any]";
            }
            if (type == typeof(PyDict))
            {
                return "Dict[Any, Any]";
            }
            if (typeof(PyObject).IsAssignableFrom(type))
            {
                return "Any";
            }

            if (type.IsArray)
            {
                return $"List[{FormatType(type.GetElementType())}]";
            }

            if (typeof(Delegate).IsAssignableFrom(type) && !type.ContainsGenericParameters)
            {
                var invoke = type.GetMethod("Invoke");
                if (invoke != null)
                {
                    var args = string.Join(", ", invoke.GetParameters().Select(p => FormatType(p.ParameterType)));
                    return $"Callable[[{args}], {FormatType(invoke.ReturnType)}]";
                }
            }

            // Enums have an integer type code but keep their Python-visible name
            if (!type.IsEnum)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Boolean:
                        return "bool";
                    case TypeCode.Char:
                    case TypeCode.String:
                        return "str";
                    case TypeCode.SByte:
                    case TypeCode.Byte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                        return "int";
                    case TypeCode.Single:
                    case TypeCode.Double:
                    case TypeCode.Decimal:
                        return "float";
                    case TypeCode.DateTime:
                        return "datetime";
                }
            }

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                var genericArguments = type.GetGenericArguments();

                // list and dictionary shapes the runtime converts from Python lists/dicts
                if (definition == typeof(List<>) || definition == typeof(IList<>) ||
                    definition == typeof(IEnumerable<>) || definition == typeof(ICollection<>) ||
                    definition == typeof(IReadOnlyList<>) || definition == typeof(IReadOnlyCollection<>))
                {
                    return $"List[{FormatType(genericArguments[0])}]";
                }
                if (definition == typeof(Dictionary<,>) || definition == typeof(IDictionary<,>) ||
                    definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(KeyValuePair<,>))
                {
                    return $"Dict[{FormatType(genericArguments[0])}, {FormatType(genericArguments[1])}]";
                }

                var name = type.Name;
                var tick = name.IndexOf('`');
                if (tick >= 0)
                {
                    name = name.Substring(0, tick);
                }
                var args = genericArguments.Select(FormatType);
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
            if (value is bool b)
            {
                return b ? "True" : "False";
            }
            if (value is Enum e)
            {
                // Render enum defaults the way Python callers access them, e.g. Resolution.DAILY
                return $"{e.GetType().Name}.{e.ToString().ToSnakeCase(constant: true)}";
            }
            return value.ToString();
        }
    }
}
