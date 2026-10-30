R7 NATIVE MIXED DATA STAGE ONLY - 2026-10-31
Production runtime routing unchanged. 7 newly-authored data assets contain entry order, trigger, bleeding amount and duration, plus rolled-damage operation kind. Damage calculation and runtime parity not yet migrated.
Apply: copy Assets/ inside this package's unique folder into the Unity project root.
Rollback: remove the new native mixed SO script, editor migration script and NativeMixed assets/folder including their meta files. Original SkillDefinition assets have not been modified.
Backups are for recovery only, not for copying over active Assets.
