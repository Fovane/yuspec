# Changelog

All notable changes to this package are documented in this file.

## 1.0.1 - 2026-04-27

- Added polling-based hot reload for assigned `TextAsset` and `YuspecSpecAsset` specs.
- Added hot reload diagnostics and debugger settings display.
- Fixed `YuspecEventBridge` to emit parser-compatible event names.
- Added runtime tests for hot reload and event bridge zone emission.
- Added `docs/v1-readiness-audit.md` with the v1.0 readiness audit.

## 1.0.0 - 2026-04-27

- Added parser support for event rules, behaviors/state blocks, scenario blocks, and comments.
- Added syntax tree model with source locations.
- Added strict validation for duplicates, transitions, intervals, and scenario entity references.
- Added runtime state machine execution and scenario runner.
- Added polling-based hot reload for assigned spec assets.
- Fixed Unity event bridge emitted event names to match the supported parser syntax.
- Added debugger tabs for overview, specs, diagnostics, entities, events, actions, state machines, scenarios, and settings.
- Added runtime and editor test assemblies and baseline tests.
- Added common bridge action stubs for Demo Dungeon flows.
- Updated Door, GoblinAI, Quest, BossPhase, and DemoDungeon samples for supported v1 syntax.
- Validated the Unity package with Unity 6000.3.8f1 EditMode tests and Door+Chest scene validation.
