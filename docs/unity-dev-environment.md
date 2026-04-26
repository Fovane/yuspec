# Unity Dev Environment

YUSPEC has a local Unity development project for testing the package while the
language evolves.

Unity version:

```text
6000.3.8f1
```

Project path:

```text
unity/YuspecUnityDev
```

Package path:

```text
unity/Packages/com.yuspec.unity
```

The project references the package through `Packages/manifest.json`:

```json
"com.yuspec.unity": "file:../../Packages/com.yuspec.unity"
```

## Open The Project

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" -projectPath "C:\Users\yucel\OneDrive\LaptopDesktop\Yuspec Projesi\Main\unity\YuspecUnityDev"
```

## Rebuild Door Example Scene

Inside Unity:

```text
YUSPEC > Dev > Rebuild Door Example Scene
```

From PowerShell:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\yucel\OneDrive\LaptopDesktop\Yuspec Projesi\Main\unity\YuspecUnityDev" -executeMethod Yuspec.Dev.Editor.YuspecDevSceneBuilder.RebuildDoorExampleSceneBatch -logFile "C:\Users\yucel\OneDrive\LaptopDesktop\Yuspec Projesi\Main\unity\YuspecUnityDev-rebuild.log"
```

## Validate Runtime Slice

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\yucel\OneDrive\LaptopDesktop\Yuspec Projesi\Main\unity\YuspecUnityDev" -executeMethod Yuspec.Dev.Editor.YuspecDevSceneBuilder.ValidateDoorExampleRuntimeBatch -logFile "C:\Users\yucel\OneDrive\LaptopDesktop\Yuspec Projesi\Main\unity\YuspecUnityDev-validate.log"
```

Expected result:

```text
YUSPEC Door runtime validation passed: Door.state == Open.
```

## Manual Debugging

Open:

```text
Assets/YuspecDev/Scenes/DoorExample.unity
```

Enter Play Mode and open:

```text
Window > YUSPEC > Debugger
```

Expected debugger data:

- Loaded `DoorExample.yuspec`
- Parsed `Player.Interact with Door` handler
- Recent `Player.Interact` event
- Passed `Player.has(Door.key)` condition
- `Door.state = Open`
- `play_animation` and `play_sound` action trace

## Git Hygiene

Commit Unity assets, project settings, package files, and `.meta` files.

Do not commit generated folders:

- `Library/`
- `Logs/`
- `Temp/`
- `UserSettings/`
- `Obj/`
