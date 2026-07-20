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

    public virtual bool CanBreakPart => false;

    public virtual int ExchangeRollCount
    {
        get
        {
            SkillDefinition definition = RuntimeDefinition;

            if (definition != null)
                return Mathf.Max(1, definition.ExchangeRollCount);

            int fallback =
                owner?.BattleContext?.Rules?.Clash
                    ?.DefaultExchangeRollCount ?? 3;

            return ActionType == ActionType.NormalAttack ||
                   ActionType == ActionType.Duel
                ? Mathf.Max(1, fallback)
                : 1;
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
                ActionType.Preparation =>
                    PreparationTier == PreparationTier.Strong
                        ? 1
                        : 0,
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
            definition.Effects == null ||
            action == null)
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

            foreach (SkillEffectDefinition effect
                     in definition.Effects)
            {
                if (effect == null)
                    continue;

                SkillEffectResult result =
                    effect.TryApply(
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
    }

    private void OnActionEnd(BattleAction action)
    {
        if (action?.Skill != this)
            return;

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
