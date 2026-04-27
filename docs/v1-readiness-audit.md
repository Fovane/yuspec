# YUSPEC Unity v1.0.1 Public Preview Readiness Audit

Date: 2026-04-27

Scope: `unity/Packages/com.yuspec.unity`

This audit checks what the Unity package actually supports today versus what is
documented as planned or limited. It reflects the package after the hot reload
and event bridge fixes.

## Verification Summary

Unity execution was verified with Unity `6000.3.8f1` through the local
development project.

Commands/results:

- Unity EditMode tests: `12/12 passed`
- Door+Chest scene validation: passed
- Door+Chest validation message:

```text
YUSPEC Door+Chest runtime validation passed: Door.state == Open, Chest.state == Open, Player has Gold.
```

## Package Structure

Status: ready.

The package contains the expected UPM structure:

- `package.json`: present, valid package manifest, version `1.0.1`
- `Runtime/Yuspec.Runtime.asmdef`: present
- `Editor/Yuspec.Editor.asmdef`: present, Editor-only
- `README.md`: present
- `CHANGELOG.md`: present
- `LICENSE.md`: present
- `Samples~`: present
- `Tests/Runtime`: present
- `Tests/Editor`: present
- `Documentation~`: present

## Feature Classification

| Feature | Status | Evidence | Notes |
|---|---|---|---|
| Entity declarations | Working subset | `YuspecSpecParser`, `YuspecRuntime`, runtime tests | Supports simple entity blocks and literal/list properties. |
| Event handlers | Working subset | Parser/runtime/tests/Door sample | Supports `on Actor.Event`, optional `with Target`, optional `when`. No function-style event names. |
| Conditions | Working subset | Parser/runtime/tests/samples | Supports `Actor.has(Target.property)` and `Entity.property == value`. State transition comparisons support `self.property` operators. |
| Action binding | Working subset | `YuspecActionRegistry`, tests | Reflection discovery, duplicate detection, count/type conversion diagnostics. |
| Built-in `set` | Working | Runtime tests and Door sample | `set Entity.property = value` is runtime-native. |
| State machines | Working subset | Parser/runtime/tests/Goblin sample | Supports `behavior`, `state`, `on enter`, `on exit`, `do`, `every`, event transitions, and simple `self` comparisons. |
| Scenario tests | Working subset | Parser/runtime/tests/Door sample | Supports `given Entity has Item`, `given Entity property = value`, `when Actor.Event Target`, and `expect Entity.property == value`. |
| Debugger trace | Working subset | `YuspecDebugWindow` | Shows real runtime specs, diagnostics, entities, events, trace, actions, state machines, scenarios, settings. |
| Demo Dungeon | Working sample | `Samples~/DemoDungeon` | Syntax is supported and required default bridge actions exist. It remains a sample, not a full playable dungeon scene. |
| Hot reload | Working subset | `YuspecRuntime.ReloadSpecsIfChanged`, tests | Polls assigned `TextAsset`/`YuspecSpecAsset` content hashes and reloads changed specs. Not a full live migration system. |
| Spec importer | Working subset | `YuspecSpecImporter` | Imports `.yuspec` files as `YuspecSpecAsset`. |
| Unity event bridge | Working subset | `YuspecEventBridge`, tests | Emits parser-compatible event names such as `Player.EnterBossRoom` and `Boss.HealthBelow`. |

## Hot Reload Scope

Hot reload is implemented as a polling-based subset.

What works:

- Detects content changes for assigned `TextAsset` and `YuspecSpecAsset` specs.
- Reloads specs when content hashes change.
- Re-applies declarations to already registered entities.
- Rebuilds state machine sessions.
- Emits diagnostic `YSP0600`.
- Exposes status in the debugger settings tab.

Current limits:

- Does not preserve scenario results or trace history across reload.
- Does not discover newly created scene entities by itself.
- Does not perform semantic migration of active state machine sessions.
- Does not watch arbitrary files outside the assigned runtime spec list.

Classification: Working subset.

## Sample Audit

### DoorExample

Status: ready.

- `.yuspec` syntax is supported.
- Required actions exist: `set`, `play_animation`, `play_sound`, `give`.
- Runtime supports the conditions used.
- README explains setup and limitations.
- Covered by runtime tests and scene validation.

### GoblinAI

Status: working subset.

