using System;
using System.Collections.Generic;
using UnityEngine;

public enum EmotionAugmentRuntimeReadiness
{
    RuntimeConnected = 0,
    DataOnlyPendingRuntime = 1,
    DesignValuePending = 2
}

[Serializable]
public sealed class CanonicalNormalEnemyEncounterProfile
{
    [Range(1, 3)] public int Stage = 1;
    [Range(3, 4)] public int EnemyCount = 3;
    [Min(1)] public int HpPerEnemy = 135;
    [Min(1)] public int StaggerMax = 100;
    [Min(1)] public int ActionSlotsPerEnemy = 1;
    [Min(1)] public int RollsPerAction = 3;

    [Header("HP Resistance type counts")]
    [Min(0)] public int WeakTypeCount;
    [Min(0)] public int NeutralTypeCount = 3;
    [Min(0)] public int ResistantTypeCount;
}

[Serializable]
public sealed class CanonicalBossSkillProfile
{
    public string SkillKey;
    [Min(1)] public int RollCount = 1;
    [Min(0)] public int BasePower;
    public bool Duel = true;
    public bool CanBreakPart;
}

