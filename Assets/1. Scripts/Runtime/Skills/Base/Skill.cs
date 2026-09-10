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

    public Character Owner => owner;

    private BattleEvent registeredBattleEvent;
    private readonly SkillEffectDispatcher effectDispatcher = new();

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
                ? Mathf.Clamp(fallback, 1, 8)
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
        SkillUsageLedger.GetUseCount(
            owner,
            GetUsageIdentity());

    public bool IsFirstUseThisTurn =>
        UseCountThisTurn == 1;

    public virtual bool CanUseByResource(Character character)
    {
        return SkillCostService.CanUse(
            this,
            character);
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

        return SkillCostService.TryConsume(
            this,
            character,
            sourceAction);
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
        // 생성/초기화와 활성 이벤트 수명을 분리한다.
        // 어떤 런타임 Skill을 Register할지는 CharacterEventBinder의 정책으로 남긴다.
        UnregisterRuntimeEvents();

        owner = character;
        this.battleEvent = battleEvent;
    }

    public virtual void Register()
    {
        RegisterRuntimeEvents();
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
        return SkillRollService.RollBase(this);
    }

    public SkillRollData GetRollData(int exchangeIndex) =>
        RuntimeDefinition?.GetRollData(exchangeIndex);

    public virtual CombatRollType GetRollType(int exchangeIndex) =>
        GetRollData(exchangeIndex)?.Type ?? CombatRollType.Attack;

    public bool ShouldReuseRollData(int exchangeIndex) =>
        GetRollData(exchangeIndex)?.ReuseValueAcrossAction == true ||
        (GetRollData(exchangeIndex) == null &&
         RollReusePolicy == SkillRollReusePolicy.OncePerAction);

    public virtual RollResult RollPowerResultForExchange(int exchangeIndex)
    {
        return SkillRollService.RollExchange(
            this,
            exchangeIndex);
    }

    public void NotifyBeforeUse(
        BattleAction action,
        BattleAction opponentAction = null,
        bool isClash = false,
        bool isOneSided = false)
    {
        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.BeforeUse,
            opponentAction,
            isClash: isClash,
            isOneSided: isOneSided);
    }

    public void NotifyClashStart(
        BattleAction action,
        BattleAction opponentAction)
    {
        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnClashStart,
            opponentAction,
            isClash: true);
    }

    public void NotifyOneSidedStart(
        BattleAction action)
    {
        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnOneSidedStart,
            isOneSided: true);
    }

    public void NotifyBeforeAttack(
        BattleAction action,
        BattleAction opponentAction,
        bool isClash,
        bool isOneSided)
    {
        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.BeforeAttack,
            opponentAction,
            isClash: isClash,
            isOneSided: isOneSided);
    }

    public void NotifyRollStart(
        BattleAction action,
        BattleAction opponentAction,
        int rollIndex,
        bool isClash,
        bool isOneSided)
    {
        // OnRollStart는 실제 RNG를 뽑기 전에 발생한다.
        // 이전 교환의 LastRollResult를 잘못 노출하지 않도록 결과는 null로 둔다.
        RollResult rollResult = null;

        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnRollStart,
            opponentAction,
            exchangeResult: null,
            rollIndex: rollIndex,
            rollResult: rollResult,
            isClash: isClash,
            isOneSided: isOneSided,
            rollSucceeded: false);

        ExecuteRollDetailedEffects(
            action,
            opponentAction,
            rollIndex,
            SkillEffectTiming.OnRollStart,
            null,
            null,
            isClash,
            isOneSided,
            false);
    }

    public void NotifyRollResolved(
        BattleAction action,
        BattleAction opponentAction,
        int rollIndex,
        bool won,
        DamageContext damageContext,
        bool isClash = false,
        bool isOneSided = false)
    {
        SkillRollData data = GetRollData(rollIndex);

        // 기존 OnWin/OnLose 전용 리스트는 Duel-vs-Duel 규칙을 그대로 보존한다.
        bool legacyDuelEffectsAllowed =
            ActionType != ActionType.Duel ||
            opponentAction?.ActionType == ActionType.Duel;

        if (legacyDuelEffectsAllowed)
        {
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
                    damageContext,
                    rollIndex: rollIndex,
                    rollResult: action?.LastRollResult,
                    isClash: isClash,
                    isOneSided: isOneSided,
                    rollSucceeded: won);
            }
            else
            {
                ExecuteExplicitEffects(
                    legacyEffects,
                    action,
                    won ? SkillEffectTiming.OnRollWin : SkillEffectTiming.OnRollLose,
                    opponentAction,
                    damageContext,
                    rollIndex: rollIndex,
                    rollResult: action?.LastRollResult,
                    isClash: isClash,
                    isOneSided: isOneSided,
                    rollSucceeded: won);
            }
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
                    damageContext,
                    rollIndex: rollIndex,
                    rollResult: action?.LastRollResult,
                    isClash: isClash,
                    isOneSided: isOneSided,
                    rollSucceeded: won);
            }
            else
            {
                ExecuteExplicitEffects(
                    penalty.Effects,
                    action,
                    SkillEffectTiming.OnMultiRollPenaltyAfterRoll,
                    opponentAction,
                    damageContext,
                    rollIndex: rollIndex,
                    rollResult: action?.LastRollResult,
                    isClash: isClash,
                    isOneSided: isOneSided,
                    rollSucceeded: won);
            }
        }
    }

    /// <summary>
    /// 굴림의 성공/실패가 확정된 직후, 실제 HP/흐트러짐 피해를 적용하기 전에 호출한다.
    /// 따라서 OnRollSuccess/Failure 효과가 이후 피해 계산에 영향을 줄 수 있다.
    /// </summary>
    public void NotifyRollOutcome(
        BattleAction action,
        BattleAction opponentAction,
        int rollIndex,
        bool succeeded,
        bool isClash,
        bool isOneSided)
    {
        SkillEffectTiming timing =
            succeeded
                ? SkillEffectTiming.OnRollSuccess
                : SkillEffectTiming.OnRollFailure;

        ExecuteDefinitionEffects(
            action,
            timing,
            opponentAction,
            rollIndex: rollIndex,
            rollResult: action?.LastRollResult,
            isClash: isClash,
            isOneSided: isOneSided,
            rollSucceeded: succeeded);

        ExecuteRollDetailedEffects(
            action,
            opponentAction,
            rollIndex,
            timing,
            null,
            null,
            isClash,
            isOneSided,
            succeeded);
    }

    public void NotifyHit(
        BattleAction action,
        BattleAction opponentAction,
        int rollIndex,
        ClashExchangeResult exchangeResult,
        bool isClash,
        bool isOneSided)
    {
        if (action == null || exchangeResult == null)
            return;

        // 적중은 "최종 HP 숫자가 1 이상인가"가 아니라
        // 공격/흐트러짐 굴림이 실제 교환에서 승리해 타깃에 도달했는가로 정의한다.
        // 따라서 Guard가 HP 피해를 전부 흡수해도 OnHit은 정상 발동한다.
        bool hitEstablished =
            exchangeResult.WinnerAction == action &&
            !exchangeResult.WasCancelled &&
            (action.CurrentRollType == CombatRollType.Attack ||
             action.CurrentRollType == CombatRollType.Stagger);

        if (!hitEstablished)
            return;

        DamageContext damageContext =
            exchangeResult.DamageContext;

        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnHit,
            opponentAction,
            damageContext,
            exchangeResult: exchangeResult,
            rollIndex: rollIndex,
            rollResult: action.LastRollResult,
            isClash: isClash,
            isOneSided: isOneSided,
            rollSucceeded: true);

        ExecuteRollDetailedEffects(
            action,
            opponentAction,
            rollIndex,
            SkillEffectTiming.OnHit,
            damageContext,
            exchangeResult,
            isClash,
            isOneSided,
            true);
    }

    public void NotifyRollEnd(
        BattleAction action,
        BattleAction opponentAction,
        int rollIndex,
        bool succeeded,
        ClashExchangeResult exchangeResult,
        bool isClash,
        bool isOneSided)
    {
        DamageContext damageContext =
            exchangeResult?.DamageContext;

        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnRollEnd,
            opponentAction,
            damageContext,
            exchangeResult: exchangeResult,
            rollIndex: rollIndex,
            rollResult: action?.LastRollResult,
            isClash: isClash,
            isOneSided: isOneSided,
            rollSucceeded: succeeded);

        ExecuteRollDetailedEffects(
            action,
            opponentAction,
            rollIndex,
            SkillEffectTiming.OnRollEnd,
            damageContext,
            exchangeResult,
            isClash,
            isOneSided,
            succeeded);
    }

    public void NotifyAttackEnd(
        BattleAction action,
        BattleAction opponentAction,
        bool isClash,
        bool isOneSided)
    {
        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnAttackEnd,
            opponentAction,
            isClash: isClash,
            isOneSided: isOneSided);
    }

    private void ExecuteRollDetailedEffects(
        BattleAction action,
        BattleAction opponentAction,
        int rollIndex,
        SkillEffectTiming timing,
        DamageContext damageContext,
        ClashExchangeResult exchangeResult,
        bool isClash,
        bool isOneSided,
        bool rollSucceeded)
    {
        SkillRollData data = GetRollData(rollIndex);

        if (!HasValidEffectEntries(data?.EffectEntries))
            return;

        ExecuteExplicitEffects(
            data.EffectEntries,
            action,
            timing,
            opponentAction,
            damageContext,
            exchangeResult,
            rollIndex,
            action?.LastRollResult,
            isClash,
            isOneSided,
            rollSucceeded);
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
        DamageContext damageContext = null,
        ClashExchangeResult exchangeResult = null,
        int rollIndex = -1,
        RollResult rollResult = null,
        bool isClash = false,
        bool isOneSided = false,
        bool rollSucceeded = false)
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
            exchangeResult,
            UseCountThisTurn,
            rollIndex,
            rollResult,
            isClash,
            isOneSided,
            rollSucceeded);

        foreach (SkillEffectEntry entry in effects)
            entry?.TryApply(context, timing);
    }

    private void ExecuteExplicitEffects(
        IReadOnlyList<SkillEffectDefinition> effects,
        BattleAction action,
        SkillEffectTiming timing,
        BattleAction opponentAction = null,
        DamageContext damageContext = null,
        ClashExchangeResult exchangeResult = null,
        int rollIndex = -1,
        RollResult rollResult = null,
        bool isClash = false,
        bool isOneSided = false,
        bool rollSucceeded = false)
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
            exchangeResult,
            UseCountThisTurn,
            rollIndex,
            rollResult,
            isClash,
            isOneSided,
            rollSucceeded);

        foreach (SkillEffectDefinition effect in effects)
            effect?.TryApply(context, timing);
    }

    protected IReadOnlyList<SkillEffectResult>
        ExecuteDefinitionEffects(
            BattleAction action,
            SkillEffectTiming timing,
            BattleAction opponentAction = null,
            DamageContext damageContext = null,
            KillEventContext killContext = null,
            ClashExchangeResult exchangeResult = null,
            int rollIndex = -1,
            RollResult rollResult = null,
            bool isClash = false,
            bool isOneSided = false,
            bool rollSucceeded = false)
    {
        return effectDispatcher.Execute(
            RuntimeDefinition,
            action,
            timing,
            UseCountThisTurn,
            opponentAction,
            damageContext,
            killContext,
            exchangeResult,
            rollIndex,
            rollResult,
            isClash,
            isOneSided,
            rollSucceeded);
    }

    private IReadOnlyList<SkillEffectResult>
        ExecuteOwnerDefinitionEffects(
            SkillEffectTiming timing)
    {
        return effectDispatcher.ExecuteOwner(
            owner,
            RuntimeDefinition,
            timing,
            UseCountThisTurn);
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
            damageContext,
            isOneSided: true);
    }

    public void NotifyClashDraw(
        BattleAction action,
        BattleAction opponentAction)
    {
        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnClashDraw,
            opponentAction,
            isClash: true);
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

        registeredBattleEvent.OnBattleStarted +=
            OnBattleStarted;
        registeredBattleEvent.OnBattleEnded +=
            OnBattleEnded;
        registeredBattleEvent.OnTurnStart +=
            OnTurnStart;
        registeredBattleEvent.OnTurnEnd +=
            OnTurnEnd;
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

        registeredBattleEvent.OnBattleStarted -=
            OnBattleStarted;
        registeredBattleEvent.OnBattleEnded -=
            OnBattleEnded;
        registeredBattleEvent.OnTurnStart -=
            OnTurnStart;
        registeredBattleEvent.OnTurnEnd -=
            OnTurnEnd;
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

    private void OnBattleStarted()
    {
        ExecuteOwnerDefinitionEffects(
            SkillEffectTiming.OnBattleStart);
    }

    private void OnBattleEnded()
    {
        ExecuteOwnerDefinitionEffects(
            SkillEffectTiming.OnBattleEnd);
    }

    private void OnTurnStart(int turn)
    {
        ExecuteOwnerDefinitionEffects(
            SkillEffectTiming.OnTurnStart);
    }

    private void OnTurnEnd(int turn)
    {
        ExecuteOwnerDefinitionEffects(
            SkillEffectTiming.OnTurnEnd);
    }

    private void OnActionStart(BattleAction action)
    {
        if (action?.Skill != this)
            return;

        SkillUsageLedger.Increment(
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

        // 구형 OnActionEnd는 기존 에셋 호환을 위해 그대로 발행한다.
        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnActionEnd);

        // Gameplay v5의 "스킬 종료시"는 해당 행동의 모든 굴림/공격 처리가
        // 끝나고 ActionEnd가 발행되는 지점으로 정의한다.
        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnSkillEnd);
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

        BattleAction action =
            context?.Action;

        if (action?.Skill != this)
            return;

        int rollIndex =
            Mathf.Max(
                0,
                action.CurrentRollIndex);

        RollResult rollResult =
            action.LastRollResult?.Clone();

        if (context.GetDisplayDamage() > 0)
        {
            ExecuteDefinitionEffects(
                action,
                SkillEffectTiming.AfterDamage,
                null,
                context,
                rollIndex: rollIndex,
                rollResult: rollResult,
                rollSucceeded: true);
        }

        if (context.WasCritical)
        {
            ExecuteDefinitionEffects(
                action,
                SkillEffectTiming.OnCritical,
                null,
                context,
                rollIndex: rollIndex,
                rollResult: rollResult,
                rollSucceeded: true);
        }
    }

    private void OnKillResolved(
        KillEventContext context)
    {
        BattleAction action =
            context?.SourceAction;

        if (action?.Skill != this)
            return;

        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnKill,
            null,
            context.DamageContext,
            context,
            rollIndex: Mathf.Max(0, action.CurrentRollIndex),
            rollResult: action.LastRollResult?.Clone(),
            rollSucceeded: true);
    }

}
