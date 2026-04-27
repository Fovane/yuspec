# Changelog

## [1.0.1] - 2026-04-27

### Added
- Polling-based hot reload for assigned Unity spec assets.
- Hot reload diagnostics and debugger visibility.
- v1.0.1 readiness audit document.

### Fixed
- Unity event bridge now emits parser-compatible event names.
- Added tests for hot reload and event bridge zone emission.

## [1.0.0] - 2026-04-27

### Added
- Unity Package Manager package at `unity/Packages/com.yuspec.unity`.
- Unity runtime for entity declarations, event handlers, conditions, action calls,
  state machines, and scenario checks.
- Editor debugger for specs, diagnostics, entities, events, actions, states, and
  scenarios.
- Door+Chest, GoblinAI, Quest, BossPhase, and DemoDungeon samples.
- Unity 6000.3.8f1 dev project and validation harness.

### Changed
- Repositioned the repository around Unity gameplay rule orchestration.
- Updated README, docs, roadmap, and launch notes for the Unity v1.0 package.
- Kept the original C++ implementation as the legacy/general DSL foundation.

### Validated
- C++ build passes with `cmake --build build --config Debug`.
- Unity EditMode package tests pass: 10/10.
- Unity Door+Chest scene validation passes.

All notable changes to YUSPEC are documented in this file.  
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [1.0.2] — 2026-03-03

### Changed
- README: Added 3 new Known Limitations: flat FSM expressiveness, computational model/Turing-completeness, error handling model
- README: Roadmap v1.1 now includes `--strict` mode, `on_error` handler, recursion depth limit
- README: Roadmap v2.0 now includes hierarchical states, parallel regions, history states, `on_exit`
- Academic paper: Expanded Section 8.2 with flat FSM analysis, computational model, permissive error model
- Academic paper: Updated conclusion/future work with new topics
- Author identity corrected: Yücel Sabah (Fovane), yucelsabah00@outlook.com, sabahgamestudios.com
- LICENSE: Updated copyright holder to Yücel Sabah
- CONTRIBUTING: Added security contact email

---

## [1.0.1] — 2026-03-03

### Changed
- README: Repositioned from "domain-agnostic" to "behavioral specification language"
- README: Added honest "Known Limitations" section addressing all five critique areas
- README: Reprioritized roadmap based on community feedback (enum types, EventBus routing)
- Academic paper: Added "Threats to Validity" section (Section 9)
- Academic paper: Expanded Known Limitations with EventBus scalability, type system gaps
- Academic paper: Changed title to clarify modeling/specification nature
- Academic paper: Added TLA+ reference [23] for modeling language comparison
- GitHub links updated to https://github.com/Fovane/yuspec

---

## [1.0.0] — 2026-03-03

### Added — Language
- `define entity` — first-class entity archetypes with typed property tables
- `define behavior` — composable FSM behaviors attachable to entities
- `define event` — typed event declarations consumed by the global EventBus
- `define zone` — named runtime containers for entity instantiation
- `define scenario` — structured test harness with `assert`, `expect`, `log`
- 38 reserved keywords
- Full type system: `int`, `float`, `bool`, `string`, `duration`, `list`, `map`, `any`
- Duration literals: `100ms`, `3s`, `1m`, `2h`
- Universal event handlers (`on event E { }` — fires from any state)
- State-gated event handlers (`on event E from A -> B { }`)
- Timeout transitions (`on timeout 3s from A -> B { }`)
- Rules (`rule NAME when COND then { }`)
- `emit`, `transition`, `spawn`, `wait`, `set`, `let`, `if/else`, `foreach`
- Built-in functions: `str`, `int`, `float`, `sqrt`, `abs`, `max`, `min`,
  `floor`, `ceil`, `rand`, `rand_int`, `len`, `entities_of`
- Full operator set: arithmetic, comparison, logical, string concatenation

### Added — Compiler (`yuspec_compiler_v1`)
- Lexer: full tokenizer with 38-keyword table, duration literals, string escapes
- Parser: recursive descent, `std::variant`-based typed AST
- Semantic analysis: two-pass (declaration collection + type checking / reference resolution)
- Error reporting with source position (file:line:col)

### Added — Runtime (`yuspec_runtime_v1`)
- `Value` tagged-union (int64, double, bool, string, entity, list, map)
- `Env` lexical scope chain
- `World` — ECS: create/destroy/tick/reset entities by type
- `EventBus` — publish-subscribe with ordered history for `expect` assertions
- `SMInstance` — FSM lifecycle: start/tick/handle_event/enter_state
  - Signal propagation fix: `execute_actions` captures `Signal::Transition`
  - `pending_transition_` correctly applied after every action block
- `SMManager` — manages all active SMInstances
- `Interpreter` — tree-walking executor, scenario isolation with full reset

### Added — CLI (`yuspec1`)
- `tokens   <file>` — dump token stream
- `parse    <file>` — print AST
- `validate <file>` — semantic analysis only
- `run      <file>` — execute all zones
- `test     <file>` — run all scenarios, report pass/fail
- `--verbose`, `--ticks N`, `--tick-ms N`, `--zone NAME`, `--scenario NAME` flags

### Added — Examples (7 domains)
- `examples/network/01_tcp_handshake.yus` — TCP connection state machine
- `examples/workflow/01_approval.yus` — multi-stage approval pipeline with escalation
- `examples/distributed/01_orchestration.yus` — canary deployment controller
- `examples/iot/01_sensor.yus` — temperature/humidity sensor + HVAC controller
- `examples/simulation/01_traffic.yus` — traffic light + vehicle simulation
- `examples/testing/01_scenario.yus` — YUSPEC as a testing DSL (34/34 passing)
- `examples/game/01_mmo.yus` — full MMO RPG with combat, quests, leveling (11/11 passing)

### Fixed
- `execute_actions` now captures `Signal::Transition` returned by interpreter
- `handle_event` correctly applies pending transition after handler execution
- `enter_state` correctly chains transitions initiated from `on_enter` actions
- `MonsterAI` universal event handler fires `PlayerAttack` from any state (not just Chasing/Attacking)
- `PlayerProgressionBehavior` emits `ExpGained` for `QuestCompleted` reward events
- World/EventBus/SMManager `reset()` called between scenarios for full isolation

### Build
- CMake 3.16+, C++17, MSVC 2022 / GCC 9+ / Clang 10+
- No external runtime dependencies (stdlib only)
- Raylib 5.0 for legacy v0.1 viewer (FetchContent, optional)
- Install target, CPack packaging

---

## [0.1.0] — 2025 (initial prototype)

### Added
- 5-command game DSL: `terrain`, `player`, `hostile`, `attach_logic`, `spawn`
- Flag-based ECS runtime
- Raylib 2D viewer (`yuspec_view`)
- Basic lexer/parser/validator for v0.1 syntax
