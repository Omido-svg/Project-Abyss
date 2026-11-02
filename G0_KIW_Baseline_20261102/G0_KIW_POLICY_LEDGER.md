# G0 K/I/W risk and policy ledger (2026-11-02)

## K: character unique keywords
- Required keyword missing: 6; policy undecided: 10; see original G0_K_REQUIRED.csv. The scan uses name/path heuristics and is NOT proof of runtime implementation.
- Olaf Bleed: planned scope is per Part, but current G0 required list may say Actor. Reconcile storage and actual damage target before R2-K.
- Hifumi BloodPrice: distinguish from Olaf skill identifiers with similar names. Ownership requires explicit validation.
- Unresolved: Madness extra incoming damage channel; Mark detonation weapon snapshot; Stealth vs Taunt targeting conflict. No invented policies.
- Counter is a SkillTag, not a N/T status. Part bleed/mark and per-slot taunt must not be treated as universal actor status.

## I: reaction and playback
- The original ActionResolver runs ResolvePair then PlayClash. The StepwiseSession exists separately and is not presumed production-default.
- HifumiCounterRoll, EnemyCounterRoll, SecondaryHit, FollowUpRoll, TriggeredSkillAction are different concepts. Preserve behavior.
- The current Golden captures outcome and trace; this is not a complete R6-I mid-exchange interruption Golden.

## W: weight allocation
- Authored SkillDefinitions whose weight exceeds 1: 0. Synthetic legacy W01-W03 PlayMode fixture does not prove authored weighted content.
- Preserve SecondaryTargetPart, AnotherPartOnPrimaryCharacter, secondary damage, UI and Undo semantics before enabling SlotWeighted mode.
- R4-W/R5-W/R6-W later own preview/commit/reaction, actual target damage event count and stable slot allocation tests.
