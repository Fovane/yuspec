# Goblin AI

This sample demonstrates supported v1 state machine syntax.

It demonstrates:

- Entity properties
- `behavior` binding to an entity type
- State transitions (`on Trigger -> TargetState`)
- Repeated `every` actions
- State entry and `do` actions
- Scenario block parsing

Setup:

1. Add `YuspecRuntime` and assign `GoblinAI.yuspec`.
2. Add a Goblin GameObject with `YuspecEntity` (`EntityType = Goblin`).
3. Emit `Goblin.PlayerSeen`, `Goblin.InAttackRange`, and `Goblin.PlayerOutOfRange`.
4. Open `Window > YUSPEC > Debugger > State Machines` and observe transitions.
