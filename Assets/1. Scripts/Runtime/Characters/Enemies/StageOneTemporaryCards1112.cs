using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Battle-scoped cards outside the equipped loadout. No source assets are edited.</summary>
public sealed class StageOneTemporaryCards1112
{
    private Skill sealedSkill;
    private Character sealedOwner;
    private StageOneEnemy1112Mechanic confessionSource;
    private StageOneTemporarySkill1112 confession, cooling;
    public void Clear()
    {
        sealedSkill = null; sealedOwner = null; confessionSource = null;
        confession = null; cooling = null;
    }
    public bool IsBlocked(Skill skill) => sealedSkill != null && skill == sealedSkill;
    public bool HasSealedSkill => sealedSkill != null;
    public void SealSkill(Character target, StageOneEnemy1112Mechanic source)
    {
        if (sealedSkill != null || target == null) return;
        var equipped = target.CombatRulesRuntime?.Loadout;
        var candidates = target.RuntimeSkills.Where(s => s?.Definition != null &&
            equipped?.IsEquipped(s.Definition) == true).ToArray();
        if (candidates.Length == 0) return;
        sealedSkill = candidates[UnityEngine.Random.Range(0, candidates.Length)];
        sealedOwner = target; confessionSource = source; confession = null;
    }
    public IReadOnlyList<Skill> ForSlot(Character character, BodyPart part, IReadOnlyList<Skill> original)
    {
        var result = new List<Skill>();
        if (original != null)
            foreach (var s in original)
            {
                if (character == sealedOwner && s == sealedSkill)
                {
                    var d = Resources.Load<SkillDefinition>("ProjectAbyss/Canonical1112/Temporary/Confession");
                    if (d != null)
                    {
                        confession ??= new StageOneTemporarySkill1112(d, this, confessionSource, sealedSkill.ActionType);
                        confession.Initialize(character, character.BattleEvent);
                        result.Add(confession);
                    }
                }
                else result.Add(s);
            }
        if (character == character.BattleContext?.Player && BodyPartSkillAccessPolicy.Allows(part, ActionType.Preparation))
        {
            var source = character.BattleContext.Enemies.Select(e => e?.GetMechanic<StageOneEnemy1112Mechanic>())
                .FirstOrDefault(m => m?.Kind == StageOneEnemyKind1112.MachineBoss && m.CoolingCards > 0);
            var d = Resources.Load<SkillDefinition>("ProjectAbyss/Canonical1112/Temporary/Cooling");
            if (source != null && d != null)
            {
                if (cooling == null || cooling.Source != source)
                    cooling = new StageOneTemporarySkill1112(d, this, source, ActionType.Preparation);
                cooling.Initialize(character, character.BattleEvent);
                result.Add(cooling);
            }
        }
        return result;
    }
    public bool CanConfess(Character target) => sealedSkill != null && sealedOwner == target;
    public Action Confess(BodyPart part)
    {
        if (sealedSkill == null || confessionSource == null || part == null) return null;
        var saved = sealedSkill; int suspicion = confessionSource.SuspicionAt(part);
        confessionSource.ClearSuspicion(part); sealedSkill = null;
        return () => { sealedSkill = saved; confessionSource.RestoreSuspicion(part, suspicion); };
    }
}

public sealed class StageOneTemporarySkill1112 : Skill
{
    private readonly SkillDefinition definition;
    private readonly StageOneTemporaryCards1112 store;
    private readonly ActionType slotCategory;
    public StageOneEnemy1112Mechanic Source { get; }
    public bool IsCooling => definition.name == "Cooling";
    protected override SkillDefinition RuntimeDefinition => definition;
    public override ActionType ActionType => slotCategory;
    public override ActionPhase DefaultPhase => ActionPhase.FORESIGHT;
    public override bool CanClash => false;
    public override bool GainPrestige => false;
    public override int EnergyCost => IsCooling ? 1 : 0;
    public StageOneTemporarySkill1112(SkillDefinition d, StageOneTemporaryCards1112 cards,
        StageOneEnemy1112Mechanic source, ActionType category)
    { definition = d; store = cards; Source = source; slotCategory = category; SkillName = d.SkillName; }
    public override bool CanUseByResource(Character character) => base.CanUseByResource(character) &&
        (IsCooling ? Source.CoolingCards > 0 && Source.Owner != null && !Source.Owner.IsDead && Source.Owner.BodyParts.Any(p => !p.IsBroken && Source.IsOverclocked(p)) : store.CanConfess(character));
    public IReadOnlyList<ActionPlanningChoiceOption> Choices()
    {
        var parts = IsCooling ? Source.Owner?.BodyParts : Owner?.BodyParts;
        if (parts == null) return Array.Empty<ActionPlanningChoiceOption>();
        return parts.Where(p => !p.IsBroken && (!IsCooling || Source.IsOverclocked(p)))
            .Select(p => new ActionPlanningChoiceOption(p.PartId, p.DisplayName)).ToArray();
    }
    public override void Execute(BattleAction action)
    {
        var parts = IsCooling ? Source.Owner?.BodyParts : Owner?.BodyParts;
        string choice = action?.Slot?.PlanningChoiceId;
        var part = parts?.FirstOrDefault(p => p.PartId == choice && !p.IsBroken);
        if (part == null) throw new InvalidOperationException("냉각/고해의 대상 부위를 선택해야 합니다.");
        if (IsCooling)
        {
            if (!Source.Cool(part)) throw new InvalidOperationException("냉각 가능한 오버클록 부위가 아닙니다.");
            action.Slot.PlanningUndo.Record(() => Source.RestoreCooling(part));
        }
        else
        {
            var undo = store.Confess(part);
            if (undo == null) throw new InvalidOperationException("고해로 복구할 스킬이 없습니다.");
            action.Slot.PlanningUndo.Record(undo);
        }
    }
}
