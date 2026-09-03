# Changelog

## 1.2.0

### Production collider workflow
- Added `Duplicate` and `Mirror L/R` buttons to Sphere/Capsule collider inspectors.
- Added Hierarchy menu commands to duplicate or mirror the selected Secondary Rig collider.
- Mirror uses the character root local X=0 plane and attempts to remap the opposite Humanoid bone automatically.
- Added `Auto Fit To Weighted Skinned Mesh` per-collider tool.
- Added `Auto Fit All Body Colliders` to `SecondaryRigController` inspector.
- Auto Fit samples SkinnedMesh vertices influenced by the collider Follow Target; generated results remain an editable starting point.

### Collider Scene visibility
- Non-selected Secondary Rig colliders can now be shown as muted clean outlines in Scene View.
- Added `Tools > Project Abyss > Secondary Rig > Scene Gizmos > Show All Colliders`.
- Added optional collider labels toggle.
- Selected collider still uses one editable outline, endpoint/center position handles, and one radius handle.

### Distance LOD
- Added Full / Reduced / Minimal / Disabled simulation tiers.
- Default distances: 10m / 25m / 40m.
- Reduced tier defaults to 1 substep + 1 iteration, body collision ON, secondary-to-secondary collision OFF.
- Minimal tier defaults to spring/constraints only with expensive collisions OFF.
- Disabled tier snaps once to TGT pose and stops simulation until the character returns in range.
- Optional Camera override and distance-reference Transform supported.

### Performance diagnostics
- Added runtime `SecondaryRigPerformanceStats`.
- Controller inspector shows current LOD, distance, chain/node count, collider count, cross constraints, substeps/iterations, last simulation ms and smoothed ms in Play Mode.

### Production validation
- Added `Validate Production Setup` to Controller inspector and Setup Wizard.
- Validates Animator/Humanoid status, Preset Library, chain bindings, duplicate roots, hierarchy ownership, missing preset names, collider health, follower targets, and LOD distance ordering.

### Preset Library improvements
- Preset Library implementation now lives in `SecondaryRigPresetLibrary.cs` so Unity can resolve a proper same-name Script asset.
- Added data version, Add Missing Recommended Presets, Validate Library, Duplicate Preset, and guarded Reset All.
- Setup Wizard adds missing defaults to an existing library without overwriting project tuning.
- If an older v1.0/v1.1 preset asset becomes a missing-script asset after package replacement, use `Create / Load Recommended Presets` once to create the v2 asset and reassign it. Chain/Collider data on the character is unaffected.

### Upgrade stability
- Existing Controller/chain/collider serialized field names were preserved.
- Existing v1.1 Stress Test and physics bindings do not need rebuilding.
- Rebuild is only needed when JSON/bone bindings change or when you intentionally regenerate recommended body colliders.

## 1.1.0
- Clean sphere/capsule Scene handles and visibility improvements.
- Reusable `SecondaryRigStressTest` with Gentle / Strong / Extreme presets.
- Generated collider root is assigned directly to `SecondaryRigController.ColliderRoot`.

## 1.0.0
- Initial reusable Target/Spring runtime solver.
- JSON manifest importer and setup wizard.
- Preset-driven Hair/Cloth physics.
- Humanoid body proxy collider generator.
- Secondary-to-secondary particle collision.
- Wide-cloth cross-chain constraints.
- Teleport/spawn reset and runtime validation tools.
