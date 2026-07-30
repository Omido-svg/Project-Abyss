using System;
using System.Collections.Generic;
using UnityEngine;

public enum PrestigeUsePolicy
{
    None,
    OncePerTurn,
    Unlimited
}

public enum SkillRollReusePolicy
{
    RollEachExchange,
    OncePerAction
}

public enum PreparationTier
{
    Weak = 0,
    Strong = 1
}

public abstract class Skill
{
    public string SkillName { get; protected set; }
    public abstract ActionType ActionType { get; }

    public int BasePower { get; protected set; }
    public SkillResolver Resolver { get; protected set; }

    protected Character owner;
    protected BattleEvent battleEvent;

    private BattleEvent registeredBattleEvent;
    private readonly HashSet<SkillEffectDispatchKey> activeEffectDispatches = new();

    protected virtual SkillDefinition RuntimeDefinition => null;
    public SkillDefinition Definition => RuntimeDefinition;

    public virtual bool CanBreakPart => false;

    public virtual int AttackWeight =>
        RuntimeDefinition?.AttackWeight
            ?.EffectiveWeight ?? 1;

    public virtual float
        SecondaryTargetDamageMultiplier =>
            RuntimeDefinition?.AttackWeight
                ?.SecondaryDamageMultiplier ?? 1f;

    public virtual AttackWeightSecondaryPartMode
        SecondaryAttackWeightPartMode =>
            RuntimeDefinition?.AttackWeight
                ?.SecondaryPartMode ??
            global::AttackWeightSecondaryPartMode
                .RandomValidTargetPoint;

    public virtual bool
        AllowBrokenAttackWeightParts =>
            RuntimeDefinition?.AttackWeight
                ?.AllowBrokenSecondaryParts ?? true;

    public virtual int ExchangeRollCount
    {
        get
        {
            SkillDefinition definition = RuntimeDefinition;

            if (definition != null)
                return definition.EffectiveRollCount;

            int fallback =
                owner?.BattleContext?.Rules?.Clash
                    ?.DefaultExchangeRollCount ?? 3;

            return ActionType == ActionType.NormalAttack ||
                   ActionType == ActionType.Duel
                ? Mathf.Clamp(fallback, 2, 8)
                : 2;
        }
    }

    public virtual SkillRollReusePolicy RollReusePolicy
    {
        get
        {
            SkillDefinition definition = RuntimeDefinition;

            // 친치로는 행동 시작 시 한 번 굴린 값을 해당 행동의 모든 교환에 재사용한다.
            if (definition?.ResolverType == SkillResolverType.Chinchiro)
                return SkillRollReusePolicy.OncePerAction;

            return definition?.RollReusePolicy ??
                   SkillRollReusePolicy.RollEachExchange;
        }
    }

    public virtual PreparationTier PreparationTier =>
        RuntimeDefinition?.PreparationTier ??
        PreparationTier.Weak;

    public virtual ActionPhase DefaultPhase
    {
        get
        {
            SkillDefinition definition = RuntimeDefinition;

            if (ActionType == ActionType.Prestige &&
                definition?.ResolvePrestigeInCombat == true)
            {
                return ActionPhase.COMBAT;
            }

            return ActionType switch
            {
                ActionType.Prestige => ActionPhase.PRETURN,
                ActionType.Preparation => ActionPhase.FORESIGHT,
                _ => ActionPhase.COMBAT
            };
        }
    }

    public virtual bool CanClash
    {
        get
        {
            SkillDefinition definition = RuntimeDefinition;

            if (definition?.OverrideCanClash == true)
                return definition.CanClashValue;

            return ActionType == ActionType.NormalAttack ||
                   ActionType == ActionType.Duel;
        }
    }

    public virtual bool GainPrestige =>
        ActionType == ActionType.Duel;

    public virtual int EnergyCost
    {
        get
        {
            SkillDefinition definition = RuntimeDefinition;

            if (definition?.OverrideEnergyCost == true)
                return Mathf.Max(0, definition.EnergyCost);

            return ActionType switch
            {
                ActionType.Duel => 1,
                ActionType.NormalAttack => 0,
                ActionType.Preparation => 1,
                ActionType.Prestige => 0,
                _ => 0
            };
        }
    }

