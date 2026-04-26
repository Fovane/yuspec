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
        private readonly List<YuspecTraceEntry> traceEntries = new List<YuspecTraceEntry>();
        private readonly YuspecActionRegistry actionRegistry = new YuspecActionRegistry();

        public IReadOnlyList<UnityEngine.Object> Specs => specs ?? Array.Empty<UnityEngine.Object>();
        public bool StrictMode => strictMode;
        public YuspecActionRegistry ActionRegistry => actionRegistry;
        public IReadOnlyList<YuspecCompiledSpec> CompiledSpecs => compiledSpecs;
        public IReadOnlyList<YuspecDiagnostic> Diagnostics => diagnostics;
        public IReadOnlyList<YuspecEvent> RecentEvents => recentEvents;
        public IReadOnlyList<string> DebugTrace => debugTrace;
        public IReadOnlyList<YuspecTraceEntry> TraceEntries => traceEntries;
        public IReadOnlyCollection<YuspecEntity> Entities => entitiesById.Values;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            actionRegistry.Clear();
            diagnostics.Clear();
            recentEvents.Clear();
            debugTrace.Clear();
            traceEntries.Clear();
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
            ValidateStrictReferences();
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
            AddTrace(YuspecTraceKind.Event, yuspecEvent.ToString());
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

                AddTrace(YuspecTraceKind.HandlerMatched, $"handler matched {handler.EventName}", handler.SourceName, handler.Line);
                if (!EvaluateCondition(handler.Condition, actor, target))
                {
                    AddTrace(YuspecTraceKind.ConditionFailed, $"condition failed for {handler.EventName}", handler.SourceName, handler.Line);
                    continue;
                }

                AddTrace(YuspecTraceKind.ConditionPassed, $"condition passed for {handler.EventName}", handler.SourceName, handler.Line);
                foreach (var action in handler.Actions)
                {
                    ExecuteActionCall(action, actor, target, handler);
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

        public void ClearTrace()
        {
            recentEvents.Clear();
            debugTrace.Clear();
            traceEntries.Clear();
        }

        private void ValidateActionBindings()
        {
            foreach (var action in compiledSpecs.SelectMany(spec => spec.EventHandlers).SelectMany(handler => handler.Actions))
            {
                if (action.IsSetAction)
                {
                    continue;
                }

                if (!actionRegistry.TryGetAction(action.Name, out var binding))
                {
                    diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0103", $"Unknown action '{action.Name}'.", string.Empty, action.Line, 1));
                    continue;
                }

                if (binding.ParameterTypes.Length != action.Arguments.Count)
                {
                    diagnostics.Add(new YuspecDiagnostic(
                        YuspecDiagnosticSeverity.Error,
                        "YSP0105",
                        $"Action '{action.Name}' expects {binding.ParameterTypes.Length} argument(s), got {action.Arguments.Count}.",
                        string.Empty,
                        action.Line,
                        1));
                }
            }
        }

        private void ValidateStrictReferences()
        {
            if (!strictMode)
            {
                return;
            }

            var declarations = compiledSpecs
                .SelectMany(spec => spec.Entities)
                .GroupBy(entity => entity.EntityType, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var handler in compiledSpecs.SelectMany(spec => spec.EventHandlers))
            {
                RequireEntityDeclaration(declarations, handler.ActorType, handler.SourceName, handler.Line, "handler actor");
                if (!string.IsNullOrWhiteSpace(handler.TargetType))
                {
                    RequireEntityDeclaration(declarations, handler.TargetType, handler.SourceName, handler.Line, "handler target");
                }

                ValidateConditionReferences(declarations, handler);
                foreach (var action in handler.Actions)
                {
                    ValidateActionReferences(declarations, handler, action);
                }
            }
        }

        private void ValidateConditionReferences(Dictionary<string, YuspecEntityDeclaration> declarations, YuspecEventHandler handler)
        {
            var condition = handler.Condition;
            if (condition == null || condition.Kind == YuspecConditionKind.None)
            {
                return;
            }

            if (condition.Kind == YuspecConditionKind.HasValue)
            {
                RequireEntityDeclaration(declarations, condition.LeftEntity, handler.SourceName, handler.Line, "condition entity");
                RequireEntityDeclaration(declarations, condition.RightEntity, handler.SourceName, handler.Line, "condition entity");
                RequirePropertyDeclaration(declarations, condition.RightEntity, condition.RightValue, handler.SourceName, handler.Line);
                return;
            }

            if (condition.Kind == YuspecConditionKind.Equals)
            {
                RequireEntityDeclaration(declarations, condition.LeftEntity, handler.SourceName, handler.Line, "condition entity");
                RequirePropertyDeclaration(declarations, condition.LeftEntity, condition.LeftProperty, handler.SourceName, handler.Line);
                ValidateValueReference(declarations, condition.RightValue, handler.SourceName, handler.Line);
            }
        }

        private void ValidateActionReferences(Dictionary<string, YuspecEntityDeclaration> declarations, YuspecEventHandler handler, YuspecActionCall action)
        {
            if (action.IsSetAction)
            {
                RequireEntityDeclaration(declarations, action.TargetEntity, handler.SourceName, action.Line, "action target");
                RequirePropertyDeclaration(declarations, action.TargetEntity, action.TargetProperty, handler.SourceName, action.Line);
                ValidateValueReference(declarations, action.AssignedValue, handler.SourceName, action.Line);
                return;
            }

            foreach (var argument in action.Arguments)
            {
                ValidateValueReference(declarations, argument, handler.SourceName, action.Line);
            }
        }

        private void ValidateValueReference(Dictionary<string, YuspecEntityDeclaration> declarations, string token, string sourceName, int line)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            var parsed = YuspecSpecParser.ParseLiteral(token);
            if (!ReferenceEquals(parsed, token))
            {
                return;
            }

            if (token.Contains("."))
            {
                var parts = token.Split(new[] { '.' }, 2);
                RequireEntityDeclaration(declarations, parts[0], sourceName, line, "value entity");
                RequirePropertyDeclaration(declarations, parts[0], parts[1], sourceName, line);
                return;
            }

            if (declarations.ContainsKey(token))
            {
                return;
            }
        }

        private void RequireEntityDeclaration(
            Dictionary<string, YuspecEntityDeclaration> declarations,
            string entityType,
            string sourceName,
            int line,
            string usage)
        {
            if (string.IsNullOrWhiteSpace(entityType) || declarations.ContainsKey(entityType))
            {
                return;
            }

            diagnostics.Add(new YuspecDiagnostic(
                YuspecDiagnosticSeverity.Error,
                "YSP0110",
                $"Unknown entity '{entityType}' used as {usage}.",
                sourceName,
                line,
                1));
        }

        private void RequirePropertyDeclaration(
            Dictionary<string, YuspecEntityDeclaration> declarations,
            string entityType,
            string propertyName,
            string sourceName,
            int line)
        {
            if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(propertyName))
            {
                return;
            }

            if (!declarations.TryGetValue(entityType, out var declaration))
            {
                return;
            }

            if (declaration.Properties.ContainsKey(propertyName) ||
                string.Equals(propertyName, "id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propertyName, "type", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propertyName, "state", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            diagnostics.Add(new YuspecDiagnostic(
                YuspecDiagnosticSeverity.Error,
                "YSP0111",
                $"Unknown property '{entityType}.{propertyName}'.",
                sourceName,
                line,
                1));
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
                if (string.Equals(property.Key, "state", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(entity.CurrentState))
                    {
                        entity.SetProperty(property.Key, property.Value);
                    }

                    continue;
                }

                if (!entity.Properties.ContainsKey(property.Key))
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

        private void ExecuteActionCall(YuspecActionCall action, YuspecEntity actor, YuspecEntity target, YuspecEventHandler handler)
        {
            if (action.IsSetAction)
            {
                var targetEntity = ResolveEntity(action.TargetEntity, actor, target);
                if (targetEntity == null)
                {
                    AddRuntimeDiagnostic("YSP0400", $"Unknown entity '{action.TargetEntity}' for set action.", handler.SourceName, action.Line);
                    return;
                }

                var value = ResolveValue(action.AssignedValue, actor, target);
                targetEntity.SetProperty(action.TargetProperty, value);
                AddTrace(YuspecTraceKind.ActionExecuted, $"set {targetEntity.EntityId}.{action.TargetProperty} = {value}", handler.SourceName, action.Line);
                return;
            }

            var args = action.Arguments.Select(argument => ResolveValue(argument, actor, target)).ToArray();
            AddTrace(YuspecTraceKind.ActionExecuted, $"{action.Name}({string.Join(", ", args.Select(arg => arg?.ToString() ?? "null"))})", handler.SourceName, action.Line);
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

        private void AddTrace(YuspecTraceKind kind, string message, string sourceName = "", int line = 0)
        {
            var time = Application.isPlaying ? Time.time : 0f;
            var entry = new YuspecTraceEntry(kind, message, sourceName, line, time);
            traceEntries.Add(entry);
            debugTrace.Add(entry.ToString());
            if (traceEntries.Count > maxRecentEvents)
            {
                traceEntries.RemoveAt(0);
            }

            if (debugTrace.Count > maxRecentEvents)
            {
                debugTrace.RemoveAt(0);
            }
        }

        private void AddRuntimeDiagnostic(string code, string message, string sourceName, int line)
        {
            var diagnostic = new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, code, message, sourceName, line, 1);
            diagnostics.Add(diagnostic);
            AddTrace(YuspecTraceKind.Diagnostic, diagnostic.ToString(), sourceName, line);
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
