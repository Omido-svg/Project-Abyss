using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterResourceController
{
    private readonly Character owner;

    public CombatResourceBank Resources { get; private set; }

    public CharacterResourceController(Character owner)
    {
        this.owner = owner;
        Resources = new CombatResourceBank();
    }

    public void Reset()
    {
        IEnumerable<CombatResourceDefinition> definitions =
            owner?.Data?.InitialResources;

        if (Resources == null)
            Resources = new CombatResourceBank();

        Resources.Reset(definitions);
    }

    public int GetResource(string key)
    {
        return Resources?.Get(key) ?? 0;
    }

    public void SetResource(
        string key,
        int value,
        int max = int.MaxValue)
    {
        if (Resources == null)
            return;

        if (max == int.MaxValue)
            Resources.Set(key, value);
        else
            Resources.Set(key, value, max);
    }

    public void AddResource(
        string key,
        int amount,
        int max = int.MaxValue)
    {
        Resources?.Add(key, amount, max);
    }

    public bool TryConsumeResource(
        string key,
        int amount)
    {
        return Resources != null &&
               Resources.TryConsume(key, amount);
    }

    public void AddPrestige(int amount)
    {
        if (owner?.RuntimeStatus == null ||
            owner.CurrentStatus == null ||
            amount <= 0)
        {
            return;
        }

        int finalAmount = Mathf.RoundToInt(
            amount * owner.CurrentStatus.prestigeGainMultiplier);

        finalAmount = Mathf.Max(1, finalAmount);

        owner.RuntimeStatus.currentPrestige =
            Mathf.Min(
                owner.RuntimeStatus.currentPrestige + finalAmount,
                owner.CurrentStatus.maxPrestige);

        Debug.Log(
            $"{owner.Data?.CharacterName ?? owner.name} 위세 획득 : " +
            $"+{finalAmount} " +
            $"({owner.RuntimeStatus.currentPrestige}/" +
            $"{owner.CurrentStatus.maxPrestige})");
    }

    public bool TryConsumePrestige(int amount)
    {
        if (owner?.RuntimeStatus == null || amount <= 0)
            return amount <= 0;

        if (owner.RuntimeStatus.currentPrestige < amount)
            return false;

        owner.RuntimeStatus.currentPrestige -= amount;
        return true;
    }

    public void SetPrestige(int value)
    {
        if (owner?.RuntimeStatus == null)
            return;

        int max = owner.CurrentStatus?.maxPrestige ?? 0;
        owner.RuntimeStatus.currentPrestige =
            Mathf.Clamp(value, 0, Mathf.Max(0, max));
    }

    public void AddBlock(int amount)
    {
        if (owner?.RuntimeStatus == null || amount <= 0)
            return;

        owner.RuntimeStatus.currentBlock += amount;

        Debug.Log(
            $"{owner.Data?.CharacterName ?? owner.name} 방어도 {amount} 획득 " +
            $"현재 방어도 : {owner.RuntimeStatus.currentBlock}");
    }

    public void ClearBlock()
    {
        if (owner?.RuntimeStatus == null)
            return;

        owner.RuntimeStatus.currentBlock = 0;
    }
}
