# Vision

YUSPEC is pivoting from a general event-driven executable specification language
into a Unity gameplay specification and orchestration layer.

Old direction:

```text
General event-driven executable specification language
```

New direction:

```text
Unity gameplay specification and orchestration layer
```

The reason is practical. General DSLs are hard to sell because the audience has
to imagine a use case. Unity developers already have a concrete pain: gameplay
logic spreads across many MonoBehaviours, Inspector references, trigger scripts,
state controllers, quest scripts, and one-off event handlers.

YUSPEC should make gameplay flow easier to read, diff, test, and debug than
scattered scripts or large node graphs.

## Product Position

YUSPEC is a text-based gameplay rule layer for Unity.

It does not replace Unity. It does not remove C#. C# remains the technical bridge
to Unity APIs, components, animations, audio, physics, and scene objects.

YUSPEC centralizes rules:

- Entities and gameplay properties
- Event handlers
- Conditions
- Action calls
- State machines
- Scenario tests

The north star:

```text
A Unity developer should be able to open one .yuspec file and understand the
gameplay flow that would otherwise be scattered across many MonoBehaviour
scripts.
```

## Target User

The first target users are:

- Solo Unity developers
- Small indie teams
- Prototype and game jam developers
- Developers building doors, quests, triggers, loot, enemy states, and boss flows
- Developers who dislike both C# script sprawl and large visual node graphs

## Design Principles

- Readability is more important than clever syntax.
- Unity API details belong in reusable C# actions.
- Gameplay intent belongs in `.yuspec` files.
- Silent failure is not acceptable.
- Strict mode should be the default Unity experience.
- Debugging is a core feature, not an afterthought.
- Every new language feature should serve a demo or real workflow.
