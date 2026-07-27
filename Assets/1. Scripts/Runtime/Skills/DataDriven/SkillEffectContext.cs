using System;
using System.Reflection;
using UnityEngine;

public class SkillEffectContext
{
    public BattleAction Action { get; }
    public SkillDefinition SkillDefinition { get; }
    public SkillEffectTiming Timing { get; }

    public BattleAction OpponentAction { get; }
    public DamageContext DamageContext { get; }
    public DamageResult DamageResult =>
        DamageContext?.Result;
    public KillEventContext KillContext { get; }

    public int UseCountThisTurn { get; }
    public bool IsFirstUseThisTurn =>
        UseCountThisTurn == 1;

    public Character Owner => Action?.Owner;
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
            action,
            skillDefinition,
            SkillEffectTiming.OnExecute,
            null,
            null,
            null,
            0,
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
            action,
            skillDefinition,
            timing,
            opponentAction,
            damageContext,
            killContext,
            useCountThisTurn,
            damageContext?.Target ??
            action?.Target,
            damageContext?.TargetPart ??
            action?.TargetPart)
    {
    }

    private SkillEffectContext(
        BattleAction action,
        SkillDefinition skillDefinition,
        SkillEffectTiming timing,
        BattleAction opponentAction,
        DamageContext damageContext,
        KillEventContext killContext,
        int useCountThisTurn,
        Character target,
        BodyPart targetPart)
    {
        Action = action;
        SkillDefinition = skillDefinition;
        Timing = timing;
        OpponentAction = opponentAction;
        DamageContext = damageContext;
        KillContext = killContext;
        UseCountThisTurn = useCountThisTurn;
        Target = target;
        TargetPart = targetPart;
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
            Action,
            SkillDefinition,
            Timing,
            OpponentAction,
            DamageContext,
            KillContext,
            UseCountThisTurn,
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
        object battleManager =
            BattleContext?.battleManager;

        if (battleManager == null)
            return null;

        Type type = battleManager.GetType();

        PropertyInfo property =
            type.GetProperty(
                "DamageManager",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        if (property?.GetValue(battleManager)
            is DamageManager propertyManager)
        {
            return propertyManager;
        }

        FieldInfo field =
            type.GetField(
                "damageManager",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        return field?.GetValue(battleManager)
            as DamageManager;
    }
}