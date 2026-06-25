using UnityEngine;

public enum PartType
{
    HEAD,
    LEFT_HAND,
    RIGHT_HAND,
    LEGS
}

public class BodyPart
{
    public PartType Type;

    // 속도 범위
    public int MinSpeed;
    public int MaxSpeed;

    // 이번 턴 굴린 속도
    public int CurrentSpeed;

    public bool IsBroken;

    public float PartMaxHP;
    public float PartHP;

    public Skill CurrentSkill;

    //--------------------------------

    public BodyPart(
        PartType type,
        int minSpeed,
        int maxSpeed,
        float hp)
    {
        Type = type;

        MinSpeed = minSpeed;
        MaxSpeed = maxSpeed;

        CurrentSpeed = 0;

        IsBroken = false;

        PartMaxHP = hp;
        PartHP = hp;
    }

    public void RollSpeed()
    {
        if (IsBroken)
        {
            CurrentSpeed = 0;
            return;
        }

        CurrentSpeed = Random.Range(MinSpeed, MaxSpeed + 1);
    }

    public void Recover()
    {
        IsBroken = false;
        PartHP = PartMaxHP;
    }
    
    public bool CanUseSkill(Skill skill)
    {
        switch (Type)
        {
            case PartType.LEFT_HAND:
            case PartType.RIGHT_HAND:
                return skill.ActionType == ActionType.NormalAttack ||
                    skill.ActionType == ActionType.Duel;
                    
            case PartType.HEAD:
                return skill.ActionType == ActionType.NormalAttack ||
                    skill.ActionType == ActionType.Duel ||
                    skill.ActionType == ActionType.Preparation;

            case PartType.LEGS:
                return skill.ActionType == ActionType.Preparation;
        }

        return false;
    }
}