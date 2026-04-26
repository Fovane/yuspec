# Legacy Examples

The repository still contains general-purpose YUSPEC examples from the earlier
executable specification direction:

- `examples/game/01_mmo.yus`
- `examples/network/01_tcp_handshake.yus`
- `examples/workflow/01_approval.yus`
- `examples/distributed/01_orchestration.yus`
- `examples/iot/01_sensor.yus`
- `examples/simulation/01_traffic.yus`
- `examples/testing/01_scenario.yus`
- `examples/00_arena_min.yus`
- `examples/03_showcase.yus`

These examples are useful for understanding the current C++ compiler/runtime,
especially entity declarations, behaviors, event handling, scenarios, and tests.

They are no longer the primary product identity. The new primary direction is:

```text
YUSPEC is a text-based gameplay rule layer for Unity.
```

Future Unity examples should live under:

```text
unity/Packages/com.yuspec.unity/Samples~
```

The old examples should remain available until the Unity runtime is mature
enough to replace them with equivalent gameplay-focused samples.
