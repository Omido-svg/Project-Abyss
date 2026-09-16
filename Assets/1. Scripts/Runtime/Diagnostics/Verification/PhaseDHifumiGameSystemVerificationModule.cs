using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Phase D-3 — Hifumi runtime gate.
/// H-01/H-02/H-03/H-05/H-06/H-07/H-09/H-10을 0916 정본 기준으로 검증한다.
/// 전체 Hifumi SkillDefinition/SO 이관은 Phase E(H-08) 범위다.
/// </summary>
public sealed class PhaseDHifumiGameSystemVerificationModule : IGameSystemVerificationModule
{
    public string ModuleId => "phase_d.hifumi";
    public int Order => 520;

    public IEnumerable<GameSystemVerificationCase> BuildCases()
    {
        yield return Runtime(
            "phased.hifumi.h01.bone_scale",
            "H-01",
            "히후미 뼈 500·5구간·만개",
            GameSystemVerificationCategory.Resource,
            "뼈 0..500, 100당 구간 0..5, 만개=500, UI/조건이 같은 기준 사용",
            VerifyBoneScale);

        yield return Runtime(
            "phased.hifumi.h02.goldan_reverse_scale",
            "H-02",
            "골단 본체 위력 역스케일",
            GameSystemVerificationCategory.Damage,
            "골단 Base20-(뼈구간×4); 반격은 반대로 구간 보너스",
            VerifyGoldanReverseScale);

        yield return Runtime(
            "phased.hifumi.h03.final_hp_damage_bone",
            "H-03",
            "뼈 적립 FinalHpDamage 1:1",
            GameSystemVerificationCategory.Damage,
            "방어도/감소 적용 뒤 실제 HP 피해만큼 뼈; 구 짓눌림 반감/역보정 없음",
            VerifyFinalDamageBone);

        yield return Runtime(
            "phased.hifumi.h05.counter_gate_split",
            "H-05",
            "반격 태그와 Duel 게이트 분리",
            GameSystemVerificationCategory.Contract,
            "푼돈 걸기 평타 반격은 Duel 게이트 없음; 육참/골단만 Duel×Duel 패배 적립",
            VerifyCounterGateSplit);

        yield return Runtime(
            "phased.hifumi.h06.counter_resolver",
            "H-06",
            "반격 Resolver 최신 계약",
            GameSystemVerificationCategory.Damage,
            "결투 8+구간+1+친치로, 평타4+1+친치로; 육참 뼈보상, 골단 만개350/약화/파괴권한/바25/전량소모",
            VerifyCounterResolver);

        yield return Runtime(
            "phased.hifumi.h07.catastrophe",
            "H-07",
            "친치로 1·2·3 대실패",
            GameSystemVerificationCategory.Contract,
            "1·2·3은 강제패배 + 자기피해60 + 반격굴림+1; paired/one-sided 공통 hook",
            VerifyCatastrophe);

        yield return Runtime(
            "phased.hifumi.h09.bold_judgment",
            "H-09",
            "과감한 판단 사용시 + 합종료시",
            GameSystemVerificationCategory.Lifecycle,
            "사용시 뼈40 소비(부족시+20)+다음턴 속도-1; 합 종료 lost=0일 때만 뼈+50",
            VerifyBoldJudgment);

        yield return Runtime(
            "phased.hifumi.h10.duel_governor",
            "H-10",
            "히후미 다음 턴 Duel 자기 거버너",
            GameSystemVerificationCategory.Lifecycle,
            "결투 승리 교환은 다음 턴 1턴·비누적 위력감소 상태 예약; 감소량 0=Unset 데이터",
            VerifyGovernor);
    }

    private static GameSystemVerificationCase Runtime(
        string id,
        string req,
        string name,
        GameSystemVerificationCategory category,
        string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> probe) =>
        new(id, req, name, category,
            GameSystemVerificationExecutionMode.IsolatedRuntime,
            expected, probe, true);

