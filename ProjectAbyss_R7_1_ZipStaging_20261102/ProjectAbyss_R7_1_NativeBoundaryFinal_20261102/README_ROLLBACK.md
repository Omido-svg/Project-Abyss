# R7-1 native boundary final patch — 2026-11-02

## Overview
This package is already applied to the live Unity project via MCP. Do NOT install again.
It contains changed scripts and their original GUID-preserving .meta, the Preparation native status/flat-setup assets and their .meta, original code backups and verification notes.
No original SkillDefinition, prefab, or scene data was modified, nor was any git commit/push performed.

## Installation on a different copy of the same repository
1. Confirm Unity 6000.56f project and prerequisites G0..R7 native systems are present.
2. Stop PlayMode.
3. Copy the Assets directory of this ZIP into the project root while preserving matching .meta.
4. Unity Refresh/compile.
5. If needed, run menu Game System Verification > R7-1 - Native Boundary and Adapter Isolation.
6. Do NOT use the original SkillDefinition.Effects to re-dispatch already native effects.

## Rollback
1. Exit PlayMode.
2. Restore each C# and .meta from Backup/BeforeClosure to its original relative Assets path.
3. Keep preexisting status/setup native ScriptableObjects and their GUID intact unless
   explicitly reverting the entire earlier R7-1 migration as well.
4. Refresh/compile. Do NOT save dirty Battle Test Scene as part of rollback.
5. Review Verification/R7_1_FINAL_REPORT.md before deleting any effect data.

## Test scope
R7-1 EditMode gate 20/20 PASS, script compiler/console 0 errors.
R9 full regression NOT RUN; PlayMode tests NOT RUN in this session by user request.
Previous Context 37 parity tests were historical, not repeated.

## Canonical rule constraints
Reference: 전투 시스템 규칙(1029)(3).md.
The elite test Preparation card is not defined with specific values in that document.
It is not automatically declared canonically balanced by this patch.
Refer to Verification/R7_1_FINAL_REPORT.md for policy debt and R7-8 follow-up.
