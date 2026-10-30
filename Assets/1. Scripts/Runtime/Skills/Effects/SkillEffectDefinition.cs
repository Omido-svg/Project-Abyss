using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class SkillEffectDefinition : ScriptableObject
{
    [Header("Trigger Contract — Rules 2026-09")]
    [SerializeField]
    [Tooltip("정본 발동 Trigger. 신규 콘텐츠는 Authoring UI에 노출되는 정본 Trigger만 사용합니다.")]
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
        if (currentTiming != scheduledTiming &&
            !CanWakeFromRuntimeEvent(currentTiming))
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

        // The selector is resolved once, then both Entry and Definition
        // conditions refer to the exact same target. Direct legacy callers
        // still use this path.
        return TryApplyWithSelectedTarget(
            context.WithTarget(targetSelector),
            currentTiming,
            overrides,
            scheduledTiming);
    }

    /// <summary>
    /// R3: Entry resolves target before its own candidate conditions; Definition
    /// must not roll a second random target after those conditions pass.
    /// </summary>
    public SkillEffectResult TryApplyWithSelectedTarget(
        SkillEffectContext selectedContext,
        SkillEffectTiming currentTiming,
        SkillEffectOverrides overrides,
        SkillEffectTiming scheduledTiming)
    {
        return TryApplyWithSelectedTarget(selectedContext,currentTiming,
            overrides,scheduledTiming,null);
    }

    public SkillEffectResult TryApplyWithSelectedTarget(
        SkillEffectContext selectedContext,
        SkillEffectTiming currentTiming,
        SkillEffectOverrides overrides,
        SkillEffectTiming scheduledTiming,
        Func<SkillEffectContext,SkillEffectOverrides,bool> nativeApply)
    {
        if (currentTiming != scheduledTiming &&
            !CanWakeFromRuntimeEvent(currentTiming))
            return SkillEffectResult.NotScheduled(this, currentTiming);

        if (selectedContext == null)
            return SkillEffectResult.Failed(this, currentTiming, null, "Context is null.");

        if (!EvaluateConditions(selectedContext, out string failureMessage))
            return SkillEffectResult.ConditionFailed(
                this, currentTiming, selectedContext, failureMessage);

        if(nativeApply!=null)
        {
            bool committed=nativeApply(selectedContext,overrides);
            return committed
                ? SkillEffectResult.Applied(this,currentTiming,selectedContext)
                : SkillEffectResult.Failed(this,currentTiming,selectedContext,
                    "Native command was not committed.");
        }
        Apply(selectedContext, overrides);
        return SkillEffectResult.Applied(this, currentTiming, selectedContext);
    }

    public bool CanWakeFromRuntimeEvent(SkillEffectTiming runtimeTiming)
    {
        if (SkillEffectTimingCatalog.IsAuthoringTiming(runtimeTiming) ||
            conditions == null)
        {
            return false;
        }

        for (int i = 0; i < conditions.Count; i++)
        {
            SkillEffectCondition condition = conditions[i];
            if (condition != null &&
                condition.IsRuntimeEventConditionFor(runtimeTiming))
            {
                return true;
            }
        }

        return false;
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
