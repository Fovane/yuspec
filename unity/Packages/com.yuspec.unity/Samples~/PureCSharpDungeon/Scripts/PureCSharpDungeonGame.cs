using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Yuspec.Unity.Samples.PureCSharpDungeon
{
    public sealed class PureCSharpDungeonGame : MonoBehaviour
    {
        [SerializeField] private bool autoCreatePrimitiveScene = true;
        [SerializeField] private bool runScenarioChecksOnStart;

        private readonly Dictionary<string, PureCSharpDungeonEntity> entities = new Dictionary<string, PureCSharpDungeonEntity>(StringComparer.OrdinalIgnoreCase);
        private float goblinAttackTimer;
        private float bossAttackTimer;
        private float goblinChaseMessageTimer;
        private bool enteredRoom2;
        private bool enteredBossRoom;

        private PureCSharpDungeonEntity Player => entities["Player"];
        private PureCSharpDungeonEntity Goblin => entities["Goblin"];
        private PureCSharpDungeonEntity Boss => entities["Boss"];
        private PureCSharpDungeonEntity ExitDoor => entities["ExitDoor"];

        private void Start()
        {
            if (autoCreatePrimitiveScene)
            {
                CreatePrimitiveScene();
            }

            RegisterSceneEntities();
            EnsureInput();
            ConfigureCamera();

            if (runScenarioChecksOnStart)
            {
                foreach (var result in RunScenarioChecks())
                {
                    Debug.Log($"[Pure C# Scenario] {result}");
                }
            }
        }

        private void Update()
        {
            TickRoomEntry();
            TickGoblinStateMachine(Time.deltaTime);
            TickBossStateMachine(Time.deltaTime);
        }

        public void MovePlayer(Vector2 input)
        {
            if (!entities.TryGetValue("Player", out var player) || !player.Alive)
            {
                return;
            }

            var direction = new Vector3(input.x, 0f, input.y);
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            player.transform.position += direction * player.MoveSpeed * Time.deltaTime;
            if (direction.sqrMagnitude > 0.0001f)
            {
                player.transform.forward = direction;
            }
        }

        public void InteractWithNearestEntity()
        {
            if (!entities.TryGetValue("Player", out var player))
            {
                return;
            }

            var target = entities.Values
                .Where(entity => entity != player && entity.gameObject.activeInHierarchy)
                .Select(entity => new { Entity = entity, Distance = Vector3.Distance(player.transform.position, entity.transform.position) })
                .Where(candidate => candidate.Distance <= player.InteractRange)
                .OrderBy(candidate => candidate.Distance)
                .Select(candidate => candidate.Entity)
                .FirstOrDefault();

            if (target != null)
            {
                Interact(player, target);
            }
        }

        public void Interact(PureCSharpDungeonEntity actor, PureCSharpDungeonEntity target)
        {
            if (actor == null || target == null || actor.EntityId != "Player")
            {
                return;
            }

            switch (target.EntityId)
            {
                case "Chest":
                    InteractChest(actor, target);
                    break;
                case "DoorRoom1ToRoom2":
                case "DoorRoom2ToBossRoom":
                    InteractDoor(actor, target);
                    break;
                case "Merchant":
                    ShowDialogue("MerchantGreeting", target);
                    break;
                case "Goblin":
                    HitGoblin(actor, target);
                    break;
                case "Boss":
                    HitBoss(actor, target);
                    break;
            }
        }

        public IReadOnlyList<string> RunScenarioChecks()
        {
            var results = new List<string>
            {
                CheckChestGivesKey(),
                CheckLockedDoorStaysClosedWithoutKey(),
                CheckDoorOpensWithKey(),
                CheckGoblinDiesAtZeroHealth(),
                CheckBossEntersPhase2(),
                CheckExitDoorOpensOnBossDeath()
            };

            return results;
        }

        private void InteractChest(PureCSharpDungeonEntity player, PureCSharpDungeonEntity chest)
        {
            if (chest.State != "Closed")
            {
                return;
            }

            chest.State = "Open";
            chest.Opened = true;
            player.GiveItem(chest.Reward);
            PlaySound("chest_open");
            ShowUiMessage("You found the Room 2 key.");
            chest.SetColor(Color.gray);
        }

        private void InteractDoor(PureCSharpDungeonEntity player, PureCSharpDungeonEntity door)
        {
            if (player.HasItem(door.Key))
            {
                door.OpenDoor();
                PlaySound("door_open");
                ShowUiMessage(door.EntityId == "DoorRoom1ToRoom2" ? "The door to Room 2 opens." : "The boss room is open.");
                return;
            }

            if (door.Locked)
            {
                PlaySound("door_locked");
                ShowUiMessage(door.EntityId == "DoorRoom1ToRoom2" ? "The Room 2 door is locked." : "You need the BossKey.");
            }
        }

        private void HitGoblin(PureCSharpDungeonEntity player, PureCSharpDungeonEntity goblin)
        {
            if (!goblin.Alive)
            {
                return;
            }

            goblin.TakeDamage(player.Attack);
            PlaySound("goblin_hit");
            if (goblin.Health <= 0)
            {
                TransitionGoblin("Dead");
            }
        }

        private void HitBoss(PureCSharpDungeonEntity player, PureCSharpDungeonEntity boss)
        {
            if (!boss.Alive)
            {
                return;
            }

            boss.TakeDamage(player.Attack);
            PlaySound("boss_hit");
            if (boss.Health <= 0)
            {
                TransitionBoss("Dead");
            }
            else if (boss.Health <= 60 && boss.State == "Phase1")
            {
                TransitionBoss("Phase2");
            }
        }

        private void TickRoomEntry()
        {
            if (!enteredRoom2 && Player.transform.position.x >= 5.5f)
            {
                enteredRoom2 = true;
                Player.State = "Room2";
                Goblin.Spawned = true;
                Goblin.gameObject.SetActive(true);
                ShowUiMessage("Room 2: a goblin blocks the way.");
                TransitionGoblin("Chase");
            }

            if (!enteredBossRoom && Player.transform.position.x >= 13.5f)
            {
                enteredBossRoom = true;
                Player.State = "BossRoom";
                entities["BossRoom"].State = "Open";
                PlaySound("boss_theme");
                ShowDialogue("BossIntro", Boss);
            }
        }

        private void TickGoblinStateMachine(float deltaTime)
        {
            if (!Goblin.Alive || Goblin.State == "Dead")
            {
                return;
            }

            if (Goblin.Health <= 0)
            {
                TransitionGoblin("Dead");
                return;
            }

            if (Goblin.State == "Chase")
            {
                goblinChaseMessageTimer += deltaTime;
                if (goblinChaseMessageTimer >= 0.5f)
                {
                    goblinChaseMessageTimer = 0f;
                    ShowUiMessage("The goblin is chasing you.");
                }

                if (Vector3.Distance(Goblin.transform.position, Player.transform.position) <= 1.2f)
                {
                    TransitionGoblin("Attack");
                }
            }
            else if (Goblin.State == "Attack")
            {
                goblinAttackTimer += deltaTime;
                if (goblinAttackTimer >= 1f)
                {
                    goblinAttackTimer = 0f;
                    Player.TakeDamage(Goblin.Damage);
                    PlaySound("goblin_attack");
                }

                if (Vector3.Distance(Goblin.transform.position, Player.transform.position) > 1.5f)
                {
                    TransitionGoblin("Chase");
                }
            }
        }

        private void TickBossStateMachine(float deltaTime)
        {
            if (!Boss.Alive || Boss.State == "Dead" || !enteredBossRoom)
            {
                return;
            }

            if (Boss.Health <= 0)
            {
                TransitionBoss("Dead");
                return;
            }

            if (Boss.Health <= 60 && Boss.State == "Phase1")
            {
                TransitionBoss("Phase2");
            }

            bossAttackTimer += deltaTime;
            var interval = Boss.State == "Phase2" ? 0.75f : 1f;
            if (bossAttackTimer >= interval)
            {
                bossAttackTimer = 0f;
                Player.TakeDamage(Boss.Damage);
            }
        }

        private void TransitionGoblin(string state)
        {
            if (Goblin.State == state)
            {
                return;
            }

            Goblin.State = state;
            if (state == "Dead")
            {
                Goblin.Alive = false;
                entities["Room2"].State = "Cleared";
                Player.GiveItem(Goblin.Reward);
                PlaySound("goblin_dead");
                ShowUiMessage("Goblin defeated. BossKey acquired.");
                Goblin.DestroyVisual();
            }
        }

        private void TransitionBoss(string state)
        {
            if (Boss.State == state)
            {
                return;
            }

            Boss.State = state;
            if (state == "Phase2")
            {
                PlaySound("boss_phase_2");
                ShowUiMessage("Boss enters Phase 2.");
                return;
            }

            if (state == "Dead")
            {
                Boss.Alive = false;
                Boss.DestroyVisual();
                ExitDoor.OpenDoor();
                PlaySound("exit_open");
                ShowUiMessage("The exit door opens.");
            }
        }

        private void ShowDialogue(string dialogueName, PureCSharpDungeonEntity speaker)
        {
            if (dialogueName == "MerchantGreeting")
            {
                Debug.Log($"[Pure C# Dialogue:{dialogueName}] {speaker.EntityId}: Welcome to the dungeon outpost.");
                Debug.Log($"[Pure C# Dialogue:{dialogueName}] {speaker.EntityId}: The chest beside me holds the first key.");
                Debug.Log($"[Pure C# Dialogue:{dialogueName}] {speaker.EntityId}: Bring back the BossKey if you survive.");
                Debug.Log($"[Pure C# Dialogue:{dialogueName}] Choice: I am ready. -> end");
                return;
            }

            if (dialogueName == "BossIntro")
            {
                Debug.Log($"[Pure C# Dialogue:{dialogueName}] {speaker.EntityId}: You should not have opened that door.");
                Debug.Log($"[Pure C# Dialogue:{dialogueName}] Choice: Fight. -> end");
            }
        }

        private static void PlaySound(string soundId)
        {
            Debug.Log($"[Pure C# Sound] {soundId}");
        }

        private static void ShowUiMessage(string message)
        {
            Debug.Log($"[Pure C# UI] {message}");
        }

        private string CheckChestGivesKey()
        {
            ResetRuleState();
            Interact(Player, entities["Chest"]);
            return ScenarioResult("chest gives key", entities["Chest"].State == "Open" && Player.HasItem("Room2Key"));
        }

        private string CheckLockedDoorStaysClosedWithoutKey()
        {
            ResetRuleState();
            Player.Inventory.Clear();
            Interact(Player, entities["DoorRoom1ToRoom2"]);
            return ScenarioResult("locked door stays closed without key", entities["DoorRoom1ToRoom2"].State == "Closed");
        }

        private string CheckDoorOpensWithKey()
        {
            ResetRuleState();
            Player.GiveItem("Room2Key");
            Interact(Player, entities["DoorRoom1ToRoom2"]);
            return ScenarioResult("door opens with key", entities["DoorRoom1ToRoom2"].State == "Open");
        }

        private string CheckGoblinDiesAtZeroHealth()
        {
            ResetRuleState();
            Goblin.Health = 0;
            TransitionGoblin("Dead");
            return ScenarioResult("goblin transitions to Dead state on health <= 0", Goblin.State == "Dead");
        }

        private string CheckBossEntersPhase2()
        {
            ResetRuleState();
            Boss.Health = 60;
            TransitionBoss("Phase2");
            return ScenarioResult("boss enters Phase2", Boss.State == "Phase2");
        }

        private string CheckExitDoorOpensOnBossDeath()
        {
            ResetRuleState();
            Boss.Health = 0;
            TransitionBoss("Dead");
            return ScenarioResult("exit door opens on boss death", ExitDoor.State == "Open");
        }

        private static string ScenarioResult(string name, bool passed)
        {
            return $"{name}: {(passed ? "Passed" : "Failed")}";
        }

        private void ResetRuleState()
        {
            Player.Health = 100;
            Player.Alive = true;
            Player.Inventory.Clear();
            entities["Chest"].State = "Closed";
            entities["Chest"].Opened = false;
            entities["DoorRoom1ToRoom2"].State = "Closed";
            entities["DoorRoom1ToRoom2"].Locked = true;
            Goblin.State = "Idle";
            Goblin.Health = 30;
            Goblin.Alive = true;
            Goblin.Spawned = false;
            Boss.State = "Phase1";
            Boss.Health = 120;
            Boss.Alive = true;
            ExitDoor.State = "Closed";
            ExitDoor.Locked = true;
        }

        private void RegisterSceneEntities()
        {
            entities.Clear();
            foreach (var entity in FindObjectsOfType<PureCSharpDungeonEntity>())
            {
                entities[entity.EntityId] = entity;
            }
        }

        private void EnsureInput()
        {
            var input = Player.GetComponent<PureCSharpDungeonInput>();
            if (input == null)
            {
                input = Player.gameObject.AddComponent<PureCSharpDungeonInput>();
            }

            input.Configure(this);
        }

        private void CreatePrimitiveScene()
        {
            CreateFloor("PureRoom1Floor", new Vector3(0f, -0.05f, 0f), Color.gray);
            CreateFloor("PureRoom2Floor", new Vector3(8f, -0.05f, 0f), new Color(0.35f, 0.35f, 0.45f));
            CreateFloor("PureBossRoomFloor", new Vector3(16f, -0.05f, 0f), new Color(0.45f, 0.35f, 0.35f));

            CreateEntity("Player", PureDungeonEntityKind.Player, PrimitiveType.Capsule, new Vector3(-2.5f, 0.5f, 0f), Color.cyan, entity =>
            {
                entity.Health = 100;
                entity.MaxHealth = 100;
                entity.Attack = 10;
                entity.MoveSpeed = 4f;
                entity.InteractRange = 1.6f;
            });

            CreateEntity("Chest", PureDungeonEntityKind.Chest, PrimitiveType.Cube, new Vector3(1.5f, 0.5f, 1.5f), Color.yellow, entity =>
            {
                entity.State = "Closed";
                entity.Reward = "Room2Key";
            });

            CreateEntity("Merchant", PureDungeonEntityKind.Merchant, PrimitiveType.Capsule, new Vector3(-1.5f, 0.5f, 1.7f), Color.magenta);
            CreateDoor("DoorRoom1ToRoom2", new Vector3(4f, 0.8f, 0f), "Room2Key", "Room2");
            CreateEntity("Room2", PureDungeonEntityKind.Room, PrimitiveType.Cube, new Vector3(8f, 0.05f, -2.7f), Color.clear, entity => entity.State = "Quiet", new Vector3(5.5f, 0.1f, 0.2f));

            CreateEntity("Goblin", PureDungeonEntityKind.Enemy, PrimitiveType.Capsule, new Vector3(8f, 0.5f, 0f), Color.green, entity =>
            {
                entity.State = "Idle";
                entity.Health = 30;
                entity.Damage = 8;
                entity.Reward = "BossKey";
                entity.Tags.Add("enemy");
                entity.Tags.Add("goblin");
            });

            CreateDoor("DoorRoom2ToBossRoom", new Vector3(12f, 0.8f, 0f), "BossKey", "BossRoom");
            CreateEntity("BossRoom", PureDungeonEntityKind.Room, PrimitiveType.Cube, new Vector3(16f, 0.05f, -2.7f), Color.clear, entity => entity.State = "Locked", new Vector3(5.5f, 0.1f, 0.2f));
            CreateEntity("Boss", PureDungeonEntityKind.Boss, PrimitiveType.Capsule, new Vector3(16f, 0.7f, 0f), Color.black, entity =>
            {
                entity.State = "Phase1";
                entity.Health = 120;
                entity.Damage = 15;
                entity.Tags.Add("enemy");
                entity.Tags.Add("boss");
            }, new Vector3(1.4f, 1.4f, 1.4f));
            CreateDoor("ExitDoor", new Vector3(19f, 0.8f, 0f), string.Empty, "Exit", PureDungeonEntityKind.ExitDoor);
        }

        private static void CreateFloor(string name, Vector3 position, Color color)
        {
            if (GameObject.Find(name) != null)
            {
                return;
            }

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name;
            floor.transform.position = position;
            floor.transform.localScale = new Vector3(6f, 0.1f, 5f);
            floor.GetComponent<Renderer>().material.color = color;
        }

        private void CreateDoor(string id, Vector3 position, string key, string destination, PureDungeonEntityKind kind = PureDungeonEntityKind.Door)
        {
            CreateEntity(id, kind, PrimitiveType.Cube, position, Color.red, entity =>
            {
                entity.State = "Closed";
                entity.Locked = true;
                entity.Key = key;
                entity.Destination = destination;
            }, new Vector3(0.35f, 1.6f, 2.2f));
        }

        private void CreateEntity(string id, PureDungeonEntityKind kind, PrimitiveType primitive, Vector3 position, Color color)
        {
            CreateEntity(id, kind, primitive, position, color, null, Vector3.one);
        }

        private void CreateEntity(string id, PureDungeonEntityKind kind, PrimitiveType primitive, Vector3 position, Color color, Action<PureCSharpDungeonEntity> configure)
        {
            CreateEntity(id, kind, primitive, position, color, configure, Vector3.one);
        }

        private void CreateEntity(string id, PureDungeonEntityKind kind, PrimitiveType primitive, Vector3 position, Color color, Action<PureCSharpDungeonEntity> configure, Vector3 scale)
        {
            if (GameObject.Find(id) != null)
            {
                return;
            }

            var gameObject = GameObject.CreatePrimitive(primitive);
            gameObject.name = id;
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;

            var entity = gameObject.AddComponent<PureCSharpDungeonEntity>();
            entity.EntityId = id;
            entity.Kind = kind;
            entity.SetColor(color);
            configure?.Invoke(entity);
        }

        private static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }

            camera.orthographic = true;
            camera.orthographicSize = 6.5f;
            camera.transform.position = new Vector3(8f, 14f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
