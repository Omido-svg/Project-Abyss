# R7-2 GUID/Save Compatibility patch (2026-11-02)

This patch has ALREADY been applied to the active Unity project with MCP.
Do not install it there again. No source SkillDefinition, existing asset GUID,
scene or character loadout asset has been changed.

## New identity contract
- Four duplicate SkillIds remain in the authored SO source for old save/character mechanics.
- Eight GUIDs and references are held in a new Resources ScriptableObject.
- Additive new GUID arrays in CharacterSkillLoadoutState preserve old serialized ID lists.
- Old ID-only saves choose sole local asset or the explicitly selected PhaseE legacy default.
- New GUID-qualified saves select exact source or reject without fallback.
- Old ID-only saves cannot reconstruct which of two equally named assets was actually used.
- SkillCatalog.FindById rejects ambiguous duplicate lookups.
- SkillUpgradeState uses GUID-specific keys for these four groups.

## Installation on a different copy of this Unity project
1. Stop PlayMode and back up the project.
2. Copy Assets/ content from ZIP into Unity project root, preserve .meta files.
3. Refresh/compile.
4. Run only the targeted EditMode menu:
   Game System Verification > R7-2 - Skill ID GUID and Save Contract (Edit Only).
5. Inspect Verification/R7_2_FINAL_HANDOFF.md and GUID manifest.

## Rollback
1. Stop PlayMode.
2. Restore old C# script versions (with .meta) from Backup/BeforeR7_2
   to their original Assets paths.
3. Remove ONLY the NEW R7SkillIdentityRegistry20261102.cs, its .meta,
   the NEW R7Stage2SkillIdContractGate20261102.cs, its .meta,
   and the new Assets/.../ProjectAbyss/R7 directory including .asset and .meta,
   IF the user has explicitly decided to revert the entire R7-2 patch.
4. Refresh/compile. Do NOT modify any of the original eight SkillDefinition .asset/.meta.
5. Never save the unsaved Dirty Battle Test Scene as part of rollback.

## Verification scope
R7-2 dedicated EditMode gate PASS 18/18; old JSON save restore PASS;
Unity compile and console errors 0. R9/PlayMode intentionally NOT RUN.
No Git commit/push. Source GUID hashes are in Verification/.
