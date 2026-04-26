# Language

This document defines the intended Unity MVP syntax. The Door+Chest entity,
handler, condition, and action subset is implemented in the Unity package.
State machines and executable scenarios remain product targets.

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

Initial MVP conditions should stay small:

```yuspec
Player.has(Door.key)
Door.state == Closed
self.health <= 0
Quest.Active("FindTheBlacksmith")
```

Strict mode should report unknown entities, properties, functions, and obvious
type mismatches.

## Actions

Actions are named calls. Most actions are implemented in C# and registered with
`YuspecActionAttribute`.

```yuspec
play_animation Door "Open"
damage Player by self.damage
spawn self.drops at self.position
```

Some actions may be built in by the YUSPEC runtime:

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

Planned state machine constructs:

- `behavior Name for EntityType`
- `state Name`
- `on Event -> State`
- `every interval:`
- `do:` entry action block

## Scenario

```yuspec
scenario "door opens with key" {
    given Player has "IronKey"
    when Player.Interact Door
    expect Door.state == Open
}
```

Scenario tests should let gameplay logic run without manually driving a Unity
scene. The Unity editor runner can later surface these results in the debugger.
Scenario blocks are currently skipped by the Unity parser.

## MVP Constraint

The first working slice supports the Door+Chest example subset. The language
should expand from working demos, not from speculative syntax.
