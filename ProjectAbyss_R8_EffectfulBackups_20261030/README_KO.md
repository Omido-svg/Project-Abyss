# R8 효과 카드 배치 전환 원본 보호 (2026-10-30)
기본 게임 실행 경로는 레거시. 이 폴더는 명시적 Opt-in 이관 전 원본 바이트 보존만 수행합니다.
검사 대상: SkillDefinition 145 / 중복 ID 종류 4
배치 전환 조건 통과: SkillDefinition 68 / 루트 EffectEntries 85
저장된 .asset + .meta 원본 복사본 136개
원본을 실제 변경하거나 신규 Command에 전문 효과를 완전 포팅한 것이 아닙니다.
R7ExclusiveEntryMigrationScope가 명시적으로 켜진 경우에만 AbilityRunner.Plan→LegacySkillEffectEntryAdapter로 전환됩니다.
어떤 이관에서든 두 실행 경로를 동시 구독시키면 안 됩니다.
