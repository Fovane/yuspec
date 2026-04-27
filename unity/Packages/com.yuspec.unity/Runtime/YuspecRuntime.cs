using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Yuspec.Unity
{
    [ExecuteAlways]
    public sealed class YuspecRuntime : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Object[] specs;
        [SerializeField] private bool strictMode = true;
        [SerializeField] private bool hotReload = true;
        [SerializeField] private float hotReloadPollInterval = 0.5f;
        [SerializeField] private int maxRecentEvents = 100;

        private readonly Dictionary<string, YuspecEntity> entitiesById = new Dictionary<string, YuspecEntity>(StringComparer.OrdinalIgnoreCase);
        private readonly List<YuspecCompiledSpec> compiledSpecs = new List<YuspecCompiledSpec>();
        private readonly List<UnityEngine.Object> compiledSpecAssets = new List<UnityEngine.Object>();
        private readonly List<YuspecDiagnostic> diagnostics = new List<YuspecDiagnostic>();
        private readonly List<YuspecEvent> recentEvents = new List<YuspecEvent>();
        private readonly List<string> debugTrace = new List<string>();
        private readonly List<YuspecTraceEntry> traceEntries = new List<YuspecTraceEntry>();
        private readonly List<YuspecStateMachineStatus> stateMachineStatuses = new List<YuspecStateMachineStatus>();
        private readonly List<YuspecScenarioResult> scenarioResults = new List<YuspecScenarioResult>();
        private readonly List<StateMachineSession> stateMachineSessions = new List<StateMachineSession>();
        private readonly Dictionary<UnityEngine.Object, int> specContentHashes = new Dictionary<UnityEngine.Object, int>();
        private readonly Dictionary<string, FileSystemWatcher> fileWatchers = new Dictionary<string, FileSystemWatcher>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentQueue<string> pendingHotReloadPaths = new ConcurrentQueue<string>();
        private readonly Dictionary<string, UnityEngine.Object> scriptableObjectCache = new Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);
        private readonly YuspecActionRegistry actionRegistry = new YuspecActionRegistry();
        private YuspecDialogueRuntime dialogueRuntime;
        private float hotReloadTimer;

        private static readonly Regex ExpectEquals = new Regex(
            "^([A-Za-z_][A-Za-z0-9_]*)\\.([A-Za-z_][A-Za-z0-9_]*)\\s*==\\s*(.+)$",
            RegexOptions.Compiled);
        private static readonly Regex ScenarioWhenEvent = new Regex(
            "^([A-Za-z_][A-Za-z0-9_]*)\\.([A-Za-z_][A-Za-z0-9_]*)(?:\\s+([A-Za-z_][A-Za-z0-9_]*))?$",
            RegexOptions.Compiled);

        public IReadOnlyList<UnityEngine.Object> Specs => specs ?? Array.Empty<UnityEngine.Object>();
        public bool StrictMode => strictMode;
        public bool HotReload => hotReload;
        public float HotReloadPollInterval => hotReloadPollInterval;
        public YuspecActionRegistry ActionRegistry => actionRegistry;
        public YuspecDialogueRuntime DialogueRuntime => dialogueRuntime;
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
            DrainHotReloadQueue();

            if (Application.isPlaying)
            {
                TickStateMachines(Time.deltaTime);
            }

            TickHotReload(Application.isPlaying ? Time.deltaTime : hotReloadPollInterval);
        }

        private void OnDestroy()
        {
            DisposeFileWatchers();
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
            compiledSpecs.Clear();
            compiledSpecAssets.Clear();
            scriptableObjectCache.Clear();

            dialogueRuntime = GetComponent<YuspecDialogueRuntime>();
            if (dialogueRuntime == null)
            {
                dialogueRuntime = gameObject.AddComponent<YuspecDialogueRuntime>();
            }

            actionRegistry.RegisterFromLoadedAssemblies();
            foreach (var diagnostic in actionRegistry.Diagnostics)
            {
                AddDiagnostic(diagnostic);
            }

            LoadSpecs();
        }

        public void LoadSpecs()
        {
            compiledSpecs.Clear();
            compiledSpecAssets.Clear();
            scriptableObjectCache.Clear();

            if (specs == null)
            {
                DisposeFileWatchers();
                return;
            }

            var entityTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var spec in specs)
            {
                if (spec == null)
                {
                    AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Warning, "YSP0100", "Spec slot is empty."));
                    continue;
                }

                var sourceText = GetSpecText(spec);
                var sourceName = GetSpecSourceName(spec);
                if (sourceText == null)
                {
                    AddDiagnostic(new YuspecDiagnostic(
                        YuspecDiagnosticSeverity.Error,
                        "YSP0104",
                        $"Unsupported spec asset type '{spec.GetType().Name}'. Use a .yuspec asset or TextAsset.",
                        sourceName,
                        1,
                        1));
                    continue;
                }

                var parser = new YuspecSpecParser();
                var compiledSpec = parser.Parse(sourceName, sourceText);
                compiledSpecs.Add(compiledSpec);
                compiledSpecAssets.Add(spec);
                foreach (var diagnostic in parser.Diagnostics)
                {
                    AddDiagnostic(diagnostic);
                }

                AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Info, "YSP0101", $"Loaded spec '{sourceName}'.", sourceName));

                foreach (var declaration in compiledSpec.Entities)
                {
                    if (!entityTypes.Add(declaration.EntityType))
                    {
                        AddDiagnostic(new YuspecDiagnostic(
                            YuspecDiagnosticSeverity.Error,
                            "YSP0102",
                            $"Duplicate entity declaration '{declaration.EntityType}'.",
                            sourceName,
                            declaration.Line,
                            1));
                    }
                }
            }

            LoadScriptableObjectBindings();
            ValidateActionBindings();
            ValidateStrictReferences();
            ValidateStaticAnalysis();
            ApplyDeclarationsToRegisteredEntities(false);
            BuildStateMachineSessions(false);
            CaptureSpecHashes();
            ConfigureFileWatchers();
        }

        public bool ReloadSpecsIfChanged()
        {
            for (var index = 0; index < compiledSpecAssets.Count; index++)
            {
                var asset = compiledSpecAssets[index];
                var currentHash = GetSpecHash(asset);
                if (!specContentHashes.TryGetValue(asset, out var previousHash) || previousHash != currentHash)
                {
                    var sourceText = GetSpecText(asset);
                    if (sourceText == null)
                    {
                        return false;
                    }

                    return ReloadSpecAtIndex(index, GetSpecSourceName(asset), sourceText);
                }
            }

            return false;
        }

        public void RegisterEntity(YuspecEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(entity.EntityId))
            {
                AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0200", "Entity id is empty."));
                return;
            }

            if (entitiesById.TryGetValue(entity.EntityId, out var existing) && existing != entity)
            {
                AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0201", $"Duplicate entity id '{entity.EntityId}'."));
                return;
            }

            entitiesById[entity.EntityId] = entity;
            ApplyDeclaration(entity, false);
            BuildStateMachineSessions(true);
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
                BuildStateMachineSessions(true);
            }
        }

        public YuspecEntityDeclaration GetEntityDeclaration(string entityType)
        {
            return compiledSpecs
                .SelectMany(spec => spec.Entities)
                .FirstOrDefault(candidate => string.Equals(candidate.EntityType, entityType, StringComparison.OrdinalIgnoreCase));
        }

        public bool TryGetEntityPropertyDeclaration(string entityType, string propertyName, out YuspecPropertyDeclaration property)
        {
            property = null;
            var declaration = GetEntityDeclaration(entityType);
            return declaration != null && declaration.Properties.TryGetValue(propertyName, out property);
        }

        public void ReportTypeMismatch(string entityType, string propertyName, YuspecPropertyType expectedType, object value, string sourceName = "", int line = 0)
        {
            AddDiagnostic(new YuspecDiagnostic(
                YuspecDiagnosticSeverity.Error,
                "YSP1002R",
                $"Type mismatch for '{entityType}.{propertyName}': expected {YuspecSpecParser.FormatType(expectedType)}, got {YuspecSpecParser.FormatClrType(value)}.",
                sourceName,
                line,
                1));
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
                AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0300", "Event name is empty."));
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
                    ExecuteActions(everyBlock.Actions, session.Entity, session.Entity, session.Behavior.Name, everyBlock.SourceName, everyBlock.Line);
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
                AddDiagnostic(diagnostic);
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

        private void LoadScriptableObjectBindings()
        {
            foreach (var declaration in compiledSpecs.SelectMany(spec => spec.Entities).Where(entity => !string.IsNullOrWhiteSpace(entity.ScriptableObjectPath)))
            {
                var asset = LoadScriptableObject(declaration.ScriptableObjectPath);
                if (asset == null)
                {
                    AddDiagnostic(new YuspecDiagnostic(
                        YuspecDiagnosticSeverity.Error,
                        "YSP0501",
                        $"ScriptableObject asset not found at '{declaration.ScriptableObjectPath}'.",
                        declaration.SourceName,
                        declaration.Line,
                        1));
                    continue;
                }

                foreach (var property in declaration.Properties.Values)
                {
                    if (!TryGetMemberValue(asset, property.Name, out var memberValue))
                    {
                        AddDiagnostic(new YuspecDiagnostic(
                            YuspecDiagnosticSeverity.Error,
                            "YSP0502",
                            $"ScriptableObject '{declaration.ScriptableObjectPath}' has no field or property '{property.Name}'.",
                            property.SourceName,
                            property.Line,
                            property.Column));
                        continue;
                    }

                    var normalized = NormalizeScriptableObjectValue(memberValue);
                    if (!YuspecSpecParser.TryConvertToYuspecType(normalized, property.Type, out var converted))
                    {
                        AddDiagnostic(new YuspecDiagnostic(
                            YuspecDiagnosticSeverity.Error,
                            "YSP0503",
                            $"ScriptableObject value '{property.Name}' type mismatch: expected {YuspecSpecParser.FormatType(property.Type)}, got {YuspecSpecParser.FormatClrType(normalized)}.",
                            property.SourceName,
                            property.Line,
                            property.Column));
                        continue;
                    }

                    property.Value = converted;
                    property.HasDefaultValue = true;
                }
            }
        }

        private void ValidateActionBindings()
        {
            foreach (var action in EnumerateAllActions())
            {
                if (action.IsSetAction || IsBuiltInAction(action.Name))
                {
                    continue;
                }

                if (!actionRegistry.TryGetAction(action.Name, out var binding))
                {
                    AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0103", $"Unknown action '{action.Name}'.", string.Empty, action.Line, 1));
                    continue;
                }

                var parameterTypes = binding.ParameterTypes;
                if (IsActorInjectedMoveTowards(action, parameterTypes))
                {
                    parameterTypes = parameterTypes.Skip(1).ToArray();
                }

                if (parameterTypes.Length != action.Arguments.Count)
                {
                    AddDiagnostic(new YuspecDiagnostic(
                        YuspecDiagnosticSeverity.Error,
                        "YSP0105",
                        $"Action '{action.Name}' expects {parameterTypes.Length} argument(s), got {action.Arguments.Count}.",
                        string.Empty,
                        action.Line,
                        1));
                    continue;
                }

                for (var index = 0; index < action.Arguments.Count; index++)
                {
                    var inferred = ResolvePotentialLiteralType(action.Arguments[index]);
                    if (inferred == null)
                    {
                        continue;
                    }

                    var required = parameterTypes[index];
                    if (!IsTypeCompatible(required, inferred))
                    {
                        AddDiagnostic(new YuspecDiagnostic(
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
            ValidateDialogues(declarations);
        }

        private void ValidateStaticAnalysis()
        {
            var eventHandlers = compiledSpecs.SelectMany(spec => spec.EventHandlers).ToList();
            var graph = new Dictionary<string, List<EmittedEventEdge>>(StringComparer.OrdinalIgnoreCase);
            foreach (var handler in eventHandlers)
            {
                if (!graph.TryGetValue(handler.EventName, out var edges))
                {
                    edges = new List<EmittedEventEdge>();
                    graph[handler.EventName] = edges;
                }

                foreach (var action in handler.Actions)
                {
                    if (TryGetEmittedEvent(action, handler.ActorType, out var emittedEvent))
                    {
                        edges.Add(new EmittedEventEdge(emittedEvent, handler.SourceName, action.Line));
                    }
                }
            }

            var reportedCycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var eventName in graph.Keys.ToList())
            {
                VisitEventCycle(eventName, graph, new List<string>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase), reportedCycles);
            }

            foreach (var behavior in compiledSpecs.SelectMany(spec => spec.Behaviors))
            {
                foreach (var state in behavior.States)
                {
                    foreach (var every in state.EveryBlocks)
                    {
                        foreach (var action in every.Actions)
                        {
                            if (!TryGetEmittedEvent(action, behavior.EntityType, out var emittedEvent))
                            {
                                continue;
                            }

                            if (CanReachEvent(emittedEvent, emittedEvent, graph, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                            {
                                AddDiagnostic(new YuspecDiagnostic(
                                    YuspecDiagnosticSeverity.Error,
                                    "YSP0402",
                                    $"Every block in state '{state.Name}' can re-trigger event '{emittedEvent}' indefinitely.",
                                    every.SourceName,
                                    every.Line,
                                    1));
                            }
                        }
                    }
                }
            }
        }

        private void VisitEventCycle(
            string eventName,
            Dictionary<string, List<EmittedEventEdge>> graph,
            List<string> path,
            HashSet<string> pathSet,
            HashSet<string> reportedCycles)
        {
            if (pathSet.Contains(eventName))
            {
                var start = path.IndexOf(eventName);
                var cycle = path.Skip(Math.Max(0, start)).Concat(new[] { eventName }).ToList();
                var key = string.Join("->", cycle);
                if (reportedCycles.Add(key))
                {
                    var source = graph.TryGetValue(eventName, out var edges) && edges.Count > 0 ? edges[0] : new EmittedEventEdge(eventName, string.Empty, 1);
                    AddDiagnostic(new YuspecDiagnostic(
                        YuspecDiagnosticSeverity.Error,
                        "YSP0401",
                        $"Event handler cycle detected: {string.Join(" -> ", cycle)}.",
                        source.SourceName,
                        source.Line,
                        1));
                }

                return;
            }

            if (!graph.TryGetValue(eventName, out var nextEvents))
            {
                return;
            }

            path.Add(eventName);
            pathSet.Add(eventName);
            foreach (var next in nextEvents)
            {
                VisitEventCycle(next.EventName, graph, path, pathSet, reportedCycles);
            }

            path.RemoveAt(path.Count - 1);
            pathSet.Remove(eventName);
        }

        private static bool CanReachEvent(string current, string target, Dictionary<string, List<EmittedEventEdge>> graph, HashSet<string> visited)
        {
            if (!graph.TryGetValue(current, out var nextEvents))
            {
                return false;
            }

            foreach (var next in nextEvents)
            {
                if (string.Equals(next.EventName, target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (visited.Add(next.EventName) && CanReachEvent(next.EventName, target, graph, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<YuspecActionCall> EnumerateAllActions()
        {
            foreach (var action in compiledSpecs.SelectMany(spec => spec.EventHandlers).SelectMany(handler => handler.Actions))
            {
                yield return action;
            }

            foreach (var behavior in compiledSpecs.SelectMany(spec => spec.Behaviors))
            {
                foreach (var state in behavior.States)
                {
                    foreach (var action in state.EnterActions.Concat(state.ExitActions).Concat(state.DoActions).Concat(state.EveryBlocks.SelectMany(block => block.Actions)))
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
                AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0112", $"Duplicate event handler '{duplicate.EventName}'.", duplicate.SourceName, duplicate.Line, 1));
            }
        }

        private void ValidateBehaviors(Dictionary<string, YuspecEntityDeclaration> declarations)
        {
            foreach (var behavior in compiledSpecs.SelectMany(spec => spec.Behaviors))
            {
                RequireEntityDeclaration(declarations, behavior.EntityType, behavior.SourceName, behavior.Line, "behavior entity type");
                if (behavior.States.Count == 0)
                {
                    AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0113", $"Behavior '{behavior.Name}' has no states.", behavior.SourceName, behavior.Line, 1));
                    continue;
                }

                var duplicateStates = behavior.States
                    .GroupBy(state => state.Name, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1)
                    .SelectMany(group => group.Skip(1));

                foreach (var duplicate in duplicateStates)
                {
                    AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0114", $"Duplicate state '{duplicate.Name}' in behavior '{behavior.Name}'.", duplicate.SourceName, duplicate.Line, 1));
                }

                var stateNames = new HashSet<string>(behavior.States.Select(state => state.Name), StringComparer.OrdinalIgnoreCase);
                foreach (var state in behavior.States)
                {
                    foreach (var transition in state.Transitions)
                    {
                        if (!stateNames.Contains(transition.TargetState))
                        {
                            AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0115", $"Unknown transition target '{transition.TargetState}' in behavior '{behavior.Name}'.", transition.SourceName, transition.Line, 1));
                        }
                    }

                    foreach (var block in state.EveryBlocks)
                    {
                        if (!TryParseIntervalSeconds(block.IntervalText, out var interval) || interval <= 0f)
                        {
                            AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0116", $"Invalid interval '{block.IntervalText}' in state '{state.Name}'.", block.SourceName, block.Line, 1));
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
                    AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0403", $"State '{state.Name}' in behavior '{behavior.Name}' is unreachable from initial state '{behavior.States[0].Name}'.", state.SourceName, state.Line, 1));
                }
            }
        }

        private void ValidateScenarios(Dictionary<string, YuspecEntityDeclaration> declarations)
        {
            foreach (var scenario in compiledSpecs.SelectMany(spec => spec.Scenarios))
            {
                foreach (var step in scenario.GivenSteps.Concat(scenario.WhenSteps).Concat(scenario.ExpectSteps))
                {
                    ValidateScenarioEntityReference(declarations, step.Text, scenario.Name, step.Line);
                }
            }
        }

        private void ValidateDialogues(Dictionary<string, YuspecEntityDeclaration> declarations)
        {
            foreach (var dialogue in compiledSpecs.SelectMany(spec => spec.Dialogues))
            {
                RequireEntityDeclaration(declarations, dialogue.EntityType, dialogue.SourceName, dialogue.Line, "dialogue entity type");
            }
        }

        private void ValidateScenarioEntityReference(Dictionary<string, YuspecEntityDeclaration> declarations, string stepText, string scenarioName, int line)
        {
            if (string.IsNullOrWhiteSpace(stepText))
            {
                return;
            }

            var entityToken = stepText.Split(new[] { ' ', '.' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(entityToken) || declarations.ContainsKey(entityToken))
            {
                return;
            }

            AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0118", $"Scenario '{scenarioName}' references unknown entity '{entityToken}'.", string.Empty, line, 1));
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
                ValidateSetActionType(declarations, action, handler.SourceName);
                return;
            }

            if (string.Equals(action.Name, "emit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action.Name, "start_dialogue", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (var argument in action.Arguments)
            {
                ValidateValueReference(declarations, argument, handler.SourceName, action.Line);
            }
        }

        private void ValidateSetActionType(Dictionary<string, YuspecEntityDeclaration> declarations, YuspecActionCall action, string sourceName)
        {
            if (!declarations.TryGetValue(action.TargetEntity, out var declaration) ||
                !declaration.Properties.TryGetValue(action.TargetProperty, out var property) ||
                property.Type == YuspecPropertyType.Unknown ||
                action.AssignedValue.Contains("."))
            {
                return;
            }

            var parsed = YuspecSpecParser.ParseLiteral(action.AssignedValue);
            if (ReferenceEquals(parsed, action.AssignedValue))
            {
                return;
            }

            if (!YuspecSpecParser.TryConvertToYuspecType(parsed, property.Type, out _))
            {
                AddDiagnostic(new YuspecDiagnostic(
                    YuspecDiagnosticSeverity.Error,
                    "YSP0119",
                    $"Set action type mismatch for '{action.TargetEntity}.{action.TargetProperty}': expected {YuspecSpecParser.FormatType(property.Type)}, got {YuspecSpecParser.FormatClrType(parsed)}.",
                    sourceName,
                    action.Line,
                    1));
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
            }
        }

        private void RequireEntityDeclaration(Dictionary<string, YuspecEntityDeclaration> declarations, string entityType, string sourceName, int line, string usage)
        {
            if (string.IsNullOrWhiteSpace(entityType) ||
                declarations.ContainsKey(entityType) ||
                string.Equals(entityType, "self", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entityType, "actor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entityType, "target", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0110", $"Unknown entity '{entityType}' used as {usage}.", sourceName, line, 1));
        }

        private void RequirePropertyDeclaration(Dictionary<string, YuspecEntityDeclaration> declarations, string entityType, string propertyName, string sourceName, int line)
        {
            if (string.IsNullOrWhiteSpace(entityType) ||
                string.IsNullOrWhiteSpace(propertyName) ||
                string.Equals(entityType, "self", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entityType, "actor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entityType, "target", StringComparison.OrdinalIgnoreCase) ||
                !declarations.TryGetValue(entityType, out var declaration))
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

            AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP0111", $"Unknown property '{entityType}.{propertyName}'.", sourceName, line, 1));
        }

        private void ApplyDeclarationsToRegisteredEntities(bool overwriteExisting)
        {
            foreach (var entity in entitiesById.Values)
            {
                ApplyDeclaration(entity, overwriteExisting);
            }
        }

        private void ApplyDeclaration(YuspecEntity entity, bool overwriteExisting)
        {
            var declaration = GetEntityDeclaration(entity.EntityType);
            if (declaration == null)
            {
                return;
            }

            foreach (var property in declaration.Properties.Values)
            {
                if (!property.HasDefaultValue)
                {
                    continue;
                }

                if (string.Equals(property.Name, "state", StringComparison.OrdinalIgnoreCase))
                {
                    if (overwriteExisting || string.IsNullOrEmpty(entity.CurrentState))
                    {
                        entity.SetPropertyFromRuntime(property.Name, CloneValue(property.Value));
                    }

                    continue;
                }

                if (overwriteExisting || !entity.Properties.ContainsKey(property.Name))
                {
                    entity.SetPropertyFromRuntime(property.Name, CloneValue(property.Value));
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

            if (string.IsNullOrWhiteSpace(handler.TargetType))
            {
                return true;
            }

            return target != null && string.Equals(target.EntityType, handler.TargetType, StringComparison.OrdinalIgnoreCase);
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
                return left != null &&
                       right != null &&
                       right.TryGetProperty(condition.RightValue, out var requiredValue) &&
                       left.HasValue(requiredValue?.ToString());
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
                ExecuteSetAction(action, actor, target, handler);
                return;
            }

            if (string.Equals(action.Name, "emit", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteEmitAction(action, actor, target);
                return;
            }

            if (string.Equals(action.Name, "start_dialogue", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteStartDialogueAction(action, actor, target, handler.SourceName);
                return;
            }

            var args = ResolveActionArguments(action, actor, target);
            AddTrace(YuspecTraceKind.ActionExecuted, $"{action.Name}({string.Join(", ", args.Select(arg => arg?.ToString() ?? "null"))})", handler.SourceName, action.Line);
            ExecuteAction(action.Name, args);
        }

        private void ExecuteSetAction(YuspecActionCall action, YuspecEntity actor, YuspecEntity target, YuspecEventHandler handler)
        {
            var targetEntity = ResolveEntity(action.TargetEntity, actor, target);
            if (targetEntity == null)
            {
                AddRuntimeDiagnostic("YSP0400", $"Unknown entity '{action.TargetEntity}' for set action.", handler.SourceName, action.Line);
                return;
            }

            var value = ResolveValue(action.AssignedValue, actor, target);
            if (TryGetEntityPropertyDeclaration(targetEntity.EntityType, action.TargetProperty, out var propertyDeclaration))
            {
                if (!YuspecSpecParser.TryConvertToYuspecType(value, propertyDeclaration.Type, out var converted))
                {
                    AddRuntimeDiagnostic(
                        "YSP0404",
                        $"Type mismatch for set {targetEntity.EntityType}.{action.TargetProperty}: expected {YuspecSpecParser.FormatType(propertyDeclaration.Type)}, got {YuspecSpecParser.FormatClrType(value)}.",
                        handler.SourceName,
                        action.Line);
                    return;
                }

                value = converted;
            }

            targetEntity.SetPropertyFromRuntime(action.TargetProperty, value);
            TryWriteBackScriptableObject(targetEntity.EntityType, action.TargetProperty, value, handler.SourceName, action.Line);
            AddTrace(YuspecTraceKind.ActionExecuted, $"set {targetEntity.EntityId}.{action.TargetProperty} = {FormatValue(value)}", handler.SourceName, action.Line);
        }

        private void ExecuteEmitAction(YuspecActionCall action, YuspecEntity actor, YuspecEntity target)
        {
            if (action.Arguments.Count == 0)
            {
                AddRuntimeDiagnostic("YSP0405", "emit requires an event name.", string.Empty, action.Line);
                return;
            }

            var eventText = ResolveValue(action.Arguments[0], actor, target)?.ToString();
            if (string.IsNullOrWhiteSpace(eventText))
            {
                AddRuntimeDiagnostic("YSP0406", "emit event name is empty.", string.Empty, action.Line);
                return;
            }

            if (!eventText.Contains(".") && actor != null)
            {
                eventText = $"{actor.EntityType}.{eventText}";
            }

            var emittedTarget = action.Arguments.Count > 1 ? ResolveEntity(action.Arguments[1], actor, target) : target;
            Emit(eventText, actor, emittedTarget);
        }

        private void ExecuteStartDialogueAction(YuspecActionCall action, YuspecEntity actor, YuspecEntity target, string sourceName)
        {
            if (action.Arguments.Count != 1)
            {
                AddRuntimeDiagnostic("YSP0601", "start_dialogue requires exactly one dialogue name.", sourceName, action.Line);
                return;
            }

            var dialogueName = ResolveValue(action.Arguments[0], actor, target)?.ToString();
            var dialogue = compiledSpecs.SelectMany(spec => spec.Dialogues)
                .FirstOrDefault(candidate => string.Equals(candidate.Name, dialogueName, StringComparison.OrdinalIgnoreCase));
            if (dialogue == null)
            {
                AddRuntimeDiagnostic("YSP0602", $"Unknown dialogue '{dialogueName}'.", sourceName, action.Line);
                return;
            }

            var speaker = target ?? actor;
            dialogueRuntime.StartDialogue(dialogue, speaker);
            AddTrace(YuspecTraceKind.ActionExecuted, $"start_dialogue {dialogue.Name}", sourceName, action.Line);
        }

        private object[] ResolveActionArguments(YuspecActionCall action, YuspecEntity actor, YuspecEntity target)
        {
            if (string.Equals(action.Name, "move_towards", StringComparison.OrdinalIgnoreCase) &&
                actionRegistry.TryGetAction(action.Name, out var moveBinding) &&
                IsActorInjectedMoveTowards(action, moveBinding.ParameterTypes))
            {
                return new object[]
                {
                    actor,
                    ResolveValue(action.Arguments[0], actor, target),
                    ResolveValue(action.Arguments[1], actor, target),
                    ResolveValue(action.Arguments[2], actor, target)
                };
            }

            if (!actionRegistry.TryGetAction(action.Name, out var binding) || binding.ParameterTypes.Length != action.Arguments.Count)
            {
                return action.Arguments.Select(argument => ResolveValue(argument, actor, target)).ToArray();
            }

            var args = new object[action.Arguments.Count];
            for (var i = 0; i < action.Arguments.Count; i++)
            {
                var raw = ResolveValue(action.Arguments[i], actor, target);
                args[i] = ConvertToType(raw, binding.ParameterTypes[i]);
            }

            return args;
        }

        private static bool IsActorInjectedMoveTowards(YuspecActionCall action, Type[] parameterTypes)
        {
            return string.Equals(action.Name, "move_towards", StringComparison.OrdinalIgnoreCase) &&
                   parameterTypes.Length == 4 &&
                   parameterTypes[0] == typeof(YuspecEntity) &&
                   parameterTypes[1] == typeof(YuspecEntity) &&
                   action.Arguments.Count == 3;
        }

        private static object ConvertToType(object value, Type targetType)
        {
            if (value == null)
            {
                return null;
            }

            var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (type.IsInstanceOfType(value))
            {
                return value;
            }

            if (type == typeof(float) && value is int intValue)
            {
                return (float)intValue;
            }

            if (type == typeof(string[]) && value is List<string> list)
            {
                return list.ToArray();
            }

            if (type == typeof(List<string>) && value is string[] array)
            {
                return array.ToList();
            }

            try
            {
                return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            }
            catch
            {
                return value;
            }
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
            return resolvedEntity != null ? (object)resolvedEntity : YuspecSpecParser.ParseLiteral(token);
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

        private void BuildStateMachineSessions(bool preserveCurrentState)
        {
            stateMachineSessions.Clear();
            stateMachineStatuses.Clear();

            foreach (var entity in entitiesById.Values)
            {
                foreach (var behavior in compiledSpecs.SelectMany(spec => spec.Behaviors).Where(behavior => string.Equals(behavior.EntityType, entity.EntityType, StringComparison.OrdinalIgnoreCase)))
                {
                    var initial = behavior.States.FirstOrDefault();
                    if (initial == null)
                    {
                        continue;
                    }

                    var selected = initial;
                    if (preserveCurrentState && !string.IsNullOrWhiteSpace(entity.CurrentState))
                    {
                        selected = behavior.States.FirstOrDefault(state => string.Equals(state.Name, entity.CurrentState, StringComparison.OrdinalIgnoreCase)) ?? initial;
                    }

                    var session = new StateMachineSession(behavior, entity, selected);
                    stateMachineSessions.Add(session);
                    stateMachineStatuses.Add(session.Status);
                    entity.CurrentState = selected.Name;

                    if (!preserveCurrentState || selected == initial)
                    {
                        ExecuteActions(selected.EnterActions, entity, entity, behavior.Name, selected.SourceName, selected.Line);
                    }
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

                    TransitionTo(session, transition.TargetState, transition.SourceName, transition.Line);
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
            return !string.IsNullOrWhiteSpace(eventSuffix) && string.Equals(normalized, eventSuffix, StringComparison.OrdinalIgnoreCase);
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

                var property = text.Substring("self.".Length, opIndex - "self.".Length).Trim();
                var right = text.Substring(opIndex + marker.Length).Trim();
                return entity.TryGetProperty(property, out var leftValue) && CompareValues(leftValue, YuspecSpecParser.ParseLiteral(right), op);
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

        private void TransitionTo(StateMachineSession session, string targetStateName, string sourceName, int line)
        {
            var targetState = session.Behavior.States.FirstOrDefault(state => string.Equals(state.Name, targetStateName, StringComparison.OrdinalIgnoreCase));
            if (targetState == null)
            {
                AddRuntimeDiagnostic("YSP0500", $"Unknown transition target '{targetStateName}' in behavior '{session.Behavior.Name}'.", sourceName, line);
                return;
            }

            ExecuteActions(session.CurrentState.ExitActions, session.Entity, session.Entity, session.Behavior.Name, sourceName, line);
            session.CurrentState = targetState;
            session.StateElapsed = 0f;
            session.EveryTimers.Clear();
            session.LastTransitionTime = Application.isPlaying ? Time.time : 0f;
            session.Entity.CurrentState = targetState.Name;
            session.Status.CurrentState = targetState.Name;
            session.Status.StateElapsed = 0f;
            session.Status.LastTransitionTime = session.LastTransitionTime;
            AddTrace(YuspecTraceKind.ActionExecuted, $"state transition {session.Entity.EntityId}: {targetState.Name}", sourceName, line);
            ExecuteActions(targetState.EnterActions, session.Entity, session.Entity, session.Behavior.Name, targetState.SourceName, targetState.Line);
        }

        private void ExecuteActions(IEnumerable<YuspecActionCall> actions, YuspecEntity actor, YuspecEntity target, string eventName, string sourceName, int line)
        {
            foreach (var action in actions)
            {
                ExecuteActionCall(action, actor, target, new YuspecEventHandler(actor?.EntityType ?? string.Empty, eventName, target?.EntityType ?? string.Empty, YuspecCondition.None, sourceName, line));
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
                return float.TryParse(normalized.Substring(0, normalized.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds) &&
                       (seconds = milliseconds / 1000f) >= 0f;
            }

            if (normalized.EndsWith("min", StringComparison.Ordinal))
            {
                return float.TryParse(normalized.Substring(0, normalized.Length - 3), NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes) &&
                       (seconds = minutes * 60f) >= 0f;
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
            if (text.IndexOf(" has ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var parts = Regex.Split(text, "\\s+has\\s+", RegexOptions.IgnoreCase);
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

                entity.SetPropertyFromRuntime("inventory", inventory);
                return;
            }

            if (text.Contains("="))
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

            if (!ValuesEqual(actual, expected))
            {
                result.Passed = false;
                result.Failures.Add($"expect failed; {match.Groups[1].Value}.{property} expected '{FormatValue(expected)}', actual '{FormatValue(actual)}'");
            }
        }

        private bool ReloadSpecAtIndex(int index, string sourceName, string sourceText)
        {
            if (index < 0 || index >= compiledSpecs.Count)
            {
                return false;
            }

            var oldHandlers = compiledSpecs[index].EventHandlers.Count;
            diagnostics.RemoveAll(diagnostic => string.Equals(diagnostic.source, sourceName, StringComparison.OrdinalIgnoreCase));
            var parser = new YuspecSpecParser();
            var compiledSpec = parser.Parse(sourceName, sourceText);
            compiledSpecs[index] = compiledSpec;
            foreach (var diagnostic in parser.Diagnostics)
            {
                AddDiagnostic(diagnostic);
            }

            LoadScriptableObjectBindings();
            ValidateActionBindings();
            ValidateStrictReferences();
            ValidateStaticAnalysis();
            ApplyDeclarationsToRegisteredEntities(false);
            BuildStateMachineSessions(true);
            CaptureSpecHashes();

            var updatedHandlers = compiledSpec.EventHandlers.Count;
            Debug.Log($"[YUSPEC] Hot reloaded: {Path.GetFileName(sourceName)} — {updatedHandlers} handlers updated");
            AddDiagnostic(new YuspecDiagnostic(YuspecDiagnosticSeverity.Info, "YSP0600", $"Hot reloaded: {Path.GetFileName(sourceName)} - {updatedHandlers} handlers updated", sourceName, 1, 1));
            return true;
        }

        private void DrainHotReloadQueue()
        {
            if (!hotReload)
            {
                return;
            }

            var reloaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (pendingHotReloadPaths.TryDequeue(out var path))
            {
                if (!reloaded.Add(path))
                {
                    continue;
                }

                var index = FindCompiledSpecIndexByPath(path);
                if (index < 0 || !File.Exists(path))
                {
                    continue;
                }

                ReloadSpecAtIndex(index, GetSpecSourceName(compiledSpecAssets[index]), File.ReadAllText(path));
            }
        }

        private int FindCompiledSpecIndexByPath(string path)
        {
            for (var i = 0; i < compiledSpecAssets.Count; i++)
            {
                var specPath = GetSpecPath(compiledSpecAssets[i]);
                if (!string.IsNullOrWhiteSpace(specPath) && string.Equals(Path.GetFullPath(specPath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private void ConfigureFileWatchers()
        {
            DisposeFileWatchers();
            if (!hotReload)
            {
                return;
            }

            foreach (var path in compiledSpecAssets.Select(GetSpecPath).Where(path => !string.IsNullOrWhiteSpace(path) && path.EndsWith(".yuspec", StringComparison.OrdinalIgnoreCase)))
            {
                var fullPath = Path.GetFullPath(path);
                var directory = Path.GetDirectoryName(fullPath);
                var fileName = Path.GetFileName(fullPath);
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory) || fileWatchers.ContainsKey(fullPath))
                {
                    continue;
                }

                var watcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };
                watcher.Changed += (_, __) => pendingHotReloadPaths.Enqueue(fullPath);
                watcher.Created += (_, __) => pendingHotReloadPaths.Enqueue(fullPath);
                watcher.Renamed += (_, __) => pendingHotReloadPaths.Enqueue(fullPath);
                fileWatchers[fullPath] = watcher;
            }
        }

        private void DisposeFileWatchers()
        {
            foreach (var watcher in fileWatchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            fileWatchers.Clear();
        }

        private UnityEngine.Object LoadScriptableObject(string assetPath)
        {
            if (scriptableObjectCache.TryGetValue(assetPath, out var cached))
            {
                return cached;
            }

            UnityEngine.Object asset = null;
#if UNITY_EDITOR
            asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
#else
            if (assetPath.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
            {
                var resourcesPath = assetPath.Substring("Assets/Resources/".Length);
                resourcesPath = Path.ChangeExtension(resourcesPath, null);
                asset = Resources.Load(resourcesPath);
            }
#endif
            scriptableObjectCache[assetPath] = asset;
            return asset;
        }

        private static bool TryGetMemberValue(UnityEngine.Object asset, string memberName, out object value)
        {
            value = null;
            var type = asset.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = type.GetField(memberName, flags);
            if (field != null)
            {
                value = field.GetValue(asset);
                return true;
            }

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.GetIndexParameters().Length == 0 && property.CanRead)
            {
                value = property.GetValue(asset, null);
                return true;
            }

            return false;
        }

        private void TryWriteBackScriptableObject(string entityType, string propertyName, object value, string sourceName, int line)
        {
            var declaration = GetEntityDeclaration(entityType);
            if (declaration == null || string.IsNullOrWhiteSpace(declaration.ScriptableObjectPath))
            {
                return;
            }

            var asset = LoadScriptableObject(declaration.ScriptableObjectPath);
            if (asset == null)
            {
                return;
            }

            if (!TrySetMemberValue(asset, propertyName, value, out var reason))
            {
                AddRuntimeDiagnostic("YSP0504", reason, sourceName, line);
            }
        }

        private static bool TrySetMemberValue(UnityEngine.Object asset, string memberName, object value, out string reason)
        {
            reason = string.Empty;
            var type = asset.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var classMutable = type.GetCustomAttribute<YuspecMutableAttribute>() != null;
            var field = type.GetField(memberName, flags);
            if (field != null)
            {
                if (!classMutable && field.GetCustomAttribute<YuspecMutableAttribute>() == null)
                {
                    reason = $"ScriptableObject field '{memberName}' is not marked [YuspecMutable].";
                    return false;
                }

                field.SetValue(asset, ConvertToType(value, field.FieldType));
#if UNITY_EDITOR
                EditorUtility.SetDirty(asset);
#endif
                return true;
            }

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.GetIndexParameters().Length == 0 && property.CanWrite)
            {
                if (!classMutable && property.GetCustomAttribute<YuspecMutableAttribute>() == null)
                {
                    reason = $"ScriptableObject property '{memberName}' is not marked [YuspecMutable].";
                    return false;
                }

                property.SetValue(asset, ConvertToType(value, property.PropertyType), null);
#if UNITY_EDITOR
                EditorUtility.SetDirty(asset);
#endif
                return true;
            }

            reason = $"ScriptableObject has no writable field or property '{memberName}'.";
            return false;
        }

        private static object NormalizeScriptableObjectValue(object value)
        {
            if (value is string[] array)
            {
                return array.ToList();
            }

            return value;
        }

        private static string GetSpecText(UnityEngine.Object spec)
        {
            if (spec is TextAsset textAsset)
            {
                return textAsset.text;
            }

            if (spec is YuspecSpecAsset specAsset)
            {
                if (!string.IsNullOrWhiteSpace(specAsset.SourcePath) && File.Exists(specAsset.SourcePath))
                {
                    return File.ReadAllText(specAsset.SourcePath);
                }

                return specAsset.SourceText;
            }

            return null;
        }

        private static string GetSpecSourceName(UnityEngine.Object spec)
        {
            if (spec is YuspecSpecAsset specAsset && !string.IsNullOrWhiteSpace(specAsset.SourcePath))
            {
                return specAsset.SourcePath;
            }

            var path = GetSpecPath(spec);
            return string.IsNullOrWhiteSpace(path) ? spec.name : path;
        }

        private static string GetSpecPath(UnityEngine.Object spec)
        {
            if (spec is YuspecSpecAsset specAsset && !string.IsNullOrWhiteSpace(specAsset.SourcePath))
            {
                return specAsset.SourcePath;
            }

#if UNITY_EDITOR
            return spec != null ? AssetDatabase.GetAssetPath(spec) : string.Empty;
#else
            return string.Empty;
#endif
        }

        private void TickHotReload(float deltaTime)
        {
            if (!hotReload)
            {
                return;
            }

            hotReloadTimer += Mathf.Max(0f, deltaTime);
            var interval = Mathf.Max(0.1f, hotReloadPollInterval);
            if (hotReloadTimer < interval)
            {
                return;
            }

            hotReloadTimer = 0f;
            ReloadSpecsIfChanged();
        }

        private void CaptureSpecHashes()
        {
            specContentHashes.Clear();
            foreach (var spec in compiledSpecAssets)
            {
                specContentHashes[spec] = GetSpecHash(spec);
            }
        }

        private static int GetSpecHash(UnityEngine.Object spec)
        {
            return StringComparer.Ordinal.GetHashCode(GetSpecText(spec) ?? string.Empty);
        }

        private void AddRuntimeDiagnostic(string code, string message, string sourceName, int line)
        {
            var diagnostic = new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, code, message, sourceName, line, 1);
            AddDiagnostic(diagnostic);
            AddTrace(YuspecTraceKind.Diagnostic, diagnostic.ToString(), sourceName, line);
        }

        private void AddDiagnostic(YuspecDiagnostic diagnostic)
        {
            diagnostics.Add(diagnostic);
            if (diagnostic.severity == YuspecDiagnosticSeverity.Error)
            {
                global::Yuspec.YuspecDiagnosticReporter.Report(
                    string.IsNullOrWhiteSpace(diagnostic.source) ? "YUSPEC" : diagnostic.source,
                    diagnostic.line <= 0 ? 1 : diagnostic.line,
                    diagnostic.column <= 0 ? 1 : diagnostic.column,
                    diagnostic.message);
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

        private static bool IsBuiltInAction(string actionName)
        {
            return string.Equals(actionName, "emit", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actionName, "start_dialogue", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetEmittedEvent(YuspecActionCall action, string defaultActorType, out string eventName)
        {
            eventName = string.Empty;
            if (!string.Equals(action.Name, "emit", StringComparison.OrdinalIgnoreCase) || action.Arguments.Count == 0)
            {
                return false;
            }

            eventName = YuspecSpecParser.ParseLiteral(action.Arguments[0])?.ToString() ?? string.Empty;
            if (!eventName.Contains(".") && !string.IsNullOrWhiteSpace(defaultActorType))
            {
                eventName = $"{defaultActorType}.{eventName}";
            }

            return !string.IsNullOrWhiteSpace(eventName);
        }

        private static Type ResolvePotentialLiteralType(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var trimmed = token.Trim();
            if (trimmed.Contains(".") || Regex.IsMatch(trimmed, "^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                return null;
            }

            var value = YuspecSpecParser.ParseLiteral(trimmed);
            return ReferenceEquals(value, trimmed) ? null : value?.GetType();
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

            if (normalizedExpected == typeof(float) && (actualType == typeof(int) || actualType == typeof(float)))
            {
                return true;
            }

            if (normalizedExpected == typeof(string[]) && actualType == typeof(List<string>))
            {
                return true;
            }

            return false;
        }

        private static object CloneValue(object value)
        {
            if (value is List<string> list)
            {
                return new List<string>(list);
            }

            return value;
        }

        private static bool ValuesEqual(object actual, object expected)
        {
            if (actual is IEnumerable<string> actualList && expected is IEnumerable<string> expectedList)
            {
                return actualList.SequenceEqual(expectedList, StringComparer.OrdinalIgnoreCase);
            }

            return string.Equals(FormatValue(actual), FormatValue(expected), StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatValue(object value)
        {
            if (value is IEnumerable<string> values && !(value is string))
            {
                return "[" + string.Join(", ", values) + "]";
            }

            return value?.ToString() ?? "null";
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

        private readonly struct EmittedEventEdge
        {
            public string EventName { get; }
            public string SourceName { get; }
            public int Line { get; }

            public EmittedEventEdge(string eventName, string sourceName, int line)
            {
                EventName = eventName;
                SourceName = sourceName;
                Line = line;
            }
        }
    }
}