    private static bool Fixture(
        GameSystemVerificationContext context,
        out GameSystemVerificationFixture fixture,
        out Hifumi hifumi,
        out HifumiMechanic mechanic,
        out GameSystemVerificationProbeResult failure)
    {
        hifumi = null;
        mechanic = null;

        if (!context.TryGetFixture(out fixture, out string reason))
        {
            failure = GameSystemVerificationProbeResult.Fail(reason);
            return false;
        }

        hifumi = fixture.Player as Hifumi;
        mechanic = hifumi?.HifumiMechanic;

        if (hifumi == null || mechanic == null)
        {
            failure = GameSystemVerificationProbeResult.Fail(
                $"Phase D Hifumi Gate는 Player가 Hifumi인 전투에서 실행해야 합니다. Player={fixture.Player?.GetType().Name ?? "NULL"}");
            return false;
        }

        failure = null;
        return true;
    }

    private static GameSystemVerificationProbeResult VerifyBoneScale(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out _, out _, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;

        m.SetBoneForVerification(99);
        bool a = m.Bone == 99 && m.BoneTier == 0 && !m.IsBloom;
        m.SetBoneForVerification(100);
        bool b = m.BoneTier == 1;
        m.SetBoneForVerification(499);
        bool c = m.BoneTier == 4 && !m.IsBloom;
        m.SetBoneForVerification(500);
        bool d = m.BoneTier == 5 && m.IsBloom && Mathf.Approximately(m.GaugeNormalized, 1f);
        m.AddBone(999);
        bool cap = m.Bone == 500;

        bool ok = a && b && c && d && cap &&
                  HifumiMechanic.MaxBone == 500 &&
                  HifumiMechanic.MaxBoneTier == 5;

        return ok
            ? GameSystemVerificationProbeResult.Pass("Bone 99/100/499/500 => tier 0/1/4/5; bloom/cap=500")
            : GameSystemVerificationProbeResult.Fail(
                $"a={a}, b={b}, c={c}, d={d}, cap={cap}, Bone={m.Bone}, Tier={m.BoneTier}");
    }

    private static GameSystemVerificationProbeResult VerifyGoldanReverseScale(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out _, out _, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;

        m.SetBoneForVerification(0);
        int p0 = 20 + m.ResolveDuelBasePowerAdjustment(HifumiSkillIds.Goldan);
        m.SetBoneForVerification(300);
        int p3 = 20 + m.ResolveDuelBasePowerAdjustment(HifumiSkillIds.Goldan);
        m.SetBoneForVerification(500);
        int p5 = 20 + m.ResolveDuelBasePowerAdjustment(HifumiSkillIds.Goldan);

        RollResult pair = HifumiChinchiroRuntime.BuildResultForVerification(p3, 2, 2, 5);
        bool ok = p0 == 20 && p3 == 8 && p5 == 0 &&
                  pair.RawValue == 5 && pair.FinalPower == 13;

        return ok
            ? GameSystemVerificationProbeResult.Pass("Goldan base 20/8/0 at tier 0/3/5; pair unmatched die applied")
            : GameSystemVerificationProbeResult.Fail(
                $"Base={p0}/{p3}/{p5}, PairRaw={pair.RawValue}, PairFinal={pair.FinalPower}");
    }

    private static GameSystemVerificationProbeResult VerifyFinalDamageBone(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out _, out _, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;

        int normalDamage = m.ResolveIncomingDamageForVerification(21, false, false);
        int crushedDamage = m.ResolveIncomingDamageForVerification(21, true, false);
        int pokerDamage = m.ResolveIncomingDamageForVerification(21, false, true);
        int normalBone = m.ResolveBoneGainForVerification(normalDamage, false, false, false);
        int crushedBone = m.ResolveBoneGainForVerification(crushedDamage, true, false, false);
        int pokerBone = m.ResolveBoneGainForVerification(pokerDamage, false, false, true);

        bool ok = normalDamage == 21 && crushedDamage == 21 && pokerDamage == 17 &&
                  normalBone == 21 && crushedBone == 21 && pokerBone == 21;

        return ok
            ? GameSystemVerificationProbeResult.Pass("Normal/Crushed FinalHP=21; Poker=17 then +4 bone buyback =>21")
            : GameSystemVerificationProbeResult.Fail(
                $"Damage={normalDamage}/{crushedDamage}/{pokerDamage}, Bone={normalBone}/{crushedBone}/{pokerBone}");
    }

