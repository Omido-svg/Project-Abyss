using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 0916 올라프 패시브의 단일 Source of Truth.
/// 광기 단계, 평타 기본 잔효과, 결투 패배 광기, 블로토와 기존 위세 런타임을 처리한다.
/// 카드별 추가 효과는 SkillDefinition/Effect authoring에 남긴다.
/// </summary>
public sealed class OlafMadnessMechanic : CombatMechanic, ICharacterUniqueGaugeProvider
{
    public const int CrouchBlockGain = 12;
    public const int MaxMadnessValue = 8;
    public const int MadnessPerStage = 4;
    public const int PartBreakMadnessGain = 1;

    private int madness;

    // 출혈 폭발 특전은 한 행동당 첫 성공 교환에서만 1회 허용한다.
    private readonly HashSet<BattleAction> bleedingExplosionActions = new();
    // R7-9: one passive application per resolved damage context and newly
    // broken body part. Nothing is stored on a shared ScriptableObject.
    private readonly HashSet<DamageContext> passiveHitContexts=new();
    private readonly HashSet<BodyPartBreakEventContext> passiveCountedBreakEvents=new();

    public int CurrentMadness => madness;
    public int MaxMadness => MaxMadnessValue;
    public bool IsBlooming => madness >= MaxMadnessValue;

    /// <summary>0~3=0, 4~7=1, 8=2.</summary>
    public int MadnessStage => Mathf.Clamp(CurrentMadness / MadnessPerStage, 0, 2);

    public int PassivePowerBonus => MadnessStage;
    public int PassiveIncomingDamageIncrease => MadnessStage;
    public int RedNormalBleedingGain => IsBlooming ? 2 : 1;

    public string GaugeLabel => "광기";
    public float GaugeNormalized => (float)CurrentMadness / MaxMadnessValue;
    public string GaugeValueText => $"{CurrentMadness}/{MaxMadnessValue}";
    public int GaugeStateVersion => madness;

