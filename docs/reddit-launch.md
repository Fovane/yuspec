# Reddit Launch Notes

YUSPEC should not be introduced as a finished Unity replacement. The honest
launch angle is early feedback on a working Unity gameplay rule layer.

## Target Subreddits

- `r/Unity3D`
- `r/gamedev`
- `r/IndieDev`

Post to one subreddit first, learn from the response, then adapt the wording.

## Suggested Title

```text
I am building a text-based gameplay rule layer for Unity to reduce MonoBehaviour script sprawl
```

## Short Post Draft

```text
I am building YUSPEC, a text-based gameplay rule layer for Unity.

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

The first Unity slice now runs a Door+Chest example:

- Player has IronKey
- Door opens only if the condition passes
- Chest opens when closed
- Chest gives Gold through a C# action binding
- Debugger shows loaded specs, diagnostics, entities, events, and action trace

The package is still early. State machines, executable scenario tests, and a larger demo dungeon are next.

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
- Do not claim it is production ready.
- Do not call it a finished v1.0 until state machines, scenario tests, package
  tests, and the Demo Dungeon are implemented.
