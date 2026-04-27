using System;
using System.Collections.Generic;

namespace Yuspec.Unity
{
    public sealed class YuspecCompiledSpec
    {
        public string SourceName { get; }
        public YuspecSyntaxTree SyntaxTree { get; }
        public List<YuspecEntityDeclaration> Entities { get; } = new List<YuspecEntityDeclaration>();
        public List<YuspecEventHandler> EventHandlers { get; } = new List<YuspecEventHandler>();
        public List<YuspecBehaviorDefinition> Behaviors { get; } = new List<YuspecBehaviorDefinition>();
        public List<YuspecScenarioDefinition> Scenarios { get; } = new List<YuspecScenarioDefinition>();
        public List<YuspecDialogueDefinition> Dialogues { get; } = new List<YuspecDialogueDefinition>();

        public YuspecCompiledSpec(string sourceName)
        {
            SourceName = sourceName;
            SyntaxTree = new YuspecSyntaxTree(sourceName);
        }
    }

    public enum YuspecTraceKind
    {
        Event,
        HandlerMatched,
        ConditionPassed,
        ConditionFailed,
        ActionExecuted,
        Diagnostic
    }

    public sealed class YuspecTraceEntry
    {
        public YuspecTraceKind Kind { get; }
        public string Message { get; }
        public string SourceName { get; }
        public int Line { get; }
        public float Time { get; }

        public YuspecTraceEntry(YuspecTraceKind kind, string message, string sourceName = "", int line = 0, float time = 0f)
        {
            Kind = kind;
            Message = message;
            SourceName = sourceName;
            Line = line;
            Time = time;
        }

        public override string ToString()
        {
            var location = string.IsNullOrEmpty(SourceName) ? string.Empty : $" {SourceName}:{Line}";
            return $"{Time:0.000} [{Kind}]{location} {Message}";
        }
    }

    public enum YuspecPropertyType
    {
        Unknown,
        Int,
        Float,
        Bool,
        String,
        StringArray
    }

    public sealed class YuspecPropertyDeclaration
    {
        public string Name { get; }
        public YuspecPropertyType Type { get; }
        public object Value { get; set; }
        public bool HasDefaultValue { get; set; }
        public string SourceName { get; }
        public int Line { get; }
        public int Column { get; }

        public YuspecPropertyDeclaration(
            string name,
            YuspecPropertyType type,
            object value,
            bool hasDefaultValue,
            string sourceName,
            int line,
            int column)
        {
            Name = name;
            Type = type;
            Value = value;
            HasDefaultValue = hasDefaultValue;
            SourceName = sourceName;
            Line = line;
            Column = column;
        }
    }

    public sealed class YuspecEntityDeclaration
    {
        public string EntityType { get; }
        public string SourceName { get; }
        public int Line { get; }
        public string ScriptableObjectPath { get; }
        public Dictionary<string, YuspecPropertyDeclaration> Properties { get; } =
            new Dictionary<string, YuspecPropertyDeclaration>(StringComparer.OrdinalIgnoreCase);

        public YuspecEntityDeclaration(string entityType, string sourceName, int line, string scriptableObjectPath = "")
        {
            EntityType = entityType;
            SourceName = sourceName;
            Line = line;
            ScriptableObjectPath = scriptableObjectPath ?? string.Empty;
        }

        public YuspecPropertyType GetPropertyType(string propertyName)
        {
            return Properties.TryGetValue(propertyName, out var property) ? property.Type : YuspecPropertyType.Unknown;
        }

        public object GetPropertyValue(string propertyName)
        {
            return Properties.TryGetValue(propertyName, out var property) ? property.Value : null;
        }
    }

    public sealed class YuspecEventHandler
    {
        public string ActorType { get; }
        public string EventName { get; }
        public string TargetType { get; }
        public YuspecCondition Condition { get; }
        public List<YuspecActionCall> Actions { get; } = new List<YuspecActionCall>();
        public string SourceName { get; }
        public int Line { get; }

        public YuspecEventHandler(
            string actorType,
            string eventName,
            string targetType,
            YuspecCondition condition,
            string sourceName,
            int line)
        {
            ActorType = actorType;
            EventName = eventName;
            TargetType = targetType;
            Condition = condition;
            SourceName = sourceName;
            Line = line;
        }
    }

    public enum YuspecConditionKind
    {
        None,
        HasValue,
        Equals
    }

    public sealed class YuspecCondition
    {
        public static readonly YuspecCondition None = new YuspecCondition(YuspecConditionKind.None, string.Empty, string.Empty, string.Empty, string.Empty);

        public YuspecConditionKind Kind { get; }
        public string LeftEntity { get; }
        public string LeftProperty { get; }
        public string RightEntity { get; }
        public string RightValue { get; }

