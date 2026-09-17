#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0917 patch-specific gate.
/// 미정 설계는 FAIL로 위조하지 않고 PENDING으로 출력한다.
/// </summary>
public static class Canonical0917PatchVerification
{
    private const string YujinLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_Loadout.asset";

    private const string OlafLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Olaf_TODO_Loadout.asset";

    [MenuItem("Game System Verification/0917 Spec Patch/Verify 0917 Patch")]
    public static void VerifyFromMenu()
    {
        List<string> pass = new();
        List<string> pending = new();
        List<string> fail = new();

        VerifyYujin(pass, fail);
        VerifyOlaf(pass, pending, fail);
        VerifyHifumi(pass, pending, fail);
        VerifyRegistry(pass, fail);

        string summary =
            "[0917 Spec Patch Verification]\n" +
            $"PASS={pass.Count} PENDING={pending.Count} FAIL={fail.Count}\n\n" +
            "PASS\n- " +
            string.Join("\n- ", pass) +
            "\n\nPENDING\n- " +
            string.Join("\n- ", pending) +
            "\n\nFAIL\n- " +
            string.Join("\n- ", fail);

        if (fail.Count > 0)
            Debug.LogError(summary);
        else if (pending.Count > 0)
            Debug.LogWarning(summary);
        else
            Debug.Log(summary);
    }

    private static void VerifyYujin(
        List<string> pass,
        List<string> fail)
    {
        CharacterCombatLoadout loadout =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(
                YujinLoadoutPath);

        if (loadout == null)
        {
            fail.Add("Yujin loadout missing");
            return;
        }

        int normal =
            loadout.NormalSkillPool?.Count ?? 0;

        int duel =
            loadout.DuelSkillPool?.Count ?? 0;

        int preparation =
            (loadout.CommonPreparationPool?.Count ?? 0) +
            (loadout.CharacterPreparationPool?.Count ?? 0);

        int prestige =
            loadout.PrestigeSkillPool?.Count ?? 0;

        HashSet<string> ids =
            new HashSet<string>(
                loadout.EnumerateAllDefinitions()
                    .Where(x => x != null)
                    .Select(x => x.SkillId),
                StringComparer.Ordinal);

        string[] expected =
            YujinSkillIds.CanonicalNormal
                .Concat(YujinSkillIds.CanonicalDuel)
                .Concat(YujinSkillIds.CanonicalPreparation)
                .Concat(YujinSkillIds.CanonicalPrestige)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        if (normal == 9 &&
            duel == 16 &&
            preparation == 9 &&
            prestige == 3 &&
            expected.Length == 37 &&
            expected.All(ids.Contains))
        {
            pass.Add("Yujin canonical pool = N9/D16/P9/R3, total37");
        }
        else
        {
            fail.Add(
                $"Yujin pool mismatch N={normal}/9 D={duel}/16 P={preparation}/9 R={prestige}/3 IDs={ids.Count}/{expected.Length}");
        }

        SkillDefinition o =
            loadout.EnumerateAllDefinitions()
                .FirstOrDefault(
                    x => x?.SkillId ==
                         YujinSkillIds.AdvanceTiming);

        SkillDefinition p =
            loadout.EnumerateAllDefinitions()
                .FirstOrDefault(
                    x => x?.SkillId ==
                         YujinSkillIds.FinishIt);

        if (o != null &&
            o.ActionType == ActionType.Duel &&
            o.Color == SkillColor.Red &&
            o.OverrideEnergyCost &&
            o.EnergyCost == 2 &&
            Has0917Effect(
                o,
                Yujin0917EffectOperation
                    .ForceTargetMarkIgnition))
        {
            pass.Add("Yujin O 때를 앞당기다 asset/effect wired");
        }
        else
        {
            fail.Add("Yujin O asset/effect mismatch");
        }

        if (p != null &&
            p.ActionType == ActionType.Duel &&
            p.Color == SkillColor.Red &&
            p.OverrideEnergyCost &&
            p.EnergyCost == 4 &&
            p.Rulebreaker?.HasDynamicCost == true &&
            p.Rulebreaker.EnergyCostReductionPerCommittedUse == 1 &&
            p.Rulebreaker.ResolveMinimumEnergyCost() == 1 &&
            Has0917Effect(
                p,
                Yujin0917EffectOperation
                    .ConsumeAllSenseForPower))
        {
            pass.Add("Yujin P 끝장을 보다 dynamic-cost/effect wired");
        }
        else
        {
            fail.Add("Yujin P asset/effect mismatch");
        }
    }

