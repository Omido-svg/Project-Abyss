using UnityEngine;

public class CharacterLifeController
{
    private readonly Character owner;
    private readonly CharacterMechanicController mechanicController;

    public bool IsDead { get; private set; }

    public CharacterLifeController(
        Character owner,
        CharacterMechanicController mechanicController)
    {
        this.owner = owner;
        this.mechanicController = mechanicController;
    }

    public void Reset()
    {
        IsDead = false;
    }

    public void CheckDead()
    {
        if (CanDie())
        {
            Die();
        }
    }

    public bool CanDie()
    {
        if (owner == null)
            return false;

        if (mechanicController != null &&
            !mechanicController.CanOwnerDie())
        {
            return false;
        }

        if (owner.RuntimeStatus != null &&
            owner.RuntimeStatus.currentHP <= 0)
        {
            return true;
        }

        bool allBroken = true;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            if (!part.IsBroken)
            {
                allBroken = false;
                break;
            }
        }

        return allBroken;
    }

    public void Die()
    {
        if (owner == null)
            return;

        if (IsDead)
            return;

        IsDead = true;

        Debug.Log($"{owner.Data.CharacterName} 사망");

        owner.BattleEvent?.RaiseCharacterDeath(owner);
    }
}