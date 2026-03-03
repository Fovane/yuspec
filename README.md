# YUSPEC

> **Entity-Behavior Programming — a behavioral specification language for modeling event-driven systems**

```yuspec
define entity Player {
  property hp     float  default 500.0
  property level  int    default 1
  has PlayerCombatBehavior
}

define behavior PlayerCombatBehavior {
  state Alive   initial;
  state Dead    terminal;

  on event Attacked {
    set self.hp = self.hp - event.damage
    if (self.hp <= 0.0) { transition Dead }
  }
}

define scenario QuickTest {
  spawn Player count 1 with hp = 80.0
  emit Attacked { damage: 80.0 }
  let players = entities_of("Player")
  assert (players[0].alive == false)  "player died correctly"
}
```

```
$ yuspec1 test demo.yus
Assertions: 1 passed, 0 failed    PASS
```

---

## What is YUSPEC?

YUSPEC (**Y**our **U**niversal **Spec**ification Language) is a declarative
behavioral specification language built on the **Entity-Behavior Programming (EBP)**
paradigm.

Programs are collections of **Entities** (typed archetypes with property tables) and
**Behaviors** (composable finite-state machines that react to events, timeouts, and
conditions). Communication happens exclusively through a global **EventBus** —
entities never hold direct references to each other.

> **Important distinction:** YUSPEC is a *modeling and simulation* language. It does
> not send real TCP packets, read physical sensor data, or perform actual deployments.
> It models the *behavioral logic* of these systems — the state machines, event flows,
> and decision rules — in a unified, testable notation.

| Concept | YUSPEC construct |
|---------|-----------------|
| Data    | `define entity` — typed property table |
| Logic   | `define behavior` — FSM with event handlers & rules |
| Communication | `emit` / `on event` — pub-sub EventBus |
| Testing | `define scenario` — `assert`, `expect`, `log` |
| Grouping | `define zone` — named runtime container |

---

## Why YUSPEC?

### One language, seven modeling domains

The same language models behavioral logic across different problem spaces.
All examples are FSM-based simulations — YUSPEC's strength is providing a unified
notation for event-driven state machines regardless of the domain:

| Domain | Example |
| 🎮 Game Development      | `examples/game/01_mmo.yus` — MMO RPG with combat, quests, leveling |
| 🌐 Network Protocols     | `examples/network/01_tcp_handshake.yus` — TCP state machine |
| 📋 Workflow Automation   | `examples/workflow/01_approval.yus` — multi-stage approval + escalation |
| ⚡ Distributed Systems  | `examples/distributed/01_orchestration.yus` — canary deployment |
| 🌡️ IoT / Robotics        | `examples/iot/01_sensor.yus` — sensor + HVAC controller |
| 🚦 Simulation            | `examples/simulation/01_traffic.yus` — traffic lights + vehicles |
| ✅ Test Scripting         | `examples/testing/01_scenario.yus` — YUSPEC as a testing DSL |

### Declarative over imperative

Describe **what** exists (entities, states, events), not **how** to iterate over them.

### Composable behaviors

Multiple behaviors can coexist on a single entity, each evolving its own state
independently. Behaviors are defined once and reused across many entity types.

### Designed for testability

`define scenario` is a first-class language construct. `assert` and `expect`
give structured pass/fail reporting with zero boilerplate.

---

## Quick Start

### Prerequisites

- CMake 3.16+
- C++17 compiler: MSVC 2019+, GCC 9+, or Clang 10+

### Build

```bash
git clone https://github.com/Fovane/yuspec.git
cd yuspec

# Configure
cmake -S . -B build

# Build the CLI
cmake --build build --target yuspec1 --config Debug
```

On Windows (MSVC):
```powershell
cmake -S . -B build -G "Visual Studio 17 2022"
cmake --build build --target yuspec1 --config Debug
```

### Run your first program

```bash
# Run all test scenarios
./build/Debug/yuspec1 test examples/testing/01_scenario.yus

# Run the MMO game example
./build/Debug/yuspec1 test examples/game/01_mmo.yus

# Validate syntax only
./build/Debug/yuspec1 validate examples/network/01_tcp_handshake.yus

# Execute a zone
./build/Debug/yuspec1 run examples/workflow/01_approval.yus
```

