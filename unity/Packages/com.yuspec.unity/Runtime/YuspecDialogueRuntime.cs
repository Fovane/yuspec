using System;
using UnityEngine;

namespace Yuspec.Unity
{
    public sealed class YuspecDialogueRuntime : MonoBehaviour
    {
        public event Action<string, YuspecEntity, string> OnLine;
        public event Action<string, YuspecEntity, string, string> OnChoice;
        public event Action<string, YuspecEntity> OnEnd;

        public void StartDialogue(YuspecDialogueDefinition dialogue, YuspecEntity speaker)
        {
            if (dialogue == null)
            {
                return;
            }

            foreach (var entry in dialogue.Entries)
            {
                if (entry.Kind == YuspecDialogueEntryKind.Line)
                {
                    OnLine?.Invoke(dialogue.Name, speaker, entry.Text);
                    continue;
                }

                OnChoice?.Invoke(dialogue.Name, speaker, entry.Text, entry.Target);
                if (string.Equals(entry.Target, "end", StringComparison.OrdinalIgnoreCase))
                {
                    OnEnd?.Invoke(dialogue.Name, speaker);
                }
            }
        }
    }
}
