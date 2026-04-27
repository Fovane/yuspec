# Reddit Launch Notes

YUSPEC should not be introduced as a Unity replacement. The honest launch angle
is a v1.0 Unity package for a focused text-based gameplay rule layer.

## Target Subreddits

- `r/Unity3D`
- `r/gamedev`
- `r/IndieDev`

Post to one subreddit first, learn from the response, then adapt the wording.

## Suggested Title

```text
I made YUSPEC v1.0: a text-based gameplay rule layer for Unity to reduce MonoBehaviour script sprawl
```

## Short Post Draft

```text
I made YUSPEC v1.0, a text-based gameplay rule layer for Unity.

The goal is not to replace Unity or remove C#. C# still implements technical actions. YUSPEC is for centralizing gameplay rules that usually get scattered across many MonoBehaviour scripts.

Example:

entity Door {
    state = Closed
    key = "IronKey"
}

on Player.Interact with Door when Player.has(Door.key):
    set Door.state = Open
    play_animation Door "Open"
    play_sound "door_open"

The Unity package now runs Door+Chest and Demo Dungeon samples:

- Player has IronKey
- Door opens only if the condition passes
- Chest opens when closed
- Chest gives Gold through a C# action binding
- Goblin state machine transitions are represented in text
- Scenario tests can run in the debugger
- Debugger shows loaded specs, diagnostics, entities, events, action trace, state machines, and scenarios

It is not a replacement for Unity or C#. The point is to move gameplay flow out
of scattered MonoBehaviour scripts and into a readable `.yuspec` layer, while C#
keeps implementing technical actions.

I am looking for feedback from Unity developers:

- Would you use a text-based rule layer for gameplay flow?
- Where would this fit badly in a real Unity project?
- What diagnostics/debugger features would make it useful?
```

## What To Show

- The README GIF.
- A screenshot of `Window > YUSPEC > Debugger`.
- The `DoorExample.yuspec` file.
- A short clip showing Play Mode changing Door and Chest state.

## Avoid

- Do not claim YUSPEC replaces C#.
- Do not claim it replaces C#.
- Do not overclaim production use in large teams yet.
- Be explicit that v1.0 is a focused package release, not a mature Asset Store
  product.
