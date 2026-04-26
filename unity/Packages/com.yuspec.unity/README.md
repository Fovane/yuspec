# YUSPEC Unity Package

YUSPEC is a text-based gameplay rule layer for Unity.

This package currently provides the Unity-facing scaffold:

- Runtime entity and event bridge components
- Reflection-based action registry
- Diagnostics model
- Spec asset importer
- Editor debugger window
- Sample `.yuspec` files

The Door-style subset is implemented as the first runtime slice: entity
properties, `on Actor.Event with Target when ...:`, `Player.has(Door.key)`,
`set`, `play_animation`, `play_sound`, and debugger trace.

State machines, scenarios, and the full Demo Dungeon remain roadmap work.

Open the debugger from:

```text
Window > YUSPEC > Debugger
```
