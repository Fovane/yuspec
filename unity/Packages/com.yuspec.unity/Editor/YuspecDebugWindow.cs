using System.Linq;
using Yuspec.Unity;
using UnityEditor;
using UnityEngine;

namespace Yuspec.Unity.Editor
{
    public sealed class YuspecDebugWindow : EditorWindow
    {
        private Vector2 scroll;

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
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                {
                    Repaint();
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (runtime == null)
            {
                EditorGUILayout.HelpBox("No YuspecRuntime found in the open scene.", MessageType.Info);
                DrawPlaceholderSections();
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawLoadedSpecs(runtime);
            DrawDiagnostics(runtime);
            DrawRegisteredActions(runtime);
            DrawSceneEntities(runtime);
            DrawRecentEvents(runtime);
            DrawCurrentStates(runtime);

            EditorGUILayout.EndScrollView();
        }

        private static void DrawLoadedSpecs(YuspecRuntime runtime)
        {
            DrawSection("Loaded Specs");
            foreach (var spec in runtime.Specs.Where(spec => spec != null))
            {
                EditorGUILayout.LabelField(spec.name);
            }
        }

        private static void DrawDiagnostics(YuspecRuntime runtime)
        {
            DrawSection("Diagnostics");
            foreach (var diagnostic in runtime.Diagnostics)
            {
                EditorGUILayout.LabelField(diagnostic.ToString(), EditorStyles.wordWrappedLabel);
            }
        }

        private static void DrawRegisteredActions(YuspecRuntime runtime)
        {
            DrawSection("Registered Actions");
            foreach (var action in runtime.ActionRegistry.RegisteredActions.OrderBy(action => action.Name))
            {
                EditorGUILayout.LabelField(action.ToString());
            }
        }

        private static void DrawSceneEntities(YuspecRuntime runtime)
        {
            DrawSection("Scene Entities");
            foreach (var entity in runtime.Entities.OrderBy(entity => entity.EntityId))
            {
                EditorGUILayout.LabelField($"{entity.EntityId} type={entity.EntityType} state={entity.CurrentState}");
            }
        }

        private static void DrawRecentEvents(YuspecRuntime runtime)
        {
            DrawSection("Recent Events");
            foreach (var yuspecEvent in runtime.RecentEvents.Reverse())
            {
                EditorGUILayout.LabelField(yuspecEvent.ToString(), EditorStyles.wordWrappedLabel);
            }
        }

        private static void DrawCurrentStates(YuspecRuntime runtime)
        {
            DrawSection("Current States");
            foreach (var entity in runtime.Entities.Where(entity => !string.IsNullOrEmpty(entity.CurrentState)))
            {
                EditorGUILayout.LabelField(entity.EntityId, entity.CurrentState);
            }
        }

        private static void DrawPlaceholderSections()
        {
            DrawSection("Loaded Specs");
            EditorGUILayout.LabelField("Waiting for a YuspecRuntime.");
            DrawSection("Diagnostics");
            EditorGUILayout.LabelField("No runtime diagnostics.");
            DrawSection("Registered Actions");
            EditorGUILayout.LabelField("No runtime action registry.");
            DrawSection("Scene Entities");
            EditorGUILayout.LabelField("No scene entities.");
            DrawSection("Recent Events");
            EditorGUILayout.LabelField("No events.");
            DrawSection("Current States");
            EditorGUILayout.LabelField("No states.");
        }

        private static void DrawSection(string title)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
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
