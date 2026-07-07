using System.Collections.Generic;

public class AIManager
{
    private readonly BattleContext battleContext;
    private readonly ActionManager actionManager;

    public AIManager(
        BattleContext battleContext,
        ActionManager actionManager)
    {
        this.battleContext = battleContext;
        this.actionManager = actionManager;
    }

    public void DecideEnemyActions()
    {
        foreach (Enemy enemy in battleContext.Enemies)
        {
            List<ActionSlot> slots = enemy.DecideSlots(battleContext);

            foreach (ActionSlot slot in slots)
            {
                actionManager.AddOrReplaceSlot(slot);
            }
        }
    }
}