using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
                "정예 적 5부위 전투 모델",
                CharacterVerificationCategory.Boundary,
                CharacterVerificationExecutionMode.IsolatedRuntime,
                "EliteEnemy가 데이터 정의 5부위를 구성하고 각 부위의 안정적인 PartId/슬롯 역할을 가지며 Last Stand를 지원하는지 검사합니다.");

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
            enemy.RuntimeSkills;

        List<string> failures =
            new List<string>();

        if (skills == null ||
            skills.Count == 0)
        {
            failures.Add(
                "RuntimeSkills가 비어 있습니다.");
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

            }
        }

        return failures.Count == 0
            ? context.Pass(
                "모든 일반 적 RuntimeSkill = 3회 독립 굴림",
                $"Skills={skills?.Count ?? 0}")
            : context.Fail(
                "모든 일반 적 RuntimeSkill = 3회 독립 굴림",
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

        IReadOnlyList<BodyPart> parts = enemy.BodyParts;
        IReadOnlyList<EnemyBodyPartDefinition> definitions =
            context.Bundle?.EliteBodyPartDefinitions;

        List<string> failures = new List<string>();

        if (parts == null || parts.Count != 5)
            failures.Add($"BodyPart Count={parts?.Count ?? 0}, Expected=5");

        if (definitions == null || definitions.Count != 5)
            failures.Add($"Definition Count={definitions?.Count ?? 0}, Expected=5");

        HashSet<string> ids = new HashSet<string>(System.StringComparer.Ordinal);
        if (parts != null)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                BodyPart part = parts[i];
                if (part == null)
                {
                    failures.Add($"Part[{i}]=NULL");
                    continue;
                }

                if (part.Owner != enemy)
                    failures.Add($"{part.PartId}: Owner 불일치");

                if (part.MaxPartHP <= 0)
                    failures.Add($"{part.PartId}: MaxHP={part.MaxPartHP}");

                if (string.IsNullOrWhiteSpace(part.PartId) || !ids.Add(part.PartId))
                    failures.Add($"PartId 중복/비어있음: {part.PartId}");

                if (!part.UsesDataDefinedRules)
                    failures.Add($"{part.PartId}: UsesDataDefinedRules=false");

                if (definitions == null || i >= definitions.Count || definitions[i] == null)
                    continue;

                EnemyBodyPartDefinition definition = definitions[i];
                if (!string.Equals(part.PartId, definition.PartId?.Trim(), System.StringComparison.Ordinal))
                    failures.Add($"Part[{i}] PartId runtime={part.PartId}, data={definition.PartId}");
                if (!string.Equals(part.DisplayName, definition.DisplayName?.Trim(), System.StringComparison.Ordinal))
                    failures.Add($"{part.PartId}: DisplayName runtime={part.DisplayName}, data={definition.DisplayName}");
                if (part.SlotRole != definition.SlotRole)
                    failures.Add($"{part.PartId}: SlotRole runtime={part.SlotRole}, data={definition.SlotRole}");
                if (part.WeakenedRollCountPenalty != Mathf.Max(0, definition.RollCountPenalty))
                    failures.Add($"{part.PartId}: WeakenedRollPenalty 불일치");
                if (part.WeakenedSpeedMaxPenalty != Mathf.Max(0, definition.SpeedMaxPenalty))
                    failures.Add($"{part.PartId}: WeakenedSpeedPenalty 불일치");
                if (part.WeakenedNormalOnly != definition.NormalAttackOnly)
                    failures.Add($"{part.PartId}: WeakenedNormalOnly 불일치");
                if (part.BrokenRollCountPenalty != Mathf.Max(0, definition.BrokenRollCountPenalty))
                    failures.Add($"{part.PartId}: BrokenRollPenalty 불일치");
                if (part.BrokenSpeedMaxPenalty != Mathf.Max(0, definition.BrokenSpeedMaxPenalty))
                    failures.Add($"{part.PartId}: BrokenSpeedPenalty 불일치");
                if (part.BrokenEnergyMaxPenalty != Mathf.Max(0, definition.BrokenEnergyMaxPenalty))
                    failures.Add($"{part.PartId}: BrokenEnergyPenalty 불일치");
                if (part.BrokenNormalOnly != definition.BrokenNormalAttackOnly)
                    failures.Add($"{part.PartId}: BrokenNormalOnly 불일치");

                string[] runtimeForbiddenSkills =
                    part.BrokenForbiddenSkillIds?
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value.Trim())
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray() ?? Array.Empty<string>();

                string[] dataForbiddenSkills =
                    definition.BrokenForbiddenSkillIds?
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value.Trim())
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray() ?? Array.Empty<string>();

                if (!runtimeForbiddenSkills.SequenceEqual(dataForbiddenSkills))
                    failures.Add($"{part.PartId}: BrokenForbiddenSkillIds 불일치");
            }
        }

        if (!enemy.SupportsLastStand)
            failures.Add("EliteEnemy SupportsLastStand=false");

        return failures.Count == 0
            ? context.Pass(
                "Data-defined 5부위 + 약화/파괴 데이터 복사 + LastStand",
                "PASS")
            : context.Fail(
                "Data-defined 5부위 + 약화/파괴 데이터 복사 + LastStand",
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
