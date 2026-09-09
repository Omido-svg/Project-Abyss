using System.Collections.Generic;

/// <summary>
/// 이미 계산이 끝난 하나의 피해 영향을 화면에 재생하기 위한 immutable-ish DTO.
/// 전투 로직을 다시 계산하지 않고 DamageContext의 factual snapshot만 복사한다.
/// </summary>
public sealed class TargetImpactPresentation
{
    private readonly List<int> hitDamages = new();
    private readonly List<int> characterHpHitDamages = new();
    private readonly List<int> partHpHitDamages = new();

    public Character Target { get; private set; }
    public BodyPart TargetPart { get; private set; }
    public DamageContext DamageContext { get; private set; }
    public bool IsPrimary { get; private set; }

    public int DisplayDamage { get; private set; }
    public int FinalHpDamage { get; private set; }
    public int PartHpDamage { get; private set; }
    public int DirectHpDamage { get; private set; }

    public bool WasCritical { get; private set; }
    public bool WasKilled { get; private set; }
    public bool BrokePart { get; private set; }
    public bool WeakenedPart { get; private set; }

    public bool HasCharacterHpSnapshot { get; private set; }
    public int CharacterHpBefore { get; private set; }
    public int CharacterHpAfter { get; private set; }
    public int CharacterMaxHp { get; private set; }
    public bool CharacterWasDeadBefore { get; private set; }
    public bool CharacterWasDeadAfter { get; private set; }

    public bool HasPartSnapshot { get; private set; }
    public int PartHpBefore { get; private set; }
    public int PartHpAfter { get; private set; }
    public BodyPartState PartStateBefore { get; private set; }
    public BodyPartState PartStateAfter { get; private set; }

    public IReadOnlyList<int> HitDamages => hitDamages;
    public IReadOnlyList<int> CharacterHpHitDamages => characterHpHitDamages;
    public IReadOnlyList<int> PartHpHitDamages => partHpHitDamages;

    public int HitCount =>
        System.Math.Max(
            1,
            System.Math.Max(
                hitDamages.Count,
                System.Math.Max(
                    characterHpHitDamages.Count,
                    partHpHitDamages.Count)));

    public static TargetImpactPresentation FromDamageContext(
        DamageContext context,
        bool isPrimary,
        IReadOnlyList<int> displayHitDamages,
        IReadOnlyList<int> characterHitDamages,
        IReadOnlyList<int> partHitDamages)
    {
        if (context?.Target == null)
            return null;

        TargetImpactPresentation result =
            new TargetImpactPresentation
            {
                Target = context.Target,
                TargetPart = context.TargetPart,
                DamageContext = context,
                IsPrimary = isPrimary,

                DisplayDamage =
                    System.Math.Max(
                        0,
                        context.GetDisplayDamage()),

                FinalHpDamage =
                    System.Math.Max(
                        0,
                        context.FinalHpDamage),

                PartHpDamage =
                    System.Math.Max(
                        0,
                        context.PartHpDamage),

                DirectHpDamage =
                    System.Math.Max(
                        0,
                        context.DirectHpDamage),

                WasCritical = context.WasCritical,
                WasKilled = context.WasKilled,
                BrokePart = context.BrokePart,
                WeakenedPart = context.WeakenedPart,

                HasCharacterHpSnapshot = true,
                CharacterHpBefore =
                    System.Math.Max(
                        0,
                        context.TargetHpBefore),
                CharacterHpAfter =
                    System.Math.Max(
                        0,
                        context.TargetHpAfter),
                CharacterMaxHp =
                    System.Math.Max(
                        1,
                        context.Target.MaxCombatHP),
                CharacterWasDeadBefore =
                    context.TargetWasDeadBefore,
                CharacterWasDeadAfter =
                    context.TargetWasDeadAfter,

                HasPartSnapshot =
                    context.HasTargetPartSnapshot,
                PartHpBefore =
                    System.Math.Max(
                        0,
                        context.TargetPartHpBefore),
                PartHpAfter =
                    System.Math.Max(
                        0,
                        context.TargetPartHpAfter),
                PartStateBefore =
                    context.TargetPartStateBefore,
                PartStateAfter =
                    context.TargetPartStateAfter
            };

        CopyNonNegative(
            displayHitDamages,
            result.hitDamages);

        CopyNonNegative(
            characterHitDamages,
            result.characterHpHitDamages);

        CopyNonNegative(
            partHitDamages,
            result.partHpHitDamages);

        return result;
    }

    public int GetDamageForHitIndex(int hitIndex)
    {
        return GetValueForHitIndex(
            hitDamages,
            hitIndex);
    }

    public int GetCharacterHpDamageForHitIndex(
        int hitIndex)
    {
        return GetValueForHitIndex(
            characterHpHitDamages,
            hitIndex);
    }

    public int GetPartHpDamageForHitIndex(
        int hitIndex)
    {
        return GetValueForHitIndex(
            partHpHitDamages,
            hitIndex);
    }

    public bool IsFinalHit(int hitIndex)
    {
        return hitIndex >= HitCount - 1;
    }

    private static int GetValueForHitIndex(
        IReadOnlyList<int> values,
        int hitIndex)
    {
        if (values == null || values.Count == 0)
            return 0;

        if (hitIndex < 0)
            return values[0];

        return hitIndex < values.Count
            ? values[hitIndex]
            : 0;
    }

    private static void CopyNonNegative(
        IReadOnlyList<int> source,
        List<int> destination)
    {
        if (source == null || destination == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(
                System.Math.Max(
                    0,
                    source[i]));
        }
    }
}
