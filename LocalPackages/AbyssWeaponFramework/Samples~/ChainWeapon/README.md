# Chain Weapon Sample

1. Create a ChainRoot with direct child link objects.
2. Select ChainRoot and run `Tools > Abyss Weapon Framework > Chain > Build Joints From Selected Root`.
3. Add colliders to physical links.
4. Put `ChainWeaponPhysicsModule` on the weapon root.
5. Put `ChainWeaponHitRelay` on links that should produce physical hits.

For production chains, tune Rigidbody mass/drag, joint limits, solver iteration settings, collision layers and link colliders for your art scale.
