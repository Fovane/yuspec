using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Yuspec.Unity
{
    public sealed class YuspecRuntime : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Object[] specs;
        [SerializeField] private bool strictMode = true;
        [SerializeField] private int maxRecentEvents = 100;

        private readonly Dictionary<string, YuspecEntity> entitiesById = new Dictionary<string, YuspecEntity>();
        private readonly List<YuspecCompiledSpec> compiledSpecs = new List<YuspecCompiledSpec>();
        private readonly List<YuspecDiagnostic> diagnostics = new List<YuspecDiagnostic>();
        private readonly List<YuspecEvent> recentEvents = new List<YuspecEvent>();
        private readonly List<string> debugTrace = new List<string>();
        private readonly YuspecActionRegistry actionRegistry = new YuspecActionRegistry();

        public IReadOnlyList<UnityEngine.Object> Specs => specs ?? Array.Empty<UnityEngine.Object>();
        public bool StrictMode => strictMode;
        public YuspecActionRegistry ActionRegistry => actionRegistry;
        public IReadOnlyList<YuspecCompiledSpec> CompiledSpecs => compiledSpecs;
        public IReadOnlyList<YuspecDiagnostic> Diagnostics => diagnostics;
        public IReadOnlyList<YuspecEvent> RecentEvents => recentEvents;
        public IReadOnlyList<string> DebugTrace => debugTrace;
        public IReadOnlyCollection<YuspecEntity> Entities => entitiesById.Values;

        private void Awake()
        {
            actionRegistry.Clear();
            actionRegistry.RegisterFromLoadedAssemblies();
            diagnostics.AddRange(actionRegistry.Diagnostics);
            LoadSpecs();
        }

        public void LoadSpecs()
        {
            compiledSpecs.Clear();
            diagnostics.RemoveAll(diagnostic => diagnostic.code.StartsWith("YSP01", StringComparison.Ordinal) || diagnostic.code.StartsWith("YSP10", StringComparison.Ordinal));

            if (specs == null)
            {
                return;
            }

            var entityTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parser = new YuspecSpecParser();
            foreach (var spec in specs)
            {
                if (spec == null)
                {
                    diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Warning, "YSP0100", "Spec slot is empty."));
                    continue;
                }

                var sourceText = GetSpecText(spec);
                if (sourceText == null)
                {
                    diagnostics.Add(new YuspecDiagnostic(
                        YuspecDiagnosticSeverity.Error,
                        "YSP0104",
                        $"Unsupported spec asset type '{spec.GetType().Name}'. Use a .yuspec asset or TextAsset.",
                        spec.name));
                    continue;
                }

                var compiledSpec = parser.Parse(spec.name, sourceText);
                compiledSpecs.Add(compiledSpec);
                diagnostics.AddRange(parser.Diagnostics);
                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Info, "YSP0101", $"Loaded spec '{spec.name}'.", spec.name));

                foreach (var declaration in compiledSpec.Entities)
                {
                    if (!entityTypes.Add(declaration.EntityType))
                    {
                        diagnostics.Add(new YuspecDiagnostic(
                            YuspecDiagnosticSeverity.Error,
                            "YSP0102",
                            $"Duplicate entity declaration '{declaration.EntityType}'.",
                            spec.name));
                    }
                }
            }

            ValidateActionBindings();
            ApplyDeclarationsToRegisteredEntities();
        }

        public void RegisterEntity(YuspecEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            var entityId = entity.EntityId;
            if (string.IsNullOrWhiteSpace(entityId))
            {
                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0200", "Entity id is empty."));
                return;
            }

            if (entitiesById.TryGetValue(entityId, out var existing) && existing != entity)
            {
                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0201", $"Duplicate entity id '{entityId}'."));
                return;
            }

            entitiesById[entityId] = entity;
            ApplyDeclaration(entity);
        }

        public void UnregisterEntity(YuspecEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            if (entitiesById.TryGetValue(entity.EntityId, out var existing) && existing == entity)
            {
                entitiesById.Remove(entity.EntityId);
            }
        }

        public void Emit(string eventName)
        {
            Emit(eventName, null, null);
        }

        public void Emit(string eventName, YuspecEntity actor)
        {
            Emit(eventName, actor, null);
        }

        public void Emit(string eventName, YuspecEntity actor, YuspecEntity target)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0300", "Event name is empty."));
                return;
            }

            var yuspecEvent = new YuspecEvent(eventName, actor, target);
            recentEvents.Add(yuspecEvent);
            AddTrace($"event {yuspecEvent}");
            if (recentEvents.Count > maxRecentEvents)
            {
                recentEvents.RemoveAt(0);
            }

            foreach (var handler in compiledSpecs.SelectMany(spec => spec.EventHandlers))
            {
                if (!Matches(handler, eventName, actor, target))
                {
                    continue;
                }

                AddTrace($"handler matched {handler.SourceName}:{handler.Line} {handler.EventName}");
                if (!EvaluateCondition(handler.Condition, actor, target))
                {
                    AddTrace($"condition failed for {handler.EventName}");
                    continue;
                }

                AddTrace($"condition passed for {handler.EventName}");
                foreach (var action in handler.Actions)
                {
                    ExecuteActionCall(action, actor, target);
                }
            }
        }

        public bool ExecuteAction(string actionName, params object[] args)
        {
            var before = actionRegistry.Diagnostics.Count;
            var result = actionRegistry.Invoke(actionName, args);

            foreach (var diagnostic in actionRegistry.Diagnostics.Skip(before))
            {
                diagnostics.Add(diagnostic);
            }

            return result;
        }

        public bool TryFindEntity(string entityId, out YuspecEntity entity)
        {
            return entitiesById.TryGetValue(entityId, out entity);
        }

        private void ValidateActionBindings()
        {
            foreach (var action in compiledSpecs.SelectMany(spec => spec.EventHandlers).SelectMany(handler => handler.Actions))
            {
                if (action.IsSetAction)
                {
                    continue;
                }

                if (!actionRegistry.TryGetAction(action.Name, out _))
                {
                    diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0103", $"Unknown action '{action.Name}'.", string.Empty, action.Line, 1));
                }
            }
        }

        private void ApplyDeclarationsToRegisteredEntities()
        {
            foreach (var entity in entitiesById.Values)
            {
                ApplyDeclaration(entity);
            }
        }

        private void ApplyDeclaration(YuspecEntity entity)
        {
            var declaration = compiledSpecs
                .SelectMany(spec => spec.Entities)
                .FirstOrDefault(candidate => string.Equals(candidate.EntityType, entity.EntityType, StringComparison.OrdinalIgnoreCase));

            if (declaration == null)
            {
                return;
            }

            foreach (var property in declaration.Properties)
            {
                if (!entity.TryGetProperty(property.Key, out _))
                {
                    entity.SetProperty(property.Key, property.Value);
                }
            }
        }

        private bool Matches(YuspecEventHandler handler, string eventName, YuspecEntity actor, YuspecEntity target)
        {
            if (!string.Equals(handler.EventName, eventName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (actor != null && !string.Equals(actor.EntityType, handler.ActorType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(handler.TargetType) ||
                   target == null ||
                   string.Equals(target.EntityType, handler.TargetType, StringComparison.OrdinalIgnoreCase);
        }

        private bool EvaluateCondition(YuspecCondition condition, YuspecEntity actor, YuspecEntity target)
        {
            if (condition == null || condition.Kind == YuspecConditionKind.None)
            {
                return true;
            }

            if (condition.Kind == YuspecConditionKind.HasValue)
            {
                var left = ResolveEntity(condition.LeftEntity, actor, target);
                var right = ResolveEntity(condition.RightEntity, actor, target);
                if (left == null || right == null)
                {
                    return false;
                }

                return right.TryGetProperty(condition.RightValue, out var requiredValue) && left.HasValue(requiredValue?.ToString());
            }

            if (condition.Kind == YuspecConditionKind.Equals)
            {
                var left = ResolveEntity(condition.LeftEntity, actor, target);
                if (left == null || !left.TryGetProperty(condition.LeftProperty, out var leftValue))
                {
                    return false;
                }

                var rightValue = ResolveValue(condition.RightValue, actor, target);
                return string.Equals(leftValue?.ToString(), rightValue?.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private void ExecuteActionCall(YuspecActionCall action, YuspecEntity actor, YuspecEntity target)
        {
            if (action.IsSetAction)
            {
                var targetEntity = ResolveEntity(action.TargetEntity, actor, target);
                if (targetEntity == null)
                {
                    diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0400", $"Unknown entity '{action.TargetEntity}' for set action.", string.Empty, action.Line, 1));
                    return;
                }

                var value = ResolveValue(action.AssignedValue, actor, target);
                targetEntity.SetProperty(action.TargetProperty, value);
                AddTrace($"action set {targetEntity.EntityId}.{action.TargetProperty} = {value}");
                return;
            }

            var args = action.Arguments.Select(argument => ResolveValue(argument, actor, target)).ToArray();
            AddTrace($"action {action.Name}({string.Join(", ", args.Select(arg => arg?.ToString() ?? "null"))})");
            ExecuteAction(action.Name, args);
        }

        private object ResolveValue(string token, YuspecEntity actor, YuspecEntity target)
        {
            token = token?.Trim() ?? string.Empty;

            if (token.Contains("."))
            {
                var parts = token.Split(new[] { '.' }, 2);
                var entity = ResolveEntity(parts[0], actor, target);
                if (entity != null && entity.TryGetProperty(parts[1], out var propertyValue))
                {
                    return propertyValue;
                }
            }

            var resolvedEntity = ResolveEntity(token, actor, target);
            if (resolvedEntity != null)
            {
                return resolvedEntity;
            }

            return YuspecSpecParser.ParseLiteral(token);
        }

        private YuspecEntity ResolveEntity(string reference, YuspecEntity actor, YuspecEntity target)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            if (actor != null &&
                (string.Equals(reference, actor.EntityType, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(reference, actor.EntityId, StringComparison.OrdinalIgnoreCase)))
            {
                return actor;
            }

            if (target != null &&
                (string.Equals(reference, target.EntityType, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(reference, target.EntityId, StringComparison.OrdinalIgnoreCase)))
            {
                return target;
            }

            if (entitiesById.TryGetValue(reference, out var byId))
            {
                return byId;
            }

            return entitiesById.Values.FirstOrDefault(entity => string.Equals(entity.EntityType, reference, StringComparison.OrdinalIgnoreCase));
        }

        private void AddTrace(string message)
        {
            debugTrace.Add(message);
            if (debugTrace.Count > maxRecentEvents)
            {
                debugTrace.RemoveAt(0);
            }
        }

        private static string GetSpecText(UnityEngine.Object spec)
        {
            if (spec is TextAsset textAsset)
            {
                return textAsset.text;
            }

            if (spec is YuspecSpecAsset specAsset)
            {
                return specAsset.SourceText;
            }

            return null;
        }
    }
}