- `behavior`, `state`, `on enter`, `every`, transitions, `do` block syntax are supported.
- Required actions exist as default bridge stubs: `play_animation`, `move_towards`, `damage`, `spawn`, `destroy`.
- Runtime state machine transition behavior has direct test coverage.
- The sample demonstrates orchestration and debug visibility, not production AI movement.

### DemoDungeon

Status: working sample.

- Event rules, conditions, state machine syntax, and scenarios are supported.
- Required default actions exist.
- Boss and quest flows use parser-compatible event names.
- README explains setup.
- It is not a complete Unity scene with production gameplay systems; it is a package sample showing centralized rules.

### QuestExample

Status: working sample.

- Uses supported event names and equality conditions.
- Required actions exist: `start_quest`, `complete_quest`, `give`.
- Scenario syntax is supported.

### BossPhaseExample

Status: working sample.

- Uses supported `Boss.HealthBelow` event name.
- Required actions exist: `set_state`, `play_cutscene`, `play_music`.
- Scenario syntax is supported.

## Debugger Audit

Status: working subset.

The debugger shows real runtime data:

- Loaded specs
- Diagnostics
- Scene entities and properties
- Recent events
- Trace entries
- Registered actions
- Parsed handlers
- State machine sessions
- Behavior definitions
- Scenario definitions/results
- Strict mode and hot reload settings

Sections that are inherently empty until runtime activity:

- Scenario Results: shows "No scenario run yet" until `Run Scenarios` is clicked.
- State Machines: shows no active sessions until matching entities are registered.

These are not placeholder sections; they reflect runtime state.

## Test Audit

Status: adequate for v1.0, but not exhaustive.

Runtime tests cover:

- Event rule runtime updates entity state.
- Unknown transition target strict diagnostic.
- State machine event transition.
- Scenario runner pass case.
- Action registry wrong argument count.
- Hot reload of changed spec asset.
- EventBridge parser-compatible zone event emission.

Parser tests cover:

- Entity + event rule parse.
- Behavior/state machine syntax.
- Scenario syntax.
- Comment stripping outside strings.

Editor tests cover:

- Debugger window opens.

Critical behavior still under-tested:

- Failed condition trace details.
- Duplicate entity ID diagnostics.
- Duplicate action binding diagnostics.
- Duplicate handler diagnostics.
- Unreachable state warnings.
- Scenario failure reporting.
- `every` interval execution over simulated time.
- `.yuspec` importer round-trip behavior.
- Debugger tab content assertions beyond window creation.

## README Claim Audit

The current README/package README claims are mostly aligned with implementation.

Accurate claims:

- Unity package scaffold: yes.
- Entity/event/action/condition subset: yes.
- State machines: yes, subset.
- Scenario tests: yes, subset.
- Debugger: yes, real runtime data.
- Demo Dungeon: yes, sample-level support.
- Hot reload: yes, subset.

Claims that must remain carefully worded:

- "Production ready" should not be used yet.
- "Full state machine support" should not be used; the accurate phrase is "working subset".
- "Hot reload" should be described as assigned-spec polling, not full live migration.
- Demo Dungeon should be presented as a sample, not a complete playable dungeon product.

## v1.0 Readiness

Ready:

- UPM package structure.
- Runtime/editor assemblies.
- Basic DSL parser/runtime.
- C# action binding.
- Strict diagnostics subset.
- Debugger window with real data.
- Runtime/editor test discovery.
- Door+Chest validation scene.
- README/docs aligned with current capabilities.
- Demo GIF in README.

Partially ready:

- State machine runtime.
- Scenario runner.
- Hot reload.
- Demo Dungeon.
- Unity event bridge.
- Strict type checking.

Missing or deferred:

- CI automation.
- Asset Store packaging polish.
- Full hot reload migration model.
- Rich function-style event arguments.
- Broader condition language.
- Production gameplay service integrations.
- More debugger tests.

## Recommended Next Sprint

1. Add CI for C++ build plus Unity EditMode tests.
2. Add scenario failure and strict diagnostic tests.
3. Add importer tests for `.yuspec` assets.
4. Expand debugger tests to assert real tab content.
5. Decide whether function-style events such as `Player.EnterZone("BossRoom")` belong in v1.1 or should stay out.
6. Add a small DemoDungeon scene harness if the sample is meant to become more than a textual package sample.

## Release Judgment

YUSPEC Unity v1.0.1 is ready as a focused public preview Unity package release,
as long as it is described as a working gameplay rule layer subset rather than a
complete Unity scripting replacement.
