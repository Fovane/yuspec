# Action Binding

Use `YuspecActionAttribute` to expose C# methods to YUSPEC:

```csharp
[YuspecAction("play_animation")]
public void PlayAnimation(YuspecEntity target, string animationName)
{
    target.GetComponent<Animator>().Play(animationName);
}
```

Strict mode should validate that every action referenced by a spec has a
registered binding and receives the expected number and type of arguments.
