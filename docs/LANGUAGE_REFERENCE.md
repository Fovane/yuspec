# YUSPEC v1.0 — Complete Language Reference

---

## 1. Lexical Structure

### 1.1 Source Encoding
YUSPEC source files are UTF-8 encoded plain text. The conventional file extension is `.yus`.

### 1.2 Comments

```yuspec
// Single-line comment — extends to end of line
```

Block comments are not supported in v1.0.

### 1.3 Identifiers
Identifiers begin with a letter or underscore, followed by any combination of
letters, digits, and underscores. They are case-sensitive.

```
identifier ::= [a-zA-Z_][a-zA-Z0-9_]*
```

### 1.4 Keywords (38)

```
define   entity    behavior   statemachine  zone       scenario
event    property  state      rule          initial    terminal
timeout  retry     on         from          emit       transition
log      set       let        if            else       foreach
in       wait      expect     assert        spawn      count
with     has       when       then          true       false
and      or        not
```

### 1.5 Literal Types

| Type | Syntax | Examples |
|------|--------|---------|
| Integer | `[0-9]+` or `-[0-9]+` | `0`, `42`, `-7` |
| Float | `[0-9]+\.[0-9]+` | `3.14`, `-0.5`, `1.0` |
| Boolean | `true` \| `false` | `true`, `false` |
| String | `"..."` with `\"`, `\\`, `\n`, `\t` escapes | `"hello"` |
| Duration | `[0-9]+(ms\|s\|m\|h)` | `100ms`, `3s`, `1m`, `2h` |

Duration literals are stored as `int64` milliseconds at parse time:
- `100ms` → 100
- `3s` → 3000
- `1m` → 60000
- `2h` → 7200000

### 1.6 Operators

| Operator | Category | Precedence |
|----------|----------|-----------|
| `not` | Logical unary | 7 (highest) |
| `*`, `/`, `%` | Arithmetic | 6 |
| `+`, `-` | Arithmetic / string concat | 5 |
| `<`, `<=`, `>`, `>=` | Comparison | 4 |
| `==`, `!=` | Equality | 3 |
| `and` | Logical | 2 |
| `or` | Logical | 1 (lowest) |

---

## 2. Type System

YUSPEC is **dynamically typed at runtime** with **partial static inference** in the
semantic analysis pass.

### 2.1 Primitive Types

| Keyword | Internal | Description |
|---------|----------|-------------|
| `int` | `int64_t` | 64-bit signed integer |
| `float` | `double` | 64-bit IEEE 754 double |
| `bool` | `bool` | `true` or `false` |
| `string` | `std::string` | UTF-8 text |
| `duration` | `int64_t` (ms) | Time interval |

### 2.2 Composite Types

| Keyword | Description |
|---------|-------------|
| `list` | Ordered heterogeneous list `[v1, v2, ...]` |
| `map` | String-keyed mapping `{ key: value, ... }` |

### 2.3 Special Types

| Keyword | Description |
|---------|-------------|
| `any` | Escape hatch — disables type checking for this property |
| entity reference | Passed as `Value::from_entity(id)` — internal use |

### 2.4 Type Coercion

**Implicit coercions** (applied automatically by the interpreter):
- `int` → `float` in mixed arithmetic expressions
- Any value → `string` via the `str()` built-in (explicit)

**No implicit coercions** across `bool`, `string`, `list`, `map`.

---

## 3. Declarations

All top-level constructs begin with `define`.

### 3.1 `define event`

```
define event <Name> {
  property <name> <type> ;
  ...
}
```

Declares a typed event schema. The `event` keyword in handler bodies refers to
the current event instance; fields are accessed as `event.<name>`.

### 3.2 `define entity`

```
define entity <Name> {
  property <name> <type> default <expr>
  ...
  has <BehaviorName>
  ...
}
```

Declares an entity archetype. `property` fields have required `default` values
(evaluated at spawn time). `has` attaches a behavior to every spawned instance.

### 3.3 `define behavior`

```
define behavior <Name> [for <EntityType>] {
  state-decl*
  handler*
}
```

