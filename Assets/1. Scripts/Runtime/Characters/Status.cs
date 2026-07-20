using UnityEngine;

public class CurrentStatus
{
    public int maxPrestige = 100;
    public int maxEnergy = 2;

    public int minSpeed;
    public int maxSpeed;

    public int flatDamageBonus;
    public float damageMultiplier = 1f;

    public int defense;
    public float defensePenetrationRate;

    public float prestigeGainMultiplier = 1f;

    public CurrentStatus(CharacterData data)
    {
        if (data == null)
        {
            minSpeed = 0;
            maxSpeed = 0;
            return;
        }

        maxPrestige = Mathf.Max(0, data.maxPrestige);
        maxEnergy = Mathf.Max(0, data.maxEnergy);
        damageMultiplier = Mathf.Max(0f, data.damageMultiplier);
        defensePenetrationRate =
            Mathf.Clamp01(data.defensePenetration);

        minSpeed = data.minSpeed;
        maxSpeed = Mathf.Max(
            minSpeed,
            data.maxSpeed);
    }

    public void Clamp()
    {
        maxPrestige = Mathf.Max(0, maxPrestige);
        maxEnergy = Mathf.Max(0, maxEnergy);
        maxSpeed = Mathf.Max(minSpeed, maxSpeed);
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
        defense = Mathf.Max(0, defense);
        defensePenetrationRate =
            Mathf.Clamp01(defensePenetrationRate);
        prestigeGainMultiplier =
            Mathf.Max(0f, prestigeGainMultiplier);
    }
}

public class RuntimeStatus
{
    public int currentHP;
    public int currentBlock;
    public int currentPrestige;

    public RuntimeStatus(CurrentStatus status)
    {
        Reset(status);
    }

    public void Reset(CurrentStatus status)
    {
        currentHP = 0;
        currentBlock = 0;
        currentPrestige = 0;

        Clamp(status, int.MaxValue);
    }

    public void Clamp(
        CurrentStatus status,
        int maxHP)
    {
        currentHP = Mathf.Clamp(
            currentHP,
            0,
            Mathf.Max(0, maxHP));

        currentBlock = Mathf.Max(0, currentBlock);

        int maxPrestige =
            status == null
                ? 0
                : Mathf.Max(0, status.maxPrestige);

        currentPrestige = Mathf.Clamp(
            currentPrestige,
            0,
            maxPrestige);
    }
}