using System.Linq;
using Yuspec.Unity;
using UnityEditor;
using UnityEngine;

namespace Yuspec.Unity.Editor
{
    public sealed class YuspecDebugWindow : EditorWindow
    {
        private enum DebugTab
        {
            Overview,
            Specs,
            Diagnostics,
            Entities,
            Events,
            Actions,
            StateMachines,
            Scenarios,
            Settings
        }

        private Vector2 scroll;
        private DebugTab selectedTab;
        private bool autoRefresh = true;

        [MenuItem("Window/YUSPEC/Debugger")]
        public static void Open()
        {
            GetWindow<YuspecDebugWindow>("YUSPEC Debugger");
        }

        private void OnGUI()
        {
            var runtime = FindRuntime();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("YUSPEC Debugger", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (runtime != null && GUILayout.Button("Reload Specs", EditorStyles.toolbarButton))
                {
                    runtime.Initialize();
                    Repaint();
                }

                if (runtime != null && GUILayout.Button("Clear Trace", EditorStyles.toolbarButton))
                {
                    runtime.ClearTrace();
                    Repaint();
                }

                if (runtime != null && GUILayout.Button("Run Scenarios", EditorStyles.toolbarButton))
                {
                    runtime.RunScenarios();
                    Repaint();
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                {
                    Repaint();
                }
            }

            selectedTab = (DebugTab)GUILayout.Toolbar((int)selectedTab, new[]
            {
                "Overview",
                "Specs",
                "Diagnostics",
                "Entities",
                "Events",
                "Actions",
                "State Machines",
                "Scenarios",
                "Settings"
            });

            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (runtime == null)
            {
                EditorGUILayout.HelpBox("No YuspecRuntime found in the open scene.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            switch (selectedTab)
            {
                case DebugTab.Overview:
                    DrawOverview(runtime);
                    break;
                case DebugTab.Specs:
                    DrawSpecs(runtime);
                    break;
                case DebugTab.Diagnostics:
                    DrawDiagnostics(runtime);
                    break;
                case DebugTab.Entities:
                    DrawEntities(runtime);
                    break;
                case DebugTab.Events:
                    DrawEvents(runtime);
                    break;
                case DebugTab.Actions:
                    DrawActions(runtime);
                    break;
                case DebugTab.StateMachines:
                    DrawStateMachines(runtime);
                    break;
                case DebugTab.Scenarios:
                    DrawScenarios(runtime);
                    break;
                case DebugTab.Settings:
                    DrawSettings(runtime);
                    break;
            }

            EditorGUILayout.EndScrollView();

            if (autoRefresh)
            {
                Repaint();
            }
        }

        private static void DrawOverview(YuspecRuntime runtime)
        {
            DrawSection("Overview");
            EditorGUILayout.LabelField("Specs", runtime.CompiledSpecs.Count.ToString());
            EditorGUILayout.LabelField("Entities", runtime.Entities.Count.ToString());
            EditorGUILayout.LabelField("Handlers", runtime.CompiledSpecs.Sum(spec => spec.EventHandlers.Count).ToString());
            EditorGUILayout.LabelField("Behaviors", runtime.CompiledSpecs.Sum(spec => spec.Behaviors.Count).ToString());
            EditorGUILayout.LabelField("Scenarios", runtime.CompiledSpecs.Sum(spec => spec.Scenarios.Count).ToString());
            EditorGUILayout.LabelField("Diagnostics", runtime.Diagnostics.Count.ToString());
            EditorGUILayout.LabelField("Recent Events", runtime.RecentEvents.Count.ToString());
            EditorGUILayout.LabelField("Registered Actions", runtime.ActionRegistry.RegisteredActions.Count.ToString());
        }

        private static void DrawSpecs(YuspecRuntime runtime)
        {
            DrawSection("Specs");
            foreach (var compiled in runtime.CompiledSpecs)
            {
                EditorGUILayout.LabelField(compiled.SourceName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Entities: {compiled.Entities.Count}");
                EditorGUILayout.LabelField($"  Event Rules: {compiled.EventHandlers.Count}");
                EditorGUILayout.LabelField($"  Behaviors: {compiled.Behaviors.Count}");
                EditorGUILayout.LabelField($"  Scenarios: {compiled.Scenarios.Count}");
            }
        }

        private static void DrawDiagnostics(YuspecRuntime runtime)
        {
            DrawSection("Diagnostics");
            if (!runtime.Diagnostics.Any())
            {
                EditorGUILayout.LabelField("No diagnostics.");
                return;
            }

            foreach (var diagnostic in runtime.Diagnostics)
            {
                var style = diagnostic.severity == YuspecDiagnosticSeverity.Error
                    ? EditorStyles.helpBox
                    : EditorStyles.wordWrappedLabel;
                EditorGUILayout.LabelField(diagnostic.ToString(), style);
            }
        }

        private static void DrawEntities(YuspecRuntime runtime)
        {
            DrawSection("Entities");
            foreach (var entity in runtime.Entities.OrderBy(entity => entity.EntityId))
            {
                EditorGUILayout.LabelField($"{entity.EntityId} type={entity.EntityType} state={entity.CurrentState}");
                foreach (var property in entity.Properties.OrderBy(property => property.Key))
                {
                    EditorGUILayout.LabelField($"  {property.Key}", FormatValue(property.Value));
                }
            }
        }

        private static void DrawEvents(YuspecRuntime runtime)
        {
            DrawSection("Events");
            foreach (var yuspecEvent in runtime.RecentEvents.Reverse())
            {
                EditorGUILayout.LabelField(yuspecEvent.ToString(), EditorStyles.wordWrappedLabel);
            }

            DrawSection("Trace");
            foreach (var trace in runtime.TraceEntries.Reverse())
            {
                EditorGUILayout.LabelField(trace.ToString(), EditorStyles.wordWrappedLabel);
            }
        }

        private static void DrawActions(YuspecRuntime runtime)
        {
            DrawSection("Registered Actions");
            foreach (var action in runtime.ActionRegistry.RegisteredActions.OrderBy(action => action.Name))
            {
                EditorGUILayout.LabelField(action.ToString());
            }

            DrawSection("Parsed Event Handlers");
            foreach (var handler in runtime.CompiledSpecs.SelectMany(spec => spec.EventHandlers))
            {
                var target = string.IsNullOrWhiteSpace(handler.TargetType) ? string.Empty : $" with {handler.TargetType}";
                EditorGUILayout.LabelField($"{handler.EventName}{target} actions={handler.Actions.Count}");
            }
        }

        private static void DrawStateMachines(YuspecRuntime runtime)
        {
            DrawSection("State Machines");
            if (!runtime.StateMachineStatuses.Any())
            {
                EditorGUILayout.LabelField("No active state machine sessions.");
            }

            foreach (var status in runtime.StateMachineStatuses)
            {
                EditorGUILayout.LabelField($"{status.EntityId} [{status.BehaviorName}]", status.CurrentState);
                EditorGUILayout.LabelField("  Elapsed", status.StateElapsed.ToString("0.000"));
            }

            DrawSection("Behavior Definitions");
            foreach (var behavior in runtime.CompiledSpecs.SelectMany(spec => spec.Behaviors))
            {
                EditorGUILayout.LabelField($"{behavior.Name} for {behavior.EntityType}", EditorStyles.boldLabel);
                foreach (var state in behavior.States)
                {
                    EditorGUILayout.LabelField($"  state {state.Name}");
                }
            }
        }

        private static void DrawScenarios(YuspecRuntime runtime)
        {
            DrawSection("Scenario Results");
            if (!runtime.ScenarioResults.Any())
            {
                EditorGUILayout.LabelField("No scenario run yet. Use Run Scenarios button.");
            }

            foreach (var result in runtime.ScenarioResults)
            {
                var status = result.Passed ? "PASS" : "FAIL";
                EditorGUILayout.LabelField($"{status} - {result.Name}", EditorStyles.boldLabel);
                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    EditorGUILayout.LabelField($"  {result.Message}", EditorStyles.wordWrappedLabel);
                }
            }

            DrawSection("Scenario Definitions");
            foreach (var scenario in runtime.CompiledSpecs.SelectMany(spec => spec.Scenarios))
            {
                EditorGUILayout.LabelField(scenario.Name, EditorStyles.boldLabel);
                foreach (var step in scenario.GivenSteps)
                {
                    EditorGUILayout.LabelField($"  given {step.Text}");
                }

                foreach (var step in scenario.WhenSteps)
                {
                    EditorGUILayout.LabelField($"  when {step.Text}");
                }

                foreach (var step in scenario.ExpectSteps)
                {
                    EditorGUILayout.LabelField($"  expect {step.Text}");
                }
            }
        }

        private void DrawSettings(YuspecRuntime runtime)
        {
            DrawSection("Settings");
            EditorGUILayout.LabelField("Strict Mode", runtime.StrictMode ? "Enabled" : "Disabled");
            EditorGUILayout.LabelField("Hot Reload", runtime.HotReload ? "Enabled" : "Disabled");
            EditorGUILayout.LabelField("Hot Reload Poll Interval", $"{runtime.HotReloadPollInterval:0.00}s");
            autoRefresh = EditorGUILayout.Toggle("Auto Refresh", autoRefresh);
        }

        private static void DrawSection(string title)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is System.Collections.IEnumerable values && !(value is string))
            {
                return string.Join(", ", values.Cast<object>().Select(item => item?.ToString() ?? "null"));
            }

            return value.ToString();
        }

        private static YuspecRuntime FindRuntime()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<YuspecRuntime>();
#else
            return FindObjectOfType<YuspecRuntime>();
#endif
        }
    }
}
