using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Character/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character")]
    public string CharacterName;

    [Header("Target Model")]
    public CharacterTargetMode TargetMode =
        CharacterTargetMode.Auto;

    [Min(1)]
    public int SingleHpMax = 1;

    [Header("Prestige")]
    [Min(0)]
    public int maxPrestige = 100;

    [Header("Damage")]
    [Min(0f)]
    public float damageMultiplier = 1f;

    [Range(0f, 1f)]
    public float defensePenetration = 0f;

    [Header("Speed")]
    public int minSpeed = 3;
    public int maxSpeed = 8;

    [Header("Initial Custom Resources")]
    public List<CombatResourceDefinition> InitialResources = new();

#if UNITY_EDITOR
    private void OnValidate()
    {
        SingleHpMax = Mathf.Max(1, SingleHpMax);
        maxPrestige = Mathf.Max(0, maxPrestige);
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
        defensePenetration = Mathf.Clamp01(defensePenetration);

        if (maxSpeed < minSpeed)
            maxSpeed = minSpeed;

        if (InitialResources == null)
            InitialResources = new List<CombatResourceDefinition>();
    }
#endif
}
