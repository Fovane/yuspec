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
            Runtime?.Emit($"{SafeType(actor)}.EnterZone(\"{zoneName}\")", actor);
        }

        public void EmitDeath(YuspecEntity entity)
        {
            Runtime?.Emit($"{SafeType(entity)}.Died", entity);
        }

        public void EmitHealthBelow(YuspecEntity entity, float percentage)
        {
            Runtime?.Emit($"{SafeType(entity)}.HealthBelow({percentage:0.##}%)", entity);
        }

        private static string SafeType(YuspecEntity entity)
        {
            return entity != null ? entity.EntityType : "Unknown";
        }
    }
}
