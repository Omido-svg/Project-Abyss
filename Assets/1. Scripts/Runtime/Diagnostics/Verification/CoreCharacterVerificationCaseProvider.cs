using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Olaf / Yujin / Hifumi에 공통으로 적용되는 "완전 커버리지" 계약.
/// 캐릭터 고유 Provider의 세부 메커닉 검증과 별개로,
/// 스킬 개수·검증 Case 폐쇄성·페이즈·부위 접근·공유 속도·도사림 계획/취소·고유 게이지를 고정한다.
/// </summary>
public sealed class CoreCharacterVerificationCaseProvider :
    ICharacterVerificationCaseProvider
{
    public const string VerificationManifest =
        "core3.data.verification_manifest";

    public const string SkillManifest =
        "core3.data.skill_manifest";

    public const string PhaseContract =
        "core3.data.phase_contract";

    public const string BodyPartAccessContract =
        "core3.runtime.bodypart_skill_access";

    public const string SharedBodyPartSpeed =
        "core3.runtime.shared_bodypart_speed";

    public const string PreparationPlanCancel =
        "core3.runtime.preparation_plan_cancel";

    public const string PreparationReplace =
        "core3.runtime.preparation_replace";

    public const string UniqueGaugeContract =
        "core3.runtime.unique_gauge";

    private static readonly string[] CoreCaseIds =
    {
        VerificationManifest,
        SkillManifest,
        PhaseContract,
        BodyPartAccessContract,
        SharedBodyPartSpeed,
        PreparationPlanCancel,
        PreparationReplace,
        UniqueGaugeContract
    };

    public bool Supports(
        CharacterAuthoringBundle bundle,
        Character character)
    {
        CharacterAuthoringKind? kind =
            bundle?.Kind;

        return kind == CharacterAuthoringKind.Olaf ||
               kind == CharacterAuthoringKind.Yujin ||
               kind == CharacterAuthoringKind.Hifumi ||
               character is Olaf ||
               character is Yujin ||
               character is Hifumi;
    }

    public IEnumerable<CharacterVerificationCaseDefinition>
        CreateDefaultCases(
            CharacterAuthoringBundle bundle)
    {
        yield return CharacterVerificationCaseDefinition.Create(
            VerificationManifest,
            "Core 3 전체 검증 Case 폐쇄성",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "Olaf/Yujin/Hifumi의 공통·고유·스킬별 검증 Case가 모두 생성됐는지 확인합니다. 누락된 Case가 있으면 FAIL합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            SkillManifest,
            "Core 3 스킬 수·ID 완전성",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "현재 디자인 기준 스킬 수(Olaf 11 / Yujin 12 / Hifumi 12), ID 중복, RuntimeSkill 생성 가능 여부를 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            PhaseContract,
            "스킬 타입 ↔ 실행 페이즈 계약",
            CharacterVerificationCategory.Boundary,
            CharacterVerificationExecutionMode.DataOnly,
            "Prestige=PRETURN, Preparation=FORESIGHT, Normal/Duel=COMBAT 계약이 모든 스킬에 적용되는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            BodyPartAccessContract,
            "부위별 스킬 접근 계약",
            CharacterVerificationCategory.Boundary,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "HEAD=전체, 양팔=일반/결투, LEGS=도사림 규칙과 실제 GetSelectableSkills 결과가 일치하는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            SharedBodyPartSpeed,
            "동일 부위 다중 슬롯 공유 속도",
            CharacterVerificationCategory.Boundary,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "같은 BodyPart의 ActionIndex 0/1이 하나의 SpeedManager 값을 공유하고 ConfigureActionSlot이 개별 속도를 재굴림하지 않는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            PreparationPlanCancel,
            "도사림 계획·에너지 예약·취소",
            CharacterVerificationCategory.Preparation,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "도사림이 START 전 FORESIGHT ActionSlot으로만 계획되고 즉시 에너지를 소비하지 않으며, 제거 시 예약 비용까지 사라지는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            PreparationReplace,
            "도사림 동일 슬롯 교체 계약",
            CharacterVerificationCategory.Preparation,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "START 전 같은 행동 슬롯에서 도사림 A→B로 바꾸면 슬롯 1개만 남고 ActionId를 보존하며 예약 비용이 최종 스킬 기준으로 교체되는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            UniqueGaugeContract,
            "캐릭터 고유 게이지 계약",
            CharacterVerificationCategory.UI,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "Olaf=광기, Yujin=환형, Hifumi=뼈 고유 게이지 Provider의 라벨·정규화·표시값을 검사합니다.");
    }

    public bool TryExecute(
        CharacterVerificationContext context,
        out CharacterVerificationCaseResult result)
    {
        result = context?.Definition?.CaseId switch
        {
            VerificationManifest =>
                VerifyVerificationManifest(context),

            SkillManifest =>
                VerifySkillManifest(context),

            PhaseContract =>
                VerifyPhaseContract(context),

            BodyPartAccessContract =>
                VerifyBodyPartAccess(context),

            SharedBodyPartSpeed =>
                VerifySharedBodyPartSpeed(context),

            PreparationPlanCancel =>
                VerifyPreparationPlanCancel(context),

            PreparationReplace =>
                VerifyPreparationReplace(context),

            UniqueGaugeContract =>
                VerifyUniqueGauge(context),

            _ => null
        };

        return result != null;
    }

    private static CharacterVerificationCaseResult
        VerifyVerificationManifest(
            CharacterVerificationContext context)
    {
        IReadOnlyList<CharacterVerificationCaseDefinition> effective =
            CharacterVerificationRunner.BuildEffectiveDefinitions(
                context?.Profile);

        HashSet<string> actual =
            new HashSet<string>(
                effective?
                    .Where(item => item != null)
                    .Select(item => item.CaseId) ??
                Array.Empty<string>(),
                StringComparer.Ordinal);

        HashSet<string> expected =
            new HashSet<string>(
                StringComparer.Ordinal);

        AddCommonCaseIds(expected);

        foreach (string id in CoreCaseIds)
            expected.Add(id);

        List<SkillDefinition> definitions =
            CharacterVerificationScenarioTools.GetDefinitions(
                context?.Bundle);

        foreach (SkillDefinition definition in definitions)
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.SkillId))
            {
                continue;
            }

            expected.Add(
                CharacterFeatureCoverageCaseProvider.SkillPrefix +
                definition.SkillId);
        }

        AddCharacterSpecificCaseIds(
            context?.Bundle?.Kind ?? CharacterAuthoringKind.Custom,
            expected);

        List<string> missing =
            expected
                .Where(id => !actual.Contains(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

        bool valid =
            missing.Count == 0;

        string detail =
            $"Expected={expected.Count}, Actual={actual.Count}, " +
            $"Skills={definitions.Count}";

        if (missing.Count > 0)
        {
            detail +=
                "\n누락:\n" +
                string.Join("\n", missing);
        }

        return valid
            ? context.Pass(
                "Core 3 공통/고유/스킬별 Case가 모두 존재",
                detail)
            : context.Fail(
                "Core 3 공통/고유/스킬별 Case가 모두 존재",
                detail);
    }

    private static CharacterVerificationCaseResult
        VerifySkillManifest(
            CharacterVerificationContext context)
    {
        List<SkillDefinition> definitions =
            CharacterVerificationScenarioTools.GetDefinitions(
                context?.Bundle);

        int expectedCount =
            GetExpectedSkillCount(
                context?.Bundle?.Kind ?? CharacterAuthoringKind.Custom);

        HashSet<string> ids =
            new HashSet<string>(
                StringComparer.Ordinal);

        List<string> failures =
            new List<string>();

        foreach (SkillDefinition definition in definitions)
        {
            if (definition == null)
            {
                failures.Add("NULL SkillDefinition");
                continue;
            }

            if (string.IsNullOrWhiteSpace(definition.SkillId))
                failures.Add($"{definition.name}: SkillId 없음");
            else if (!ids.Add(definition.SkillId))
                failures.Add($"중복 SkillId: {definition.SkillId}");

            if (definition.CreateRuntimeSkill() == null)
                failures.Add($"RuntimeSkill 생성 실패: {definition.SkillName}");
        }

        if (expectedCount > 0 &&
            definitions.Count != expectedCount)
        {
            failures.Add(
                $"스킬 수 불일치: {definitions.Count}/{expectedCount}");
        }

        string actual =
            $"Definitions={definitions.Count}, UniqueIds={ids.Count}, " +
            $"Expected={expectedCount}";

        return failures.Count == 0
            ? context.Pass(
                $"스킬 {expectedCount}개·고유 ID·RuntimeSkill 생성",
                actual)
            : context.Fail(
                $"스킬 {expectedCount}개·고유 ID·RuntimeSkill 생성",
                actual,
                string.Join("\n", failures));
    }

    private static CharacterVerificationCaseResult
        VerifyPhaseContract(
            CharacterVerificationContext context)
    {
        List<string> failures =
            new List<string>();

        List<SkillDefinition> definitions =
            CharacterVerificationScenarioTools.GetDefinitions(
                context?.Bundle);

        foreach (SkillDefinition definition in definitions)
        {
            Skill skill =
                definition?.CreateRuntimeSkill();

            if (skill == null)
            {
                failures.Add(
                    $"RuntimeSkill 없음: {definition?.SkillName ?? "NULL"}");
                continue;
            }

            ActionPhase expected =
                ExpectedPhase(skill.ActionType);

            if (skill.DefaultPhase != expected)
            {
                failures.Add(
                    $"{skill.SkillName}: {skill.ActionType} -> " +
                    $"{skill.DefaultPhase}, expected {expected}");
            }
        }

        return failures.Count == 0
            ? context.Pass(
                "Prestige=PRETURN / Preparation=FORESIGHT / Normal·Duel=COMBAT",
                $"{definitions.Count}개 스킬 일치")
            : context.Fail(
                "Prestige=PRETURN / Preparation=FORESIGHT / Normal·Duel=COMBAT",
                $"FAIL {failures.Count}",
                string.Join("\n", failures));
    }

    private static CharacterVerificationCaseResult
        VerifyBodyPartAccess(
            CharacterVerificationContext context)
    {
        Character character =
            context?.Character;

        if (character?.BodyParts == null)
        {
            return context.Fail(
                "부위형 캐릭터 BodyParts 존재",
                "BodyParts=NULL");
        }

        List<string> failures =
            new List<string>();

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null || part.IsBroken)
                continue;

            IReadOnlyList<Skill> selectable =
                character.GetSelectableSkills(
                    part,
                    0);

            if (selectable == null)
                continue;

            foreach (Skill skill in selectable)
            {
                if (skill == null)
                    continue;

                if (!BodyPartSkillAccessPolicy.Allows(
                        part,
                        skill.ActionType))
                {
                    failures.Add(
                        $"{part.Type}에 금지 스킬 노출: " +
                        $"{skill.SkillName}/{skill.ActionType}");
                }
            }
        }

        VerifyAccessMatrix(failures);

        return failures.Count == 0
            ? context.Pass(
                "HEAD 전체 / 양팔 일반·결투 / LEGS 도사림",
                "Policy + GetSelectableSkills 일치")
            : context.Fail(
                "HEAD 전체 / 양팔 일반·결투 / LEGS 도사림",
                $"FAIL {failures.Count}",
                string.Join("\n", failures));
    }

    private static CharacterVerificationCaseResult
        VerifySharedBodyPartSpeed(
            CharacterVerificationContext context)
    {
        Character character =
            context?.Character;

        BodyPart part =
            character?.BodyParts?
                .FirstOrDefault(
                    item => item != null &&
                            !item.IsBroken);

        if (character == null ||
            part == null ||
            context.BattleContext == null)
        {
            return context.Fail(
                "공유 속도 Fixture 존재",
                "Character/Part/BattleContext 없음");
        }

        SpeedManager speedManager =
            new SpeedManager(
                context.BattleContext);

        speedManager.SetSpeedForDebug(
            character,
            part,
            10);

        ActionSlot first =
            new ActionSlot
            {
                Owner = character,
                Part = part,
                ActionIndex = 0,
                Speed = 10
            };

        ActionSlot second =
            new ActionSlot
            {
                Owner = character,
                Part = part,
                ActionIndex = 1,
                Speed = 10
            };

        character.ConfigureActionSlot(first);
        character.ConfigureActionSlot(second);

        bool configurePreserved =
            first.Speed == 10 &&
            second.Speed == 10;

        speedManager.ApplySpeedToSlots(
            new[]
            {
                first,
                second
            });

        bool firstPass =
            first.Speed == 10 &&
            second.Speed == 10;

        speedManager.SetSpeedForDebug(
            character,
            part,
            13);

        speedManager.ApplySpeedToSlots(
            new[]
            {
                first,
                second
            });

        bool secondPass =
            first.Speed == 13 &&
            second.Speed == 13;

        bool valid =
            configurePreserved &&
            firstPass &&
            secondPass;

        string detail =
            $"Part={part.Type}, ConfigurePreserved={configurePreserved}, " +
            $"10=>{firstPass}, 13=>{secondPass}, Final={first.Speed}/{second.Speed}";

        return valid
            ? context.Pass(
                "동일 BodyPart의 모든 ActionIndex는 같은 Speed",
                detail)
            : context.Fail(
                "동일 BodyPart의 모든 ActionIndex는 같은 Speed",
                detail);
    }

    private static CharacterVerificationCaseResult
        VerifyPreparationPlanCancel(
            CharacterVerificationContext context)
    {
        Character character =
            context?.Character;

        if (character == null)
        {
            return context.Fail(
                "도사림 계획 Fixture 존재",
                "Character=NULL");
        }

        Skill preparation =
            character.RuntimeSkills?
                .FirstOrDefault(
                    skill =>
                        skill != null &&
                        skill.ActionType == ActionType.Preparation);

        if (preparation == null)
        {
            return context.Fail(
                "Core 3 캐릭터에 도사림 RuntimeSkill 존재",
                "Preparation RuntimeSkill 없음");
        }

        BodyPart part =
            FindPreparationPart(
                character,
                preparation);

        if (part == null)
        {
            return context.Fail(
                "도사림을 배치할 수 있는 부위 존재",
                "Allowed BodyPart 없음");
        }

        if (character.CurrentEnergy < preparation.EnergyCost)
        {
            character.AddEnergy(
                character.MaxEnergy);
        }

        int energyBefore =
            character.CurrentEnergy;

        ActionManager actionManager =
            new ActionManager();

        try
        {
            ActionSlot slot =
                new ActionSlot
                {
                    Owner = character,
                    Part = part,
                    Skill = preparation,
                    Speed = 7,
                    ActionIndex = 0,
                    Phase = preparation.DefaultPhase,
                    TargetCharacter = character,
                    TargetPart = part,
                    TargetSlot = null
                };

            bool added =
                actionManager.TryAddOrReplaceSlot(
                    slot);

            int energyAfterPlan =
                character.CurrentEnergy;

            int plannedCost =
                actionManager.GetPlannedEnergyCost(
                    character);

            bool removed =
                actionManager.RemoveSlot(
                    character,
                    part,
                    0);

            int energyAfterCancel =
                character.CurrentEnergy;

            int costAfterCancel =
                actionManager.GetPlannedEnergyCost(
                    character);

            bool readded =
                actionManager.TryAddOrReplaceSlot(
                    slot);

            ActionExecutionQueue queue =
                new ClashBuilder()
                    .BuildQueue(
                        actionManager.Slots);

            bool queueValid =
                queue.PreparationQueue.Count == 1 &&
                queue.PrestigeQueue.Count == 0 &&
                queue.ClashQueue.Count == 0;

            bool valid =
                added &&
                removed &&
                readded &&
                preparation.DefaultPhase == ActionPhase.FORESIGHT &&
                energyAfterPlan == energyBefore &&
                energyAfterCancel == energyBefore &&
                plannedCost == preparation.EnergyCost &&
                costAfterCancel == 0 &&
                queueValid;

            string detail =
                $"Skill={preparation.SkillName}, Part={part.Type}, " +
                $"Add/Remove/ReAdd={added}/{removed}/{readded}, " +
                $"Energy={energyBefore}->{energyAfterPlan}->{energyAfterCancel}, " +
                $"Reserved={plannedCost}->0, QueuePreparation={queue.PreparationQueue.Count}";

            return valid
                ? context.Pass(
                    "START 전 계획만 등록·취소 가능·FORESIGHT 큐 실행",
                    detail)
                : context.Fail(
                    "START 전 계획만 등록·취소 가능·FORESIGHT 큐 실행",
                    detail);
        }
        finally
        {
            actionManager.Dispose();
        }
    }

    private static CharacterVerificationCaseResult
        VerifyPreparationReplace(
            CharacterVerificationContext context)
    {
        Character character =
            context?.Character;

        if (character == null)
        {
            return context.Fail(
                "도사림 교체 Fixture 존재",
                "Character=NULL");
        }

        List<Skill> preparations =
            character.RuntimeSkills?
                .Where(
                    skill =>
                        skill != null &&
                        skill.ActionType == ActionType.Preparation)
                .ToList() ??
            new List<Skill>();

        if (preparations.Count < 2)
        {
            return context.Fail(
                "Core 3 캐릭터에 교체 가능한 도사림 2개 이상",
                $"Preparation={preparations.Count}");
        }

        BodyPart part =
            character.GetBodyPart(
                PartType.LEGS);

        Skill firstSkill =
            preparations.FirstOrDefault(
                skill => CanSelectOnPart(
                    character,
                    part,
                    skill));

        Skill secondSkill =
            preparations.FirstOrDefault(
                skill =>
                    skill != firstSkill &&
                    CanSelectOnPart(
                        character,
                        part,
                        skill));

        if (part == null ||
            firstSkill == null ||
            secondSkill == null)
        {
            return context.Fail(
                "LEGS에서 교체 가능한 도사림 2개",
                $"Part={part?.Type.ToString() ?? "NULL"}, " +
                $"First={firstSkill?.SkillName ?? "NULL"}, Second={secondSkill?.SkillName ?? "NULL"}");
        }

        int needed =
            Math.Max(
                firstSkill.EnergyCost,
                secondSkill.EnergyCost);

        if (character.CurrentEnergy < needed)
            character.AddEnergy(character.MaxEnergy);

        int energyBefore =
            character.CurrentEnergy;

        ActionManager actionManager =
            new ActionManager();

        try
        {
            ActionSlot first =
                new ActionSlot
                {
                    Owner = character,
                    Part = part,
                    Skill = firstSkill,
                    Speed = 7,
                    ActionIndex = 0,
                    Phase = ActionPhase.FORESIGHT,
                    TargetCharacter = character,
                    TargetPart = part
                };

            bool firstAdded =
                actionManager.TryAddOrReplaceSlot(
                    first);

            long firstActionId =
                first.ActionId;

            ActionSlot second =
                new ActionSlot
                {
                    Owner = character,
                    Part = part,
                    Skill = secondSkill,
                    Speed = 7,
                    ActionIndex = 0,
                    Phase = ActionPhase.FORESIGHT,
                    TargetCharacter = character,
                    TargetPart = part
                };

            bool secondAdded =
                actionManager.TryAddOrReplaceSlot(
                    second);

            ActionSlot live =
                actionManager.FindSlot(
                    character,
                    part,
                    0);

            int planned =
                actionManager.GetPlannedEnergyCost(
                    character);

            bool valid =
                firstAdded &&
                secondAdded &&
                actionManager.Slots.Count == 1 &&
                live == second &&
                second.ActionId == firstActionId &&
                live.Skill == secondSkill &&
                planned == secondSkill.EnergyCost &&
                character.CurrentEnergy == energyBefore;

            string actual =
                $"{firstSkill.SkillName}->{secondSkill.SkillName}, " +
                $"Slots={actionManager.Slots.Count}, ActionId={firstActionId}->{second.ActionId}, " +
                $"Reserved={planned}/{secondSkill.EnergyCost}, Energy={energyBefore}->{character.CurrentEnergy}";

            return valid
                ? context.Pass(
                    "도사림 교체는 동일 ActionSlot 수정 / 최종 예약만 유지",
                    actual)
                : context.Fail(
                    "도사림 교체는 동일 ActionSlot 수정 / 최종 예약만 유지",
                    actual);
        }
        finally
        {
            actionManager.Dispose();
        }
    }

    private static CharacterVerificationCaseResult
        VerifyUniqueGauge(
            CharacterVerificationContext context)
    {
        Character character =
            context?.Character;

        ICharacterUniqueGaugeProvider gauge =
            character?.Mechanics?
                .OfType<ICharacterUniqueGaugeProvider>()
                .FirstOrDefault();

        string expectedLabel =
            context?.Bundle?.Kind switch
            {
                CharacterAuthoringKind.Olaf => "광기",
                CharacterAuthoringKind.Yujin => "환형",
                CharacterAuthoringKind.Hifumi => "뼈",
                _ => string.Empty
            };

        bool valid =
            gauge != null &&
            string.Equals(
                gauge.GaugeLabel,
                expectedLabel,
                StringComparison.Ordinal) &&
            gauge.GaugeNormalized >= 0f &&
            gauge.GaugeNormalized <= 1f &&
            !string.IsNullOrWhiteSpace(
                gauge.GaugeValueText);

        string actual =
            gauge == null
                ? "GaugeProvider=NULL"
                : $"Label={gauge.GaugeLabel}, Normalized={gauge.GaugeNormalized:0.###}, " +
                  $"Value={gauge.GaugeValueText}";

        return valid
            ? context.Pass(
                $"고유 게이지 {expectedLabel} / 0..1 / 표시값",
                actual)
            : context.Fail(
                $"고유 게이지 {expectedLabel} / 0..1 / 표시값",
                actual);
    }

    private static ActionPhase ExpectedPhase(
        ActionType actionType)
    {
        return actionType switch
        {
            ActionType.Prestige => ActionPhase.PRETURN,
            ActionType.Preparation => ActionPhase.FORESIGHT,
            _ => ActionPhase.COMBAT
        };
    }

    private static int GetExpectedSkillCount(
        CharacterAuthoringKind kind)
    {
        return kind switch
        {
            CharacterAuthoringKind.Olaf => 11,
            CharacterAuthoringKind.Yujin => 12,
            CharacterAuthoringKind.Hifumi => 12,
            _ => 0
        };
    }

    private static BodyPart FindPreparationPart(
        Character character,
        Skill preparation)
    {
        if (character?.BodyParts == null ||
            preparation == null)
        {
            return null;
        }

        BodyPart legs =
            character.GetBodyPart(
                PartType.LEGS);

        if (CanSelectOnPart(
                character,
                legs,
                preparation))
        {
            return legs;
        }

        foreach (BodyPart part in character.BodyParts)
        {
            if (CanSelectOnPart(
                    character,
                    part,
                    preparation))
            {
                return part;
            }
        }

        return null;
    }

    private static bool CanSelectOnPart(
        Character character,
        BodyPart part,
        Skill skill)
    {
        if (character == null ||
            part == null ||
            part.IsBroken ||
            skill == null ||
            !BodyPartSkillAccessPolicy.Allows(
                part,
                skill.ActionType))
        {
            return false;
        }

        IReadOnlyList<Skill> selectable =
            character.GetSelectableSkills(
                part,
                0);

        if (selectable == null)
            return false;

        string skillId =
            skill.Definition?.SkillId;

        return selectable.Any(
            candidate =>
                candidate == skill ||
                (!string.IsNullOrWhiteSpace(skillId) &&
                 string.Equals(
                     candidate?.Definition?.SkillId,
                     skillId,
                     StringComparison.Ordinal)));
    }

    private static void VerifyAccessMatrix(
        ICollection<string> failures)
    {
        PartType[] parts =
        {
            PartType.HEAD,
            PartType.LEFT_HAND,
            PartType.RIGHT_HAND,
            PartType.LEGS
        };

        ActionType[] actions =
        {
            ActionType.NormalAttack,
            ActionType.Duel,
            ActionType.Preparation,
            ActionType.Prestige
        };

        foreach (PartType part in parts)
        {
            foreach (ActionType action in actions)
            {
                bool expected =
                    part switch
                    {
                        PartType.HEAD => true,
                        PartType.LEFT_HAND =>
                            action == ActionType.NormalAttack ||
                            action == ActionType.Duel,
                        PartType.RIGHT_HAND =>
                            action == ActionType.NormalAttack ||
                            action == ActionType.Duel,
                        PartType.LEGS =>
                            action == ActionType.Preparation,
                        _ => false
                    };

                bool actual =
                    BodyPartSkillAccessPolicy.Allows(
                        part,
                        action);

                if (actual != expected)
                {
                    failures.Add(
                        $"Policy {part}/{action}: {actual}, expected {expected}");
                }
            }
        }
    }

    private static void AddCommonCaseIds(
        ISet<string> target)
    {
        target.Add(CharacterFeatureCoverageCaseProvider.Manifest);
        target.Add(CharacterFeatureCoverageCaseProvider.Mechanics);

        target.Add(GenericCharacterVerificationCaseProvider.CoreReferences);
        target.Add(GenericCharacterVerificationCaseProvider.PrefabCompatibility);
        target.Add(GenericCharacterVerificationCaseProvider.LoadoutIntegrity);
        target.Add(GenericCharacterVerificationCaseProvider.SkillDefinitions);
        target.Add(GenericCharacterVerificationCaseProvider.Presentation);
        target.Add(GenericCharacterVerificationCaseProvider.RuntimeInitialization);
        target.Add(GenericCharacterVerificationCaseProvider.RuntimeBodyParts);
        target.Add(GenericCharacterVerificationCaseProvider.RuntimeSkills);
        target.Add(GenericCharacterVerificationCaseProvider.DeterministicRandom);
        target.Add(GenericCharacterVerificationCaseProvider.LiveSceneBinding);
    }

    private static void AddCharacterSpecificCaseIds(
        CharacterAuthoringKind kind,
        ISet<string> target)
    {
        switch (kind)
        {
            case CharacterAuthoringKind.Olaf:
                target.Add(OlafCharacterVerificationCaseProvider.SkillCoverage);
                target.Add(OlafCharacterVerificationCaseProvider.MechanicRegistration);
                target.Add(OlafCharacterVerificationCaseProvider.MadnessBoundary);
                target.Add(OlafCharacterVerificationCaseProvider.MadnessRollModifier);
                target.Add(OlafCharacterVerificationCaseProvider.MadnessConsume);
                target.Add(OlafCharacterVerificationCaseProvider.ImmortalFuryLifecycle);
                target.Add(OlafCharacterVerificationCaseProvider.DiceRuntimeResolution);
                target.Add(OlafCharacterVerificationCaseProvider.DiceVisualSides);
                target.Add(OlafCharacterVerificationCaseProvider.StandardExplosionLimit);
                target.Add(OlafCharacterVerificationCaseProvider.OneSidedUniqueEffects);
                break;

            case CharacterAuthoringKind.Yujin:
                target.Add(YujinCharacterVerificationCaseProvider.SkillCoverage);
                target.Add(YujinCharacterVerificationCaseProvider.MechanicRegistration);
                target.Add(YujinCharacterVerificationCaseProvider.UnlimitedWeaponSwitch);
                target.Add(YujinCharacterVerificationCaseProvider.WeaponProfiles);
                target.Add(YujinCharacterVerificationCaseProvider.SenseTurnStart);
                target.Add(YujinCharacterVerificationCaseProvider.SenseReroll);
                target.Add(YujinCharacterVerificationCaseProvider.MarkIgnition);
                target.Add(YujinCharacterVerificationCaseProvider.WeaponSwitchBeforeAction);
                target.Add(YujinCharacterVerificationCaseProvider.HwanhyeongLoadoutContract);
                target.Add(YujinCharacterVerificationCaseProvider.HwanhyeongFlowPassive);
                target.Add(YujinCharacterVerificationCaseProvider.NakilRemainingRollRemoval);
                break;

            case CharacterAuthoringKind.Hifumi:
                target.Add(HifumiCharacterVerificationCaseProvider.SkillCoverage);
                target.Add(HifumiCharacterVerificationCaseProvider.SkillContracts);
                target.Add(HifumiCharacterVerificationCaseProvider.MechanicRegistration);
                target.Add(HifumiCharacterVerificationCaseProvider.BoneDamageRules);
                target.Add(HifumiCharacterVerificationCaseProvider.ActionCosts);
                target.Add(HifumiCharacterVerificationCaseProvider.CounterContracts);
                target.Add(HifumiCharacterVerificationCaseProvider.GoldanPower);
                target.Add(HifumiCharacterVerificationCaseProvider.PrestigeContracts);
                target.Add(HifumiCharacterVerificationCaseProvider.GaugeContract);
                break;
        }
    }
}
