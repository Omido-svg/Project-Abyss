# v1 → v2 Migration

## 유지되는 것

- Package ID: `com.projectabyss.weapon-system`
- Runtime namespace: `ProjectAbyss.WeaponSystem`
- `WeaponDefinition`
- `WeaponInstance`
- `WeaponGripPoint`
- `WeaponSocket`
- `WeaponMountController`
- `WeaponLoadoutDefinition`
- `HumanoidWeaponIK`
- 기존 Equip/Unequip/Remount API

따라서 v1을 이미 사용 중인 프로젝트의 코드 변경을 최소화했습니다.

## 추가 권장 컴포넌트

Character Root:

- `WeaponOwnerProxy`
- `WeaponSkeletonMap`
- `WeaponHandPoseController`
- Humanoid: `HumanoidWeaponIK`
- Generic: `MappedWeaponIK`
- `WeaponHolsterController` (선택)

Weapon Root:

- `WeaponAttackController`
- 공격 방식별 `WeaponAttackModule`
- `WeaponHandPoseModule` (선택)
- `WeaponEffectModule` / `WeaponAttackEffectsModule` (선택)

Setup Wizard의 `Ensure Framework Components`와 `Apply Preset / Repair Structure`를 이용하면 기존 Prefab에 필요한 새 요소를 추가할 수 있습니다.
