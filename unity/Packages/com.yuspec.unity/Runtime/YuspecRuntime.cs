using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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
        private readonly List<YuspecStateMachineStatus> stateMachineStatuses = new List<YuspecStateMachineStatus>();
        private readonly List<YuspecScenarioResult> scenarioResults = new List<YuspecScenarioResult>();
        private readonly List<StateMachineSession> stateMachineSessions = new List<StateMachineSession>();
        private readonly YuspecActionRegistry actionRegistry = new YuspecActionRegistry();

        private static readonly Regex ExpectEquals = new Regex(
            @"^([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)\s*==\s*(.+)$",
            RegexOptions.Compiled);
        private static readonly Regex ScenarioWhenEvent = new Regex(
            @"^([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)(?:\s+([A-Za-z_][A-Za-z0-9_]*))?$",
            RegexOptions.Compiled);

        public IReadOnlyList<UnityEngine.Object> Specs => specs ?? Array.Empty<UnityEngine.Object>();
        public bool StrictMode => strictMode;
        public YuspecActionRegistry ActionRegistry => actionRegistry;
        public IReadOnlyList<YuspecCompiledSpec> CompiledSpecs => compiledSpecs;
        public IReadOnlyList<YuspecDiagnostic> Diagnostics => diagnostics;
        public IReadOnlyList<YuspecEvent> RecentEvents => recentEvents;
        public IReadOnlyList<string> DebugTrace => debugTrace;
        public IReadOnlyList<YuspecTraceEntry> TraceEntries => traceEntries;
        public IReadOnlyCollection<YuspecEntity> Entities => entitiesById.Values;
        public IReadOnlyList<YuspecStateMachineStatus> StateMachineStatuses => stateMachineStatuses;
        public IReadOnlyList<YuspecScenarioResult> ScenarioResults => scenarioResults;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            TickStateMachines(Time.deltaTime);
        }

        public void Initialize()
        {
            actionRegistry.Clear();
            diagnostics.Clear();
            recentEvents.Clear();
            debugTrace.Clear();
            traceEntries.Clear();
            scenarioResults.Clear();
            stateMachineStatuses.Clear();
            stateMachineSessions.Clear();
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
            BuildStateMachineSessions();
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
            BuildStateMachineSessions();
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
                BuildStateMachineSessions();
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

            EvaluateEventDrivenTransitions(eventName, actor, target);
        }

        public void TickStateMachines(float deltaTime)
        {
            if (stateMachineSessions.Count == 0)
            {
                return;
            }

            foreach (var session in stateMachineSessions)
            {
                session.StateElapsed += Mathf.Max(0f, deltaTime);

                if (session.CurrentState == null)
                {
                    continue;
                }

                foreach (var everyBlock in session.CurrentState.EveryBlocks)
                {
                    if (!TryParseIntervalSeconds(everyBlock.IntervalText, out var intervalSeconds) || intervalSeconds <= 0f)
                    {
                        continue;
                    }

                    session.EveryTimers.TryGetValue(everyBlock, out var elapsed);
                    elapsed += Mathf.Max(0f, deltaTime);
                    if (elapsed < intervalSeconds)
                    {
                        session.EveryTimers[everyBlock] = elapsed;
                        continue;
                    }

                    session.EveryTimers[everyBlock] = 0f;
                    ExecuteActions(everyBlock.Actions, session.Entity, session.Entity, session.Behavior.Name, everyBlock.Line);
                }

                session.Status.StateElapsed = session.StateElapsed;
            }
        }

        public IReadOnlyList<YuspecScenarioResult> RunScenarios()
        {
            scenarioResults.Clear();

            foreach (var scenario in compiledSpecs.SelectMany(spec => spec.Scenarios))
            {
                scenarioResults.Add(RunScenario(scenario));
            }

            return scenarioResults;
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
            foreach (var action in EnumerateAllActions())
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
                    continue;
                }

                for (var index = 0; index < action.Arguments.Count; index++)
                {
                    var inferred = ResolvePotentialType(action.Arguments[index]);
                    if (inferred == null)
                    {
                        continue;
                    }

                    var required = binding.ParameterTypes[index];
                    if (!IsTypeCompatible(required, inferred))
                    {
                        diagnostics.Add(new YuspecDiagnostic(
                            YuspecDiagnosticSeverity.Error,
                            "YSP0106",
                            $"Action '{action.Name}' argument {index + 1} type mismatch: expected {required.Name}, got {inferred.Name}.",
                            string.Empty,
                            action.Line,
                            1));
                    }
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

            ValidateDuplicateHandlers();
            ValidateBehaviors(declarations);
            ValidateScenarios(declarations);
        }

        private IEnumerable<YuspecActionCall> EnumerateAllActions()
        {
            foreach (var handlerAction in compiledSpecs.SelectMany(spec => spec.EventHandlers).SelectMany(handler => handler.Actions))
            {
                yield return handlerAction;
            }

            foreach (var behavior in compiledSpecs.SelectMany(spec => spec.Behaviors))
            {
                foreach (var state in behavior.States)
                {
                    foreach (var action in state.EnterActions)
                    {
                        yield return action;
                    }

                    foreach (var action in state.ExitActions)
                    {
                        yield return action;
                    }

                    foreach (var action in state.DoActions)
                    {
                        yield return action;
                    }

                    foreach (var action in state.EveryBlocks.SelectMany(block => block.Actions))
                    {
                        yield return action;
                    }
                }
            }
        }

        private void ValidateDuplicateHandlers()
        {
            var duplicates = compiledSpecs
                .SelectMany(spec => spec.EventHandlers)
                .GroupBy(handler => $"{handler.EventName}|{handler.TargetType}|{handler.Condition?.Kind}|{handler.Condition?.LeftEntity}|{handler.Condition?.LeftProperty}|{handler.Condition?.RightEntity}|{handler.Condition?.RightValue}", StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .SelectMany(group => group.Skip(1));

            foreach (var duplicate in duplicates)
            {
                diagnostics.Add(new YuspecDiagnostic(
                    YuspecDiagnosticSeverity.Error,
                    "YSP0112",
                    $"Duplicate event handler '{duplicate.EventName}'.",
                    duplicate.SourceName,
                    duplicate.Line,
                    1));
            }
        }

        private void ValidateBehaviors(Dictionary<string, YuspecEntityDeclaration> declarations)
        {
            foreach (var behavior in compiledSpecs.SelectMany(spec => spec.Behaviors))
            {
                RequireEntityDeclaration(declarations, behavior.EntityType, string.Empty, behavior.Line, "behavior entity type");
                if (behavior.States.Count == 0)
                {
                    diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0113", $"Behavior '{behavior.Name}' has no states.", string.Empty, behavior.Line, 1));
                    continue;
                }

                var duplicateStates = behavior.States
                    .GroupBy(state => state.Name, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1)
                    .SelectMany(group => group.Skip(1));

                foreach (var duplicate in duplicateStates)
                {
                    diagnostics.Add(new YuspecDiagnostic(
                        YuspecDiagnosticSeverity.Error,
                        "YSP0114",
                        $"Duplicate state '{duplicate.Name}' in behavior '{behavior.Name}'.",
                        string.Empty,
                        duplicate.Line,
                        1));
                }

                var stateNames = new HashSet<string>(behavior.States.Select(state => state.Name), StringComparer.OrdinalIgnoreCase);
                foreach (var state in behavior.States)
                {
                    foreach (var transition in state.Transitions)
                    {
                        if (!stateNames.Contains(transition.TargetState))
                        {
                            diagnostics.Add(new YuspecDiagnostic(
                                YuspecDiagnosticSeverity.Error,
                                "YSP0115",
                                $"Unknown transition target '{transition.TargetState}' in behavior '{behavior.Name}'.",
                                string.Empty,
                                transition.Line,
                                1));
                        }
                    }

                    foreach (var block in state.EveryBlocks)
                    {
                        if (!TryParseIntervalSeconds(block.IntervalText, out var interval) || interval <= 0f)
                        {
                            diagnostics.Add(new YuspecDiagnostic(
                                YuspecDiagnosticSeverity.Error,
                                "YSP0116",
                                $"Invalid interval '{block.IntervalText}' in state '{state.Name}'.",
                                string.Empty,
                                block.Line,
                                1));
                        }
                    }
                }

                var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var queue = new Queue<string>();
                queue.Enqueue(behavior.States[0].Name);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (!reachable.Add(current))
                    {
                        continue;
                    }

                    var currentState = behavior.States.FirstOrDefault(state => string.Equals(state.Name, current, StringComparison.OrdinalIgnoreCase));
                    if (currentState == null)
                    {
                        continue;
                    }

                    foreach (var next in currentState.Transitions.Select(transition => transition.TargetState))
                    {
                        if (!reachable.Contains(next))
                        {
                            queue.Enqueue(next);
                        }
                    }
                }

                foreach (var state in behavior.States.Where(state => !reachable.Contains(state.Name)))
                {
                    diagnostics.Add(new YuspecDiagnostic(
                        YuspecDiagnosticSeverity.Warning,
                        "YSP0117",
                        $"State '{state.Name}' in behavior '{behavior.Name}' is unreachable from initial state '{behavior.States[0].Name}'.",
                        string.Empty,
                        state.Line,
                        1));
                }
            }
        }

        private void ValidateScenarios(Dictionary<string, YuspecEntityDeclaration> declarations)
        {
            foreach (var scenario in compiledSpecs.SelectMany(spec => spec.Scenarios))
            {
                foreach (var step in scenario.GivenSteps)
                {
                    ValidateScenarioEntityReference(declarations, step.Text, scenario.Name, step.Line);
                }

                foreach (var step in scenario.WhenSteps)
                {
                    ValidateScenarioEntityReference(declarations, step.Text, scenario.Name, step.Line);
                }

                foreach (var step in scenario.ExpectSteps)
                {
                    ValidateScenarioEntityReference(declarations, step.Text, scenario.Name, step.Line);
                }
            }
        }

        private void ValidateScenarioEntityReference(Dictionary<string, YuspecEntityDeclaration> declarations, string stepText, string scenarioName, int line)
        {
            if (string.IsNullOrWhiteSpace(stepText))
            {
                return;
            }

            var entityToken = stepText.Split(new[] { ' ', '.' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(entityToken))
            {
                return;
            }

            if (declarations.ContainsKey(entityToken))
            {
                return;
            }

            diagnostics.Add(new YuspecDiagnostic(
                YuspecDiagnosticSeverity.Error,
                "YSP0118",
                $"Scenario '{scenarioName}' references unknown entity '{entityToken}'.",
                string.Empty,
                line,
                1));
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

            if (string.Equals(reference, "self", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(reference, "actor", StringComparison.OrdinalIgnoreCase))
            {
                return actor;
            }

            if (string.Equals(reference, "target", StringComparison.OrdinalIgnoreCase))
            {
                return target;
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

        private static Type ResolvePotentialType(string token)
        {
            var value = YuspecSpecParser.ParseLiteral(token);
            return value?.GetType();
        }

        private static bool IsTypeCompatible(Type expectedType, Type actualType)
        {
            if (expectedType.IsAssignableFrom(actualType))
            {
                return true;
            }

            var normalizedExpected = Nullable.GetUnderlyingType(expectedType) ?? expectedType;

            if (normalizedExpected == typeof(string))
            {
                return true;
            }

            if (normalizedExpected == typeof(float) && (actualType == typeof(int) || actualType == typeof(double) || actualType == typeof(float)))
            {
                return true;
            }

            if (normalizedExpected == typeof(int) && actualType == typeof(int))
            {
                return true;
            }

            if (normalizedExpected == typeof(bool) && actualType == typeof(bool))
            {
                return true;
            }

            return false;
        }

        private void BuildStateMachineSessions()
        {
            stateMachineSessions.Clear();
            stateMachineStatuses.Clear();

            var behaviors = compiledSpecs.SelectMany(spec => spec.Behaviors).ToList();
            foreach (var entity in entitiesById.Values)
            {
                foreach (var behavior in behaviors.Where(behavior => string.Equals(behavior.EntityType, entity.EntityType, StringComparison.OrdinalIgnoreCase)))
                {
                    var initial = behavior.States.FirstOrDefault();
                    if (initial == null)
                    {
                        continue;
                    }

                    var session = new StateMachineSession(behavior, entity, initial);
                    stateMachineSessions.Add(session);
                    stateMachineStatuses.Add(session.Status);

                    entity.CurrentState = initial.Name;
                    ExecuteActions(initial.EnterActions, entity, entity, behavior.Name, initial.Line);
                }
            }
        }

        private void EvaluateEventDrivenTransitions(string eventName, YuspecEntity actor, YuspecEntity target)
        {
            foreach (var session in stateMachineSessions)
            {
                if (session.CurrentState == null)
                {
                    continue;
                }

                foreach (var transition in session.CurrentState.Transitions)
                {
                    if (!IsTransitionTriggered(transition.TriggerText, session, eventName, actor, target))
                    {
                        continue;
                    }

                    TransitionTo(session, transition.TargetState, transition.Line);
                    break;
                }
            }
        }

        private bool IsTransitionTriggered(string triggerText, StateMachineSession session, string eventName, YuspecEntity actor, YuspecEntity target)
        {
            if (string.IsNullOrWhiteSpace(triggerText))
            {
                return false;
            }

            var normalized = triggerText.Trim();

            if (TryEvaluateSelfComparison(normalized, session.Entity))
            {
                return true;
            }

            if (string.Equals(normalized, eventName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var eventSuffix = eventName?.Split('.').LastOrDefault();
            if (!string.IsNullOrWhiteSpace(eventSuffix) && string.Equals(normalized, eventSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (actor != null && actor == session.Entity && !string.IsNullOrWhiteSpace(eventSuffix) && string.Equals(normalized, eventSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool TryEvaluateSelfComparison(string text, YuspecEntity entity)
        {
            if (entity == null || !text.StartsWith("self.", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var operators = new[] { "<=", ">=", "==", "!=", "<", ">" };
            foreach (var op in operators)
            {
                var marker = $" {op} ";
                var opIndex = text.IndexOf(marker, StringComparison.Ordinal);
                if (opIndex <= 0)
                {
                    continue;
                }

                var left = text.Substring(0, opIndex).Trim();
                var right = text.Substring(opIndex + marker.Length).Trim();
                var property = left.Substring("self.".Length);
                if (!entity.TryGetProperty(property, out var leftValue))
                {
                    return false;
                }

                var rightValue = YuspecSpecParser.ParseLiteral(right);
                return CompareValues(leftValue, rightValue, op);
            }

            return false;
        }

        private static bool CompareValues(object left, object right, string op)
        {
            if (double.TryParse(left?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var leftNumber) &&
                double.TryParse(right?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rightNumber))
            {
                switch (op)
                {
                    case "<=": return leftNumber <= rightNumber;
                    case ">=": return leftNumber >= rightNumber;
                    case "<": return leftNumber < rightNumber;
                    case ">": return leftNumber > rightNumber;
                    case "==": return Math.Abs(leftNumber - rightNumber) < 0.0001;
                    case "!=": return Math.Abs(leftNumber - rightNumber) >= 0.0001;
                }
            }

            var leftText = left?.ToString() ?? string.Empty;
            var rightText = right?.ToString() ?? string.Empty;
            switch (op)
            {
                case "==": return string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase);
                case "!=": return !string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase);
                default: return false;
            }
        }

        private void TransitionTo(StateMachineSession session, string targetStateName, int line)
        {
            var targetState = session.Behavior.States.FirstOrDefault(state => string.Equals(state.Name, targetStateName, StringComparison.OrdinalIgnoreCase));
            if (targetState == null)
            {
                AddRuntimeDiagnostic("YSP0500", $"Unknown transition target '{targetStateName}' in behavior '{session.Behavior.Name}'.", string.Empty, line);
                return;
            }

            ExecuteActions(session.CurrentState.ExitActions, session.Entity, session.Entity, session.Behavior.Name, line);
            session.CurrentState = targetState;
            session.StateElapsed = 0f;
            session.EveryTimers.Clear();
            session.LastTransitionTime = Application.isPlaying ? Time.time : 0f;
            session.Entity.CurrentState = targetState.Name;

            session.Status.CurrentState = targetState.Name;
            session.Status.StateElapsed = 0f;
            session.Status.LastTransitionTime = session.LastTransitionTime;

            AddTrace(YuspecTraceKind.ActionExecuted, $"state transition {session.Entity.EntityId}: {targetState.Name}", string.Empty, line);
            ExecuteActions(targetState.EnterActions, session.Entity, session.Entity, session.Behavior.Name, line);
        }

        private void ExecuteActions(IEnumerable<YuspecActionCall> actions, YuspecEntity actor, YuspecEntity target, string sourceName, int line)
        {
            foreach (var action in actions)
            {
                var actorType = actor != null ? actor.EntityType : string.Empty;
                var targetType = target != null ? target.EntityType : string.Empty;
                ExecuteActionCall(action, actor, target, new YuspecEventHandler(actorType, sourceName, targetType, YuspecCondition.None, sourceName, line));
            }
        }

        private static bool TryParseIntervalSeconds(string intervalText, out float seconds)
        {
            seconds = 0f;
            if (string.IsNullOrWhiteSpace(intervalText))
            {
                return false;
            }

            var normalized = intervalText.Trim().ToLowerInvariant();
            if (normalized.EndsWith("ms", StringComparison.Ordinal))
            {
                if (float.TryParse(normalized.Substring(0, normalized.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds))
                {
                    seconds = milliseconds / 1000f;
                    return true;
                }

                return false;
            }

            if (normalized.EndsWith("s", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
        }

        private YuspecScenarioResult RunScenario(YuspecScenarioDefinition scenario)
        {
            var result = new YuspecScenarioResult { Name = scenario.Name, Passed = true, Message = "Passed" };

            foreach (var given in scenario.GivenSteps)
            {
                ApplyGiven(given, result);
            }

            foreach (var when in scenario.WhenSteps)
            {
                ApplyWhen(when, result);
            }

            foreach (var expect in scenario.ExpectSteps)
            {
                CheckExpect(expect, result);
            }

            if (!result.Passed)
            {
                result.Message = string.Join(" | ", result.Failures);
            }

            return result;
        }

        private void ApplyGiven(YuspecScenarioStepDefinition step, YuspecScenarioResult result)
        {
            var text = step.Text;
            if (text.Contains(" has ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = text.Split(new[] { " has " }, StringSplitOptions.None);
                var entity = ResolveEntity(parts[0].Trim(), null, null);
                if (entity == null)
                {
                    result.Passed = false;
                    result.Failures.Add($"given unknown entity: {parts[0].Trim()}");
                    return;
                }

                var item = YuspecSpecParser.ParseLiteral(parts[1].Trim())?.ToString();
                var inventory = new List<string>();
                if (entity.TryGetProperty("inventory", out var inventoryValue) && inventoryValue is IEnumerable<string> existing)
                {
                    inventory.AddRange(existing);
                }

                if (!inventory.Contains(item, StringComparer.OrdinalIgnoreCase))
                {
                    inventory.Add(item);
                }

                entity.SetProperty("inventory", inventory);
                return;
            }

            if (text.Contains("=", StringComparison.Ordinal))
            {
                var eqIndex = text.IndexOf('=');
                var left = text.Substring(0, eqIndex).Trim();
                var right = text.Substring(eqIndex + 1).Trim();
                var leftParts = left.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (leftParts.Length == 2)
                {
                    var entity = ResolveEntity(leftParts[0], null, null);
                    if (entity == null)
                    {
                        result.Passed = false;
                        result.Failures.Add($"given unknown entity: {leftParts[0]}");
                        return;
                    }

                    entity.SetProperty(leftParts[1], YuspecSpecParser.ParseLiteral(right));
                }
            }
        }

        private void ApplyWhen(YuspecScenarioStepDefinition step, YuspecScenarioResult result)
        {
            var match = ScenarioWhenEvent.Match(step.Text);
            if (!match.Success)
            {
                result.Passed = false;
                result.Failures.Add($"invalid when step: {step.Text}");
                return;
            }

            var actorRef = match.Groups[1].Value;
            var eventName = match.Groups[2].Value;
            var targetRef = match.Groups[3].Success ? match.Groups[3].Value : string.Empty;

            var actor = ResolveEntity(actorRef, null, null);
            var target = string.IsNullOrWhiteSpace(targetRef) ? null : ResolveEntity(targetRef, null, null);
            if (actor == null)
            {
                result.Passed = false;
                result.Failures.Add($"when unknown actor: {actorRef}");
                return;
            }

            Emit($"{actor.EntityType}.{eventName}", actor, target);
        }

        private void CheckExpect(YuspecScenarioStepDefinition step, YuspecScenarioResult result)
        {
            var match = ExpectEquals.Match(step.Text);
            if (!match.Success)
            {
                result.Passed = false;
                result.Failures.Add($"invalid expect step: {step.Text}");
                return;
            }

            var entity = ResolveEntity(match.Groups[1].Value, null, null);
            var property = match.Groups[2].Value;
            var expected = ResolveValue(match.Groups[3].Value, null, null);

            if (entity == null || !entity.TryGetProperty(property, out var actual))
            {
                result.Passed = false;
                result.Failures.Add($"expect failed; missing {match.Groups[1].Value}.{property}");
                return;
            }

            if (!string.Equals(actual?.ToString(), expected?.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                result.Passed = false;
                result.Failures.Add($"expect failed; {match.Groups[1].Value}.{property} expected '{expected}', actual '{actual}'");
            }
        }

        private sealed class StateMachineSession
        {
            public YuspecBehaviorDefinition Behavior;
            public YuspecEntity Entity;
            public YuspecStateDefinition CurrentState;
            public float StateElapsed;
            public float LastTransitionTime;
            public readonly YuspecStateMachineStatus Status;
            public readonly Dictionary<YuspecTimedActionBlock, float> EveryTimers = new Dictionary<YuspecTimedActionBlock, float>();

            public StateMachineSession(YuspecBehaviorDefinition behavior, YuspecEntity entity, YuspecStateDefinition initialState)
            {
                Behavior = behavior;
                Entity = entity;
                CurrentState = initialState;
                StateElapsed = 0f;
                LastTransitionTime = Application.isPlaying ? Time.time : 0f;

                Status = new YuspecStateMachineStatus
                {
                    BehaviorName = behavior.Name,
                    EntityId = entity.EntityId,
                    EntityType = entity.EntityType,
                    CurrentState = initialState.Name,
                    StateElapsed = 0f,
                    LastTransitionTime = LastTransitionTime
                };
            }
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
