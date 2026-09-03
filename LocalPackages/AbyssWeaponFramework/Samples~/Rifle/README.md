# Rifle / Ranged Sample

`RifleExample` shows the optional `IWeaponAimProvider` contract. The Hitscan/Projectile module asks the owner for an aim ray when no explicit direction is passed.

`DemoDamageReceiver` is intentionally tiny. Real games should replace it with an adapter that forwards `WeaponHitInfo` into their own health/body-part/damage/status system.
