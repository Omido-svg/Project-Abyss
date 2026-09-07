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

    // Stage 1 Boss는 아직 전용 Timeline authoring이 없으므로, 현재 프로젝트의
    // 검증된 EliteEnemy Timeline Visual을 임시 presentation template로 사용한다.
    // GUID를 우선 사용해 Asset 이동에도 견디고, path는 복구 fallback이다.
    private const string DuelVisualTemplateGuid =
        "203df655b02da1844a4cdc12ead711a5";

    private const string DuelVisualTemplatePath =
        "Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/Cutscenes/" +
        "정예병의 압박(결투)/정예병의 압박(결투)_Timeline_Visual.asset";

    private const string NormalVisualTemplateGuid =
        "c111b34dea6a24d41b28f2de6af1d4c6";

    private const string NormalVisualTemplatePath =
        "Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/Cutscenes/" +
        "정예병의 참격(일반공격)/정예병의 참격(일반공격)_Timeline_Visual.asset";

    [MenuItem("Project Abyss/Gameplay v5/Apply Stage 1 Boss A-A-B Data")]
    public static void Apply()
    {
        EnsureFolders();

        SkillVisualDefinition duelVisualTemplate =
            LoadVisualTemplate(
                DuelVisualTemplateGuid,
                DuelVisualTemplatePath);

        SkillVisualDefinition normalVisualTemplate =
            LoadVisualTemplate(
                NormalVisualTemplateGuid,
                NormalVisualTemplatePath);

        if (duelVisualTemplate == null ||
            normalVisualTemplate == null)
        {
            Debug.LogError(
                "[Gameplay v5] Stage 1 Boss presentation template을 찾지 못했습니다. " +
                "A/B Skill asset은 변경하지 않았습니다.");
            return;
        }

        SkillDefinition a = LoadOrCreate(
            "Stage1Boss_A_Duel.asset", "STAGE1_BOSS_A", "A",
            ActionType.Duel, 1,
            new[] { CombatRollType.Stagger, CombatRollType.Attack, CombatRollType.Attack },
            13,
            duelVisualTemplate);

        SkillDefinition b = LoadOrCreate(
            "Stage1Boss_B_Duel.asset", "STAGE1_BOSS_B", "B",
            ActionType.Duel, 1,
            new[] { CombatRollType.Attack, CombatRollType.Attack },
            14,
            duelVisualTemplate);

        SkillDefinition fallback = LoadOrCreate(
            "Stage1Boss_B_Fallback.asset", "STAGE1_BOSS_B_FALLBACK", "B",
            ActionType.NormalAttack, 0,
            new[] { CombatRollType.Attack, CombatRollType.Attack },
            14,
            normalVisualTemplate);

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
        Debug.Log("[Gameplay v5] Stage 1 Boss A/A/B migration complete. Energy=2 + Presentation linked.");
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
        int power,
        SkillVisualDefinition presentationTemplate)
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

        // 전용 Boss Visual을 나중에 연결한 경우 migration 재실행이 덮어쓰지 않는다.
        // 현재처럼 PresentationAsset이 비어 있을 때만 검증된 임시 Timeline을 연결한다.
        if (SkillPresentationAccess.Get(definition) == null &&
            presentationTemplate != null)
        {
            SkillPresentationAccess.Set(
                definition,
                presentationTemplate);
        }

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static SkillVisualDefinition LoadVisualTemplate(
        string guid,
        string fallbackPath)
    {
        string guidPath =
            string.IsNullOrWhiteSpace(guid)
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(guid);

        SkillVisualDefinition visual =
            !string.IsNullOrWhiteSpace(guidPath)
                ? AssetDatabase.LoadAssetAtPath<SkillVisualDefinition>(guidPath)
                : null;

        if (visual == null &&
            !string.IsNullOrWhiteSpace(fallbackPath))
        {
            visual =
                AssetDatabase.LoadAssetAtPath<SkillVisualDefinition>(
                    fallbackPath);
        }

        if (visual != null &&
            visual.HasCompleteTimelineSet)
        {
            return visual;
        }

        if (visual != null)
        {
            Debug.LogError(
                $"[Gameplay v5] Presentation template이 불완전합니다: {AssetDatabase.GetAssetPath(visual)} / " +
                string.Join(", ", visual.GetMissingRequirements()));
        }

        return null;
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
