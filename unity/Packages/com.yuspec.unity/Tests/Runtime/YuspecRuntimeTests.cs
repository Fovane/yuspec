using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

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

        private static YuspecRuntime CreateRuntime(string source)
        {
            var runtimeObject = new GameObject("Runtime");
            var runtime = runtimeObject.AddComponent<YuspecRuntime>();
            var textAsset = new TextAsset(source);

            var field = typeof(YuspecRuntime).GetField("specs", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(runtime, new UnityEngine.Object[] { textAsset });
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
    }
}
