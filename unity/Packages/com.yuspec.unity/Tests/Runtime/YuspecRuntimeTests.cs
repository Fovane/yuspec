using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Yuspec.Unity.Tests
{
    public sealed class YuspecRuntimeTests
    {
        [Test]
        public void EventRuleRuntime_UpdatesDoorState()
        {
            const string source = @"
entity Player {
    inventory = [""IronKey""]
}

entity Door {
    state = Closed
    key = ""IronKey""
}

on Player.Interact with Door when Player.has(Door.key):
    set Door.state = Open
";

            var runtime = CreateRuntime(source);
            var player = CreateEntity("Player01", "Player");
            var door = CreateEntity("Door01", "Door");

            runtime.RegisterEntity(player);
            runtime.RegisterEntity(door);
            runtime.Emit("Player.Interact", player, door);

            Assert.That(door.CurrentState, Is.EqualTo("Open"));

            DestroyEntity(player);
            DestroyEntity(door);
            DestroyRuntime(runtime);
        }

        [Test]
        public void StrictValidation_ReportsUnknownTransitionTarget()
        {
            const string source = @"
entity Goblin {
    state = Idle
}

behavior GoblinAI for Goblin {
    state Idle {
        on PlayerSeen -> MissingState
    }
}
";

            LogAssert.Expect(LogType.Error, new Regex(@".*\(\d+,1\): error YUSPEC: Unknown transition target 'MissingState'.*"));
            var runtime = CreateRuntime(source);
            Assert.That(runtime.Diagnostics.Any(d => d.code == "YSP0115"), Is.True);
            DestroyRuntime(runtime);
        }

        [Test]
        public void StateMachineRuntime_TransitionsOnEvent()
        {
            const string source = @"
entity Goblin {
    state = Idle
}

behavior GoblinAI for Goblin {
    state Idle {
        on PlayerSeen -> Chase
    }

    state Chase {
        every 0.1s:
            set self.state = Chase
    }
}
";

            var runtime = CreateRuntime(source);
            var goblin = CreateEntity("Goblin01", "Goblin");
            runtime.RegisterEntity(goblin);

            runtime.Emit("Goblin.PlayerSeen", goblin, null);

            Assert.That(goblin.CurrentState, Is.EqualTo("Chase"));
            Assert.That(runtime.StateMachineStatuses.Any(), Is.True);

            DestroyEntity(goblin);
            DestroyRuntime(runtime);
        }

        [Test]
        public void StateMachineRuntime_MoveTowardsAdvancesGoblinTowardPlayer()
        {
            const string source = @"
entity Goblin {
    state = Idle
}

entity Player {
}

behavior GoblinAI for Goblin {
    state Idle {
        on PlayerSeen -> Chase
    }

    state Chase {
        every 0.1s:
            move_towards Player speed 3
    }
}
";

            var runtime = CreateRuntime(source);
            var goblin = CreateEntity("Goblin01", "Goblin");
            var player = CreateEntity("Player01", "Player");
            goblin.transform.position = Vector3.zero;
            player.transform.position = new Vector3(5f, 0f, 0f);

            runtime.RegisterEntity(goblin);
            runtime.RegisterEntity(player);
            runtime.Emit("Goblin.PlayerSeen", goblin, player);
            runtime.TickStateMachines(0.2f);

            Assert.That(goblin.transform.position.x, Is.GreaterThan(0f));
            Assert.That(goblin.transform.position.x, Is.LessThan(5f));
            Assert.That(goblin.transform.position.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(runtime.Diagnostics.Any(d => d.code == "YSP0105"), Is.False);

            DestroyEntity(goblin);
            DestroyEntity(player);
            DestroyRuntime(runtime);
        }

        [Test]
        public void StateMachineRuntime_DormantBossDoesNotDamagePlayerUntilActivated()
        {
            const string source = @"
entity Player {
    health: int = 100
}

entity Boss {
    state: string = ""Dormant""
    damage: int = 15
}

behavior BossAI for Boss {
    state Dormant {
        on PlayerSeen -> Phase1
    }

    state Phase1 {
        every 1s:
            take_damage Player by Boss.damage
    }
}
";

            var runtime = CreateRuntime(source);
            var player = CreateEntity("Player01", "Player");
            var boss = CreateEntity("Boss01", "Boss");

            runtime.RegisterEntity(player);
            runtime.RegisterEntity(boss);
            runtime.TickStateMachines(1.1f);

            Assert.That(player.TryGetProperty("health", out var idleHealth), Is.True);
            Assert.That(idleHealth, Is.EqualTo(100));

            runtime.Emit("Boss.PlayerSeen", boss, player);
            runtime.TickStateMachines(1.1f);

            Assert.That(player.TryGetProperty("health", out var activeHealth), Is.True);
            Assert.That(activeHealth, Is.EqualTo(85));

            DestroyEntity(player);
            DestroyEntity(boss);
            DestroyRuntime(runtime);
        }

        [Test]
        public void ScenarioRunner_ReturnsPassForDoorCase()
        {
            const string source = @"
entity Player {
    inventory = []
}

entity Door {
    state = Closed
    key = ""IronKey""
}

on Player.Interact with Door when Player.has(Door.key):
    set Door.state = Open

scenario ""door opens with key"" {
    given Player has ""IronKey""
    when Player.Interact Door
    expect Door.state == Open
}
";

            var runtime = CreateRuntime(source);
            var player = CreateEntity("Player01", "Player");
            var door = CreateEntity("Door01", "Door");
            runtime.RegisterEntity(player);
            runtime.RegisterEntity(door);

            var results = runtime.RunScenarios();

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Passed, Is.True);

            DestroyEntity(player);
            DestroyEntity(door);
            DestroyRuntime(runtime);
        }

        [Test]
        public void ActionRegistry_RejectsWrongArgumentCount()
        {
            var registry = new YuspecActionRegistry();
            registry.RegisterFromAssembly(typeof(YuspecUnityActions).Assembly);

            var ok = registry.Invoke("play_sound");
            Assert.That(ok, Is.False);
            Assert.That(registry.Diagnostics.Any(d => d.code == "YSP0003"), Is.True);
        }

        [Test]
        public void HotReload_ReloadsChangedSpecAsset()
        {
            var specAsset = ScriptableObject.CreateInstance<YuspecSpecAsset>();
            specAsset.SetSource("HotReloadSpec", @"
entity Door {
    state = Closed
}
");

            var runtime = CreateRuntime(specAsset);
            var door = CreateEntity("Door01", "Door");
            runtime.RegisterEntity(door);
            Assert.That(door.CurrentState, Is.EqualTo("Closed"));
            door.CurrentState = "Locked";

            specAsset.SetSource("HotReloadSpec", @"
entity Door {
    state = Open
}
");

            LogAssert.Expect(LogType.Log, new Regex(@"\[YUSPEC\] Hot reloaded: HotReloadSpec . 0 handlers updated"));
            var reloaded = runtime.ReloadSpecsIfChanged();

            Assert.That(reloaded, Is.True);
            Assert.That(door.CurrentState, Is.EqualTo("Locked"));
            Assert.That(runtime.Diagnostics.Any(d => d.code == "YSP0600"), Is.True);

            DestroyEntity(door);
            DestroyRuntime(runtime);
            UnityEngine.Object.DestroyImmediate(specAsset);
        }

        [Test]
        public void EventBridge_EnterZoneEntity_EmitsSupportedEventName()
        {
            const string source = @"
entity Player {
}

entity BossRoom {
    state = Locked
}

on Player.EnterBossRoom with BossRoom:
    set BossRoom.state = Open
";

            var runtime = CreateRuntime(source);
            var player = CreateEntity("Player01", "Player");
            var bossRoom = CreateEntity("BossRoom01", "BossRoom");
            var bridgeObject = new GameObject("Bridge");
            var bridge = bridgeObject.AddComponent<YuspecEventBridge>();

            runtime.RegisterEntity(player);
            runtime.RegisterEntity(bossRoom);
            bridge.EmitEnterZone(player, bossRoom);

            Assert.That(bossRoom.CurrentState, Is.EqualTo("Open"));

            UnityEngine.Object.DestroyImmediate(bridgeObject);
            DestroyEntity(player);
            DestroyEntity(bossRoom);
            DestroyRuntime(runtime);
        }

        [Test]
        public void TypedProperties_ActionBindingReceivesTypedArguments_AndScenarioPasses()
        {
            const string source = @"
entity Player {
}

entity Goblin {
    health: int = 30
}

on Player.Hit with Goblin:
    damage Goblin by 5

scenario ""typed damage lowers health"" {
    when Player.Hit Goblin
    expect Goblin.health == 25
}
";

            var runtime = CreateRuntime(source);
            var player = CreateEntity("Player01", "Player");
            var goblin = CreateEntity("Goblin01", "Goblin");
            runtime.RegisterEntity(player);
            runtime.RegisterEntity(goblin);

            LogAssert.Expect(LogType.Log, new Regex(@"YUSPEC damage 'Goblin01' by 5\."));
            var results = runtime.RunScenarios();

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Passed, Is.True, results[0].Message);
            Assert.That(goblin.TryGetProperty("health", out var health), Is.True);
            Assert.That(health, Is.EqualTo(25));

            DestroyEntity(player);
            DestroyEntity(goblin);
            DestroyRuntime(runtime);
        }

        [Test]
        public void StaticAnalysis_ReportsEventHandlerCycle()
        {
            const string source = @"
entity Player {
}

on Player.Start:
    emit Player.Start
";

            LogAssert.Expect(LogType.Error, new Regex(@".*\(\d+,1\): error YUSPEC: Event handler cycle detected: Player\.Start -> Player\.Start.*"));
            var runtime = CreateRuntime(source);

            Assert.That(runtime.Diagnostics.Any(d => d.code == "YSP0401"), Is.True);

            DestroyRuntime(runtime);
        }

        [Test]
        public void DialogueRuntime_StartDialogueBuiltinRaisesLineChoiceAndEnd()
        {
            const string source = @"
entity Player {
}

entity Merchant {
}

dialogue ""MerchantGreeting"" for Merchant {
    line ""Welcome, traveler.""
    choice ""Goodbye."" -> end
}

on Player.TalkTo with Merchant:
    start_dialogue ""MerchantGreeting""
";

            var runtime = CreateRuntime(source);
            var player = CreateEntity("Player01", "Player");
            var merchant = CreateEntity("Merchant01", "Merchant");
            var lines = 0;
            var choices = 0;
            var ended = false;
            runtime.DialogueRuntime.OnLine += (_, __, ___) => lines++;
            runtime.DialogueRuntime.OnChoice += (_, __, ___, ____) => choices++;
            runtime.DialogueRuntime.OnEnd += (_, __) => ended = true;

            runtime.RegisterEntity(player);
            runtime.RegisterEntity(merchant);
            runtime.Emit("Player.TalkTo", player, merchant);

            Assert.That(lines, Is.EqualTo(1));
            Assert.That(choices, Is.EqualTo(1));
            Assert.That(ended, Is.True);

            DestroyEntity(player);
            DestroyEntity(merchant);
            DestroyRuntime(runtime);
        }

#if UNITY_EDITOR
        [Test]
        public void ScriptableObjectBinding_ReadsValuesAndWritesBackWhenMutable()
        {
            const string assetPath = "Assets/YuspecPlayerConfigTest.asset";
            AssetDatabase.DeleteAsset(assetPath);
            var config = ScriptableObject.CreateInstance<PlayerConfig>();
            config.health = 42;
            config.moveSpeed = 7.5f;
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();

            const string source = @"
entity PlayerConfig from ""Assets/YuspecPlayerConfigTest.asset"" {
    health: int
    moveSpeed: float
}

on PlayerConfig.Buff:
    set PlayerConfig.health = 50
";

            var runtime = CreateRuntime(source);
            var entity = CreateEntity("PlayerConfig01", "PlayerConfig");
            runtime.RegisterEntity(entity);

            Assert.That(entity.TryGetProperty("health", out var health), Is.True);
            Assert.That(health, Is.EqualTo(42));

            runtime.Emit("PlayerConfig.Buff", entity, null);

            Assert.That(config.health, Is.EqualTo(50));

            DestroyEntity(entity);
            DestroyRuntime(runtime);
            AssetDatabase.DeleteAsset(assetPath);
        }
#endif

        private static YuspecRuntime CreateRuntime(string source)
        {
            return CreateRuntime(new TextAsset(source));
        }

        private static YuspecRuntime CreateRuntime(UnityEngine.Object spec)
        {
            var runtimeObject = new GameObject("Runtime");
            var runtime = runtimeObject.AddComponent<YuspecRuntime>();

            var field = typeof(YuspecRuntime).GetField("specs", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(runtime, new[] { spec });
            runtime.Initialize();
            return runtime;
        }

        private static YuspecEntity CreateEntity(string id, string type)
        {
            var gameObject = new GameObject(id);
            var entity = gameObject.AddComponent<YuspecEntity>();
            entity.EntityId = id;
            entity.EntityType = type;
            return entity;
        }

        private static void DestroyEntity(YuspecEntity entity)
        {
            if (entity != null)
            {
                UnityEngine.Object.DestroyImmediate(entity.gameObject);
            }
        }

        private static void DestroyRuntime(YuspecRuntime runtime)
        {
            if (runtime != null)
            {
                UnityEngine.Object.DestroyImmediate(runtime.gameObject);
            }
        }

        [YuspecMutable]
        private sealed class PlayerConfig : ScriptableObject
        {
            public int health;
            public float moveSpeed;
        }
    }
}
