using System;
using System.Collections.Generic;
using UnityEngine;

public enum EmotionAugment0922EffectKind
{
    PermanentSelfStatus = 0,
    TurnStartGuardStrength = 1,
    GoldThresholdStrength = 2,
    PrestigeTargetRuptureInfinite = 3,
    TimedHpRecovery = 4,
    TimedStaggerRecovery = 5
}

/// <summary>
/// 0922 §14에서 공용 상태이상과 증강 고유 지속효과를 분리하기 위한 런타임 효과.
/// 공용 상태는 StatusEffectFactory.CreateCanonical0922를 통해 실제 N/T/∞ Entry를 만들고,
/// 연민의 재생류처럼 카드 자체 지속효과인 것은 StatusEffect를 만들지 않는다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Progression/0922 Emotion Augment Effect",
    fileName = "EmotionAugmentCanonical0922Effect")]
public sealed class EmotionAugmentCanonical0922EffectDefinition :
    EmotionAugmentEffectDefinition
{
    public EmotionAugment0922EffectKind Kind;

    [Header("Common status")]
    public StatusEffectId PrimaryStatus = StatusEffectId.Strength;
    [Min(0)] public int PrimaryValue = 1;
    public int PrimaryDuration = StatusEffect.InfiniteDuration;

    public bool HasSecondaryStatus;
    public StatusEffectId SecondaryStatus = StatusEffectId.Swift;
    [Min(0)] public int SecondaryValue = 1;
    public int SecondaryDuration = StatusEffect.InfiniteDuration;

    [Header("Conditional")]
    [Min(0)] public int GoldThreshold = 500;
    public bool BlockPrestige;

    [Header("Augment-owned timed recovery")]
    [Min(1)] public int Turns = 3;
    [Min(0)] public int Amount = 1;

    public override void Apply(EmotionAugmentRuntimeContext context)
    {
        // Stateful effects are owned by the mechanic returned below.
        // This intentionally avoids creating orphan infinite statuses before the
        // mechanic has a chance to own/clean them.
    }

    public override CombatMechanic CreateMechanic(
        EmotionAugmentRuntimeContext context)
    {
        return new EmotionAugmentCanonical0922Mechanic(this);
    }
}

internal sealed class EmotionAugmentCanonical0922Mechanic : CombatMechanic
{
    private readonly EmotionAugmentCanonical0922EffectDefinition definition;
    private readonly List<OwnedStatus> ownedStatuses = new();
    private bool guardLostThisTurn;
    private int timedTurnsRemaining;

    private sealed class OwnedStatus
    {
        public Character Target;
        public BodyPart Part;
        public StatusEffect Effect;
    }

    public override string MechanicName =>
        $"Emotion Augment 0922 · {definition?.Kind}";

    public EmotionAugmentCanonical0922Mechanic(
        EmotionAugmentCanonical0922EffectDefinition definition)
    {
        this.definition = definition;
        timedTurnsRemaining = Mathf.Max(1, definition?.Turns ?? 1);
    }

    public override void OnRegister()
    {
        if (definition == null || owner == null)
            return;

        SubscribeToBattleEvent(
            () => battleEvent.OnTurnStart += OnTurnStart,
            () => battleEvent.OnTurnStart -= OnTurnStart,
            "OnTurnStart");

        SubscribeToBattleEvent(
            () => battleEvent.OnTurnEnd += OnTurnEnd,
            () => battleEvent.OnTurnEnd -= OnTurnEnd,
            "OnTurnEnd");

        SubscribeToBattleEvent(
            () => battleEvent.OnActionStart += OnActionStart,
            () => battleEvent.OnActionStart -= OnActionStart,
            "OnActionStart");

        SubscribeToBattleEvent(
            () => battleEvent.OnExchangeResolved += OnExchangeResolved,
            () => battleEvent.OnExchangeResolved -= OnExchangeResolved,
            "OnExchangeResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnBattleEnded += OnBattleEnded,
            () => battleEvent.OnBattleEnded -= OnBattleEnded,
            "OnBattleEnded");

        switch (definition.Kind)
        {
            case EmotionAugment0922EffectKind.PermanentSelfStatus:
                EnsurePermanentSelfStatuses();
                break;

            case EmotionAugment0922EffectKind.GoldThresholdStrength:
                RefreshGoldThresholdStrength();
                break;
        }
    }

    public override void OnUnregister()
    {
        RemoveOwnedStatuses();
    }

    public override bool CanUseSkill(BodyPart part, Skill skill)
    {
        if (definition?.BlockPrestige == true &&
            skill?.ActionType == ActionType.Prestige)
        {
            return false;
        }

        return true;
    }

    private void OnTurnStart(int turn)
    {
        if (owner == null || owner.IsDead || definition == null)
            return;

        switch (definition.Kind)
        {
            case EmotionAugment0922EffectKind.TurnStartGuardStrength:
                guardLostThisTurn = false;
                RemoveOwnedStatusesFrom(owner);
                AddOwnedStatus(
                    owner,
                    null,
                    StatusEffectId.Strength,
                    1,
                    StatusEffect.InfiniteDuration);
                break;

            case EmotionAugment0922EffectKind.GoldThresholdStrength:
                RefreshGoldThresholdStrength();
                break;
        }
    }