    private static bool Has0917Effect(
        SkillDefinition skill,
        Yujin0917EffectOperation operation)
    {
        return
            skill?.EffectEntries != null &&
            skill.EffectEntries.Any(entry =>
                entry?.Definition is
                    Yujin0917SkillEffectDefinition effect &&
                effect.Operation == operation);
    }

    private static void VerifyOlaf(
        List<string> pass,
        List<string> pending,
        List<string> fail)
    {
        CharacterCombatLoadout loadout =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(
                OlafLoadoutPath);

        if (loadout == null)
        {
            fail.Add("Olaf loadout missing");
            return;
        }

        SkillDefinition repay =
            loadout.EnumerateAllDefinitions()
                .FirstOrDefault(
                    x => x?.SkillId ==
                         OlafSkillIds.PaybackAll);

        if (repay?.Rulebreaker?.Enabled == true &&
            repay.Rulebreaker.ApplyCurrentDisadvantageOrWorsePowerBonus &&
            repay.Rulebreaker.CurrentDisadvantageOrWorsePowerBonus == 4 &&
            !repay.Rulebreaker.ApplyCurrentLastStandPowerBonus)
        {
            pass.Add("Olaf 전부 되갚다 = B<=-30 +4 runtime data");
        }
        else
        {
            fail.Add("Olaf 전부 되갚다 0917 rulebreaker mismatch");
        }

        SkillDefinition shield =
            loadout.EnumerateAllDefinitions()
                .FirstOrDefault(
                    x => x?.SkillId ==
                         OlafSkillIds.ShieldWall);

        if (shield?.Description?.Contains("[0917_") == true)
            pass.Add("Olaf 방패벽 0917 data ownership marker present");
        else
            fail.Add("Olaf 방패벽 0917 texture patch missing");

        pending.Add(
            "Olaf Bite 확정 범위 vs 공통 C44 공식은 0917 문서 내부 충돌. " +
            "패치는 legacy hardcode를 끄고 card-specific data를 보존했으며 기획 결정 전까지 PENDING.");
    }

    private static void VerifyHifumi(
        List<string> pass,
        List<string> pending,
        List<string> fail)
    {
        int normal =
            HifumiSkillIds.CanonicalNormal.Length;

        int duel =
            HifumiSkillIds.CanonicalDuel.Length;

        int preparation =
            HifumiSkillIds.CanonicalPreparation.Length;

        int prestige =
            HifumiSkillIds.CanonicalPrestige.Length;

        if (normal == 3 &&
            duel == 9 &&
            preparation == 4 &&
            prestige == 3)
        {
            pass.Add("Hifumi 0917 ID structure = Normal3/Duel9/Preparation4/Prestige3 (19)");
        }
        else
        {
            fail.Add(
                $"Hifumi ID shape mismatch {normal}/{duel}/{preparation}/{prestige}");
        }

        const string mechanicPath =
            "Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs";

        string source =
            System.IO.File.Exists(mechanicPath)
                ? System.IO.File.ReadAllText(mechanicPath)
                : string.Empty;

        if (source.Contains("previewCounterCount") &&
            source.Contains("counterCount *= 2"))
        {
            pass.Add("Hifumi Goldan Bloom counter pool x2 source patch present");
        }
        else
        {
            fail.Add("Hifumi Goldan Bloom x2 source patch missing");
        }

        pending.Add(
            "Hifumi D~I + Normal③는 구조만 확정되고 필수 수치가 미정. " +
            "HifumiPendingSkills.json에 Unset schema로 기록했으며 플레이 가능한 SkillDefinition은 생성하지 않음.");
    }

    private static void VerifyRegistry(
        List<string> pass,
        List<string> fail)
    {
        RunFlowLiveContentRegistry registry =
            AssetDatabase.LoadAssetAtPath<RunFlowLiveContentRegistry>(
                "Assets/Resources/ProjectAbyss/RunFlowLiveContentRegistry.asset");

        if (registry?.EmotionAugmentCatalog != null)
            pass.Add("RunFlowLiveContentRegistry EmotionAugmentCatalog linked");
        else
            fail.Add("RunFlowLiveContentRegistry EmotionAugmentCatalog is null");
    }
}
#endif
