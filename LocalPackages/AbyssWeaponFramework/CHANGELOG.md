# Changelog

## 2.0.0

- Rebranded display name to **Abyss Weapon Framework** while keeping the v1 package id/namespace for upgrade compatibility.
- Added game-agnostic ownership and hit contracts: `IWeaponOwner`, `IWeaponDamageReceiver`, `IWeaponAimProvider`, `WeaponHitInfo`.
- Added reusable Attack Profiles and Melee / Hitscan / Projectile runtime modules.
- Added projectile sweep simulation and generic hit delivery.
- Added `WeaponAttackController`.
- Added `WeaponSkeletonMap`, Humanoid auto-map, manual Generic skeleton mapping, and `MappedWeaponIK`.
- Added reusable `HandPoseDefinition`, runtime pose controller/module, and editor capture tool.
- Added holster convenience controller.
- Added effect/audio event module and attack-effects bridge.
- Added chain physics module, physical collision relay, and chain setup editor utility.
- Reworked Setup Wizard with weapon presets, profile creation, preview equip, skeleton setup, and validation.
- Added Anchor & Grip Editor and expanded validators.
- Added Dual Pistol, Two-Handed Sword, Rifle, and Chain Weapon samples.
- Runtime still has zero dependency on Project Abyss battle/character/skill code.

## 1.0.0

- Socket/grip based mounting core.
- Loadouts, dual wield, two-handed socket occupancy, remount/stow.
- Humanoid support-hand IK.
- Generic points and WeaponModule lifecycle extension.
