#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class Stage1BossV5Migration
{
    private const string Root = "Assets/2. Data/TestEncounters/Data/Stage1BossV5";
    private const string BossDataPath = "Assets/2. Data/TestEncounters/Data/BossEnemy_Test_Data.asset";
    private const string Phase01Path = "Assets/2. Data/TestEncounters/Data/BossEnemy_Test_Phase01.asset";
    private const string Phase02Path = "Assets/2. Data/TestEncounters/Data/BossEnemy_Test_Phase02.asset";

    [MenuItem("Project Abyss/Gameplay v5/Apply Stage 1 Boss A-A-B Data")]
    public static void Apply()
    {
        EnsureFolders();

        SkillDefinition a = LoadOrCreate(
            "Stage1Boss_A_Duel.asset", "STAGE1_BOSS_A", "A",
            ActionType.Duel, 1,
            new[] { CombatRollType.Stagger, CombatRollType.Attack, CombatRollType.Attack },
            13);

        SkillDefinition b = LoadOrCreate(
            "Stage1Boss_B_Duel.asset", "STAGE1_BOSS_B", "B",
            ActionType.Duel, 1,
            new[] { CombatRollType.Attack, CombatRollType.Attack },
            14);

        SkillDefinition fallback = LoadOrCreate(
            "Stage1Boss_B_Fallback.asset", "STAGE1_BOSS_B_FALLBACK", "B",
            ActionType.NormalAttack, 0,
            new[] { CombatRollType.Attack, CombatRollType.Attack },
            14);

        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(BossDataPath);
        if (data != null)
        {
            Undo.RecordObject(data, "Gameplay v5 Stage1 Boss CharacterData");
            data.CombatantTier = CombatantTier.Boss;
            data.maxEnergy = 2;
            data.TurnStartEnergyPolicy = TurnStartEnergyPolicy.RefillToMaximum;
            data.OverrideMaxStaggerGauge = false;
            EditorUtility.SetDirty(data);
        }
        else
        {
            Debug.LogWarning($"[Gameplay v5] Boss CharacterData not found: {BossDataPath}");
        }

        ConfigurePhase(AssetDatabase.LoadAssetAtPath<BossPhaseData>(Phase01Path), a, b, fallback);
        ConfigurePhase(AssetDatabase.LoadAssetAtPath<BossPhaseData>(Phase02Path), a, b, fallback);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Gameplay v5] Stage 1 Boss A/A/B migration complete.");
    }

    private static void ConfigurePhase(
        BossPhaseData phase,
        SkillDefinition a,
        SkillDefinition b,
        SkillDefinition fallback)
    {
        if (phase == null) return;

        Undo.RecordObject(phase, "Gameplay v5 Stage1 Boss Phase");
        phase.SlotConfigs = new List<CharacterSlotConfig>
        {
            CreateSlot("BOSS_A_01", "A 1", a, null),
            CreateSlot("BOSS_A_02", "A 2", a, null),
            CreateSlot("BOSS_B_03", "B", b, fallback)
        };
        phase.NormalSkillPool = new List<SkillDefinition> { fallback };
        phase.DuelSkillPool = new List<SkillDefinition> { a, b };
        phase.PreparationSkillPool = new List<SkillDefinition>();
        phase.PrestigeSkillPool = new List<SkillDefinition>();
        phase.DuelWeight = 1f;
        phase.NormalWeight = 1f;
        EditorUtility.SetDirty(phase);
    }

    private static CharacterSlotConfig CreateSlot(
        string id,
        string displayName,
        SkillDefinition fixedSkill,
        SkillDefinition fallback)
    {
        return new CharacterSlotConfig
        {
            SlotId = id,
            DisplayName = displayName,
            Enabled = true,
            HasLinkedPart = false,
            FixedSkill = fixedSkill,
            InsufficientEnergyFallbackSkill = fallback,
            TargetingPolicy = AITargetingPolicy.RandomValid,
            AllowedActionTypes = new List<ActionType>
            {
                ActionType.NormalAttack,
                ActionType.Duel
            }
        };
    }

    private static SkillDefinition LoadOrCreate(
        string fileName,
        string skillId,
        string displayName,
        ActionType actionType,
        int energyCost,
        CombatRollType[] rollTypes,
        int power)
    {
        string path = $"{Root}/{fileName}";
        SkillDefinition definition = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<SkillDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        Undo.RecordObject(definition, "Gameplay v5 Stage1 Boss Skill");
        SerializedObject serialized = new SerializedObject(definition);
        SerializedProperty skillIdProperty = serialized.FindProperty("skillId");
        if (skillIdProperty != null)
            skillIdProperty.stringValue = skillId;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        definition.SkillName = displayName;
        definition.ActionType = actionType;
        definition.BasePower = 0;
        definition.OverrideEnergyCost = true;
        definition.EnergyCost = Mathf.Max(0, energyCost);
        definition.CanBreakPart = true;
        definition.OverrideCanClash = true;
        definition.CanClashValue = actionType == ActionType.Duel;
        definition.Rolls = new List<SkillRollData>();

        for (int i = 0; i < rollTypes.Length; i++)
        {
            definition.Rolls.Add(new SkillRollData
            {
                Index = i,
                Type = rollTypes[i],
                DiceMode = DicePowerMode.AbsoluteRange,
                MinPower = power,
                MaxPower = power,
                OverridePhysicalType = false
            });
        }

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static void EnsureFolders()
    {
        string[] segments = Root.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }
}
#endif