    public virtual float IgnoreBlock => 0f;

    public virtual PrestigeUsePolicy PrestigeUsePolicy
    {
        get
        {
            SkillDefinition definition = RuntimeDefinition;

            if (definition != null &&
                definition.OverridePrestigeUsePolicy)
            {
                return definition.PrestigeUsePolicy;
            }

            return ActionType == ActionType.Prestige
                ? PrestigeUsePolicy.OncePerTurn
                : PrestigeUsePolicy.None;
        }
    }

    public int UseCountThisTurn =>
        SkillUsageTracker.GetUseCount(
            owner,
            GetUsageIdentity());

    public bool IsFirstUseThisTurn =>
        UseCountThisTurn == 1;

    public virtual bool CanUseByResource(Character character)
    {
        if (character == null)
            return false;

        if (!character.CanAffordEnergy(EnergyCost))
            return false;

        SkillDefinition definition = RuntimeDefinition;

        if (definition != null &&
            definition.OverrideResourceRules)
        {
            if (character.RuntimeStatus == null)
                return false;

            if (definition.RequireFullPrestige)
            {
                if (character.CurrentStatus == null ||
                    character.CurrentStatus.maxPrestige <= 0 ||
                    character.RuntimeStatus.currentPrestige <
                    character.CurrentStatus.maxPrestige)
                {
                    return false;
                }
            }

            if (definition.PrestigeCost > 0 &&
                character.RuntimeStatus.currentPrestige <
                definition.PrestigeCost)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(
                    definition.CustomResourceKey) &&
                definition.CustomResourceCost > 0 &&
                SkillResourceAccess.Get(
                    character,
                    definition.CustomResourceKey) <
                definition.CustomResourceCost)
            {
                return false;
            }

            return true;
        }

        if (ActionType != ActionType.Prestige)
            return true;

        if (character.CurrentStatus == null ||
            character.RuntimeStatus == null)
        {
            return false;
        }

        if (character.CurrentStatus.maxPrestige <= 0)
            return false;