#### State Declaration

```
state <Name> [initial] [terminal] [timeout <duration>] [retry <n>] ;
```

- `initial` — starting state (exactly one required)
- `terminal` — entity is "done" on entry; no further transitions
- `timeout <duration>` — automatically transition after this interval
- `retry <n>` — reset the timeout counter up to N times before a timeout fires

#### Event Handler (state-gated)

```
on event <EventName> from <FromState> -> <ToState> {
  <actions>
}
```

Only fires when the FSM is in `<FromState>`. On match, executes `<actions>`,
then transitions to `<ToState>`.

#### Event Handler (universal)

```
on event <EventName> {
  <actions>
}
```

Fires from **any** state. Does not automatically transition — use `transition X`
inside the action body to change state.

#### Timeout Handler

```
on timeout [from <FromState>] -> <ToState> {
  <actions>
}
```

Fires when the current state's timeout expires. Optional `from` guard restricts
to a specific state.

#### Rule

```
rule <Name> when <condition> then {
  <actions>
}
```

Evaluated every tick. When `<condition>` is true, `<actions>` execute.

### 3.4 `define zone`

```
define zone <Name> {
  spawn ...
  emit  ...
  wait  ...
  ...
}
```

A named execution context. Run with `yuspec1 run --zone NAME file.yus`.

### 3.5 `define scenario`

```
define scenario <Name> {
  <actions>
}
```

A structured test harness. The runtime:
1. Resets `World`, `EventBus`, and `SMManager` (full isolation)
2. Executes all actions in order
3. Reports `assert` / `expect` pass/fail counts

---

## 4. Actions

Actions are the executable statements of YUSPEC. They appear inside behavior
handlers, zone bodies, and scenario bodies.

### `set`

```
set <target> = <expr>
```

Assigns `<expr>` to a variable or entity property. `self.<field>` writes back
to the live entity property table.

### `let`

```
let <name> = <expr>
```

Declares a new local variable in the current scope. Immutable binding in v1.0
(re-assignment requires `set`).

### `emit`

```
emit <EventName> { <field>: <expr>, ... }
```

Publishes an event to the global EventBus. All matching `on event` handlers across
all live entity behaviors fire in the same simulation tick.

### `transition`

```
transition <StateName>
```

Requests an FSM state change. The SMInstance stores this as `pending_transition_`
and applies it after the current action block completes, including chaining
(an `on_enter` block may itself request another transition).

### `log`

```
log <expr>
```

Prints the string representation of `<expr>` to stdout.

### `wait`

```
wait <duration>
```

Advances the simulation clock by `<duration>` milliseconds. During this time,
all timeouts are evaluated and events are dispatched.

### `spawn`

```
spawn <EntityType> count <n> with <prop>=<expr>, ...
```

Creates `<n>` instances of the named entity archetype. `with` overrides default
property values.

### `if / else`

```
if (<condition>) {
  <actions>
} else {
  <actions>
}
```

### `foreach`

```
foreach <var> in <collection> {
  <actions>
}
```

Iterates over a `list` or entity collection (`entities_of("Type")`).

### `assert`

```
assert (<condition>) "<message>"
```

Terminates the scenario as failed immediately if `<condition>` is false.

### `expect`

```
expect <EventName> "<message>"
```

Checks the EventBus history. Fails the scenario if `<EventName>` was never
emitted during the current scenario. Does **not** require ordering.

---

## 5. Built-in Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `str` | `str(any) -> string` | Convert to string |
| `int` | `int(any) -> int` | Convert to integer |
| `float` | `float(any) -> float` | Convert to float |
| `sqrt` | `sqrt(float) -> float` | Square root |
| `abs` | `abs(number) -> number` | Absolute value |
| `max` | `max(a, b) -> number` | Maximum of two values |
| `min` | `min(a, b) -> number` | Minimum of two values |
| `floor` | `floor(float) -> int` | Floor to integer |
| `ceil` | `ceil(float) -> int` | Ceiling to integer |
| `rand` | `rand() -> float` | Uniform random in [0, 1) |
| `rand_int` | `rand_int(a, b) -> int` | Uniform random integer in [a, b] |
| `len` | `len(list) -> int` | Number of elements |
| `entities_of` | `entities_of(string) -> list` | All live instances of named type |

