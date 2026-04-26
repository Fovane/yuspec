# Roadmap

## Phase 0 - Repo Pivot

- Rewrite README around Unity gameplay orchestration.
- Add product docs.
- Add Unity examples.
- Add Unity package scaffold.
- Mark old general examples as legacy.

## Phase 1 - Unity Package Scaffold

- `com.yuspec.unity` package manifest.
- Runtime and editor asmdefs.
- Runtime bridge classes.
- Debug window with loaded specs, parsed handlers, diagnostics, entities, events,
  and trace output.
- Samples and package docs.

Success criterion: Unity can add the package locally and the debugger menu item
appears without compile errors.

## Phase 2 - Action Registry

- Reflection-based method discovery.
- Duplicate binding diagnostics.
- Argument count validation.
- Basic type conversion.
- Unknown action diagnostics.

Success criterion: `[YuspecAction("play_sound")]` methods can be discovered and
called by name.

## Phase 3 - Minimal Runtime

- Door+Chest example syntax.
- Minimal line-oriented Unity parser for the Door subset.
- Entity declarations.
- Event handler matching.
- `Player.has(Door.key)` condition.
- `Chest.state == Closed` condition.
- `set`, `play_animation`, `play_sound`, and `give` action execution.
- Strict diagnostics for unknown entities/properties/actions and action argument
  count.
- Debug trace for events, conditions, and actions.

Success criterion: Door opens in Unity only when the player has the required key,
then Chest opens and gives Gold.

Current status: implemented in the Unity package scaffold and dev scene harness.

## Phase 4 - State Machines

- `behavior` block.
- `state` block.
- `on event -> state`.
- Current state tracking.
- `every` interval support.
- `do` entry action block.

Success criterion: Goblin can move through Idle, Chase, Attack, and Dead states.

## Phase 5 - Scenario Tests

- `scenario` parser.
- `given` / `when` / `expect`.
- CLI test runner.
- Unity editor scenario runner.
- Debugger result view.

Success criterion: `scenario "door opens with key"` reports pass/fail.

## Phase 6 - Demo Dungeon

- Player picks up key.
- Locked door opens with key.
- Chest grants loot.
- Goblin death drops loot.
- Quest starts and completes.
- Boss room locks entrance.
- Boss phase changes below 50 percent.
- Boss death unlocks exit.

Success criterion: a 60 second demo shows why YUSPEC reduces script sprawl.

## Phase 7 - Publish Readiness

- Versioned package manifest.
- CHANGELOG.
- LICENSE.
- Samples.
- Tests.
- Documentation.
- CI.
- Release zip.
- Demo video.
- Asset Store description.
