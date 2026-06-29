using UnityEngine;

[CreateAssetMenu(menuName = "Character/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character")]
    public string CharacterName;

    // HP
    public int maxHP = 100;

    // 위세
    public int maxPrestige = 100;

    // 데미지 보정
    public float damageMultiplier = 1f;

    // 방어도
    public float defensePenetration = 0f;
    
    [Header("Speed")]
    public int minSpeed = 3;
    public int maxSpeed = 8;
}