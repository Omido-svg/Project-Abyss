using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유진의 무기, 살수의 감, 표식, 봉인, 처형, 재굴림, 위세를 통합한다.
/// 표식과 결투 효과는 개별 교환 결과가 공개되는 즉시 처리한다.
/// </summary>
public sealed class YujinMechanic : CombatMechanic,
    ICharacterUniqueGaugeProvider,
    IActionPlanningRule,
    IActionPlanningChoiceRule,
    IActionPlanningCommitRule,
    IExchangeContinuationRule,
    IExchangePreResolutionRule,
    IAdditionalStaggerDamageProvider
{
    public const int MarkIgnitionThreshold = YujinWeapons.MarkIgnitionThreshold;
    public const int WeaponSwitchEnergyCost = 1;

    private readonly Dictionary<CombatStatusAnchor, int>
        marks = new();

    private readonly HashSet<string>
        freeRetrialRerolls = new();

    private readonly DelayedEffectTriggerQueue
        delayedTriggers = new();

    // Planning에서 즉시 적용되는 도사림을 ActionId 단위로 추적한다.
    // 우클릭/AutoPlan 재계획 시 특정 슬롯 하나만 취소해도 다른 동일 도사림은 유지된다.
    private readonly HashSet<long>
        capturePlanningActions = new();

    private readonly HashSet<long>
        sentencingPlanningActions = new();

    private YujinWeaponType currentWeapon;
    private YujinWeaponType pendingWeapon;
    private bool hasPendingWeapon;
    private int pendingWeaponEnergyPaid;
    private int sense;

    private bool captureActive;
    private bool sentencingActive;
    private bool brandActive;
    private bool retrialActive;

    private int forcedFrontCharges;
    private int jointLiabilityGranted;
    private bool weaponSwitchUsedThisTurn;
    private bool weaponChangedThisTurn;
    private int hwanhyeongAttackBonus;
    private int hwanhyeongDefenseBonus;

    /// <summary>
    /// 무기 변경이 실제로 완료된 직후 호출된다.
    /// UI와 자동계획은 이 이벤트 하나를 구독해 표시를 동기화한다.
    /// </summary>
    public event Action<YujinWeaponType, YujinWeaponType> WeaponChanged;

    /// <summary>
    /// 각인·추격을 선택할 때 해당 행동 슬롯에 복사할 살수의 감 사용 선택값.
    /// 실제 재굴림은 BattleAction의 슬롯 값만 사용한다.
    /// </summary>
    public bool AutoUseSense { get; set; }

    public IYujinPeekDecisionProvider PeekDecisionProvider { get; set; }

    public int PendingDelayedTriggerCount => delayedTriggers.Count;

    public bool HasUsedWeaponSwitchThisTurn =>
        weaponSwitchUsedThisTurn;

    /// <summary>0916 손에 익히다 조건: 지난 턴 예약된 환형이 이번 TurnStart에 실제 적용됐는지.</summary>
    public bool WeaponChangedThisTurn => weaponChangedThisTurn;

    public bool IsWeaponSwitchLocked =>
        battleContext?.Services?.TurnManager?.IsResolving == true;

    public YujinWeaponType CurrentWeapon => currentWeapon;
    public bool HasPendingWeapon => hasPendingWeapon;
    public YujinWeaponType PendingWeapon => pendingWeapon;

    public string GaugeLabel => "환형";
    public float GaugeNormalized => ((int)currentWeapon + 1f) / 3f;
    public string GaugeValueText =>
        hasPendingWeapon
            ? $"{currentWeapon} → {pendingWeapon}"
            : currentWeapon.ToString();
    public int GaugeStateVersion =>
        ((int)currentWeapon & 0xFF) |
        (((int)pendingWeapon & 0xFF) << 8) |
        (hasPendingWeapon ? 1 << 16 : 0);

    public YujinWeaponProfile CurrentWeaponProfile =>
        YujinWeapons.Get(currentWeapon);

    public int Sense => sense;

    public float CurrentFrontChance =>
        Mathf.Clamp01(
            CurrentWeaponProfile.FrontChance +
            (captureActive ? 0.10f : 0f));

    public override string MechanicName =>
        "Yujin Weapon / Mark / Sense";

    public YujinMechanic(
        YujinWeaponType startingWeapon)
    {
        currentWeapon = startingWeapon;
    }

    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnTurnStart += OnTurnStart,
            () => battleEvent.OnTurnStart -= OnTurnStart,
            "OnTurnStart");

        SubscribeToBattleEvent(
            () => battleEvent.OnTurnEnd += OnTurnEnd,
            () => battleEvent.OnTurnEnd -= OnTurnEnd,
            "OnTurnEnd");

        SubscribeToBattleEvent(
            () => battleEvent.OnExchangeResolved += OnExchangeResolved,
            () => battleEvent.OnExchangeResolved -= OnExchangeResolved,
            "OnExchangeResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnBodyPartBreakResolved += OnBodyPartBreakResolved,
            () => battleEvent.OnBodyPartBreakResolved -= OnBodyPartBreakResolved,
            "OnBodyPartBreakResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnKillResolved += OnKillResolved,
            () => battleEvent.OnKillResolved -= OnKillResolved,
            "OnKillResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnBattleEnded += OnBattleEnded,
            () => battleEvent.OnBattleEnded -= OnBattleEnded,
            "OnBattleEnded");
    }

    public override void OnUnregister()
    {
        marks.Clear();
        freeRetrialRerolls.Clear();
        delayedTriggers.Clear();
        PeekDecisionProvider = null;
        capturePlanningActions.Clear();
        sentencingPlanningActions.Clear();
        sense = 0;
        weaponSwitchUsedThisTurn = false;
        hasPendingWeapon = false;
        pendingWeaponEnergyPaid = 0;
        weaponChangedThisTurn = false;
        ClearTurnBuffs();
    }

    /// <summary>
    /// 아직 환형을 예약하지 않은 상태에서 새 무기 전환을 시작할 수 있는지 검사한다.
    /// 이미 예약된 환형의 "선택 수정"은 CanSelectHwanhyeongWeapon을 사용한다.
    /// </summary>
    public bool CanSwitchWeapon(
        YujinWeaponType weapon)
    {
        if (IsWeaponSwitchLocked ||
            weaponSwitchUsedThisTurn ||
            owner == null ||
            hasPendingWeapon ||
            weapon == currentWeapon)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 계획 단계에서 환형 카드를 선택할 수 있는지 검사한다.
    /// 첫 선택은 현재 무기와 다른 무기만 가능하고,
    /// 이미 환형이 예약된 뒤에는 다른 환형으로 무료 교체하거나
    /// 현재 무기를 선택해 예약을 취소할 수 있다.
    /// </summary>
    public bool CanSelectHwanhyeongWeapon(
        YujinWeaponType weapon)
    {
        if (IsWeaponSwitchLocked ||
            owner == null)
        {
            return false;
        }

        if (hasPendingWeapon)
        {
            // 같은 예약을 다시 누르는 것만 막는다.
            // currentWeapon 선택은 "예약 취소"로 취급한다.
            return weapon != pendingWeapon;
        }

        return !weaponSwitchUsedThisTurn &&
               weapon != currentWeapon;
    }

    /// <summary>
    /// 구형 UI/검증 호출 호환. 빛 1을 즉시 소비하지만 실제 무기 변경은 다음 턴 시작에 확정한다.
    /// 신규 환형 도사림은 스킬 비용에서 빛을 처리한 뒤 QueueWeaponSwitchFromPreparation을 사용한다.
    /// </summary>
    public bool TrySwitchWeapon(
        YujinWeaponType weapon)
    {
        if (!CanSwitchWeapon(weapon) ||
            owner.CurrentEnergy < WeaponSwitchEnergyCost ||
            !owner.TryConsumeEnergy(WeaponSwitchEnergyCost))
        {
            return false;
        }

        return QueueWeaponSwitchFromPreparation(
            weapon,
            WeaponSwitchEnergyCost);
    }

    public bool QueueWeaponSwitchFromPreparation(
        YujinWeaponType weapon,
        int paidEnergyCost = WeaponSwitchEnergyCost)
    {
        // 이 메서드는 ActionResolver가 FORESIGHT를 실제 해결하는 동안 호출된다.
        // 따라서 UI/계획 입력용 잠금(IsWeaponSwitchLocked)을 검사하면 안 된다.
        // 기존 구현은 CanSwitchWeapon()을 재사용해 IsResolving=true인 순간
        // 모든 환형 실행을 조용히 거절하고 있었다.
        if (!CanQueueResolvedWeaponSwitch(weapon))
        {
            Debug.LogWarning(
                $"[유진][환형] 예약 실패 / " +
                $"Current={currentWeapon}, Requested={weapon}, " +
                $"Pending={hasPendingWeapon}, UsedThisTurn={weaponSwitchUsedThisTurn}, " +
                $"Resolving={IsWeaponSwitchLocked}");
            return false;
        }

        pendingWeapon = weapon;
        hasPendingWeapon = true;
        pendingWeaponEnergyPaid =
            Mathf.Max(0, paidEnergyCost);
        weaponSwitchUsedThisTurn = true;

        Debug.Log(
            $"[유진][환형] {currentWeapon} -> {pendingWeapon} 예약 / " +
            $"적용=다음 턴 / 지불한 빛={pendingWeaponEnergyPaid}");

        return true;
    }

    /// <summary>
    /// 이미 비용이 지불된 환형 도사림을 전투 해결 중 예약할 때 사용하는 검증.
    /// 입력 잠금은 계획/UI에만 적용하고, 실제 해결 경로에서는 현재 상태 불변식만 검사한다.
    /// </summary>
    private bool CanQueueResolvedWeaponSwitch(
        YujinWeaponType weapon)
    {
        return owner != null &&
               !weaponSwitchUsedThisTurn &&
               !hasPendingWeapon &&
               weapon != currentWeapon;
    }

    /// <summary>
    /// 이미 비용을 지불한 환형 예약을 계획 단계에서 수정한다.
    /// 다른 무기로 바꿀 때 추가 빛은 들지 않는다.
    /// 현재 무기를 선택하면 예약 자체를 취소하고 기존 환형 비용을 환불한다.
    /// </summary>
    public bool TryEditPendingWeaponSwitch(
        YujinWeaponType weapon)
    {
        if (!hasPendingWeapon ||
            !CanSelectHwanhyeongWeapon(weapon))
        {
            return false;
        }

        YujinWeaponType previousPending =
            pendingWeapon;

        if (weapon == currentWeapon)
        {
            int refund =
                Mathf.Max(0, pendingWeaponEnergyPaid);

            hasPendingWeapon = false;
            pendingWeapon = currentWeapon;
            pendingWeaponEnergyPaid = 0;
            weaponSwitchUsedThisTurn = false;

            if (refund > 0)
            {
                owner?.AddEnergy(
                    refund,
                    CombatResourceChangeReason.Restore);
            }

            Debug.Log(
                $"[유진][환형] 예약 취소 {previousPending} / " +
                $"빛 +{refund}");

            return true;
        }

        pendingWeapon = weapon;
        weaponSwitchUsedThisTurn = true;

        Debug.Log(
            $"[유진][환형] 예약 변경 {previousPending} -> {pendingWeapon} / " +
            "추가 빛 0");

        return true;
    }

    public string GetHwanhyeongSelectionReason(
        YujinWeaponType weapon)
    {
        if (owner == null)
            return "유진 메커니즘 미연결";

        if (IsWeaponSwitchLocked)
            return "전투 해결 중에는 환형 변경 불가";

        if (hasPendingWeapon)
        {
            if (weapon == pendingWeapon)
                return $"이미 {pendingWeapon} 전환 예약됨";

            // 다른 무기 또는 현재 무기(예약 취소)는 선택 가능.
            return string.Empty;
        }

        if (weaponSwitchUsedThisTurn)
            return "이번 턴 환형 사용됨";

        if (weapon == currentWeapon)
            return "현재 무기와 동일";

        return string.Empty;
    }

    private enum HwanhyeongMode
    {
        None = 0,
        Attack = 1,
        Defense = 2
    }

    private static HwanhyeongMode GetHwanhyeongMode(string skillId)
    {
        if (skillId == YujinSkillIds.HwanhyeongDefense)
            return HwanhyeongMode.Defense;
        if (skillId == YujinSkillIds.HwanhyeongAttack ||
            skillId == YujinSkillIds.HwanhyeongBaeku ||
            skillId == YujinSkillIds.HwanhyeongJeokseol ||
            skillId == YujinSkillIds.HwanhyeongNakil)
            return HwanhyeongMode.Attack;
        return HwanhyeongMode.None;
    }

    private static bool IsHwanhyeongSkill(string skillId) =>
        GetHwanhyeongMode(skillId) != HwanhyeongMode.None;

    private static bool TryParseWeaponChoice(string choiceId, out YujinWeaponType weapon)
    {
        return System.Enum.TryParse(choiceId, true, out weapon);
    }

    IReadOnlyList<ActionPlanningChoiceOption> IActionPlanningChoiceRule.GetPlanningChoices(
        ActionPlanningSkillContext context)
    {
        if (context.Owner != owner || !IsHwanhyeongSkill(context.Skill?.Definition?.SkillId))
            return System.Array.Empty<ActionPlanningChoiceOption>();

        List<ActionPlanningChoiceOption> result = new(2);
        foreach (YujinWeaponType weapon in System.Enum.GetValues(typeof(YujinWeaponType)))
        {
            if (weapon == currentWeapon)
                continue;
            result.Add(new ActionPlanningChoiceOption(weapon.ToString(), GetWeaponDisplayName(weapon)));
        }
        return result;
    }

    private static string GetWeaponDisplayName(YujinWeaponType weapon)
    {
        return weapon switch
        {
            YujinWeaponType.Baeku => "백우",
            YujinWeaponType.Jeokseol => "적설",
            YujinWeaponType.Nakil => "낙일",
            _ => weapon.ToString()
        };
    }

    bool IActionPlanningCommitRule.TryCommitPlannedSlot(ActionSlot slot, out string failureReason)
    {
        failureReason = string.Empty;
        if (slot?.Owner != owner || !IsHwanhyeongSkill(slot.Skill?.Definition?.SkillId))
            return true;
        if (slot.PlanningEffectCommitted)
            return true;
        if (!TryParseWeaponChoice(slot.PlanningChoiceId, out YujinWeaponType targetWeapon) ||
            targetWeapon == currentWeapon)
        {
            failureReason = "환형은 현재 들지 않은 무기 2개 중 하나를 선택해야 합니다.";
            return false;
        }
        if (hasPendingWeapon || weaponSwitchUsedThisTurn)
        {
            failureReason = "이번 턴 환형이 이미 지정되었습니다.";
            return false;
        }

        pendingWeapon = targetWeapon;
        hasPendingWeapon = true;
        pendingWeaponEnergyPaid = 0;
        weaponSwitchUsedThisTurn = true;

        HwanhyeongMode mode = GetHwanhyeongMode(slot.Skill.Definition?.SkillId);
        if (mode == HwanhyeongMode.Attack)
        {
            hwanhyeongAttackBonus = currentWeapon switch
            {
                YujinWeaponType.Nakil => 8,
                YujinWeaponType.Jeokseol => 4,
                _ => 2
            };
        }
        else if (mode == HwanhyeongMode.Defense)
        {
            hwanhyeongDefenseBonus = 30;
            owner.AddBlock(hwanhyeongDefenseBonus);
        }

        slot.PlanningEffectCommitted = true;
        Debug.Log($"[유진][환형] 계획 즉시 적용 / mode={mode}, current={currentWeapon}, next={pendingWeapon}");
        return true;
    }

    void IActionPlanningCommitRule.RollbackPlannedSlot(ActionSlot slot)
    {
        if (slot?.Owner != owner || !slot.PlanningEffectCommitted ||
            !IsHwanhyeongSkill(slot.Skill?.Definition?.SkillId))
            return;

        if (hwanhyeongDefenseBonus > 0)
            owner?.RemoveBlock(hwanhyeongDefenseBonus);

        hwanhyeongAttackBonus = 0;
        hwanhyeongDefenseBonus = 0;
        hasPendingWeapon = false;
        pendingWeapon = currentWeapon;
        pendingWeaponEnergyPaid = 0;
        weaponSwitchUsedThisTurn = false;
        slot.PlanningEffectCommitted = false;
    }

    string IActionPlanningRule.GetSkillSelectionBlockReason(
        ActionPlanningSkillContext context)
    {
        if (context.Skill == null || context.Owner != owner)
            return string.Empty;

        string selectedSkillId = context.Skill.Definition?.SkillId;

        if (selectedSkillId == YujinSkillIds.FamiliarHand && !weaponChangedThisTurn)
            return "지난 턴 환형이 완료되어 이번 턴 새 무기가 활성화된 상태에서만 사용할 수 있습니다.";

        if (selectedSkillId == YujinSkillIds.AceInTheHole && sense < GetAceInTheHoleSenseCost())
            return $"살수의 감이 부족합니다. 필요 {GetAceInTheHoleSenseCost()}, 현재 {sense}.";

        if (selectedSkillId == YujinSkillIds.LedgerCleanup && !HasAnyMarkAtLeast(20))
            return "사용 조건: 표식 20 이상인 대상이 필요합니다.";

        if ((selectedSkillId == YujinSkillIds.HoldBreath || selectedSkillId == YujinSkillIds.Sharpen) &&
            !HasAnyMarkAtLeast(10))
            return "소모할 표식 10이 없습니다.";

        if (!IsHwanhyeongSkill(selectedSkillId))
            return string.Empty;

        ActionSlot existing =
            FindPlannedHwanhyeongSlot(
                context.PlannedSlots);

        bool editingSameSlot = existing != null &&
            IsSamePart(existing.Part, context.Part) &&
            existing.ActionIndex == context.ActionIndex;

        if (existing != null && !editingSameSlot)
            return "이번 턴 환형 이미 지정됨";

        if (!string.IsNullOrWhiteSpace(context.ChoiceId) &&
            (!TryParseWeaponChoice(context.ChoiceId, out YujinWeaponType selectedWeapon) ||
             selectedWeapon == currentWeapon))
        {
            return "현재 들지 않은 무기 2개 중 하나를 선택해야 합니다.";
        }

        return !editingSameSlot && (weaponSwitchUsedThisTurn || hasPendingWeapon)
            ? "이번 턴 환형 이미 지정됨"
            : string.Empty;
    }

    bool IActionPlanningRule.IsEnergyReservationExempt(
        ActionPlanningSkillContext context)
    {
        return false;
    }

    void IActionPlanningRule.ConfigurePlannedSlot(
        ActionPlanningSkillContext context,
        ActionSlot slot)
    {
        if (slot == null || context.Owner != owner)
            return;

        slot.UseCharacterRerollResource =
            IsSenseEligibleAction(context.Skill) && AutoUseSense;

        if (IsHwanhyeongSkill(context.Skill?.Definition?.SkillId))
        {
            slot.PlanningChoiceId = context.ChoiceId;
            if (string.IsNullOrWhiteSpace(slot.PlanningChoiceId))
            {
                foreach (YujinWeaponType candidate in System.Enum.GetValues(typeof(YujinWeaponType)))
                {
                    if (candidate == currentWeapon) continue;
                    slot.PlanningChoiceId = candidate.ToString();
                    break;
                }
            }
        }
    }

    void IActionPlanningRule.RestorePlanningState(
        ActionSlot slot)
    {
        if (slot == null ||
            slot.Owner != owner ||
            !IsSenseEligibleAction(slot.Skill))
        {
            return;
        }

        AutoUseSense =
            slot.UseCharacterRerollResource;
    }

    private ActionSlot FindPlannedHwanhyeongSlot(
        IReadOnlyList<ActionSlot> plannedSlots)
    {
        if (plannedSlots == null)
            return null;

        foreach (ActionSlot slot in plannedSlots)
        {
            if (slot?.Owner != owner ||
                slot.Skill == null ||
                slot.Phase != ActionPhase.FORESIGHT)
            {
                continue;
            }

            if (IsHwanhyeongSkill(slot.Skill.Definition?.SkillId))
            {
                return slot;
            }
        }

        return null;
    }

    private static bool IsSamePart(
        BodyPart first,
        BodyPart second)
    {
        if (first == null || second == null)
            return first == null && second == null;

        return first.HasSameIdentity(second);
    }

    /// <summary>
    /// Character Validator와 결정론적 테스트에서만 사용하는 비용 없는 전환.
    /// 검증용 상태 세팅은 실제 WeaponChanged 이벤트를 발생시키지 않아
    /// 환형 완료 패시브 같은 런타임 반응을 오염시키지 않는다.
    /// 실전 환형은 도사림 스킬 실행에서 QueueWeaponSwitchFromPreparation을 사용한다.
    /// </summary>
    public void SetWeaponForVerification(
        YujinWeaponType weapon)
    {
        hasPendingWeapon = false;
        pendingWeaponEnergyPaid = 0;
        weaponSwitchUsedThisTurn = false;
        SetWeapon(
            weapon,
            notifyChanged: false);
    }

    private void SetWeapon(
        YujinWeaponType weapon,
        bool notifyChanged = true)
    {
        if (weapon == currentWeapon)
            return;

        YujinWeaponType previous =
            currentWeapon;

        currentWeapon = weapon;

        if (notifyChanged)
        {
            WeaponChanged?.Invoke(
                previous,
                currentWeapon);
        }
    }

    /// <summary>
    /// 환형 도사림 SkillId를 실제 전환 대상 무기로 해석한다.
    /// UI, AI, 실행 경로가 동일한 매핑을 사용하도록 한 곳에 모은다.
    /// </summary>
    public static bool TryGetHwanhyeongWeapon(
        string skillId,
        out YujinWeaponType weapon)
    {
        switch (skillId)
        {
            case YujinSkillIds.HwanhyeongBaeku:
                weapon = YujinWeaponType.Baeku;
                return true;

            case YujinSkillIds.HwanhyeongJeokseol:
                weapon = YujinWeaponType.Jeokseol;
                return true;

            case YujinSkillIds.HwanhyeongNakil:
                weapon = YujinWeaponType.Nakil;
                return true;

            default:
                weapon = default;
                return false;
        }
    }

    /// <summary>
    /// 환형은 일반 도사림과 달리 현재 무기/예약/턴당 1회 조건을 가진다.
    /// 스킬 카드 단계에서 미리 거절해 빛을 소비한 뒤 아무 일도 일어나지 않는
    /// 상태를 방지한다.
    /// </summary>
    public override bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        if (skill == null)
            return false;

        string skillId =
            skill.Definition?.SkillId;

        if (!IsHwanhyeongSkill(skillId))
            return true;

        // 실제 계획 제약은 IActionPlanningRule이 담당한다.
        // 해결 단계에서는 PlanningEffectCommitted 슬롯의 비용 소비/종료 처리를 막으면 안 된다.
        return true;
    }

    public static bool IsSenseEligibleSkill(
        string skillId)
    {
        // Legacy callers can still ask by ID, but the runtime contract is
        // action-category based: every NormalAttack/Duel slot may reserve Sense.
        return !string.IsNullOrWhiteSpace(skillId);
    }

    public static bool IsSenseEligibleAction(
        Skill skill)
    {
        return skill != null &&
               (skill.ActionType == ActionType.NormalAttack ||
                skill.ActionType == ActionType.Duel);
    }

    int IExchangeContinuationRule.ModifyOpponentRemainingRollCount(
        BattleAction winnerAction,
        BattleAction opponentAction,
        int currentRemainingRollCount)
    {
        return winnerAction?.Owner == owner &&
               CurrentWeapon == YujinWeaponType.Nakil
            ? 0
            : Mathf.Max(0, currentRemainingRollCount);
    }

    bool IExchangePreResolutionRule.TryCancelPairedExchange(
        BattleAction ownAction,
        BattleAction opponentAction,
        int exchangeIndex,
        RollResult opponentRoll,
        out string reason)
    {
        reason = string.Empty;

        if (ownAction?.Owner != owner ||
            ownAction.ActionType != ActionType.Duel ||
            opponentAction?.ActionType != ActionType.Duel ||
            sense <= 0 ||
            PeekDecisionProvider == null)
        {
            return false;
        }

        YujinPeekDecision decision =
            PeekDecisionProvider.Decide(
                new YujinPeekDecisionContext(
                    owner as Yujin,
                    ownAction,
                    opponentAction,
                    exchangeIndex,
                    opponentRoll?.Clone()));

        if (decision != YujinPeekDecision.Fold)
            return false;

        sense--;
        reason = "Yujin Sense Peek/Fold";
        return true;
    }

    int IAdditionalStaggerDamageProvider.GetAdditionalStaggerDamage(
        BattleAction action,
        Character target)
    {
        if (action?.Owner != owner || target == null)
            return 0;

        return Mathf.Max(
            0,
            CurrentWeaponProfile.AdditionalStaggerDamagePerHit);
    }

    public int GetMark(
        BodyPart part)
    {
        return part?.Owner != null
            ? GetMark(part.Owner, part)
            : 0;
    }

    public int GetMark(
        Character target,
        BodyPart part = null)
    {
        CombatStatusAnchor anchor =
            CombatStatusAnchor.Resolve(target, part);

        return anchor.IsValid &&
               marks.TryGetValue(anchor, out int value)
            ? value
            : 0;
    }

    public void GrantMark(
        Character target,
        BodyPart part,
        int amount,
        BattleAction sourceAction = null)
    {
        AddMark(
            CombatStatusAnchor.Resolve(target, part),
            amount,
            sourceAction);
    }

    public int GetSkillPower(
        Skill skill,
        bool front)
    {
        if (skill == null)
            return 0;

        int power =
            YujinWeapons.ResolveCoinPower(
                CurrentWeapon,
                front,
                skill.EnergyCost);

        if (!front && sentencingActive)
            power += 2;

        // 0916 잔혹한 마무리: 강화 성장 대상은 무기별로 다르다.
        // 백우 U1/U2 = 위력 +1/+1, 적설 U1 = 위력 +1, 낙일 = 추가타 상한만 성장.
        if (skill.Definition != null &&
            string.Equals(skill.Definition.SkillId, YujinSkillIds.BrutalFinish, StringComparison.OrdinalIgnoreCase))
        {
            int level = battleContext?.SkillUpgrades?.GetLevel(skill.Definition) ?? 0;
            if (currentWeapon == YujinWeaponType.Baeku)
                power += Mathf.Clamp(level, 0, 2);
            else if (currentWeapon == YujinWeaponType.Jeokseol && level >= 1)
                power += 1;
        }

        power += hwanhyeongAttackBonus;
        return power;
    }

    /// <summary>
    /// Legacy verification helper. 실제 런타임은 Skill.EnergyCost를 전달하는 overload를 사용한다.
    /// 구 ID 2종은 현재 자산의 기본 비용(평타0/결투1)만 반영한다.
    /// </summary>
    public int GetSkillPower(
        string skillId,
        bool front)
    {
        int cost =
            skillId == YujinSkillIds.Inscription ||
            skillId == YujinSkillIds.Pursuit
                ? 1
                : 0;

        int power =
            YujinWeapons.ResolveCoinPower(
                CurrentWeapon,
                front,
                cost);

        if (!front && sentencingActive)
            power += 2;

        power += hwanhyeongAttackBonus;
        return power;
    }

    public bool TryConsumeForcedFront()
    {
        if (forcedFrontCharges <= 0)
            return false;

        forcedFrontCharges--;
        return true;
    }

    public override bool TryRequestExchangeReroll(
        ExchangeRerollContext context)
    {
        BattleAction action =
            context?.Action;

        if (action?.Owner != owner ||
            action.LastRollResult == null)
        {
            return false;
        }

        string key =
            $"{action.ActionId}:{context.ExchangeIndex}";

        // 재심은 뒷면에만 무료 1회 재굴림을 제공한다.
        if (!action.LastRollResult.IsCritical &&
            retrialActive &&
            freeRetrialRerolls.Add(key))
        {
            return true;
        }

        bool senseEligible =
            IsSenseEligibleAction(action.Skill);

        // 선택 시점의 결정을 ActionSlot에 고정한다.
        bool useSense =
            action.Slot?.UseCharacterRerollResource == true;

        if (!senseEligible ||
            !useSense ||
            context.IsWinning ||
            sense <= 0)
        {
            return false;
        }

        sense--;
        return true;
    }

    public void ExecuteSkill(
        BattleAction action)
    {
        if (action?.Owner != owner ||
            action.Skill?.Definition == null)
        {
            return;
        }

        string skillId =
            action.Skill.Definition.SkillId;

        if (IsHwanhyeongSkill(skillId))
        {
            // P0 D-03: 계획 시 보너스/예약을 이미 확정한다. 해결 단계에서는 자원 소비 후 중복 적용하지 않는다.
            if (action.Slot?.PlanningEffectCommitted == true)
                return;

            Debug.LogWarning("[유진][환형] Planning commit 없이 실행되었습니다. 환형은 계획 단계의 무기 선택 UI를 통해 사용해야 합니다.");
            return;
        }

        switch (skillId)
        {
            case YujinSkillIds.Capture:
                RegisterPlanningToggle(
                    action,
                    capturePlanningActions,
                    () => captureActive = true,
                    () => captureActive =
                        capturePlanningActions.Count > 0);
                break;

            case YujinSkillIds.Sentencing:
                RegisterPlanningToggle(
                    action,
                    sentencingPlanningActions,
                    () => sentencingActive = true,
                    () => sentencingActive =
                        sentencingPlanningActions.Count > 0);
                break;

            case YujinSkillIds.Brand:
                ActivateBrand(action);
                break;

            case YujinSkillIds.JointLiability:
                forcedFrontCharges++;
                jointLiabilityGranted = 1;
                break;

            case YujinSkillIds.Retrial:
                retrialActive = true;
                break;
        }
    }

    private static void RegisterPlanningToggle(
        BattleAction action,
        HashSet<long> activeActions,
        System.Action activate,
        System.Action refresh)
    {
        if (activeActions == null)
            return;

        long actionId =
            action?.ActionId ?? 0;

        if (actionId > 0)
            activeActions.Add(actionId);

        activate?.Invoke();

        if (action?.Slot?.PlanningUndo == null)
            return;

        action.Slot.PlanningUndo.Record(
            () =>
            {
                if (actionId > 0)
                    activeActions.Remove(actionId);

                refresh?.Invoke();
            });
    }

    private void OnTurnStart(int turn)
    {
        capturePlanningActions.Clear();
        sentencingPlanningActions.Clear();

        weaponChangedThisTurn = false;

        if (hasPendingWeapon)
        {
            YujinWeaponType previous = currentWeapon;
            YujinWeaponType next = pendingWeapon;
            hasPendingWeapon = false;
            pendingWeaponEnergyPaid = 0;
            SetWeapon(next);
            weaponChangedThisTurn = previous != currentWeapon;

            Debug.Log(
                $"[유진][환형] {previous} -> {currentWeapon} 전환 완료 / " +
                $"Turn={turn}");
        }

        weaponSwitchUsedThisTurn = false;
        AddSense(1);
    }

    private void OnTurnEnd(int turn)
    {
        weaponChangedThisTurn = false;
        ClearTurnBuffs();
        freeRetrialRerolls.Clear();
        delayedTriggers.AdvanceTurn();
    }

    private void OnBattleEnded()
    {
        sense = 0;
        marks.Clear();
        delayedTriggers.Clear();
        freeRetrialRerolls.Clear();
        hasPendingWeapon = false;
        pendingWeaponEnergyPaid = 0;
        ClearTurnBuffs();
    }

    private void ClearTurnBuffs()
    {
        capturePlanningActions.Clear();
        sentencingPlanningActions.Clear();
        captureActive = false;
        sentencingActive = false;
        brandActive = false;
        retrialActive = false;
        forcedFrontCharges = 0;
        jointLiabilityGranted = 0;
        hwanhyeongAttackBonus = 0;
        hwanhyeongDefenseBonus = 0;
    }

    private void OnExchangeResolved(
        ClashExchangeResult exchange)
    {
        if (exchange == null ||
            exchange.WasCancelled ||
            exchange.IsTie)
        {
            return;
        }

        BattleAction action =
            exchange.FirstAction?.Owner == owner
                ? exchange.FirstAction
                : exchange.SecondAction?.Owner == owner
                    ? exchange.SecondAction
                    : null;

        BattleAction opponent =
            action == exchange.FirstAction
                ? exchange.SecondAction
                : exchange.FirstAction;

        if (action == null ||
            exchange.WinnerAction != action ||
            action.CurrentRollType != CombatRollType.Attack)
        {
            return;
        }

        string skillId =
            action.Skill?.Definition?.SkillId;

        List<CombatStatusAnchor> hitAnchors =
            CollectHitAnchors(
                action,
                exchange);

        if (skillId == YujinSkillIds.Inspection)
        {
            AddMarkToAnchors(
                hitAnchors,
                CurrentWeaponProfile.NormalMarkAmount,
                action);
        }
        else if (skillId == YujinSkillIds.Breakfast)
        {
            AddSense(1);
        }
        else if (skillId == YujinSkillIds.Inscription &&
                 opponent?.ActionType == ActionType.Duel)
        {
            AddMarkToAnchors(
                hitAnchors,
                CurrentWeaponProfile.DuelMarkAmount,
                action);
        }

        if (brandActive)
        {
            AddMarkToAnchors(
                hitAnchors,
                3,
                action);
        }

        TryExecuteNakil(
            action,
            hitAnchors);

        if (skillId == YujinSkillIds.Pursuit &&
            opponent?.ActionType == ActionType.Duel &&
            !exchange.IsOneSided)
        {
            ResolvePursuitExtraCoins(
                action);
        }
    }

    private List<CombatStatusAnchor> CollectHitAnchors(
        BattleAction action,
        ClashExchangeResult exchange)
    {
        List<CombatStatusAnchor> result =
            new List<CombatStatusAnchor>();

        AddUniqueAnchor(
            result,
            action?.Target,
            action?.TargetPart);

        if (exchange?.SecondaryDamageContexts != null)
        {
            foreach (DamageContext context
                     in exchange.SecondaryDamageContexts)
            {
                AddUniqueAnchor(
                    result,
                    context?.Target,
                    context?.TargetPart);
            }
        }

        return result;
    }

    private static void AddUniqueAnchor(
        ICollection<CombatStatusAnchor> anchors,
        Character target,
        BodyPart part)
    {
        if (anchors == null || target == null)
            return;

        CombatStatusAnchor anchor =
            CombatStatusAnchor.Resolve(target, part);

        if (anchor.IsValid && !anchors.Contains(anchor))
            anchors.Add(anchor);
    }

    private void AddMarkToAnchors(
        IEnumerable<CombatStatusAnchor> anchors,
        int amount,
        BattleAction sourceAction)
    {
        if (anchors == null)
            return;

        foreach (CombatStatusAnchor anchor in anchors)
            AddMark(anchor, amount, sourceAction);
    }

    private void AddMark(
        CombatStatusAnchor anchor,
        int amount,
        BattleAction sourceAction)
    {
        if (!anchor.IsValid ||
            anchor.IsBroken ||
            amount <= 0)
        {
            return;
        }

        if (HasDesignation(anchor))
            amount += 2;

        int value =
            GetMark(anchor.Character, anchor.Part) +
            amount;

        if (value < MarkIgnitionThreshold)
        {
            marks[anchor] = value;
            return;
        }

        // 오버플로는 이월하지 않는다.
        marks[anchor] = 0;

        IgniteMark(
            anchor,
            sourceAction);

        TriggerJointLiability();
    }

    public bool HasAnyMarkAtLeast(int amount)
    {
        int required = Mathf.Max(0, amount);
        foreach (KeyValuePair<CombatStatusAnchor, int> pair in marks)
        {
            if (pair.Key.IsValid && !pair.Key.IsBroken && pair.Value >= required)
                return true;
        }
        return false;
    }

    public bool TryConsumeMark(Character target, BodyPart part, int amount)
    {
        int required = Mathf.Max(0, amount);
        if (required == 0)
            return true;

        CombatStatusAnchor anchor = CombatStatusAnchor.Resolve(target, part);
        if (!anchor.IsValid || !marks.TryGetValue(anchor, out int current) || current < required)
            return false;

        int remaining = current - required;
        if (remaining <= 0) marks.Remove(anchor);
        else marks[anchor] = remaining;
        return true;
    }

    /// <summary>
    /// 준비행동은 적을 조준하지 않는 정본 제약이 있어, 표식 소모 준비행동은
    /// 현재 가장 많이 쌓인 유효 anchor에서 소모한다. 동률은 Dictionary 순서로 고정된다.
    /// TEMP_BALANCE_V1 해석이며 향후 표식 소모 대상 UI가 확정되면 교체한다.
    /// </summary>
    public bool TryConsumeAnyMarkForPreparation(int amount)
    {
        int required = Mathf.Max(0, amount);
        CombatStatusAnchor selected = default;
        int best = -1;
        foreach (KeyValuePair<CombatStatusAnchor, int> pair in marks)
        {
            if (!pair.Key.IsValid || pair.Key.IsBroken || pair.Value < required)
                continue;
            if (pair.Value > best)
            {
                selected = pair.Key;
                best = pair.Value;
            }
        }
        return best >= required && TryConsumeMark(selected.Character, selected.Part, required);
    }

    public bool TrySpendSense(int amount)
    {
        int cost = Mathf.Max(0, amount);
        if (sense < cost)
            return false;
        sense -= cost;
        return true;
    }

    public void GrantForcedFrontCharges(int amount)
    {
        forcedFrontCharges += Mathf.Max(0, amount);
    }

    public int GetAceInTheHoleSenseCost() => currentWeapon switch
    {
        YujinWeaponType.Baeku => 3,
        YujinWeaponType.Jeokseol => 2,
        YujinWeaponType.Nakil => 2,
        _ => 2
    };

    private static bool HasDesignation(CombatStatusAnchor anchor)
    {
        if (!anchor.IsValid)
            return false;

        if (anchor.Part?.StatusEffects != null)
        {
            foreach (StatusEffect effect in anchor.Part.StatusEffects)
                if (effect is YujinDesignationStatus) return true;
        }

        if (anchor.Character?.StatusEffects != null)
        {
            foreach (StatusEffect effect in anchor.Character.StatusEffects)
                if (effect is YujinDesignationStatus) return true;
        }

        return false;
    }

    public long RegisterMarkTrap(
        Character target,
        BodyPart part,
        int fixedDamage,
        BattleAction sourceAction = null)
    {
        CombatStatusAnchor anchor = CombatStatusAnchor.Resolve(target, part);
        return delayedTriggers.Schedule(
            anchor,
            YujinDelayedTriggerKeys.MarkIgnited,
            durationTurns: 0,
            autoTriggerAfterTurns: 0,
            payload: new YujinMarkRiderPayload(
                YujinMarkRiderType.Trap,
                Mathf.Max(0, fixedDamage)),
            sourceAction: sourceAction);
    }

    public long RegisterMarkDeadline(
        Character target,
        BodyPart part,
        int durationTurns,
        int ignitionMultiplier,
        BattleAction sourceAction = null)
    {
        CombatStatusAnchor anchor = CombatStatusAnchor.Resolve(target, part);
        return delayedTriggers.Schedule(
            anchor,
            YujinDelayedTriggerKeys.MarkIgnited,
            durationTurns: Mathf.Max(1, durationTurns),
            autoTriggerAfterTurns: 0,
            payload: new YujinMarkRiderPayload(
                YujinMarkRiderType.Deadline,
                Mathf.Max(1, ignitionMultiplier)),
            sourceAction: sourceAction);
    }

    private void IgniteMark(
        CombatStatusAnchor anchor,
        BattleAction sourceAction)
    {
        Character target = anchor.Character;
        if (target == null)
            return;

        IReadOnlyList<DelayedEffectTriggerEntry> riders =
            delayedTriggers.ConsumeTriggered(
                anchor,
                YujinDelayedTriggerKeys.MarkIgnited);

        int ignitionMultiplier = 1;
        if (riders != null)
        {
            foreach (DelayedEffectTriggerEntry rider in riders)
            {
                if (rider?.Payload is YujinMarkRiderPayload payload &&
                    payload.Type == YujinMarkRiderType.Deadline)
                {
                    ignitionMultiplier = Mathf.Max(
                        ignitionMultiplier,
                        payload.Value);
                }
            }
        }

        YujinWeaponProfile profile = CurrentWeaponProfile;

        switch (profile.MarkIgnition)
        {
            case YujinMarkIgnitionType.MomentumPush:
                battleContext?.ResolveMomentumManager()
                    ?.ApplySkillShift(
                        owner,
                        50 * ignitionMultiplier);
                break;

            case YujinMarkIgnitionType.Seal:
                ApplySeal(
                    anchor,
                    Mathf.Max(1, ignitionMultiplier),
                    sourceAction);
                break;

            case YujinMarkIgnitionType.Weaken:
                if (anchor.IsPartAnchor)
                {
                    target.WeakenPart(
                        anchor.Part,
                        owner,
                        sourceAction);

                    WeakenAdditionalParts(
                        target,
                        anchor.Part,
                        ignitionMultiplier - 1,
                        sourceAction);
                }
                break;
        }

        if (riders == null)
            return;

        foreach (DelayedEffectTriggerEntry rider in riders)
        {
            if (rider?.Payload is not YujinMarkRiderPayload payload ||
                payload.Type != YujinMarkRiderType.Trap ||
                payload.Value <= 0)
            {
                continue;
            }

            ApplyMarkTrapDamage(
                anchor,
                payload.Value,
                rider.SourceAction ?? sourceAction);
        }
    }

    private void ApplySeal(
        CombatStatusAnchor anchor,
        int turns,
        BattleAction sourceAction)
    {
        Character target = anchor.Character;
        if (target == null)
            return;

        StatusEffect seal = new SealedPartStatus(Mathf.Max(1, turns));

        if (anchor.IsPartAnchor)
        {
            battleContext?.EffectResolver
                ?.ApplyBodyPartStatus(
                    EffectRequest.BodyPartStatus(
                        owner,
                        target,
                        anchor.Part,
                        seal,
                        sourceAction,
                        sourceAction?.CurrentRollIndex ?? -1));
        }
        else
        {
            battleContext?.EffectResolver
                ?.ApplyCharacterStatus(
                    EffectRequest.CharacterStatus(
                        owner,
                        target,
                        seal,
                        sourceAction,
                        sourceAction?.CurrentRollIndex ?? -1));
        }
    }

    private void WeakenAdditionalParts(
        Character target,
        BodyPart primary,
        int count,
        BattleAction sourceAction)
    {
        if (target?.BodyParts == null || count <= 0)
            return;

        foreach (BodyPart part in target.BodyParts)
        {
            if (count <= 0)
                break;

            if (part == null ||
                part == primary ||
                part.IsBroken ||
                part.IsWeakened)
            {
                continue;
            }

            target.WeakenPart(part, owner, sourceAction);
            count--;
        }
    }

    private void ApplyMarkTrapDamage(
        CombatStatusAnchor anchor,
        int damage,
        BattleAction sourceAction)
    {
        Character target = anchor.Character;
        if (target == null || target.IsDead || damage <= 0)
            return;

        DamageRequest request = DamageRequest.Custom(
            anchor.IsPartAnchor
                ? DamageType.SkillPart
                : DamageType.Direct,
            owner,
            target,
            anchor.Part,
            damage,
            1f,
            canBreakPart: false,
            applyMomentum: false,
            applyGuard: true,
            sourceAction: sourceAction);

        battleContext?.ResolveDamageManager()
            ?.ApplyDamageContext(request);
    }

    private void TryExecuteNakil(
        BattleAction action,
        IEnumerable<CombatStatusAnchor> hitAnchors)
    {
        if (!CurrentWeaponProfile.CanExecute ||
            action?.LastRollResult?.IsCritical != true ||
            hitAnchors == null)
        {
            return;
        }

        foreach (CombatStatusAnchor anchor in hitAnchors)
        {
            if (!anchor.IsPartAnchor ||
                anchor.Part?.IsWeakened != true ||
                anchor.Part.IsBroken)
            {
                continue;
            }

            anchor.Character?.ForceBreakPart(
                anchor.Part,
                owner,
                action);

            TriggerJointLiability();
        }
    }

    private void ResolvePursuitExtraCoins(
        BattleAction action)
    {
        ResolvePursuitExtraCoinsCore(
            action,
            () => UnityEngine.Random.value < CurrentFrontChance);
    }

    /// <summary>
    /// Character Validator 전용 결정론적 진입점.
    /// 실제 전투 로직과 동일한 추가 코인 처리 경로를 사용하되
    /// 앞/뒷면 결과만 외부 시퀀스로 고정한다.
    /// </summary>
    public int ResolvePursuitExtraCoinsForVerification(
        BattleAction action,
        IReadOnlyList<bool> frontSequence)
    {
        int index = 0;

        return ResolvePursuitExtraCoinsCore(
            action,
            () =>
            {
                bool front =
                    frontSequence != null &&
                    index < frontSequence.Count &&
                    frontSequence[index];

                index++;
                return front;
            });
    }

    private int ResolvePursuitExtraCoinsCore(
        BattleAction action,
        Func<bool> resolveFront)
    {
        if (action?.Owner != owner ||
            action.Target == null ||
            resolveFront == null)
        {
            return 0;
        }

        int upgradeLevel =
            action.Skill?.Definition != null
                ? battleContext?.SkillUpgrades?.GetLevel(action.Skill.Definition) ?? 0
                : 0;

        // 0916 잔혹한 마무리 추가타 상한:
        // 백우 1/1/1, 적설 1/1/2, 낙일 1/3/5 (기본/U1/U2).
        int limit = currentWeapon switch
        {
            YujinWeaponType.Jeokseol => upgradeLevel >= 2 ? 2 : 1,
            YujinWeaponType.Nakil => upgradeLevel >= 2 ? 5 : upgradeLevel >= 1 ? 3 : 1,
            _ => 1
        };

        int appliedHits = 0;

        for (int i = 0;
             i < limit && sense > 0;
             i++)
        {
            sense--;

            bool front = resolveFront();

            if (!front)
                break;

            int power =
                GetSkillPower(
                    action.Skill,
                    true);

            DamageRequest request =
                DamageRequest.Custom(
                    action.TargetPart == null
                        ? DamageType.Direct
                        : DamageType.SkillPart,
                    owner,
                    action.Target,
                    action.TargetPart,
                    power,
                    1f,
                    canBreakPart: false,
                    applyMomentum: false,
                    applyGuard: true,
                    sourceAction: action);

            request.WasCritical = true;

            DamageContext result =
                battleContext?.ResolveDamageManager()
                    ?.ApplyDamageContext(request);

            if (result != null &&
                result.GetDisplayDamage() > 0)
            {
                appliedHits++;
            }

            if (CurrentWeapon == YujinWeaponType.Nakil &&
                action.TargetPart?.IsWeakened == true &&
                action.TargetPart.IsBroken == false)
            {
                action.Target.ForceBreakPart(
                    action.TargetPart,
                    owner,
                    action);

                TriggerJointLiability();
            }
        }

        return appliedHits;
    }

    private void ActivateBrand(
        BattleAction action)
    {
        brandActive = true;

        List<CombatStatusAnchor> targets =
            new List<CombatStatusAnchor>();

        AddUniqueAnchor(
            targets,
            action.Target,
            action.TargetPart);

        if (CurrentWeapon ==
                YujinWeaponType.Jeokseol &&
            action.Slot?.SecondaryTargetPart != null)
        {
            BodyPart secondary =
                action.Slot.SecondaryTargetPart;

            AddUniqueAnchor(
                targets,
                secondary.Owner,
                secondary);
        }

        AddMarkToAnchors(
            targets,
            CurrentWeaponProfile.CriticalValue,
            action);
    }

    private void OnBodyPartBreakResolved(
        BodyPartBreakEventContext context)
    {
        if (context?.Target == null || context.Part == null)
            return;

        // Y-07 common cleanup: a rider/mark anchored to a destroyed part cannot fire later.
        RemoveAnchoredState(context.Target, context.Part);

        // 살수의 감 재충전은 적 부위 파괴만 인정한다.
        // 자신의 부위 파괴는 명시적으로 제외한다.
        if (context.Target == owner)
            return;

        AddSense(1);
        TriggerJointLiability();
    }

    private void OnKillResolved(
        KillEventContext context)
    {
        if (context?.Victim == null)
            return;

        // Y-07 common cleanup: dead targets cannot retain delayed riders or marks.
        RemoveAnchoredState(context.Victim);

        if (context.Killer != owner)
            return;

        AddSense(1);
        TriggerJointLiability();
    }

    private void RemoveAnchoredState(
        Character target,
        BodyPart part = null)
    {
        if (target == null)
            return;

        List<CombatStatusAnchor> toRemove = new();
        foreach (CombatStatusAnchor anchor in marks.Keys)
        {
            if (!ReferenceEquals(anchor.Character, target))
                continue;

            if (part != null && !ReferenceEquals(anchor.Part, part))
                continue;

            toRemove.Add(anchor);
        }

        foreach (CombatStatusAnchor anchor in toRemove)
            marks.Remove(anchor);

        if (part == null)
            delayedTriggers.RemoveForCharacter(target);
        else
            delayedTriggers.RemoveForPart(target, part);
    }

    private void TriggerJointLiability()
    {
        if (jointLiabilityGranted <= 0 ||
            jointLiabilityGranted >= 4)
        {
            return;
        }

        forcedFrontCharges++;
        jointLiabilityGranted++;
    }

    /// <summary>
    /// 유진 고유 메커닉/패시브가 살수의 감을 지급하는 공용 진입점.
    /// 외부 패시브가 내부 필드를 직접 만지지 않도록 한다.
    /// </summary>
    public void GrantSense(int amount)
    {
        sense = Mathf.Max(
            0,
            sense + Mathf.Max(0, amount));
    }

    private void AddSense(int amount)
    {
        GrantSense(amount);
    }
}
