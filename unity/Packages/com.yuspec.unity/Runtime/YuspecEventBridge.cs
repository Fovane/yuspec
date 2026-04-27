using UnityEngine;

namespace Yuspec.Unity
{
    public sealed class YuspecEventBridge : MonoBehaviour
    {
        [SerializeField] private YuspecRuntime runtime;

        private YuspecRuntime Runtime
        {
            get
            {
                if (runtime == null)
                {
#if UNITY_2023_1_OR_NEWER
                    runtime = FindFirstObjectByType<YuspecRuntime>();
#else
                    runtime = FindObjectOfType<YuspecRuntime>();
#endif
                }

                return runtime;
            }
        }

        public void EmitInteract(YuspecEntity actor, YuspecEntity target)
        {
            Runtime?.Emit($"{SafeType(actor)}.Interact", actor, target);
        }

        public void EmitEnterZone(YuspecEntity actor, string zoneName)
        {
            Runtime?.Emit($"{SafeType(actor)}.Enter{SafeEventSegment(zoneName)}", actor);
        }

        public void EmitEnterZone(YuspecEntity actor, YuspecEntity zone)
        {
            Runtime?.Emit($"{SafeType(actor)}.Enter{SafeType(zone)}", actor, zone);
        }

        public void EmitDeath(YuspecEntity entity)
        {
            Runtime?.Emit($"{SafeType(entity)}.Died", entity);
        }

        public void EmitHealthBelow(YuspecEntity entity, float percentage)
        {
            entity?.SetProperty("healthPercent", percentage);
            Runtime?.Emit($"{SafeType(entity)}.HealthBelow", entity);
        }

        private static string SafeType(YuspecEntity entity)
        {
            return entity != null ? entity.EntityType : "Unknown";
        }

        private static string SafeEventSegment(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "Unknown";
            }

            var builder = new System.Text.StringBuilder();
            foreach (var character in text)
            {
                if (char.IsLetterOrDigit(character) || character == '_')
                {
                    builder.Append(character);
                }
            }

            return builder.Length > 0 ? builder.ToString() : "Unknown";
        }
    }
}
