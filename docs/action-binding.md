# Action Binding

YUSPEC actions are implemented in C# and called by name from `.yuspec` files.

This keeps gameplay orchestration readable while preserving full access to Unity
APIs in normal C# code.

## Attribute Binding

```csharp
public sealed class CommonGameplayActions
{
    [YuspecAction("play_animation")]
    public void PlayAnimation(YuspecEntity target, string animationName)
    {
        var animator = target.GetComponent<Animator>();
        if (animator != null)
        {
            animator.Play(animationName);
        }
    }
}
```

## Example Actions

```csharp
[YuspecAction("damage")]
public void Damage(YuspecEntity target, int amount)
{
    var health = target.GetComponent<Health>();
    if (health != null)
    {
        health.ApplyDamage(amount);
    }
}

[YuspecAction("spawn")]
public void Spawn(string prefabId, YuspecEntity spawnPoint)
{
    // Resolve prefab and instantiate it at the spawn point.
}

[YuspecAction("show_dialogue")]
public void ShowDialogue(string dialogueId)
{
    // Delegate to the game's dialogue system.
}

[YuspecAction("start_quest")]
public void StartQuest(string questId)
{
    // Delegate to the game's quest system.
}
```

## Strict Validation

Strict mode should validate:

- Action exists
- Argument count matches
- Argument types can be converted
- Duplicate action names are reported
- Missing bindings are surfaced in the debugger

The initial Unity scaffold implements action discovery and basic invocation. Full
DSL-to-action semantic validation will come with the first parser vertical slice.