    private static GameSystemVerificationProbeResult VerifyCounterGateSplit(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out _, out _, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;

        HifumiCounterPreview small = m.BuildCounterPreviewForVerification(
            HifumiSkillIds.SmallChange, 1, 300, false, false);
        HifumiCounterPreview yukcham = m.BuildCounterPreviewForVerification(
            HifumiSkillIds.Yukcham, 1, 300, false, false);
        HifumiCounterPreview goldan = m.BuildCounterPreviewForVerification(
            HifumiSkillIds.Goldan, 1, 300, false, false);
        HifumiCounterPreview reckless = m.BuildCounterPreviewForVerification(
            HifumiSkillIds.RecklessBet, 1, 300, false, false);

        bool ok =
            HifumiSkillIds.IsNormalCounterSkill(HifumiSkillIds.SmallChange) &&
            !HifumiSkillIds.IsDuelCounterSkill(HifumiSkillIds.SmallChange) &&
            HifumiSkillIds.IsDuelCounterSkill(HifumiSkillIds.Yukcham) &&
            HifumiSkillIds.IsDuelCounterSkill(HifumiSkillIds.Goldan) &&
            small.Enabled && small.CounterPower == 5 &&
            yukcham.Enabled && goldan.Enabled && !reckless.Enabled;

        return ok
            ? GameSystemVerificationProbeResult.Pass("SmallChange normal-counter; Yukcham/Goldan duel-counter; Reckless untagged")
            : GameSystemVerificationProbeResult.Fail(
                $"Small={small.Enabled}/{small.CounterPower}, Yuk={yukcham.Enabled}, Goldan={goldan.Enabled}, Reckless={reckless.Enabled}");
    }

    private static GameSystemVerificationProbeResult VerifyCounterResolver(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out _, out _, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;

        HifumiCounterPreview yuk = m.BuildCounterPreviewForVerification(
            HifumiSkillIds.Yukcham, 2, 250, false, false);
        HifumiCounterPreview gold = m.BuildCounterPreviewForVerification(
            HifumiSkillIds.Goldan, 1, 450, false, false);
        HifumiCounterPreview bloom = m.BuildCounterPreviewForVerification(
            HifumiSkillIds.Goldan, 1, 500, false, false);

        RollResult pair = HifumiChinchiroRuntime.BuildResultForVerification(11, 2, 2, 5);
        RollResult shigoro = HifumiChinchiroRuntime.BuildResultForVerification(11, 4, 5, 6);

        bool ok =
            yuk.CounterPower == 11 && yuk.BoneGain == 60 && !yuk.ConsumeAllBone &&
            gold.CounterPower == 13 && gold.ConsumeAllBone && gold.Heal == 0 &&
            bloom.CounterPower == 14 && bloom.Heal == 350 && bloom.ConsumeAllBone &&
            bloom.Weaken && bloom.BreakPart && bloom.MomentumPush == 25 &&
            pair.RawValue == 5 && pair.FinalPower == 16 &&
            shigoro.RawValue == 18 && shigoro.FinalPower == 29;

        return ok
            ? GameSystemVerificationProbeResult.Pass("Counter bases/rewards + canonical Chinchiro pair/456 PASS")
            : GameSystemVerificationProbeResult.Fail(
                $"Yuk={yuk.CounterPower}/{yuk.BoneGain}, Gold={gold.CounterPower}/{gold.ConsumeAllBone}, " +
                $"Bloom={bloom.CounterPower}/{bloom.Heal}/{bloom.Weaken}/{bloom.BreakPart}/{bloom.MomentumPush}, " +
                $"Pair={pair.RawValue}/{pair.FinalPower}, 456={shigoro.RawValue}/{shigoro.FinalPower}");
    }

