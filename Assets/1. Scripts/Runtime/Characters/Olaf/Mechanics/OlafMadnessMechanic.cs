using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 올라프의 혈상, 광기, 만개, 도사림, 위세를 한 곳에서 처리한다.
/// 모든 결투 부가효과는 합 전체가 아니라 OnExchangeResolved의 개별 교환 단위다.
/// </summary>
public sealed class OlafMadnessMechanic : CombatMechanic, ICharacterUniqueGaugeProvider
{
    public const int MaxMadnessValue = 10;

    private int madness;

    // 「표준」의 혈상 폭발은 BattleAction 한 번당 최대 1회만 허용한다.
    private readonly HashSet<BattleAction>
        standardExplosionActions = new();

    public int CurrentMadness => madness;
    public int MaxMadness => MaxMadnessValue;
    public bool IsBlooming => madness >= MaxMadnessValue;

    public string GaugeLabel => "광기";
    public float GaugeNormalized => (float)CurrentMadness / MaxMadnessValue;
    public string GaugeValueText => $"{CurrentMadness}/{MaxMadnessValue}";
    public int GaugeStateVersion => madness;

    public override string MechanicName =>
        "Olaf Blood Wound / Madness";

    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnExchangeResolved += OnExchangeResolved,
            () => battleEvent.OnExchangeResolved -= OnExchangeResolved,
            "OnExchangeResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnBodyPartBreakResolved += OnBodyPartBreakResolved,
            () => battleEvent.OnBodyPartBreakResolved -= OnBodyPartBreakResolved,
            "OnBodyPartBreakResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnActionEnd += OnActionEnd,
            () => battleEvent.OnActionEnd -= OnActionEnd,
            "OnActionEnd");
    }

    public override void OnUnregister()
    {
        madness = 0;
        standardExplosionActions.Clear();
    }

    public override int ModifyRoll(
        BattleAction action,
        int roll)
    {
        if (action?.Owner != owner)
            return roll;

        return roll +
               Mathf.FloorToInt(
                   CurrentMadness / 5f);
    }

    public override int ModifyDamageTaken(
        DamageContext context,
        int damage)
    {
        if (context?.Target != owner)
            return damage;

        return Mathf.Max(
            0,
            damage +
            Mathf.FloorToInt(
                CurrentMadness / 5f));
    }

    public void AddMadness(int amount)
    {
        if (amount <= 0)
            return;

        madness = Mathf.Clamp(
            madness + amount,
            0,
            MaxMadnessValue);
    }

    public void SetMadnessToMax()
    {
        madness = MaxMadnessValue;
    }

    /// <summary>
    /// BattleDebugTuner 호환 API.
    /// 디버그 값은 새 설계의 광기 상한 10을 기준으로 즉시 설정한다.
    /// 패시브 발동이나 로그 같은 부수효과는 발생시키지 않는다.
    /// </summary>
    public void SetMadnessForDebug(
        int value)
    {
        madness = Mathf.Clamp(
            value,
            0,
            MaxMadnessValue);
    }

    /// <summary>
    /// DataDriven OlafPrestigeEffect가 사용하는 호환 API.
    /// 새 TODO형 「터뜨리는 광기」는 ExecuteSkill에서 별도로 처리하므로
    /// 이 메서드는 해당 SkillEffectDefinition이 실제로 연결된 경우에만 사용된다.
    /// </summary>
    public int ConsumeMadnessForPrestigeDamage()
    {
        return ConsumeMadnessForPrestigeDamage(
            null,
            5,
            true);
    }

    /// <summary>
    /// 현재 광기 × 광기당 피해를 반환한다.
    /// consumeMadness가 true일 때만 계산 후 광기를 0으로 만든다.
    /// sourceAction은 기존 호출 시그니처 보존용이며 새 설계에서는 별도 처치 자격을 만들지 않는다.
    /// </summary>
    public int ConsumeMadnessForPrestigeDamage(
        BattleAction sourceAction,
        int damagePerMadness,
        bool consumeMadness)
    {
        int stack = Mathf.Max(0, madness);
        int damage =
            stack *
            Mathf.Max(0, damagePerMadness);

        if (consumeMadness &&
            stack > 0)
        {
            madness = 0;
        }

        return damage;
    }

    public void ExecuteSkill(
        BattleAction action)
    {
        if (action?.Owner != owner ||
            action.Skill?.Definition == null)
        {
            return;
        }

        string id =
            action.Skill.Definition.SkillId;

        switch (id)
        {
            case OlafSkillIds.Crouch:
                if (owner.RuntimeStatus != null)
                    owner.RuntimeStatus.currentBlock = 12;
                break;

            case OlafSkillIds.Glare:
                owner.AddTurnClashPowerBonus(1);
                break;

            case OlafSkillIds.ShowOff:
                WeakenLowestNormalPart();
                AddMadness(2);
                break;

            case OlafSkillIds.BloomingWound:
                ApplyBloomingWound(action);
                break;

            case OlafSkillIds.BurstingMadness:
                ApplyBurstingMadness(action);
                break;

            case OlafSkillIds.BacksToWall:
                owner.GetMechanic<OlafImmortalFuryMechanic>()
                    ?.Activate(action);
                break;
        }
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

        BattleAction myAction =
            exchange.FirstAction?.Owner == owner
                ? exchange.FirstAction
                : exchange.SecondAction?.Owner == owner
                    ? exchange.SecondAction
                    : null;

        BattleAction opponentAction =
            myAction == exchange.FirstAction
                ? exchange.SecondAction
                : exchange.FirstAction;

        if (myAction == null ||
            opponentAction == null ||
            exchange.IsOneSided)
        {
            return;
        }

        bool won =
            exchange.WinnerAction == myAction;

        bool attackWonAndHit =
            won &&
            myAction.CurrentRollType == CombatRollType.Attack &&
            exchange.DamageContext != null;

        if (attackWonAndHit)
        {
            int commonAmount =
                CurrentMadness >= 3
                    ? 2
                    : 1;

            ApplyBleeding(
                myAction.Target,
                myAction.TargetPart,
                ScaleBleeding(commonAmount),
                myAction,
                exchange.ExchangeIndex);
        }

        if (myAction.ActionType != ActionType.Duel ||
            opponentAction.ActionType != ActionType.Duel)
        {
            return;
        }

        string id =
            myAction.Skill?.Definition?.SkillId;

        if (id == OlafSkillIds.Standard)
        {
            if (won)
            {
                ApplyBleeding(
                    myAction.Target,
                    myAction.TargetPart,
                    ScaleBleeding(1),
                    myAction,
                    exchange.ExchangeIndex);

                TryExplodeBleeding(myAction);
            }
            else
            {
                // 최신 설계: 「표준」은 진 교환에서만 광기 +1.
                AddMadness(1);
            }
        }
        else if (id == OlafSkillIds.Rend)
        {
            // 「난도질」의 고유 혈상은 결투 대 결투의 매 교환에 부여한다.
            ApplyBleeding(
                myAction.Target,
                myAction.TargetPart,
                ScaleBleeding(1),
                myAction,
                exchange.ExchangeIndex);

            if (!won)
            {
                // 최신 설계: 「난도질」도 진 교환에서만 광기 +1.
                AddMadness(1);
            }
        }
    }

    private void OnActionEnd(
        BattleAction action)
    {
        if (action != null)
            standardExplosionActions.Remove(action);
    }

    private void OnBodyPartBreakResolved(
        BodyPartBreakEventContext context)
    {
        if (context?.Part == null)
            return;

        AddMadness(2);

        if (!IsBlooming ||
            context.Target == owner ||
            context.Target == null)
        {
            return;
        }

        NormalizeWeakenedParts(2);
    }

    private int ScaleBleeding(int amount)
    {
        int safe = Mathf.Max(0, amount);
        return IsBlooming
            ? safe * 2
            : safe;
    }

    private void ApplyBleeding(
        Character target,
        BodyPart part,
        int amount,
        BattleAction sourceAction = null,
        int sourceExchangeIndex = -1)
    {
        if (target == null ||
            part == null ||
            part.IsBroken ||
            amount <= 0)
        {
            return;
        }

        battleContext?.EffectResolver
            ?.ApplyBodyPartStatus(
                EffectRequest.BodyPartStatus(
                    owner,
                    target,
                    part,
                    new Bleeding(amount),
                    sourceAction,
                    sourceExchangeIndex));
    }

    private bool TryExplodeBleeding(
        BattleAction action)
    {
        if (action == null ||
            standardExplosionActions.Contains(action))
        {
            return false;
        }

        Character target =
            action.Target;

        BodyPart part =
            action.TargetPart;

        Bleeding bleeding =
            target?.GetPartStatus<Bleeding>(part);

        if (bleeding == null ||
            !bleeding.CanExplode ||
            part == null ||
            part.IsBroken)
        {
            return false;
        }

        // 혈상이 실제로 폭발 가능한 순간에만 사용 횟수를 소모한다.
        standardExplosionActions.Add(action);

        int stack =
            bleeding.ConsumeAll();

        target.RemovePartStatus(
            part,
            bleeding,
            StatusEffectRemoveReason.Manual);

        int damage =
            stack * 10;

        int hpBefore =
            Mathf.CeilToInt(part.PartHP);

        DamageRequest request =
            DamageRequest.Custom(
                DamageType.BleedExplosion,
                owner,
                target,
                part,
                damage,
                1f,
                canBreakPart: false,
                applyMomentum: false,
                applyGuard: false,
                sourceAction: action);

        request.ApplyAttackerModifiers = false;
        request.ApplyTargetModifiers = false;

        battleContext?.ResolveDamageManager()
            ?.ApplyDamageContext(request);

        if (damage >= hpBefore &&
            !part.IsBroken)
        {
            target.ForceBreakPart(
                part,
                owner,
                action);
        }

        return true;
    }

    private void ApplyBloomingWound(
        BattleAction action)
    {
        int amount =
            Mathf.Clamp(
                3 + CurrentMadness / 5,
                3,
                5);

        // 위세 자체의 3~5는 이미 만개 값을 포함한 최종량이므로 재배율하지 않는다.
        ApplyBleeding(
            action.Target,
            action.TargetPart,
            amount,
            action,
            action?.CurrentRollIndex ?? -1);
    }

    private void ApplyBurstingMadness(
        BattleAction action)
    {
        AddMadness(2);

        int bleeding =
            action.Target?.GetPartStatus<Bleeding>(
                action.TargetPart)?.Stack ?? 0;

        int damage =
            CurrentMadness * 5 +
            Mathf.Max(0, bleeding);

        if (damage <= 0 ||
            action.Target == null)
        {
            return;
        }

        DamageRequest request =
            DamageRequest.Custom(
                action.TargetPart == null
                    ? DamageType.Direct
                    : DamageType.SkillPart,
                owner,
                action.Target,
                action.TargetPart,
                damage,
                1f,
                canBreakPart: false,
                applyMomentum: false,
                applyGuard: true,
                sourceAction: action);

        battleContext?.ResolveDamageManager()
            ?.ApplyDamageContext(request);
    }

    private void WeakenLowestNormalPart()
    {
        BodyPart selected = null;

        if (owner?.BodyParts == null)
            return;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null ||
                part.IsBroken ||
                part.IsWeakened)
            {
                continue;
            }

            if (selected == null ||
                part.PartHP < selected.PartHP)
            {
                selected = part;
            }
        }

        if (selected != null)
        {
            owner.WeakenPart(
                selected,
                owner,
                null);
        }
    }

    private void NormalizeWeakenedParts(int count)
    {
        if (owner?.BodyParts == null ||
            count <= 0)
        {
            return;
        }

        List<BodyPart> candidates =
            new List<BodyPart>();

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part?.IsWeakened == true)
                candidates.Add(part);
        }

        candidates.Sort(
            (left, right) =>
                left.PartHP.CompareTo(
                    right.PartHP));

        int normalized = 0;

        foreach (BodyPart part in candidates)
        {
            if (normalized >= count)
                break;

            owner.SetBodyPartStateForDebug(
                part,
                Mathf.Max(1f, part.PartHP),
                part.MaxPartHP,
                BodyPartState.Normal,
                clearNonStructuralStatuses: false);

            normalized++;
        }
    }
}