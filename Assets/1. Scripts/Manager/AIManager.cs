using System.Collections.Generic;

public class AIManager
{
    private readonly BattleContext battleContext;

    // AI도 ActionBuffer에 넣는다.
    private readonly ActionBuffer actionBuffer;

    //--------------------------------

    public AIManager(
        BattleContext battleContext,
        ActionBuffer actionBuffer)
    {
        this.battleContext = battleContext;
        this.actionBuffer = actionBuffer;
    }

    //--------------------------------

    public void DecideEnemyActions()
    {
        foreach (Enemy enemy in battleContext.Enemies)
        {
            List<ActionSlot> slots = enemy.DecideSlots(battleContext);

            foreach (ActionSlot slot in slots)
            {
                actionBuffer.Add(slot);
            }
        }
    }
}