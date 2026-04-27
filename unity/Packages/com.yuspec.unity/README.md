# YUSPEC Unity Package

YUSPEC is a text-based gameplay rule layer for Unity.

Tagline: Write gameplay rules, not script spaghetti.

This package targets the v1.0 workflow:

- Install package
- Add `YuspecRuntime` to scene
- Attach `YuspecEntity` to GameObjects
- Author `.yuspec` files for gameplay rules
- Bind custom C# actions via `[YuspecAction]`
- Debug events, entities, actions, states, and scenarios
- Run scenario checks from debugger
- Hot reload changed assigned specs while iterating

This package currently provides the Unity-facing scaffold:

- Runtime entity and event bridge components
- Event-rule execution runtime
- Reflection-based action registry
- Strict diagnostics
- Behavior/state machine parsing and runtime
- Scenario parser and runner
- Poll-based hot reload for assigned spec assets
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

## Supported v1 Syntax Areas

- Entity declarations and properties
- Event handlers with optional `with` and `when`
- Action calls and `set` assignments
- Behavior/state blocks with transitions and `every` blocks
- Scenario blocks with `given`, `when`, and `expect`
- `//` and `#` comments

## Hot Reload

`YuspecRuntime` can poll assigned `TextAsset` and `YuspecSpecAsset` entries for
content changes. When a changed spec is detected, the runtime reloads specs,
re-applies declarations to registered entities, rebuilds state machine sessions,
and emits diagnostic `YSP0600`.

This is a v1 subset. It is not a full live migration system and does not
preserve scenario results or trace history across reload.

## Notes

YUSPEC is delivered through stable vertical slices.
If a feature is not implemented, it should appear as a diagnostic or documented
limitation, not as a silent no-op.
