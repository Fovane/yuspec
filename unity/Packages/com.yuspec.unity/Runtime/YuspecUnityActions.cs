using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Yuspec.Unity
{
    public static class YuspecUnityActions
    {
        public static event Action<string> UiMessage;

        [YuspecAction("move_player")]
        public static void MovePlayer(YuspecEntity player)
        {
            if (player == null)
            {
                return;
            }

            var moveX = ReadFloat(player, "moveX", 0f);
            var moveY = ReadFloat(player, "moveY", 0f);
            var speed = ReadFloat(player, "moveSpeed", 4f);
            var direction = new Vector3(moveX, 0f, moveY);
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            player.transform.position += direction * speed * Time.deltaTime;
            if (direction.sqrMagnitude > 0.0001f)
            {
                player.transform.forward = direction;
            }
        }

        [YuspecAction("take_damage")]
        public static void TakeDamage(YuspecEntity target, string byKeyword, int amount)
        {
            if (target == null)
            {
                return;
            }

            var current = ReadInt(target, "health", 0);
            var next = Mathf.Max(0, current - Mathf.Max(0, amount));
            target.SetProperty("health", next);
            target.SetProperty("alive", next > 0);
            Debug.Log($"YUSPEC take_damage '{target.EntityId}' {byKeyword} {amount} -> {next}.");
        }

        [YuspecAction("open_door")]
        public static void OpenDoor(YuspecEntity door)
        {
            if (door == null)
            {
                return;
            }

            door.SetProperty("state", "Open");
            door.SetProperty("locked", false);
            var collider = door.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            var renderer = door.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.green;
            }

            Debug.Log($"YUSPEC open_door '{door.EntityId}'.");
        }

        [YuspecAction("give_item")]
        public static void GiveItem(YuspecEntity target, string itemId)
        {
            Give(target, itemId);
        }

        [YuspecAction("spawn_enemy")]
        public static void SpawnEnemy(YuspecEntity enemy)
        {
            if (enemy == null)
            {
                return;
            }

            enemy.gameObject.SetActive(true);
            enemy.SetProperty("spawned", true);
            enemy.SetProperty("alive", true);
            Debug.Log($"YUSPEC spawn_enemy '{enemy.EntityId}'.");
        }

        [YuspecAction("destroy_entity")]
        public static void DestroyEntityForDemo(YuspecEntity target)
        {
            if (target == null)
            {
                return;
            }

            target.SetProperty("destroyed", true);
            target.SetProperty("alive", false);
            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }

            foreach (var collider in target.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            Debug.Log($"YUSPEC destroy_entity '{target.EntityId}'.");
        }

        [YuspecAction("show_ui_message")]
        public static void ShowUiMessage(string message)
        {
            UiMessage?.Invoke(message);
            Debug.Log($"YUSPEC ui '{message}'.");
        }

        [YuspecAction("start_dialogue")]
        public static void StartDialogueFallback(string dialogueName)
        {
            var runtime = FindRuntime();
            var dialogue = runtime?.CompiledSpecs
                .SelectMany(spec => spec.Dialogues)
                .FirstOrDefault(candidate => string.Equals(candidate.Name, dialogueName, StringComparison.OrdinalIgnoreCase));
            runtime?.DialogueRuntime?.StartDialogue(dialogue, null);
        }

        [YuspecAction("play_animation")]
        public static void PlayAnimation(YuspecEntity target, string animationName)
        {
            if (target == null || string.IsNullOrWhiteSpace(animationName))
            {
                return;
            }

            var animator = target.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play(animationName);
                return;
            }

            Debug.Log($"YUSPEC play_animation skipped: '{target.EntityId}' has no Animator.");
        }

        [YuspecAction("play_sound")]
        public static void PlaySound(string soundId)
        {
            if (string.IsNullOrWhiteSpace(soundId))
            {
                return;
            }

            // Projects can replace this with their own audio service binding.
            Debug.Log($"YUSPEC play_sound '{soundId}'.");
        }

        [YuspecAction("give")]
        public static void Give(YuspecEntity target, string itemId)
        {
            if (target == null || string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            var inventory = new List<string>();
            if (target.TryGetProperty("inventory", out var existing) && existing is IEnumerable<string> existingItems)
            {
                inventory.AddRange(existingItems);
            }

            if (!inventory.Any(item => string.Equals(item, itemId, System.StringComparison.OrdinalIgnoreCase)))
            {
                inventory.Add(itemId);
            }

            target.SetProperty("inventory", inventory);
            Debug.Log($"YUSPEC give '{itemId}' to '{target.EntityId}'.");
        }

        [YuspecAction("move_towards")]
        public static void MoveTowards(YuspecEntity target, string speedKeyword, float speed)
        {
            if (target == null)
            {
                return;
            }

            Debug.Log($"YUSPEC move_towards '{target.EntityId}' {speedKeyword} {speed}.");
        }

        [YuspecAction("damage")]
        public static void Damage(YuspecEntity target, string byKeyword, int amount)
        {
            if (target == null)
            {
                return;
            }

            if (!target.TryGetProperty("health", out var healthValue) || !int.TryParse(healthValue?.ToString(), out var health))
            {
                Debug.Log($"YUSPEC damage skipped: '{target.EntityId}' has no numeric health.");
                return;
            }

            target.SetProperty("health", Mathf.Max(0, health - amount));
            Debug.Log($"YUSPEC damage '{target.EntityId}' {byKeyword} {amount}.");
        }

        [YuspecAction("spawn")]
        public static void Spawn(string itemId, string atKeyword, object location)
        {
            Debug.Log($"YUSPEC spawn '{itemId}' {atKeyword} '{location}'.");
        }

        [YuspecAction("destroy")]
        public static void DestroyEntity(YuspecEntity target)
        {
            if (target == null)
            {
                return;
            }

            target.SetProperty("destroyed", true);
            Debug.Log($"YUSPEC destroy '{target.EntityId}'.");
        }

        [YuspecAction("start_quest")]
        public static void StartQuest(string questId)
        {
            Debug.Log($"YUSPEC start_quest '{questId}'.");
        }

        [YuspecAction("complete_quest")]
        public static void CompleteQuest(string questId)
        {
            Debug.Log($"YUSPEC complete_quest '{questId}'.");
        }

        [YuspecAction("play_music")]
        public static void PlayMusic(string musicId)
        {
            Debug.Log($"YUSPEC play_music '{musicId}'.");
        }

        [YuspecAction("play_cutscene")]
        public static void PlayCutscene(string cutsceneId)
        {
            Debug.Log($"YUSPEC play_cutscene '{cutsceneId}'.");
        }

        [YuspecAction("set_state")]
        public static void SetState(YuspecEntity target, string stateName)
        {
            if (target == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            target.CurrentState = stateName;
            Debug.Log($"YUSPEC set_state '{target.EntityId}' -> '{stateName}'.");
        }

        private static int ReadInt(YuspecEntity entity, string propertyName, int fallback)
        {
            if (entity.TryGetProperty(propertyName, out var value) && int.TryParse(value?.ToString(), out var parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static float ReadFloat(YuspecEntity entity, string propertyName, float fallback)
        {
            if (entity.TryGetProperty(propertyName, out var value) && float.TryParse(value?.ToString(), out var parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static YuspecRuntime FindRuntime()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<YuspecRuntime>();
#else
            return UnityEngine.Object.FindObjectOfType<YuspecRuntime>();
#endif
        }
    }
}
