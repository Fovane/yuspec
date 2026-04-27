# YUSPEC Unity Package

YUSPEC is a text-based gameplay rule layer for Unity.

Tagline: Write gameplay rules, not script spaghetti.

Current status: `YUSPEC Unity v1.1.0 Public Preview`.

Install via Unity Package Manager:

```json
"com.yuspec.unity": "https://github.com/Fovane/yuspec.git?path=/unity/Packages/com.yuspec.unity#v1.1.0"
```

Release notes: [docs/releases/v1.1.0.md](../../../docs/releases/v1.1.0.md)

Root README: [README.md](../../../README.md)

This package targets the `v1.1.0 Public Preview` workflow:

- Install package
- Add `YuspecRuntime` to scene
- Attach `YuspecEntity` to GameObjects
- Author `.yuspec` files for gameplay rules
- Bind custom C# actions via `[YuspecAction]`
- Use typed entity properties and typed action arguments
- Bind initial values from ScriptableObject assets with `from`
- Add lightweight dialogue blocks through `start_dialogue`
- Debug events, entities, actions, states, and scenarios
- Run scenario checks from debugger
- Hot reload changed `.yuspec` files while preserving entity values

This package currently provides the Unity-facing scaffold:

- Runtime entity and event bridge components
- Event-rule execution runtime
- Reflection-based action registry
- Strict diagnostics with clickable Unity Console output
- Typed property validation
- Static analysis for cycles, repeated re-trigger loops, and unreachable states
- ScriptableObject binding through `from` and `[YuspecMutable]`
- Lightweight dialogue runtime
- Behavior/state machine parsing and runtime
- Scenario parser and runner
- FileSystemWatcher-based hot reload
- Spec asset importer
- Editor debugger tabs
- Sample `.yuspec` files
- Runtime and editor tests

## Usage

1. Add `YuspecRuntime` to an empty GameObject.
2. Assign one or more `.yuspec` TextAssets to the runtime.
3. Add `YuspecEntity` to relevant GameObjects.
4. Set entity IDs and types to match the `.yuspec` file.
5. Emit gameplay events through `YuspecEventBridge` or `YuspecRuntime.Emit`.
6. Open `Window > YUSPEC > Debugger`.
7. Use tabs to inspect overview, specs, diagnostics, entities, events, actions,
   state machines, scenarios, and settings.
8. Use `Run Scenarios` to execute parsed scenario blocks.

## Quick Setup

1. Add the package through Unity Package Manager or `Packages/manifest.json`.
2. Add `YuspecRuntime` to an empty GameObject.
3. Assign one or more `.yuspec` TextAssets to the runtime.
4. Add `YuspecEntity` to gameplay GameObjects.
5. Set entity IDs/types to match the `.yuspec` file.
6. Emit gameplay events through `YuspecEventBridge` or `YuspecRuntime.Emit`.
7. Open `Window > YUSPEC > Debugger`.

## Door Example First Steps

1. Open `Samples~/DoorExample`.
2. Assign `DoorExample.yuspec` to `YuspecRuntime`.
3. Add `Player`, `Door`, and `Chest` entities to the scene.
4. Trigger `Player.Interact` through the runtime or bridge component.
5. Open `Window > YUSPEC > Debugger` and confirm the event trace and state updates.

## Top-Down Dungeon Demo

`Samples~/TopDownDungeon` is a primitive-only showcase demo for v1.1.0. It keeps
gameplay rules in `.yuspec` files and uses C# only for Unity input, primitive
scene setup, and bound action execution.

Included specs:

- `player.yuspec`
- `room1.yuspec`
- `room2.yuspec`
- `room3.yuspec`
- `dialogue.yuspec`

Included scripts:

- `YuspecDemoBootstrapper.cs`
- `YuspecDemoInput.cs`

## Pure C# Comparison Demo

`Samples~/PureCSharpDungeon` implements the same primitive dungeon without
YUSPEC. It exists as a comparison sample: the same chest, door, dialogue,
Goblin, Boss, and scenario logic is encoded directly in C# branches, timers, and
state mutation methods.

Use it beside `TopDownDungeon` when evaluating what YUSPEC removes from ordinary
Unity gameplay scripts.

## Bind A Custom Action

```csharp
[YuspecAction("play_animation")]
public void PlayAnimation(YuspecEntity target, string animationName)
{
    target.GetComponent<Animator>().Play(animationName);
}
```

Custom actions are discovered from loaded assemblies and invoked by name.

## Supported v1.1 Syntax Areas

- Entity declarations and typed properties
- Event handlers with optional `with` and `when`
- Action calls and `set` assignments
- Behavior/state blocks with transitions and `every` blocks
- Scenario blocks with `given`, `when`, and `expect`
- ScriptableObject binding with `from`
- Dialogue blocks with `line`, `choice`, `end`, and `start_dialogue`
- `//` and `#` comments

## Known Limitations

- YUSPEC Unity v1.1.0 is a public preview release.
- State machine support is a working subset.
- Scenario tests are a working subset.
- Hot reload does not attempt live scene migration.
- The package has been validated on the documented Unity version, but wider Unity version coverage still needs community testing.
- It is not a replacement for Unity, all C#, physics systems, networking internals, shaders, animation authoring, or visual node editing.

## Hot Reload

`YuspecRuntime` watches `.yuspec` files for save events and queues reload work
back onto Unity's main thread. When a changed spec is detected, the runtime
re-parses and re-validates the changed file, updates affected handlers and state
machines, preserves current entity property values, and logs the reload result.

This is not a full live scene migration system and does not preserve scenario
results or trace history across reload.

## Notes

YUSPEC is delivered through stable vertical slices.
If a feature is not implemented, it should appear as a diagnostic or documented
limitation, not as a silent no-op.
