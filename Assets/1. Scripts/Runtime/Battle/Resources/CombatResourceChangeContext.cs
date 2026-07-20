
using System;

public enum CombatResourceChangeReason
{
    Initialization = 0,
    TurnRefill = 1,
    SkillCost = 2,
    SkillEffect = 3,
    Debug = 4,
    Restore = 5,
    External = 6
}

[Serializable]
public sealed class CombatResourceChangeContext
{
    public Character Owner;
    public string ResourceKey;
    public int Before;
    public int After;
    public int Maximum;
    public CombatResourceChangeReason Reason;
    public BattleAction SourceAction;
    public Skill SourceSkill;

    public int Delta => After - Before;
    public bool IsSpend => Delta < 0;
    public bool IsGain => Delta > 0;
}
