using System;
using System.Reflection;
using UnityEngine;

public class SkillEffectContext
{
    private readonly Character ownerOverride;

    public BattleAction Action { get; }
    public SkillDefinition SkillDefinition { get; }
    public SkillEffectTiming Timing { get; }

    public BattleAction OpponentAction { get; }
    public DamageContext DamageContext { get; }
    public DamageResult DamageResult =>
        DamageContext?.Result;
    public KillEventContext KillContext { get; }
    public ClashExchangeResult ExchangeResult { get; }

    /// <summary>
    /// 굴림과 무관한 페이즈에서는 -1.
    /// UI에 보여줄 때는 RollNumber(1-based)를 사용한다.
    /// </summary>
    public int RollIndex { get; }
    public int RollNumber =>
        RollIndex >= 0
            ? RollIndex + 1
            : 0;

    public RollResult RollResult { get; }
    public CombatRollType RollType { get; }
    public PhysicalDamageType PhysicalType { get; }
    public bool IsClash { get; }
    public bool IsOneSided { get; }
    public bool RollSucceeded { get; }

    public int UseCountThisTurn { get; }
    public bool IsFirstUseThisTurn =>
        UseCountThisTurn == 1;

    public Character Owner =>
        Action?.Owner ??
        ownerOverride;

    public BodyPart OwnerPart => Action?.OwnerPart;

    public Character OriginalTarget => Action?.Target;
    public BodyPart OriginalTargetPart => Action?.TargetPart;

    public Character Target { get; }
    public BodyPart TargetPart { get; }

    public BattleContext BattleContext =>
        Owner?.BattleContext;

    public BattleEffectResolver Resolver =>
        BattleContext?.EffectResolver;

    public DamageManager DamageManager =>
        ResolveDamageManager();

    public SkillEffectContext(
        BattleAction action,
        SkillDefinition skillDefinition)
        : this(
            action?.Owner,
            action,
            skillDefinition,
            SkillEffectTiming.OnExecute,
            null,
            null,
            null,
            null,
            0,
            -1,
            null,
            false,
            false,
            false,
            action?.Target,
            action?.TargetPart)
    {
    }

    public SkillEffectContext(
        BattleAction action,
        SkillDefinition skillDefinition,
        SkillEffectTiming timing,
        BattleAction opponentAction,
        DamageContext damageContext,
        KillEventContext killContext,
        int useCountThisTurn)
        : this(
            action?.Owner,
            action,
            skillDefinition,
            timing,
            opponentAction,
            damageContext,
            killContext,
            null,
            useCountThisTurn,
            -1,
            null,
            false,
            false,
            false,
            damageContext?.Target ??
            action?.Target,
            damageContext?.TargetPart ??
            action?.TargetPart)
    {
    }

    public SkillEffectContext(
        BattleAction action,
        SkillDefinition skillDefinition,
        SkillEffectTiming timing,
        BattleAction opponentAction,
        DamageContext damageContext,
        KillEventContext killContext,
        ClashExchangeResult exchangeResult,
        int useCountThisTurn,
        int rollIndex,
        RollResult rollResult,
        bool isClash,
        bool isOneSided,
        bool rollSucceeded)
        : this(
            action?.Owner,
            action,
            skillDefinition,
            timing,
            opponentAction,
            damageContext,
            killContext,
            exchangeResult,
            useCountThisTurn,
            rollIndex,
            rollResult,
            isClash,
            isOneSided,
            rollSucceeded,
            damageContext?.Target ??
            action?.Target,
            damageContext?.TargetPart ??
            action?.TargetPart)
    {
    }

    /// <summary>
    /// 전투 시작/턴 종료처럼 BattleAction이 존재하지 않는 스킬 효과 페이즈용.
    /// CurrentTarget은 안전하게 스킬 소유자 자신으로 초기화한다.
    /// </summary>
    public SkillEffectContext(
        Character owner,
        SkillDefinition skillDefinition,
        SkillEffectTiming timing,
        int useCountThisTurn)
        : this(
            owner,
            null,
            skillDefinition,
            timing,
            null,
            null,
            null,
            null,
            useCountThisTurn,
            -1,
            null,
            false,
            false,
            false,
            owner,
            null)
    {
    }

