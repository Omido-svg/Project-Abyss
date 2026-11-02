# R7-4 Native Compound/Conditional Roll Guard patch, 2026-11-02

ALREADY APPLIED to the active Unity project through Unity MCP.
No reinstallation is needed in the active project.

## What changed
- Adds native R7MixedOperationEntry per-roll and conditional gates.
- Routes mixed-operation timing/roll/conditional decisions through a typed native gate.
- Preserves existing 7 native mixed graphs as schema2; no authored SO is rewritten.
- Previously migrated 2 damage+bleeding compound cards remain native and in exact authored order.
- No new SkillDefinition was migrated in this stage. Specialized sources remain for R7-5/6/7.
- No GUID or original SkillDefinition was modified.

## Install on a matching project copy
1. Exit PlayMode and back up the project.
2. Merge the ZIP's Assets tree into the Unity project root preserving all .meta files.
3. Refresh/compile.
4. Optional target check only:
   Game System Verification > R7-4 - Compound/Conditions/Roll Gate (Edit Only)
Do NOT run R9 unless explicitly requested.

## Restore
1. Exit PlayMode.
2. Restore modified C# files and .meta from Backup/BeforeR7_4 to matching Assets paths.
3. Only when rolling back R7-4 completely and no subsequent stage uses them,
   remove the NEW R7NativeMixedGate20261102.cs + .meta and
   R7Stage4NativeCompoundGate20261102.cs + .meta.
4. Refresh/compile. Do NOT touch original 7 native SOs, the original 145 SkillDefinitions,
   GUID files, or the dirty Battle Test Scene.
5. Git commit/push not performed.

## Verification scope
R7-4 focused EditMode data and native predicate Gate 59/59 PASS.
Previous schema2 source-value Gate 7/7 PASS.
No R9 full regression, no PlayMode dynamic parity test.
Full details: Verification/R7_4_FINAL_HANDOFF.md.