    private static GameSystemVerificationProbeResult VerifyCatastrophe(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out _, out Hifumi hifumi, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;

        RollResult catastrophe = HifumiChinchiroRuntime.BuildResultForVerification(12, 1, 2, 3);
        BattleAction action = new BattleAction
        {
            Slot = new ActionSlot
            {
                ActionId = 930071,
                Owner = hifumi,
                Part = hifumi.BodyParts != null && hifumi.BodyParts.Count > 0
                    ? hifumi.BodyParts[0]
                    : null,
                Speed = 5
            }
        };

        bool forced = ((IForcedRollFailureRule)m).IsForcedRollFailure(
            action, catastrophe, out _);

        bool pairedHook = typeof(ClashManager).GetMethod(
            "TryResolveForcedRollJudgment",
            BindingFlags.NonPublic | BindingFlags.Static) != null;
        bool oneSidedHook = typeof(ClashManager).GetMethod(
            "IsForcedRollFailure",
            BindingFlags.NonPublic | BindingFlags.Static) != null;

        bool ok = catastrophe.ChinchiroCombination == ChinchiroCombination.Hifumi &&
                  catastrophe.ChinchiroSelfDamage == 60 && forced && pairedHook && oneSidedHook;

        return ok
            ? GameSystemVerificationProbeResult.Pass("1·2·3 catastrophe=forced failure/self60; Clash paired+one-sided hooks wired")
            : GameSystemVerificationProbeResult.Fail(
                $"Combo={catastrophe.ChinchiroCombination}, Self={catastrophe.ChinchiroSelfDamage}, Forced={forced}, Hooks={pairedHook}/{oneSidedHook}");
    }

    private static GameSystemVerificationProbeResult VerifyBoldJudgment(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out _, out _, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;

        m.SetBoneForVerification(100);
        m.ApplyActionStartCostForVerification(HifumiSkillIds.BoldJudgment);
        bool paid = m.Bone == 60 && m.NextTurnSpeedPenaltyQueued;

        m.SetBoneForVerification(20);
        m.ApplyActionStartCostForVerification(HifumiSkillIds.BoldJudgment);
        bool shortfall = m.Bone == 40 && m.NextTurnSpeedPenaltyQueued;

        m.SetBoneForVerification(100);
        bool noReward = !m.ApplyBoldJudgmentClashEndForVerification(1) && m.Bone == 100;
        bool reward = m.ApplyBoldJudgmentClashEndForVerification(0) && m.Bone == 150;

        bool ok = paid && shortfall && noReward && reward;
        return ok
            ? GameSystemVerificationProbeResult.Pass("OnUse cost/fallback + speed queue; OnClashEnd perfect block +50 exactly")
            : GameSystemVerificationProbeResult.Fail(
                $"Paid={paid}, Short={shortfall}, NoReward={noReward}, Reward={reward}, Bone={m.Bone}");
    }

    private static GameSystemVerificationProbeResult VerifyGovernor(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out _, out Hifumi hifumi, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;

        bool unset = hifumi.DuelGovernorPowerPenalty == 0;

        m.QueueGovernorForVerification(3);
        m.QueueGovernorForVerification(3);
        bool queued = m.GovernorQueued;
        m.ActivateGovernorForVerification();

        int yukAdjustment = m.ResolveDuelBasePowerAdjustment(HifumiSkillIds.Yukcham);
        m.SetBoneForVerification(300);
        int goldAdjustment = m.ResolveDuelBasePowerAdjustment(HifumiSkillIds.Goldan);

        bool ok = unset && queued && m.ActiveGovernorPenalty == 3 &&
                  yukAdjustment == -3 && goldAdjustment == -15;

        return ok
            ? GameSystemVerificationProbeResult.Pass("Governor amount Unset in authoring; verification 3 applies once/nonstack next-turn Duel only")
            : GameSystemVerificationProbeResult.Fail(
                $"Unset={unset}, Queued={queued}, Active={m.ActiveGovernorPenalty}, Adjust={yukAdjustment}/{goldAdjustment}");
    }
}