---

## CLI Reference

```
yuspec1 tokens   <file.yus>            Dump token stream (debug)
yuspec1 parse    <file.yus>            Print AST
yuspec1 validate <file.yus>            Semantic analysis only
yuspec1 run      <file.yus>            Execute all zones
yuspec1 test     <file.yus>            Run all scenarios, report pass/fail

Options:
  --verbose            Print tick trace and event log
  --ticks N            Max simulation ticks (default: 10000)
  --tick-ms N          Milliseconds per tick (default: 16)
  --zone NAME          Run a specific zone
  --scenario NAME      Run a specific scenario
```

---

## Language Reference

### Entities

```yuspec
define entity Monster {
  property monster_id   int     default 0
  property hp           float   default 100.0
  property alive        bool    default true
  has MonsterAI
  has LootDropBehavior
}
```

Spawn instances inside a zone or scenario:

```yuspec
spawn Monster count 5 with monster_id = 1, hp = 200.0
```

### Behaviors (State Machines)

```yuspec
define behavior MonsterAI {
  state Idle      initial  timeout 3s;
  state Chasing            timeout 10s;
  state Attacking;
  state Dead      terminal;

  // State-gated transition
  on timeout from Idle -> Chasing {
    log "Monster spotted player!"
  }

  // Universal handler — fires from ANY state
  on event PlayerAttack {
    let net = event.damage - self.defense
    if (net < 1.0) { set net = 1.0 }
    set self.hp = self.hp - net
    if (self.hp <= 0.0) {
      emit EntityDied { entity_id: self.monster_id }
      transition Dead
    }
  }

  // Condition-based rule
  rule AggroCheck
    when self.hp < 30.0
    then { log "Monster fleeing!" }
}
```

### Events

```yuspec
define event PlayerAttack {
  property attacker_id  int
  property target_id    int
  property damage       float
  property skill        string
}
```

Emit anywhere:

```yuspec
emit PlayerAttack { attacker_id: 1, target_id: 2, damage: 75.0, skill: "slash" }
```

### Scenarios (Test Harness)

```yuspec
define scenario CombatTest {
  spawn Player  count 1 with player_id = 1, hp = 500.0, attack = 70.0
  spawn Monster count 1 with monster_id = 100, hp = 80.0, defense = 5.0

  emit PlayerAttack { attacker_id: 1, target_id: 100, damage: 70.0, skill: "basic" }
  wait 100ms

  let monsters = entities_of("Monster")
  assert (monsters[0].alive == false)  "monster should be dead"
  expect EntityDied                    "EntityDied should have been emitted"
}
```

### Type System

| Type       | Example literals              |
|------------|-------------------------------|
| `int`      | `42`, `-7`, `0`               |
| `float`    | `3.14`, `-0.5`, `1.0`         |
| `bool`     | `true`, `false`               |
| `string`   | `"hello world"`               |
| `duration` | `100ms`, `3s`, `1m`, `2h`     |
| `list`     | `[1, 2, 3]`                   |
| `map`      | `{ key: value }`              |
| `any`      | escape hatch, no type check   |

### Built-in Functions

```
str(v)          Convert any value to string
int(v)          Convert to integer
float(v)        Convert to float
sqrt(x)         Square root
abs(x)          Absolute value
max(a, b)       Maximum
min(a, b)       Minimum
floor(x)        Floor
ceil(x)         Ceiling
rand()          Random float in [0, 1)
rand_int(a, b)  Random int in [a, b]
len(list)       List length
entities_of(t)  All live Entity instances of type t
```

---

## Architecture