---

## 6. Special Variables

| Name | Available in | Description |
|------|-------------|-------------|
| `self` | Behavior handlers | The entity instance owning this behavior |
| `event` | `on event` handlers | The current event being processed |

`self` is re-injected at the start of every action block execution, so mutations
via `set self.<field> = <value>` are immediately visible to subsequent actions.

---

## 7. Scoping Rules

- Each behavior handler, rule body, and scenario body has its own lexical scope.
- `let` introduces a binding in the innermost enclosing scope.
- Scopes form a chain; inner scopes shadow outer ones.
- `self` is always injected at the outermost scope of each action block.

---

## 8. Execution Model

```
Tick loop (default 16ms per tick, up to 10000 ticks):
  1. For each SMInstance:
       a. Evaluate all rules whose condition is true → execute actions
       b. Advance timeout counters
       c. If timeout expired → execute on_timeout handler → apply pending_transition_
  2. Dispatch queued EventBus events:
       For each event in queue:
         For each SMInstance:
           Check universal handlers
           Check state-gated handlers for current state
           Execute matching handler actions → apply pending_transition_
```

**Scenario execution** bypasses the tick loop for `emit` + `assert`/`expect` —
events are dispatched immediately in the same call stack, then assertions are
checked.

---

## 9. Grammar (EBNF, abridged)

```ebnf
program      ::= decl*
decl         ::= event_decl | entity_decl | behavior_decl
               | zone_decl  | scenario_decl

event_decl   ::= 'define' 'event' IDENT '{' prop_field* '}'
entity_decl  ::= 'define' 'entity' IDENT '{' (prop_field | has_clause)* '}'
behavior_decl::= 'define' 'behavior' IDENT ('for' IDENT)?
                 '{' (state_decl | handler | rule_decl)* '}'

state_decl   ::= 'state' IDENT state_flag* ';'
state_flag   ::= 'initial' | 'terminal'
               | 'timeout' duration_lit
               | 'retry' INT_LIT

handler      ::= on_event_handler | on_timeout_handler
on_event_handler ::= 'on' 'event' IDENT ('from' IDENT '->' IDENT)?
                     '{' action* '}'
on_timeout_handler ::= 'on' 'timeout' ('from' IDENT)?
                       ('->' IDENT)? '{' action* '}'
rule_decl    ::= 'rule' IDENT 'when' expr 'then' '{' action* '}'

action       ::= set_action | let_action | emit_action | transition_action
               | log_action | wait_action | if_action  | foreach_action
               | spawn_action | assert_action | expect_action | call_action

expr         ::= or_expr
or_expr      ::= and_expr ('or' and_expr)*
and_expr     ::= eq_expr ('and' eq_expr)*
eq_expr      ::= cmp_expr (('==' | '!=') cmp_expr)*
cmp_expr     ::= add_expr (('<' | '<=' | '>' | '>=') add_expr)*
add_expr     ::= mul_expr (('+' | '-') mul_expr)*
mul_expr     ::= unary_expr (('*' | '/' | '%') unary_expr)*
unary_expr   ::= 'not' unary_expr | postfix_expr
postfix_expr ::= primary_expr ('.' IDENT | '[' expr ']' | '(' args ')')*
primary_expr ::= INT_LIT | FLOAT_LIT | STRING_LIT | BOOL_LIT
               | DURATION_LIT | IDENT | '[' expr_list ']'
               | '{' map_entries '}' | '(' expr ')'
```

---

## 10. Error Handling

Compiler errors report `file:line:col: message`. Runtime panics print the
offending action and entity ID.

Scenario failures:
- `assert` failure → immediate scenario fail with message
- `expect` failure → scenario summary shows which events were missing

Exit codes:
- `0` — all scenarios/zones passed
- `1` — usage error
- `2` — compile/semantic error
- `3` — runtime error
- `4` — test assertion failure
