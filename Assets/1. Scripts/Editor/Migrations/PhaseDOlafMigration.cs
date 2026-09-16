#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase D / Olaf 최소 Core Data 이관.
/// 전체 스킬 풀/만렙 수치 이관은 Phase E(O-02) 범위이므로 여기서는
/// 기존 로드아웃 GUID를 유지하면서 O-03/O-04에 필요한 자산 계약만 갱신한다.
/// </summary>
[InitializeOnLoad]
public static class PhaseDOlafMigration
{
    private const string ShowOffPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Skills/Olaf_ShowOff.asset";
    private const string StandardPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Skills/Olaf_Standard.asset";
    private const string RendPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Skills/Olaf_Rend.asset";

    static PhaseDOlafMigration()
    {
        EditorApplication.delayCall += ApplyIfNeeded;
    }

    [MenuItem("Tools/Project Abyss/Migrations/Phase D/Apply Olaf Runtime Migration")]
    public static void ApplyFromMenu()
    {
        bool changed = Apply();
        Debug.Log(changed
            ? "[Phase D Olaf] Core Data migration applied."
            : "[Phase D Olaf] Core Data migration already up to date or source assets were not found.");
    }

    private static void ApplyIfNeeded()
    {
        Apply();
    }

    private static bool Apply()
    {
        bool changed = false;
        changed |= MigrateBloto();
        changed |= MigrateStandard();
        changed |= MigrateRendUtilityContract();

        if (changed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        return changed;
    }

    private static bool MigrateBloto()
    {
        SkillDefinition definition =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(ShowOffPath);
        if (definition == null)
            return false;

        bool changed = false;
        SerializedObject serialized = new SerializedObject(definition);
        SerializedProperty id = serialized.FindProperty("skillId");
        if (id != null && id.stringValue != OlafSkillIds.Bloto)
        {
            id.stringValue = OlafSkillIds.Bloto;
            changed = true;
        }

        changed |= Set(ref definition.SkillName, "블로토");
        changed |= Set(ref definition.Description,
            "blót — 자기 최저 체력 부위 1개를 약화하고 광기 +1. 적에게 공포(위력 -1, 3턴, 비누적/재부여 시 갱신)를 부여한다.");

        if (definition.ActionType != ActionType.Preparation)
        {
            definition.ActionType = ActionType.Preparation;
            changed = true;
        }

        if (definition.PreparationTier != PreparationTier.Strong)
        {
            definition.PreparationTier = PreparationTier.Strong;
            changed = true;
        }

        if (!definition.OverrideEnergyCost || definition.EnergyCost != 1)
        {
            definition.OverrideEnergyCost = true;
            definition.EnergyCost = 1;
            changed = true;
        }

        if (definition.CanBreakPart || definition.BreakMode != PartBreakMode.None)
        {
            definition.CanBreakPart = false;
            definition.BreakMode = PartBreakMode.None;
            changed = true;
        }

        if (definition.name != "Olaf_Bloto")
        {
            definition.name = "Olaf_Bloto";
            changed = true;
        }

        if (changed)
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        return changed;
    }

    private static bool MigrateStandard()
    {
        SkillDefinition definition =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(StandardPath);
        if (definition == null)
            return false;

        bool changed = false;
        if (!definition.CanBreakPart)
        {
            definition.CanBreakPart = true;
            changed = true;
        }

        if (definition.BreakMode != PartBreakMode.WeakenedOnly)
        {
            definition.BreakMode = PartBreakMode.WeakenedOnly;
            changed = true;
        }

        definition.Rulebreaker ??= new SkillRulebreakerSettings();
        if (!definition.Rulebreaker.Enabled)
        {
            definition.Rulebreaker.Enabled = true;
            changed = true;
        }

        if (!definition.Rulebreaker.ExplodeBleedingOncePerAction)
        {
            definition.Rulebreaker.ExplodeBleedingOncePerAction = true;
            changed = true;
        }

        // 0916 §17.6: 폭발 계수는 (미정). 여기서 10 같은 임시값을 확정하지 않는다.
        // 기존 새 필드가 0이면 그대로 Unset/disabled 상태를 유지한다.
        if (changed)
            EditorUtility.SetDirty(definition);

        return changed;
    }

    private static bool MigrateRendUtilityContract()
    {
        SkillDefinition definition =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(RendPath);
        if (definition == null)
            return false;

        bool changed = false;
        if (definition.CanBreakPart)
        {
            definition.CanBreakPart = false;
            changed = true;
        }

        if (definition.BreakMode != PartBreakMode.None)
        {
            definition.BreakMode = PartBreakMode.None;
            changed = true;
        }

        if (changed)
            EditorUtility.SetDirty(definition);

        return changed;
    }

    private static bool Set(ref string field, string value)
    {
        if (field == value)
            return false;
        field = value;
        return true;
    }
}
#endif
