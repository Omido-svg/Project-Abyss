using System.Collections.Generic;
using UnityEngine;

public abstract class SkillEffectDefinition : ScriptableObject
{
    [Header("Execution Contract")]
    [SerializeField]
    private SkillEffectTiming timing =
        SkillEffectTiming.OnExecute;

    [SerializeField]
    private SkillEffectTargetSelector targetSelector =
        SkillEffectTargetSelector.CurrentTarget;

    [SerializeField]
    private List<SkillEffectCondition> conditions = new();

    public SkillEffectTiming Timing => timing;
    public SkillEffectTargetSelector TargetSelector =>
        targetSelector;

    public IReadOnlyList<SkillEffectCondition> Conditions =>
        conditions;

    public SkillEffectResult TryApply(
        SkillEffectContext context,
        SkillEffectTiming currentTiming)
    {
        return TryApply(context, currentTiming, null);
    }

    public SkillEffectResult TryApply(
        SkillEffectContext context,
        SkillEffectTiming currentTiming,
        SkillEffectOverrides overrides)
    {
        return TryApply(
            context,
            currentTiming,
            overrides,
            timing);
    }

    public SkillEffectResult TryApply(
        SkillEffectContext context,
        SkillEffectTiming currentTiming,
        SkillEffectOverrides overrides,
        SkillEffectTiming scheduledTiming)
    {
        if (currentTiming != scheduledTiming)
        {
            return SkillEffectResult.NotScheduled(
                this,
                currentTiming);
        }

        if (context == null)
        {
            return SkillEffectResult.Failed(
                this,
                currentTiming,
                null,
                "Context is null.");
        }

        SkillEffectContext selectedContext =
            context.WithTarget(targetSelector);

        if (!EvaluateConditions(
                selectedContext,
                out string failureMessage))
        {
            return SkillEffectResult.ConditionFailed(
                this,
                currentTiming,
                selectedContext,
                failureMessage);
        }

        Apply(selectedContext, overrides);

        return SkillEffectResult.Applied(
            this,
            currentTiming,
            selectedContext);
    }

    public abstract void Apply(
        SkillEffectContext context);

    public virtual void Apply(
        SkillEffectContext context,
        SkillEffectOverrides overrides)
    {
        Apply(context);
    }

    private bool EvaluateConditions(
        SkillEffectContext context,
        out string failureMessage)
    {
        failureMessage = null;

        if (conditions == null ||
            conditions.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < conditions.Count; i++)
        {
            SkillEffectCondition condition =
                conditions[i];

            if (condition == null)
                continue;

            if (condition.Evaluate(context))
                continue;

            failureMessage =
                $"Condition[{i}] {condition.Type} failed.";
            return false;
        }

        return true;
    }
}