        public YuspecCondition(
            YuspecConditionKind kind,
            string leftEntity,
            string leftProperty,
            string rightEntity,
            string rightValue)
        {
            Kind = kind;
            LeftEntity = leftEntity;
            LeftProperty = leftProperty;
            RightEntity = rightEntity;
            RightValue = rightValue;
        }
    }

    public sealed class YuspecActionCall
    {
        public string Name { get; }
        public List<string> Arguments { get; } = new List<string>();
        public string TargetEntity { get; }
        public string TargetProperty { get; }
        public string AssignedValue { get; }
        public int Line { get; }

        public bool IsSetAction => string.Equals(Name, "set", StringComparison.OrdinalIgnoreCase);

        public YuspecActionCall(string name, int line)
        {
            Name = name;
            Line = line;
        }

        public YuspecActionCall(string targetEntity, string targetProperty, string assignedValue, int line)
            : this("set", line)
        {
            TargetEntity = targetEntity;
            TargetProperty = targetProperty;
            AssignedValue = assignedValue;
        }
    }

    public sealed class YuspecBehaviorDefinition
    {
        public string Name { get; }
        public string EntityType { get; }
        public string SourceName { get; }
        public int Line { get; }
        public List<YuspecStateDefinition> States { get; } = new List<YuspecStateDefinition>();

        public YuspecBehaviorDefinition(string name, string entityType, string sourceName, int line)
        {
            Name = name;
            EntityType = entityType;
            SourceName = sourceName;
            Line = line;
        }
    }

    public sealed class YuspecStateDefinition
    {
        public string Name { get; }
        public string SourceName { get; }
        public int Line { get; }
        public List<YuspecActionCall> EnterActions { get; } = new List<YuspecActionCall>();
        public List<YuspecActionCall> ExitActions { get; } = new List<YuspecActionCall>();
        public List<YuspecActionCall> DoActions { get; } = new List<YuspecActionCall>();
        public List<YuspecTimedActionBlock> EveryBlocks { get; } = new List<YuspecTimedActionBlock>();
        public List<YuspecTransitionDefinition> Transitions { get; } = new List<YuspecTransitionDefinition>();

        public YuspecStateDefinition(string name, string sourceName, int line)
        {
            Name = name;
            SourceName = sourceName;
            Line = line;
        }
    }

    public sealed class YuspecTimedActionBlock
    {
        public string IntervalText { get; }
        public string SourceName { get; }
        public int Line { get; }
        public List<YuspecActionCall> Actions { get; } = new List<YuspecActionCall>();

        public YuspecTimedActionBlock(string intervalText, string sourceName, int line)
        {
            IntervalText = intervalText;
            SourceName = sourceName;
            Line = line;
        }
    }

    public sealed class YuspecTransitionDefinition
    {
        public string TriggerText { get; }
        public string TargetState { get; }
        public string SourceName { get; }
        public int Line { get; }

        public YuspecTransitionDefinition(string triggerText, string targetState, string sourceName, int line)
        {
            TriggerText = triggerText;
            TargetState = targetState;
            SourceName = sourceName;
            Line = line;
        }
    }

    public sealed class YuspecScenarioDefinition
    {
        public string Name { get; }
        public int Line { get; }
        public List<YuspecScenarioStepDefinition> GivenSteps { get; } = new List<YuspecScenarioStepDefinition>();
        public List<YuspecScenarioStepDefinition> WhenSteps { get; } = new List<YuspecScenarioStepDefinition>();
        public List<YuspecScenarioStepDefinition> ExpectSteps { get; } = new List<YuspecScenarioStepDefinition>();

        public YuspecScenarioDefinition(string name, int line)
        {
            Name = name;
            Line = line;
        }
    }

    public sealed class YuspecScenarioStepDefinition
    {
        public string Text { get; }
        public int Line { get; }

        public YuspecScenarioStepDefinition(string text, int line)
        {
            Text = text;
            Line = line;
        }
    }

    public enum YuspecDialogueEntryKind
    {
        Line,
        Choice
    }

    public sealed class YuspecDialogueEntry
    {
        public YuspecDialogueEntryKind Kind { get; }
        public string Text { get; }
        public string Target { get; }
        public int Line { get; }

        public YuspecDialogueEntry(YuspecDialogueEntryKind kind, string text, string target, int line)
        {
            Kind = kind;
            Text = text;
            Target = target ?? string.Empty;
            Line = line;
        }
    }

    public sealed class YuspecDialogueDefinition
    {
        public string Name { get; }
        public string EntityType { get; }
        public string SourceName { get; }
        public int Line { get; }
        public List<YuspecDialogueEntry> Entries { get; } = new List<YuspecDialogueEntry>();

        public YuspecDialogueDefinition(string name, string entityType, string sourceName, int line)
        {
            Name = name;
            EntityType = entityType;
            SourceName = sourceName;
            Line = line;
        }
    }
}
