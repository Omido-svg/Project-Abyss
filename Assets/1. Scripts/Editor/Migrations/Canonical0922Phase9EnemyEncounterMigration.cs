#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0922 Phase 9 — Enemy / Encounter / Boss canonical migration.
/// Only confirmed §12 values are written. Unspecified enemy names, skill values,
/// cover conflict rules and boss destruction content remain PENDING_CANONICAL.
/// </summary>
public static class Canonical0922Phase9EnemyEncounterMigration
{
    private const string EncounterFolder =
        "Assets/Resources/Canonical0922";
    private const string EncounterCatalogPath =
        EncounterFolder + "/EnemyEncounterCatalog.asset";

    private const string NormalEnemyLoadoutPath =
        "Assets/2. Data/Characters/Enemies/NormalEnemy/Loadout/NormalEnemy Combat Loadout.asset";

    private const string BossBundlePath =
        "Assets/2. Data/TestEncounters/Data/BossEnemy_Test_Bundle.asset";
    private const string BossDataPath =
        "Assets/2. Data/TestEncounters/Data/BossEnemy_Test_Data.asset";
    private const string BossPhasePath =
        "Assets/2. Data/TestEncounters/Data/BossEnemy_Test_Phase01.asset";
    private const string BossAPath =
        "Assets/2. Data/TestEncounters/Data/Stage1BossV5/Stage1Boss_A_Duel.asset";
    private const string BossBPath =
        "Assets/2. Data/TestEncounters/Data/Stage1BossV5/Stage1Boss_B_Duel.asset";
    private const string BossBFallbackPath =
        "Assets/2. Data/TestEncounters/Data/Stage1BossV5/Stage1Boss_B_Fallback.asset";

    private static readonly string[] NormalEnemyDataPaths =
    {
        "Assets/2. Data/Characters/Enemies/NormalEnemy/CharacterData/Normal EN Data 1.asset",
        "Assets/2. Data/Characters/Enemies/NormalEnemy/CharacterData/Normal EN Data 2.asset",
        "Assets/2. Data/Characters/Enemies/NormalEnemy/CharacterData/Normal EN Data 3.asset",
        "Assets/2. Data/Characters/Enemies/NormalEnemy/CharacterData/Normal EN Data 4.asset"
    };

