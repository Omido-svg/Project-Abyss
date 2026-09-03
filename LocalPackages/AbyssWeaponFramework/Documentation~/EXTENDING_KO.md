# Extending the Framework

## 새로운 공격 방식

`WeaponAttackModule`을 상속하고 `WeaponAttackProfile` ScriptableObject를 정의합니다.

```csharp
public sealed class BeamAttackModule : WeaponAttackModule
{
    public override WeaponAttackKind Kind => WeaponAttackKind.Custom;
    protected override bool ExecuteAttack(in WeaponAttackRequest request)
    {
        // Query / simulation
        // PublishHit(in hit);
        return true;
    }
}
```

## 새로운 특수 무기

장착 lifecycle만 필요하면 `WeaponModule`을 상속합니다.

예:

- WhipPhysicsModule
- ReturningProjectileModule
- ChargeWeaponModule
- TransformingWeaponModule
- HeatModule

이 모듈은 게임별 HP/Skill 시스템을 참조하지 않아야 재사용성이 유지됩니다.

## 게임별 Adapter

게임 규칙은 package 밖에서 다음 인터페이스를 구현합니다.

- `IWeaponDamageReceiver`
- `IWeaponOwner`
- `IWeaponAimProvider`
- `IWeaponHitFilter`

이 방식으로 FPS, TPS, 액션 RPG, 턴제 연출 시스템 모두 같은 Weapon Prefab 구조를 사용할 수 있습니다.
