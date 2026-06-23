// 캐릭터1의 기본 스탯 (레벨업, 장비, 증강 등에 의해 변경)
public class CurrentStatus
{
    // 체력
    public int maxHP;

    // 공격 / 방어
    public int attackLevel;
    public int defenseLevel;

    // 흐트러짐
    public int maxStagger;

    // 위세
    public int maxPrestige;

    // 정신력
    public int maxMentality;
    public int minMentality;

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

    //----------------------------------------
    // 기본 생성자
    //----------------------------------------

    public CurrentStatus()
    {

    }

    //----------------------------------------
    // CharacterData로부터 생성
    //----------------------------------------

    public CurrentStatus(CharacterData data)
    {
        maxHP = data.maxHP;

        attackLevel = data.attackLevel;
        defenseLevel = data.defenseLevel;

        maxStagger = data.maxStagger;
        maxPrestige = data.maxPrestige;

        maxMentality = data.maxMentality;
        minMentality = data.minMentality;

        criticalChance = data.criticalChance;
        criticalDamage = data.criticalDamage;

        damageMultiplier = data.damageMultiplier;

        damageReduction = data.damageReduction;
        defensePenetration = data.defensePenetration;

        statusChance = data.statusChance;
        statusResistance = data.statusResistance;

        accuracy = data.accuracy;
        dodgeChance = data.dodgeChance;
    }

    //----------------------------------------
    // 복사 생성자
    //----------------------------------------

    public CurrentStatus(CurrentStatus other)
    {
        maxHP = other.maxHP;

        attackLevel = other.attackLevel;
        defenseLevel = other.defenseLevel;

        maxStagger = other.maxStagger;
        maxPrestige = other.maxPrestige;

        maxMentality = other.maxMentality;
        minMentality = other.minMentality;

        criticalChance = other.criticalChance;
        criticalDamage = other.criticalDamage;

        damageMultiplier = other.damageMultiplier;

        damageReduction = other.damageReduction;
        defensePenetration = other.defensePenetration;

        statusChance = other.statusChance;
        statusResistance = other.statusResistance;

        accuracy = other.accuracy;
        dodgeChance = other.dodgeChance;
    }
}

// 실제 인게임에서 변할 수 있는 스테이터스
public class RuntimeStatus
{
    public int currentHP;
    public int currentStagger;
    public int currentPrestige;
    public int currentMentality;

    public RuntimeStatus(CurrentStatus data)
    {
        currentHP = data.maxHP;
        currentStagger = data.maxStagger;
        currentPrestige = 0;
        currentMentality = 0;
    }
}