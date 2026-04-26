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
& "C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" -projectPath "C:\Users\yucel\OneDrive\LaptopDesktop\Yuspec Projesi\Main\unity\YuspecUnityDev"
```

## Rebuild The Door+Chest Scene

In the Unity Editor:

```text
YUSPEC > Dev > Rebuild Door Example Scene
```

Or from PowerShell:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\yucel\OneDrive\LaptopDesktop\Yuspec Projesi\Main\unity\YuspecUnityDev" -executeMethod Yuspec.Dev.Editor.YuspecDevSceneBuilder.RebuildDoorExampleSceneBatch -logFile "C:\Users\yucel\OneDrive\LaptopDesktop\Yuspec Projesi\Main\unity\YuspecUnityDev-rebuild.log"
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
& "C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\yucel\OneDrive\LaptopDesktop\Yuspec Projesi\Main\unity\YuspecUnityDev" -executeMethod Yuspec.Dev.Editor.YuspecDevSceneBuilder.ValidateDoorExampleRuntimeBatch -logFile "C:\Users\yucel\OneDrive\LaptopDesktop\Yuspec Projesi\Main\unity\YuspecUnityDev-validate.log"
```

For CI-style rebuild plus validation in one pass:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\yucel\OneDrive\LaptopDesktop\Yuspec Projesi\Main\unity\YuspecUnityDev" -executeMethod Yuspec.Dev.Editor.YuspecDevSceneBuilder.RebuildAndValidateDoorExampleSceneBatch -logFile "C:\Users\yucel\OneDrive\LaptopDesktop\Yuspec Projesi\Main\unity\YuspecUnityDev-rebuild-validate.log"
```

Expected log line:

```text
YUSPEC Door+Chest runtime validation passed: Door.state == Open, Chest.state == Open, Player has Gold.
```
