public class CurrentStatus
{
    // 위세
    public int maxPrestige = 100;

    // 데미지
    public float damageMultiplier = 1f;
    
    // 방어도
    public float defensePenetration = 1;


    public CurrentStatus(CharacterData data)
    {
        maxPrestige = data.maxPrestige;

        damageMultiplier = data.damageMultiplier;
        defensePenetration = data.defensePenetration;
    }
}

// 실제 인게임에서 변할 수 있는 스테이터스
public class RuntimeStatus
{
    // HP
    public int currentHP;
    
    // 방어도
    public int currentDefensePenetration;

    // 위세
    public int currentPrestige;
    
    //--------------------------------
    public RuntimeStatus(CurrentStatus status)
    {
        currentDefensePenetration = 0;
        currentPrestige = 0;
    }
}