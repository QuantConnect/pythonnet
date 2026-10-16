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
        private static readonly ConcurrentDictionary<Assembly, bool> _isFrameworkAssembly = new();
        private static volatile bool _hasInterfaceTargets;

        // The framework ships thousands of extension methods (LINQ above all); binding them onto
        // every collection would bloat each class, so only user assemblies are indexed.
        private static readonly string[] _frameworkAssemblyPrefixes = { "System", "Microsoft", "mscorlib", "netstandard", "WindowsBase", "Python.Runtime" };

        /// <summary>
        /// Drops the index so a re-initialized runtime rebuilds it while re-scanning the assemblies
        /// </summary>
        internal static void Clear()
        {
            _methodsByExtendedType.Clear();
            _isFrameworkAssembly.Clear();
            _hasInterfaceTargets = false;
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
            // The compiler marks the assemblies declaring extension methods, C# itself skips the unmarked ones
            if (IsFrameworkAssembly(assembly) || !assembly.IsDefined(typeof(ExtensionAttribute), false))
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
                    if (!IsBindableTarget(extendedType))
                    {
                        continue;
                    }

                    if (extendedType.IsInterface)
                    {
                        _hasInterfaceTargets = true;
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
        internal static IReadOnlyList<MethodInfo> GetMethodsToBind(Type type)
        {
            List<MethodInfo>? result = null;
            AddMethods(type, ref result);

            if (_hasInterfaceTargets && !type.IsInterface)
            {
                Type[]? baseInterfaces = null;
                foreach (var @interface in type.GetInterfaces())
                {
                    if (!_methodsByExtendedType.ContainsKey(@interface))
                    {
                        continue;
                    }

                    baseInterfaces ??= type.BaseType?.GetInterfaces() ?? Type.EmptyTypes;
                    if (Array.IndexOf(baseInterfaces, @interface) < 0)
                    {
                        AddMethods(@interface, ref result);
                    }
                }
            }

            return result ?? (IReadOnlyList<MethodInfo>)Array.Empty<MethodInfo>();
        }

        private static void AddMethods(Type extendedType, ref List<MethodInfo>? result)
        {
            if (_methodsByExtendedType.TryGetValue(extendedType, out var methods))
            {
                lock (methods)
                {
                    (result ??= new List<MethodInfo>()).AddRange(methods);
                }
            }
        }

        /// <summary>
        /// Whether the extension methods of the given type should be bound to its instances. The binder cannot
        /// infer open generic targets (this T, this IEnumerable&lt;T&gt;) from the instance. Targets made only of
        /// framework types (object, IEnumerable, double[], List&lt;int&gt;) are shared by countless classes, which
        /// would all be bloated with them (Accord.Math alone extends each primitive array with ~600 methods),
        /// so those keep their static form only.
        /// </summary>
        private static bool IsBindableTarget(Type type)
        {
            if (type.IsByRef || type.ContainsGenericParameters)
            {
                return false;
            }
            if (type.HasElementType)
            {
                return IsBindableTarget(type.GetElementType()!);
            }
            if (!IsFrameworkAssembly(type.Assembly))
            {
                return true;
            }
            return type.IsGenericType && type.GetGenericArguments().Any(IsBindableTarget);
        }

        private static bool IsFrameworkAssembly(Assembly assembly)
        {
            return _isFrameworkAssembly.GetOrAdd(assembly, a =>
            {
                var name = a.GetName().Name ?? string.Empty;
                return _frameworkAssemblyPrefixes.Any(prefix => name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal));
            });
        }
    }
}
