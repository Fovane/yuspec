# Door Example

This sample is the first implemented vertical slice.

It demonstrates:

- A `Player` entity with an inventory
- A `Door` entity with `state` and `key`
- A `Chest` entity with `state` and `reward`
- A `Player.Interact` event
- A `Player.has(Door.key)` condition
- A `Chest.state == Closed` condition
- Four actions: `set`, `play_animation`, `play_sound`, and `give`
- A scenario test target

Current status: this is the first implemented Unity runtime slice. Scenario
blocks are still future syntax and are skipped by the parser.

To test it in a Unity scene:

1. Add a `YuspecRuntime` object and assign `DoorExample.yuspec` to `specs`.
2. Add a Player GameObject with `YuspecEntity`, `EntityType = Player`.
3. Add a Door GameObject with `YuspecEntity`, `EntityType = Door`.
4. Add a Chest GameObject with `YuspecEntity`, `EntityType = Chest`.
5. Give the player the `IronKey` tag or an inventory/property value containing
   `IronKey`.
6. Call `YuspecRuntime.Emit("Player.Interact", playerEntity, doorEntity)`.
7. Call `YuspecRuntime.Emit("Player.Interact", playerEntity, chestEntity)`.
8. Open `Window > YUSPEC > Debugger` to inspect the matched handler, condition,
   actions, recent event, and debug trace.

Limitations:

- Only the Door+Chest event-handler subset is implemented.
- `behavior` and `scenario` blocks are parsed as future syntax and skipped.
- `play_sound` is a placeholder action that logs until a project binds real audio.
