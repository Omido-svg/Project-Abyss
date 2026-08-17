using System;
using System.Collections.Generic;
using System.Linq;

public sealed class HifumiCharacterVerificationCaseProvider :
    ICharacterVerificationCaseProvider
{
    public const string SkillCoverage =
        "hifumi.data.skill_coverage";

    public const string SkillContracts =
        "hifumi.data.skill_contracts";

    public const string MechanicRegistration =
        "hifumi.passive.mechanic_registration";

    public const string BoneDamageRules =
        "hifumi.passive.bone_damage_rules";

    public const string ActionCosts =
        "hifumi.skill.action_costs";

    public const string CounterContracts =
        "hifumi.duel.counter_contracts";

    public const string GoldanPower =
        "hifumi.duel.goldan_power";

    public const string PrestigeContracts =
        "hifumi.prestige.contracts";

    public const string GaugeContract =
        "hifumi.ui.bone_gauge";

    private static readonly string[] RequiredSkillIds =
    {
        HifumiSkillIds.SmallChange,
        HifumiSkillIds.BoldJudgment,
        HifumiSkillIds.Yukcham,
        HifumiSkillIds.Goldan,
        HifumiSkillIds.RecklessBet,
        HifumiSkillIds.PokerFace,
        HifumiSkillIds.EngraveBone,
        HifumiSkillIds.GiveFlesh,
        HifumiSkillIds.FoldHand,
        HifumiSkillIds.GamblerMove,
        HifumiSkillIds.AllIn,
        HifumiSkillIds.Trick
    };

    public bool Supports(
        CharacterAuthoringBundle bundle,
        Character character)
    {
        return bundle?.Kind == CharacterAuthoringKind.Hifumi ||
               character is Hifumi;
    }

    public IEnumerable<CharacterVerificationCaseDefinition>
        CreateDefaultCases(
            CharacterAuthoringBundle bundle)
    {
        yield return CharacterVerificationCaseDefinition.Create(
            SkillCoverage,
            "히후미 정의 스킬 12종 등록",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "TODO2/PPT에서 현재 정의된 일반 2·결투 3·도사림 4·위세 3 SkillId가 데이터 그래프에 모두 존재하는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            SkillContracts,
            "히후미 스킬 수치 계약",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "푼돈 걸기/과감한 판단/육참/골단/무모한 베팅의 Base·굴림수·빛·친치로 Resolver와 위세 임계값을 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            MechanicRegistration,
            "히후미 통합 메커닉 등록",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "HifumiMechanic 생성·등록·Owner 연결과 최대 HP 500 구성을 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            BoneDamageRules,
            "뼈 획득·짓눌림·포커페이스",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "실피해 1:1 뼈, 짓눌림 피해 1/2·뼈×2, 포커페이스 교환당 피해 -1·피격당 뼈+4 계약을 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            ActionCosts,
            "과감한 판단·무모한 베팅 뼈 비용",
            CharacterVerificationCategory.NormalAttack,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "뼈 40 소비/부족 시 +20과 무모한 베팅의 70 게이트·40 소비/부족 시 +100을 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            CounterContracts,
            "육참·골단 반격/만개 계약",
            CharacterVerificationCategory.Duel,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "진 교환당 반격 1개, Base8+뼈구간, 2구간 30·3/4구간 60 회복, 500 만개 350/약화·파괴/바25/전량소모를 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            GoldanPower,
            "골단 뼈 구간 위력",
            CharacterVerificationCategory.Duel,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "TODO2 우선 규칙인 골단 Base20 + (뼈구간×4)가 실제 ModifyRoll 경로에 적용되는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            PrestigeContracts,
            "히후미 위세 3형 계약",
            CharacterVerificationCategory.Prestige,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "도박수 뼈 전소/100당+6, 올인 최고결과·대실패 전소, 속임수 아라시/대실패 고정을 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            GaugeContract,
            "히후미 뼈 World Gauge",
            CharacterVerificationCategory.UI,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "ICharacterUniqueGaugeProvider의 뼈 0~500 정규화와 만개 표시를 검사합니다.");
    }

    public bool TryExecute(
        CharacterVerificationContext context,
        out CharacterVerificationCaseResult result)
    {
        result = context?.Definition?.CaseId switch
        {
            SkillCoverage => VerifySkillCoverage(context),
            SkillContracts => VerifySkillContracts(context),
            MechanicRegistration => VerifyMechanic(context),
            BoneDamageRules => VerifyBoneDamageRules(context),
            ActionCosts => VerifyActionCosts(context),
            CounterContracts => VerifyCounterContracts(context),
            GoldanPower => VerifyGoldanPower(context),
            PrestigeContracts => VerifyPrestigeContracts(context),
            GaugeContract => VerifyGauge(context),
            _ => null
        };

        return result != null;
    }

    private static CharacterVerificationCaseResult VerifySkillCoverage(
        CharacterVerificationContext context)
    {
        HashSet<string> existing =
            new HashSet<string>(
                context.Bundle
                    .EnumerateSkillDefinitions()
                    .Where(item => item != null)
                    .Select(item => item.SkillId),
                StringComparer.Ordinal);

        List<string> missing =
            RequiredSkillIds
                .Where(id => !existing.Contains(id))
                .ToList();

        return missing.Count == 0
            ? context.Pass("히후미 정의 SkillId 12/12", "12/12 등록")
            : context.Fail(
                "히후미 정의 SkillId 12/12",
                $"{RequiredSkillIds.Length - missing.Count}/{RequiredSkillIds.Length}",
                "누락:\n" + string.Join("\n", missing));
    }

    private static CharacterVerificationCaseResult VerifySkillContracts(
        CharacterVerificationContext context)
    {
        (string Id, ActionType Type, int Base, int Rolls, int Energy)[] expected =
        {
            (HifumiSkillIds.SmallChange, ActionType.NormalAttack, 12, 2, 0),
            (HifumiSkillIds.BoldJudgment, ActionType.NormalAttack, 14, 3, 1),
            (HifumiSkillIds.Yukcham, ActionType.Duel, 12, 4, 1),
            (HifumiSkillIds.Goldan, ActionType.Duel, 20, 2, 1),
            (HifumiSkillIds.RecklessBet, ActionType.Duel, 13, 5, 2)
        };

        List<string> failures = new List<string>();

        foreach (var contract in expected)
        {
            SkillDefinition definition =
                CharacterVerificationScenarioTools.FindDefinition(
                    context.Bundle,
                    contract.Id);

            if (definition == null)
            {
                failures.Add(contract.Id + " 없음");
                continue;
            }

            if (definition.ActionType != contract.Type ||
                definition.BasePower != contract.Base ||
                definition.ExchangeRollCount != contract.Rolls ||
                definition.EnergyCost != contract.Energy ||
                definition.ResolverType != SkillResolverType.Chinchiro)
            {
                failures.Add(
                    $"{contract.Id}: Type={definition.ActionType}, " +
                    $"Base={definition.BasePower}, Rolls={definition.ExchangeRollCount}, " +
                    $"Light={definition.EnergyCost}, Resolver={definition.ResolverType}");
            }
        }

        VerifyPrestigeDefinition(
            context,
            HifumiSkillIds.GamblerMove,
            50,
            failures);

        VerifyPrestigeDefinition(
            context,
            HifumiSkillIds.AllIn,
            50,
            failures);

        VerifyPrestigeDefinition(
            context,
            HifumiSkillIds.Trick,
            65,
            failures);

        return failures.Count == 0
            ? context.Pass(
                "공격 5종 수치 + 위세 임계 50/50/65",
                "모든 계약 일치")
            : context.Fail(
                "공격 5종 수치 + 위세 임계 50/50/65",
                $"{failures.Count}개 불일치",
                string.Join("\n", failures));
    }

    private static void VerifyPrestigeDefinition(
        CharacterVerificationContext context,
        string id,
        int expectedCost,
        ICollection<string> failures)
    {
        SkillDefinition definition =
            CharacterVerificationScenarioTools.FindDefinition(
                context.Bundle,
                id);

        if (definition == null ||
            definition.ActionType != ActionType.Prestige ||
            definition.PrestigeCost != expectedCost)
        {
            failures.Add(
                $"{id}: " +
                (definition == null
                    ? "NULL"
                    : $"Type={definition.ActionType}, Cost={definition.PrestigeCost}"));
        }
    }

    private static CharacterVerificationCaseResult VerifyMechanic(
        CharacterVerificationContext context)
    {
        Hifumi hifumi = context.Character as Hifumi;
        HifumiMechanic mechanic = hifumi?.HifumiMechanic;

        int hpSum =
            hifumi?.BodyParts?
                .Where(part => part != null)
                .Sum(part => UnityEngine.Mathf.RoundToInt(part.MaxPartHP)) ?? 0;

        bool valid =
            mechanic != null &&
            mechanic.IsRegistered &&
            mechanic.Owner == hifumi &&
            hpSum == 500 &&
            hifumi.MaxCombatHP == 500;

        return valid
            ? context.Pass(
                "HifumiMechanic 등록 + 4부위 총 HP 500",
                $"Registered / HP={hifumi.MaxCombatHP}")
            : context.Fail(
                "HifumiMechanic 등록 + 4부위 총 HP 500",
                $"Mechanic={(mechanic == null ? "NULL" : "Present")}, " +
                $"Registered={mechanic?.IsRegistered}, PartsHP={hpSum}, MaxHP={hifumi?.MaxCombatHP}");
    }

    private static CharacterVerificationCaseResult VerifyBoneDamageRules(
        CharacterVerificationContext context)
    {
        HifumiMechanic mechanic =
            (context.Character as Hifumi)?.HifumiMechanic;

        if (mechanic == null)
            return context.Fail("HifumiMechanic 존재", "NULL");

        int normalDamage =
            mechanic.ResolveIncomingDamageForVerification(
                21,
                lastStand: false,
                pokerFace: false);

        int crushedDamage =
            mechanic.ResolveIncomingDamageForVerification(
                21,
                lastStand: true,
                pokerFace: false);

        int pokerDamage =
            mechanic.ResolveIncomingDamageForVerification(
                21,
                lastStand: false,
                pokerFace: true);

        int normalBone =
            mechanic.ResolveBoneGainForVerification(
                normalDamage,
                lastStand: false,
                selfCost: false,
                pokerFaceHit: false);

        int crushedBone =
            mechanic.ResolveBoneGainForVerification(
                crushedDamage,
                lastStand: true,
                selfCost: false,
                pokerFaceHit: false);

        int pokerBone =
            mechanic.ResolveBoneGainForVerification(
                pokerDamage,
                lastStand: false,
                selfCost: false,
                pokerFaceHit: true);

        bool valid =
            normalDamage == 21 &&
            crushedDamage == 11 &&
            pokerDamage == 20 &&
            normalBone == 21 &&
            crushedBone == 22 &&
            pokerBone == 24;

        return valid
            ? context.Pass(
                "21피해→뼈21 / 짓눌림 11피해·뼈22 / 포커 20피해·뼈24",
                "21/21 · 11/22 · 20/24")
            : context.Fail(
                "21피해→뼈21 / 짓눌림 11피해·뼈22 / 포커 20피해·뼈24",
                $"Damage={normalDamage}/{crushedDamage}/{pokerDamage}, " +
                $"Bone={normalBone}/{crushedBone}/{pokerBone}");
    }

    private static CharacterVerificationCaseResult VerifyActionCosts(
        CharacterVerificationContext context)
    {
        HifumiMechanic mechanic =
            (context.Character as Hifumi)?.HifumiMechanic;

        if (mechanic == null)
            return context.Fail("HifumiMechanic 존재", "NULL");

        mechanic.SetBoneForVerification(100);
        mechanic.ApplyActionStartCostForVerification(
            HifumiSkillIds.BoldJudgment);
        int boldPaid = mechanic.Bone;

        mechanic.SetBoneForVerification(20);
        mechanic.ApplyActionStartCostForVerification(
            HifumiSkillIds.BoldJudgment);
        int boldShort = mechanic.Bone;

        mechanic.SetBoneForVerification(80);
        mechanic.ApplyActionStartCostForVerification(
            HifumiSkillIds.RecklessBet);
        int recklessPaid = mechanic.Bone;

        mechanic.SetBoneForVerification(60);
        mechanic.ApplyActionStartCostForVerification(
            HifumiSkillIds.RecklessBet);
        int recklessShort = mechanic.Bone;

        bool valid =
            boldPaid == 60 &&
            boldShort == 40 &&
            recklessPaid == 40 &&
            recklessShort == 160;

        return valid
            ? context.Pass(
                "과감 100→60 / 부족20→40 / 무모80→40 / 부족60→160",
                $"{boldPaid}/{boldShort}/{recklessPaid}/{recklessShort}")
            : context.Fail(
                "과감 100→60 / 부족20→40 / 무모80→40 / 부족60→160",
                $"{boldPaid}/{boldShort}/{recklessPaid}/{recklessShort}");
    }

    private static CharacterVerificationCaseResult VerifyCounterContracts(
        CharacterVerificationContext context)
    {
        HifumiMechanic mechanic =
            (context.Character as Hifumi)?.HifumiMechanic;

        if (mechanic == null)
            return context.Fail("HifumiMechanic 존재", "NULL");

        HifumiCounterPreview band2 =
            mechanic.BuildCounterPreviewForVerification(
                HifumiSkillIds.Yukcham,
                2,
                250,
                sourcePartBroken: false,
                targetAlreadyWeakened: false);

        HifumiCounterPreview band4 =
            mechanic.BuildCounterPreviewForVerification(
                HifumiSkillIds.Goldan,
                1,
                450,
                sourcePartBroken: false,
                targetAlreadyWeakened: false);

        HifumiCounterPreview bloomWeaken =
            mechanic.BuildCounterPreviewForVerification(
                HifumiSkillIds.Goldan,
                3,
                500,
                sourcePartBroken: false,
                targetAlreadyWeakened: false);

        HifumiCounterPreview bloomBreak =
            mechanic.BuildCounterPreviewForVerification(
                HifumiSkillIds.Yukcham,
                1,
                500,
                sourcePartBroken: false,
                targetAlreadyWeakened: true);

        HifumiCounterPreview disabled =
            mechanic.BuildCounterPreviewForVerification(
                HifumiSkillIds.RecklessBet,
                4,
                500,
                sourcePartBroken: false,
                targetAlreadyWeakened: false);

        bool valid =
            band2.Enabled &&
            band2.CounterCount == 2 &&
            band2.CounterPower == 10 &&
            band2.Heal == 30 &&
            !band2.ConsumeAllBone &&
            band4.CounterPower == 12 &&
            band4.Heal == 60 &&
            !band4.ConsumeAllBone &&
            bloomWeaken.CounterCount == 3 &&
            bloomWeaken.CounterPower == 12 &&
            bloomWeaken.Heal == 350 &&
            bloomWeaken.ConsumeAllBone &&
            bloomWeaken.Weaken &&
            !bloomWeaken.BreakPart &&
            bloomWeaken.MomentumPush == 25 &&
            bloomBreak.BreakPart &&
            !bloomBreak.Weaken &&
            !disabled.Enabled;

        return valid
            ? context.Pass(
                "반격 게이트·구간·만개 계약 전체",
                "PASS")
            : context.Fail(
                "반격 게이트·구간·만개 계약 전체",
                "불일치",
                $"Band2={Describe(band2)}\nBand4={Describe(band4)}\n" +
                $"BloomW={Describe(bloomWeaken)}\nBloomB={Describe(bloomBreak)}\n" +
                $"Disabled={Describe(disabled)}");
    }

    private static CharacterVerificationCaseResult VerifyGoldanPower(
        CharacterVerificationContext context)
    {
        Hifumi hifumi = context.Character as Hifumi;
        HifumiMechanic mechanic = hifumi?.HifumiMechanic;

        SkillDefinition definition =
            CharacterVerificationScenarioTools.FindDefinition(
                context.Bundle,
                HifumiSkillIds.Goldan);

        Skill runtime =
            CharacterVerificationScenarioTools.FindRuntimeSkill(
                hifumi,
                definition);

        BodyPart part =
            CharacterVerificationScenarioTools.GetUsablePart(hifumi);

        BattleAction action =
            CharacterVerificationScenarioTools.CreateAction(
                hifumi,
                part,
                runtime,
                context.OpponentCharacter,
                CharacterVerificationScenarioTools.GetUsablePart(
                    context.OpponentCharacter),
                945001);

        action.CurrentRollType = CombatRollType.Attack;

        mechanic.SetBoneForVerification(350);
        int modified =
            mechanic.ModifyRoll(
                action,
                definition?.BasePower ?? 0);

        // flat +1 + 골단 구간3*4
        int expected = 20 + 1 + 12;

        return modified == expected
            ? context.Pass(
                "골단 350뼈: Base20 + 친치로보정1 + 구간3×4 = 33",
                modified.ToString())
            : context.Fail(
                "골단 350뼈: 33",
                modified.ToString());
    }

    private static CharacterVerificationCaseResult VerifyPrestigeContracts(
        CharacterVerificationContext context)
    {
        Hifumi hifumi = context.Character as Hifumi;
        HifumiMechanic mechanic = hifumi?.HifumiMechanic;

        if (hifumi == null || mechanic == null)
            return context.Fail("HifumiMechanic 존재", "NULL");

        mechanic.SetBoneForVerification(250);
        int beforePower = hifumi.TurnClashPowerBonus;
        mechanic.ExecuteSkillForVerification(
            HifumiSkillIds.GamblerMove);

        bool gambler =
            mechanic.Bone == 0 &&
            hifumi.TurnClashPowerBonus == beforePower + 12;

        mechanic.SetBoneForVerification(100);
        mechanic.ResolveAllInForVerification(
            ChinchiroCombination.Blank,
            ChinchiroCombination.Shigoro,
            ChinchiroCombination.Moku);
        bool allInBest = mechanic.Bone == 200;

        mechanic.SetBoneForVerification(300);
        mechanic.ResolveAllInForVerification(
            ChinchiroCombination.Arashi,
            ChinchiroCombination.Hifumi,
            ChinchiroCombination.Arashi);
        bool allInBust = mechanic.Bone == 0;

        mechanic.ExecuteSkillForVerification(
            HifumiSkillIds.Trick);

        bool forcedArashi =
            mechanic.TryGetForcedChinchiro(
                out ChinchiroCombination first) &&
            first == ChinchiroCombination.Arashi;

        mechanic.SetTrickOutcomeForTurn(true);

        bool forcedHifumi =
            mechanic.TryGetForcedChinchiro(
                out ChinchiroCombination second) &&
            second == ChinchiroCombination.Hifumi;

        bool valid =
            gambler &&
            allInBest &&
            allInBust &&
            forcedArashi &&
            forcedHifumi;

        return valid
            ? context.Pass(
                "도박수/올인/속임수 계약",
                "PASS")
            : context.Fail(
                "도박수/올인/속임수 계약",
                $"Gambler={gambler}, AllInBest={allInBest}, " +
                $"AllInBust={allInBust}, TrickA={forcedArashi}, TrickH={forcedHifumi}");
    }

    private static CharacterVerificationCaseResult VerifyGauge(
        CharacterVerificationContext context)
    {
        HifumiMechanic mechanic =
            (context.Character as Hifumi)?.HifumiMechanic;

        if (mechanic == null)
            return context.Fail("HifumiMechanic 존재", "NULL");

        mechanic.SetBoneForVerification(250);
        bool half =
            mechanic.GaugeLabel == "뼈" &&
            UnityEngine.Mathf.Abs(mechanic.GaugeNormalized - 0.5f) < 0.001f &&
            mechanic.GaugeValueText.Contains("250/500");

        mechanic.SetBoneForVerification(500);
        bool bloom =
            UnityEngine.Mathf.Abs(mechanic.GaugeNormalized - 1f) < 0.001f &&
            mechanic.GaugeValueText.Contains("만개");

        return half && bloom
            ? context.Pass(
                "뼈 250=0.5 / 500=1.0·만개",
                "PASS")
            : context.Fail(
                "뼈 250=0.5 / 500=1.0·만개",
                $"Half={half}, Bloom={bloom}, Text={mechanic.GaugeValueText}");
    }

    private static string Describe(
        HifumiCounterPreview value)
    {
        return
            $"Enabled={value.Enabled}, Count={value.CounterCount}, " +
            $"Power={value.CounterPower}, Heal={value.Heal}, " +
            $"Consume={value.ConsumeAllBone}, Weaken={value.Weaken}, " +
            $"Break={value.BreakPart}, Push={value.MomentumPush}";
    }
}
