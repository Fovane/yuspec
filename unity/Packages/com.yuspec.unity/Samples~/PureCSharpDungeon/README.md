# Pure C# Dungeon Demo

This sample implements the same top-down dungeon flow as `Samples~/TopDownDungeon`,
but without YUSPEC.

Gameplay rules are encoded directly in C#:

- Chest gives the Room 2 key
- Door stays closed without the key
- Door opens with the key
- Goblin has Idle/Chase/Attack/Dead state logic
- Boss has Phase1/Phase2/Dead state logic
- Exit door opens on boss death
- Merchant and boss dialogue are hard-coded arrays

The sample is intentionally small and primitive-only. Its purpose is comparison:
the YUSPEC version keeps gameplay orchestration in `.yuspec` files, while this
version moves the same rules into C# branches, timers, fields, and method calls.
