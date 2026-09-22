using System;
using UnityEngine;

public enum Yujin0917EffectOperation
{
    None = 0,

    /// <summary>
    /// O — 때를 앞당기다.
    /// 감 4는 YujinDuelRuntimeSkill의 resource commit에서 이미 지불한다.
    /// 현재 표식을 44 임계까지 밀어 기존 AddMark→IgniteMark 경로를 그대로 통과시킨다.
    /// </summary>
    ForceTargetMarkIgnition = 1,

    /// <summary>
    /// P — 끝장을 보다.
    /// 사용시 남은 살수의 감을 전부 소모하고, 소모 1당 이 행동의 위력 +2.
    /// </summary>
    ConsumeAllSenseForPower = 2
}

/// <summary>
/// 0917 신규 유진 O/P 전용 effect.
/// 0916 effect 타입을 수정하지 않아 구 자산 직렬화를 보존한다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Skill/Effect/0917/Yujin O-P",
    fileName = "Yujin0917Effect")]
public sealed class Yujin0917SkillEffectDefinition :
    SkillEffectDefinition
{
    public Yujin0917EffectOperation Operation;

    public override void Apply(
        SkillEffectContext context)
    {
        if (context?.Owner == null)
            return;

        YujinMechanic mechanic =
            context.Owner
                .GetMechanic<YujinMechanic>();

        if (mechanic == null)
            return;

        switch (Operation)
        {
            case Yujin0917EffectOperation.ForceTargetMarkIgnition:
                ForceIgnition(
                    context,
                    mechanic);
                break;

            case Yujin0917EffectOperation.ConsumeAllSenseForPower:
                ConsumeSenseForPower(
                    context,
                    mechanic);
                break;
        }
    }

    private static void ForceIgnition(
        SkillEffectContext context,
        YujinMechanic mechanic)
    {
        if (context.Target == null)
            return;

        // 0922 Phase 6:
        // O도 즉시/예약 표식과 동일한 ApplyMarkGain 원자 경로를 사용한다.
        // 따라서 지정 총합, 44 발화, K/L rider, overflow 폐기가 한 곳에서 처리된다.
        mechanic.ForceMarkIgnition(
            context.Target,
            context.TargetPart,
            context.Action);
    }

    private static void ConsumeSenseForPower(
        SkillEffectContext context,
        YujinMechanic mechanic)
    {
        int spent =
            Mathf.Max(
                0,
                mechanic.Sense);

        if (spent <= 0)
            return;

        if (!mechanic.TrySpendSense(spent))
            return;

        if (context.Action != null)
        {
            context.Action.RulebreakerFlatPowerBonus +=
                spent * 2;
        }
    }
}