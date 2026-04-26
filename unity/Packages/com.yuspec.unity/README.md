# YUSPEC Unity Package

YUSPEC is a text-based gameplay rule layer for Unity.

This package is currently an early prototype.
The Door+Chest sample is the first supported runtime slice.
State machines, scenario tests, and Demo Dungeon are planned.

This package currently provides the Unity-facing scaffold:

- Runtime entity and event bridge components
- Reflection-based action registry
- Diagnostics model
- Spec asset importer
- Editor debugger window
- Sample `.yuspec` files

## Usage

1. Add `YuspecRuntime` to an empty GameObject.
2. Assign one or more `.yuspec` TextAssets to the runtime.
3. Add `YuspecEntity` to relevant GameObjects.
4. Set entity IDs and types to match the `.yuspec` file.
5. Emit gameplay events through `YuspecEventBridge` or `YuspecRuntime.Emit`.
6. Open `Window > YUSPEC > Debugger` to inspect runtime state.

## Working Slice

The Door+Chest subset currently covers entity properties, `on Actor.Event with
Target when ...:`, `Player.has(Door.key)`, `Target.state == Closed`, `set`,
`play_animation`, `play_sound`, `give`, strict diagnostics, and debugger trace.

## Notes

The package does not claim full DSL support yet.
Goblin AI, scenario tests, and the full Demo Dungeon remain roadmap work.
