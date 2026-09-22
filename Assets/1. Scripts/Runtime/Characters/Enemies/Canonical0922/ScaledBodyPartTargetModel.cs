using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 0922 enemy Whole-HP price model.
/// Part HP remains on the ordinary BodyPart ledger; only Whole HP maximum /
/// initial value is scaled by the enemy tier multiplier.
/// </summary>
public sealed class ScaledBodyPartTargetModel : ICombatTargetModel
{
    private readonly BodyPartTargetModel inner = new();
    private readonly float wholeHpMultiplier;

    public ScaledBodyPartTargetModel(float wholeHpMultiplier)
    {
        this.wholeHpMultiplier = Mathf.Max(0f, wholeHpMultiplier);
    }

    public bool UsesBodyParts => true;
    public bool RequiresTargetPart => true;
    public float WholeHpMultiplier => wholeHpMultiplier;

    public int CalculateInitialHp(Character owner) =>
        Scale(inner.CalculateInitialHp(owner));

    public int GetMaxHp(Character owner) =>
        Scale(inner.GetMaxHp(owner));

    public bool IsValidTargetPart(
        Character owner,
        BodyPart part,
        bool allowBrokenPart) =>
        inner.IsValidTargetPart(owner, part, allowBrokenPart);

    public bool IsStructureDestroyed(Character owner) =>
        inner.IsStructureDestroyed(owner);

    public IReadOnlyList<TargetPoint> GetTargetPoints(
        Character owner,
        bool includeBrokenParts) =>
        inner.GetTargetPoints(owner, includeBrokenParts) ??
        Array.Empty<TargetPoint>();

    public int ScaleForVerification(int unscaledWholeHp) =>
        Scale(unscaledWholeHp);

    private int Scale(int value) =>
        Mathf.Max(
            0,
            Mathf.RoundToInt(
                Mathf.Max(0, value) * wholeHpMultiplier));
}
