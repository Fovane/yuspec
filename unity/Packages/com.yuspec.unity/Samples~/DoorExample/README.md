# Door Example

This sample is the first planned vertical slice.

It demonstrates:

- A `Door` entity with `state` and `key`
- A `Player.Interact` event
- A `Player.has(Door.key)` condition
- Three actions: `set`, `play_animation`, and `play_sound`
- A scenario test target

Current status: this is the first implemented Unity runtime slice.

To test it in a Unity scene:

1. Add a `YuspecRuntime` object and assign `DoorExample.yuspec` to `specs`.
2. Add a Player GameObject with `YuspecEntity`, `EntityType = Player`.
3. Add a Door GameObject with `YuspecEntity`, `EntityType = Door`.
4. Give the player the `IronKey` tag or an inventory/property value containing
   `IronKey`.
5. Call `YuspecRuntime.Emit("Player.Interact", playerEntity, doorEntity)`.
6. Open `Window > YUSPEC > Debugger` to inspect the matched handler, condition,
   actions, recent event, and debug trace.

Limitations:

- Only the Door-style subset is implemented.
- `behavior` and `scenario` blocks are parsed as future syntax and skipped.
- `play_sound` is a placeholder action that logs until a project binds real audio.
