using System.Collections.Generic;

namespace Yuspec.Unity
{
    public readonly struct YuspecSourceLocation
    {
        public string SourceName { get; }
        public int Line { get; }
        public int Column { get; }

        public YuspecSourceLocation(string sourceName, int line, int column)
        {
            SourceName = sourceName;
            Line = line;
            Column = column;
        }
    }

    public sealed class YuspecSyntaxTree
    {
        public string SourceName { get; }
        public List<YuspecEntitySyntax> Entities { get; } = new List<YuspecEntitySyntax>();
        public List<YuspecEventRuleSyntax> EventRules { get; } = new List<YuspecEventRuleSyntax>();
        public List<YuspecBehaviorSyntax> Behaviors { get; } = new List<YuspecBehaviorSyntax>();
        public List<YuspecScenarioSyntax> Scenarios { get; } = new List<YuspecScenarioSyntax>();
        public List<YuspecDialogueSyntax> Dialogues { get; } = new List<YuspecDialogueSyntax>();

        public YuspecSyntaxTree(string sourceName)
        {
            SourceName = sourceName;
        }
    }

    public sealed class YuspecEntitySyntax
    {
        public string EntityType { get; }
        public string SourceAssetPath { get; }
        public YuspecSourceLocation Location { get; }
        public List<YuspecPropertySyntax> Properties { get; } = new List<YuspecPropertySyntax>();

        public YuspecEntitySyntax(string entityType, string sourceAssetPath, YuspecSourceLocation location)
        {
            EntityType = entityType;
            SourceAssetPath = sourceAssetPath ?? string.Empty;
            Location = location;
        }
    }

    public sealed class YuspecPropertySyntax
    {
        public string Name { get; }
        public object Value { get; }
        public bool HasValue { get; }
        public YuspecPropertyType Type { get; }
        public YuspecSourceLocation Location { get; }

        public YuspecPropertySyntax(string name, object value, bool hasValue, YuspecPropertyType type, YuspecSourceLocation location)
        {
            Name = name;
            Value = value;
            HasValue = hasValue;
            Type = type;
            Location = location;
        }
    }

    public sealed class YuspecEventRuleSyntax
    {
        public string ActorType { get; }
        public string EventName { get; }
        public string TargetType { get; }
        public string ConditionText { get; }
        public YuspecSourceLocation Location { get; }
        public List<YuspecActionSyntax> Actions { get; } = new List<YuspecActionSyntax>();

        public YuspecEventRuleSyntax(string actorType, string eventName, string targetType, string conditionText, YuspecSourceLocation location)
        {
            ActorType = actorType;
            EventName = eventName;
            TargetType = targetType;
            ConditionText = conditionText;
            Location = location;
        }
    }

    public sealed class YuspecActionSyntax
    {
        public string Name { get; }
        public string RawText { get; }
        public YuspecSourceLocation Location { get; }
        public IReadOnlyList<string> Arguments { get; }

        public YuspecActionSyntax(string name, string rawText, IReadOnlyList<string> arguments, YuspecSourceLocation location)
        {
            Name = name;
            RawText = rawText;
            Arguments = arguments;
            Location = location;
        }
    }

    public sealed class YuspecBehaviorSyntax
    {
        public string Name { get; }
        public string EntityType { get; }
        public YuspecSourceLocation Location { get; }
        public List<YuspecStateSyntax> States { get; } = new List<YuspecStateSyntax>();

        public YuspecBehaviorSyntax(string name, string entityType, YuspecSourceLocation location)
        {
            Name = name;
            EntityType = entityType;
            Location = location;
        }
    }

    public sealed class YuspecStateSyntax
    {
        public string Name { get; }
        public YuspecSourceLocation Location { get; }
        public List<YuspecActionSyntax> EnterActions { get; } = new List<YuspecActionSyntax>();
        public List<YuspecActionSyntax> ExitActions { get; } = new List<YuspecActionSyntax>();
        public List<YuspecActionSyntax> DoActions { get; } = new List<YuspecActionSyntax>();
        public List<YuspecEverySyntax> EveryBlocks { get; } = new List<YuspecEverySyntax>();
        public List<YuspecTransitionSyntax> Transitions { get; } = new List<YuspecTransitionSyntax>();

        public YuspecStateSyntax(string name, YuspecSourceLocation location)
        {
            Name = name;
            Location = location;
        }
    }

    public sealed class YuspecEverySyntax
    {
        public string IntervalText { get; }
        public YuspecSourceLocation Location { get; }
        public List<YuspecActionSyntax> Actions { get; } = new List<YuspecActionSyntax>();

        public YuspecEverySyntax(string intervalText, YuspecSourceLocation location)
        {
            IntervalText = intervalText;
            Location = location;
        }
    }

    public sealed class YuspecTransitionSyntax
    {
        public string TriggerText { get; }
        public string TargetState { get; }
        public YuspecSourceLocation Location { get; }

        public YuspecTransitionSyntax(string triggerText, string targetState, YuspecSourceLocation location)
        {
            TriggerText = triggerText;
            TargetState = targetState;
            Location = location;
        }
    }

    public sealed class YuspecScenarioSyntax
    {
        public string Name { get; }
        public YuspecSourceLocation Location { get; }
        public List<YuspecScenarioStepSyntax> GivenSteps { get; } = new List<YuspecScenarioStepSyntax>();
        public List<YuspecScenarioStepSyntax> WhenSteps { get; } = new List<YuspecScenarioStepSyntax>();
        public List<YuspecScenarioStepSyntax> ExpectSteps { get; } = new List<YuspecScenarioStepSyntax>();

        public YuspecScenarioSyntax(string name, YuspecSourceLocation location)
        {
            Name = name;
            Location = location;
        }
    }

    public sealed class YuspecScenarioStepSyntax
    {
        public string Text { get; }
        public YuspecSourceLocation Location { get; }

        public YuspecScenarioStepSyntax(string text, YuspecSourceLocation location)
        {
            Text = text;
            Location = location;
        }
    }

    public sealed class YuspecDialogueSyntax
    {
        public string Name { get; }
        public string EntityType { get; }
        public YuspecSourceLocation Location { get; }
        public List<YuspecDialogueEntrySyntax> Entries { get; } = new List<YuspecDialogueEntrySyntax>();

        public YuspecDialogueSyntax(string name, string entityType, YuspecSourceLocation location)
        {
            Name = name;
            EntityType = entityType;
            Location = location;
        }
    }

    public sealed class YuspecDialogueEntrySyntax
    {
        public YuspecDialogueEntryKind Kind { get; }
        public string Text { get; }
        public string Target { get; }
        public YuspecSourceLocation Location { get; }

        public YuspecDialogueEntrySyntax(YuspecDialogueEntryKind kind, string text, string target, YuspecSourceLocation location)
        {
            Kind = kind;
            Text = text;
            Target = target ?? string.Empty;
            Location = location;
        }
    }
}
