
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterResourceController
{
    private readonly Character owner;

    public CombatResourceBank Resources { get; private set; }

    public int CurrentEnergy =>
        Resources?.Get(CombatResourceKeys.Energy) ?? 0;

    public int MaxEnergy =>
        Resources?.GetMax(CombatResourceKeys.Energy) ?? 0;

    public CharacterResourceController(Character owner)
    {
        this.owner = owner;
        Resources = new CombatResourceBank();
    }

    public void Reset()
    {
        IEnumerable<CombatResourceDefinition> definitions =
            owner?.Data?.InitialResources;

        Resources ??= new CombatResourceBank();
        Resources.Reset(definitions);
    }

    /// <summary>
    /// CurrentStatus까지 빌드된 뒤 호출한다. 에너지는 시스템 예약 자원이므로
    /// CharacterData.InitialResources의 같은 키보다 CurrentStatus.maxEnergy가 우선한다.
    /// </summary>
    public void ConfigureEnergy(
        int maximum,
        bool fillToMaximum)
    {
        Resources ??= new CombatResourceBank();

        int safeMaximum = Mathf.Max(0, maximum);
        int before = Resources.Get(CombatResourceKeys.Energy);
        int after = fillToMaximum
            ? safeMaximum
            : Mathf.Clamp(before, 0, safeMaximum);

        Resources.Configure(
            CombatResourceKeys.Energy,
            after,
            safeMaximum);

        PublishResourceChange(
            CombatResourceKeys.Energy,
            before,
            after,
            safeMaximum,
            CombatResourceChangeReason.Initialization,
            null,
            null);
    }

    public void RestoreEnergyToFull()
    {
        SetEnergy(
            MaxEnergy,
            CombatResourceChangeReason.TurnRefill,
            null,
            null);
    }

    public bool CanAffordEnergy(int amount)
    {
        return amount <= 0 || CurrentEnergy >= amount;
    }

    public bool TryConsumeEnergy(
        int amount,
        BattleAction sourceAction = null,
        Skill sourceSkill = null)
    {
        amount = Mathf.Max(0, amount);

        if (amount == 0)
            return true;

        if (!CanAffordEnergy(amount))
            return false;

        int before = CurrentEnergy;
        bool consumed = Resources != null &&
                        Resources.TryConsume(
                            CombatResourceKeys.Energy,
                            amount);

        if (!consumed)
            return false;

        int after = CurrentEnergy;

        PublishResourceChange(
            CombatResourceKeys.Energy,
            before,
            after,
            MaxEnergy,
            CombatResourceChangeReason.SkillCost,
            sourceAction,
            sourceSkill);

        return true;
    }

    public void SetEnergy(
        int value,
        CombatResourceChangeReason reason =
            CombatResourceChangeReason.External,
        BattleAction sourceAction = null,
        Skill sourceSkill = null)
    {
        Resources ??= new CombatResourceBank();

        int before = CurrentEnergy;
        int maximum = Mathf.Max(0, MaxEnergy);
        Resources.Set(
            CombatResourceKeys.Energy,
            value,
            maximum);

        int after = CurrentEnergy;

        PublishResourceChange(
            CombatResourceKeys.Energy,
            before,
            after,
            maximum,
            reason,
            sourceAction,
            sourceSkill);
    }

    public void AddEnergy(
        int amount,
        CombatResourceChangeReason reason =
            CombatResourceChangeReason.SkillEffect,
        BattleAction sourceAction = null,
        Skill sourceSkill = null)
    {
        SetEnergy(
            CurrentEnergy + amount,
            reason,
            sourceAction,
            sourceSkill);
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

        if (string.Equals(
                key,
                CombatResourceKeys.Energy,
                System.StringComparison.OrdinalIgnoreCase))
        {
            SetEnergy(value);
            return;
        }

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
        if (string.Equals(
                key,
                CombatResourceKeys.Energy,
                System.StringComparison.OrdinalIgnoreCase))
        {
            AddEnergy(amount);
            return;
        }

        Resources?.Add(key, amount, max);
    }

    public bool TryConsumeResource(
        string key,
        int amount)
    {
        if (string.Equals(
                key,
                CombatResourceKeys.Energy,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return TryConsumeEnergy(amount);
        }

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
            $"{owner.Data?.CharacterName ?? owner.name} 가드 {amount} 획득 " +
            $"현재 가드 : {owner.RuntimeStatus.currentBlock}");
    }

    public void ClearBlock()
    {
        if (owner?.RuntimeStatus == null)
            return;

        owner.RuntimeStatus.currentBlock = 0;
    }

    private void PublishResourceChange(
        string key,
        int before,
        int after,
        int maximum,
        CombatResourceChangeReason reason,
        BattleAction sourceAction,
        Skill sourceSkill)
    {
        if (before == after &&
            reason != CombatResourceChangeReason.Initialization)
        {
            return;
        }

        CombatResourceChangeContext context =
            new CombatResourceChangeContext
            {
                Owner = owner,
                ResourceKey = key,
                Before = before,
                After = after,
                Maximum = Mathf.Max(0, maximum),
                Reason = reason,
                SourceAction = sourceAction,
                SourceSkill = sourceSkill
            };

        owner?.BattleEvent?
            .RaiseCombatResourceChanged(context);

        if (key == CombatResourceKeys.Energy)
        {
            Debug.Log(
                $"[Energy] " +
                $"Owner={owner?.Data?.CharacterName ?? owner?.name}, " +
                $"Reason={reason}, {before}->{after}/{maximum}");
        }
    }
}
