using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Character/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character Name")]
    public string CharacterName;
    
    [Header("Status")]
    public int maxHP = 100;

    public int attackLevel = 1;
    public int defenseLevel = 1;

    public int maxStagger = 100;
    public int maxPrestige = 100;

    public int maxMentality = 45;
    public int minMentality = -45;

    public float criticalChance = 0.1f;
    public float criticalDamage = 1.4f;

    public float damageMultiplier = 1f;

    public float damageReduction = 0;

    public float defensePenetration = 0;

    public float statusChance = 0;

    public float statusResistance = 0;

    public float accuracy = 100;

    public float dodgeChance = 0;
}