```
yuspec/
├── compiler/
│   ├── include/yuspec/
│   │   ├── v1_token.h        TK enum, Token, SrcPos, Diag
│   │   ├── v1_ast.h          All Expr/Action/Decl variant types
│   │   ├── v1_lexer.h
│   │   ├── v1_parser.h
│   │   └── v1_sema.h
│   └── src/
│       ├── v1_lexer.cpp      Tokenizer
│       ├── v1_parser.cpp     Recursive descent parser
│       └── v1_sema.cpp       Two-pass semantic analysis
│
├── runtime/
│   ├── include/yuspec_rt/
│   │   ├── v1_value.h        Value tagged-union, Env scope chain
│   │   ├── v1_ecs.h          World, Entity, EntityId
│   │   ├── v1_event_bus.h    EventBus, Event, history
│   │   ├── v1_sm_runtime.h   SMInstance, SMManager
│   │   └── v1_interpreter.h  Interpreter, Signal, RunConfig
│   └── src/
│       ├── v1_sm_runtime.cpp FSM lifecycle + Signal propagation
│       └── v1_interpreter.cpp Tree-walking executor
│
├── tools/yuspec1_cli/
│   └── main.cpp              CLI entry point
│
└── examples/                 7 domain examples
```

### Compiler Pipeline

```
Source text
    │
    ▼
Lexer (v1_lexer.cpp)
    │  Token stream
    ▼
Parser (v1_parser.cpp)
    │  std::variant AST
    ▼
Sema (v1_sema.cpp)
    │  Pass 1: collect declarations
    │  Pass 2: resolve refs, type-check
    ▼
Interpreter (v1_interpreter.cpp)
    │  Tree-walk execution
    ▼
Output / test results
```

### Runtime Subsystems

```
World          Entity lifecycle — create / destroy / tick / reset
EventBus       Publish-subscribe — emit_tracked / subscribe / history
SMInstance     Per-entity FSM — start / tick / handle_event / enter_state
SMManager      Manages all active SMInstances
Interpreter    exec_actions / eval_expr / run_scenario
```

**Signal Propagation** — the key correctness property:  
`execute_actions` captures the `Signal::Transition` returned by the interpreter
and stores it as `pending_transition_`. After every action block (event handler,
`on_enter`, timeout), any pending transition is applied immediately — this makes
`transition X` inside event handlers work correctly.

---

## Test Results

### testing/01_scenario.yus — 8 scenarios, 34 assertions

| Scenario | Passed | Failed | Result |
|----------|--------|--------|--------|
| BasicVariablesTest     | 3 | 0 | PASS |
| ArithmeticTest         | 4 | 0 | PASS |
| StringTest             | 4 | 0 | PASS |
| BooleanTest            | 4 | 0 | PASS |
| EntityTest             | 4 | 0 | PASS |
| EventTest              | 3 | 0 | PASS |
| StateMachineTest       | 8 | 0 | PASS |
| ControlFlowTest        | 4 | 0 | PASS |
| **Total**              | **34** | **0** | **PASS** |

### game/01_mmo.yus — 5 scenarios, 11 assertions

| Scenario | Passed | Failed | Result |
|----------|--------|--------|--------|
| CombatTest          | 2 | 0 | PASS |
| KillQuestTest       | 2 | 0 | PASS |
| LevelUpTest         | 3 | 0 | PASS |
| SkillCombatTest     | 2 | 0 | PASS |
| PlayerDeathTest     | 2 | 0 | PASS |
| **Total**           | **11** | **0** | **PASS** |

---

## Known Limitations

We believe in being honest about what YUSPEC v1.0 can and cannot do.

### Modeling, not execution

YUSPEC **models** the behavioral logic of systems. It does not:
- Send real network packets (TCP example models the *state machine*, not the wire protocol)
- Read physical sensors (IoT example models *thresholds and reactions*, not hardware I/O)
- Perform actual deployments (distributed example models *orchestration logic*, not infrastructure)

The value proposition is a *unified behavioral notation*, not runtime integration with external systems. Think of it as "executable specifications" — closer to TLA+ or Alloy in intent, but with a developer-friendly syntax.

### Tree-walking interpreter (slow)

The current runtime evaluates AST nodes directly. Approximate performance:

| Approach | Relative Speed |
|----------|---------------|
| Tree-walking (current) | 1x (baseline) |
| Bytecode VM (planned v1.2) | ~20-50x |
| JIT (future) | ~100-500x |

For simulations up to ~1,000 entities, v1.0 is adequate. For 10,000+ entities,
a bytecode VM is necessary. This is the **#1 priority** for v1.2.

### Global EventBus scalability

The single global EventBus checks every handler on every `emit`. With 1,000 entities
× 5 handlers each, that's 5,000 callback checks per event. Current mitigations: none.

