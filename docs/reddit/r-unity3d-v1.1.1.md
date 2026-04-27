# r/Unity3D Post Draft

## Title

I made a public preview of YUSPEC, a text-based gameplay rule layer for Unity

## Body

Hi r/Unity3D,

I have been working on YUSPEC, a small text-based gameplay rule layer for Unity, and I just published a public preview package.

The idea is to keep Unity/C# responsible for engine integration, input, movement, audio, UI, and scene objects, while gameplay orchestration lives in `.yuspec` files:

```text
on Player.Interact with Chest when Chest.state == Closed:
    set Chest.state = Open
    give_item Player Chest.reward
    play_sound "chest_open"

behavior GoblinAI for Goblin {
    state Chase {
        every 0.01s:
            move_towards Player speed Goblin.chaseSpeed

        on InAttackRange -> Attack
    }
}
```

Current public preview includes:

- Unity Package Manager install from GitHub
- typed entity properties
- event rules and conditions
- behavior/state machine blocks
- scenario checks
- clickable Unity Console diagnostics
- hot reload for `.yuspec` files
- a primitive TopDownDungeon sample
- a VS Code extension for `.yuspec` syntax highlighting/completion

Repo and install instructions:

https://github.com/Fovane/yuspec

Release:

https://github.com/Fovane/yuspec/releases/tag/v1.1.1

UPM dependency:

```json
"com.yuspec.unity": "https://github.com/Fovane/yuspec.git?path=/unity/Packages/com.yuspec.unity#v1.1.1"
```

This is a public preview, not a production-ready framework. I am mainly looking for feedback from Unity developers on:

- whether the rule syntax feels readable
- where this would or would not fit in real Unity projects
- what diagnostics/debugging would make this practical
- whether the package setup works cleanly across Unity versions

Tested locally with Unity 6000.3.8f1. Feedback, criticism, and small sample requests are welcome.