    public override string MechanicName => "Olaf Bleeding / Madness";

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
        bleedingExplosionActions.Clear();
        passiveHitContexts.Clear();
        passiveCountedBreakEvents.Clear();
    }

    public override int ModifyRoll(BattleAction action, int roll)
    {
        if (action?.Owner != owner)
            return roll;

        return roll + PassivePowerBonus;
    }

    public override int ModifyDamageTaken(DamageContext context, int damage)
    {
        if (context?.Target != owner)
            return damage;

        return Mathf.Max(0, damage + PassiveIncomingDamageIncrease);
    }

    public void AddMadness(int amount)
    {
        if (amount <= 0)
            return;

        madness = Mathf.Clamp(madness + amount, 0, MaxMadnessValue);
    }

    public bool TryConsumeMadness(int amount)
    {
        int safe = Mathf.Max(0, amount);
        if (safe <= 0 || madness < safe)
            return false;

        madness -= safe;
        return true;
    }

    public void SetMadnessToMax() => madness = MaxMadnessValue;

    public void SetMadnessForDebug(int value)
    {
        madness = Mathf.Clamp(value, 0, MaxMadnessValue);
    }

    // Legacy EffectDefinition 호환. 새 0916 Berserk 런타임은 ExecuteSkill에서 별도 처리한다.
    public int ConsumeMadnessForPrestigeDamage()
    {
        return ConsumeMadnessForPrestigeDamage(null, 5, true);
    }

    public int ConsumeMadnessForPrestigeDamage(
        BattleAction sourceAction,
        int damagePerMadness,
        bool consumeMadness)
    {
        int stack = Mathf.Max(0, madness);
        int damage = stack * Mathf.Max(0, damagePerMadness);

        if (consumeMadness && stack > 0)
            madness = 0;

        return damage;
    }

    public void ExecuteSkill(BattleAction action)
    {
        if (action?.Owner != owner || action.Skill?.Definition == null)
            return;

        // 0916 §17.1: 강한 도사림은 카드 고유 효과와 별개로 광기 +1을 준다.
        if (action.ActionType == ActionType.Preparation &&
            action.Skill.Definition.PreparationTier == PreparationTier.Strong)
        {
            int madnessBeforePreparation = madness;
            AddMadness(1);
            action.Slot?.PlanningUndo?.Record(
                () => madness = madnessBeforePreparation);
        }

        switch (action.Skill.Definition.SkillId)
        {
            case OlafSkillIds.Crouch:
                owner.AddBlock(CrouchBlockGain);
                action.Slot?.PlanningUndo?.Record(
                    () => owner?.RemoveBlock(CrouchBlockGain));
                break;

            case OlafSkillIds.Glare:
                owner.AddTurnClashPowerBonus(1);
                action.Slot?.PlanningUndo?.Record(
                    () => owner?.AddTurnClashPowerBonus(-1));
                break;

            case OlafSkillIds.Bloto:
                ExecuteBloto(action);
                break;

            case OlafSkillIds.BloomingWound:
                ApplyBloomingWound(action);
                break;

            case OlafSkillIds.BurstingMadness:
                ApplyBurstingMadness(action);
                break;

            case OlafSkillIds.BacksToWall:
                owner.GetMechanic<OlafImmortalFuryMechanic>()?.Activate(action);
                break;
        }
    }

    private void ExecuteBloto(BattleAction action)
    {
        // 1029 Bloto: +2 Madness total. The strong-preparation passive
        // already adds +1, this is Bloto's additional +1 with planning undo.
        int previousMadness=madness;
        AddMadness(1);
        action.Slot?.PlanningUndo?.Record(()=>madness=previousMadness);
        BodyPart weakened = WeakenLowestNormalPart(out float hpBeforeWeaken);
        List<FearRollbackSnapshot> fearSnapshots = ApplyFearToEnemies(action);

        action.Slot?.PlanningUndo?.Record(
            () =>
            {
                if (weakened?.IsWeakened == true)
                {
                    owner?.RestoreTemporaryWeakenedPart(
                        weakened,
                        hpBeforeWeaken);
                }

                RollbackFear(fearSnapshots);
            });
    }

    private void OnExchangeResolved(ClashExchangeResult exchange)
    {
        if (exchange==null||exchange.WasCancelled||
            exchange.IsTie||exchange.IsContentError||owner==null)
            return;

        BattleAction myAction =
            exchange.FirstAction?.Owner == owner
                ? exchange.FirstAction
                : exchange.SecondAction?.Owner == owner
                    ? exchange.SecondAction
                    : null;
        if (myAction == null)
            return;

        BattleAction opponentAction =
            myAction == exchange.FirstAction
                ? exchange.SecondAction
                : exchange.FirstAction;

        // R7-9: the fixed bleeding passive belongs to ACTUAL ATTACK hits,
        // not every nominally won exchange. Include independently resolved
        // secondary weight-target hits, but never manufacture hits from a
        // cancelled/defensive exchange with no applied DamageContext.
        int hitBleeding = IsBlooming ? 2 : 1;
        ApplyPassiveHitBleeding(exchange,myAction,
            exchange.DamageContext,hitBleeding);
        if(exchange.SecondaryDamageContexts!=null)
            foreach(var secondary in exchange.SecondaryDamageContexts)
                if(!object.ReferenceEquals(secondary,exchange.DamageContext))
                    ApplyPassiveHitBleeding(exchange,myAction,
                        secondary,hitBleeding);

        // BLUE NORMAL '승리시' is a real won exchange. A one-sided hit
        // still applies passive Bleeding above, but grants no win-only Madness.
        if(R7FixedPassiveContract20261103.IsOlafBlueNormalWin(exchange,myAction))
            AddMadness(1);

        if(R7FixedPassiveContract20261103.IsOlafDuelLoss(
            exchange,myAction,opponentAction))
            AddMadness(1);

        // The first winning ATTACK Duel exchange may explode Bleeding once
        // if the canonical source carries the explosion privilege.
        if(exchange.IsDuelExchange&&!exchange.IsOneSided&&
           R7FixedPassiveContract20261103.IsOlafAttackHit(
               exchange,myAction,exchange.DamageContext))
            TryExplodeBleeding(myAction);
    }

    private void ApplyPassiveHitBleeding(
        ClashExchangeResult exchange,BattleAction action,
        DamageContext hit,int amount)
    {
        if(!R7FixedPassiveContract20261103.IsOlafAttackHit(
            exchange,action,hit)||!passiveHitContexts.Add(hit))return;
        ApplyBleeding(hit.Target,hit.TargetPart,amount,
            action,exchange.ExchangeIndex);
    }

    private void OnActionEnd(BattleAction action)
    {
        if (action != null)
            bleedingExplosionActions.Remove(action);
    }

    private void OnBodyPartBreakResolved(BodyPartBreakEventContext context)
    {
        if(R7FixedPassiveContract20261103.IsOlafRelevantPartBreak(
            context,owner,battleContext)&&
           passiveCountedBreakEvents.Add(context))
            AddMadness(PartBreakMadnessGain);
    }

    private void ApplyBleeding(
        Character target,
        BodyPart part,
        int amount,
        BattleAction sourceAction = null,
        int sourceExchangeIndex = -1)
    {
        if (target == null || amount <= 0)
            return;

        CombatStatusAnchor anchor = CombatStatusAnchor.Resolve(target, part);
        if (!anchor.IsValid)
            return;

        EffectRequest request = anchor.IsPartAnchor
            ? EffectRequest.BodyPartStatus(
                owner, target, anchor.Part, new Bleeding(amount),
                sourceAction, sourceExchangeIndex)
            : EffectRequest.CharacterStatus(
                owner, target, new Bleeding(amount),
                sourceAction, sourceExchangeIndex);

        if (anchor.IsPartAnchor)
            battleContext?.EffectResolver?.ApplyBodyPartStatus(request);
        else
            battleContext?.EffectResolver?.ApplyCharacterStatus(request);
    }

    private bool TryExplodeBleeding(BattleAction action)
    {
        SkillRulebreakerSettings rule = action?.Skill?.Definition?.Rulebreaker;
        // 1029 §17.1: explosion is a canonical x5, not the stale
        // pre-balance multiplier (currently 0 on both PhaseE duel SOs).
        const int multiplier = 5;

        // 0916 §17.6: 폭발 계수는 미정이다. SO에 값이 들어오기 전에는 실행하지 않는다.
        if (rule?.Enabled != true ||
            !rule.ExplodeBleedingOncePerAction ||
            multiplier <= 0 ||
            action == null ||
            bleedingExplosionActions.Contains(action))
        {
            return false;
        }

        CombatStatusAnchor anchor =
            CombatStatusAnchor.Resolve(action.Target, action.TargetPart);

        Bleeding bleeding = anchor.IsPartAnchor
            ? action.Target?.GetPartStatus<Bleeding>(anchor.Part)
            : action.Target?.GetStatus<Bleeding>();

        if (!anchor.IsValid || bleeding == null || bleeding.Stack <= 0)
            return false;

        bleedingExplosionActions.Add(action);
        int stack = bleeding.ConsumeAll();

        if (anchor.IsPartAnchor)
            action.Target.RemovePartStatus(anchor.Part, bleeding, StatusEffectRemoveReason.Manual);
        else
            action.Target.RemoveStatus(bleeding, StatusEffectRemoveReason.Manual);

        DamageRequest request =
            DamageRequest.Custom(
                DamageType.BleedExplosion,
                owner,
                action.Target,
                anchor.Part,
                stack * multiplier,
                1f,
                canBreakPart: false,
                applyMomentum: false,
                applyGuard: false,
                sourceAction: action);

        request.ApplyAttackerModifiers = false;
        request.ApplyTargetModifiers = false;
        battleContext?.ResolveDamageManager()?.ApplyDamageContext(request);
        return true;
    }

    private void ApplyBloomingWound(BattleAction action)
    {
        ApplyBleeding(
            action.Target,
            action.TargetPart,
            3 + MadnessStage,
            action,
            action?.CurrentRollIndex ?? -1);
    }

    private void ApplyBurstingMadness(BattleAction action)
    {
        AddMadness(2);

        CombatStatusAnchor anchor =
            CombatStatusAnchor.Resolve(action.Target, action.TargetPart);

        int bleeding = anchor.IsPartAnchor
            ? action.Target?.GetPartStatus<Bleeding>(anchor.Part)?.Stack ?? 0
            : action.Target?.GetStatus<Bleeding>()?.Stack ?? 0;

        int damage = CurrentMadness * 5 + Mathf.Max(0, bleeding);
        if (damage <= 0 || action.Target == null)
            return;

        DamageRequest request =
            DamageRequest.Custom(
                action.TargetPart == null ? DamageType.Direct : DamageType.SkillPart,
                owner,
                action.Target,
                action.TargetPart,
                damage,
                1f,
                canBreakPart: false,
                applyMomentum: false,
                applyGuard: true,
                sourceAction: action);

        battleContext?.ResolveDamageManager()?.ApplyDamageContext(request);
    }

    private BodyPart WeakenLowestNormalPart(out float hpBeforeWeaken)
    {
        BodyPart selected = null;
        hpBeforeWeaken = 0f;

        if (owner?.BodyParts == null)
            return null;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null || part.IsBroken || part.IsWeakened)
                continue;

            if (selected == null ||
                part.PartHP < selected.PartHP ||
                (Mathf.Approximately(part.PartHP, selected.PartHP) &&
                 GetBlotoTiePriority(part.Type) < GetBlotoTiePriority(selected.Type)))
            {
                selected = part;
            }
        }

        if (selected != null)
        {
            hpBeforeWeaken = selected.PartHP;
            owner.WeakenPart(selected, owner, null);
        }

        return selected;
    }

    private static int GetBlotoTiePriority(PartType type)
    {
        return type switch
        {
            PartType.LEFT_HAND => 0,
            PartType.RIGHT_HAND => 1,
            PartType.LEGS => 2,
            PartType.HEAD => 3,
            _ => 99
        };
    }

    private List<FearRollbackSnapshot> ApplyFearToEnemies(BattleAction action)
    {
        List<FearRollbackSnapshot> snapshots = new();
        if (battleContext?.Enemies == null)
            return snapshots;

        foreach (Character enemy in battleContext.Enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            OlafFearStatus existing = enemy.GetStatus<OlafFearStatus>();
            snapshots.Add(
                new FearRollbackSnapshot(
                    enemy,
                    existing != null,
                    existing?.Duration ?? 0));

            battleContext.EffectResolver?.ApplyCharacterStatus(
                EffectRequest.CharacterStatus(
                    owner,
                    enemy,
                    new OlafFearStatus(
                        OlafFearStatus.DefaultDuration),
                    action));
        }

        return snapshots;
    }

    private void RollbackFear(List<FearRollbackSnapshot> snapshots)
    {
        if (snapshots == null)
            return;

        foreach (FearRollbackSnapshot snapshot in snapshots)
        {
            Character target = snapshot.Target;
            if (target == null)
                continue;

            OlafFearStatus current = target.GetStatus<OlafFearStatus>();
            if (current != null)
                target.RemoveStatus(current, StatusEffectRemoveReason.Manual);

            if (snapshot.HadFear)
            {
                target.AddStatus(
                    new OlafFearStatus(
                        snapshot.PreviousDuration),
                    owner);
            }
        }
    }

    private readonly struct FearRollbackSnapshot
    {
        public Character Target { get; }
        public bool HadFear { get; }
        public int PreviousDuration { get; }

        public FearRollbackSnapshot(
            Character target,
            bool hadFear,
            int previousDuration)
        {
            Target = target;
            HadFear = hadFear;
            PreviousDuration =
                previousDuration < 0
                    ? StatusEffect.InfiniteDuration
                    : Mathf.Max(
                        0,
                        previousDuration);
        }
    }
}