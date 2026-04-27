# Demo Dungeon

This sample combines supported v1 event rules, state machines, and scenarios.

It demonstrates:


- Key pickup and door unlock
- Chest reward
- Goblin state machine transitions
- Boss phase transition event
- Exit unlock on boss death
- Scenario assertions for core flow

Setup:

1. Add `YuspecRuntime` and assign `DemoDungeon.yuspec`.
2. Add `YuspecEntity` components matching entity types in the spec.
3. Emit interaction and combat events from your gameplay bridge.
4. Use the debugger tabs to inspect events, actions, state, and scenario results.
