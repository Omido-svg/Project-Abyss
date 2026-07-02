using UnityEngine;

public class CharacterResourceController
{
    private readonly Character owner;

    public CombatResourceBank Resources { get; private set; }

    public CharacterResourceController(Character owner)
    {
        this.owner = owner;

        Resources =
            new CombatResourceBank();
    }

    //------------------------------------------------
    // 초기화
    //------------------------------------------------

    public void Reset()
    {
        Resources =
            new CombatResourceBank();
    }

    //------------------------------------------------
    // 위세
    //------------------------------------------------

    public void AddPrestige(int amount)
    {
        if (owner == null)
            return;

        if (owner.RuntimeStatus == null)
            return;

        if (owner.CurrentStatus == null)
            return;

        if (amount <= 0)
            return;

        int finalAmount =
            Mathf.RoundToInt(
                amount * owner.CurrentStatus.prestigeGainMultiplier);

        finalAmount =
            Mathf.Max(1, finalAmount);

        owner.RuntimeStatus.currentPrestige =
            Mathf.Min(
                owner.RuntimeStatus.currentPrestige + finalAmount,
                owner.CurrentStatus.maxPrestige);

        Debug.Log(
            $"{owner.Data.CharacterName} 위세 획득 : +{finalAmount} " +
            $"({owner.RuntimeStatus.currentPrestige}/{owner.CurrentStatus.maxPrestige})");
    }

    //------------------------------------------------
    // 방어도
    //------------------------------------------------

    public void AddBlock(int amount)
    {
        if (owner == null)
            return;

        if (owner.RuntimeStatus == null)
            return;

        if (amount <= 0)
            return;

        owner.RuntimeStatus.currentBlock += amount;

        Debug.Log(
            $"{owner.Data.CharacterName} 방어도 {amount} 획득 " +
            $"현재 방어도 : {owner.RuntimeStatus.currentBlock}");
    }

    public void ClearBlock()
    {
        if (owner == null)
            return;

        if (owner.RuntimeStatus == null)
            return;

        owner.RuntimeStatus.currentBlock = 0;
    }
}