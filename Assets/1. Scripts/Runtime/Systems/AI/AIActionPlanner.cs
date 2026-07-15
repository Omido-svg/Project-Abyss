using System.Collections.Generic;
using UnityEngine;

public sealed class AIPlanningState
{
    private readonly List<ActionSlot> plannedSlots = new();
    private int plannedPrestigeCount;

    public AIPlanningState(
        Enemy owner,
        BattleContext context)
    {
        Owner = owner;
        Context = context;
    }

    public Enemy Owner { get; }
    public BattleContext Context { get; }
    public IReadOnlyList<ActionSlot> PlannedSlots => plannedSlots;

    public bool CanPlanPrestige(
        Skill skill)
    {
        if (skill == null ||
            skill.ActionType != ActionType.Prestige)
        {
            return false;
        }

        return skill.PrestigeUsePolicy switch
        {
            PrestigeUsePolicy.None => false,
            PrestigeUsePolicy.OncePerTurn =>
                plannedPrestigeCount == 0,
            PrestigeUsePolicy.Unlimited => true,
            _ => false
        };
    }

    public void Register(
        ActionSlot slot)
    {
        if (slot == null)
            return;

        plannedSlots.Add(slot);

        if (slot.Skill?.ActionType ==
            ActionType.Prestige)
        {
            plannedPrestigeCount++;
        }
    }

    public int CountPlannedSkill(
        Skill skill)
    {
        if (skill == null)
            return 0;

        int count = 0;

        foreach (ActionSlot slot in plannedSlots)
        {
            if (slot?.Skill == skill)
                count++;
        }

        return count;
    }

    public int CountActionType(
        ActionType actionType)
    {
        int count = 0;

        foreach (ActionSlot slot in plannedSlots)
        {
            if (slot?.Skill?.ActionType == actionType)
                count++;
        }

        return count;
    }
}

public sealed class AIActionPlanner
{
    private readonly AISlotPlanner slotPlanner;
    private readonly AISkillSelector skillSelector;

    public AIActionPlanner(
        AISlotPlanner slotPlanner,
        AISkillSelector skillSelector)
    {
        this.slotPlanner = slotPlanner;
        this.skillSelector = skillSelector;
    }

    public List<ActionSlot> PlanEnemy(
        Enemy enemy,
        BattleContext context)
    {
        List<ActionSlot> result = new();

        if (enemy == null ||
            enemy.IsDead ||
            context?.battleManager?.SpeedManager == null)
        {
            return result;
        }

        AIPlanningState state =
            new AIPlanningState(
                enemy,
                context);

        List<AIActionSource> sources =
            slotPlanner.CreateSources(enemy);

        foreach (AIActionSource source in sources)
        {
            if (source == null || !source.IsValid)
                continue;

            for (int actionIndex = 0;
                 actionIndex < source.MaxSlots;
                 actionIndex++)
            {
                AISkillDecision decision =
                    skillSelector.SelectSkill(
                        state,
                        source,
                        actionIndex);

                if (decision == null ||
                    !decision.IsValid)
                {
                    continue;
                }

                ActionSlot slot =
                    CreateSlot(
                        context,
                        source,
                        actionIndex,
                        decision);

                if (slot == null)
                    continue;

                state.Register(slot);
                result.Add(slot);

                Debug.Log(
                    $"[AI PLAN] " +
                    $"Owner={GetCharacterName(enemy)}, " +
                    $"Part={GetPartName(source.Part)}, " +
                    $"Index={actionIndex}, " +
                    $"Skill={decision.Skill.SkillName}, " +
                    $"Target={decision.TargetPoint}, " +
                    $"Speed={slot.Speed}, " +
                    $"Score={decision.Score:0.0}");
            }
        }

        return result;
    }

    private ActionSlot CreateSlot(
        BattleContext context,
        AIActionSource source,
        int actionIndex,
        AISkillDecision decision)
    {
        if (context?.battleManager?.SpeedManager == null ||
            source?.Owner == null ||
            decision?.Skill == null ||
            !decision.TargetPoint.IsValid)
        {
            return null;
        }

        return new ActionSlot
        {
            Owner = source.Owner,
            Part = source.Part,
            Skill = decision.Skill,
            TargetCharacter =
                decision.TargetPoint.Character,
            TargetPart =
                decision.TargetPoint.Part,
            Speed = context.battleManager
                .SpeedManager
                .GetSpeed(
                    source.Owner,
                    source.Part),
            Phase = decision.Skill.DefaultPhase,
            ActionIndex = actionIndex,
            TargetSlot = null
        };
    }

    private string GetCharacterName(
        Character character)
    {
        if (character == null)
            return "NULL";

        return character.Data == null
            ? character.name
            : character.Data.CharacterName;
    }

    private string GetPartName(
        BodyPart part)
    {
        return part == null
            ? "NONE"
            : part.Type.ToString();
    }
}
