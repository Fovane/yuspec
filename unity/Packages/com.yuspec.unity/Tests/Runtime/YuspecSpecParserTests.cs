using System.Linq;
using NUnit.Framework;

namespace Yuspec.Unity.Tests
{
    public sealed class YuspecSpecParserTests
    {
        [Test]
        public void Parse_EventRuleAndEntity_BuildsCompiledAndSyntaxTrees()
        {
            const string source = @"
entity Door {
    state = Closed
    key = \"IronKey\"
}

on Player.Interact with Door when Player.has(Door.key):
    set Door.state = Open
    play_sound \"door_open\"
";

            var parser = new YuspecSpecParser();
            var spec = parser.Parse("DoorSpec", source);

            Assert.That(parser.Diagnostics.Any(d => d.code == "YSP1001"), Is.False);
            Assert.That(spec.Entities.Count, Is.EqualTo(1));
            Assert.That(spec.EventHandlers.Count, Is.EqualTo(1));

            Assert.That(spec.SyntaxTree.Entities.Count, Is.EqualTo(1));
            Assert.That(spec.SyntaxTree.EventRules.Count, Is.EqualTo(1));
            Assert.That(spec.SyntaxTree.Entities[0].Location.Line, Is.EqualTo(2));
            Assert.That(spec.SyntaxTree.EventRules[0].Actions.Count, Is.EqualTo(2));
        }

        [Test]
        public void Parse_BehaviorStateMachine_BuildsBehaviorSyntax()
        {
            const string source = @"
behavior GoblinAI for Goblin {
    state Idle {
        on enter:
            play_animation self \"Idle\"

        on PlayerSeen -> Chase
    }

    state Chase {
        every 0.25s:
            move_towards Player speed 3

        on PlayerLost -> Idle
    }
}
";

            var parser = new YuspecSpecParser();
            var spec = parser.Parse("GoblinSpec", source);

            Assert.That(spec.SyntaxTree.Behaviors.Count, Is.EqualTo(1));
            var behavior = spec.SyntaxTree.Behaviors[0];
            Assert.That(behavior.Name, Is.EqualTo("GoblinAI"));
            Assert.That(behavior.EntityType, Is.EqualTo("Goblin"));
            Assert.That(behavior.States.Count, Is.EqualTo(2));
            Assert.That(behavior.States[0].EnterActions.Count, Is.EqualTo(1));
            Assert.That(behavior.States[0].Transitions.Count, Is.EqualTo(1));
            Assert.That(behavior.States[1].EveryBlocks.Count, Is.EqualTo(1));
            Assert.That(behavior.States[1].EveryBlocks[0].IntervalText, Is.EqualTo("0.25s"));
            Assert.That(parser.Diagnostics.Any(d => d.code == "YSP1001"), Is.False);
        }

        [Test]
        public void Parse_ScenarioBlock_BuildsScenarioSyntax()
        {
            const string source = @"
scenario \"door opens with key\" {
    given Player has \"IronKey\"
    when Player.Interact Door
    expect Door.state == Open
}
";

            var parser = new YuspecSpecParser();
            var spec = parser.Parse("ScenarioSpec", source);

            Assert.That(spec.SyntaxTree.Scenarios.Count, Is.EqualTo(1));
            var scenario = spec.SyntaxTree.Scenarios[0];
            Assert.That(scenario.Name, Is.EqualTo("door opens with key"));
            Assert.That(scenario.GivenSteps.Count, Is.EqualTo(1));
            Assert.That(scenario.WhenSteps.Count, Is.EqualTo(1));
            Assert.That(scenario.ExpectSteps.Count, Is.EqualTo(1));
        }

        [Test]
        public void Parse_Comments_AreIgnoredOutsideStrings()
        {
            const string source = @"
# full-line comment
entity Door {
    key = \"Iron#Key\" # inline hash comment
    state = Closed // inline slash comment
}
";

            var parser = new YuspecSpecParser();
            var spec = parser.Parse("CommentSpec", source);

            Assert.That(spec.Entities.Count, Is.EqualTo(1));
            var keyValue = spec.Entities[0].Properties["key"];
            Assert.That(keyValue, Is.EqualTo("Iron#Key"));
            Assert.That(parser.Diagnostics.Any(d => d.severity == YuspecDiagnosticSeverity.Error), Is.False);
        }
    }
}
