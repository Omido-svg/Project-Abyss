# R7-0 migration source ledger
145 SkillDefinitions all inventoried by GUID; not a claim that every effect is fully migrated.

- BLOCKED_DUPLICATE_ID: 8
- METADATA_OBSERVER_ONLY: 9
- PENDING_EFFECT_NATIVE: 42
- PENDING_NO_EFFECT_POLICY: 49
- VERIFIED_NATIVE_GRAPH: 36
- VERIFIED_PREPARATION_NATIVE_GRAPH: 1

- Augments: 63 (pending canonical: 1)
- Run items: 60 (temporary mechanics: 60)
- Passive/mechanic code *candidates*: 18 (needs character-wise R7-9 review, not a proven passive count)
- Residual adapter callsites: 6 (source search candidates, not all live routes)
- 109/145 non-36-native skills include 1 newly direct-native Preparation, 8 duplicates and other pending rows.
- R7-1's Preparation does not count inside the prior 36, to avoid retroactively changing the baseline.

See four CSVs for full source-level evidence.
