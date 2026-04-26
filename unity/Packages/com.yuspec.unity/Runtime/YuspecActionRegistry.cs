using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Yuspec.Unity
{
    public sealed class YuspecActionRegistry
    {
        public sealed class ActionBinding
        {
            public string Name { get; }
            public MethodInfo Method { get; }
            public object Target { get; }
            public Type[] ParameterTypes { get; }

            public ActionBinding(string name, MethodInfo method, object target)
            {
                Name = name;
                Method = method;
                Target = target;
                ParameterTypes = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            }

            public override string ToString()
            {
                return $"{Name}({string.Join(", ", ParameterTypes.Select(type => type.Name))})";
            }
        }

        private readonly Dictionary<string, ActionBinding> bindings = new Dictionary<string, ActionBinding>(StringComparer.OrdinalIgnoreCase);
        private readonly List<YuspecDiagnostic> diagnostics = new List<YuspecDiagnostic>();

        public IReadOnlyCollection<ActionBinding> RegisteredActions => bindings.Values;
        public IReadOnlyList<YuspecDiagnostic> Diagnostics => diagnostics;

        public void Clear()
        {
            bindings.Clear();
            diagnostics.Clear();
        }

        public void RegisterFromLoadedAssemblies()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                RegisterFromAssembly(assembly);
            }
        }

        public void RegisterFromAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                return;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.Where(type => type != null).ToArray();
            }

            foreach (var type in types)
            {
                RegisterFromType(type);
            }
        }

        public bool TryGetAction(string actionName, out ActionBinding binding)
        {
            return bindings.TryGetValue(actionName, out binding);
        }

        public bool Invoke(string actionName, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0001", "Action name is empty."));
                return false;
            }

            if (!bindings.TryGetValue(actionName, out var binding))
            {
                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0002", $"Unknown action '{actionName}'."));
                return false;
            }

            var parameters = binding.Method.GetParameters();
            if (parameters.Length != args.Length)
            {
                diagnostics.Add(new YuspecDiagnostic(
                    YuspecDiagnosticSeverity.Error,
                    "YSP0003",
                    $"Action '{actionName}' expects {parameters.Length} argument(s), got {args.Length}."));
                return false;
            }

            var converted = new object[args.Length];
            for (var i = 0; i < args.Length; i++)
            {
                if (!TryConvert(args[i], parameters[i].ParameterType, out converted[i]))
                {
                    diagnostics.Add(new YuspecDiagnostic(
                        YuspecDiagnosticSeverity.Error,
                        "YSP0004",
                        $"Action '{actionName}' argument {i + 1} cannot convert to {parameters[i].ParameterType.Name}."));
                    return false;
                }
            }

            try
            {
                binding.Method.Invoke(binding.Target, converted);
                return true;
            }
            catch (TargetInvocationException exception)
            {
                diagnostics.Add(new YuspecDiagnostic(
                    YuspecDiagnosticSeverity.Error,
                    "YSP0005",
                    $"Action '{actionName}' failed: {exception.InnerException?.Message ?? exception.Message}"));
                return false;
            }
        }

        private void RegisterFromType(Type type)
        {
            if (type == null)
            {
                return;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            foreach (var method in type.GetMethods(flags))
            {
                var attribute = method.GetCustomAttribute<YuspecActionAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                Register(method, attribute.Name);
            }
        }

        private void Register(MethodInfo method, string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0006", $"Action on '{method.DeclaringType?.FullName}.{method.Name}' has an empty name."));
                return;
            }

            if (bindings.ContainsKey(actionName))
            {
                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0007", $"Duplicate action binding '{actionName}'."));
                return;
            }

            var target = CreateTarget(method);
            if (!method.IsStatic && target == null)
            {
                diagnostics.Add(new YuspecDiagnostic(
                    YuspecDiagnosticSeverity.Warning,
                    "YSP0008",
                    $"Action '{actionName}' is an instance method and no target could be created."));
                return;
            }

            bindings[actionName] = new ActionBinding(actionName, method, target);
        }

        private static object CreateTarget(MethodInfo method)
        {
            if (method.IsStatic)
            {
                return null;
            }

            var type = method.DeclaringType;
            if (type == null)
            {
                return null;
            }

            if (typeof(MonoBehaviour).IsAssignableFrom(type))
            {
#if UNITY_2023_1_OR_NEWER
                return UnityEngine.Object.FindFirstObjectByType(type);
#else
                return UnityEngine.Object.FindObjectOfType(type);
#endif
            }

            return type.GetConstructor(Type.EmptyTypes) != null ? Activator.CreateInstance(type) : null;
        }

        private static bool TryConvert(object value, Type targetType, out object converted)
        {
            if (value == null)
            {
                converted = null;
                return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
            }

            if (targetType.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            try
            {
                var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
                if (type.IsEnum && value is string enumText)
                {
                    converted = Enum.Parse(type, enumText, true);
                    return true;
                }

                converted = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                converted = null;
                return false;
            }
        }
    }
}
