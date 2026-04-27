# Door Example

This sample demonstrates supported v1.0.1 public preview event rules and scenario tests.

It demonstrates:

- A `Player` entity with an inventory
- A `Door` entity with `state` and `key`
- A `Chest` entity with `state` and `reward`
- A `Player.Interact` event
- A `Player.has(Door.key)` condition
- A `Chest.state == Closed` condition
- Four actions: `set`, `play_animation`, `play_sound`, and `give`
- Two runnable scenario tests

Current status: this sample uses supported runtime syntax.

To test it in a Unity scene:

1. Add a `YuspecRuntime` object and assign `DoorExample.yuspec` to `specs`.
2. Add a Player GameObject with `YuspecEntity`, `EntityType = Player`.
3. Add a Door GameObject with `YuspecEntity`, `EntityType = Door`.
4. Add a Chest GameObject with `YuspecEntity`, `EntityType = Chest`.
5. Ensure the player has `IronKey` in inventory or another runtime property.
6. Call `YuspecRuntime.Emit("Player.Interact", playerEntity, doorEntity)`.
7. Call `YuspecRuntime.Emit("Player.Interact", playerEntity, chestEntity)`.
8. Open `Window > YUSPEC > Debugger` to inspect events, actions, and state.
9. Click `Run Scenarios` in the debugger and verify both scenarios pass.

Limitations:

- `play_sound` logs until a project binds real audio.
