using System;
using System.Linq;
using UnityEngine;
using Yuspec.Unity;

namespace Yuspec.Unity.Samples.TopDownDungeon
{
    public sealed class YuspecDemoInput : MonoBehaviour
    {
        [SerializeField] private YuspecRuntime runtime;
        [SerializeField] private YuspecEntity player;
        [SerializeField] private float room2EnterX = 5.5f;
        [SerializeField] private float bossRoomEnterX = 13.5f;
        [SerializeField] private float goblinAttackExitPadding = 0.3f;

        private bool emittedRoom2Enter;
        private bool emittedBossRoomEnter;
        private bool goblinInAttackRange;

        public void Configure(YuspecRuntime configuredRuntime, YuspecEntity configuredPlayer)
        {
            runtime = configuredRuntime;
            player = configuredPlayer;
        }

        private void Awake()
        {
            player = player != null ? player : GetComponent<YuspecEntity>();
        }

        private void Update()
        {
            runtime = runtime != null ? runtime : FindRuntime();
            player = player != null ? player : GetComponent<YuspecEntity>();
            if (runtime == null || player == null)
            {
                return;
            }

            var x = ReadAxis(KeyCode.A, KeyCode.D);
            var y = ReadAxis(KeyCode.S, KeyCode.W);
            player.SetProperty("moveX", x);
            player.SetProperty("moveY", y);

            if (Mathf.Abs(x) > 0.01f || Mathf.Abs(y) > 0.01f)
            {
                runtime.Emit("Player.Move", player);
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                var target = FindNearestInteractable();
                if (target != null)
                {
                    runtime.Emit("Player.Interact", player, target);
                }
            }

            EmitRoomEnterEvents();
            EmitGoblinRangeEvents();
        }

        private void EmitRoomEnterEvents()
        {
            if (!emittedRoom2Enter && player.transform.position.x >= room2EnterX)
            {
                emittedRoom2Enter = true;
                runtime.Emit("Player.EnterRoom2", player, FindEntity("Room2"));
            }

            if (!emittedBossRoomEnter && player.transform.position.x >= bossRoomEnterX)
            {
                emittedBossRoomEnter = true;
                runtime.Emit("Player.EnterBossRoom", player, FindEntity("BossRoom"));
            }
        }

        private void EmitGoblinRangeEvents()
        {
            var goblin = FindEntity("Goblin");
            if (goblin == null || !IsAliveAndSpawned(goblin))
            {
                goblinInAttackRange = false;
                return;
            }

            var distance = Vector3.Distance(goblin.transform.position, player.transform.position);
            var attackRange = ReadFloat(goblin, "attackRange", 1.2f);
            if (!goblinInAttackRange && distance <= attackRange)
            {
                goblinInAttackRange = true;
                runtime.Emit("Goblin.InAttackRange", goblin, player);
                return;
            }

            if (goblinInAttackRange && distance > attackRange + goblinAttackExitPadding)
            {
                goblinInAttackRange = false;
                runtime.Emit("Goblin.PlayerOutOfRange", goblin, player);
            }
        }

        private YuspecEntity FindNearestInteractable()
        {
            var interactRange = ReadFloat(player, "interactRange", 1.6f);
            var playerPosition = player.transform.position;
            return runtime.Entities
                .Where(entity => entity != null && entity != player && entity.gameObject.activeInHierarchy)
                .Select(entity => new { Entity = entity, Distance = Vector3.Distance(playerPosition, entity.transform.position) })
                .Where(candidate => candidate.Distance <= interactRange)
                .OrderBy(candidate => candidate.Distance)
                .Select(candidate => candidate.Entity)
                .FirstOrDefault();
        }

        private YuspecEntity FindEntity(string id)
        {
            return runtime.Entities.FirstOrDefault(entity =>
                entity != null && string.Equals(entity.EntityId, id, StringComparison.OrdinalIgnoreCase));
        }

        private static float ReadAxis(KeyCode negative, KeyCode positive)
        {
            var value = 0f;
            if (Input.GetKey(negative))
            {
                value -= 1f;
            }

            if (Input.GetKey(positive))
            {
                value += 1f;
            }

            return value;
        }

        private static float ReadFloat(YuspecEntity entity, string property, float fallback)
        {
            if (entity.TryGetProperty(property, out var value) && float.TryParse(value?.ToString(), out var parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static bool IsAliveAndSpawned(YuspecEntity entity)
        {
            return ReadBool(entity, "alive", true) &&
                   ReadBool(entity, "spawned", entity.gameObject.activeInHierarchy) &&
                   entity.gameObject.activeInHierarchy;
        }

        private static bool ReadBool(YuspecEntity entity, string property, bool fallback)
        {
            if (entity.TryGetProperty(property, out var value) && bool.TryParse(value?.ToString(), out var parsed))
            {
                return parsed;
            }

            return fallback;
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
