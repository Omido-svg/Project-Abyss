using System;

[Serializable]
public sealed class DamageSnapshot
{
    public DamageStage Stage;
    public int Damage;

    public int TargetHp;
    public int TargetPartHp;

    public int GuardValue;

    public bool HasTargetPart;
    public BodyPartState TargetPartState;

    public DamageSnapshot(
        DamageStage stage,
        int damage,
        Character target,
        BodyPart targetPart,
        int guardValue)
    {
        Stage = stage;
        Damage = damage;

        TargetHp = target?.CurrentHP ?? 0;
        GuardValue = guardValue;

        HasTargetPart = targetPart != null;

        if (targetPart != null)
        {
            TargetPartHp =
                UnityEngine.Mathf.Max(
                    0,
                    UnityEngine.Mathf.RoundToInt(
                        targetPart.PartHP));

            TargetPartState = targetPart.State;
        }
    }
}
