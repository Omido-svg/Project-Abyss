using System;
using System.Linq;

/// <summary>1112 §18.7. Independent action counters, never turn durations.</summary>
public sealed class YujinStealthStatus : StatusEffect, IUniqueKeywordStatus
{
    public string UniqueKeywordId => "yujin.stealth";
    public override StatusEffectDurationPolicy DurationPolicy => StatusEffectDurationPolicy.Permanent;
    public override bool CanMergeWith(StatusEffect other) => false;
    public YujinStealthStatus(int actions)
    {
        if (actions <= 0) throw new ArgumentOutOfRangeException(nameof(actions));
        Name = "은신";
        Stack = actions;
        Duration = InfiniteDuration;
    }
    public override void OnApply()
    {
        if (owner?.BattleEvent != null) owner.BattleEvent.OnActionEnd += OnActionEnd;
    }
    public override void OnRemove()
    {
        if (owner?.BattleEvent != null) owner.BattleEvent.OnActionEnd -= OnActionEnd;
    }
    private void OnActionEnd(BattleAction action)
    {
        if (action?.Owner != owner || action.Slot?.Part != ownerPart) return;
        if (--Stack <= 0) owner.RemovePartStatus(ownerPart, this);
    }
    public static bool IsHidden(BodyPart part) => part != null && !part.IsBroken &&
        part.StatusEffects.Any(x => x is YujinStealthStatus && x.Stack > 0);
    public static void ClearActive(Character target, BodyPart part)
    {
        if (target == null || part == null) return;
        foreach (var status in part.StatusEffects.OfType<YujinStealthStatus>().ToArray())
            target.RemovePartStatus(part, status);
    }
    public static void Reserve(Character target, BodyPart part, int actions, Character source)
    {
        if (target == null || part == null || part.IsBroken || actions <= 0) return;
        target.BattleContext.TurnStartEffects.Enqueue(() =>
        {
            if (!target.IsDead && !part.IsBroken)
                target.AddPartStatus(part, new YujinStealthStatus(actions), source);
        });
    }
}
