# Debugging

Debugging is a core feature of YUSPEC. The product exists to make gameplay flow
visible, so the runtime must explain what happened and why.

## Debug Traces

The debugger should eventually show:

- Event trace: received events and matched handlers
- State trace: current states and state transitions
- Action trace: executed actions and arguments
- Condition trace: passed and failed conditions
- Scenario results: pass/fail results and assertion messages

## Unity Editor Window

The Unity package includes a debugger window:

```text
Window > YUSPEC > Debugger
```

Initial sections:

- Loaded Specs
- Diagnostics
- Registered Actions
- Scene Entities
- Recent Events
- Current States

The first useful debugger milestone is the Door example, now represented by the
runtime debug trace:

```text
Player.Interact with Door
condition Player.has(Door.key) passed
set Door.state = Open
play_animation Door "Open"
play_sound "door_open"
```

If the player has no key, the debugger should show the failed condition instead
of silently doing nothing.
