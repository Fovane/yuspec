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
$UNITY_EDITOR = "<path-to-Unity.exe>"
& $UNITY_EDITOR -projectPath "<repo-root>\unity\YuspecUnityDev"
```

## Rebuild Door+Chest Example Scene

Inside Unity:

```text
YUSPEC > Dev > Rebuild Door Example Scene
```

From PowerShell:

```powershell
& $UNITY_EDITOR -batchmode -quit -projectPath "<repo-root>\unity\YuspecUnityDev" -executeMethod Yuspec.Dev.Editor.YuspecDevSceneBuilder.RebuildDoorExampleSceneBatch -logFile "<repo-root>\unity\YuspecUnityDev-rebuild.log"
```

## Validate Runtime Slice

```powershell
& $UNITY_EDITOR -batchmode -quit -projectPath "<repo-root>\unity\YuspecUnityDev" -executeMethod Yuspec.Dev.Editor.YuspecDevSceneBuilder.ValidateDoorExampleRuntimeBatch -logFile "<repo-root>\unity\YuspecUnityDev-validate.log"
```

For CI-style rebuild plus validation in one pass:

```powershell
& $UNITY_EDITOR -batchmode -quit -projectPath "<repo-root>\unity\YuspecUnityDev" -executeMethod Yuspec.Dev.Editor.YuspecDevSceneBuilder.RebuildAndValidateDoorExampleSceneBatch -logFile "<repo-root>\unity\YuspecUnityDev-rebuild-validate.log"
```

Expected result:

```text
YUSPEC Door+Chest runtime validation passed: Door.state == Open, Chest.state == Open, Player has Gold.
```

## Run Package Tests

The dev project enables `com.unity.test-framework` and marks
`com.yuspec.unity` as testable.

```powershell
& $UNITY_EDITOR -batchmode -projectPath "<repo-root>\unity\YuspecUnityDev" -runTests -testPlatform editmode -testResults "<repo-root>\unity\YuspecUnityDev-editmode-results.xml" -logFile "<repo-root>\unity\YuspecUnityDev-editmode.log"
```

Expected v1.0 result:

```text
testcasecount="10" result="Passed" total="10" passed="10"
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
- Parsed `Player.Interact with Chest` handler
- Recent `Player.Interact` event
- Passed `Player.has(Door.key)` condition
- `Door.state = Open`
- Passed `Chest.state == Closed` condition
- `Chest.state = Open`
- `Player.inventory = IronKey, Gold`
- `play_animation`, `play_sound`, and `give` action trace

## Hot Reload

`YuspecRuntime` has a polling-based hot reload subset. When hot reload is
enabled, the runtime checks assigned `TextAsset` or `YuspecSpecAsset` content
for changes and reloads specs when the content hash changes.

Current scope:

- Reloads assigned spec assets.
- Re-applies declarations to registered entities.
- Rebuilds state machine sessions.
- Records diagnostic `YSP0600`.

Current limits:

- It does not preserve scenario results or trace history across reload.
- It does not discover newly created scene entities by itself.
- It does not support a full dependency graph or live migration model yet.

## Git Hygiene

Commit Unity assets, project settings, package files, and `.meta` files.

Do not commit generated folders:

- `Library/`
- `Logs/`
- `Temp/`
- `UserSettings/`
- `Obj/`
