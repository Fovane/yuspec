using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yuspec.Dev;
using Yuspec.Unity;

namespace Yuspec.Dev.Editor
{
    public static class YuspecDevSceneBuilder
    {
        private const string ScenePath = "Assets/YuspecDev/Scenes/DoorExample.unity";
        private const string SpecPath = "Assets/YuspecDev/DoorExample.yuspec";

        [MenuItem("YUSPEC/Dev/Rebuild Door Example Scene")]
        public static void RebuildDoorExampleScene()
        {
            RebuildDoorExampleScene(validateAfterBuild: false);
        }

        private static void RebuildDoorExampleScene(bool validateAfterBuild)
        {
            Directory.CreateDirectory("Assets/YuspecDev/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DoorExample";

            var runtimeObject = new GameObject("YUSPEC Runtime");
            var runtime = runtimeObject.AddComponent<YuspecRuntime>();
            AssignRuntimeSpec(runtime);

            var playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerObject.name = "Player";
            playerObject.transform.position = new Vector3(-1.5f, 1f, 0f);
            var player = playerObject.AddComponent<YuspecEntity>();
            player.EntityId = "Player";
            player.EntityType = "Player";

            var doorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorObject.name = "Door";
            doorObject.transform.position = new Vector3(1.5f, 1f, 0f);
            doorObject.transform.localScale = new Vector3(1f, 2f, 0.25f);
            var door = doorObject.AddComponent<YuspecEntity>();
            door.EntityId = "Door";
            door.EntityType = "Door";

            var chestObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chestObject.name = "Chest";
            chestObject.transform.position = new Vector3(3.25f, 0.65f, 0f);
            chestObject.transform.localScale = new Vector3(1.1f, 0.7f, 0.85f);
            var chest = chestObject.AddComponent<YuspecEntity>();
            chest.EntityId = "Chest";
            chest.EntityType = "Chest";

            var driverObject = new GameObject("Door Example Driver");
            var driver = driverObject.AddComponent<DoorExampleDriver>();
            AssignDriverReferences(driver, runtime, player, door, chest);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(1.1f, 2.7f, -6.5f);
            cameraObject.transform.rotation = Quaternion.Euler(19f, 0f, 0f);
            cameraObject.AddComponent<Camera>();

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            lightObject.AddComponent<Light>().type = LightType.Directional;

            if (validateAfterBuild)
            {
                ValidateCurrentDoorExampleScene();
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"YUSPEC Door example scene rebuilt at {ScenePath}");
        }

        public static void RebuildDoorExampleSceneBatch()
        {
            RebuildDoorExampleScene();
        }

        public static void RebuildAndValidateDoorExampleSceneBatch()
        {
            RebuildDoorExampleScene(validateAfterBuild: true);
        }

        public static void ValidateDoorExampleRuntimeBatch()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new FileNotFoundException($"Could not open scene at {ScenePath}");
            }

            ValidateCurrentDoorExampleScene();
        }

        private static void ValidateCurrentDoorExampleScene()
        {
#if UNITY_2023_1_OR_NEWER
            var runtime = Object.FindFirstObjectByType<YuspecRuntime>();
            var entities = Object.FindObjectsByType<YuspecEntity>(FindObjectsSortMode.None);
#else
            var runtime = Object.FindObjectOfType<YuspecRuntime>();
            var entities = Object.FindObjectsOfType<YuspecEntity>();
#endif
            if (runtime == null)
            {
                throw new MissingReferenceException("Door scene has no YuspecRuntime.");
            }

            YuspecEntity player = null;
            YuspecEntity door = null;
            YuspecEntity chest = null;
            foreach (var entity in entities)
            {
                if (entity.EntityType == "Player")
                {
                    player = entity;
                }
                else if (entity.EntityType == "Door")
                {
                    door = entity;
                }
                else if (entity.EntityType == "Chest")
                {
                    chest = entity;
                }
            }

            if (player == null || door == null || chest == null)
            {
                throw new MissingReferenceException("Door scene must contain Player, Door, and Chest YuspecEntity components.");
            }

            runtime.RegisterEntity(player);
            runtime.RegisterEntity(door);
            runtime.RegisterEntity(chest);
            runtime.Initialize();
            player.SetProperty("inventory", new List<string> { "IronKey" });
            runtime.Emit("Player.Interact", player, door);
            runtime.Emit("Player.Interact", player, chest);

            if (!door.TryGetProperty("state", out var state) || state?.ToString() != "Open")
            {
                throw new System.InvalidOperationException($"Expected Door.state to be Open, got '{state ?? "null"}'.");
            }

            if (!chest.TryGetProperty("state", out var chestState) || chestState?.ToString() != "Open")
            {
                throw new System.InvalidOperationException($"Expected Chest.state to be Open, got '{chestState ?? "null"}'.");
            }

            if (!player.HasValue("Gold"))
            {
                throw new System.InvalidOperationException("Expected Player inventory to contain Gold after opening the Chest.");
            }

            Debug.Log("YUSPEC Door+Chest runtime validation passed: Door.state == Open, Chest.state == Open, Player has Gold.");
        }

        private static void AssignRuntimeSpec(YuspecRuntime runtime)
        {
            var specAsset = AssetDatabase.LoadAssetAtPath<Object>(SpecPath);
            if (specAsset == null)
            {
                Debug.LogError($"Could not load YUSPEC spec at {SpecPath}");
                return;
            }

            var serializedRuntime = new SerializedObject(runtime);
            var specsProperty = serializedRuntime.FindProperty("specs");
            specsProperty.arraySize = 1;
            specsProperty.GetArrayElementAtIndex(0).objectReferenceValue = specAsset;
            serializedRuntime.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignDriverReferences(
            DoorExampleDriver driver,
            YuspecRuntime runtime,
            YuspecEntity player,
            YuspecEntity door,
            YuspecEntity chest)
        {
            var serializedDriver = new SerializedObject(driver);
            serializedDriver.FindProperty("runtime").objectReferenceValue = runtime;
            serializedDriver.FindProperty("player").objectReferenceValue = player;
            serializedDriver.FindProperty("door").objectReferenceValue = door;
            serializedDriver.FindProperty("chest").objectReferenceValue = chest;
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
