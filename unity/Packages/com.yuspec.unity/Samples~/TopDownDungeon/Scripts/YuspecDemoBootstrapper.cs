using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Yuspec.Unity;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Yuspec.Unity.Samples.TopDownDungeon
{
    public sealed class YuspecDemoBootstrapper : MonoBehaviour
    {
        [SerializeField] private YuspecRuntime runtime;
        [SerializeField] private UnityEngine.Object[] specFiles;
        [SerializeField] private bool autoCreatePrimitiveScene = true;
        [SerializeField] private bool runScenariosOnStart = false;

        private void Start()
        {
            runtime = runtime != null ? runtime : FindRuntime();
            if (runtime == null)
            {
                runtime = new GameObject("YUSPEC Runtime").AddComponent<YuspecRuntime>();
            }

            if (autoCreatePrimitiveScene)
            {
                CreatePrimitiveScene();
            }

            if (specFiles == null || specFiles.Length == 0)
            {
                specFiles = LoadDefaultSpecFiles();
            }

            SetRuntimeSpecs(runtime, specFiles);
            runtime.Initialize();

            foreach (var entity in FindObjectsOfTypeAll<YuspecEntity>())
            {
                runtime.RegisterEntity(entity);
            }

            EnsureInput(runtime);
            ConfigureCamera();
            HookDemoOutput(runtime);

            if (runScenariosOnStart)
            {
                var results = runtime.RunScenarios();
                foreach (var result in results)
                {
                    Debug.Log($"[YUSPEC Demo Scenario] {result.Name}: {(result.Passed ? "Passed" : result.Message)}");
                }
            }
        }

        private static void SetRuntimeSpecs(YuspecRuntime targetRuntime, UnityEngine.Object[] specs)
        {
            var field = typeof(YuspecRuntime).GetField("specs", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                global::Yuspec.YuspecDiagnosticReporter.Report("YuspecDemoBootstrapper.cs", 1, 1, "Could not find YuspecRuntime specs field.");
                return;
            }

            field.SetValue(targetRuntime, specs ?? Array.Empty<UnityEngine.Object>());
        }

        private static YuspecRuntime FindRuntime()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<YuspecRuntime>();
#else
            return FindObjectOfType<YuspecRuntime>();
#endif
        }

        private static YuspecEntity[] FindObjectsOfTypeAll<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<YuspecEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            return FindObjectsOfType<YuspecEntity>();
#endif
        }

        private static UnityEngine.Object[] LoadDefaultSpecFiles()
        {
#if UNITY_EDITOR
            var paths = new[]
            {
                "Packages/com.yuspec.unity/Samples~/TopDownDungeon/player.yuspec",
                "Packages/com.yuspec.unity/Samples~/TopDownDungeon/room1.yuspec",
                "Packages/com.yuspec.unity/Samples~/TopDownDungeon/room2.yuspec",
                "Packages/com.yuspec.unity/Samples~/TopDownDungeon/room3.yuspec",
                "Packages/com.yuspec.unity/Samples~/TopDownDungeon/dialogue.yuspec"
            };

            return paths
                .Select(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>)
                .Where(asset => asset != null)
                .ToArray();
#else
            return Array.Empty<UnityEngine.Object>();
#endif
        }

        private static void EnsureInput(YuspecRuntime targetRuntime)
        {
            var player = FindEntity("Player");
            if (player == null)
            {
                return;
            }

            var input = player.GetComponent<YuspecDemoInput>();
            if (input == null)
            {
                input = player.gameObject.AddComponent<YuspecDemoInput>();
            }

            input.Configure(targetRuntime, player);
        }

        private static void HookDemoOutput(YuspecRuntime targetRuntime)
        {
            YuspecUnityActions.UiMessage += message => Debug.Log($"[YUSPEC UI] {message}");
            if (targetRuntime.DialogueRuntime == null)
            {
                return;
            }

            targetRuntime.DialogueRuntime.OnLine += (dialogue, speaker, line) =>
                Debug.Log($"[YUSPEC Dialogue:{dialogue}] {speaker?.EntityId ?? "Narrator"}: {line}");
            targetRuntime.DialogueRuntime.OnChoice += (dialogue, speaker, text, target) =>
                Debug.Log($"[YUSPEC Dialogue:{dialogue}] Choice: {text} -> {target}");
            targetRuntime.DialogueRuntime.OnEnd += (dialogue, speaker) =>
                Debug.Log($"[YUSPEC Dialogue:{dialogue}] End");
        }

        private static void CreatePrimitiveScene()
        {
            CreateFloor("Room1Floor", new Vector3(0f, -0.05f, 0f), Color.gray);
            CreateFloor("Room2Floor", new Vector3(8f, -0.05f, 0f), new Color(0.35f, 0.35f, 0.45f));
            CreateFloor("BossRoomFloor", new Vector3(16f, -0.05f, 0f), new Color(0.45f, 0.35f, 0.35f));

            CreateEntity("Player", "Player", PrimitiveType.Capsule, new Vector3(-2.5f, 0.5f, 0f), Color.cyan);
            CreateEntity("Chest", "Chest", PrimitiveType.Cube, new Vector3(1.5f, 0.5f, 1.5f), Color.yellow);
            CreateEntity("Merchant", "Merchant", PrimitiveType.Capsule, new Vector3(-1.5f, 0.5f, 1.7f), Color.magenta);
            CreateEntity("DoorRoom1ToRoom2", "DoorRoom1ToRoom2", PrimitiveType.Cube, new Vector3(4f, 0.8f, 0f), Color.red, new Vector3(0.35f, 1.6f, 2.2f));
            CreateEntity("Room2", "Room2", PrimitiveType.Cube, new Vector3(8f, 0.05f, -2.7f), Color.clear, new Vector3(5.5f, 0.1f, 0.2f));
            CreateEntity("Goblin", "Goblin", PrimitiveType.Capsule, new Vector3(8f, 0.5f, 0f), Color.green);
            CreateEntity("DoorRoom2ToBossRoom", "DoorRoom2ToBossRoom", PrimitiveType.Cube, new Vector3(12f, 0.8f, 0f), Color.red, new Vector3(0.35f, 1.6f, 2.2f));
            CreateEntity("BossRoom", "BossRoom", PrimitiveType.Cube, new Vector3(16f, 0.05f, -2.7f), Color.clear, new Vector3(5.5f, 0.1f, 0.2f));
            CreateEntity("Boss", "Boss", PrimitiveType.Capsule, new Vector3(16f, 0.7f, 0f), Color.black, new Vector3(1.4f, 1.4f, 1.4f));
            CreateEntity("ExitDoor", "ExitDoor", PrimitiveType.Cube, new Vector3(19f, 0.8f, 0f), Color.red, new Vector3(0.35f, 1.6f, 2.2f));
        }

        private static void CreateFloor(string name, Vector3 position, Color color)
        {
            if (GameObject.Find(name) != null)
            {
                return;
            }

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name;
            floor.transform.position = position;
            floor.transform.localScale = new Vector3(6f, 0.1f, 5f);
            SetColor(floor, color);
        }

        private static YuspecEntity CreateEntity(string id, string type, PrimitiveType primitiveType, Vector3 position, Color color)
        {
            return CreateEntity(id, type, primitiveType, position, color, Vector3.one);
        }

        private static YuspecEntity CreateEntity(string id, string type, PrimitiveType primitiveType, Vector3 position, Color color, Vector3 scale)
        {
            var existing = FindEntity(id);
            if (existing != null)
            {
                return existing;
            }

            var gameObject = GameObject.CreatePrimitive(primitiveType);
            gameObject.name = id;
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            SetColor(gameObject, color);

            var entity = gameObject.AddComponent<YuspecEntity>();
            entity.EntityId = id;
            entity.EntityType = type;
            return entity;
        }

        private static YuspecEntity FindEntity(string id)
        {
            return FindObjectsOfTypeAll<YuspecEntity>()
                .FirstOrDefault(entity => string.Equals(entity.EntityId, id, StringComparison.OrdinalIgnoreCase));
        }

        private static void SetColor(GameObject target, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer != null && color.a > 0f)
            {
                renderer.material.color = color;
            }
        }

        private static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }

            camera.orthographic = true;
            camera.orthographicSize = 6.5f;
            camera.transform.position = new Vector3(8f, 14f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
