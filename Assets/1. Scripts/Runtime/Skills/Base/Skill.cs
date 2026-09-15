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

    public SkillColor Color => SkillColorRules.Resolve(RuntimeDefinition);
    public bool IsRed => Color == SkillColor.Red;
    public bool IsBlue => Color == SkillColor.Blue;

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

    /// <summary>
    /// Planning에서 즉시 실행된 도사림을 명시적으로 취소했을 때
    /// ActionStart가 올린 이번 턴 사용 횟수만 되돌린다.
    /// 전투 중 슬롯 소실/Resolution 경로에서는 호출하지 않는다.
    /// </summary>
    internal void RollbackPlanningUse()
    {
        SkillUsageLedger.Decrement(
            owner,
            GetUsageIdentity());
    }

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

    /// <summary>
    /// 정본의 "매칭시". 원래 행동 쌍이 결투 대 결투라면 굴림 위치마다 발동한다.
    /// 상대 굴림이 먼저 소진되어 현재 위치가 일방타격으로 처리되더라도
    /// 원래 결투 대 결투 게이트가 성립한 행동 쌍이면 매칭은 유지한다.
    /// </summary>
    public void NotifyDuelMatched(
        BattleAction action,
        BattleAction opponentAction,
        int rollIndex,
        bool isOneSided)
    {
        if (action?.ActionType != ActionType.Duel ||
            opponentAction?.ActionType != ActionType.Duel)
        {
            return;
        }

        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnDuelMatched,
            opponentAction,
            rollIndex: rollIndex,
            rollResult: null,
            isClash: true,
            isOneSided: isOneSided,
            rollSucceeded: false);

        ExecuteRollDetailedEffects(
            action,
            opponentAction,
            rollIndex,
            SkillEffectTiming.OnDuelMatched,
            null,
            null,
            isClash: true,
            isOneSided: isOneSided,
            rollSucceeded: false);
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
    /// 굴림 승패가 확정된 직후, 실제 피해 적용 전에 호출한다.
    /// Rules 2026-09 정본의 "승리시/패배시"를 여기서 발행한다.
    /// - 평타: 맞붙은 교환 승/패 + 유효한 일방타격 승리를 인정
    /// - 결투: 결투 대 결투에서 실제로 맞붙은 교환 승/패만 인정
    /// 구 OnRollSuccess/Failure는 기존 직렬화 데이터 호환을 위해 뒤에서 계속 발행한다.
    /// </summary>
    public void NotifyRollOutcome(
        BattleAction action,
        BattleAction opponentAction,
        int rollIndex,
        bool succeeded,
        bool isClash,
        bool isOneSided)
    {
        bool isDuel =
            action?.ActionType == ActionType.Duel;

        bool duelVsDuel =
            isDuel &&
            opponentAction?.ActionType == ActionType.Duel;

        bool canonicalOutcomeAllowed =
            isOneSided
                ? !isDuel && succeeded
                : !isDuel || duelVsDuel;

        if (canonicalOutcomeAllowed)
        {
            SkillEffectTiming canonicalTiming =
                succeeded
                    ? SkillEffectTiming.OnExchangeWin
                    : SkillEffectTiming.OnExchangeLose;

            ExecuteDefinitionEffects(
                action,
                canonicalTiming,
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
                canonicalTiming,
                null,
                null,
                isClash,
                isOneSided,
                succeeded);
        }

        // Serialized compatibility: Gameplay v5 세부 phase를 쓰는 기존 데이터는
        // migration 전에도 동작하도록 보존한다. 신규 Authoring에는 노출하지 않는다.
        SkillEffectTiming legacyTiming =
            succeeded
                ? SkillEffectTiming.OnRollSuccess
                : SkillEffectTiming.OnRollFailure;

        ExecuteDefinitionEffects(
            action,
            legacyTiming,
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
            legacyTiming,
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
        bool rollSucceeded,
        KillEventContext killContext = null)
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
            rollSucceeded,
            killContext);
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
        bool rollSucceeded = false,
        KillEventContext killContext = null)
    {
        if (effects == null || action == null || RuntimeDefinition == null)
            return;

        SkillEffectContext context = new SkillEffectContext(
            action,
            RuntimeDefinition,
            timing,
            opponentAction,
            damageContext,
            killContext,
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
        bool rollSucceeded = false,
        KillEventContext killContext = null)
    {
        if (effects == null || action == null || RuntimeDefinition == null)
            return;

        SkillEffectContext context = new SkillEffectContext(
            action,
            RuntimeDefinition,
            timing,
            opponentAction,
            damageContext,
            killContext,
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
            bool rollSucceeded = false,
            ClashResultContext clashResult = null)
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
            rollSucceeded,
            clashResult);
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

    /// <summary>
    /// 실제 맞붙은 교환 다수결로 합 결과가 확정된 뒤 호출한다.
    /// 일방타격은 승수에 포함되지 않으며, 정본의 합 단위 효과는 이 지점에서만 발동한다.
    /// </summary>
    public void NotifyClashResolved(ClashResultContext result)
    {
        if (result == null ||
            !result.IsClash ||
            result.IsDraw ||
            result.WinnerAction == null ||
            result.LoserAction == null)
        {
            return;
        }

        if (result.WinnerAction.Skill == this)
        {
            ExecuteDefinitionEffects(
                result.WinnerAction,
                SkillEffectTiming.OnClashWin,
                result.LoserAction,
                isClash: true,
                clashResult: result);
        }

        // 합 패배시도 정본 Authoring 트리거다. 승패는 실제 맞붙은
        // 교환 승수의 다수결이며 일방타격은 집계하지 않는다.
        if (result.LoserAction.Skill == this)
        {
            ExecuteDefinitionEffects(
                result.LoserAction,
                SkillEffectTiming.OnClashLose,
                result.WinnerAction,
                isClash: true,
                clashResult: result);
        }
    }

    /// <summary>
    /// 정본의 "합 종료시". 맞붙는 교환과 남은 굴림의 일방타격까지
    /// 해당 합에 속한 모든 굴림 처리가 끝난 뒤 양쪽 스킬에 1회 발동한다.
    /// 합 전체 승리/패배(OnClashWin/OnClashLose)보다 늦은 시점이다.
    /// </summary>
    public void NotifyClashEnded(ClashResultContext result)
    {
        if (result == null ||
            !result.IsClash)
        {
            return;
        }

        if (result.FirstAction?.Skill == this)
        {
            ExecuteDefinitionEffects(
                result.FirstAction,
                SkillEffectTiming.OnClashEnd,
                result.SecondAction,
                isClash: true,
                clashResult: result);
            return;
        }

        if (result.SecondAction?.Skill == this)
        {
            ExecuteDefinitionEffects(
                result.SecondAction,
                SkillEffectTiming.OnClashEnd,
                result.FirstAction,
                isClash: true,
                clashResult: result);
        }
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
        registeredBattleEvent.OnDamageEventResolved +=
            OnDamageEventResolved;
        registeredBattleEvent.OnBodyPartWeakenResolved +=
            OnBodyPartWeakenResolved;
        registeredBattleEvent.OnBodyPartBreakResolved +=
            OnBodyPartBreakResolved;
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
        registeredBattleEvent.OnDamageEventResolved -=
            OnDamageEventResolved;
        registeredBattleEvent.OnBodyPartWeakenResolved -=
            OnBodyPartWeakenResolved;
        registeredBattleEvent.OnBodyPartBreakResolved -=
            OnBodyPartBreakResolved;
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

        bool isClash =
            context.IsClashDamage;
        bool isOneSided =
            !isClash;

        if (context.GetDisplayDamage() > 0)
        {
            ExecuteDefinitionEffects(
                action,
                SkillEffectTiming.AfterDamage,
                null,
                context,
                rollIndex: rollIndex,
                rollResult: rollResult,
                isClash: isClash,
                isOneSided: isOneSided,
                rollSucceeded: true);

            ExecuteRollDetailedEffects(
                action,
                null,
                rollIndex,
                SkillEffectTiming.AfterDamage,
                context,
                null,
                isClash,
                isOneSided,
                true);
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
                isClash: isClash,
                isOneSided: isOneSided,
                rollSucceeded: true);

            ExecuteRollDetailedEffects(
                action,
                null,
                rollIndex,
                SkillEffectTiming.OnCritical,
                context,
                null,
                isClash,
                isOneSided,
                true);
        }
    }

    private void OnBodyPartWeakenResolved(
        BodyPartWeakenEventContext context)
    {
        if (context?.IsDamageDriven != true ||
            context.SourceAction?.Skill != this ||
            context.DamageContext == null)
        {
            return;
        }

        DispatchDamageTransitionTiming(
            context.SourceAction,
            context.DamageContext,
            SkillEffectTiming.OnPartWeakened);
    }

    private void OnBodyPartBreakResolved(
        BodyPartBreakEventContext context)
    {
        if (context?.IsDamageDriven != true ||
            context.SourceAction?.Skill != this ||
            context.DamageContext == null)
        {
            return;
        }

        DispatchDamageTransitionTiming(
            context.SourceAction,
            context.DamageContext,
            SkillEffectTiming.OnPartBroken);
    }

    private void DispatchDamageTransitionTiming(
        BattleAction action,
        DamageContext damageContext,
        SkillEffectTiming timing)
    {
        if (action == null || damageContext == null)
            return;

        int rollIndex =
            Mathf.Max(0, action.CurrentRollIndex);
        bool isClash =
            damageContext.IsClashDamage;
        bool isOneSided =
            !isClash;

        ExecuteDefinitionEffects(
            action,
            timing,
            null,
            damageContext,
            rollIndex: rollIndex,
            rollResult: action.LastRollResult?.Clone(),
            isClash: isClash,
            isOneSided: isOneSided,
            rollSucceeded: true);

        ExecuteRollDetailedEffects(
            action,
            null,
            rollIndex,
            timing,
            damageContext,
            null,
            isClash,
            isOneSided,
            true);
    }

    private void OnKillResolved(
        KillEventContext context)
    {
        BattleAction action =
            context?.SourceAction;

        if (action?.Skill != this)
            return;

        DamageContext damageContext =
            context.DamageContext;
        int rollIndex =
            Mathf.Max(0, action.CurrentRollIndex);
        bool isClash =
            damageContext?.IsClashDamage == true;
        bool isOneSided =
            !isClash;

        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnKill,
            null,
            damageContext,
            context,
            rollIndex: rollIndex,
            rollResult: action.LastRollResult?.Clone(),
            isClash: isClash,
            isOneSided: isOneSided,
            rollSucceeded: true);

        ExecuteRollDetailedEffects(
            action,
            null,
            rollIndex,
            SkillEffectTiming.OnKill,
            damageContext,
            null,
            isClash,
            isOneSided,
            true,
            context);
    }

}