        return character.RuntimeStatus.currentPrestige >=
               character.CurrentStatus.maxPrestige;
    }

    public virtual bool TryConsumeResource(
        Character character,
        BattleAction sourceAction = null)
    {
        if (character == null ||
            !CanUseByResource(character))
        {
            return false;
        }

        // 모든 비용의 충족 여부를 먼저 검증한 뒤 실제 차감한다.
        // 에너지가 부족한 행동은 효과 실행 전에 취소된다.
        if (!character.TryConsumeEnergy(
                EnergyCost,
                sourceAction,
                this))
        {
            return false;
        }

        SkillDefinition definition = RuntimeDefinition;

        if (definition != null &&
            definition.OverrideResourceRules)
        {
            if (character.RuntimeStatus != null)
            {
                if (definition.ConsumeAllPrestige)
                {
                    character.RuntimeStatus.currentPrestige = 0;
                }
                else if (definition.PrestigeCost > 0)
                {
                    character.RuntimeStatus.currentPrestige =
                        Mathf.Max(
                            0,
                            character.RuntimeStatus.currentPrestige -
                            definition.PrestigeCost);
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    definition.CustomResourceKey))
            {
                if (definition.ConsumeAllCustomResource)
                {
                    SkillResourceAccess.Set(
                        character,
                        definition.CustomResourceKey,
                        0);
                }
                else if (definition.CustomResourceCost > 0)
                {
                    SkillResourceAccess.Modify(
                        character,
                        definition.CustomResourceKey,
                        -definition.CustomResourceCost,
                        0,
                        int.MaxValue);
                }
            }

            return true;
        }

        if (ActionType == ActionType.Prestige &&
            character.RuntimeStatus != null)
        {
            character.RuntimeStatus.currentPrestige = 0;

            Debug.Log(
                $"{character.Data?.CharacterName ?? character.name} " +
                "위세 게이지 소모 : 0");
        }

        return true;
    }

    // 기존 호출부 호환. 신규 해결 파이프라인은 TryConsumeResource의 반환값을 확인한다.
    public virtual void ConsumeResource(Character character)
    {
        if (!TryConsumeResource(character))
        {
            Debug.LogWarning(
                $"[Skill Resource] 비용 소비 실패 / " +
                $"Owner={character?.Data?.CharacterName ?? character?.name}, " +
                $"Skill={SkillName}, EnergyCost={EnergyCost}");
        }
    }

    public virtual bool CanAIUse(
        Character character,
        BodyPart part,
        BattleContext context)
    {
        return CanUseByResource(character);
    }

    public virtual int GetMomentumPushBonus(
        BattleAction action) => 0;

    public virtual int GetPrestigeGainBonus(
        BattleAction action) => 0;

    public int MinPower =>
        Resolver == null
            ? BasePower
            : BasePower + Resolver.MinValue;

    public int MaxPower =>
        Resolver == null
            ? BasePower
            : BasePower + Resolver.MaxValue;

    public virtual void Initialize(
        Character character,
        BattleEvent battleEvent)
    {
        UnregisterRuntimeEvents();

        owner = character;
        this.battleEvent = battleEvent;

        RegisterRuntimeEvents();
    }

    public virtual void Register()
    {
    }

    public virtual void Unregister()
    {
        UnregisterRuntimeEvents();
    }

    public virtual int RollRawPower()
    {
        RollResult result = RollPowerResult();
        return result?.FinalPower ?? BasePower;
    }

    public abstract void Execute(BattleAction action);

    public virtual RollResult RollPowerResult()
    {
        SkillResolverType fallbackType =
            RuntimeDefinition?.ResolverType ??
            CharacterRandomDebugOverride
                .InferResolverType(
                    Resolver);

        if (CharacterRandomDebugOverride.TryCreateRoll(
                owner,
                this,
                null,
                fallbackType,
                0,
                out RollResult debugResult))
        {
            return debugResult;
        }

        if (Resolver == null)
        {
            return new RollResult
            {
                BasePower = BasePower,
                RawValue = 0,
                ModifiedValue = 0,
                FinalPower = BasePower
            };
        }

        return Resolver.RollResult(this);
    }

    public SkillRollData GetRollData(int exchangeIndex) =>
        RuntimeDefinition?.GetRollData(exchangeIndex);

    public CombatRollType GetRollType(int exchangeIndex) =>
        GetRollData(exchangeIndex)?.Type ?? CombatRollType.Attack;

    public bool ShouldReuseRollData(int exchangeIndex) =>
        GetRollData(exchangeIndex)?.ReuseValueAcrossAction == true ||
        (GetRollData(exchangeIndex) == null &&
         RollReusePolicy == SkillRollReusePolicy.OncePerAction);

    public virtual RollResult RollPowerResultForExchange(int exchangeIndex)
    {
        SkillRollData data = GetRollData(exchangeIndex);
        if (data == null)
            return RollPowerResult();

        SkillResolverType fallbackType =
            RuntimeDefinition?.ResolverType ??
            CharacterRandomDebugOverride
                .InferResolverType(
                    Resolver);

        if (CharacterRandomDebugOverride.TryCreateRoll(
                owner,
                this,
                data,
                fallbackType,
                exchangeIndex,
                out RollResult debugResult))
        {
            return debugResult;
        }

        return CombatRollResolver.Roll(
            this,
            data,
            fallbackType);
    }

    public void NotifyRollResolved(
        BattleAction action,
        BattleAction opponentAction,
        int rollIndex,
        bool won,
        DamageContext damageContext)
    {
        // 결투를 평타로 받거나 아예 받지 않은 경우에는
        // 결투 전용 굴림 효과가 발생하지 않는다.
        if (ActionType == ActionType.Duel &&
            opponentAction?.ActionType != ActionType.Duel)
        {
            return;
        }

        SkillRollData data = GetRollData(rollIndex);
        IReadOnlyList<SkillEffectEntry> effectEntries = won
            ? data?.OnWinEffectEntries
            : data?.OnLoseEffectEntries;
        IReadOnlyList<SkillEffectDefinition> legacyEffects = won
            ? data?.OnWinEffects
            : data?.OnLoseEffects;

        if (HasValidEffectEntries(effectEntries))
        {
            ExecuteExplicitEffects(
                effectEntries,
                action,
                won ? SkillEffectTiming.OnRollWin : SkillEffectTiming.OnRollLose,
                opponentAction,
                damageContext);
        }
        else
        {
            ExecuteExplicitEffects(
                legacyEffects,
                action,
                won ? SkillEffectTiming.OnRollWin : SkillEffectTiming.OnRollLose,
                opponentAction,
                damageContext);
        }

        MultiRollPenaltyData penalty = RuntimeDefinition?.MultiRollPenalty;
        if (penalty?.HasExecutableEffect == true &&
            penalty.Timing == MultiRollPenaltyTiming.AfterRoll &&
            penalty.TriggerAfterRollIndex == rollIndex)
        {
            if (HasValidEffectEntries(penalty.EffectEntries))
            {
                ExecuteExplicitEffects(
                    penalty.EffectEntries,
                    action,
                    SkillEffectTiming.OnMultiRollPenaltyAfterRoll,
                    opponentAction,
                    damageContext);
            }
            else
            {
                ExecuteExplicitEffects(
                    penalty.Effects,
                    action,
                    SkillEffectTiming.OnMultiRollPenaltyAfterRoll,
                    opponentAction,
                    damageContext);
            }
        }
    }

    private void ExecuteMultiRollPenalty(
        BattleAction action,
        MultiRollPenaltyTiming timing)
    {
        MultiRollPenaltyData penalty = RuntimeDefinition?.MultiRollPenalty;
        if (penalty?.HasExecutableEffect != true || penalty.Timing != timing)
            return;

        SkillEffectTiming effectTiming = timing switch
        {
            MultiRollPenaltyTiming.OnActionStart => SkillEffectTiming.OnMultiRollPenaltyStart,
            MultiRollPenaltyTiming.AfterRoll => SkillEffectTiming.OnMultiRollPenaltyAfterRoll,
            _ => SkillEffectTiming.OnMultiRollPenaltyEnd
        };

        if (HasValidEffectEntries(penalty.EffectEntries))
            ExecuteExplicitEffects(penalty.EffectEntries, action, effectTiming);
        else
            ExecuteExplicitEffects(penalty.Effects, action, effectTiming);
    }

    private static bool HasValidEffectEntries(
        IReadOnlyList<SkillEffectEntry> entries)
    {
        if (entries == null)
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i]?.Definition != null)
                return true;
        }

        return false;
    }

    private void ExecuteExplicitEffects(
        IReadOnlyList<SkillEffectEntry> effects,
        BattleAction action,
        SkillEffectTiming timing,
        BattleAction opponentAction = null,
        DamageContext damageContext = null)
    {
        if (effects == null || action == null || RuntimeDefinition == null)
            return;

        SkillEffectContext context = new SkillEffectContext(
            action,
            RuntimeDefinition,
            timing,
            opponentAction,
            damageContext,
            null,
            UseCountThisTurn);

        foreach (SkillEffectEntry entry in effects)
            entry?.TryApply(context, timing);
    }

    private void ExecuteExplicitEffects(
        IReadOnlyList<SkillEffectDefinition> effects,
        BattleAction action,
        SkillEffectTiming timing,
        BattleAction opponentAction = null,
        DamageContext damageContext = null)
    {
        if (effects == null || action == null || RuntimeDefinition == null)
            return;

        SkillEffectContext context = new SkillEffectContext(
            action,
            RuntimeDefinition,
            timing,
            opponentAction,
            damageContext,
            null,
            UseCountThisTurn);

        foreach (SkillEffectDefinition effect in effects)
            effect?.TryApply(context, timing);
    }

    protected IReadOnlyList<SkillEffectResult>
        ExecuteDefinitionEffects(
            BattleAction action,
            SkillEffectTiming timing,
            BattleAction opponentAction = null,
            DamageContext damageContext = null,
            KillEventContext killContext = null)
    {
        SkillDefinition definition = RuntimeDefinition;

        if (definition == null ||
            action == null ||
            (!definition.HasEffectEntries &&
             (definition.Effects == null || definition.Effects.Count == 0)))
        {
            return Array.Empty<SkillEffectResult>();
        }

        SkillEffectDispatchKey dispatchKey =
            new SkillEffectDispatchKey(
                action.ActionId,
                timing);

        if (!activeEffectDispatches.Add(dispatchKey))
            return Array.Empty<SkillEffectResult>();

        try
        {
            SkillEffectContext context =
                new SkillEffectContext(
                    action,
                    definition,
                    timing,
                    opponentAction,
                    damageContext,
                    killContext,
                    UseCountThisTurn);

            List<SkillEffectResult> results = new();

            foreach (SkillEffectEntry entry
                     in definition.EnumerateEffectEntries())
            {
                if (entry?.Definition == null)
                    continue;

                SkillEffectResult result =
                    entry.TryApply(
                        context,
                        timing);

                if (result != null)
                    results.Add(result);
            }

            return results;
        }
        finally
        {
            activeEffectDispatches.Remove(
                dispatchKey);
        }
    }

    public void NotifyExchangeWin(
        BattleAction action,
        BattleAction opponentAction,
        DamageContext damageContext)
    {
        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnExchangeWin,
            opponentAction,
            damageContext);
    }

    public void NotifyExchangeLose(
        BattleAction action,
        BattleAction opponentAction,
        DamageContext damageContext)
    {
        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnExchangeLose,
            opponentAction,
            damageContext);
    }

    public void NotifyOneSideHit(
        BattleAction action,
        DamageContext damageContext)
    {
        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnOneSideHit,
            null,
            damageContext);
    }

    public void NotifyClashDraw(
        BattleAction action,
        BattleAction opponentAction)
    {
        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnClashDraw,
            opponentAction);
    }

    private object GetUsageIdentity()
    {
        return (object)RuntimeDefinition ?? this;
    }

    private void RegisterRuntimeEvents()
    {
        if (battleEvent == null)
            return;

        registeredBattleEvent = battleEvent;

        registeredBattleEvent.OnTurnStart +=
            OnTurnStart;
        registeredBattleEvent.OnActionStart +=
            OnActionStart;
        registeredBattleEvent.OnActionEnd +=
            OnActionEnd;
        registeredBattleEvent.OnClashWin +=
            OnClashWin;
        registeredBattleEvent.OnClashLose +=
            OnClashLose;
        registeredBattleEvent.OnDamageEventResolved +=
            OnDamageEventResolved;
        registeredBattleEvent.OnKillResolved +=
            OnKillResolved;
    }

    private void UnregisterRuntimeEvents()
    {
        if (registeredBattleEvent == null)
            return;

        registeredBattleEvent.OnTurnStart -=
            OnTurnStart;
        registeredBattleEvent.OnActionStart -=
            OnActionStart;
        registeredBattleEvent.OnActionEnd -=
            OnActionEnd;
        registeredBattleEvent.OnClashWin -=
            OnClashWin;
        registeredBattleEvent.OnClashLose -=
            OnClashLose;
        registeredBattleEvent.OnDamageEventResolved -=
            OnDamageEventResolved;
        registeredBattleEvent.OnKillResolved -=
            OnKillResolved;

        registeredBattleEvent = null;
    }

    private void OnTurnStart(int turn)
    {
        SkillUsageTracker.ResetOwner(owner);
    }

    private void OnActionStart(BattleAction action)
    {
        if (action?.Skill != this)
            return;

        SkillUsageTracker.Increment(
            owner,
            GetUsageIdentity());

        ExecuteMultiRollPenalty(
            action,
            MultiRollPenaltyTiming.OnActionStart);
    }

    private void OnActionEnd(BattleAction action)
    {
        if (action?.Skill != this)
            return;

        ExecuteMultiRollPenalty(
            action,
            MultiRollPenaltyTiming.OnActionEnd);

        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnActionEnd);
    }

    private void OnClashWin(
        BattleAction winnerAction,
        BattleAction loserAction)
    {
        if (winnerAction?.Skill != this)
            return;

        if (ActionType == ActionType.Duel &&
            loserAction?.ActionType != ActionType.Duel)
        {
            return;
        }

        ExecuteDefinitionEffects(
            winnerAction,
            SkillEffectTiming.OnClashWin,
            loserAction);
    }

    private void OnClashLose(
        BattleAction loserAction,
        BattleAction winnerAction)
    {
        if (loserAction?.Skill != this)
            return;

        if (ActionType == ActionType.Duel &&
            winnerAction?.ActionType != ActionType.Duel)
        {
            return;
        }

        ExecuteDefinitionEffects(
            loserAction,
            SkillEffectTiming.OnClashLose,
            winnerAction);
    }

    private void OnDamageEventResolved(
        DamageEventResult eventResult)
    {
        DamageContext context =
            eventResult?.Context;

        if (context?.Action?.Skill != this)
            return;

        if (context.GetDisplayDamage() > 0)
        {
            ExecuteDefinitionEffects(
                context.Action,
                SkillEffectTiming.AfterDamage,
                null,
                context);
        }

        if (context.WasCritical)
        {
            ExecuteDefinitionEffects(
                context.Action,
                SkillEffectTiming.OnCritical,
                null,
                context);
        }
    }

    private void OnKillResolved(
        KillEventContext context)
    {
        if (context?.SourceAction?.Skill != this)
            return;

        ExecuteDefinitionEffects(
            context.SourceAction,
            SkillEffectTiming.OnKill,
            null,
            context.DamageContext,
            context);
    }

    private readonly struct SkillEffectDispatchKey :
        IEquatable<SkillEffectDispatchKey>
    {
        private readonly long actionId;
        private readonly SkillEffectTiming timing;

        public SkillEffectDispatchKey(
            long actionId,
            SkillEffectTiming timing)
        {
            this.actionId = actionId;
            this.timing = timing;
        }

        public bool Equals(
            SkillEffectDispatchKey other) =>
            actionId == other.actionId &&
            timing == other.timing;

        public override bool Equals(object obj) =>
            obj is SkillEffectDispatchKey other &&
            Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (actionId.GetHashCode() * 397) ^
                       (int)timing;
            }
        }
    }
}

