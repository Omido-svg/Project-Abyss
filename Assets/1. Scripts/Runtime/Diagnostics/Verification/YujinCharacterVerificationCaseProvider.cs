using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public sealed class YujinCharacterVerificationCaseProvider :
    ICharacterVerificationCaseProvider
{
    public const string SkillCoverage =
        "yujin.data.skill_coverage";

    public const string MechanicRegistration =
        "yujin.passive.mechanic_registration";

    public const string UnlimitedWeaponSwitch =
        "yujin.passive.unlimited_weapon_switch";

    public const string WeaponProfiles =
        "yujin.passive.weapon_profiles";

    public const string SenseTurnStart =
        "yujin.passive.sense_turn_start";

    public const string SenseReroll =
        "yujin.duel.sense_reroll";

    public const string MarkIgnition =
        "yujin.passive.mark_ignition_nakil";

    public const string WeaponSwitchBeforeAction =
        "yujin.passive.weapon_switch_before_action_only";

    public const string HwanhyeongLoadoutContract =
        "yujin.preparation.hwanhyeong_loadout_contract";

    public const string HwanhyeongFlowPassive =
        "yujin.passive.hwanhyeong_flow";

    public const string NakilRemainingRollRemoval =
        "yujin.duel.nakil_remove_remaining_rolls";

    private static readonly string[] RequiredSkillIds =
    {
        YujinSkillIds.Inspection,
        YujinSkillIds.Breakfast,
        YujinSkillIds.Inscription,
        YujinSkillIds.Pursuit,
        YujinSkillIds.Capture,
        YujinSkillIds.Sentencing,
        YujinSkillIds.Brand,
        YujinSkillIds.JointLiability,
        YujinSkillIds.Retrial,
        YujinSkillIds.HwanhyeongBaeku,
        YujinSkillIds.HwanhyeongJeokseol,
        YujinSkillIds.HwanhyeongNakil
    };

    public bool Supports(
        CharacterAuthoringBundle bundle,
        Character character)
    {
        return bundle?.Kind == CharacterAuthoringKind.Yujin ||
               character is Yujin;
    }

    public IEnumerable<CharacterVerificationCaseDefinition>
        CreateDefaultCases(
            CharacterAuthoringBundle bundle)
    {
        yield return CharacterVerificationCaseDefinition.Create(
            SkillCoverage,
            "유진 스킬 ID 전체 등록",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "기존 9개 스킬과 환형 준비 행동 3종을 포함한 고유 SkillId 12개가 데이터 그래프에 모두 존재하는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            HwanhyeongLoadoutContract,
            "환형 3종 기본 도사림 장착",
            CharacterVerificationCategory.Preparation,
            CharacterVerificationExecutionMode.DataOnly,
            "백우·적설·낙일 환형 3종이 기본 장착 도사림이며 포획·선고는 교체 후보로 보존되는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            MechanicRegistration,
            "유진 통합 메커닉 등록",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "무기·살수의 감·표식 메커닉 생성과 BattleEvent 구독을 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            HwanhyeongFlowPassive,
            "패시브 · 환형의 흐름",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "환형으로 예약한 무기 변경이 다음 턴 실제 완료될 때 살수의 감 +1을 추가로 얻는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            UnlimitedWeaponSwitch,
            "환형 빛 1·턴당 1회",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "환형이 빛 1을 소비하고 같은 턴 두 번째 전환을 거부하며 다음 턴 다시 사용 가능한지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            WeaponSwitchBeforeAction,
            "환형 계획 중 예약 허용",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "행동 슬롯이 이미 배치된 계획 단계에서도 환형을 다음 턴 무기 변경으로 예약할 수 있는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            WeaponProfiles,
            "무기 프로필 수치",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "백우·적설·낙일의 코인 수, 앞면 확률, 크리값, 표식량을 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            SenseTurnStart,
            "살수의 감 턴 시작 획득",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "TurnStart +1과 적 부위 파괴 +1, 자신의 부위 파괴 제외를 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            SenseReroll,
            "살수의 감 자동 재굴림",
            CharacterVerificationCategory.Duel,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "각인·추격의 행동 슬롯에서 사용을 선택한 경우에만 감을 소비해 재굴림하는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            NakilRemainingRollRemoval,
            "낙일 승리 시 상대 잔여 굴림 제거",
            CharacterVerificationCategory.Duel,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "낙일 상태의 유진이 교환에서 승리하면 첫 번째·두 번째 행동 위치와 무관하게 상대의 남은 굴림만 0이 되는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            MarkIgnition,
            "표식 44·낙일 발화",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "표식 44 도달 시 표식 초기화와 낙일의 대상 부위 약화를 검사합니다.");
    }

    public bool TryExecute(
        CharacterVerificationContext context,
        out CharacterVerificationCaseResult result)
    {
        result = context?.Definition?.CaseId switch
        {
            SkillCoverage =>
                VerifySkillCoverage(context),

            HwanhyeongLoadoutContract =>
                VerifyHwanhyeongLoadoutContract(context),

            MechanicRegistration =>
                VerifyMechanic(context),

            HwanhyeongFlowPassive =>
                VerifyHwanhyeongFlowPassive(context),

            UnlimitedWeaponSwitch =>
                VerifyUnlimitedWeaponSwitch(context),

            WeaponSwitchBeforeAction =>
                VerifyWeaponSwitchBeforeAction(context),

            WeaponProfiles =>
                VerifyWeaponProfiles(context),

            SenseTurnStart =>
                VerifySenseTurnStart(context),

            SenseReroll =>
                VerifySenseReroll(context),

            NakilRemainingRollRemoval =>
                VerifyNakilRemainingRollRemoval(context),

            MarkIgnition =>
                VerifyMarkIgnition(context),

            _ => null
        };

        return result != null;
    }

    private static CharacterVerificationCaseResult
        VerifySkillCoverage(
            CharacterVerificationContext context)
    {
        HashSet<string> existing =
            new HashSet<string>(
                context.Bundle
                    .EnumerateSkillDefinitions()
                    .Where(item => item != null)
                    .Select(item => item.SkillId),
                StringComparer.Ordinal);

        List<string> missing =
            RequiredSkillIds
                .Where(id => !existing.Contains(id))
                .ToList();

        return missing.Count == 0
            ? context.Pass(
                "유진 고유 스킬 ID 12개 존재",
                "12/12 등록")
            : context.Fail(
                "유진 고유 스킬 ID 12개 존재",
                $"{12 - missing.Count}/12 등록",
                "누락:\n" + string.Join("\n", missing));
    }

    private static CharacterVerificationCaseResult
        VerifyHwanhyeongLoadoutContract(
            CharacterVerificationContext context)
    {
        CharacterCombatLoadout loadout =
            context?.Bundle?.CombatLoadout;

        if (loadout == null)
        {
            return context.Fail(
                "유진 CombatLoadout 존재",
                "NULL");
        }

        HashSet<string> equipped =
            new HashSet<string>(
                loadout.GetEquipped(ActionType.Preparation)
                    .Where(skill => skill != null)
                    .Select(skill => skill.SkillId),
                StringComparer.Ordinal);

        string[] hwanhyeongIds =
        {
            YujinSkillIds.HwanhyeongBaeku,
            YujinSkillIds.HwanhyeongJeokseol,
            YujinSkillIds.HwanhyeongNakil
        };

        bool equippedExactly =
            equipped.Count == 3 &&
            hwanhyeongIds.All(equipped.Contains);

        HashSet<string> candidates =
            new HashSet<string>(
                loadout.EnumeratePreparationPool()
                    .Where(skill => skill != null)
                    .Select(skill => skill.SkillId),
                StringComparer.Ordinal);

        bool oldPreparationPreserved =
            candidates.Contains(YujinSkillIds.Capture) &&
            candidates.Contains(YujinSkillIds.Sentencing);

        Dictionary<string, SkillDefinition> definitions =
            context.Bundle
                .EnumerateSkillDefinitions()
                .Where(skill => skill != null)
                .GroupBy(skill => skill.SkillId)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);

        bool contracts =
            hwanhyeongIds.All(
                id =>
                    definitions.TryGetValue(
                        id,
                        out SkillDefinition skill) &&
                    skill.ActionType == ActionType.Preparation &&
                    skill.OverrideEnergyCost &&
                    skill.EnergyCost == YujinMechanic.WeaponSwitchEnergyCost);

        bool valid =
            equippedExactly &&
            oldPreparationPreserved &&
            contracts;

        string detail =
            $"Equipped=[{string.Join(", ", equipped)}], " +
            $"CaptureCandidate={candidates.Contains(YujinSkillIds.Capture)}, " +
            $"SentencingCandidate={candidates.Contains(YujinSkillIds.Sentencing)}, " +
            $"Contracts={contracts}";

        return valid
            ? context.Pass(
                "환형 3종 기본 장착 + 기존 도사림 후보 보존",
                detail)
            : context.Fail(
                "환형 3종 기본 장착 + 기존 도사림 후보 보존",
                detail);
    }

    private static CharacterVerificationCaseResult
        VerifyHwanhyeongFlowPassive(
            CharacterVerificationContext context)
    {
        Yujin yujin =
            context?.Character as Yujin;

        YujinMechanic mechanic =
            yujin?.YujinMechanic;

        YujinHwanhyeongFlowPassiveMechanic passiveMechanic =
            yujin?.GetMechanic<
                YujinHwanhyeongFlowPassiveMechanic>();

        bool bundleHasPassive =
            context?.Bundle?.EquippedAugments != null &&
            context.Bundle.EquippedAugments.Any(
                augment =>
                    augment is YujinHwanhyeongFlowPassive);

        if (yujin == null ||
            mechanic == null ||
            passiveMechanic == null ||
            !bundleHasPassive ||
            context?.BattleContext?._battleEvent == null)
        {
            return context.Fail(
                "환형의 흐름 패시브 등록",
                $"Yujin={yujin != null}, Core={mechanic != null}, " +
                $"PassiveMechanic={passiveMechanic != null}, " +
                $"BundlePassive={bundleHasPassive}");
        }

        mechanic.SetWeaponForVerification(
            YujinWeaponType.Baeku);

        // 턴 상태를 정상화하고 기본 TurnStart +1은 기준값에 포함시킨다.
        context.BattleContext._battleEvent
            .RaiseTurnStart(410);

        int beforeSwitchTurn =
            mechanic.Sense;

        bool queued =
            mechanic.QueueWeaponSwitchFromPreparation(
                YujinWeaponType.Jeokseol);

        bool stayedCurrentTurn =
            mechanic.CurrentWeapon == YujinWeaponType.Baeku &&
            mechanic.HasPendingWeapon &&
            mechanic.PendingWeapon == YujinWeaponType.Jeokseol;

        context.BattleContext._battleEvent
            .RaiseTurnStart(411);

        int switchTurnGain =
            mechanic.Sense - beforeSwitchTurn;

        bool appliedNextTurn =
            mechanic.CurrentWeapon == YujinWeaponType.Jeokseol &&
            !mechanic.HasPendingWeapon;

        int beforeNormalTurn =
            mechanic.Sense;

        context.BattleContext._battleEvent
            .RaiseTurnStart(412);

        int normalTurnGain =
            mechanic.Sense - beforeNormalTurn;

        bool valid =
            queued &&
            stayedCurrentTurn &&
            appliedNextTurn &&
            passiveMechanic.SenseGain == 1 &&
            switchTurnGain == 2 &&
            normalTurnGain == 1;

        string detail =
            $"Queued={queued}, Stayed={stayedCurrentTurn}, " +
            $"Applied={appliedNextTurn}, SwitchTurnGain={switchTurnGain}, " +
            $"NormalTurnGain={normalTurnGain}, PassiveGain={passiveMechanic.SenseGain}";

        return valid
            ? context.Pass(
                "환형 완료 턴 기본 +1 + 패시브 +1",
                detail)
            : context.Fail(
                "환형 완료 턴 기본 +1 + 패시브 +1",
                detail);
    }

    private static CharacterVerificationCaseResult
        VerifyMechanic(
            CharacterVerificationContext context)
    {
        Yujin yujin =
            context.Character as Yujin;

        YujinMechanic mechanic =
            yujin?.YujinMechanic;

        bool valid =
            mechanic != null &&
            mechanic.IsRegistered &&
            mechanic.Owner == yujin;

        return valid
            ? context.Pass(
                "YujinMechanic 생성·등록·Owner 연결",
                "Registered")
            : context.Fail(
                "YujinMechanic 생성·등록·Owner 연결",
                mechanic == null
                    ? "NULL"
                    : $"Registered={mechanic.IsRegistered}, " +
                      $"OwnerMatch={mechanic.Owner == yujin}");
    }

    private static CharacterVerificationCaseResult
        VerifyUnlimitedWeaponSwitch(
            CharacterVerificationContext context)
    {
        Yujin yujin =
            context.Character as Yujin;

        YujinMechanic mechanic =
            yujin?.YujinMechanic;

        if (mechanic == null ||
            yujin == null)
        {
            return context.Fail(
                "유진 환형",
                "Mechanic 또는 Character 없음");
        }

        mechanic.SetWeaponForVerification(
            YujinWeaponType.Baeku);

        context.BattleContext
            ?._battleEvent
            ?.RaiseTurnStart(100);

        yujin.AddEnergy(
            Mathf.Max(
                1,
                yujin.MaxEnergy));

        int energyBefore =
            yujin.CurrentEnergy;

        int eventCount = 0;
        List<string> transitions =
            new List<string>();

        void OnChanged(
            YujinWeaponType previous,
            YujinWeaponType current)
        {
            eventCount++;
            transitions.Add(
                $"{previous}->{current}");
        }

        mechanic.WeaponChanged +=
            OnChanged;

        try
        {
            bool first =
                mechanic.TrySwitchWeapon(
                    YujinWeaponType.Jeokseol);

            int energyAfterFirst =
                yujin.CurrentEnergy;

            bool queuedFirst =
                mechanic.HasPendingWeapon &&
                mechanic.PendingWeapon == YujinWeaponType.Jeokseol &&
                mechanic.CurrentWeapon == YujinWeaponType.Baeku &&
                eventCount == 0;

            bool secondSameTurn =
                mechanic.TrySwitchWeapon(
                    YujinWeaponType.Nakil);

            context.BattleContext
                ?._battleEvent
                ?.RaiseTurnStart(101);

            bool firstApplied =
                mechanic.CurrentWeapon == YujinWeaponType.Jeokseol &&
                !mechanic.HasPendingWeapon &&
                eventCount == 1;

            yujin.AddEnergy(1);

            int energyBeforeNextTurnSwitch =
                yujin.CurrentEnergy;

            bool nextTurn =
                mechanic.TrySwitchWeapon(
                    YujinWeaponType.Nakil);

            int energyAfterNextTurnSwitch =
                yujin.CurrentEnergy;

            bool queuedSecond =
                mechanic.HasPendingWeapon &&
                mechanic.PendingWeapon == YujinWeaponType.Nakil &&
                mechanic.CurrentWeapon == YujinWeaponType.Jeokseol &&
                eventCount == 1;

            context.BattleContext
                ?._battleEvent
                ?.RaiseTurnStart(102);

            bool secondApplied =
                mechanic.CurrentWeapon == YujinWeaponType.Nakil &&
                !mechanic.HasPendingWeapon &&
                eventCount == 2;

            bool valid =
                first &&
                queuedFirst &&
                !secondSameTurn &&
                firstApplied &&
                nextTurn &&
                queuedSecond &&
                secondApplied &&
                energyAfterFirst ==
                    energyBefore -
                    YujinMechanic.WeaponSwitchEnergyCost &&
                energyAfterNextTurnSwitch ==
                    energyBeforeNextTurnSwitch -
                    YujinMechanic.WeaponSwitchEnergyCost;

            string detail =
                $"First={first}/{queuedFirst}, SameTurnSecond={secondSameTurn}, " +
                $"FirstApplied={firstApplied}, NextTurn={nextTurn}/{queuedSecond}, " +
                $"SecondApplied={secondApplied}, Energy={energyBefore}→" +
                $"{energyAfterFirst}/{energyBeforeNextTurnSwitch}→" +
                $"{energyAfterNextTurnSwitch}, Events={eventCount}";

            return valid
                ? context.Pass(
                    "환형 빛 1 소비·턴당 1회·다음 턴 적용",
                    detail,
                    string.Join(", ", transitions))
                : context.Fail(
                    "환형 빛 1 소비·턴당 1회·다음 턴 적용",
                    detail,
                    string.Join(", ", transitions));
        }
        finally
        {
            mechanic.WeaponChanged -=
                OnChanged;
        }
    }

    private static CharacterVerificationCaseResult
        VerifyWeaponSwitchBeforeAction(
            CharacterVerificationContext context)
    {
        Yujin yujin =
            context.Character as Yujin;

        YujinMechanic mechanic =
            yujin?.YujinMechanic;

        if (mechanic == null ||
            yujin == null ||
            context.BattleContext == null)
        {
            return context.Fail(
                "환형 계획 중 예약 Fixture 존재",
                "Mechanic, Character 또는 BattleContext 없음");
        }

        mechanic.SetWeaponForVerification(
            YujinWeaponType.Baeku);

        context.BattleContext
            ._battleEvent
            .RaiseTurnStart(300);

        yujin.AddEnergy(
            Mathf.Max(
                1,
                yujin.MaxEnergy));

        int energyBefore =
            yujin.CurrentEnergy;

        ActionManager actionManager =
            new ActionManager();

        BattleRuntimeServices previousServices =
            context.BattleContext.Services;

        BattleRuntimeServices verificationServices =
            previousServices?.Clone() ??
            new BattleRuntimeServices();

        bool canWithSelectedSlot = false;
        bool queuedWithSelectedSlot = false;
        int energyAfterQueue = energyBefore;
        bool stayedCurrentTurn = false;
        bool appliedNextTurn = false;

        try
        {
            verificationServices.ActionManager =
                actionManager;

            context.BattleContext.Services =
                verificationServices;

            FieldInfo slotsField =
                typeof(ActionManager).GetField(
                    "slots",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            List<ActionSlot> slots =
                slotsField?.GetValue(
                    actionManager)
                    as List<ActionSlot>;

            if (slots == null)
            {
                return context.Fail(
                    "검증용 ActionManager 슬롯 목록",
                    "slots=NULL");
            }

            slots.Add(
                new ActionSlot
                {
                    ActionId = 960001,
                    Owner = yujin,
                    Part = yujin.BodyParts?
                        .FirstOrDefault(),
                    Skill = yujin.RuntimeSkills?
                        .FirstOrDefault(),
                    Speed = 5,
                    SlotId = "VERIFY:YUJIN:SELECTED"
                });

            canWithSelectedSlot =
                mechanic.CanSwitchWeapon(
                    YujinWeaponType.Jeokseol);

            queuedWithSelectedSlot =
                mechanic.TrySwitchWeapon(
                    YujinWeaponType.Jeokseol);

            energyAfterQueue =
                yujin.CurrentEnergy;

            stayedCurrentTurn =
                mechanic.CurrentWeapon == YujinWeaponType.Baeku &&
                mechanic.HasPendingWeapon &&
                mechanic.PendingWeapon == YujinWeaponType.Jeokseol;

            context.BattleContext
                ._battleEvent
                .RaiseTurnStart(301);

            appliedNextTurn =
                mechanic.CurrentWeapon == YujinWeaponType.Jeokseol &&
                !mechanic.HasPendingWeapon;
        }
        finally
        {
            context.BattleContext.Services =
                previousServices;

            actionManager.Dispose();
        }

        bool valid =
            canWithSelectedSlot &&
            queuedWithSelectedSlot &&
            energyAfterQueue ==
                energyBefore -
                YujinMechanic.WeaponSwitchEnergyCost &&
            stayedCurrentTurn &&
            appliedNextTurn;

        string detail =
            $"SelectedSlot={canWithSelectedSlot}/{queuedWithSelectedSlot}, " +
            $"Energy={energyBefore}→{energyAfterQueue}, " +
            $"StayedCurrentTurn={stayedCurrentTurn}, " +
            $"AppliedNextTurn={appliedNextTurn}";

        return valid
            ? context.Pass(
                "계획 슬롯 배치 후에도 환형 예약 가능·다음 턴 적용",
                detail)
            : context.Fail(
                "계획 슬롯 배치 후에도 환형 예약 가능·다음 턴 적용",
                detail);
    }

    private static CharacterVerificationCaseResult
        VerifyWeaponProfiles(
            CharacterVerificationContext context)
    {
        YujinWeaponProfile baeku =
            YujinWeapons.Get(
                YujinWeaponType.Baeku);

        YujinWeaponProfile jeokseol =
            YujinWeapons.Get(
                YujinWeaponType.Jeokseol);

        YujinWeaponProfile nakil =
            YujinWeapons.Get(
                YujinWeaponType.Nakil);

        bool valid =
            baeku.CoinCount == 3 &&
            Math.Abs(
                baeku.FrontChance - 0.40f) <
            0.0001f &&
            baeku.CriticalValue == 12 &&
            baeku.BaseMarkAmount == 4 &&

            jeokseol.CoinCount == 2 &&
            Math.Abs(
                jeokseol.FrontChance - 0.30f) <
            0.0001f &&
            jeokseol.CriticalValue == 15 &&
            jeokseol.BaseMarkAmount == 6 &&

            nakil.CoinCount == 1 &&
            Math.Abs(
                nakil.FrontChance - 0.25f) <
            0.0001f &&
            nakil.CriticalValue == 19 &&
            nakil.BaseMarkAmount == 8;

        return valid
            ? context.Pass(
                "백우 3/40%/12/4, 적설 2/30%/15/6, 낙일 1/25%/19/8",
                "무기 프로필 일치")
            : context.Fail(
                "백우 3/40%/12/4, 적설 2/30%/15/6, 낙일 1/25%/19/8",
                $"Baeku={Describe(baeku)}, " +
                $"Jeokseol={Describe(jeokseol)}, " +
                $"Nakil={Describe(nakil)}");
    }

    private static CharacterVerificationCaseResult
        VerifySenseTurnStart(
            CharacterVerificationContext context)
    {
        Yujin yujin =
            context.Character as Yujin;

        YujinMechanic mechanic =
            yujin?.YujinMechanic;

        Character enemy =
            context.OpponentCharacter;

        BodyPart ownPart =
            yujin?.BodyParts?
                .FirstOrDefault();

        BodyPart enemyPart =
            enemy?.BodyParts?
                .FirstOrDefault();

        if (mechanic == null ||
            ownPart == null ||
            enemyPart == null)
        {
            return context.Fail(
                "살수의 감 재충전 조건",
                "Mechanic 또는 부위 Fixture 없음");
        }

        CharacterVerificationReflection.TrySetField(
            mechanic,
            "sense",
            0);

        context.BattleContext
            ?._battleEvent
            ?.RaiseTurnStart(1);

        int afterTurnStart =
            mechanic.Sense;

        context.BattleContext
            ?._battleEvent
            ?.RaiseBodyPartDestroyed(
                BodyPartBreakEventContext.External(
                    yujin,
                    yujin,
                    ownPart));

        int afterOwnBreak =
            mechanic.Sense;

        context.BattleContext
            ?._battleEvent
            ?.RaiseBodyPartDestroyed(
                BodyPartBreakEventContext.External(
                    yujin,
                    enemy,
                    enemyPart));

        int afterEnemyBreak =
            mechanic.Sense;

        bool valid =
            afterTurnStart == 1 &&
            afterOwnBreak == 1 &&
            afterEnemyBreak == 2;

        string detail =
            $"TurnStart={afterTurnStart}, " +
            $"OwnBreak={afterOwnBreak}, " +
            $"EnemyBreak={afterEnemyBreak}";

        return valid
            ? context.Pass(
                "턴 시작 +1·자가 파괴 제외·적 파괴 +1",
                detail)
            : context.Fail(
                "턴 시작 +1·자가 파괴 제외·적 파괴 +1",
                detail);
    }

    private static CharacterVerificationCaseResult
        VerifySenseReroll(
            CharacterVerificationContext context)
    {
        Yujin yujin =
            context.Character as Yujin;

        YujinMechanic mechanic =
            yujin?.YujinMechanic;

        Skill duelSkill =
            yujin?.RuntimeSkills?
                .FirstOrDefault(
                    skill =>
                        skill?.Definition != null &&
                        (skill.Definition.SkillId ==
                         YujinSkillIds.Inscription ||
                         skill.Definition.SkillId ==
                         YujinSkillIds.Pursuit));

        if (mechanic == null ||
            duelSkill == null)
        {
            return context.Fail(
                "살수의 감 재굴림 실행 조건",
                mechanic == null
                    ? "Mechanic 없음"
                    : "각인·추격 RuntimeSkill 없음");
        }

        if (!CharacterVerificationReflection.TrySetField(
                mechanic,
                "sense",
                1))
        {
            return context.Fail(
                "검증 Fixture에서 감 1 Seed",
                "sense 필드 접근 실패");
        }

        // 전역 표시값이 ON이어도 실제 행동 슬롯에서 선택하지 않으면
        // 살수의 감을 사용하지 않아야 한다.
        mechanic.AutoUseSense = true;

        ActionSlot mySlot =
            new ActionSlot
            {
                ActionId = 101,
                Owner = yujin,
                Part =
                    yujin.BodyParts?
                        .FirstOrDefault(),
                Skill = duelSkill,
                Speed = 5,
                UseCharacterRerollResource = false
            };

        BattleAction myAction =
            new BattleAction
            {
                Slot = mySlot,
                ClashPower = 5,
                LastRollResult =
                    new RollResult
                    {
                        FinalPower = 5,
                        ClashPower = 5,
                        IsCritical = false
                    }
            };

        BattleAction opponent =
            new BattleAction
            {
                Slot =
                    new ActionSlot
                    {
                        ActionId = 202,
                        Owner = yujin,
                        Skill = duelSkill,
                        Speed = 6
                    },
                ClashPower = 10,
                LastRollResult =
                    new RollResult
                    {
                        FinalPower = 10,
                        ClashPower = 10,
                        IsCritical = false
                    }
            };

        ExchangeRerollContext reroll =
            new ExchangeRerollContext(
                myAction,
                opponent,
                0,
                0);

        bool withoutSelection =
            mechanic.TryRequestExchangeReroll(
                reroll);

        int beforeSelectedRequest =
            mechanic.Sense;

        mySlot.UseCharacterRerollResource = true;

        bool requested =
            mechanic.TryRequestExchangeReroll(
                reroll);

        int remain =
            mechanic.Sense;

        bool secondRequest =
            mechanic.TryRequestExchangeReroll(
                reroll);

        bool valid =
            !withoutSelection &&
            beforeSelectedRequest == 1 &&
            requested &&
            remain == 0 &&
            !secondRequest;

        string detail =
            $"OffRequest={withoutSelection}, " +
            $"BeforeOn={beforeSelectedRequest}, " +
            $"OnRequest={requested}, Sense={remain}, " +
            $"Second={secondRequest}";

        return valid
            ? context.Pass(
                "행동별 감 사용 OFF/ON 선택",
                detail)
            : context.Fail(
                "행동별 감 사용 OFF/ON 선택",
                detail);
    }

    private static CharacterVerificationCaseResult
        VerifyNakilRemainingRollRemoval(
            CharacterVerificationContext context)
    {
        Yujin yujin =
            context.Character as Yujin;

        Character target =
            context.OpponentCharacter;

        YujinMechanic mechanic =
            yujin?.YujinMechanic;

        Skill yujinSkill =
            yujin?.RuntimeSkills?
                .FirstOrDefault(
                    item => item?.ActionType == ActionType.Duel) ??
            yujin?.RuntimeSkills?
                .FirstOrDefault();

        Skill opponentSkill =
            target?.RuntimeSkills?
                .FirstOrDefault();

        BodyPart ownerPart =
            CharacterVerificationScenarioTools.GetUsablePart(yujin);

        BodyPart targetPart =
            CharacterVerificationScenarioTools.GetUsablePart(target);

        MethodInfo method =
            typeof(ClashManager).GetMethod(
                "ApplyNakilRemainingRollRemoval",
                BindingFlags.Static |
                BindingFlags.NonPublic);

        if (mechanic == null ||
            yujinSkill == null ||
            opponentSkill == null ||
            method == null)
        {
            return context.Fail(
                "낙일 잔여 굴림 제거 Fixture와 실제 처리 메서드 존재",
                $"Mechanic={mechanic != null}, YujinSkill={yujinSkill != null}, " +
                $"OpponentSkill={opponentSkill != null}, Method={method != null}");
        }

        BattleAction mine =
            CharacterVerificationScenarioTools.CreateAction(
                yujin,
                ownerPart,
                yujinSkill,
                target,
                targetPart,
                970001);

        BattleAction theirs =
            CharacterVerificationScenarioTools.CreateAction(
                target,
                targetPart,
                opponentSkill,
                yujin,
                ownerPart,
                970002);

        mechanic.SetWeaponForVerification(
            YujinWeaponType.Nakil);

        ClashExchangeResult firstWinner =
            new ClashExchangeResult
            {
                FirstAction = mine,
                SecondAction = theirs,
                WinnerAction = mine,
                LoserAction = theirs,
                IsDuelExchange = true
            };

        object[] firstArgs =
        {
            firstWinner,
            mine,
            theirs,
            2,
            4
        };

        method.Invoke(
            null,
            firstArgs);

        int firstRemainAfter =
            (int)firstArgs[3];

        int secondRemainAfter =
            (int)firstArgs[4];

        ClashExchangeResult secondWinner =
            new ClashExchangeResult
            {
                FirstAction = theirs,
                SecondAction = mine,
                WinnerAction = mine,
                LoserAction = theirs,
                IsDuelExchange = true
            };

        object[] secondArgs =
        {
            secondWinner,
            theirs,
            mine,
            5,
            3
        };

        method.Invoke(
            null,
            secondArgs);

        int opponentFirstAfter =
            (int)secondArgs[3];

        int yujinSecondAfter =
            (int)secondArgs[4];

        ClashExchangeResult oneSided =
            new ClashExchangeResult
            {
                FirstAction = mine,
                SecondAction = theirs,
                WinnerAction = mine,
                LoserAction = theirs,
                IsDuelExchange = true,
                IsOneSided = true
            };

        object[] oneSidedArgs =
        {
            oneSided,
            mine,
            theirs,
            2,
            4
        };

        method.Invoke(
            null,
            oneSidedArgs);

        int oneSideFirst =
            (int)oneSidedArgs[3];

        int oneSideSecond =
            (int)oneSidedArgs[4];

        mechanic.SetWeaponForVerification(
            YujinWeaponType.Baeku);

        object[] nonNakilArgs =
        {
            firstWinner,
            mine,
            theirs,
            2,
            4
        };

        method.Invoke(
            null,
            nonNakilArgs);

        int nonNakilFirst =
            (int)nonNakilArgs[3];

        int nonNakilSecond =
            (int)nonNakilArgs[4];

        bool valid =
            firstRemainAfter == 2 &&
            secondRemainAfter == 0 &&
            opponentFirstAfter == 0 &&
            yujinSecondAfter == 3 &&
            oneSideFirst == 2 &&
            oneSideSecond == 4 &&
            nonNakilFirst == 2 &&
            nonNakilSecond == 4;

        string detail =
            $"YujinFirst=2/{secondRemainAfter}, " +
            $"YujinSecond={opponentFirstAfter}/3, " +
            $"OneSide={oneSideFirst}/{oneSideSecond}, " +
            $"Baeku={nonNakilFirst}/{nonNakilSecond}";

        return valid
            ? context.Pass(
                "낙일 교환 승리 시 상대 잔여 굴림만 0·일방/다른 무기 미적용",
                detail)
            : context.Fail(
                "낙일 교환 승리 시 상대 잔여 굴림만 0·일방/다른 무기 미적용",
                detail);
    }

    private static CharacterVerificationCaseResult
        VerifyMarkIgnition(
            CharacterVerificationContext context)
    {
        Yujin yujin =
            context.Character as Yujin;

        YujinMechanic mechanic =
            yujin?.YujinMechanic;

        BodyPart targetPart =
            yujin?.BodyParts?
                .FirstOrDefault(
                    part =>
                        part != null &&
                        !part.IsBroken);

        if (mechanic == null ||
            targetPart == null)
        {
            return context.Fail(
                "표식 발화 실행 조건",
                mechanic == null
                    ? "Mechanic 없음"
                    : "대상 부위 없음");
        }

        if (mechanic.CurrentWeapon !=
            YujinWeaponType.Nakil)
        {
            mechanic.SetWeaponForVerification(
                YujinWeaponType.Nakil);
        }

        bool firstInvoke =
            CharacterVerificationReflection.TryInvoke(
                mechanic,
                "AddMark",
                new object[]
                {
                    targetPart,
                    43,
                    null
                },
                out _);

        int beforeIgnition =
            mechanic.GetMark(
                targetPart);

        bool secondInvoke =
            CharacterVerificationReflection.TryInvoke(
                mechanic,
                "AddMark",
                new object[]
                {
                    targetPart,
                    1,
                    null
                },
                out _);

        int afterIgnition =
            mechanic.GetMark(
                targetPart);

        bool valid =
            firstInvoke &&
            secondInvoke &&
            beforeIgnition == 43 &&
            afterIgnition == 0 &&
            targetPart.IsWeakened;

        return valid
            ? context.Pass(
                "표식 43→44 발화, 표식 0, 낙일 약화",
                $"Mark={beforeIgnition}→{afterIgnition}, " +
                $"State={targetPart.State}")
            : context.Fail(
                "표식 43→44 발화, 표식 0, 낙일 약화",
                $"Invoke={firstInvoke}/{secondInvoke}, " +
                $"Mark={beforeIgnition}→{afterIgnition}, " +
                $"State={targetPart.State}");
    }

    private static string Describe(
        YujinWeaponProfile profile)
    {
        return
            $"{profile.Type} " +
            $"{profile.CoinCount}/" +
            $"{profile.FrontChance:0.00}/" +
            $"{profile.CriticalValue}/" +
            $"{profile.BaseMarkAmount}";
    }
}