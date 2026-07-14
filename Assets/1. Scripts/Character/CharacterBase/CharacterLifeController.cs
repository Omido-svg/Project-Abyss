using UnityEngine;

public sealed class CharacterLifeController
{
    private readonly Character owner;
    private readonly CharacterMechanicController mechanicController;
    private readonly CharacterCombatState combatState;

    public bool IsDead =>
        combatState?.IsDead ?? false;

    public CharacterLifeController(
        Character owner,
        CharacterMechanicController mechanicController)
        : this(
            owner,
            mechanicController,
            new CharacterCombatState())
    {
    }

    public CharacterLifeController(
        Character owner,
        CharacterMechanicController mechanicController,
        CharacterCombatState combatState)
    {
        this.owner = owner;
        this.mechanicController = mechanicController;
        this.combatState = combatState;
    }

    public void Reset()
    {
        combatState?.MarkAlive();
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
        if (owner == null || IsDead)
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

        CharacterTargetModel targetModel =
            owner.Targeting;

        return targetModel != null &&
               targetModel.IsStructureDestroyed(owner);
    }

    public void Die(
        Character killer = null,
        BattleAction sourceAction = null,
        DamageContext damageContext = null)
    {
        if (owner == null || IsDead)
            return;

        combatState?.MarkDead();

        Debug.Log(
            $"{owner.Data?.CharacterName ?? owner.name} 사망");

        // 표준 DamageManager 처리 중에는 최종 스냅샷을 만든 뒤
        // DamageEventDispatcher가 Death/Kill 이벤트를 한 번만 발행한다.
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