    private SkillEffectContext(
        Character owner,
        BattleAction action,
        SkillDefinition skillDefinition,
        SkillEffectTiming timing,
        BattleAction opponentAction,
        DamageContext damageContext,
        KillEventContext killContext,
        ClashExchangeResult exchangeResult,
        int useCountThisTurn,
        int rollIndex,
        RollResult rollResult,
        bool isClash,
        bool isOneSided,
        bool rollSucceeded,
        Character target,
        BodyPart targetPart)
    {
        ownerOverride = owner;
        Action = action;
        SkillDefinition = skillDefinition;
        Timing = timing;
        OpponentAction = opponentAction;
        DamageContext = damageContext;
        KillContext = killContext;
        ExchangeResult = exchangeResult;
        UseCountThisTurn = useCountThisTurn;
        RollIndex = rollIndex;
        RollResult = rollResult;
        IsClash = isClash;
        IsOneSided = isOneSided;
        RollSucceeded = rollSucceeded;
        Target = target;
        TargetPart = targetPart;

        if (rollIndex >= 0 && action?.Skill != null)
        {
            RollType =
                action.Skill.GetRollType(
                    rollIndex);

            PhysicalType =
                PhysicalDamageResolver.Resolve(
                    action,
                    rollIndex);
        }
        else
        {
            RollType = action?.CurrentRollType ?? CombatRollType.Attack;
            PhysicalType = action != null
                ? PhysicalDamageResolver.Resolve(action)
                : skillDefinition?.PhysicalType ?? PhysicalDamageType.Cut;
        }
    }

    public SkillEffectContext WithTarget(
        SkillEffectTargetSelector selector)
    {
        Character selectedCharacter = Target;
        BodyPart selectedPart = TargetPart;

        switch (selector)
        {
            case SkillEffectTargetSelector.Owner:
                selectedCharacter = Owner;
                selectedPart = OwnerPart;
                break;

            case SkillEffectTargetSelector.OwnerCharacter:
                selectedCharacter = Owner;
                selectedPart = null;
                break;

            case SkillEffectTargetSelector.Opponent:
                selectedCharacter =
                    OpponentAction?.Owner ??
                    OriginalTarget;
                selectedPart =
                    OpponentAction?.OwnerPart ??
                    OriginalTargetPart;
                break;

            case SkillEffectTargetSelector.DamageTarget:
                selectedCharacter =
                    DamageContext?.Target ??
                    OriginalTarget;
                selectedPart =
                    DamageContext?.TargetPart ??
                    OriginalTargetPart;
                break;

            case SkillEffectTargetSelector.KillVictim:
                selectedCharacter =
                    KillContext?.Victim ??
                    OriginalTarget;
                selectedPart =
                    KillContext?.DamageContext?.TargetPart ??
                    OriginalTargetPart;
                break;

            case SkillEffectTargetSelector.RandomAliveEnemy:
                selectedCharacter =
                    SelectRandomAliveEnemy();
                selectedPart = null;
                break;

            case SkillEffectTargetSelector.RandomAliveEnemyPart:
                selectedCharacter =
                    SelectRandomAliveEnemy();
                selectedPart =
                    selectedCharacter?.GetRandomUsablePart();
                break;

            case SkillEffectTargetSelector.RandomUsableOwnerPart:
                selectedCharacter = Owner;
                selectedPart =
                    Owner?.GetRandomUsablePart();
                break;
        }

        return new SkillEffectContext(
            Owner,
            Action,
            SkillDefinition,
            Timing,
            OpponentAction,
            DamageContext,
            KillContext,
            ExchangeResult,
            UseCountThisTurn,
            RollIndex,
            RollResult,
            IsClash,
            IsOneSided,
            RollSucceeded,
            selectedCharacter,
            selectedPart);
    }

    public DamageContext ApplyDamage(
        DamageRequest request)
    {
        DamageManager manager = DamageManager;

        if (manager == null)
        {
            Debug.LogWarning(
                "[SkillEffectContext] DamageManager를 찾을 수 없어 " +
                "조건부 피해를 적용하지 못했습니다.");
            return null;
        }

        return manager.ApplyDamageContext(request);
    }

    public int GetCustomResource(
        string resourceKey)
    {
        return SkillResourceAccess.Get(
            Owner,
            resourceKey);
    }

    private Character SelectRandomAliveEnemy()
    {
        BattleContext context = BattleContext;

        if (context == null || Owner == null)
            return null;

        if (Owner == context.Player)
        {
            if (context.Enemies == null)
                return null;

            System.Collections.Generic.List<Character>
                candidates = new();

            foreach (Character enemy in context.Enemies)
            {
                if (enemy != null && !enemy.IsDead)
                    candidates.Add(enemy);
            }

            if (candidates.Count == 0)
                return null;

            return candidates[
                UnityEngine.Random.Range(
                    0,
                    candidates.Count)];
        }

        return context.Player != null &&
               !context.Player.IsDead
            ? context.Player
            : null;
    }

    private DamageManager ResolveDamageManager()
    {
        return BattleContext?.ResolveDamageManager();
    }
}
