using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Yuspec.Unity
{
    public static class YuspecUnityActions
    {
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
    }
}
