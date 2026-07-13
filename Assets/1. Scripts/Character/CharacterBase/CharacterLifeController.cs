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

    public void CheckDead(
        Character killer = null,
        BattleAction sourceAction = null)
    {
        if (!CanDie())
            return;

        DamageContext activeContext =
            owner?.ActiveDamageContext;

        Die(
            activeContext?.Attacker ?? killer,
            activeContext?.Action ?? sourceAction,
            activeContext);
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

        ICombatTargetModel targetModel =
            owner.TargetModel;

        if (targetModel == null)
            return false;

        return targetModel.IsStructureDestroyed(
            owner);
    }

    public void Die(
        Character killer = null,
        BattleAction sourceAction = null,
        DamageContext damageContext = null)
    {
        if (owner == null || IsDead)
            return;

        IsDead = true;

        Debug.Log(
            $"{owner.Data.CharacterName} 사망");

        // 표준 DamageManager 처리 중이라면
        // HP/부위 적용이 모두 끝난 뒤 DamageEventDispatcher가
        // Death/Kill 이벤트를 한 번만 발행한다.
        if (damageContext != null ||
            owner.IsDamageResolutionInProgress)
        {
            return;
        }

        KillEventContext eventContext =
            KillEventContext.External(
                killer,
                owner,
                sourceAction);

        owner.BattleEvent?.RaiseCharacterDeath(
            eventContext);

        owner.BattleEvent?.RaiseKill(
            eventContext);
    }
}
