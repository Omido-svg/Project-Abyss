using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유진의 무기, 살수의 감, 표식, 봉인, 처형, 재굴림, 위세를 통합한다.
/// 표식과 결투 효과는 개별 교환 결과가 공개되는 즉시 처리한다.
/// </summary>
public sealed class YujinMechanic : CombatMechanic, ICharacterUniqueGaugeProvider, IActionPlanningRule
{
    public const int MarkIgnitionThreshold = 44;
    public const int WeaponSwitchEnergyCost = 1;

    private readonly Dictionary<BodyPart, int>
        marks = new();

    private readonly HashSet<string>
        freeRetrialRerolls = new();

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

    public bool HasUsedWeaponSwitchThisTurn =>
        weaponSwitchUsedThisTurn;

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
    }

    public override void OnUnregister()
    {
        marks.Clear();
        freeRetrialRerolls.Clear();
        sense = 0;
        weaponSwitchUsedThisTurn = false;
        hasPendingWeapon = false;
        pendingWeaponEnergyPaid = 0;
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
        if (!CanSwitchWeapon(weapon))
            return false;

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

    string IActionPlanningRule.GetSkillSelectionBlockReason(
        ActionPlanningSkillContext context)
    {
        if (context.Skill == null ||
            context.Owner != owner ||
            !TryGetHwanhyeongWeapon(
                context.Skill.Definition?.SkillId,
                out YujinWeaponType weapon))
        {
            return string.Empty;
        }

        ActionSlot existing =
            FindPlannedHwanhyeongSlot(
                context.PlannedSlots);

        if (existing != null &&
            (!IsSamePart(existing.Part, context.Part) ||
             existing.ActionIndex != context.ActionIndex))
        {
            return "이번 턴 환형 이미 지정됨";
        }

        return GetHwanhyeongSelectionReason(weapon);
    }

    bool IActionPlanningRule.IsEnergyReservationExempt(
        ActionPlanningSkillContext context)
    {
        if (context.Skill == null ||
            context.Owner != owner ||
            !HasPendingWeapon ||
            !TryGetHwanhyeongWeapon(
                context.Skill.Definition?.SkillId,
                out YujinWeaponType weapon))
        {
            return false;
        }

        return CanSelectHwanhyeongWeapon(weapon);
    }

    void IActionPlanningRule.ConfigurePlannedSlot(
        ActionPlanningSkillContext context,
        ActionSlot slot)
    {
        if (slot == null || context.Owner != owner)
            return;

        slot.UseCharacterRerollResource =
            IsSenseEligibleSkill(
                context.Skill?.Definition?.SkillId) &&
            AutoUseSense;
    }

    void IActionPlanningRule.RestorePlanningState(
        ActionSlot slot)
    {
        if (slot == null ||
            slot.Owner != owner ||
            !IsSenseEligibleSkill(
                slot.Skill?.Definition?.SkillId))
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

            if (TryGetHwanhyeongWeapon(
                    slot.Skill.Definition?.SkillId,
                    out _))
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

        return first == second ||
               first.Type == second.Type;
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

        if (!TryGetHwanhyeongWeapon(
                skillId,
                out YujinWeaponType weapon))
        {
            return true;
        }

        return CanSelectHwanhyeongWeapon(weapon);
    }

    public static bool IsSenseEligibleSkill(
        string skillId)
    {
        return skillId == YujinSkillIds.Inscription ||
               skillId == YujinSkillIds.Pursuit;
    }

    public bool RemovesOpponentRemainingRollsOnExchangeWin(
        BattleAction action)
    {
        return action?.Owner == owner &&
               CurrentWeapon == YujinWeaponType.Nakil;
    }

    public int GetMark(
        BodyPart part)
    {
        return part != null &&
               marks.TryGetValue(
                   part,
                   out int value)
            ? value
            : 0;
    }

    public int GetSkillPower(
        string skillId,
        bool front)
    {
        int power;

        if (skillId == YujinSkillIds.Inspection ||
            skillId == YujinSkillIds.Breakfast)
        {
            power = front
                ? CurrentWeapon switch
                {
                    YujinWeaponType.Baeku => 22,
                    YujinWeaponType.Jeokseol => 28,
                    _ => 43
                }
                : skillId == YujinSkillIds.Breakfast
                    ? 16
                    : 14;
        }
        else
        {
            power = front
                ? CurrentWeapon switch
                {
                    YujinWeaponType.Baeku => 23,
                    YujinWeaponType.Jeokseol => 29,
                    _ => 44
                }
                : 15;
        }

        if (!front && sentencingActive)
            power += 2;

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

        string skillId =
            action.Skill?.Definition?.SkillId;

        bool senseEligible =
            IsSenseEligibleSkill(skillId);

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

        if (TryGetHwanhyeongWeapon(
                skillId,
                out YujinWeaponType hwanhyeongWeapon))
        {
            QueueWeaponSwitchFromPreparation(
                hwanhyeongWeapon,
                action.Skill.EnergyCost);
            return;
        }

        switch (skillId)
        {
            case YujinSkillIds.Capture:
                captureActive = true;
                break;

            case YujinSkillIds.Sentencing:
                sentencingActive = true;
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

    private void OnTurnStart(int turn)
    {
        if (hasPendingWeapon)
        {
            YujinWeaponType next = pendingWeapon;
            hasPendingWeapon = false;
            pendingWeaponEnergyPaid = 0;
            SetWeapon(next);
        }

        weaponSwitchUsedThisTurn = false;
        AddSense(1);
    }

    private void OnTurnEnd(int turn)
    {
        ClearTurnBuffs();
        freeRetrialRerolls.Clear();
    }

    private void ClearTurnBuffs()
    {
        captureActive = false;
        sentencingActive = false;
        brandActive = false;
        retrialActive = false;
        forcedFrontCharges = 0;
        jointLiabilityGranted = 0;
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
            action.CurrentRollType != CombatRollType.Attack ||
            exchange.DamageContext == null)
        {
            return;
        }

        string skillId =
            action.Skill?.Definition?.SkillId;

        List<BodyPart> hitParts =
            CollectHitParts(
                action,
                exchange);

        if (skillId == YujinSkillIds.Inspection)
        {
            AddMarkToParts(
                hitParts,
                CurrentWeaponProfile.BaseMarkAmount,
                action);
        }
        else if (skillId == YujinSkillIds.Breakfast)
        {
            AddSense(1);
        }
        else if (skillId == YujinSkillIds.Inscription &&
                 opponent?.ActionType == ActionType.Duel)
        {
            AddMarkToParts(
                hitParts,
                CurrentWeaponProfile.BaseMarkAmount * 2,
                action);
        }

        if (brandActive)
        {
            AddMarkToParts(
                hitParts,
                3,
                action);
        }

        TryExecuteNakil(
            action,
            hitParts);

        if (skillId == YujinSkillIds.Pursuit &&
            opponent?.ActionType == ActionType.Duel &&
            !exchange.IsOneSided)
        {
            ResolvePursuitExtraCoins(
                action);
        }
    }

    private List<BodyPart> CollectHitParts(
        BattleAction action,
        ClashExchangeResult exchange)
    {
        List<BodyPart> result =
            new List<BodyPart>();

        AddUniquePart(
            result,
            action.TargetPart);

        if (exchange.SecondaryDamageContexts != null)
        {
            foreach (DamageContext context
                     in exchange.SecondaryDamageContexts)
            {
                AddUniquePart(
                    result,
                    context?.TargetPart);
            }
        }

        return result;
    }

    private static void AddUniquePart(
        ICollection<BodyPart> parts,
        BodyPart part)
    {
        if (parts != null &&
            part != null &&
            !part.IsBroken &&
            !parts.Contains(part))
        {
            parts.Add(part);
        }
    }

    private void AddMarkToParts(
        IEnumerable<BodyPart> parts,
        int amount,
        BattleAction sourceAction)
    {
        if (parts == null)
            return;

        foreach (BodyPart part in parts)
            AddMark(part, amount, sourceAction);
    }

    private void AddMark(
        BodyPart part,
        int amount,
        BattleAction sourceAction)
    {
        if (part == null ||
            part.IsBroken ||
            amount <= 0)
        {
            return;
        }

        int value =
            GetMark(part) +
            amount;

        if (value < MarkIgnitionThreshold)
        {
            marks[part] = value;
            return;
        }

        // 오버플로는 이월하지 않는다.
        marks[part] = 0;

        IgniteMark(
            part,
            sourceAction);

        TriggerJointLiability();
    }

    private void IgniteMark(
        BodyPart part,
        BattleAction sourceAction)
    {
        Character target =
            part?.Owner;

        if (target == null)
            return;

        switch (CurrentWeapon)
        {
            case YujinWeaponType.Baeku:
                battleContext?.ResolveMomentumManager()
                    ?.ApplySkillShift(
                        owner,
                        10);
                break;

            case YujinWeaponType.Jeokseol:
                battleContext?.EffectResolver
                    ?.ApplyBodyPartStatus(
                        EffectRequest.BodyPartStatus(
                            owner,
                            target,
                            part,
                            new SealedPartStatus(1)));
                break;

            case YujinWeaponType.Nakil:
                target.WeakenPart(
                    part,
                    owner,
                    sourceAction);
                break;
        }
    }

    private void TryExecuteNakil(
        BattleAction action,
        IEnumerable<BodyPart> hitParts)
    {
        if (CurrentWeapon != YujinWeaponType.Nakil ||
            action?.LastRollResult?.IsCritical != true ||
            hitParts == null)
        {
            return;
        }

        foreach (BodyPart part in hitParts)
        {
            if (part?.IsWeakened != true ||
                part.IsBroken)
            {
                continue;
            }

            part.Owner?.ForceBreakPart(
                part,
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

        int coinCount =
            Mathf.Max(
                1,
                CurrentWeaponProfile.CoinCount);

        int limit =
            Mathf.Max(
                0,
                6 / coinCount - 1);

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
                    YujinSkillIds.Pursuit,
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

        List<BodyPart> targets =
            new List<BodyPart>();

        AddUniquePart(
            targets,
            action.TargetPart);

        if (CurrentWeapon ==
                YujinWeaponType.Jeokseol &&
            action.Slot?.SecondaryTargetPart != null)
        {
            AddUniquePart(
                targets,
                action.Slot.SecondaryTargetPart);
        }

        AddMarkToParts(
            targets,
            CurrentWeaponProfile.CriticalValue,
            action);
    }

    private void OnBodyPartBreakResolved(
        BodyPartBreakEventContext context)
    {
        // 살수의 감 재충전은 적 부위 파괴만 인정한다.
        // 자신의 부위 파괴는 명시적으로 제외한다.
        if (context?.Target == null ||
            context.Target == owner)
        {
            return;
        }

        AddSense(1);
        TriggerJointLiability();
    }

    private void OnKillResolved(
        KillEventContext context)
    {
        if (context?.Killer != owner ||
            context.Victim == null)
        {
            return;
        }

        AddSense(1);
        TriggerJointLiability();
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