Planned:
- Event filtering / topic-based routing
- Entity-scoped event channels
- Circular event chain detection (infinite loop prevention)
- Event tracing / visualization for debugging

### Shallow type system

v1.0 provides basic types (`int`, `float`, `bool`, `string`, `duration`, `list`, `map`)
and an `any` escape hatch. Notable gaps:

| Missing Feature | Impact |
|----------------|--------|
| `define enum` | No `DamageType.Physical` — must use magic strings |
| Generic types | No `list<int>` — all lists are heterogeneous |
| Entity reference type | No `property target: Entity<Monster>` |
| Algebraic data types | No `Option<T>`, `Result<T,E>` |

The `any` type is an honest admission that the type system is incomplete.
Full type inference and custom types are planned for v1.1.

### No module system

All definitions must live in a single `.yus` file. No `import`, no namespaces,
no behavior libraries. This is the **#2 priority** for v1.1.

---

## Roadmap

### v1.1 — Language (priority: type system + modularity)
- `define enum` — enumerated types: `define enum DamageType { Physical, Magical, True }`
- `define function` — named functions: `define function clamp(x, lo, hi) -> float { ... }`
- Import system: `import "path/file.yus"`
- String interpolation: `"Player {self.name} has {self.hp} HP"`
- Entity reference types: `property target: Entity<Monster>`
- Full type inference — remove need for `any` escape hatch

### v1.2 — Runtime (priority: performance + EventBus)
- **Bytecode compiler + VM** — target 20-50x speedup over tree-walking
- Event filtering / topic-based routing — solve O(N×M) dispatch
- Circular event chain detection — prevent infinite loops at runtime
- Event tracing: `--trace` flag dumps full event flow graph
- Parallel zones with message passing
- Persistent state: serialize/deserialize World snapshots

### v1.3 — Tooling
- Language Server Protocol (LSP)
- VS Code extension: syntax highlighting, hover docs, inline errors
- Visual FSM editor + event flow visualization
- Profiler: per-behavior tick timing, event throughput metrics

### v2.0 — External Integration
- Network-transparent EventBus (bridge to real systems via adapters)
- Plugin API: custom event sources (real sensors, network sockets, databases)
- Cluster mode: entities auto-sharded across nodes
- Hot-reload behaviors without stopping simulation

---

## Domain Examples Preview

### TCP Handshake (Network)

```yuspec
define behavior TCPConnection {
  state Closed      initial;
  state SynSent;
  state Established timeout 30s;
  state FinWait;
  state TimeWait    timeout 2s;

  on event SynAckReceived from SynSent -> Established {
    emit AckSent { conn_id: self.conn_id }
    log "Connection " + str(self.conn_id) + " established"
  }

  on timeout from Established -> FinWait {
    emit FinSent { conn_id: self.conn_id }
  }
}
```

### Canary Deployment (Distributed)

```yuspec
define behavior DeploymentController {
  state Idle        initial;
  state Canary                retry 3;
  state HealthCheck;
  state FullRollout terminal;
  state Rollback    terminal;

  on event HealthCheckPassed from HealthCheck -> FullRollout {
    emit DeploymentComplete { service: self.service_name, version: self.new_version }
    log "Full rollout successful"
  }

  on event HealthCheckFailed from HealthCheck -> Rollback {
    emit RollbackStarted { service: self.service_name }
    log "Rolling back!"
  }
}
```

### IoT Sensor

```yuspec
define behavior TemperatureSensor {
  state Normal   initial;
  state Warning;
  state Critical;

  rule OverheatWarning
    when self.temperature > 70.0 and self.temperature <= 85.0
    then { emit TempWarning { sensor_id: self.sensor_id, temp: self.temperature } }

  rule OverheatCritical
    when self.temperature > 85.0
    then {
      emit TempCritical { sensor_id: self.sensor_id, temp: self.temperature }
      transition Critical
    }
}
```

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to build, test, and submit changes.

Bug reports → [GitHub Issues](https://github.com/Fovane/yuspec/issues)  
Discussion → [GitHub Discussions](https://github.com/Fovane/yuspec/discussions)

---

## License

[MIT](LICENSE) — Copyright (c) 2026 Yucel
