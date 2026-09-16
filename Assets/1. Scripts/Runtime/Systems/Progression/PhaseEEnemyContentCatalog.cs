using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Progression/Phase E Enemy Content Catalog",
    fileName = "PhaseEEnemyContentCatalog")]
public sealed class PhaseEEnemyContentCatalog : ScriptableObject
{
    public List<CanonicalNormalEnemyEncounterProfile> NormalEncounters = new();

    [Header("Stage 1 Boss")]
    public int BossPartCount = 5;
    public int BossPartHp = 150;
    public int BossWholeHp = 1500;
    public int BossStaggerMax = 400;
    public int BossStartingEnergy = 2;
    public string NormalPostureSequence = "A,A,B";
    public bool RandomTarget = true;
    public bool FallbackBToNormalWhenEnergyInsufficient = true;
    public CanonicalBossSkillProfile SkillA = new();
    public CanonicalBossSkillProfile SkillB = new();
}
