using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 적 역시 Character이므로 플레이어블과 동일한 Character Verification 파이프라인에서
/// Data / IsolatedRuntime / LiveScene 검증 대상이 된다.
/// 공통 Skill/Effect/Mechanic 검증은 기존 Generic/Feature Provider가 담당하고,
/// 이 Provider는 Normal/Elite 적의 구조적 계약을 고정한다.
/// </summary>
public sealed class EnemyCharacterVerificationCaseProvider :
    ICharacterVerificationCaseProvider
{
    public const string NormalSingleHpContract =
        "enemy.normal.single_hp.contract";

    public const string NormalRuntimeSkillContract =
        "enemy.normal.runtime_skill.contract";

    public const string EliteBodyPartContract =
        "enemy.elite.bodypart.contract";

    public const string ElitePostureSlotContract =
        "enemy.elite.posture_slot.contract";

    public bool Supports(
        CharacterAuthoringBundle bundle,
        Character character)
    {
        return bundle?.Kind == CharacterAuthoringKind.NormalEnemy ||
               bundle?.Kind == CharacterAuthoringKind.EliteEnemy ||
               character is NormalEnemy ||
               character is EliteEnemy;
    }

    public IEnumerable<CharacterVerificationCaseDefinition>
        CreateDefaultCases(
            CharacterAuthoringBundle bundle)
    {
        if (bundle?.Kind == CharacterAuthoringKind.NormalEnemy)
        {
            yield return CharacterVerificationCaseDefinition.Create(
                NormalSingleHpContract,
                "일반 적 Single HP 전투 모델",
                CharacterVerificationCategory.Boundary,
                CharacterVerificationExecutionMode.IsolatedRuntime,
                "NormalEnemy는 BodyPart 없이 Single HP를 사용하고, 전투 행동 슬롯 1개와 Last Stand 비지원 계약을 검사합니다.");

            yield return CharacterVerificationCaseDefinition.Create(
                NormalRuntimeSkillContract,
                "일반 적 3회 독립 굴림 RuntimeSkill",
                CharacterVerificationCategory.Boundary,
                CharacterVerificationExecutionMode.IsolatedRuntime,
                "NormalEnemy RuntimeSkill이 3회 교환 굴림과 RollEachExchange 정책을 사용하며 실제 캐릭터 스킬 목록에 등록되는지 검사합니다.");
        }

        if (bundle?.Kind == CharacterAuthoringKind.EliteEnemy)
        {
            yield return CharacterVerificationCaseDefinition.Create(
                EliteBodyPartContract,
                "정예 적 4부위 전투 모델",
                CharacterVerificationCategory.Boundary,
                CharacterVerificationExecutionMode.IsolatedRuntime,
                "EliteEnemy가 HEAD/LEFT_HAND/RIGHT_HAND/LEGS 4부위를 정확히 구성하고 Last Stand를 지원하는지 검사합니다.");

            yield return CharacterVerificationCaseDefinition.Create(
                ElitePostureSlotContract,
                "정예 적 자세·행동 슬롯 계약",
                CharacterVerificationCategory.Boundary,
                CharacterVerificationExecutionMode.IsolatedRuntime,
                "자세 로테이션 사용 시 EnemyPostureMechanic이 등록되고 CurrentAttackSlotLimit과 EliteEnemy.GetMaxCombatActionSlots가 항상 일치하는지 검사합니다.");
        }
    }

    public bool TryExecute(
        CharacterVerificationContext context,
        out CharacterVerificationCaseResult result)
    {
        result = context?.Definition?.CaseId switch
        {
            NormalSingleHpContract =>
                VerifyNormalSingleHp(context),

            NormalRuntimeSkillContract =>
                VerifyNormalRuntimeSkills(context),

            EliteBodyPartContract =>
                VerifyEliteBodyParts(context),

            ElitePostureSlotContract =>
                VerifyElitePostureSlots(context),

            _ => null
        };

        return result != null;
    }

    private static CharacterVerificationCaseResult
        VerifyNormalSingleHp(
            CharacterVerificationContext context)
    {
        NormalEnemy enemy =
            context?.Character as NormalEnemy;

        if (enemy == null)
        {
            return context.Fail(
                "NormalEnemy Runtime Fixture",
                "Character가 NormalEnemy가 아님");
        }

        int partCount =
            enemy.BodyParts?.Count ?? 0;

        bool valid =
            enemy.IsSingleHpTarget &&
            !enemy.UsesBodyParts &&
            partCount == 0 &&
            enemy.GetMaxCombatActionSlots() == 1 &&
            !enemy.SupportsLastStand &&
            enemy.MaxCombatHP > 0;

        string actual =
            $"SingleHP={enemy.IsSingleHpTarget}, " +
            $"UsesBodyParts={enemy.UsesBodyParts}, " +
            $"Parts={partCount}, Slots={enemy.GetMaxCombatActionSlots()}, " +
            $"LastStand={enemy.SupportsLastStand}, MaxHP={enemy.MaxCombatHP}";

        return valid
            ? context.Pass(
                "Single HP / BodyPart 0 / CombatSlot 1 / LastStand false",
                actual)
            : context.Fail(
                "Single HP / BodyPart 0 / CombatSlot 1 / LastStand false",
                actual);
    }

    private static CharacterVerificationCaseResult
        VerifyNormalRuntimeSkills(
            CharacterVerificationContext context)
    {
        NormalEnemy enemy =
            context?.Character as NormalEnemy;

        if (enemy == null)
        {
            return context.Fail(
                "NormalEnemy Runtime Fixture",
                "Character가 NormalEnemy가 아님");
        }

        IReadOnlyList<Skill> skills =
            enemy.CharacterSkills;

        List<string> failures =
            new List<string>();

        if (skills == null ||
            skills.Count == 0)
        {
            failures.Add(
                "CharacterSkills가 비어 있습니다.");
        }
        else
        {
            foreach (Skill skill in skills)
            {
                if (skill == null)
                {
                    failures.Add("NULL RuntimeSkill");
                    continue;
                }

                if (skill.ExchangeRollCount != 3)
                {
                    failures.Add(
                        $"{skill.SkillName}: ExchangeRollCount={skill.ExchangeRollCount}, Expected=3");
                }

                if (skill.RollReusePolicy !=
                    SkillRollReusePolicy.RollEachExchange)
                {
                    failures.Add(
                        $"{skill.SkillName}: RollReusePolicy={skill.RollReusePolicy}, Expected=RollEachExchange");
                }

                string id =
                    skill.Definition?.SkillId;

                bool registered =
                    enemy.RuntimeSkills?
                        .Any(
                            candidate =>
                                ReferenceEquals(candidate, skill) ||
                                (!string.IsNullOrWhiteSpace(id) &&
                                 string.Equals(
                                     candidate?.Definition?.SkillId,
                                     id,
                                     StringComparison.Ordinal))) == true;

                if (!registered)
                {
                    failures.Add(
                        $"{skill.SkillName}: CharacterSkills -> RuntimeSkills 등록 누락");
                }
            }
        }

        return failures.Count == 0
            ? context.Pass(
                "모든 일반 적 RuntimeSkill = 3회 독립 굴림 / RuntimeSkills 등록",
                $"Skills={skills?.Count ?? 0}")
            : context.Fail(
                "모든 일반 적 RuntimeSkill = 3회 독립 굴림 / RuntimeSkills 등록",
                $"FAIL {failures.Count}",
                string.Join("\n", failures));
    }

    private static CharacterVerificationCaseResult
        VerifyEliteBodyParts(
            CharacterVerificationContext context)
    {
        EliteEnemy enemy =
            context?.Character as EliteEnemy;

        if (enemy == null)
        {
            return context.Fail(
                "EliteEnemy Runtime Fixture",
                "Character가 EliteEnemy가 아님");
        }

        PartType[] expected =
        {
            PartType.HEAD,
            PartType.LEFT_HAND,
            PartType.RIGHT_HAND,
            PartType.LEGS
        };

        IReadOnlyList<BodyPart> parts =
            enemy.BodyParts;

        List<string> failures =
            new List<string>();

        if (parts == null ||
            parts.Count != expected.Length)
        {
            failures.Add(
                $"BodyPart Count={parts?.Count ?? 0}, Expected=4");
        }

        foreach (PartType type in expected)
        {
            BodyPart part =
                parts?.FirstOrDefault(
                    candidate =>
                        candidate != null &&
                        candidate.Type == type);

            if (part == null)
            {
                failures.Add(
                    $"필수 부위 누락: {type}");
                continue;
            }

            if (part.Owner != enemy)
            {
                failures.Add(
                    $"{type}: Owner 불일치");
            }

            if (part.MaxPartHP <= 0)
            {
                failures.Add(
                    $"{type}: MaxHP={part.MaxPartHP}");
            }
        }

        if (!enemy.SupportsLastStand)
            failures.Add("EliteEnemy SupportsLastStand=false");

        return failures.Count == 0
            ? context.Pass(
                "HEAD/LEFT_HAND/RIGHT_HAND/LEGS 4부위 + LastStand",
                "PASS")
            : context.Fail(
                "HEAD/LEFT_HAND/RIGHT_HAND/LEGS 4부위 + LastStand",
                $"FAIL {failures.Count}",
                string.Join("\n", failures));
    }

    private static CharacterVerificationCaseResult
        VerifyElitePostureSlots(
            CharacterVerificationContext context)
    {
        EliteEnemy enemy =
            context?.Character as EliteEnemy;

        if (enemy == null)
        {
            return context.Fail(
                "EliteEnemy Runtime Fixture",
                "Character가 EliteEnemy가 아님");
        }

        EnemyPostureMechanic posture =
            enemy.GetMechanic<EnemyPostureMechanic>();

        bool expectsPosture =
            context.Bundle?.UseElitePostureRotation == true;

        if (!expectsPosture)
        {
            return posture == null
                ? context.Pass(
                    "PostureRotation OFF이면 EnemyPostureMechanic 없음",
                    "Mechanic=NULL")
                : context.Fail(
                    "PostureRotation OFF이면 EnemyPostureMechanic 없음",
                    $"Mechanic={posture.GetType().Name}");
        }

        if (posture == null)
        {
            return context.Fail(
                "PostureRotation ON이면 EnemyPostureMechanic 등록",
                "Mechanic=NULL");
        }

        int mechanicLimit =
            posture.CurrentAttackSlotLimit;

        int characterLimit =
            enemy.GetMaxCombatActionSlots();

        bool valid =
            mechanicLimit >= 0 &&
            mechanicLimit == characterLimit;

        string actual =
            $"Posture={posture.Current}, " +
            $"MechanicLimit={mechanicLimit}, " +
            $"CharacterLimit={characterLimit}, " +
            $"ExpectedMomentumDrift={posture.ExpectedPlayerMomentumDrift}";

        return valid
            ? context.Pass(
                "CurrentAttackSlotLimit == EliteEnemy.GetMaxCombatActionSlots",
                actual)
            : context.Fail(
                "CurrentAttackSlotLimit == EliteEnemy.GetMaxCombatActionSlots",
                actual);
    }
}
