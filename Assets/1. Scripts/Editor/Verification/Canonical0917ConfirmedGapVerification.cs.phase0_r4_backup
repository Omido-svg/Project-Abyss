#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0917 구현 현황 문서의 확정 누락 3건만 검증한다.
/// 기획 미정/TEMP 항목은 이 gate의 FAIL 대상이 아니다.
/// </summary>
public static class Canonical0917ConfirmedGapVerification
{
    private const string HifumiMechanicPath =
        "Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs";

    private const string StatusEffectPath =
        "Assets/1. Scripts/Runtime/Status/StatusEffect.cs";

    private const string CommonStatusesPath =
        "Assets/1. Scripts/Runtime/Status/Common/CommonCombatStatuses.cs";

    private const string StatusFactoryPath =
        "Assets/1. Scripts/Runtime/Skills/Effects/Common/StatusEffectFactory.cs";

    private const string StaggerGaugePath =
        "Assets/1. Scripts/Runtime/Characters/CharacterBase/StaggerGaugeMechanic.cs";

    private const string HifumiLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Hifumi/Hifumi_Loadout.asset";

    private const string OlafLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Olaf_TODO_Loadout.asset";

    [MenuItem("Game System Verification/0917 Spec Patch/Verify Confirmed Missing Rules")]
    public static void VerifyFromMenu()
    {
        List<string> pass = new();
        List<string> fail = new();
        List<string> details = new();

        VerifyHifumiRecklessBet(pass, fail, details);
        VerifySturdyDisarm(pass, fail, details);
        VerifyHifumiCommonPreparations(pass, fail, details);

        string summary =
            "[0917 Confirmed Missing Rules Verification]\n" +
            $"PASS={pass.Count} FAIL={fail.Count}\n\n" +
            "PASS\n- " + string.Join("\n- ", pass) +
            "\n\nDETAILS\n- " + string.Join("\n- ", details) +
            "\n\nFAIL\n- " + string.Join("\n- ", fail);

        if (fail.Count > 0)
            Debug.LogError(summary);
        else
            Debug.Log(summary);
    }

    private static void VerifyHifumiRecklessBet(
        List<string> pass,
        List<string> fail,
        List<string> details)
    {
        string source = Read(HifumiMechanicPath);

        bool fields = source.Contains(
            "[0917_CONFIRMED_GAP:HIFUMI_C_FIELDS]",
            StringComparison.Ordinal);

        bool queue =
            source.Contains(
                "[0917_CONFIRMED_GAP:HIFUMI_C_QUEUE]",
                StringComparison.Ordinal) &&
            source.Contains(
                "recklessBetIncomingDamagePenaltyQueued += 3;",
                StringComparison.Ordinal);

        bool activate = source.Contains(
            "[0917_CONFIRMED_GAP:HIFUMI_C_TURN_START]",
            StringComparison.Ordinal);

        bool damage = source.Contains(
            "[0917_CONFIRMED_GAP:HIFUMI_C_DAMAGE]",
            StringComparison.Ordinal);

        bool recklessBranchStillUsesLegacyNoOp =
            source.Contains(
                "else if (id == HifumiSkillIds.RecklessBet)",
                StringComparison.Ordinal) &&
            ExtractRecklessBetBlock(source).Contains(
                "QueueNextTurnRupture();",
                StringComparison.Ordinal);

        if (fields && queue && activate && damage && !recklessBranchStillUsesLegacyNoOp)
        {
            pass.Add("Hifumi C 무모한 베팅 = 뼈<70 시 다음 턴 받는 HP 피해 +3/사용회수");
        }
        else
        {
            fail.Add("Hifumi C next-turn +3 patch incomplete");
        }

        details.Add(
            $"Hifumi C markers fields={fields}, queue={queue}, activate={activate}, damage={damage}, legacyNoOpInC={recklessBranchStillUsesLegacyNoOp}");
    }

