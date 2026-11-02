# Project Abyss R7-3 native general single effects
Already applied through Unity MCP to active project F:\GitCorp\Project-Abyss on 2026-11-02.

## Purpose
Four source skills are migrated from their legacy EffectDefinition.Apply to
native single-effect ScriptableObjects and typed R5 command dispatch.
Original SkillDefinition assets and GUIDs remain untouched.
Original 15 resource + 9 Bleeding native source skills remain unchanged.

## Apply to another copy of the SAME project
1. Back up the Unity project, stop PlayMode.
2. Merge this archive's Assets/ directory into the project Assets/ root.
3. Preserve all .meta GUIDs. Refresh/compile.
4. Optional minimal check: Game System Verification >
   R7-3 - Verify Native Single Effects (Edit Only).
Do not run Stage again when the four native assets already exist; Stage refuses overwrite.

## Revert / rollback
1. Stop PlayMode; make a second backup if later R7 stages have already been applied.
2. Restore BattleRuntimeServices.cs, BattleRuntimeFactory.cs, SkillEffectDispatcher.cs
   and their .meta from Backup/BeforeR7_3 at their original paths.
3. Only if no later code depends on them, remove the NEW 4 R7-3 .cs + .meta,
   and the NEW 4 NativeSingles .asset + .meta.
4. Refresh/compile. Never modify or remove the original SkillDefinition source assets.
5. Never save the dirty Battle Test Scene for the purpose of rollback.
No git commit or push was made.

## Validation
Only 14 R7-3 EditMode graph/data assertions plus route/identity checks:
PASS. Unity compile/console errors 0. R9 NOT run. PlayMode NOT run.
Runtime battle parity remains subject to R7-12 testing.
Details: Verification/R7_3_FINAL_HANDOFF.md.