internal static class SkillUsageTracker
{
    private sealed class UsageKey : IEquatable<UsageKey>
    {
        public Character Owner;
        public object Identity;

        public bool Equals(UsageKey other) =>
            other != null &&
            Owner == other.Owner &&
            ReferenceEquals(Identity, other.Identity);

        public override bool Equals(object obj) =>
            obj is UsageKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int ownerHash = Owner == null
                    ? 0
                    : Owner.GetHashCode();

                int identityHash = Identity == null
                    ? 0
                    : Identity.GetHashCode();

                return (ownerHash * 397) ^ identityHash;
            }
        }
    }

    private static readonly Dictionary<UsageKey, int>
        useCounts = new();

    public static int GetUseCount(
        Character owner,
        object identity)
    {
        if (owner == null || identity == null)
            return 0;

        UsageKey lookup = new UsageKey
        {
            Owner = owner,
            Identity = identity
        };

        return useCounts.TryGetValue(
            lookup,
            out int value)
            ? value
            : 0;
    }

    public static void Increment(
        Character owner,
        object identity)
    {
        if (owner == null || identity == null)
            return;

        UsageKey key = new UsageKey
        {
            Owner = owner,
            Identity = identity
        };

        useCounts.TryGetValue(
            key,
            out int value);

        useCounts[key] = value + 1;
    }

    public static void ResetOwner(Character owner)
    {
        if (owner == null)
            return;

        List<UsageKey> removeTargets = new();

        foreach (UsageKey key in useCounts.Keys)
        {
            if (key.Owner == owner)
                removeTargets.Add(key);
        }

        foreach (UsageKey key in removeTargets)
            useCounts.Remove(key);
    }
}