    private void OnTurnEnd(int turn)
    {
        if (owner == null || owner.IsDead || definition == null)
            return;

        if (timedTurnsRemaining <= 0)
            return;

        switch (definition.Kind)
        {
            case EmotionAugment0922EffectKind.TimedHpRecovery:
                owner.RestoreCurrentHP(Mathf.Max(0, definition.Amount));
                timedTurnsRemaining--;
                break;

            case EmotionAugment0922EffectKind.TimedStaggerRecovery:
            {
                StaggerGaugeMechanic stagger =
                    owner.GetMechanic<StaggerGaugeMechanic>();

                stagger?.Recover(Mathf.Max(0, definition.Amount));
                timedTurnsRemaining--;
                break;
            }
        }
    }

    private void OnActionStart(BattleAction action)
    {
        if (definition?.Kind !=
                EmotionAugment0922EffectKind.PrestigeTargetRuptureInfinite ||
            action?.Owner != owner ||
            action.ActionType != ActionType.Prestige)
        {
            return;
        }

        Character target = action.Target;
        if (target == null || target.IsDead)
            return;

        AddOwnedStatus(
            target,
            action.TargetPart,
            StatusEffectId.Rupture,
            Mathf.Max(1, definition.PrimaryValue),
            StatusEffect.InfiniteDuration);
    }

    private void OnExchangeResolved(ClashExchangeResult exchange)
    {
        if (definition?.Kind !=
                EmotionAugment0922EffectKind.TurnStartGuardStrength ||
            guardLostThisTurn ||
            exchange == null ||
            exchange.WasCancelled ||
            exchange.IsTie)
        {
            return;
        }

        if (exchange.LoserAction?.Owner != owner)
            return;

        guardLostThisTurn = true;
        RemoveOwnedStatusesFrom(owner);
    }

    private void OnBattleEnded()
    {
        RemoveOwnedStatuses();
    }

    private void EnsurePermanentSelfStatuses()
    {
        AddOwnedStatus(
            owner,
            null,
            definition.PrimaryStatus,
            Mathf.Max(1, definition.PrimaryValue),
            definition.PrimaryDuration);

        if (definition.HasSecondaryStatus)
        {
            AddOwnedStatus(
                owner,
                null,
                definition.SecondaryStatus,
                Mathf.Max(1, definition.SecondaryValue),
                definition.SecondaryDuration);
        }
    }

    private void RefreshGoldThresholdStrength()
    {
        int gold = battleContext?.RunProgression?.Gold ?? 0;
        bool shouldBeActive = gold >= Mathf.Max(0, definition.GoldThreshold);
        bool isActive = HasOwnedStatusOn(owner, StatusEffectId.Strength);

        if (shouldBeActive && !isActive)
        {
            AddOwnedStatus(
                owner,
                null,
                StatusEffectId.Strength,
                Mathf.Max(1, definition.PrimaryValue),
                StatusEffect.InfiniteDuration);
        }
        else if (!shouldBeActive && isActive)
        {
            RemoveOwnedStatusesFrom(owner);
        }
    }

    private bool HasOwnedStatusOn(Character target, StatusEffectId id)
    {
        Type expectedType = GetStatusType(id);
        if (expectedType == null)
            return false;

        for (int i = 0; i < ownedStatuses.Count; i++)
        {
            OwnedStatus owned = ownedStatuses[i];
            if (owned?.Target == target &&
                owned.Effect != null &&
                owned.Effect.GetType() == expectedType)
            {
                return true;
            }
        }

        return false;
    }

    private void AddOwnedStatus(
        Character target,
        BodyPart part,
        StatusEffectId id,
        int value,
        int duration)
    {
        if (target == null)
            return;

        StatusEffect effect =
            StatusEffectFactory.CreateCanonical0922(
                id,
                value,
                duration);

        if (effect == null)
            return;

        if (part != null)
            target.AddPartStatus(part, effect, owner);
        else
            target.AddStatus(effect, owner);

        ownedStatuses.Add(
            new OwnedStatus
            {
                Target = target,
                Part = part,
                Effect = effect
            });
    }

    private void RemoveOwnedStatusesFrom(Character target)
    {
        if (target == null)
            return;

        for (int i = ownedStatuses.Count - 1; i >= 0; i--)
        {
            OwnedStatus owned = ownedStatuses[i];
            if (owned?.Target != target)
                continue;

            RemoveOne(owned);
            ownedStatuses.RemoveAt(i);
        }
    }

    private void RemoveOwnedStatuses()
    {
        for (int i = ownedStatuses.Count - 1; i >= 0; i--)
            RemoveOne(ownedStatuses[i]);

        ownedStatuses.Clear();
    }

    private static void RemoveOne(OwnedStatus owned)
    {
        if (owned?.Target == null || owned.Effect == null)
            return;

        if (owned.Part != null)
            owned.Target.RemovePartStatus(owned.Part, owned.Effect);
        else
            owned.Target.RemoveStatus(owned.Effect);
    }

    private static Type GetStatusType(StatusEffectId id)
    {
        return id switch
        {
            StatusEffectId.Strength => typeof(StrengthStatus),
            StatusEffectId.Swift => typeof(SwiftStatus),
            StatusEffectId.Rupture => typeof(RuptureStatus),
            _ => null
        };
    }
}
