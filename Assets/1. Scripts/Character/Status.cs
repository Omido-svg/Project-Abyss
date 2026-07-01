using UnityEngine;

public class CurrentStatus
{
    // 위세
    public int maxPrestige = 100;

    // 속도
    public int minSpeed;
    public int maxSpeed;

    // 공격력 보너스
    public int flatDamageBonus = 0;

    // 데미지 배율
    public float damageMultiplier = 1f;

    // 방어력
    // 고정 피해 감소량
    public int defense = 0;

    // 방어 무시율
    // 0.2f = 방어력/방어도 20% 무시
    public float defensePenetrationRate = 0f;

    // 위세 획득량 배율
    public float prestigeGainMultiplier = 1f;

    public CurrentStatus(CharacterData data)
    {
        maxPrestige = data.maxPrestige;

        damageMultiplier = data.damageMultiplier;

        // CharacterData 쪽 변수 이름이 아직 defensePenetration이면
        // 일단 이렇게 받아도 됨.
        // 나중에는 data.defensePenetrationRate로 이름 바꾸는 걸 추천.
        defensePenetrationRate =
            Mathf.Clamp01(data.defensePenetration);

        minSpeed = data.minSpeed;
        maxSpeed = data.maxSpeed;
    }
}

// 실제 인게임에서 변할 수 있는 스테이터스
public class RuntimeStatus
{
    // HP
    public int currentHP;

    // 전투 중 임시 방어도 / 보호막
    public int currentBlock;

    // 위세
    public int currentPrestige;

    public RuntimeStatus(CurrentStatus status)
    {
        currentBlock = 0;
        currentPrestige = 0;
    }
}