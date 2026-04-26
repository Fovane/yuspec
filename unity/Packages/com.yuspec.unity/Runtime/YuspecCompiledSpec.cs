using System.Collections.Generic;

namespace Yuspec.Unity
{
    public sealed class YuspecCompiledSpec
    {
        public string SourceName { get; }
        public List<YuspecEntityDeclaration> Entities { get; } = new List<YuspecEntityDeclaration>();
        public List<YuspecEventHandler> EventHandlers { get; } = new List<YuspecEventHandler>();

        public YuspecCompiledSpec(string sourceName)
        {
            SourceName = sourceName;
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

    public sealed class YuspecEntityDeclaration
    {
        public string EntityType { get; }
        public Dictionary<string, object> Properties { get; } = new Dictionary<string, object>();

        public YuspecEntityDeclaration(string entityType)
        {
            EntityType = entityType;
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

        public bool IsSetAction => string.Equals(Name, "set", System.StringComparison.OrdinalIgnoreCase);

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
}
