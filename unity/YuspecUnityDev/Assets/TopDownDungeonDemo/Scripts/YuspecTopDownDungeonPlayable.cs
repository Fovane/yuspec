using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Yuspec.Unity;

public sealed class YuspecTopDownDungeonPlayable : MonoBehaviour
{
    [SerializeField] private bool runScenariosOnStart;
    [SerializeField] private float room2EnterX = 5.5f;
    [SerializeField] private float bossRoomEnterX = 13.5f;

    private YuspecRuntime runtime;
    private YuspecEntity player;
    private bool emittedRoom2Enter;
    private bool emittedBossRoomEnter;

    private void Start()
    {
        runtime = GetComponent<YuspecRuntime>();
        if (runtime == null)
        {
            runtime = gameObject.AddComponent<YuspecRuntime>();
        }

        CreatePrimitiveScene();
        SetRuntimeSpecs(runtime, LoadTopDownDungeonSpecs());
        runtime.Initialize();

        foreach (var entity in FindObjectsByType<YuspecEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            runtime.RegisterEntity(entity);
        }

        player = FindEntity("Player");
        HookOutput();
        ConfigureCamera();

        if (runScenariosOnStart)
        {
            foreach (var result in runtime.RunScenarios())
            {
                Debug.Log($"[TopDownDungeon Scenario] {result.Name}: {(result.Passed ? "Passed" : result.Message)}");
            }
        }
    }

    private void Update()
    {
        if (runtime == null || player == null)
        {
            return;
        }

        var x = ReadAxis(KeyCode.A, KeyCode.D);
        var y = ReadAxis(KeyCode.S, KeyCode.W);
        player.SetProperty("moveX", x);
        player.SetProperty("moveY", y);

        if (Mathf.Abs(x) > 0.01f || Mathf.Abs(y) > 0.01f)
        {
            runtime.Emit("Player.Move", player);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            var target = FindNearestInteractable();
            if (target != null)
            {
                runtime.Emit("Player.Interact", player, target);
            }
        }

        EmitRoomEntryEvents();
    }

    private void EmitRoomEntryEvents()
    {
        if (!emittedRoom2Enter && player.transform.position.x >= room2EnterX)
        {
            emittedRoom2Enter = true;
            runtime.Emit("Player.EnterRoom2", player, FindEntity("Room2"));
        }

        if (!emittedBossRoomEnter && player.transform.position.x >= bossRoomEnterX)
        {
            emittedBossRoomEnter = true;
            runtime.Emit("Player.EnterBossRoom", player, FindEntity("BossRoom"));
        }
    }

    private YuspecEntity FindNearestInteractable()
    {
        var range = ReadFloat(player, "interactRange", 1.6f);
        var origin = player.transform.position;
        return runtime.Entities
            .Where(entity => entity != null && entity != player && entity.gameObject.activeInHierarchy)
            .Select(entity => new { Entity = entity, Distance = Vector3.Distance(origin, entity.transform.position) })
            .Where(candidate => candidate.Distance <= range)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Entity)
            .FirstOrDefault();
    }

    private static UnityEngine.Object[] LoadTopDownDungeonSpecs()
    {
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Packages/com.yuspec.unity/Samples~/TopDownDungeon"));
        var names = new[] { "player.yuspec", "room1.yuspec", "room2.yuspec", "room3.yuspec", "dialogue.yuspec" };
        return names.Select(name =>
        {
            var path = Path.Combine(root, name);
            var asset = ScriptableObject.CreateInstance<YuspecSpecAsset>();
            asset.name = name;
            asset.SetSource(path, File.ReadAllText(path));
            return (UnityEngine.Object)asset;
        }).ToArray();
    }

    private static void SetRuntimeSpecs(YuspecRuntime targetRuntime, UnityEngine.Object[] specs)
    {
        typeof(YuspecRuntime)
            .GetField("specs", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(targetRuntime, specs);
    }

    private void HookOutput()
    {
        YuspecUnityActions.UiMessage += message => Debug.Log($"[TopDownDungeon UI] {message}");
        if (runtime.DialogueRuntime == null)
        {
            return;
        }

        runtime.DialogueRuntime.OnLine += (dialogue, speaker, line) =>
            Debug.Log($"[TopDownDungeon Dialogue:{dialogue}] {speaker?.EntityId ?? "Narrator"}: {line}");
        runtime.DialogueRuntime.OnChoice += (dialogue, speaker, text, target) =>
            Debug.Log($"[TopDownDungeon Dialogue:{dialogue}] Choice: {text} -> {target}");
        runtime.DialogueRuntime.OnEnd += (dialogue, speaker) =>
            Debug.Log($"[TopDownDungeon Dialogue:{dialogue}] End");
    }

    private void CreatePrimitiveScene()
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

        var target = GameObject.CreatePrimitive(primitiveType);
        target.name = id;
        target.transform.position = position;
        target.transform.localScale = scale;
        SetColor(target, color);

        if (color.a <= 0f)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        var entity = target.AddComponent<YuspecEntity>();
        entity.EntityId = id;
        entity.EntityType = type;
        return entity;
    }

    private static YuspecEntity FindEntity(string id)
    {
        return FindObjectsByType<YuspecEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None)
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
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
    }

    private static float ReadAxis(KeyCode negative, KeyCode positive)
    {
        var value = 0f;
        if (Input.GetKey(negative))
        {
            value -= 1f;
        }

        if (Input.GetKey(positive))
        {
            value += 1f;
        }

        return value;
    }

    private static float ReadFloat(YuspecEntity entity, string propertyName, float fallback)
    {
        if (entity.TryGetProperty(propertyName, out var value) && float.TryParse(value?.ToString(), out var parsed))
        {
            return parsed;
        }

        return fallback;
    }
}
