using System.Collections.Generic;
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

    // 현재 장착(선택)된 스킬
    public Skill CurrentSkill;

    // 이 부위가 사용할 수 있는 스킬
    public List<Skill> AvailableSkills { get; }

    //--------------------------------

    public BodyPart(
        PartType type,
        int minSpeed,
        int maxSpeed,
        float hp,
        IEnumerable<Skill> availableSkills = null)
    {
        Type = type;

        MinSpeed = minSpeed;
        MaxSpeed = maxSpeed;

        CurrentSpeed = 0;

        IsBroken = false;

        PartMaxHP = hp;
        PartHP = hp;

        AvailableSkills = availableSkills != null
            ? new List<Skill>(availableSkills)
            : new List<Skill>();
    }

    //--------------------------------

    public void RollSpeed()
    {
        if (IsBroken)
        {
            CurrentSpeed = 0;
            return;
        }

        CurrentSpeed = Random.Range(MinSpeed, MaxSpeed + 1);
    }

    //--------------------------------

    public void Recover()
    {
        IsBroken = false;
        PartHP = PartMaxHP;
    }

    //--------------------------------

    public bool CanUseSkill(Skill skill)
    {
        return AvailableSkills.Contains(skill);
    }

    //--------------------------------

    public void AddSkill(Skill skill)
    {
        if (skill == null)
            return;

        if (!AvailableSkills.Contains(skill))
            AvailableSkills.Add(skill);
    }

    //--------------------------------

    public void RemoveSkill(Skill skill)
    {
        AvailableSkills.Remove(skill);
    }
}