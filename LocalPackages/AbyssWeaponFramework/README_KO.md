# Abyss Weapon Framework 2.0

Unity 프로젝트에 독립적으로 설치하는 범용 3D Weapon Framework입니다. 패키지 내부 Runtime은 Project Abyss의 `BattleManager`, `Character`, `Skill`, `BodyPart`, 피해 공식에 의존하지 않습니다.

## 포함 범위

- Socket / Grip 기반 장착
- 한손, 양손, 쌍수, 검+방패, 총기, 수납(Holster/Remount)
- 동일 WeaponDefinition의 복수 인스턴스 장착
- Humanoid Support-Hand IK
- Generic/Custom Skeleton용 `WeaponSkeletonMap` + `MappedWeaponIK`
- 재사용 가능한 손가락 `HandPoseDefinition`
- Melee sweep / Hitscan / Projectile 공격 모듈
- 공통 `WeaponHitInfo` + `IWeaponDamageReceiver` + `IWeaponHitFilter`
- Muzzle/Blade/Aim/Projectile/FX 등의 `WeaponPoint`
- FX/Audio 모듈
- Chain weapon Rigidbody/Joint 보조 및 충돌 Hit Relay
- Weapon Module lifecycle 확장
- Setup Wizard / Hand Pose Editor / Anchor Editor / Skeleton Mapper / Validation
- Dual Pistol / Two-Handed Sword / Rifle / Chain Weapon Samples

## 설치

Unity Package Manager에서 **Add package from disk...**를 선택하고 이 폴더의 `package.json`을 선택합니다.

Local UPM 사용 시 `Packages/manifest.json` 예:

```json
"com.projectabyss.weapon-system": "file:C:/YourPath/AbyssWeaponFramework"
```

## 가장 빠른 사용법

1. `Tools > Abyss Weapon Framework > Setup Wizard`
2. Character Animator 지정
3. `Ensure Framework Components`
4. Humanoid면 `Create / Repair Hand Sockets`와 `Auto Map Humanoid Skeleton`
5. Weapon Prefab을 Prefab Mode로 열고 Wizard의 Weapon Root에 지정
6. Preset 선택: OneHandMelee / TwoHandMelee / HitscanGun / ProjectileWeapon / ChainWeapon
7. `Apply Preset / Repair Structure`
8. Grip/Blade/Muzzle marker를 Scene View에서 실제 모델에 맞게 이동
9. `Preview Equip`으로 손 위치 확인
10. `Validate Weapon`
11. WeaponDefinition 생성

## 게임 코드와의 연결

Framework의 공격 책임은 여기까지입니다.

```text
Melee / Hitscan / Projectile
        ↓
WeaponHitInfo
        ↓
IWeaponDamageReceiver
        ↓
게임별 Adapter
```

게임 측 예:

```csharp
public sealed class MyDamageAdapter : MonoBehaviour, IWeaponDamageReceiver
{
    public void ReceiveWeaponHit(in WeaponHitInfo hit)
    {
        // 여기서 HP, 부위 피해, 상태이상, 팀 판정 등을 처리
    }
}
```

Framework는 HP를 직접 줄이지 않습니다.

## 대표 구성

### 쌍권총

같은 Pistol Definition을 Loadout에 두 번 넣고:

- InstanceKey `Pistol_L`: `Grip_Main -> LeftHand`
- InstanceKey `Pistol_R`: `Grip_Main -> RightHand`

### 양손검

한 WeaponInstance 안에:

- `Grip_Main -> RightHand`
- `Grip_Support -> LeftHand`, Support Grip의 IK 활성화
- `BladeStart`, `BladeEnd`
- `MeleeWeaponModule`

### 소총

- `Grip_Main`, 필요 시 `Grip_Support`
- `Muzzle` 또는 `ProjectileOrigin`
- `HitscanWeaponModule` 또는 `ProjectileWeaponModule`
- 선택적으로 `WeaponAttackEffectsModule`

## 업그레이드

2.0은 1.0의 패키지 ID와 namespace(`ProjectAbyss.WeaponSystem`)를 유지합니다. 기존 장착 코드의 API를 최대한 유지하면서 기능을 확장했습니다. 자세한 내용은 `Documentation~/MIGRATION_2_0_KO.md`를 참고하세요.
