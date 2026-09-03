# Quick Start

## Sword

1. Weapon Root에 OneHandMelee preset.
2. Grip_Main을 손잡이 중심에 배치.
3. BladeStart / BladeEnd를 날의 양 끝에 배치.
4. MeleeAttackProfile에서 radius, mask, power 설정.
5. 공격 Animation Event에 `BeginAttackWindow`, 종료 frame에 `EndAttackWindow`.

## Rifle

1. HitscanGun preset.
2. Grip_Main, 필요 시 Grip_Support.
3. Muzzle point 배치.
4. HitscanAttackProfile에서 range/pellets/spread 설정.
5. `WeaponAttackController.TryAttack("primary")` 호출.

## Projectile

1. ProjectileWeapon preset.
2. ProjectileOrigin 배치.
3. ProjectileAttackProfile에 projectile prefab 지정.
4. prefab에는 `WeaponProjectile`이 없어도 runtime에서 자동 추가됩니다. 커스텀 projectile 동작이 필요하면 `WeaponProjectile`을 상속하세요.

## Generic Skeleton

1. `WeaponSkeletonMap` 추가.
2. Left/Right UpperArm, LowerArm, Hand를 수동 매핑.
3. 손가락 Pose가 필요하면 finger bone도 매핑.
4. `MappedWeaponIK` 추가.
