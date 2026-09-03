# Abyss Weapon Framework Architecture

## 1. 설계 원칙

Framework는 `Transform`, `Animator`, `ScriptableObject`, Unity Physics, Prefab만 이해합니다. 게임별 전투 규칙을 직접 참조하지 않습니다.

```text
Game Character / Battle / Inventory
            │ Adapter
            ▼
      Framework Contracts
            │
            ▼
Mount / Pose / IK / Attack / Physics / FX
```

## 2. Core

- `WeaponDefinition`: 정적 무기 정보와 Prefab, 태그, Attack Profile 목록.
- `WeaponInstance`: 실제 Prefab Runtime 인스턴스. Grip/Point/Module 검색과 RuntimeContext를 보유.
- `WeaponModule`: Equip/Unequip/Remount lifecycle 확장점.
- `WeaponRuntimeContext`: MountController, EquipHandle, Owner를 연결.
- `IWeaponOwner`: 선택적 Owner 추상화.

## 3. Attachment

- `WeaponSocket`: Character 측 장착 지점.
- `WeaponGripPoint`: Weapon 측 정렬 기준점.
- `WeaponMountController`: Grip ↔ Socket 해석, 점유, 생성, Loadout, Remount.
- `WeaponEquipHandle`: 장착된 한 인스턴스와 모든 Grip binding의 런타임 핸들.
- `WeaponHolsterController`: Draw/Stow convenience API.
- `WeaponPoint`: Muzzle/Blade/Aim/FX 등 의미 있는 Marker.

### 불변식

1. Weapon Root를 실제 parent하는 Mount binding은 1개입니다.
2. Support/Auxiliary binding은 Socket을 점유하지만 Root를 추가 parent하지 않습니다.
3. Dual wield는 WeaponInstance 2개입니다.
4. Two-handed는 WeaponInstance 1개가 hand socket 2개를 점유합니다.

## 4. Hand Pose / Animation

- `WeaponSkeletonMap`: 논리적 WeaponBoneId ↔ 실제 Transform.
- Humanoid는 자동 매핑 가능.
- Generic rig는 Inspector에서 수동 매핑.
- `HandPoseDefinition`: finger local rotation 저장.
- `WeaponHandPoseController`: 각 손의 활성 Pose를 LateUpdate에 적용.
- `WeaponHandPoseModule`: Equip binding에 따라 Primary/Support Pose를 자동 등록.
- `HumanoidWeaponIK`: Unity Humanoid IK.
- `MappedWeaponIK`: Generic skeleton용 2-joint CCD 방식 support-hand 보정.

## 5. Attack

모든 공격은 `WeaponAttackProfile` + `WeaponAttackModule` 조합입니다.

- `MeleeAttackProfile` + `MeleeWeaponModule`
- `HitscanAttackProfile` + `HitscanWeaponModule`
- `ProjectileAttackProfile` + `ProjectileWeaponModule`

게임 입력은 `WeaponAttackController.TryAttack("primary")` 또는 해당 Module API를 호출합니다.

Melee는 Animation Event에서 `BeginAttackWindow` / `EndAttackWindow`를 호출할 수 있습니다.

## 6. Hit Boundary

Framework는 물리 충돌을 찾은 뒤 `WeaponHitInfo`를 생성합니다.

`IWeaponDamageReceiver`를 구현한 가장 가까운 parent Adapter로 전달합니다.

Framework가 모르는 것:

- HP
- 방어력
- 팀/진영
- 상태이상
- Body Part
- 합/턴
- 스킬 데이터
- 실제 최종 피해량

`WeaponHitInfo.Power`는 게임 측 계산에 사용할 수 있는 원시 강도 값일 뿐 최종 damage가 아닙니다.

팀/무적/아군 관통 같은 후보 제외 규칙은 Owner 측 `IWeaponHitFilter`로 처리합니다. false를 반환한 Collider는 Framework가 무시하고 다음 후보를 계속 탐색합니다.

## 7. Physics / Special Weapons

`ChainWeaponPhysicsModule`은 장착/수납 상태에 따라 chain Rigidbody를 활성/비활성화합니다.

`ChainWeaponHitRelay`는 물리 링크 충돌을 같은 `WeaponHitInfo` 경계로 변환합니다.

채찍, 부메랑, 빔, 락온 등은 새 `WeaponModule` / `WeaponAttackModule`을 추가해 Core 변경 없이 구현할 수 있습니다.

## 8. Effects

- `WeaponEffectModule`: event id별 Particle/Audio/Prefab 재생.
- `WeaponAttackEffectsModule`: AttackStarted / HitProduced 이벤트를 FX event로 연결.

FX는 Damage 규칙과 분리됩니다.
