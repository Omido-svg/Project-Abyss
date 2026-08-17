#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 기존 올라프 CharacterData의 부위별 행동 분류를 현재 설계로 정규화한다.
/// 머리: 일반/결투/도사림/위세
/// 양손: 일반/결투/위세
/// 다리: 도사림
/// </summary>
public static class ProjectAbyssOlafPartSkillRuleRepair
{
    private const string MenuPath =
        "Tools/Project Abyss/Characters/Repair Olaf Part Skill Rules";

    private const string DesignDataPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Olaf_TODO_2026.asset";

    [MenuItem(MenuPath, false, 31)]
    public static void Repair()
    {
        HashSet<CharacterData> targets =
            new HashSet<CharacterData>();

        string[] bundleGuids =
            AssetDatabase.FindAssets("t:CharacterAuthoringBundle");

        foreach (string guid in bundleGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            CharacterAuthoringBundle bundle =
                AssetDatabase.LoadAssetAtPath<CharacterAuthoringBundle>(path);

            if (bundle?.Kind == CharacterAuthoringKind.Olaf &&
                bundle.CharacterData != null)
            {
                targets.Add(bundle.CharacterData);
            }
        }

        CharacterData direct =
            AssetDatabase.LoadAssetAtPath<CharacterData>(DesignDataPath);

        if (direct != null)
            targets.Add(direct);

        int changedAssets = 0;

        foreach (CharacterData data in targets)
        {
            if (RepairData(data))
                changedAssets++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message =
            changedAssets > 0
                ? $"올라프 CharacterData {changedAssets}개를 수정했습니다.\n\n" +
                  "머리: 일반/결투/도사림/위세\n" +
                  "양손: 일반/결투/위세\n" +
                  "다리: 도사림"
                : "수정할 올라프 CharacterData가 없거나 이미 정상입니다.";

        Debug.Log("[Olaf Part Skill Rule Repair] " + message);
        EditorUtility.DisplayDialog(
            "올라프 부위별 스킬 규칙 복구",
            message,
            "확인");
    }

    private static bool RepairData(CharacterData data)
    {
        if (data == null ||
            data.ActionSlots == null)
        {
            return false;
        }

        bool changed = false;

        foreach (CharacterSlotConfig slot in data.ActionSlots)
        {
            if (slot == null ||
                !slot.HasLinkedPart)
            {
                continue;
            }

            List<ActionType> expected =
                BuildExpectedTypes(slot.LinkedPartType);

            if (SameTypes(slot.AllowedActionTypes, expected))
                continue;

            Undo.RecordObject(data, "Repair Olaf Part Skill Rules");
            slot.AllowedActionTypes = expected;
            changed = true;
        }

        if (changed)
            EditorUtility.SetDirty(data);

        return changed;
    }

    private static List<ActionType> BuildExpectedTypes(
        PartType partType)
    {
        return partType switch
        {
            PartType.HEAD =>
                new List<ActionType>
                {
                    ActionType.NormalAttack,
                    ActionType.Duel,
                    ActionType.Preparation,
                    ActionType.Prestige
                },

            PartType.LEFT_HAND =>
                new List<ActionType>
                {
                    ActionType.NormalAttack,
                    ActionType.Duel,
                    ActionType.Prestige
                },

            PartType.RIGHT_HAND =>
                new List<ActionType>
                {
                    ActionType.NormalAttack,
                    ActionType.Duel,
                    ActionType.Prestige
                },

            PartType.LEGS =>
                new List<ActionType>
                {
                    ActionType.Preparation
                },

            _ => new List<ActionType>()
        };
    }

    private static bool SameTypes(
        IReadOnlyList<ActionType> current,
        IReadOnlyList<ActionType> expected)
    {
        if (current == null ||
            expected == null ||
            current.Count != expected.Count)
        {
            return false;
        }

        for (int index = 0;
             index < current.Count;
             index++)
        {
            if (current[index] != expected[index])
                return false;
        }

        return true;
    }
}
#endif
