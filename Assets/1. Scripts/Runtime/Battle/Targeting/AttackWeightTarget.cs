public sealed class AttackWeightTarget
{
    public AttackWeightTarget(
        int weightIndex,
        Character targetCharacter,
        BodyPart targetPart,
        bool isPrimary)
    {
        WeightIndex =
            UnityEngine.Mathf.Max(
                0,
                weightIndex);

        TargetCharacter =
            targetCharacter;

        TargetPart =
            targetPart;

        IsPrimary =
            isPrimary;
    }

    public int WeightIndex { get; }
    public Character TargetCharacter { get; }
    public BodyPart TargetPart { get; }
    public bool IsPrimary { get; }

    public TargetPoint TargetPoint =>
        new TargetPoint(
            TargetCharacter,
            TargetPart);

    public bool IsValid =>
        TargetCharacter != null &&
        !TargetCharacter.IsDead;

    public override string ToString()
    {
        string role =
            IsPrimary
                ? "PRIMARY"
                : $"SECONDARY #{WeightIndex}";

        return
            $"{role} / {TargetPoint}";
    }
}

public sealed class AttackWeightHitResult
{
    public AttackWeightHitResult(
        int exchangeIndex,
        AttackWeightTarget target,
        DamageContext damageContext)
    {
        ExchangeIndex =
            UnityEngine.Mathf.Max(
                0,
                exchangeIndex);

        Target =
            target;

        DamageContext =
            damageContext;
    }

    public int ExchangeIndex { get; }
    public AttackWeightTarget Target { get; }
    public DamageContext DamageContext { get; }

    public int Damage =>
        DamageContext?.GetDisplayDamage() ?? 0;
}
