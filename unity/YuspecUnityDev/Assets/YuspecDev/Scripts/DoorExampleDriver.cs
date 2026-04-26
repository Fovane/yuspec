using System.Collections.Generic;
using System.Collections;
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
        [SerializeField] private float emitDelaySeconds = 0.75f;

        private void Start()
        {
            ApplyInitialVisuals();

            if (!emitOnStart || runtime == null || player == null || door == null)
            {
                return;
            }

            StartCoroutine(RunDoorExample());
        }

        private IEnumerator RunDoorExample()
        {
            yield return new WaitForSeconds(emitDelaySeconds);

            player.SetProperty("inventory", new List<string> { "IronKey" });
            runtime.Emit("Player.Interact", player, door);
            ApplyDoorStateVisuals();
        }

        private void ApplyInitialVisuals()
        {
            SetColor(player, new Color(0.2f, 0.45f, 1f));
            SetColor(door, new Color(0.8f, 0.18f, 0.12f));
        }

        private void ApplyDoorStateVisuals()
        {
            if (door == null || !door.TryGetProperty("state", out var state))
            {
                return;
            }

            if (state?.ToString() != "Open")
            {
                return;
            }

            SetColor(door, new Color(0.1f, 0.75f, 0.25f));
            door.transform.rotation = Quaternion.Euler(0f, 72f, 0f);
            door.transform.position += new Vector3(0.35f, 0f, 0.35f);
        }

        private static void SetColor(YuspecEntity entity, Color color)
        {
            if (entity == null || !entity.TryGetComponent<Renderer>(out var renderer))
            {
                return;
            }

            renderer.material.color = color;
        }
    }
}