    [MenuItem("Game System Verification/0922 Canonical/Phase 9 - Apply Enemy Encounter Migration")]
    public static void ApplyFromMenu()
    {
        Canonical0922EnemyEncounterCatalog catalog =
            EnsureEncounterCatalog();
        catalog.ResetToCanonical();
        EditorUtility.SetDirty(catalog);

        int normalDataTouched = NormalizeNormalEnemyData();
        int normalSkillsTouched = NormalizeNormalEnemySkillRng();
        bool bossSkills = NormalizeStage1BossSkills(
            out SkillDefinition skillA,
            out SkillDefinition skillB,
            out SkillDefinition fallbackB);
        bool bossPhase = NormalizeStage1BossPhase(
            skillA,
            skillB,
            fallbackB);
        bool bossData = NormalizeStage1BossData();
        bool bossBundle = NormalizeStage1BossBundle();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[0922 Phase9] Migration applied. " +
            $"EncounterCatalog={catalog.NormalEncounters?.Count ?? 0}, " +
            $"NormalEnemyDataTouched={normalDataTouched}, " +
            $"NormalSkillsTouched={normalSkillsTouched}, " +
            $"BossSkills={bossSkills}, BossPhase={bossPhase}, " +
            $"BossData={bossData}, BossBundle={bossBundle}. " +
            "(미정)은 PENDING_CANONICAL로 보존됩니다. " +
            "이제 Phase 9 Verify를 실행하세요.");
    }

    private static Canonical0922EnemyEncounterCatalog EnsureEncounterCatalog()
    {
        EnsureFolder(EncounterFolder);

        Canonical0922EnemyEncounterCatalog catalog =
            AssetDatabase.LoadAssetAtPath<Canonical0922EnemyEncounterCatalog>(
                EncounterCatalogPath);

        if (catalog != null)
        {
            MonoScript script =
                MonoScript.FromScriptableObject(catalog);

            if (script != null &&
                script.GetClass() == typeof(Canonical0922EnemyEncounterCatalog))
            {
                return catalog;
            }

            // Phase 9 R1 could create the catalog while the ScriptableObject
            // lived in Canonical0922EnemyEncounterRules.cs. Unity compiles that
            // type but cannot bind a stable MonoScript asset when the file name
            // differs from the ScriptableObject class name. Recreate it once
            // after the R2 file split so editor restart/domain reload remains safe.
            AssetDatabase.DeleteAsset(EncounterCatalogPath);
            catalog = null;
        }
        else if (AssetDatabase.LoadMainAssetAtPath(EncounterCatalogPath) != null)
        {
            // Broken/missing-script R1 asset at the canonical path.
            AssetDatabase.DeleteAsset(EncounterCatalogPath);
        }

        catalog =
            ScriptableObject.CreateInstance<Canonical0922EnemyEncounterCatalog>();
        catalog.ResetToCanonical();
        AssetDatabase.CreateAsset(catalog, EncounterCatalogPath);
        return catalog;
    }

    private static int NormalizeNormalEnemyData()
    {
        int touched = 0;

        foreach (string path in NormalEnemyDataPaths)
        {
            CharacterData data =
                AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data == null)
                continue;

            SerializedObject so = new SerializedObject(data);
            SetInt(so, "maxEnergy", 3);
            SetBool(so, "EnableStaggerGauge", true);
            SetBool(so, "OverrideMaxStaggerGauge", true);
            SetInt(so, "MaxStaggerGauge", Canonical0922EnemyEncounterRules.NormalEnemyStaggerMax);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            touched++;
        }

        return touched;
    }

    private static int NormalizeNormalEnemySkillRng()
    {
        CharacterCombatLoadout loadout =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(
                NormalEnemyLoadoutPath);
        if (loadout == null)
            return 0;

        int touched = 0;
        HashSet<SkillDefinition> skills = new();
        if (loadout.NormalSkillPool != null)
        {
            foreach (SkillDefinition skill in loadout.NormalSkillPool)
                if (skill != null) skills.Add(skill);
        }
        if (loadout.DuelSkillPool != null)
        {
            foreach (SkillDefinition skill in loadout.DuelSkillPool)
                if (skill != null) skills.Add(skill);
        }

        foreach (SkillDefinition skill in skills)
        {
            skill.ResolverType = SkillResolverType.Dice;
            skill.DiceMin = 1;
            skill.DiceMax = 8;
            skill.ExchangeRollCount = Canonical0922EnemyEncounterRules.NormalEnemyRollCount;
            skill.RollReusePolicy = SkillRollReusePolicy.RollEachExchange;
            skill.Rolls ??= new List<SkillRollData>();
            while (skill.Rolls.Count < Canonical0922EnemyEncounterRules.NormalEnemyRollCount)
                skill.Rolls.Add(new SkillRollData());
            while (skill.Rolls.Count > Canonical0922EnemyEncounterRules.NormalEnemyRollCount)
                skill.Rolls.RemoveAt(skill.Rolls.Count - 1);

            for (int i = 0; i < skill.Rolls.Count; i++)
            {
                SkillRollData roll = skill.Rolls[i] ?? new SkillRollData();
                skill.Rolls[i] = roll;
                roll.Index = i;
                roll.RngSource = RollRngSource.Dice;
                roll.MinPower = 1;
                roll.MaxPower = 8;
                roll.ReuseValueAcrossAction = false;
            }

            EditorUtility.SetDirty(skill);
            touched++;
        }

        return touched;
    }

    private static bool NormalizeStage1BossSkills(
        out SkillDefinition skillA,
        out SkillDefinition skillB,
        out SkillDefinition fallbackB)
    {
        skillA = AssetDatabase.LoadAssetAtPath<SkillDefinition>(BossAPath);
        skillB = AssetDatabase.LoadAssetAtPath<SkillDefinition>(BossBPath);
        fallbackB = AssetDatabase.LoadAssetAtPath<SkillDefinition>(BossBFallbackPath);

        if (skillA == null || skillB == null || fallbackB == null)
            return false;

        ConfigureFixedSkill(
            skillA,
            ActionType.Duel,
            basePower: 13,
            rollCount: 3,
            energyCost: 1,
            "[CANONICAL_0922_PHASE9] Stage1 Boss A: 3굴림 · 위력13 · 결투 · 비용1 · 파괴권한 없음. 색은 PENDING_CANONICAL.");

        ConfigureFixedSkill(
            skillB,
            ActionType.Duel,
            basePower: 14,
            rollCount: 2,
            energyCost: 1,
            "[CANONICAL_0922_PHASE9] Stage1 Boss B: 2굴림 · 위력14 · 결투 · 비용1 · 파괴권한 없음. 색은 PENDING_CANONICAL.");

        ConfigureFixedSkill(
            fallbackB,
            ActionType.NormalAttack,
            basePower: 14,
            rollCount: 2,
            energyCost: 0,
            "[CANONICAL_0922_PHASE9] B의 빛 부족 fallback: 결투 플래그만 해제, 2굴림 · 위력14 구성 유지.");

        return true;
    }

    private static void ConfigureFixedSkill(
        SkillDefinition skill,
        ActionType actionType,
        int basePower,
        int rollCount,
        int energyCost,
        string description)
    {
        if (skill == null)
            return;

        skill.ActionType = actionType;
        skill.BasePower = basePower;
        skill.CanBreakPart = false;
        skill.BreakMode = PartBreakMode.None;
        skill.OverrideCanClash = true;
        skill.CanClashValue = true;
        skill.OverrideEnergyCost = true;
        skill.EnergyCost = Mathf.Max(0, energyCost);
        skill.ExchangeRollCount = Mathf.Clamp(rollCount, 1, 8);
        skill.RollReusePolicy = SkillRollReusePolicy.RollEachExchange;

        skill.Rolls ??= new List<SkillRollData>();
        while (skill.Rolls.Count < rollCount)
            skill.Rolls.Add(new SkillRollData());
        while (skill.Rolls.Count > rollCount)
            skill.Rolls.RemoveAt(skill.Rolls.Count - 1);

        for (int i = 0; i < skill.Rolls.Count; i++)
        {
            SkillRollData roll = skill.Rolls[i] ?? new SkillRollData();
            skill.Rolls[i] = roll;
            roll.Index = i;
            roll.MinPower = basePower;
            roll.MaxPower = basePower;
            roll.ReuseValueAcrossAction = false;
        }

        skill.Description = description;
        EditorUtility.SetDirty(skill);
    }

    private static bool NormalizeStage1BossPhase(
        SkillDefinition skillA,
        SkillDefinition skillB,
        SkillDefinition fallbackB)
    {
        BossPhaseData phase =
            AssetDatabase.LoadAssetAtPath<BossPhaseData>(BossPhasePath);
        if (phase == null || skillA == null || skillB == null || fallbackB == null)
            return false;

        phase.PhaseId = "STAGE1_CANONICAL_0922";
        phase.DisplayName = "Stage 1 Canonical 0922";
        phase.MinimumTurn = 1;
        phase.EnterAtOrBelowHpRate = 1f;

        phase.SlotConfigs ??= new List<CharacterSlotConfig>();
        while (phase.SlotConfigs.Count < 3)
            phase.SlotConfigs.Add(new CharacterSlotConfig());
        while (phase.SlotConfigs.Count > 3)
            phase.SlotConfigs.RemoveAt(phase.SlotConfigs.Count - 1);

        ConfigureBossSlot(phase.SlotConfigs[0], "A_01", "A", skillA, null);
        ConfigureBossSlot(phase.SlotConfigs[1], "A_02", "A", skillA, null);
        ConfigureBossSlot(phase.SlotConfigs[2], "B_01", "B", skillB, fallbackB);

        phase.NormalSkillPool ??= new List<SkillDefinition>();
        phase.DuelSkillPool ??= new List<SkillDefinition>();
        phase.PreparationSkillPool ??= new List<SkillDefinition>();
        phase.PrestigeSkillPool ??= new List<SkillDefinition>();
        phase.NormalSkillPool.Clear();
        phase.NormalSkillPool.Add(fallbackB);
        phase.DuelSkillPool.Clear();
        phase.DuelSkillPool.Add(skillA);
        phase.DuelSkillPool.Add(skillB);
        phase.PreparationSkillPool.Clear();
        phase.PrestigeSkillPool.Clear();

        EditorUtility.SetDirty(phase);
        return true;
    }

    private static void ConfigureBossSlot(
        CharacterSlotConfig slot,
        string id,
        string displayName,
        SkillDefinition fixedSkill,
        SkillDefinition fallback)
    {
        if (slot == null)
            return;

        slot.SlotId = id;
        slot.DisplayName = displayName;
        slot.Enabled = true;
        slot.HasLinkedPart = false;
        slot.FixedSkill = fixedSkill;
        slot.InsufficientEnergyFallbackSkill = fallback;
        slot.TargetingPolicy = AITargetingPolicy.RandomValid;
        slot.AllowedActionTypes ??= new List<ActionType>();
        slot.AllowedActionTypes.Clear();
        slot.AllowedActionTypes.Add(ActionType.Duel);
        if (fallback != null)
            slot.AllowedActionTypes.Add(ActionType.NormalAttack);
    }

    private static bool NormalizeStage1BossData()
    {
        CharacterData data =
            AssetDatabase.LoadAssetAtPath<CharacterData>(BossDataPath);
        if (data == null)
            return false;

        SerializedObject so = new SerializedObject(data);
        SetInt(so, "maxEnergy", 2);
        SetBool(so, "EnableStaggerGauge", true);
        SetBool(so, "OverrideMaxStaggerGauge", true);
        SetInt(so, "MaxStaggerGauge", 400);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        return true;
    }

    private static bool NormalizeStage1BossBundle()
    {
        CharacterAuthoringBundle bundle =
            AssetDatabase.LoadAssetAtPath<CharacterAuthoringBundle>(BossBundlePath);
        if (bundle == null)
            return false;

        SerializedObject so = new SerializedObject(bundle);
        SetBool(so, "useElitePostureRotation", true);

        SerializedProperty posture = so.FindProperty("elitePostureSettings");
        if (posture != null)
        {
            SetRelativeInt(posture, "MinimumTurns", 2);
            SetRelativeInt(posture, "MaximumTurns", 3);
            SetRelativeInt(posture, "NormalAttackSlotLimit", 3);
            SetRelativeInt(posture, "CrouchingAttackSlotLimit", 2);
            SetRelativeInt(posture, "OffensiveAttackSlotLimit", 4);
        }

        SerializedProperty parts = so.FindProperty("eliteBodyPartDefinitions");
        if (parts != null && parts.isArray && parts.arraySize == 5)
        {
            for (int i = 0; i < parts.arraySize; i++)
            {
                SerializedProperty element = parts.GetArrayElementAtIndex(i);
                SerializedProperty hp = element?.FindPropertyRelative("MaxPartHP");
                if (hp != null)
                    hp.floatValue = 150f;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bundle);
        return true;
    }

    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty property = so?.FindProperty(name);
        if (property != null)
            property.intValue = value;
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty property = so?.FindProperty(name);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetRelativeInt(SerializedProperty parent, string name, int value)
    {
        SerializedProperty property = parent?.FindPropertyRelative(name);
        if (property != null)
            property.intValue = value;
    }

    private static void EnsureFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
