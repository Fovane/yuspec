using System.Collections.Generic;
using UnityEngine;
using Yuspec.Unity;

namespace Yuspec.Dev
{
    public sealed class DoorExampleDriver : MonoBehaviour
    {
        [SerializeField] private YuspecRuntime runtime;
        [SerializeField] private YuspecEntity player;
        [SerializeField] private YuspecEntity door;
        [SerializeField] private bool emitOnStart = true;

        private void Start()
        {
            if (!emitOnStart || runtime == null || player == null || door == null)
            {
                return;
            }

            player.SetProperty("inventory", new List<string> { "IronKey" });
            runtime.Emit("Player.Interact", player, door);
        }
    }
}
