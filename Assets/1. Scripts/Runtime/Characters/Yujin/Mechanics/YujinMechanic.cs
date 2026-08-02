using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유진의 무기, 살수의 감, 표식, 봉인, 처형, 재굴림, 위세를 통합한다.
/// 표식과 결투 효과는 개별 교환 결과가 공개되는 즉시 처리한다.
/// </summary>
public sealed class YujinMechanic : CombatMechanic
{
    public const int MarkIgnitionThreshold = 44;

    private readonly Dictionary<BodyPart, int>
        marks = new();

    private readonly HashSet<string>
        freeRetrialRerolls = new();

    private YujinWeaponType currentWeapon;
    private int sense;

    private bool captureActive;
    private bool sentencingActive;
    private bool brandActive;
    private bool retrialActive;

    private int forcedFrontCharges;
    private int jointLiabilityGranted;

    /// <summary>
    /// 무기 변경이 실제로 완료된 직후 호출된다.
    /// UI와 자동계획은 이 이벤트 하나를 구독해 표시를 동기화한다.
    /// </summary>
    public event Action<YujinWeaponType, YujinWeaponType> WeaponChanged;

    public bool AutoUseSense { get; set; }

    /// <summary>
    /// 턴당 변경 횟수 제한은 없다. 다만 전투 해석 중에는
    /// 이미 생성된 BattleAction의 굴림 구성이 바뀌지 않도록 잠근다.
    /// </summary>
    public bool IsWeaponSwitchLocked =>
        battleContext?.battleManager?.TurnManager?.IsResolving == true;

    public YujinWeaponType CurrentWeapon =>
        currentWeapon;

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
        ClearTurnBuffs();
    }

    public bool CanSwitchWeapon(
        YujinWeaponType weapon)
    {
        return weapon != currentWeapon &&
               !IsWeaponSwitchLocked;
    }

    public bool TrySwitchWeapon(
        YujinWeaponType weapon)
    {
        if (!CanSwitchWeapon(weapon))
            return false;

        YujinWeaponType previous =
            currentWeapon;

        currentWeapon = weapon;

        WeaponChanged?.Invoke(
            previous,
            currentWeapon);

        return true;
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
            action.LastRollResult == null ||
            action.LastRollResult.IsCritical)
        {
            return false;
        }

        string key =
            $"{action.ActionId}:{context.ExchangeIndex}";

        if (retrialActive &&
            freeRetrialRerolls.Add(key))
        {
            return true;
        }

        string skillId =
            action.Skill?.Definition?.SkillId;

        bool senseEligible =
            skillId == YujinSkillIds.Inscription ||
            skillId == YujinSkillIds.Pursuit;

        bool autoUse =
            action.Slot?.UseCharacterRerollResource == true ||
            AutoUseSense;

        if (!senseEligible ||
            !autoUse ||
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

        switch (action.Skill.Definition.SkillId)
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
                battleContext?.battleManager
                    ?.MomentumManager
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
        int coinCount =
            Mathf.Max(
                1,
                CurrentWeaponProfile.CoinCount);

        int limit =
            Mathf.Max(
                0,
                6 / coinCount - 1);

        for (int i = 0;
             i < limit && sense > 0;
             i++)
        {
            sense--;

            bool front =
                UnityEngine.Random.value < CurrentFrontChance;

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
                    applyDefense: true,
                    applyGuard: true,
                    applyProtection: true,
                    sourceAction: action);

            request.WasCritical = true;

            battleContext?.battleManager?.DamageManager
                ?.ApplyDamageContext(request);

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

    private void AddSense(int amount)
    {
        sense = Mathf.Max(
            0,
            sense + Mathf.Max(0, amount));
    }
}