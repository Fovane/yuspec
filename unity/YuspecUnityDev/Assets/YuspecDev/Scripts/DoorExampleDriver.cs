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
        [SerializeField] private YuspecEntity chest;
        [SerializeField] private bool emitOnStart = true;
        [SerializeField] private float emitDelaySeconds = 0.75f;
        [SerializeField] private float chestDelaySeconds = 0.75f;

        private void Start()
        {
            ApplyInitialVisuals();

            if (!emitOnStart || runtime == null || player == null || door == null || chest == null)
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

            yield return new WaitForSeconds(chestDelaySeconds);

            runtime.Emit("Player.Interact", player, chest);
            ApplyChestStateVisuals();
        }

        private void ApplyInitialVisuals()
        {
            SetColor(player, new Color(0.2f, 0.45f, 1f));
            SetColor(door, new Color(0.8f, 0.18f, 0.12f));
            SetColor(chest, new Color(0.78f, 0.48f, 0.14f));
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

        private void ApplyChestStateVisuals()
        {
            if (chest == null || !chest.TryGetProperty("state", out var state))
            {
                return;
            }

            if (state?.ToString() != "Open")
            {
                return;
            }

            SetColor(chest, new Color(1f, 0.84f, 0.18f));
            chest.transform.localScale = new Vector3(1.15f, 0.45f, 0.85f);
            chest.transform.position += new Vector3(0f, 0.2f, 0f);
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