    private static void VerifySturdyDisarm(
        List<string> pass,
        List<string> fail,
        List<string> details)
    {
        string statusBase = Read(StatusEffectPath);
        string common = Read(CommonStatusesPath);
        string factory = Read(StatusFactoryPath);
        string stagger = Read(StaggerGaugePath);

        bool api = statusBase.Contains(
            "[0917_CONFIRMED_GAP:STAGGER_STATUS_API]",
            StringComparison.Ordinal);

        bool definitions =
            common.Contains(
                "[0917_CONFIRMED_GAP:STURDY_DISARM]",
                StringComparison.Ordinal) &&
            common.Contains(
                "-Stack;",
                StringComparison.Ordinal) &&
            common.Contains(
                "Stack;",
                StringComparison.Ordinal);

        bool factorySource = factory.Contains(
            "[0917_CONFIRMED_GAP:STURDY_DISARM_FACTORY]",
            StringComparison.Ordinal);

        bool redPath = stagger.Contains(
            "[0917_CONFIRMED_GAP:STAGGER_RED_PATH]",
            StringComparison.Ordinal);

        bool bluePath = stagger.Contains(
            "[0917_CONFIRMED_GAP:STAGGER_BLUE_PATH]",
            StringComparison.Ordinal);

        bool helper = stagger.Contains(
            "[0917_CONFIRMED_GAP:STAGGER_FLAT_HELPER]",
            StringComparison.Ordinal);

        // 이 verifier는 패치 적용 뒤 재컴파일 후 실행한다.
        // Factory가 실제 인스턴스를 내는지까지 확인한다.
        StatusEffect sturdy =
            StatusEffectFactory.Create(StatusEffectId.Sturdy, 2);
        StatusEffect disarm =
            StatusEffectFactory.Create(StatusEffectId.Disarm, 2);

        bool factoryRuntime =
            sturdy != null && sturdy.GetType().Name == nameof(SturdyStatus) &&
            disarm != null && disarm.GetType().Name == nameof(DisarmStatus);

        if (api && definitions && factorySource && redPath && bluePath && helper && factoryRuntime)
        {
            pass.Add("견고/무장해제 = 흐트러짐 내성 후 flat -N/+N, RED/BLUE 양 경로 연결");
        }
        else
        {
            fail.Add("Sturdy/Disarm stagger-damage patch incomplete");
        }

        details.Add(
            $"Sturdy/Disarm api={api}, defs={definitions}, factorySource={factorySource}, factoryRuntime={factoryRuntime}, red={redPath}, blue={bluePath}, helper={helper}");
    }

    private static void VerifyHifumiCommonPreparations(
        List<string> pass,
        List<string> fail,
        List<string> details)
    {
        CharacterCombatLoadout hifumi =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(HifumiLoadoutPath);
        CharacterCombatLoadout olaf =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(OlafLoadoutPath);

        bool valid =
            hifumi != null &&
            olaf != null &&
            hifumi.CommonPreparationPool != null &&
            olaf.CommonPreparationPool != null &&
            hifumi.CommonPreparationPool.Count == 2 &&
            olaf.CommonPreparationPool.Count == 2 &&
            hifumi.CommonPreparationPool[0] == olaf.CommonPreparationPool[0] &&
            hifumi.CommonPreparationPool[1] == olaf.CommonPreparationPool[1] &&
            hifumi.CommonPreparationPool[0] != null &&
            hifumi.CommonPreparationPool[1] != null;

        if (valid)
        {
            pass.Add("Hifumi CommonPreparationPool = 웅크리기/노려보기 2장 연결");
            details.Add(
                "Hifumi common preparations = " +
                hifumi.CommonPreparationPool[0].SkillId + ", " +
                hifumi.CommonPreparationPool[1].SkillId);
        }
        else
        {
            fail.Add("Hifumi common preparation pool mismatch");
            details.Add(
                $"Hifumi common count={hifumi?.CommonPreparationPool?.Count ?? -1}, Olaf common count={olaf?.CommonPreparationPool?.Count ?? -1}");
        }
    }

    private static string ExtractRecklessBetBlock(string source)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        const string startToken =
            "else if (id == HifumiSkillIds.RecklessBet)";
        const string endToken =
            "private void OnDamageResolved";

        int start = source.IndexOf(startToken, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        int end = source.IndexOf(endToken, start, StringComparison.Ordinal);
        if (end < 0)
            end = source.Length;

        return source.Substring(start, end - start);
    }

    private static string Read(string path) =>
        File.Exists(path)
            ? File.ReadAllText(path)
            : string.Empty;
}
#endif
