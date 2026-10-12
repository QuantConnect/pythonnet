using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Python.Runtime
{
    /// <summary>
    /// Index of the C# extension methods found in the scanned assemblies, keyed by the type they extend.
    /// Classes bind them as members so they are callable from an instance, like C# callers do.
    /// </summary>
    internal static class ExtensionMethods
    {
        private static readonly ConcurrentDictionary<Type, List<MethodInfo>> _methodsByExtendedType = new();

        // The framework ships thousands of extension methods (LINQ above all); binding them onto
        // every collection would bloat each class, so only user assemblies are indexed.
        private static readonly string[] _frameworkAssemblyPrefixes = { "System", "Microsoft", "mscorlib", "netstandard", "WindowsBase", "Python.Runtime" };

        /// <summary>
        /// Drops the index so a re-initialized runtime rebuilds it while re-scanning the assemblies
        /// </summary>
        internal static void Clear()
        {
            _methodsByExtendedType.Clear();
        }

        internal static bool IsExtensionMethod(MethodBase method)
        {
            return method.IsStatic && method.IsDefined(typeof(ExtensionAttribute), false);
        }

        /// <summary>
        /// Indexes the extension methods declared by the given exported types of an assembly
        /// </summary>
        internal static void Register(Assembly assembly, IEnumerable<Type> exportedTypes)
        {
            if (IsFrameworkAssembly(assembly))
            {
                return;
            }

            foreach (var type in exportedTypes)
            {
                // extension methods can only live in non-generic static classes
                if (!type.IsAbstract || !type.IsSealed || type.IsGenericType || !type.IsDefined(typeof(ExtensionAttribute), false))
                {
                    continue;
                }

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!IsExtensionMethod(method))
                    {
                        continue;
                    }

                    var extendedType = method.GetParameters()[0].ParameterType;
                    // The binder cannot infer open generic targets (this T, this IEnumerable<T>) from the instance
                    if (extendedType.ContainsGenericParameters || extendedType.IsByRef)
                    {
                        continue;
                    }

                    var methods = _methodsByExtendedType.GetOrAdd(extendedType, _ => new List<MethodInfo>());
                    lock (methods)
                    {
                        methods.Add(method);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the extension methods to bind to the given type: those extending the type itself and the
        /// interfaces it introduces. Extensions of base classes are inherited through the Python type hierarchy.
        /// </summary>
        internal static List<MethodInfo> GetMethodsToBind(Type type)
        {
            var result = new List<MethodInfo>();
            AddMethods(type, result);

            if (!type.IsInterface)
            {
                var baseInterfaces = type.BaseType?.GetInterfaces() ?? Type.EmptyTypes;
                foreach (var @interface in type.GetInterfaces().Except(baseInterfaces))
                {
                    AddMethods(@interface, result);
                }
            }

            return result;
        }

        private static void AddMethods(Type extendedType, List<MethodInfo> result)
        {
            if (_methodsByExtendedType.TryGetValue(extendedType, out var methods))
            {
                lock (methods)
                {
                    result.AddRange(methods);
                }
            }
        }

        private static bool IsFrameworkAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name ?? string.Empty;
            return _frameworkAssemblyPrefixes.Any(prefix => name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal));
        }
    }
}
