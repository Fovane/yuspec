using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Yuspec.Unity
{
    public sealed class YuspecRuntime : MonoBehaviour
    {
        [SerializeField] private TextAsset[] specs;
        [SerializeField] private bool strictMode = true;
        [SerializeField] private int maxRecentEvents = 100;

        private readonly Dictionary<string, YuspecEntity> entitiesById = new Dictionary<string, YuspecEntity>();
        private readonly List<YuspecDiagnostic> diagnostics = new List<YuspecDiagnostic>();
        private readonly List<YuspecEvent> recentEvents = new List<YuspecEvent>();
        private readonly YuspecActionRegistry actionRegistry = new YuspecActionRegistry();

        public IReadOnlyList<TextAsset> Specs => specs ?? Array.Empty<TextAsset>();
        public bool StrictMode => strictMode;
        public YuspecActionRegistry ActionRegistry => actionRegistry;
        public IReadOnlyList<YuspecDiagnostic> Diagnostics => diagnostics;
        public IReadOnlyList<YuspecEvent> RecentEvents => recentEvents;
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
            diagnostics.RemoveAll(diagnostic => diagnostic.code == "YSP0100");

            if (specs == null)
            {
                return;
            }

            foreach (var spec in specs)
            {
                if (spec == null)
                {
                    diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Warning, "YSP0100", "Spec slot is empty."));
                    continue;
                }

                // TODO: Parse and validate .yuspec source, then build runtime handlers.
                diagnostics.Add(new YuspecDiagnostic(
                    YuspecDiagnosticSeverity.Info,
                    "YSP0100",
                    $"Loaded spec placeholder '{spec.name}'. Parser integration is not connected yet.",
                    spec.name));
            }
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
            if (recentEvents.Count > maxRecentEvents)
            {
                recentEvents.RemoveAt(0);
            }

            // TODO: Match parsed event handlers, evaluate conditions, execute action blocks,
            // and record handler-level debug traces.
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
    }
}
