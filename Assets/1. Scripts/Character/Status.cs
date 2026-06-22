// 캐릭터1의 기본 스탯 (레벨업, 장비, 증강 등에 의해 변경)
public class BaseStatus
{
    // 체력
    public int maxHP;

    // 공격 / 방어
    public int attackLevel;
    public int defenseLevel;

    // 흐트러짐
    public int maxStagger = 100;

    // 위세
    public int maxPrestige = 100;

    // 정신력
    public int maxMentality = 45;
    public int minMentality = -45;

    // 치명타
    public float criticalChance;
    public float criticalDamage;

    // 최종 피해 증가
    public float damageMultiplier;

    // 피해 감소
    public float damageReduction;

    // 방어 무시
    public float defensePenetration;

    // 상태이상
    public float statusChance;
    public float statusResistance;

    // 명중 / 회피
    public float accuracy;
    public float dodgeChance;
    
    public BaseStatus(
        int maxHP,
        int attackLevel,
        int defenseLevel)
    {
        this.maxHP = maxHP;
        this.attackLevel = attackLevel;
        this.defenseLevel = defenseLevel;

        maxStagger = 100;
        maxPrestige = 100;
        maxMentality = 45;
        minMentality = -45;
    }
}

// 전투 중 계속 변경되는 값
public class RuntimeStatus
{
    // 현재 체력
    public int currentHP;

    // 현재 흐트러짐
    public int currentStagger;

    // 현재 위세 게이지
    public int currentPrestige;

    // 현재 정신력
    public int currentMentality;
    
    public RuntimeStatus(BaseStatus baseStatus)
    {
        currentHP = baseStatus.maxHP;
        currentStagger = baseStatus.maxStagger;
        currentPrestige = 0;
        currentMentality = 0;
    }
}