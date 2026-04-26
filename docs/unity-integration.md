# Unity Integration

YUSPEC for Unity uses `.yuspec` files for gameplay rules and C# for technical
actions.

```text
.yuspec files
    v
parser / validator / runtime
    v
Unity runtime bridge
    v
GameObjects and MonoBehaviours
```

## Runtime Concepts

### YuspecRuntime

Scene-level coordinator. It loads spec assets, owns strict-mode diagnostics,
registers entities, records recent events, and executes actions through the
action registry.

### YuspecEntity

MonoBehaviour placed on Unity objects that should participate in YUSPEC rules.
It exposes entity id, entity type, tags, current state, and a runtime property
bag.

### YuspecEventBridge

Small MonoBehaviour used by gameplay components or UnityEvents to emit YUSPEC
events such as `Player.Interact`, `Player.EnterZone`, `Boss.Died`, or
`Boss.HealthBelow`.

### YuspecActionRegistry

Discovers C# methods marked with `YuspecActionAttribute`, validates duplicate
names, and invokes actions by name.

### YuspecActionAttribute

Attribute that binds a readable YUSPEC action name to a C# method.

```csharp
[YuspecAction("damage")]
public void Damage(YuspecEntity target, int amount)
{
    target.GetComponent<Health>().ApplyDamage(amount);
}
```

### YuspecDebugWindow

Unity EditorWindow under `Window > YUSPEC > Debugger`. It should show loaded
specs, diagnostics, registered actions, scene entities, current states, recent
events, failed conditions, and scenario results.

### YuspecSpecAsset

Planned import/runtime representation for `.yuspec` files. The initial scaffold
uses `TextAsset[]` while the importer and parsed representation are designed.

## Integration Rule

YUSPEC should not directly call Unity APIs from the DSL. Unity-specific work
belongs in C# actions and bridge components.
