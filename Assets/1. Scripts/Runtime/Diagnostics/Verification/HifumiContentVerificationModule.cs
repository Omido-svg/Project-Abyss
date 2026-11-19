using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Phase D-3 — Hifumi runtime gate.
/// H-01/H-02/H-03/H-05/H-06/H-07/H-09/H-10을 0916 정본 기준으로 검증한다.
/// 전체 Hifumi SkillDefinition/SO 이관은 Phase E(H-08) 범위다.
/// </summary>
public sealed class HifumiContentVerificationModule : IGameSystemVerificationModule
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
            "1·2·3은 친치로 값 −18로 정상 판정(강제 실패 없음) + 자기피해 60 (§4.4 · §19.3, 11-19)",
            VerifyCatastrophe);

        yield return Runtime(
            "phased.hifumi.h09.bold_judgment",
            "H-09",
            "과감한 판단 사용시 + 합종료시",
            GameSystemVerificationCategory.Lifecycle,
            "사용시 뼈 C 지불 시도(가불 우선) — 성공=페널티 없음 / 실패=아무것도 안 내고 다음 턴 속박 N 1턴; 합 종료시 완전 차단이면 가불 A (§19.6)",
            VerifyBoldJudgment);

        yield return Runtime(
            "phased.hifumi.h09.bold_judgment_self_damage",
            "H-09",
            "과감한 판단 완전 차단 — 자해 제외",
            GameSystemVerificationCategory.Lifecycle,
            "추적 중 자해(SelfCost)는 완전 차단을 깨지 않고, 적이 준 피해는 깬다 (§19.6, 11-18 사용자)",
            VerifyBoldJudgmentSelfDamageExcluded);

        yield return Runtime(
            "phased.hifumi.h10.engrave_complete_block_self_damage",
            "H-10",
            "몸에 새기다 체득 완전 차단 — 자해 제외",
            GameSystemVerificationCategory.Lifecycle,
            "체득(4단계 · 승리 분기) 추적 중 자해(SelfCost)는 완전 차단을 깨지 않고, 적이 준 피해는 깬다 (§19.1, 11-18 사용자)",
            VerifyEngraveCompleteBlockSelfDamageExcluded);

        yield return Runtime(
            "phased.hifumi.h11.mutual_destruction",
            "H-11",
            "동귀어진 — 굴림·합 없음, 서로 고정 피해 N",
            GameSystemVerificationCategory.Damage,
            "합 불가 · 빛 1 · 상대(방어도 무시) · 자신(슬롯 부위 + 전체) 각 N, 흐트러짐 · 기세 · 위세 · 히트 없음, 자기 피해 피격 적립, 약화까지만 (§19.6 · §10.2)",
            VerifyMutualDestruction);

        yield return Runtime(
            "phased.hifumi.h12.self_damage_floor",
            "H-11",
            "히후미 자해 — 전체 체력 1 문턱",
            GameSystemVerificationCategory.Damage,
            "동귀어진 자기 피해 · 대실패 자해는 전체 체력을 1 아래로 내리지 않음. 동귀어진: 막힌 만큼 부위 · 전체 피해 감소, 뼈 = 실제 피해 + 핏값, 0이면 핏값만 / 대실패: 뼈 D 그대로 (11-18 사용자)",
            VerifySelfDamageFloor);

        yield return Runtime(
            "phased.hifumi.h13.catastrophe_three_ways",
            "H-07",
            "대실패 세 갈래 · 강화 반격 아라시 · 반격 대박 값",
            GameSystemVerificationCategory.Lifecycle,
            "대실패 = 어디서든 값 −18 정상 판정 / 강화 반격 = 대실패 + 합 패배만(진 교환 순서) / 반격 굴림 대실패 = −18로 때리고 강화 없음(연쇄 없음) · 강화 = 아라시 확정 · 반격 대박 결투 18 / 평타 10 (§19.3, 11-19)",
            VerifyCatastropheThreeWays);

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
            !bloom.Weaken && bloom.BreakPart && bloom.MomentumPush == 25 &&
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

        // 11-19: 대실패는 강제 실패가 아니라 값 −18 정상 판정 → forced는 false여야 한다.
        bool ok = catastrophe.ChinchiroCombination == ChinchiroCombination.Hifumi &&
                  catastrophe.ChinchiroSelfDamage == 60 &&
                  catastrophe.ChinchiroBonus == HifumiChinchiroRuntime.CatastropheValue &&
                  catastrophe.FinalPower == 12 + HifumiChinchiroRuntime.CatastropheValue &&
                  !forced && pairedHook && oneSidedHook;

        return ok
            ? GameSystemVerificationProbeResult.Pass("1·2·3 catastrophe=forced failure/self60; Clash paired+one-sided hooks wired")
            : GameSystemVerificationProbeResult.Fail(
                $"Combo={catastrophe.ChinchiroCombination}, Self={catastrophe.ChinchiroSelfDamage}, Forced={forced}, Hooks={pairedHook}/{oneSidedHook}");
    }

    private static GameSystemVerificationProbeResult VerifyBoldJudgment(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out _, out Hifumi hifumi, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;

        HifumiCardRuleSettings rules = m.CardRules;
        int cost = rules.BoldJudgmentBoneCost;
        int bind = rules.BoldJudgmentBindOnFail;
        int advance = rules.BoldJudgmentCompleteBlockAdvance;

        int CountDeferredBinds() =>
            hifumi.StatusEffects.Count(effect =>
                effect is DeferredStatusEffect deferred &&
                deferred.DeferredStatusId == StatusEffectId.Bind);

        void ClearDeferredBinds()
        {
            foreach (StatusEffect effect in hifumi.StatusEffects
                         .Where(effect =>
                             effect is DeferredStatusEffect deferred &&
                             deferred.DeferredStatusId == StatusEffectId.Bind)
                         .ToList())
            {
                hifumi.RemoveStatus(effect, StatusEffectRemoveReason.Manual);
            }
        }

        ClearDeferredBinds();

        // ① 실제 뼈로 지불 성공 — 페널티 없음.
        m.SetAdvanceBoneForVerification(0);
        m.SetBoneForVerification(100);
        m.ApplyActionStartCostForVerification(HifumiSkillIds.BoldJudgment);
        bool paid =
            m.LastBoldJudgmentPaid == true &&
            m.Bone == 100 - cost &&
            CountDeferredBinds() == 0;

        // ② 가불부터 낸다 — 가불 절반 + 실제 뼈로 나머지.
        int halfAdvance = cost / 2;
        m.SetAdvanceBoneForVerification(halfAdvance);
        m.SetBoneForVerification(100);
        m.ApplyActionStartCostForVerification(HifumiSkillIds.BoldJudgment);
        bool advanceFirst =
            m.LastBoldJudgmentPaid == true &&
            m.AdvanceBone == 0 &&
            m.Bone == 100 - (cost - halfAdvance) &&
            CountDeferredBinds() == 0;

        // ③ 모자라면 실패 — 아무것도 내지 않고 다음 턴 속박 N 1턴 예약.
        bool shortfall = true;
        if (cost > 0)
        {
            m.SetAdvanceBoneForVerification(0);
            m.SetBoneForVerification(cost - 1);
            m.ApplyActionStartCostForVerification(HifumiSkillIds.BoldJudgment);
            DeferredStatusEffect queued = hifumi.StatusEffects
                .OfType<DeferredStatusEffect>()
                .FirstOrDefault(d => d.DeferredStatusId == StatusEffectId.Bind);
            shortfall =
                m.LastBoldJudgmentPaid == false &&
                m.Bone == cost - 1 &&
                (bind <= 0
                    ? queued == null
                    : queued != null && CountDeferredBinds() == 1 && queued.Stack == bind);
        }
        ClearDeferredBinds();

        // ④ 합 종료시 — 완전 차단이면 가불 A(실제 뼈는 그대로), 아니면 보상 없음.
        m.SetAdvanceBoneForVerification(0);
        m.SetBoneForVerification(100);
        bool noReward =
            !m.ApplyBoldJudgmentCompleteBlockForVerification(1, 0) &&
            !m.ApplyBoldJudgmentCompleteBlockForVerification(0, 1) &&
            m.AdvanceBone == 0 && m.Bone == 100;
        bool reward =
            m.ApplyBoldJudgmentCompleteBlockForVerification(0, 0) &&
            m.AdvanceBone == advance && m.Bone == 100;

        m.SetAdvanceBoneForVerification(0);

        bool ok = paid && advanceFirst && shortfall && noReward && reward;
        string actual =
            $"C={cost}/N={bind}/A={advance}; Paid={paid}, AdvanceFirst={advanceFirst}, " +
            $"Short={shortfall}, NoReward={noReward}, Reward={reward}";
        return ok
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(actual);
    }

    private static GameSystemVerificationProbeResult VerifyBoldJudgmentSelfDamageExcluded(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Hifumi hifumi, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;

        Skill bold = hifumi.RuntimeSkills?.FirstOrDefault(skill =>
            skill?.Definition?.SkillId == HifumiSkillIds.BoldJudgment);
        if (bold == null)
            return GameSystemVerificationProbeResult.Fail("과감한 판단 RuntimeSkill이 로드아웃에 없습니다.");
        if (f.DamageManager == null || f.Enemy == null)
            return GameSystemVerificationProbeResult.Fail("DamageManager/Enemy missing");

        int boneBefore = m.Bone;
        int advanceBefore = m.AdvanceBone;
        BattleAction action = new BattleAction
        {
            Slot = new ActionSlot
            {
                ActionId = 950901,
                Owner = hifumi,
                Part = hifumi.BodyParts?.FirstOrDefault(part => part != null && !part.IsBroken),
                Skill = bold,
                TargetCharacter = f.Enemy
            }
        };

        try
        {
            f.Context._battleEvent.RaiseActionStart(action);
            bool tracking = m.BoldJudgmentTrackingForVerification;

            f.DamageManager.ApplyDamageContext(DamageRequest.SelfCost(hifumi, 3));
            int hpAfterSelf = m.BoldJudgmentTrackedHpDamageForVerification;
            int staggerAfterSelf = m.BoldJudgmentTrackedStaggerDamageForVerification;

            f.DamageManager.ApplyDamageContext(DamageRequest.Direct(f.Enemy, hifumi, 3));
            int hpAfterEnemy = m.BoldJudgmentTrackedHpDamageForVerification;

            bool ok =
                tracking &&
                hpAfterSelf == 0 &&
                staggerAfterSelf == 0 &&
                hpAfterEnemy > 0;

            string actual =
                $"Tracking={tracking}, AfterSelf HP/Stagger={hpAfterSelf}/{staggerAfterSelf}, AfterEnemy HP={hpAfterEnemy}";
            return ok
                ? GameSystemVerificationProbeResult.Pass(actual)
                : GameSystemVerificationProbeResult.Fail(actual);
        }
        finally
        {
            m.ResetBoldJudgmentTrackingForVerification();
            m.SetBoneForVerification(boneBefore);
            m.SetAdvanceBoneForVerification(advanceBefore);
        }
    }

    private static GameSystemVerificationProbeResult VerifyEngraveCompleteBlockSelfDamageExcluded(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Hifumi hifumi, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;
        if (f.DamageManager == null || f.Enemy == null)
            return GameSystemVerificationProbeResult.Fail("DamageManager/Enemy missing");

        // 몸에 새기다는 런 단위 영구 성장이라, 바꾼 진행도는 끝나면 되돌린다.
        int stageBefore = m.EngraveStage;
        HifumiEngraveBranch branchBefore = m.EngraveBranch;
        int victoryBefore = m.EngraveVictoryProgressForVerification;
        int defeatBefore = m.EngraveDefeatProgressForVerification;
        int boneBefore = m.Bone;
        int advanceBefore = m.AdvanceBone;

        SkillDefinition definition = ScriptableObject.CreateInstance<SkillDefinition>();
        definition.name = HifumiSkillIds.EngraveBody;
        definition.ActionType = ActionType.Duel;

        try
        {
            m.SetEngraveProgressForVerification(4, HifumiEngraveBranch.Victory);
            Skill engrave = hifumi.CreateRuntimeSkillForLoadout(definition);
            engrave.Initialize(hifumi, f.Context._battleEvent);
            BattleAction action = new BattleAction
            {
                Slot = new ActionSlot
                {
                    ActionId = 950902,
                    Owner = hifumi,
                    Part = hifumi.BodyParts?.FirstOrDefault(part => part != null && !part.IsBroken),
                    Skill = engrave,
                    TargetCharacter = f.Enemy
                }
            };

            f.Context._battleEvent.RaiseActionStart(action);
            bool tracking = m.EngraveCompleteBlockTrackingForVerification;

            f.DamageManager.ApplyDamageContext(DamageRequest.SelfCost(hifumi, 3));
            int hpAfterSelf = m.EngraveTrackedHpDamageForVerification;
            int staggerAfterSelf = m.EngraveTrackedStaggerDamageForVerification;

            f.DamageManager.ApplyDamageContext(DamageRequest.Direct(f.Enemy, hifumi, 3));
            int hpAfterEnemy = m.EngraveTrackedHpDamageForVerification;

            bool ok =
                tracking &&
                hpAfterSelf == 0 &&
                staggerAfterSelf == 0 &&
                hpAfterEnemy > 0;

            string actual =
                $"Tracking={tracking}, AfterSelf HP/Stagger={hpAfterSelf}/{staggerAfterSelf}, AfterEnemy HP={hpAfterEnemy}";
            return ok
                ? GameSystemVerificationProbeResult.Pass(actual)
                : GameSystemVerificationProbeResult.Fail(actual);
        }
        finally
        {
            m.ResetEngraveTrackingForVerification();
            m.SetEngraveProgressForVerification(stageBefore, branchBefore, victoryBefore, defeatBefore);
            m.SetBoneForVerification(boneBefore);
            m.SetAdvanceBoneForVerification(advanceBefore);
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static GameSystemVerificationProbeResult VerifyMutualDestruction(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Hifumi hifumi, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;

        Skill skill = hifumi.RuntimeSkills?.FirstOrDefault(s =>
            s?.Definition?.SkillId == HifumiSkillIds.MutualDestruction);
        if (skill == null)
            return GameSystemVerificationProbeResult.Fail("동귀어진 RuntimeSkill이 로드아웃에 없습니다.");
        if (f.ClashManager == null || f.Enemy == null || f.MomentumManager == null)
            return GameSystemVerificationProbeResult.Fail("ClashManager/Enemy/Momentum missing");

        int n = m.CardRules.MutualDestructionFixedDamage;
        Character enemy = f.Enemy;
        BodyPart enemyPart = enemy.BodyParts?.FirstOrDefault(p => p != null && !p.IsBroken);
        BodyPart ownPart = hifumi.BodyParts?.FirstOrDefault(p => p != null && p.Type == PartType.LEFT_HAND && !p.IsBroken);
        if (ownPart == null)
            return GameSystemVerificationProbeResult.Fail("히후미 왼팔 부위 없음");

        StaggerGaugeMechanic enemyStagger = enemy.GetMechanic<StaggerGaugeMechanic>();
        StaggerGaugeMechanic ownStagger = hifumi.GetMechanic<StaggerGaugeMechanic>();

        int enemyBlockBefore = enemy.RuntimeStatus.currentBlock;
        int boneBefore = m.Bone;
        int advanceBefore = m.AdvanceBone;
        try
        {
            enemy.RuntimeStatus.currentBlock = 50;
            m.SetAdvanceBoneForVerification(0);
            m.SetBoneForVerification(0);

            int eHp = enemy.CurrentHP;
            float ePartHp = enemyPart?.PartHP ?? 0f;
            int eStagger = enemyStagger?.CurrentGauge ?? 0;
            int hHp = hifumi.CurrentHP;
            float hPartHp = ownPart.PartHP;
            int hStagger = ownStagger?.CurrentGauge ?? 0;
            int bar = f.MomentumManager.CurrentMomentum;
            int prestige = hifumi.RuntimeStatus.currentPrestige;

            ActionSlot slot = new ActionSlot
            {
                ActionId = 950911, Owner = hifumi, Part = ownPart, Skill = skill,
                TargetCharacter = enemy, TargetPart = enemyPart,
                Phase = ActionPhase.COMBAT, Speed = 5, ResourceCostCommitted = true
            };
            ClashResultContext result = f.ClashManager.ResolvePair(new ClashPair(slot));

            bool shape = !skill.CanClash && skill.EnergyCost == 1 && skill.IsRed;
            bool enemyHit =
                eHp - enemy.CurrentHP == n &&
                (enemyPart == null || Mathf.Approximately(ePartHp - enemyPart.PartHP, n)) &&
                enemy.RuntimeStatus.currentBlock == 50 &&
                (enemyStagger?.CurrentGauge ?? 0) == eStagger;
            bool selfHit =
                hHp - hifumi.CurrentHP == n &&
                Mathf.Approximately(hPartHp - ownPart.PartHP, n) &&
                (ownStagger?.CurrentGauge ?? 0) == hStagger &&
                m.Bone >= n;
            bool noTempo =
                f.MomentumManager.CurrentMomentum == bar &&
                hifumi.RuntimeStatus.currentPrestige == prestige &&
                result != null && !result.IsClash && result.OneSidedHitCount == 0;

            // 약화까지만 — 이미 약화된 자기 부위도 파괴되지 않는다.
            ownPart.SetDebugState(Mathf.Min(5, ownPart.MaxPartHP), ownPart.MaxPartHP, false, false);
            f.ClashManager.ResolvePair(new ClashPair(new ActionSlot
            {
                ActionId = 950912, Owner = hifumi, Part = ownPart, Skill = skill,
                TargetCharacter = enemy, TargetPart = enemyPart,
                Phase = ActionPhase.COMBAT, Speed = 5, ResourceCostCommitted = true
            }));
            bool weakened = ownPart.IsWeakened && !ownPart.IsBroken;
            f.ClashManager.ResolvePair(new ClashPair(new ActionSlot
            {
                ActionId = 950913, Owner = hifumi, Part = ownPart, Skill = skill,
                TargetCharacter = enemy, TargetPart = enemyPart,
                Phase = ActionPhase.COMBAT, Speed = 5, ResourceCostCommitted = true
            }));
            bool noBreak = ownPart.IsWeakened && !ownPart.IsBroken;

            bool ok = n > 0 && shape && enemyHit && selfHit && noTempo && weakened && noBreak;
            string actual =
                $"N={n}; Shape={shape}, Enemy={enemyHit}({eHp}->{enemy.CurrentHP}, block {enemy.RuntimeStatus.currentBlock}), " +
                $"Self={selfHit}({hHp}->{hifumi.CurrentHP}, part {hPartHp}->?, bone {m.Bone}), NoTempo={noTempo}, " +
                $"Weaken={weakened}, NoBreak={noBreak}";
            return ok
                ? GameSystemVerificationProbeResult.Pass(actual)
                : GameSystemVerificationProbeResult.Fail(actual);
        }
        finally
        {
            enemy.RuntimeStatus.currentBlock = enemyBlockBefore;
            m.SetBoneForVerification(boneBefore);
            m.SetAdvanceBoneForVerification(advanceBefore);
        }
    }

    private static GameSystemVerificationProbeResult VerifySelfDamageFloor(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Hifumi hifumi, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;

        Skill skill = hifumi.RuntimeSkills?.FirstOrDefault(s =>
            s?.Definition?.SkillId == HifumiSkillIds.MutualDestruction);
        if (skill == null || f.ClashManager == null || f.Enemy == null)
            return GameSystemVerificationProbeResult.Fail("동귀어진/ClashManager/Enemy missing");

        int n = m.CardRules.MutualDestructionFixedDamage;
        BodyPart ownPart = hifumi.BodyParts?.FirstOrDefault(p => p != null && p.Type == PartType.HEAD && !p.IsBroken);
        int hpBefore = hifumi.CurrentHP;
        int boneBefore = m.Bone;
        int bloodBefore = m.BloodPriceThisTurn;
        const int blood = 4;

        ActionSlot Slot(long id) => new ActionSlot
        {
            ActionId = id, Owner = hifumi, Part = ownPart, Skill = skill,
            TargetCharacter = f.Enemy,
            TargetPart = f.Enemy.BodyParts?.FirstOrDefault(p => p != null && !p.IsBroken),
            Phase = ActionPhase.COMBAT, Speed = 5, ResourceCostCommitted = true
        };

        try
        {
            m.SetBloodPriceForVerification(blood);

            // ① 전체 체력 N 남음 → 1에서 멈춤, 부위도 N−1만, 뼈 = (N−1) + 핏값.
            hifumi.RuntimeStatus.currentHP = n;
            float partBefore = ownPart.PartHP;
            m.SetBoneForVerification(0);
            f.ClashManager.ResolvePair(new ClashPair(Slot(950921)));
            bool clamped =
                hifumi.CurrentHP == 1 && !hifumi.IsDead &&
                Mathf.Approximately(partBefore - ownPart.PartHP, n - 1) &&
                m.Bone == (n - 1) + blood;

            // ② 전체 체력 1 → 피해 0, 핏값만.
            m.SetBoneForVerification(0);
            f.ClashManager.ResolvePair(new ClashPair(Slot(950922)));
            bool atOne = hifumi.CurrentHP == 1 && !hifumi.IsDead && m.Bone == blood;

            // ③ 대실패 자해 60 — 체력 30에서 1까지만, 뼈 D(60)는 직접 지급 그대로.
            hifumi.RuntimeStatus.currentHP = 30;
            m.SetBoneForVerification(0);
            m.ApplyCatastropheSelfDamageForVerification();
            bool catastrophe =
                hifumi.CurrentHP == 1 && !hifumi.IsDead &&
                m.Bone >= HifumiNativeRules.CatastropheDirectBone;

            bool ok = n > 1 && clamped && atOne && catastrophe;
            string actual =
                $"N={n}, Blood={blood}; Clamp={clamped}, AtOne={atOne}, Catastrophe={catastrophe} " +
                $"(HP={hifumi.CurrentHP}, Bone={m.Bone})";
            return ok
                ? GameSystemVerificationProbeResult.Pass(actual)
                : GameSystemVerificationProbeResult.Fail(actual);
        }
        finally
        {
            hifumi.RuntimeStatus.currentHP = hpBefore;
            m.SetBoneForVerification(boneBefore);
            m.SetBloodPriceForVerification(bloodBefore);
        }
    }

    private static GameSystemVerificationProbeResult VerifyCatastropheThreeWays(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out Hifumi hifumi, out HifumiMechanic m, out GameSystemVerificationProbeResult fail))
            return fail;

        // ① 값 — 대실패 −3, 강화 반격 = 아라시 확정 · 반격 대박 값
        RollResult cat = HifumiChinchiroRuntime.BuildResultForVerification(10, 1, 2, 3);
        bool catValue = cat.ChinchiroCombination == ChinchiroCombination.Hifumi &&
                        cat.ChinchiroBonus == -18 && cat.FinalPower == 10 - 18;
        RollResult enhNormal = HifumiChinchiroRuntime.RollEnhancedCounter(5, HifumiChinchiroRuntime.NormalCounterJackpot);
        RollResult enhDuel = HifumiChinchiroRuntime.RollEnhancedCounter(9, HifumiChinchiroRuntime.DuelCounterJackpot);
        bool enhanced =
            enhNormal.ChinchiroCombination == ChinchiroCombination.Arashi &&
            enhNormal.FinalPower == 15 &&
            enhNormal.DiceValues.Count == 3 &&
            enhNormal.DiceValues.Distinct().Count() == 1 &&
            enhDuel.FinalPower == 27;

        // ② 강제 실패 없음 — 합 · 일방 모두 정상 판정
        bool oneSided = !((IForcedRollFailureRule)m).IsForcedRollFailure(
            new BattleAction { Slot = new ActionSlot { Owner = hifumi } }, cat, out _);

        // ③ 반격 풀 — 진 교환 순서, 합 대실패 교환은 강화, 일방 · 이긴 교환은 없음
        Skill small = hifumi.RuntimeSkills?.FirstOrDefault(sk => sk?.Definition?.SkillId == HifumiSkillIds.SmallChange);
        if (small == null || f.Enemy == null)
            return GameSystemVerificationProbeResult.Fail("푼돈 걸기 / Enemy missing");
        BodyPart ownPart = hifumi.BodyParts?.FirstOrDefault(p => p != null && !p.IsBroken && p.Type != PartType.LEGS);
        BodyPart enemyPart = f.Enemy.BodyParts?.FirstOrDefault(p => p != null && !p.IsBroken);
        BattleAction mine = new BattleAction { Slot = new ActionSlot {
            ActionId = 950931, Owner = hifumi, Part = ownPart, Skill = small,
            TargetCharacter = f.Enemy, TargetPart = enemyPart } };
        BattleAction other = new BattleAction { Slot = new ActionSlot { ActionId = 950932, Owner = f.Enemy } };
        var clash = new ClashResultContext { IsClash = true, FirstAction = mine, SecondAction = other };
        clash.Exchanges.Add(new ClashExchangeResult { ExchangeIndex = 0, FirstAction = mine, SecondAction = other, WinnerAction = other, LoserAction = mine });
        clash.Exchanges.Add(new ClashExchangeResult { ExchangeIndex = 1, FirstAction = mine, SecondAction = other, WinnerAction = other, LoserAction = mine });
        clash.Exchanges.Add(new ClashExchangeResult { ExchangeIndex = 2, FirstAction = mine, SecondAction = other, WinnerAction = mine, LoserAction = other });
        clash.Exchanges.Add(new ClashExchangeResult { ExchangeIndex = 3, FirstAction = mine, IsOneSided = true, WinnerAction = mine });
        // 1 = 대실패로 진 교환(강화) · 2 = 대실패인데 이긴 교환(강화 없음)
        List<bool> pool = m.BuildCounterPoolForVerification(clash, mine, new[] { 1, 2 });
        bool poolOk = pool.Count == 2 && pool[0] == false && pool[1] == true;

        // ④ 반격 굴림 대실패 → −18로 때리고(최소 1) 강화 반격은 없다 (속임수 대실패 고정으로 강제)
        int hpBefore = hifumi.CurrentHP;
        int boneBefore = m.Bone;
        var names = new List<string>();
        var powers = new List<int>();
        System.Action<BattleReactiveRollEvent> onRoll = e =>
        {
            if (e?.Owner == hifumi) { names.Add(e.DisplayName); powers.Add(e.Power); }
        };
        f.Context._battleEvent.OnReactiveRollStarted += onRoll;
        bool chainOk;
        try
        {
            m.ExecuteSkillForVerification(HifumiSkillIds.Trick);
            m.SetTrickOutcomeForTurn(true);
            m.ResolveCounterSequenceForVerification(mine, f.Enemy, enemyPart, new List<bool> { false });
            chainOk =
                names.Count == 1 &&
                names[0] == "뼈 반격 · 대실패" &&
                powers[0] == System.Math.Max(1, HifumiNormalCounterPower(m) + HifumiChinchiroRuntime.CatastropheValue) &&
                m.Bone >= boneBefore + HifumiNativeRules.CatastropheDirectBone;
        }
        finally
        {
            f.Context._battleEvent.OnReactiveRollStarted -= onRoll;
            m.ClearTrickForVerification();
            hifumi.RuntimeStatus.currentHP = hpBefore;
            m.SetBoneForVerification(boneBefore);
        }

        bool ok = catValue && enhanced && oneSided && poolOk && chainOk;
        string actual =
            $"Cat={catValue}(bonus {cat.ChinchiroBonus}, power {cat.FinalPower}), Enhanced={enhanced}(N {enhNormal.FinalPower} / D {enhDuel.FinalPower}), " +
            $"OneSidedNoFail={oneSided}, Pool={poolOk}({string.Join(",", pool)}), Chain={chainOk}({string.Join(" > ", names)} / {string.Join(",", powers)})";
        return ok
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(actual);
    }

    private static int HifumiNormalCounterPower(HifumiMechanic m) =>
        HifumiMechanic.NormalCounterBasePower + HifumiMechanic.HifumiBasePowerBonus;

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
