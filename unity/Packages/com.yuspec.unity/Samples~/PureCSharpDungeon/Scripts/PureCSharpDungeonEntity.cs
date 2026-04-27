using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yuspec.Unity.Samples.PureCSharpDungeon
{
    public enum PureDungeonEntityKind
    {
        Player,
        Room,
        Chest,
        Door,
        Merchant,
        Enemy,
        Boss,
        ExitDoor
    }

    public sealed class PureCSharpDungeonEntity : MonoBehaviour
    {
        public string EntityId;
        public PureDungeonEntityKind Kind;

        public int Health;
        public int MaxHealth;
        public int Attack;
        public int Damage;
        public float MoveSpeed;
        public float InteractRange;
        public bool Alive = true;
        public bool Spawned = true;
        public bool Opened;
        public bool Locked;
        public string State = "Idle";
        public string Key;
        public string Reward;
        public string Destination;
        public readonly List<string> Inventory = new List<string>();
        public readonly List<string> Tags = new List<string>();

        public bool HasItem(string itemId)
        {
            return Inventory.Exists(item => string.Equals(item, itemId, StringComparison.OrdinalIgnoreCase));
        }

        public void GiveItem(string itemId)
        {
            if (!string.IsNullOrWhiteSpace(itemId) && !HasItem(itemId))
            {
                Inventory.Add(itemId);
            }
        }

        public void TakeDamage(int amount)
        {
            Health = Mathf.Max(0, Health - Mathf.Max(0, amount));
            Alive = Health > 0;
        }

        public void OpenDoor()
        {
            State = "Open";
            Locked = false;

            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            SetColor(Color.green);
        }

        public void DestroyVisual()
        {
            Alive = false;
            State = "Dead";

            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }

            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }
        }

        public void SetColor(Color color)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null && color.a > 0f)
            {
                renderer.material.color = color;
            }
        }
    }
}
