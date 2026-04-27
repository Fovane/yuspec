# Language

This document defines the Unity v1 syntax subset implemented by the package.
YUSPEC is intentionally small: it models gameplay-facing entities, events,
conditions, actions, state machines, and scenarios while C# handles the
technical Unity implementation.

## Entity

```yuspec
entity Door {
    state = Closed
    key = "IronKey"
}
```

An entity block declares gameplay-facing properties. Unity scene objects bind to
these declarations through `YuspecEntity`.

## Event Handler

```yuspec
on Player.Interact with Door when Player.has(Door.key):
    set Door.state = Open
    play_animation Door "Open"
    play_sound "door_open"
```

Handlers have:

- An event name
- Optional target binding with `with`
- Optional condition with `when`
- An action block

## Conditions

The v1 condition set stays small:

```yuspec
Player.has(Door.key)
Door.state == Closed
self.health <= 0
```

Strict mode reports unknown entities, properties, actions, duplicate handlers,
duplicate states, unknown transitions, unreachable states, and obvious literal
type mismatches.

## Actions

Actions are named calls. Most actions are implemented in C# and registered with
`YuspecActionAttribute`.

```yuspec
play_animation Door "Open"
damage Player by self.damage
spawn self.drops at self.position
```

The `set` action is built into the YUSPEC runtime. Other actions are bridge
actions discovered through `[YuspecAction]`:

```yuspec
set Door.state = Open
set_state Boss Phase2
```

## Behavior And State

```yuspec
behavior GoblinAI for Goblin {
    state Idle {
        on PlayerSeen -> Chase
    }

    state Chase {
        every 0.2s:
            move_towards Player speed 3

        on InAttackRange -> Attack
        on PlayerLost -> Idle
    }
}
```

Implemented state machine constructs:

- `behavior Name for EntityType`
- `state Name`
- `on Event -> State`
- `every interval:`
- `on enter:`
- `on exit:`
- `do:`

## Scenario

```yuspec
scenario "door opens with key" {
    given Player has "IronKey"
    when Player.Interact Door
    expect Door.state == Open
}
```

Scenario tests let gameplay logic run without manually driving a Unity scene.
The Unity debugger can execute and show scenario results.

## MVP Constraint

v1 intentionally avoids function definitions, advanced type inference,
networking, hot reload guarantees, and a general-purpose programming model. The
language should continue to expand from working Unity demos, not speculative
syntax.
