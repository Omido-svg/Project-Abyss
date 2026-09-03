# Project Abyss Integration

Abyss Weapon Framework Runtime은 Project Abyss 코드를 참조하지 않습니다. Project Abyss 쪽에서 Adapter를 둡니다.

## 권장 연결

```text
YujinMechanic WeaponChanged
        ↓
AbyssWeaponLoadoutAdapter
        ↓
WeaponMountController.TryApplyLoadout(...)
```

백우/적설/낙일의 전투 수치는 기존 `YujinMechanic`에 남겨두고, 시각적/물리적 장착 상태만 Weapon Framework Loadout으로 전환하는 것이 권장됩니다.

## Damage Adapter

```text
WeaponHitInfo
    ↓
AbyssWeaponDamageAdapter : IWeaponDamageReceiver
    ↓
Project Abyss DamageRequest / DamageManager
```

Adapter는 Collider 또는 그 parent에 붙이고, HitCollider를 Project Abyss BodyPart/Hitbox와 연결하면 됩니다.

Framework 안에 `Character`, `BodyPart`, `Skill`, `BattleManager`, `DamageManager` 참조를 추가하지 마세요.
