#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ProjectAbyssYujinWeaponSkillRepair
{
    private const string MenuPath =
        "Tools/Project Abyss/Characters/Repair Yujin Weapon + Skill UI v1.2.3";

    private const string DataPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_2026.asset";

    private const string LoadoutPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_Loadout.asset";

    private const string BundlePath =
        "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_Bundle.asset";

    private const string PrefabPath =
        "Assets/2. Data/TestEncounters/Prefabs/Yujin_Test.prefab";

    [MenuItem(MenuPath, false, 42)]
    public static void Repair()
    {
        try
        {
            // 스킬 Definition과 장착 목록을 최신 TODO 설계로 다시 맞춘다.
            ProjectAbyssOlafYujinDesignMigration.Apply();

            CharacterData data =
                AssetDatabase.LoadAssetAtPath<CharacterData>(
                    DataPath);

            CharacterCombatLoadout loadout =
                AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(
                    LoadoutPath);

            CharacterAuthoringBundle bundle =
                AssetDatabase.LoadAssetAtPath<CharacterAuthoringBundle>(
                    BundlePath);

            if (data == null ||
                loadout == null)
            {
                throw new InvalidOperationException(
                    "유진 CharacterData 또는 CombatLoadout을 찾지 못했습니다.");
            }

            data.CombatLoadout = loadout;
            data.ActionSlots =
                BuildAllCategorySlots();

            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(loadout);

            RepairTestPrefab(
                data,
                loadout,
                bundle);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "유진 무기·스킬 UI 복구 완료",
                "적용 내용\n\n" +
                "- 무기 변경: 턴당 무제한\n" +
                "- 백우/적설/낙일 전환 UI 애니메이션\n" +
                "- 모든 유진 부위에서 일반·결투·도사림·위세 표시\n" +
                $"- 일반 장착 {loadout.NormalSkills.Count}개\n" +
                $"- 결투 장착 {loadout.DuelSkills.Count}개\n" +
                $"- 도사림 장착 {loadout.PreparationSkills.Count}개\n" +
                $"- 위세 장착 {loadout.PrestigeSkills.Count}개\n\n" +
                "위세는 기존대로 한 턴 1회 정책을 유지합니다.",
                "확인");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "유진 무기·스킬 UI 복구 실패",
                exception.Message,
                "확인");
        }
    }

    private static List<CharacterSlotConfig>
        BuildAllCategorySlots()
    {
        return new List<CharacterSlotConfig>
        {
            CreateSlot("HEAD", "머리", PartType.HEAD),
            CreateSlot("LEFT_ARM", "왼팔", PartType.LEFT_HAND),
            CreateSlot("RIGHT_ARM", "오른팔", PartType.RIGHT_HAND),
            CreateSlot("LEGS", "다리", PartType.LEGS)
        };
    }

    private static CharacterSlotConfig CreateSlot(
        string id,
        string displayName,
        PartType part)
    {
        return new CharacterSlotConfig
        {
            SlotId = id,
            DisplayName = displayName,
            Enabled = true,
            HasLinkedPart = true,
            LinkedPartType = part,
            OverrideSpeedRange = false,
            AllowedActionTypes =
                new List<ActionType>
                {
                    ActionType.NormalAttack,
                    ActionType.Duel,
                    ActionType.Preparation,
                    ActionType.Prestige
                }
        };
    }

    private static void RepairTestPrefab(
        CharacterData data,
        CharacterCombatLoadout loadout,
        CharacterAuthoringBundle bundle)
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);

        if (prefab == null)
            return;

        GameObject root =
            PrefabUtility.LoadPrefabContents(
                PrefabPath);

        try
        {
            Yujin yujin =
                root.GetComponent<Yujin>();

            if (yujin == null)
            {
                throw new InvalidOperationException(
                    "Yujin_Test.prefab 루트에서 Yujin 컴포넌트를 찾지 못했습니다.");
            }

            yujin.ConfigureAuthoringCore(
                data,
                loadout);

            CharacterAuthoringLink link =
                root.GetComponent<CharacterAuthoringLink>();

            if (link != null &&
                bundle != null)
            {
                link.Configure(bundle);
                EditorUtility.SetDirty(link);
            }

            EditorUtility.SetDirty(yujin);

            PrefabUtility.SaveAsPrefabAsset(
                root,
                PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
