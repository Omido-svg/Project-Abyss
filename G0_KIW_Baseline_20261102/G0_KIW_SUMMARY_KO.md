# G0 K/I/W baseline snapshot

- Time UTC: 2026-11-04T03:30:12.7164362Z
- Git: Default-Battle-Test @ bc6168ccc94f1ad65f84d912a01c956fe8aa7871
- Scene: Assets/5. Scenes/Battle Test Scene.unity (dirty=True, missing=0)
- Skills: 164; effect entries: 118; emotion augments: 63
- Content+meta fingerprints: 603; source evidence lines: 41
- PlayMode Golden scenarios: 10 (previously run in live Unity)
- Keyword missing: 5; undecided: 10; effect manual reviews: 20
- Authored cards with weight>1: 0
- G0-KIW audit PASS=10 FAIL=0

## Scope
No change to production combat, ScriptableObject values, runtime status, or enum serialization. G0 audit PASS is not R1-R6 feature completion.

## Files
- G0_ASSET_HASH_MANIFEST.csv: asset and .meta hash per GUID
- G0_I_W_SOURCE_EVIDENCE.csv: source path and line evidence
- G0_I_REACTION_BOUNDARIES.csv: mutually distinct reaction concepts
- G0_KIW_POLICY_LEDGER.md: unresolved rules, risks and protection list
- G0_KIW_FREEZE.json: all verification counts and hashes
- ../G0_Ability_Audit_20261029/G0_GOLDEN_BASELINE.json: captured PlayMode trace
