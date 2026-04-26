# YUSPEC Unity Dev Project

This Unity project is the local development harness for the YUSPEC package.

Unity version:

```text
6000.3.8f1
```

The project consumes the package from:

```text
../Packages/com.yuspec.unity
```

## Open

```powershell
$UNITY_EDITOR = "<path-to-Unity.exe>"
& $UNITY_EDITOR -projectPath "<repo-root>\unity\YuspecUnityDev"
```

## Rebuild The Door+Chest Scene

In the Unity Editor:

```text
YUSPEC > Dev > Rebuild Door Example Scene
```

Or from PowerShell:

```powershell
& $UNITY_EDITOR -batchmode -quit -projectPath "<repo-root>\unity\YuspecUnityDev" -executeMethod Yuspec.Dev.Editor.YuspecDevSceneBuilder.RebuildDoorExampleSceneBatch -logFile "<repo-root>\unity\YuspecUnityDev-rebuild.log"
```

## Door+Chest Runtime Slice

Open `Assets/YuspecDev/Scenes/DoorExample.unity`, enter Play Mode, then open:

```text
Window > YUSPEC > Debugger
```

Expected result:

- `Player.Interact` event appears.
- The Door handler is matched.
- `Player.has(Door.key)` passes.
- `Door.state` becomes `Open`.
- The Chest handler is matched.
- `Chest.state == Closed` passes.
- `Chest.state` becomes `Open`.
- `Gold` is added to the Player inventory.
- `play_animation`, `play_sound`, and `give` appear in the debug trace.

## Batch Validation

```powershell
& $UNITY_EDITOR -batchmode -quit -projectPath "<repo-root>\unity\YuspecUnityDev" -executeMethod Yuspec.Dev.Editor.YuspecDevSceneBuilder.ValidateDoorExampleRuntimeBatch -logFile "<repo-root>\unity\YuspecUnityDev-validate.log"
```

For CI-style rebuild plus validation in one pass:

```powershell
& $UNITY_EDITOR -batchmode -quit -projectPath "<repo-root>\unity\YuspecUnityDev" -executeMethod Yuspec.Dev.Editor.YuspecDevSceneBuilder.RebuildAndValidateDoorExampleSceneBatch -logFile "<repo-root>\unity\YuspecUnityDev-rebuild-validate.log"
```

Expected log line:

```text
YUSPEC Door+Chest runtime validation passed: Door.state == Open, Chest.state == Open, Player has Gold.
```
