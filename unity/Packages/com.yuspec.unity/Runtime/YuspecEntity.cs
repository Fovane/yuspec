using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yuspec.Unity
{
    public sealed class YuspecEntity : MonoBehaviour
    {
        [SerializeField] private string entityId;
        [SerializeField] private string entityType;
        [SerializeField] private string currentState;
        [SerializeField] private List<string> tags = new List<string>();

        private readonly Dictionary<string, object> properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private YuspecRuntime runtime;

        public string EntityId
        {
            get => string.IsNullOrWhiteSpace(entityId) ? name : entityId;
            set => entityId = value;
        }

        public string EntityType
        {
            get => string.IsNullOrWhiteSpace(entityType) ? gameObject.name : entityType;
            set => entityType = value;
        }

        public string CurrentState
        {
            get => currentState;
            set
            {
                currentState = value;
                properties["state"] = value;
            }
        }

        public IReadOnlyList<string> Tags => tags;
        public IReadOnlyDictionary<string, object> Properties => properties;

        private void OnEnable()
        {
            runtime = FindRuntime();
            runtime?.RegisterEntity(this);
        }

        private void OnDisable()
        {
            runtime?.UnregisterEntity(this);
            runtime = null;
        }

        public void SetProperty(string propertyName, object value)
        {
            SetProperty(propertyName, value, false);
        }

        internal void SetPropertyFromRuntime(string propertyName, object value)
        {
            SetProperty(propertyName, value, true);
        }

        private void SetProperty(string propertyName, object value, bool runtimeValidated)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return;
            }

            if (!runtimeValidated && runtime != null && runtime.TryGetEntityPropertyDeclaration(EntityType, propertyName, out var declaration))
            {
                if (!YuspecSpecParser.TryConvertToYuspecType(value, declaration.Type, out var converted))
                {
                    runtime.ReportTypeMismatch(EntityType, propertyName, declaration.Type, value, declaration.SourceName, declaration.Line);
                    return;
                }

                value = converted;
            }

            properties[propertyName] = value;
            if (string.Equals(propertyName, "state", StringComparison.OrdinalIgnoreCase))
            {
                currentState = value?.ToString();
            }
        }

        public bool TryGetProperty(string propertyName, out object value)
        {
            if (properties.TryGetValue(propertyName, out value))
            {
                return true;
            }

            if (string.Equals(propertyName, "id", StringComparison.OrdinalIgnoreCase))
            {
                value = EntityId;
                return true;
            }

            if (string.Equals(propertyName, "type", StringComparison.OrdinalIgnoreCase))
            {
                value = EntityType;
                return true;
            }

            if (string.Equals(propertyName, "state", StringComparison.OrdinalIgnoreCase))
            {
                value = CurrentState;
                return true;
            }

            value = null;
            return false;
        }

        public bool HasTag(string tag)
        {
            return tags.Contains(tag);
        }

        public bool HasValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (var property in properties.Values)
            {
                if (property is string text && string.Equals(text, value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (property is IEnumerable<string> values)
                {
                    foreach (var item in values)
                    {
                        if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return tags.Contains(value);
        }

        private static YuspecRuntime FindRuntime()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<YuspecRuntime>();
#else
            return FindObjectOfType<YuspecRuntime>();
#endif
        }
    }
}
