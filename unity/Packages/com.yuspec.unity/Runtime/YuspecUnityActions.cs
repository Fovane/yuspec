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
    }
}
