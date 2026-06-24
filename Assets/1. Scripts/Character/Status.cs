// 캐릭터1의 기본 스탯 (레벨업, 장비, 증강 등에 의해 변경)
public class CurrentStatus
{
    // 위세
    public int maxPrestige;

    // 데미지
    public float damageMultiplier = 1f;
    public float defensePenetration;

    public CurrentStatus() { }

    public CurrentStatus(CharacterData data)
    {
        maxPrestige = data.maxPrestige;

        damageMultiplier = data.damageMultiplier;
        defensePenetration = data.defensePenetration;
    }

    public CurrentStatus(CurrentStatus other)
    {
        maxPrestige = other.maxPrestige;

        damageMultiplier = other.damageMultiplier;
        defensePenetration = other.defensePenetration;
    }
}

// 실제 인게임에서 변할 수 있는 스테이터스
public class RuntimeStatus
{
    // HP
    public int currentHP;
    
    // 방어도
    public int currentBlock;

    // 위세
    public int currentPrestige;

    //--------------------------------
    public RuntimeStatus(CurrentStatus status)
    {
        currentBlock = 0;
        currentPrestige = 0;
    }
}