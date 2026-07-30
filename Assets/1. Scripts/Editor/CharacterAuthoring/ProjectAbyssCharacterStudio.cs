#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public enum CharacterStudioBootstrapKind
{
    Olaf = 0,
    EliteEnemy = 1,
    NormalEnemy = 2,
    Custom = 3
}

/// <summary>
/// 리깅 Model Asset에서 Character Prefab을 처음 생성하거나,
/// 기존 Character Prefab을 중심으로 CharacterData/CombatLoadout,
/// Skill, Passive/Item, Presentation과 Cutscene Authoring을 조립한다.
/// </summary>
public sealed class ProjectAbyssCharacterStudio : EditorWindow
{
    private const string MenuPath = "Tools/Project Abyss/Character Studio";
    private const string DefaultBundleFolder = "Assets/2. Data/Characters";

    private CharacterAuthoringBundle bundle;
    private Character importPrefab;

    private int startMode;
    private string bootstrapCharacterName = "NewCharacter";
    private CharacterStudioBootstrapKind bootstrapKind =
        CharacterStudioBootstrapKind.NormalEnemy;
    private GameObject bootstrapModelAsset;
    private MonoScript bootstrapCustomCharacterScript;
    private Avatar bootstrapAvatar;
    private RuntimeAnimatorController bootstrapAnimatorController;
    private string bootstrapOutputFolder = DefaultBundleFolder;
    private bool packReferencedAssetsOnImport = true;
    private bool assemblePrefabOnImport = true;
    private bool ensureStandardComponents = true;
    private bool createStandardHierarchy = true;
    private bool showAdvanced;
    private Vector2 scroll;
    private string assemblySummary;

    private readonly Dictionary<int, Editor> nestedEditors = new();
    private readonly Dictionary<string, bool> folds = new();

    [MenuItem(MenuPath, false, 2010)]
    public static void Open() => Open(null);

    public static void Open(CharacterAuthoringBundle selectedBundle)
    {
        ProjectAbyssCharacterStudio window =
            GetWindow<ProjectAbyssCharacterStudio>("Character Studio");

        window.minSize = new Vector2(660f, 760f);

        if (selectedBundle != null)
            window.bundle = selectedBundle;

        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        UseSelection();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
        DestroyNestedEditors();
    }

    private void OnSelectionChanged()
    {
        UseSelection();
        Repaint();
    }

    private void UseSelection()
    {
        if (Selection.activeObject is CharacterAuthoringBundle selectedBundle)
        {
            bundle = selectedBundle;
            return;
        }

        Character character = ResolveSelectedCharacter();

        if (character == null)
            return;

        CharacterAuthoringLink link =
            character.GetComponent<CharacterAuthoringLink>();

        if (link?.Bundle != null)
            bundle = link.Bundle;

        importPrefab = character;
    }

    private static Character ResolveSelectedCharacter()
    {
        if (Selection.activeObject is Character character)
            return character;

        if (Selection.activeObject is not GameObject gameObject)
            return null;

        return gameObject.GetComponent<Character>() ??
               gameObject.GetComponentInChildren<Character>(true);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Project Abyss Character Studio v6.3 — Create From Model + Assembly",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "리깅된 Model Asset에서 Character Prefab을 처음 생성하거나, 기존 Prefab을 가져와 " +
            "CharacterData, ActionSlot, Skill, Passive/Augment, Animator, Anchor, CameraPoint와 Cutscene Authoring을 조립합니다.",
            MessageType.Info);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawBundleSelector();

        if (bundle == null)
        {
            DrawStartPanel();
            EditorGUILayout.EndScrollView();
            return;
        }

        SyncKind();
        SyncRuntimeReferences(false);

        DrawQuickActions();
        DrawCore();
        DrawSkills();
        DrawPassivesAndItems();
        DrawPrefabAssembly();
        DrawPresentation();
        DrawValidation();
        DrawAdvanced();

        EditorGUILayout.EndScrollView();
    }

    private void DrawBundleSelector()
    {
        EditorGUILayout.BeginHorizontal();

        CharacterAuthoringBundle next =
            (CharacterAuthoringBundle)EditorGUILayout.ObjectField(
                "Character Bundle",
                bundle,
                typeof(CharacterAuthoringBundle),
                false);

        if (next != bundle)
        {
            bundle = next;
            DestroyNestedEditors();
        }

        using (new EditorGUI.DisabledScope(bundle == null))
        {
            if (GUILayout.Button("Ping", GUILayout.Width(54f)))
            {
                Selection.activeObject = bundle;
                EditorGUIUtility.PingObject(bundle);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawStartPanel()
    {
        EditorGUILayout.Space(10f);

        startMode = GUILayout.Toolbar(
            startMode,
            new[]
            {
                "새 캐릭터 만들기",
                "기존 Prefab 가져오기"
            });

        EditorGUILayout.Space(8f);

        if (startMode == 0)
            DrawCreateFromModelPanel();
        else
            DrawImportPrefabPanel();

        EditorGUILayout.Space(12f);

        if (GUILayout.Button("빈 Character Bundle만 만들기", GUILayout.Height(26f)))
            CreateEmptyBundle();
    }

    private void DrawCreateFromModelPanel()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            "리깅 Model → Character Prefab + Bundle",
            EditorStyles.boldLabel);

        bootstrapCharacterName = EditorGUILayout.TextField(
            "Character Name",
            bootstrapCharacterName);

        bootstrapKind = (CharacterStudioBootstrapKind)EditorGUILayout.EnumPopup(
            "Character Kind",
            bootstrapKind);

        if (bootstrapKind == CharacterStudioBootstrapKind.Custom)
        {
            bootstrapCustomCharacterScript =
                (MonoScript)EditorGUILayout.ObjectField(
                    "Custom Character Script",
                    bootstrapCustomCharacterScript,
                    typeof(MonoScript),
                    false);
        }

        bootstrapModelAsset =
            (GameObject)EditorGUILayout.ObjectField(
                "Rigged Model FBX/Prefab",
                bootstrapModelAsset,
                typeof(GameObject),
                false);

        bootstrapAvatar =
            (Avatar)EditorGUILayout.ObjectField(
                "Avatar Override",
                bootstrapAvatar,
                typeof(Avatar),
                false);

        bootstrapAnimatorController =
            (RuntimeAnimatorController)EditorGUILayout.ObjectField(
                "Animator Controller",
                bootstrapAnimatorController,
                typeof(RuntimeAnimatorController),
                false);

        EditorGUILayout.BeginHorizontal();
        bootstrapOutputFolder = EditorGUILayout.TextField(
            "Output Folder",
            bootstrapOutputFolder);

        if (GUILayout.Button("...", GUILayout.Width(32f)))
        {
            string selected = EditorUtility.OpenFolderPanel(
                "Character Output Folder",
                Application.dataPath,
                string.Empty);

            string assetPath = AbsoluteToAssetPath(selected);

            if (!string.IsNullOrWhiteSpace(assetPath))
                bootstrapOutputFolder = assetPath;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Model Asset은 Skeleton/SkinnedMesh/Animator를 가진 FBX 또는 Model Prefab입니다. " +
            "도구가 Character Root, 구체 Character 컴포넌트, Prefab, Bundle, Core Data와 표준 Anchor Hierarchy를 생성합니다.\n" +
            "Custom은 Character와 ICharacterAuthoringTarget을 구현한 비추상 MonoBehaviour Script가 필요합니다.",
            MessageType.Info);

        bool canCreate =
            bootstrapModelAsset != null &&
            !string.IsNullOrWhiteSpace(bootstrapCharacterName) &&
            ResolveBootstrapCharacterType(out _) != null;

        using (new EditorGUI.DisabledScope(!canCreate))
        {
            if (GUILayout.Button(
                    "Create Character From Model",
                    GUILayout.Height(40f)))
            {
                CreateCharacterFromModel();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawImportPrefabPanel()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            "기존 Character Prefab 가져오기",
            EditorStyles.boldLabel);

        importPrefab =
            (Character)EditorGUILayout.ObjectField(
                "Existing Character Prefab",
                importPrefab,
                typeof(Character),
                false);

        packReferencedAssetsOnImport =
            EditorGUILayout.ToggleLeft(
                "참조 중인 Project Abyss SO를 Bundle Sub-Asset으로 복사",
                packReferencedAssetsOnImport);

        assemblePrefabOnImport =
            EditorGUILayout.ToggleLeft(
                "Bundle 생성 후 Prefab 조립/복구",
                assemblePrefabOnImport);

        using (new EditorGUI.DisabledScope(importPrefab == null))
        {
            if (GUILayout.Button(
                    "이 Prefab으로 Character Studio 시작",
                    GUILayout.Height(36f)))
            {
                CreateBundleFromPrefab(importPrefab);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawQuickActions()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("현재 Assembly", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("역할", GetRoleName(bundle.CharacterPrefab));
        EditorGUILayout.ObjectField(
            "Prefab",
            bundle.CharacterPrefab,
            typeof(Character),
            false);
        EditorGUILayout.ObjectField(
            "Modern Loadout",
            bundle.CombatLoadout,
            typeof(CharacterCombatLoadout),
            false);
        EditorGUILayout.ObjectField(
            "Legacy Adapter",
            bundle.SkillSet,
            typeof(ScriptableObject),
            false);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Prefab 참조 다시 읽기"))
            CapturePrefab(bundle.CharacterPrefab, false);

        if (GUILayout.Button("누락 Core 생성/Legacy 이관"))
            EnsureCore();

        if (GUILayout.Button("Loadout ↔ Adapter 동기화"))
            SynchronizeAll(true);

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(bundle.CharacterPrefab == null))
        {
            if (GUILayout.Button("Prefab 조립/복구", GUILayout.Height(30f)))
                AssemblePrefab();
        }

        if (GUILayout.Button("Bundle 내부로 패킹", GUILayout.Height(30f)))
            ConfirmPack();

        if (GUILayout.Button("모두 저장", GUILayout.Height(30f)))
            SynchronizeAll(true);

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawCore()
    {
        if (!BeginSection("core", "1. Character Core", true))
            return;

        SerializedObject so = new(bundle);
        so.Update();
        DrawProperty(so, "displayName");
        DrawProperty(so, "authoringNotes");
        DrawProperty(so, "characterPrefab");
        DrawProperty(so, "characterData");
        DrawProperty(so, "combatLoadout");

        EditorGUILayout.HelpBox(
            "Legacy Adapter는 각 카테고리의 첫 장착 스킬을 기존 Character 클래스의 " +
            "부위 생성/고유 RuntimeSkill 코드에 연결합니다.",
            MessageType.None);

        DrawProperty(so, "skillSet");
        DrawProperty(so, "visualProfile");

        if (so.ApplyModifiedProperties())
        {
            SyncKind();
            SyncRuntimeReferences(false);
            DestroyNestedEditors();
        }

        DrawNested(
            "CharacterData — 스탯 / ActionSlot / Boss Phase",
            bundle.CharacterData,
            true);

        DrawNested(
            "CharacterCombatLoadout Raw",
            bundle.CombatLoadout,
            false);

        EndSection();
    }

    private void DrawSkills()
    {
        if (!BeginSection(
                "skills",
                "2. 스킬 후보 / 최초 장착",
                true))
        {
            return;
        }

        CharacterCombatLoadout loadout = bundle.CombatLoadout;

        if (loadout == null)
        {
            EditorGUILayout.HelpBox(
                "CharacterCombatLoadout이 없습니다.",
                MessageType.Warning);

            if (GUILayout.Button("Modern Combat Loadout 생성"))
                EnsureCore();

            EndSection();
            return;
        }

        EditorGUILayout.HelpBox(
            "후보 목록은 소유/교체 가능한 전체 SkillDefinition이고, " +
            "최초 장착 목록은 전투 시작 시 장착되는 스킬입니다. " +
            "한도는 일반 3 / 결투 2 / 도사림 3 / 위세 1입니다.",
            MessageType.Info);

        DrawSkillCategory(
            loadout,
            ActionType.NormalAttack,
            "일반공격",
            "NormalSkills",
            "NormalSkillPool");

        DrawSkillCategory(
            loadout,
            ActionType.Duel,
            "결투",
            "DuelSkills",
            "DuelSkillPool");

        DrawPreparationCategory(loadout);

        DrawSkillCategory(
            loadout,
            ActionType.Prestige,
            "위세",
            "PrestigeSkills",
            "PrestigeSkillPool");

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("장착/후보 목록 정리"))
        {
            NormalizeLoadout(loadout);
            SynchronizeAll(true);
        }

        if (GUILayout.Button("첫 장착을 Legacy Adapter에 반영"))
        {
            SyncLegacyAdapter(true);
            AssetDatabase.SaveAssets();
        }

        EditorGUILayout.EndHorizontal();

        List<SkillDefinition> definitions =
            loadout.EnumerateAllDefinitions()
                .Where(skill => skill != null)
                .Distinct()
                .OrderBy(skill => skill.ActionType)
                .ThenBy(skill => skill.SkillName)
                .ToList();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            $"스킬 상세 ({definitions.Count})",
            EditorStyles.boldLabel);

        for (int i = 0; i < definitions.Count; i++)
            DrawSkillCard(definitions[i], i);

        EndSection();
    }

    private void DrawSkillCategory(
        CharacterCombatLoadout loadout,
        ActionType actionType,
        string label,
        string equippedProperty,
        string poolProperty)
    {
        EditorGUILayout.BeginVertical("box");
        int limit = CharacterCombatLoadout.GetEquipLimit(actionType);
        int count = loadout.GetEquipped(actionType).Count;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            $"{label} — 최초 장착 {count}/{limit}",
            EditorStyles.boldLabel);

        if (GUILayout.Button("새 Skill 생성", GUILayout.Width(104f)))
            CreateSkill(loadout, actionType);

        EditorGUILayout.EndHorizontal();

        SerializedObject so = new(loadout);
        so.Update();
        DrawProperty(so, equippedProperty, "최초 장착 스킬");
        DrawProperty(so, poolProperty, "캐릭터 스킬 후보");

        if (so.ApplyModifiedProperties())
        {
            NormalizeLoadout(loadout);
            SyncLegacyAdapter(false);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPreparationCategory(CharacterCombatLoadout loadout)
    {
        EditorGUILayout.BeginVertical("box");
        int count = loadout.PreparationSkills?.Count ?? 0;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            $"도사림 — 최초 장착 {count}/{CharacterCombatLoadout.PreparationLimit}",
            EditorStyles.boldLabel);

        if (GUILayout.Button("새 Skill 생성", GUILayout.Width(104f)))
            CreateSkill(loadout, ActionType.Preparation);

        EditorGUILayout.EndHorizontal();

        SerializedObject so = new(loadout);
        so.Update();
        DrawProperty(so, "PreparationSkills", "최초 장착 도사림");
        DrawProperty(so, "CommonPreparationPool", "공용 도사림 후보");
        DrawProperty(so, "CharacterPreparationPool", "캐릭터 전용 도사림 후보");

        if (so.ApplyModifiedProperties())
        {
            NormalizeLoadout(loadout);
            SyncLegacyAdapter(false);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSkillCard(SkillDefinition skill, int index)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();

        string displayName = string.IsNullOrWhiteSpace(skill.SkillName)
            ? skill.name
            : skill.SkillName;

        EditorGUILayout.LabelField(
            $"{index + 1}. [{KoreanName(skill.ActionType)}] {displayName}",
            EditorStyles.boldLabel);

        if (GUILayout.Button("Ping", GUILayout.Width(48f)))
        {
            Selection.activeObject = skill;
            EditorGUIUtility.PingObject(skill);
        }

        if (GUILayout.Button(
                skill.VisualDefinition == null ||
                !skill.VisualDefinition.HasCompleteTimelineSet
                    ? "Timeline 생성"
                    : "Timeline 보수",
                GUILayout.Width(92f)))
        {
            CreateVisual(skill);
        }

        if (GUILayout.Button("공유 Effect 추가", GUILayout.Width(98f)))
        {
            ShowTypeMenu<SkillEffectDefinition>(
                type => AddEffect(skill, type));
        }

        if (GUILayout.Button("Cutscene", GUILayout.Width(78f)))
        {
            ProjectAbyssSkillCutsceneStudio.Open(
                skill,
                bundle);
        }

        EditorGUILayout.EndHorizontal();
        DrawNested(
            "수치 / 독립 굴림 / 공격 가중치 / 비용 / 조건",
            skill,
            true);

        if (skill.EffectEntries != null && skill.EffectEntries.Count > 0)
        {
            for (int i = 0; i < skill.EffectEntries.Count; i++)
            {
                SkillEffectDefinition definition =
                    skill.EffectEntries[i]?.Definition;
                DrawNested($"Effect Template #{i + 1}", definition, false);
            }
        }
        else if (skill.Effects != null)
        {
            for (int i = 0; i < skill.Effects.Count; i++)
                DrawNested($"Legacy Effect #{i + 1}", skill.Effects[i], false);
        }

        DrawNested("Skill Visual", skill.VisualDefinition, false);
        EditorGUILayout.EndVertical();
    }

    private void DrawPassivesAndItems()
    {
        if (!BeginSection(
                "build",
                "3. Passive / Augment / Item",
                true))
        {
            return;
        }

        DrawBuiltInMechanics(bundle.CharacterPrefab);

        SerializedObject so = new(bundle);
        so.Update();
        DrawProperty(so, "overrideLoadout");

        EditorGUILayout.HelpBox(
            "패시브 Script는 CharacterAugment 파생 ScriptableObject 인스턴스로 장착합니다. " +
            "CreateMechanics가 매 전투마다 새 CombatMechanic을 생성합니다.",
            MessageType.Info);

        DrawProperty(
            so,
            "equippedAugments",
            "처음부터 장착할 Passive/Augment");

        DrawProperty(
            so,
            "equippedItems",
            "처음부터 장착할 Item");

        so.ApplyModifiedProperties();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Passive Script Instance 추가"))
        {
            ShowTypeMenu<CharacterAugment>(
                type => AddBundleObject("equippedAugments", type));
        }

        if (GUILayout.Button("Item Script Instance 추가"))
        {
            ShowTypeMenu<CharacterItem>(
                type => AddBundleObject("equippedItems", type));
        }

        EditorGUILayout.EndHorizontal();

        DrawArrayObjects<CharacterAugment>(
            "Passive / Augment 상세",
            "equippedAugments");

        DrawArrayObjects<CharacterItem>(
            "Item 상세",
            "equippedItems");

        EndSection();
    }

    private void DrawPrefabAssembly()
    {
        if (!BeginSection("prefab", "4. Prefab 조립 / 복구", true))
            return;

        ensureStandardComponents =
            EditorGUILayout.ToggleLeft(
                "표준 Runtime/Presentation Component 자동 추가",
                ensureStandardComponents);

        createStandardHierarchy =
            EditorGUILayout.ToggleLeft(
                "Anchors / CameraPoints 표준 Hierarchy 자동 생성",
                createStandardHierarchy);

        List<string> missing =
            bundle.CharacterPrefab != null
                ? CharacterPrefabAssemblyUtility.ValidatePrefab(bundle)
                : new List<string>();

        if (missing.Count == 0 && bundle.CharacterPrefab != null)
        {
            EditorGUILayout.HelpBox(
                "표준 Prefab 구성요소가 확인되었습니다.",
                MessageType.Info);
        }
        else if (missing.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "현재 누락:\n• " + string.Join("\n• ", missing),
                MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(bundle.CharacterPrefab == null))
        {
            if (GUILayout.Button(
                    "현재 Bundle로 Prefab 조립/복구",
                    GUILayout.Height(38f)))
            {
                AssemblePrefab();
            }
        }

        if (!string.IsNullOrWhiteSpace(assemblySummary))
            EditorGUILayout.HelpBox(assemblySummary, MessageType.Info);

        EndSection();
    }

    private void DrawPresentation()
    {
        if (!BeginSection("presentation", "5. Animator / Camera / VFX", true))
            return;

        SerializedObject so = new(bundle);
        so.Update();
        DrawProperty(so, "visualProfile");
        DrawProperty(so, "presentationProfile");
        DrawProperty(so, "animatorController");
        DrawProperty(so, "overrideAnimatorController");
        DrawProperty(so, "avatar");
        DrawProperty(so, "overrideAvatar");
        so.ApplyModifiedProperties();

        DrawNested("SkillVisualProfile", bundle.VisualProfile, false);
        DrawNested(
            "CharacterPresentationProfile",
            bundle.PresentationProfile,
            false);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Battle VFX 추가"))
            CreateSupporting(typeof(BattleVfxDefinition));

        if (GUILayout.Button("Persistent VFX 추가"))
            CreateSupporting(typeof(PersistentBattleVfxDefinition));

        if (GUILayout.Button("Status Visual 추가"))
            CreateSupporting(typeof(StatusEffectVisualDefinition));

        EditorGUILayout.EndHorizontal();
        DrawPresentationAssets();
        EndSection();
    }

    private void DrawValidation()
    {
        if (!BeginSection("validation", "6. Assembly 검증", true))
            return;

        List<ValidationMessage> messages = Validate(bundle);

        if (messages.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Prefab, CharacterData, Modern Loadout, 최초 장착, " +
                "Passive/Item, Legacy Adapter 연결이 유효합니다.",
                MessageType.Info);
        }
        else
        {
            foreach (ValidationMessage message in messages)
                EditorGUILayout.HelpBox(message.Text, message.Type);
        }

        if (GUILayout.Button("전체 동기화 + 저장"))
            SynchronizeAll(true);

        EndSection();
    }

    private void DrawAdvanced()
    {
        showAdvanced = EditorGUILayout.Foldout(
            showAdvanced,
            "Advanced: Bundle 내부 Asset 목록",
            true);

        if (!showAdvanced)
            return;

        SerializedObject so = new(bundle);
        so.Update();
        DrawProperty(so, "includedAssets");
        DrawProperty(so, "supportingAssets");
        so.ApplyModifiedProperties();
    }

    private void AssemblePrefab()
    {
        SynchronizeAll(true);

        CharacterPrefabAssemblyReport report =
            CharacterPrefabAssemblyUtility.Assemble(
                bundle,
                ensureStandardComponents,
                createStandardHierarchy);

        assemblySummary = report.BuildSummary();

        if (report.Messages.Count > 0)
            assemblySummary += "\n\n" + string.Join("\n", report.Messages);

        DestroyNestedEditors();
        Repaint();
    }

    private void EnsureCore()
    {
        if (bundle == null)
            return;

        Character prefab = bundle.CharacterPrefab;
        CharacterAuthoringKind kind = DetectKind(prefab);
        string safeName = SafeName(
            string.IsNullOrWhiteSpace(bundle.DisplayName)
                ? bundle.name
                : bundle.DisplayName);

        CharacterData data = bundle.CharacterData;
        CharacterCombatLoadout loadout = bundle.CombatLoadout;
        ScriptableObject adapter = bundle.SkillSet;
        SkillVisualProfile profile = bundle.VisualProfile;

        if (data == null)
        {
            data = CreateSubAsset<CharacterData>(
                $"{safeName}_CharacterData",
                false);
            data.CharacterName = bundle.DisplayName;
            data.TargetMode = prefab is NormalEnemy
                ? CharacterTargetMode.SingleHP
                : CharacterTargetMode.BodyParts;
            data.SingleHpMax = prefab is NormalEnemy
                ? bundle.NormalEnemySingleMaxHp
                : 1;
        }

        if (loadout == null)
        {
            loadout = CreateSubAsset<CharacterCombatLoadout>(
                $"{safeName}_CombatLoadout",
                false);
        }

        if (adapter == null)
            adapter = CreateLegacyAdapter(kind, safeName);

        if (profile == null)
        {
            profile = CreateSubAsset<SkillVisualProfile>(
                $"{safeName}_VisualProfile",
                false);
        }

        data.CombatLoadout = loadout;
        EnsureDefaultSlots(data, prefab);
        MigrateLegacy(adapter, loadout);

        EnsureOneSkill(loadout, ActionType.NormalAttack, safeName);
        EnsureOneSkill(loadout, ActionType.Duel, safeName);

        if (prefab is not NormalEnemy)
            EnsureOneSkill(loadout, ActionType.Preparation, safeName);

        EnsureOneSkill(loadout, ActionType.Prestige, safeName);
        NormalizeLoadout(loadout);

        bundle.ConfigureCore(
            kind,
            bundle.DisplayName,
            data,
            adapter,
            loadout,
            profile);

        SyncLegacyAdapter(true);
        SyncVisualProfile();
        SynchronizeAll(true);
        DestroyNestedEditors();
    }

    private void CreateSkill(
        CharacterCombatLoadout loadout,
        ActionType actionType)
    {
        string safeName = SafeName(bundle.DisplayName);
        SkillDefinition skill = CreateSubAsset<SkillDefinition>(
            $"{safeName}_{actionType}_{Guid.NewGuid():N}",
            false);

        ConfigureNewSkill(skill, actionType);
        AddToPool(loadout, skill);

        List<SkillDefinition> equipped = EquippedList(loadout, actionType);
        int limit = CharacterCombatLoadout.GetEquipLimit(actionType);

        if (equipped != null && equipped.Count < limit)
            equipped.Add(skill);

        CreateVisual(skill);
        NormalizeLoadout(loadout);
        SyncLegacyAdapter(true);
        AssetDatabase.SaveAssets();
        Selection.activeObject = skill;
    }

    private void EnsureOneSkill(
        CharacterCombatLoadout loadout,
        ActionType actionType,
        string safeName)
    {
        List<SkillDefinition> equipped = EquippedList(loadout, actionType);

        if (equipped == null || equipped.Any(skill => skill != null))
            return;

        SkillDefinition skill = CreateSubAsset<SkillDefinition>(
            $"{safeName}_{actionType}_Skill",
            false);

        ConfigureNewSkill(skill, actionType);
        equipped.Add(skill);
        AddToPool(loadout, skill);
        CreateVisual(skill);
    }

    private void ConfigureNewSkill(SkillDefinition skill, ActionType actionType)
    {
        skill.SkillName = $"{bundle.DisplayName} {KoreanName(actionType)}";
        skill.ActionType = actionType;
        skill.BasePower = 1;
        skill.ExchangeRollCount = 3;
        skill.ResolverType = SkillResolverType.Dice;
        skill.DiceMin = 1;
        skill.DiceMax = 6;
        skill.CanBreakPart = actionType != ActionType.Preparation;
        skill.GainPrestige = actionType != ActionType.Preparation;
        skill.EnsureSkillId();
        EditorUtility.SetDirty(skill);
    }

    private void CreateVisual(SkillDefinition skill)
    {
        if (skill == null)
            return;

        if (skill.VisualDefinition == null)
        {
            string baseName = SafeName(
                string.IsNullOrWhiteSpace(skill.SkillName)
                    ? skill.name
                    : skill.SkillName);

            SkillVisualDefinition visual =
                CreateSubAsset<SkillVisualDefinition>(
                    $"{baseName}_Visual",
                    false);

            ConfigureTimelineVisualDefaults(
                visual,
                skill.ActionType);
            skill.VisualDefinition = visual;
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
        }

        SkillCutsceneAssetBuilder.EnsureForSkill(
            skill,
            bundle?.CharacterPrefab,
            null);

        EditorUtility.SetDirty(skill);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureTimelineVisualDefaults(
        SkillVisualDefinition visual,
        ActionType actionType)
    {
        if (visual == null)
            return;

        bool isPreparation =
            actionType == ActionType.Preparation;

        visual.AllowAsProfileFallback = false;
        visual.HasHitFrameDamage = !isPreparation;
        visual.ExpectedHitFrameCount = 1;
        visual.DistributeDamageByHitCount = !isPreparation;
        visual.PrepareMovement = !isPreparation;
        visual.RestoreMovement = !isPreparation;
        visual.PrepareFacing = !isPreparation;
        visual.RestoreFacing = !isPreparation;

        if (visual.MoveSettings != null)
        {
            visual.MoveSettings.UseMove =
                !isPreparation;
        }

        EditorUtility.SetDirty(
            visual);
    }

    private void AddEffect(SkillDefinition skill, Type type)
    {
        const string templateFolder =
            "Assets/2. Data/BattleEffects/Templates";

        EnsureAssetFolder(templateFolder);

        SkillEffectDefinition effect =
            ScriptableObject.CreateInstance(type)
                as SkillEffectDefinition;

        if (effect == null)
            return;

        string baseName =
            ObjectNames.NicifyVariableName(type.Name);
        string assetPath =
            AssetDatabase.GenerateUniqueAssetPath(
                $"{templateFolder}/{baseName}.asset");

        effect.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(effect, assetPath);

        skill.EffectEntries ??= new List<SkillEffectEntry>();
        skill.EffectEntries.Add(
            new SkillEffectEntry
            {
                Definition = effect,
                Overrides = new SkillEffectOverrides()
            });

        EditorUtility.SetDirty(skill);
        AssetDatabase.SaveAssets();

        Selection.activeObject = effect;
        EditorGUIUtility.PingObject(effect);
    }

    private void AddBundleObject(string propertyName, Type type)
    {
        ScriptableObject created =
            CreateSubAsset(
                type,
                ObjectNames.NicifyVariableName(type.Name),
                true);

        if (created == null)
            return;

        SerializedObject so = new(bundle);
        so.Update();
        SerializedProperty array = so.FindProperty(propertyName);

        if (array == null || !array.isArray)
            return;

        int index = array.arraySize;
        array.InsertArrayElementAtIndex(index);
        array.GetArrayElementAtIndex(index).objectReferenceValue = created;

        SerializedProperty overrideLoadout = so.FindProperty("overrideLoadout");

        if (overrideLoadout != null)
            overrideLoadout.boolValue = true;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bundle);
        AssetDatabase.SaveAssets();
    }

    private void ShowTypeMenu<T>(Action<Type> selected)
        where T : ScriptableObject
    {
        GenericMenu menu = new();
        bool added = false;

        foreach (Type type in TypeCache.GetTypesDerivedFrom<T>()
                     .Where(type =>
                         type != null &&
                         !type.IsAbstract &&
                         !type.IsGenericType &&
                         typeof(ScriptableObject).IsAssignableFrom(type))
                     .OrderBy(type => type.FullName))
        {
            added = true;
            Type captured = type;
            menu.AddItem(
                new GUIContent(type.FullName?.Replace('.', '/') ?? type.Name),
                false,
                () => selected?.Invoke(captured));
        }

        if (!added)
            menu.AddDisabledItem(new GUIContent("사용 가능한 구체 타입 없음"));

        menu.ShowAsContext();
    }

    private void SynchronizeAll(bool save)
    {
        SyncRuntimeReferences(false);
        NormalizeLoadout(bundle?.CombatLoadout);
        SyncLegacyAdapter(true);
        SyncVisualProfile();

        if (save)
        {
            MarkGraphDirty();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private void SyncRuntimeReferences(bool save)
    {
        if (bundle == null)
            return;

        if (bundle.CharacterData != null &&
            bundle.CombatLoadout != null &&
            bundle.CharacterData.CombatLoadout != bundle.CombatLoadout)
        {
            bundle.CharacterData.CombatLoadout = bundle.CombatLoadout;
            EditorUtility.SetDirty(bundle.CharacterData);
        }

        bundle.SynchronizeCharacterDataLoadout();
        EditorUtility.SetDirty(bundle);

        if (save)
            AssetDatabase.SaveAssets();
    }

    private void SyncLegacyAdapter(bool createIfMissing)
    {
        CharacterCombatLoadout loadout = bundle?.CombatLoadout;

        if (loadout == null)
            return;

        ScriptableObject adapter = bundle.SkillSet;

        if (adapter == null && createIfMissing)
        {
            adapter = CreateLegacyAdapter(bundle.Kind, SafeName(bundle.DisplayName));
            bundle.ConfigureLegacySkillSet(adapter);
        }

        if (adapter == null)
            return;

        SetLegacy(adapter, "NormalAttack", First(loadout, ActionType.NormalAttack));
        SetLegacy(adapter, "DuelSkill", First(loadout, ActionType.Duel));
        SetLegacy(adapter, "PreparationSkill", First(loadout, ActionType.Preparation));
        SetLegacy(adapter, "PrestigeSkill", First(loadout, ActionType.Prestige));
        EditorUtility.SetDirty(adapter);
    }

    private ScriptableObject CreateLegacyAdapter(
        CharacterAuthoringKind kind,
        string safeName)
    {
        return kind switch
        {
            CharacterAuthoringKind.Olaf =>
                CreateSubAsset<OlafSkillSet>(
                    $"{safeName}_LegacySkillAdapter",
                    false),
            CharacterAuthoringKind.EliteEnemy =>
                CreateSubAsset<EliteEnemySkillSet>(
                    $"{safeName}_LegacySkillAdapter",
                    false),
            CharacterAuthoringKind.NormalEnemy =>
                CreateSubAsset<NormalEnemySkillSet>(
                    $"{safeName}_LegacySkillAdapter",
                    false),
            _ => null
        };
    }

    private static void SetLegacy(
        ScriptableObject adapter,
        string propertyName,
        SkillDefinition skill)
    {
        if (adapter == null)
            return;

        SerializedObject so = new(adapter);
        so.Update();
        SerializedProperty property = so.FindProperty(propertyName);

        if (property != null &&
            property.propertyType == SerializedPropertyType.ObjectReference)
        {
            property.objectReferenceValue = skill;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void MigrateLegacy(
        ScriptableObject adapter,
        CharacterCombatLoadout loadout)
    {
        if (adapter == null || loadout == null)
            return;

        SerializedObject so = new(adapter);
        SerializedProperty iterator = so.GetIterator();
        bool enterChildren = true;

        while (iterator.Next(enterChildren))
        {
            enterChildren = true;

            if (iterator.propertyType != SerializedPropertyType.ObjectReference ||
                iterator.objectReferenceValue is not SkillDefinition skill)
            {
                continue;
            }

            AddToPool(loadout, skill);
            List<SkillDefinition> equipped = EquippedList(loadout, skill.ActionType);
            int limit = CharacterCombatLoadout.GetEquipLimit(skill.ActionType);

            if (equipped != null &&
                equipped.Count < limit &&
                !equipped.Contains(skill))
            {
                equipped.Add(skill);
            }
        }

        NormalizeLoadout(loadout);
    }

    private void SyncVisualProfile()
    {
        // Timeline-only 정책에서는 SkillDefinition.VisualDefinition이 유일한 런타임 원본이다.
        // 기존 SkillVisualProfile은 이전 데이터 확인용으로만 보존하며 새 스킬을 연결하지 않는다.
    }

    private static void AssignProfileVisual(
        SkillVisualProfile profile,
        SkillDefinition skill,
        bool overwrite)
    {
        if (profile == null || skill?.VisualDefinition == null)
            return;

        switch (skill.ActionType)
        {
            case ActionType.NormalAttack:
                if (overwrite || profile.NormalAttackVisual == null)
                    profile.NormalAttackVisual = skill.VisualDefinition;
                break;
            case ActionType.Duel:
                if (overwrite || profile.DuelVisual == null)
                    profile.DuelVisual = skill.VisualDefinition;
                break;
            case ActionType.Preparation:
                if (overwrite || profile.PreparationVisual == null)
                    profile.PreparationVisual = skill.VisualDefinition;
                break;
            case ActionType.Prestige:
                if (overwrite || profile.PrestigeVisual == null)
                    profile.PrestigeVisual = skill.VisualDefinition;
                break;
        }

        EditorUtility.SetDirty(profile);
    }

    private static void NormalizeLoadout(CharacterCombatLoadout loadout)
    {
        if (loadout == null)
            return;

        Normalize(loadout.NormalSkills, ActionType.NormalAttack, CharacterCombatLoadout.NormalLimit);
        Normalize(loadout.DuelSkills, ActionType.Duel, CharacterCombatLoadout.DuelLimit);
        Normalize(loadout.PreparationSkills, ActionType.Preparation, CharacterCombatLoadout.PreparationLimit);
        Normalize(loadout.PrestigeSkills, ActionType.Prestige, CharacterCombatLoadout.PrestigeLimit);
        NormalizePool(loadout.NormalSkillPool, ActionType.NormalAttack);
        NormalizePool(loadout.DuelSkillPool, ActionType.Duel);
        NormalizePool(loadout.CommonPreparationPool, ActionType.Preparation);
        NormalizePool(loadout.CharacterPreparationPool, ActionType.Preparation);
        NormalizePool(loadout.PrestigeSkillPool, ActionType.Prestige);
        EnsurePool(loadout.NormalSkills, loadout.NormalSkillPool);
        EnsurePool(loadout.DuelSkills, loadout.DuelSkillPool);
        EnsurePool(loadout.PreparationSkills, loadout.CharacterPreparationPool);
        EnsurePool(loadout.PrestigeSkills, loadout.PrestigeSkillPool);
        EditorUtility.SetDirty(loadout);
    }

    private static void Normalize(
        List<SkillDefinition> values,
        ActionType type,
        int limit)
    {
        if (values == null)
            return;

        HashSet<SkillDefinition> unique = new();

        for (int i = values.Count - 1; i >= 0; i--)
        {
            SkillDefinition value = values[i];

            if (value == null || value.ActionType != type || !unique.Add(value))
                values.RemoveAt(i);
        }

        while (values.Count > limit)
            values.RemoveAt(values.Count - 1);
    }

    private static void NormalizePool(List<SkillDefinition> values, ActionType type)
    {
        if (values == null)
            return;

        HashSet<SkillDefinition> unique = new();

        for (int i = values.Count - 1; i >= 0; i--)
        {
            SkillDefinition value = values[i];

            if (value == null || value.ActionType != type || !unique.Add(value))
                values.RemoveAt(i);
        }
    }

    private static void EnsurePool(
        IReadOnlyList<SkillDefinition> equipped,
        ICollection<SkillDefinition> pool)
    {
        if (equipped == null || pool == null)
            return;

        foreach (SkillDefinition skill in equipped)
        {
            if (skill != null && !pool.Contains(skill))
                pool.Add(skill);
        }
    }

    private static List<SkillDefinition> EquippedList(
        CharacterCombatLoadout loadout,
        ActionType type)
    {
        return type switch
        {
            ActionType.NormalAttack => loadout.NormalSkills,
            ActionType.Duel => loadout.DuelSkills,
            ActionType.Preparation => loadout.PreparationSkills,
            ActionType.Prestige => loadout.PrestigeSkills,
            _ => null
        };
    }

    private static SkillDefinition First(
        CharacterCombatLoadout loadout,
        ActionType type)
    {
        return EquippedList(loadout, type)?.FirstOrDefault(skill => skill != null);
    }

    private static void AddToPool(
        CharacterCombatLoadout loadout,
        SkillDefinition skill)
    {
        List<SkillDefinition> pool = skill.ActionType switch
        {
            ActionType.NormalAttack => loadout.NormalSkillPool,
            ActionType.Duel => loadout.DuelSkillPool,
            ActionType.Preparation => loadout.CharacterPreparationPool,
            ActionType.Prestige => loadout.PrestigeSkillPool,
            _ => null
        };

        if (pool != null && !pool.Contains(skill))
            pool.Add(skill);
    }

    private static void EnsureDefaultSlots(CharacterData data, Character prefab)
    {
        data.ActionSlots ??= new List<CharacterSlotConfig>();

        if (data.ActionSlots.Count > 0)
            return;

        if (prefab is NormalEnemy)
        {
            data.ActionSlots.Add(Slot(
                "CHARACTER_SLOT_01",
                "행동 슬롯",
                false,
                PartType.HEAD,
                ActionType.NormalAttack,
                ActionType.Duel,
                ActionType.Prestige));
            return;
        }

        data.ActionSlots.Add(Slot(
            "HEAD_SLOT_01", "머리", true, PartType.HEAD,
            ActionType.NormalAttack, ActionType.Duel,
            ActionType.Preparation, ActionType.Prestige));

        data.ActionSlots.Add(Slot(
            "LEFT_HAND_SLOT_01", "왼손", true, PartType.LEFT_HAND,
            ActionType.NormalAttack, ActionType.Duel, ActionType.Prestige));

        data.ActionSlots.Add(Slot(
            "RIGHT_HAND_SLOT_01", "오른손", true, PartType.RIGHT_HAND,
            ActionType.NormalAttack, ActionType.Duel, ActionType.Prestige));

        data.ActionSlots.Add(Slot(
            "LEGS_SLOT_01", "다리", true, PartType.LEGS,
            ActionType.Preparation, ActionType.Prestige));
    }

    private static CharacterSlotConfig Slot(
        string id,
        string label,
        bool hasPart,
        PartType part,
        params ActionType[] allowed)
    {
        return new CharacterSlotConfig
        {
            SlotId = id,
            DisplayName = label,
            Enabled = true,
            HasLinkedPart = hasPart,
            LinkedPartType = part,
            OverrideSpeedRange = false,
            AllowedActionTypes = allowed.Distinct().ToList()
        };
    }

    private void CreateCharacterFromModel()
    {
        Type characterType = ResolveBootstrapCharacterType(out string typeError);

        if (characterType == null)
        {
            EditorUtility.DisplayDialog("Character Studio", typeError, "확인");
            return;
        }

        if (!TryNormalizeAssetFolder(bootstrapOutputFolder, out string baseFolder))
        {
            EditorUtility.DisplayDialog(
                "Character Studio",
                "Output Folder는 Assets 내부 경로여야 합니다.",
                "확인");
            return;
        }

        string safeName = SafeName(bootstrapCharacterName);
        string characterFolder = $"{baseFolder}/{safeName}";
        string prefabFolder = $"{characterFolder}/Prefabs";
        EnsureAssetFolder(prefabFolder);

        GameObject root = new GameObject(safeName + "_Root");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        try
        {
            Character character = root.AddComponent(characterType) as Character;

            if (character == null)
                throw new InvalidOperationException("Character 컴포넌트 생성에 실패했습니다.");

            GameObject model = InstantiateModelAsset(bootstrapModelAsset);

            if (model == null)
                throw new InvalidOperationException("Model Asset 인스턴스 생성에 실패했습니다.");

            model.name = safeName + "_Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            Animator animator = model.GetComponentInChildren<Animator>(true);

            if (animator == null)
                animator = model.AddComponent<Animator>();

            if (bootstrapAvatar != null)
                animator.avatar = bootstrapAvatar;

            if (bootstrapAnimatorController != null)
                animator.runtimeAnimatorController = bootstrapAnimatorController;

            string prefabPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{prefabFolder}/{safeName}.prefab");

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            if (saved == null)
                throw new InvalidOperationException("Prefab 저장에 실패했습니다.");

            Character prefabCharacter = saved.GetComponent<Character>() ??
                                        saved.GetComponentInChildren<Character>(true);

            if (prefabCharacter == null)
                throw new InvalidOperationException("저장된 Prefab에서 Character를 찾지 못했습니다.");

            string bundlePath = AssetDatabase.GenerateUniqueAssetPath(
                $"{characterFolder}/{safeName}_CharacterBundle.asset");

            CreateBundleFromPrefabAtPath(
                prefabCharacter,
                bundlePath,
                pack: false,
                assemble: true);

            importPrefab = prefabCharacter;
            assemblySummary =
                $"새 캐릭터 생성 완료\nPrefab: {prefabPath}\nBundle: {bundlePath}";

            EditorUtility.DisplayDialog(
                "Character Studio",
                assemblySummary,
                "확인");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Character Studio",
                "새 캐릭터 생성 실패\n" + exception.Message,
                "확인");
        }
        finally
        {
            if (root != null)
                DestroyImmediate(root);
        }
    }

    private Type ResolveBootstrapCharacterType(out string error)
    {
        error = string.Empty;

        Type type = bootstrapKind switch
        {
            CharacterStudioBootstrapKind.Olaf => typeof(Olaf),
            CharacterStudioBootstrapKind.EliteEnemy => typeof(EliteEnemy),
            CharacterStudioBootstrapKind.NormalEnemy => typeof(NormalEnemy),
            _ => bootstrapCustomCharacterScript != null
                ? bootstrapCustomCharacterScript.GetClass()
                : null
        };

        if (type == null)
        {
            error = "사용할 Character Script를 지정하세요.";
            return null;
        }

        if (!typeof(Character).IsAssignableFrom(type) ||
            !typeof(MonoBehaviour).IsAssignableFrom(type) ||
            type.IsAbstract)
        {
            error = "선택한 Script는 비추상 Character MonoBehaviour여야 합니다.";
            return null;
        }

        if (!typeof(ICharacterAuthoringTarget).IsAssignableFrom(type))
        {
            error = "선택한 Character Script는 ICharacterAuthoringTarget을 구현해야 합니다.";
            return null;
        }

        return type;
    }

    private static GameObject InstantiateModelAsset(GameObject modelAsset)
    {
        if (modelAsset == null)
            return null;

        GameObject instance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;

        if (instance == null)
            instance = Instantiate(modelAsset);

        return instance;
    }

    private static bool TryNormalizeAssetFolder(
        string value,
        out string assetFolder)
    {
        assetFolder = string.IsNullOrWhiteSpace(value)
            ? DefaultBundleFolder
            : value.Trim().Replace('\\', '/').TrimEnd('/');

        return assetFolder == "Assets" ||
               assetFolder.StartsWith("Assets/", StringComparison.Ordinal);
    }

    private static string AbsoluteToAssetPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return null;

        string normalized = absolutePath.Replace('\\', '/').TrimEnd('/');
        string assets = Application.dataPath.Replace('\\', '/').TrimEnd('/');

        if (!normalized.StartsWith(assets, StringComparison.OrdinalIgnoreCase))
            return null;

        return "Assets" + normalized.Substring(assets.Length);
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        if (!TryNormalizeAssetFolder(assetFolder, out string normalized))
            throw new ArgumentException("Assets 내부 폴더가 아닙니다.", nameof(assetFolder));

        string[] parts = normalized.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private void CreateEmptyBundle()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Character Bundle",
            "NewCharacterBundle",
            "asset",
            "Character Assembly Bundle 위치를 선택하세요.",
            DefaultBundleFolder);

        if (string.IsNullOrWhiteSpace(path))
            return;

        CharacterAuthoringBundle created =
            ScriptableObject.CreateInstance<CharacterAuthoringBundle>();

        created.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(created, path);
        AssetDatabase.SaveAssets();
        bundle = created;
        Selection.activeObject = created;
    }

    private void CreateBundleFromPrefab(Character prefab)
    {
        if (!IsPrefab(prefab, out string prefabPath))
        {
            EditorUtility.DisplayDialog(
                "Character Studio",
                "Project 창의 Character Prefab Asset을 선택해야 합니다.",
                "확인");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Create Character Assembly Bundle",
            $"{prefab.name}_CharacterBundle",
            "asset",
            "Bundle 저장 위치를 선택하세요.",
            SuggestedFolder(prefabPath));

        if (string.IsNullOrWhiteSpace(path))
            return;

        CreateBundleFromPrefabAtPath(
            prefab,
            path,
            packReferencedAssetsOnImport,
            assemblePrefabOnImport);
    }

    private void CreateBundleFromPrefabAtPath(
        Character prefab,
        string path,
        bool pack,
        bool assemble)
    {
        if (prefab == null || string.IsNullOrWhiteSpace(path))
            return;

        CharacterAuthoringBundle created =
            ScriptableObject.CreateInstance<CharacterAuthoringBundle>();

        created.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(created, path);
        bundle = created;
        CapturePrefab(prefab, pack);
        EnsureCore();

        if (assemble)
            AssemblePrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = bundle;
    }

    private void CapturePrefab(Character prefab, bool pack)
    {
        if (bundle == null || prefab == null || !IsPrefab(prefab, out _))
            return;

        CharacterData data = prefab.Data;
        CharacterCombatLoadout loadout = data?.CombatLoadout;
        ScriptableObject adapter =
            ReadReference<ScriptableObject>(prefab, "skillSet");
        List<CharacterItem> items =
            ReadList<CharacterItem>(prefab, "equippedItems");
        List<CharacterAugment> passives =
            ReadList<CharacterAugment>(prefab, "equippedAugments");

        if (pack)
        {
            CharacterAuthoringCloneUtility clone =
                new CharacterAuthoringCloneUtility(bundle);

            clone.CapturePrefab(prefab.gameObject);
            if (data != null) clone.CloneRoot(data);
            if (loadout != null) clone.CloneRoot(loadout);
            if (adapter != null) clone.CloneRoot(adapter);
            foreach (CharacterItem item in items) clone.CloneRoot(item);
            foreach (CharacterAugment passive in passives) clone.CloneRoot(passive);
            clone.FinalizeClones();

            data = clone.GetClone(data) ?? data;
            loadout = clone.GetClone(loadout) ?? loadout;
            adapter = clone.GetClone(adapter) ?? adapter;
            items = items.Select(item => clone.GetClone(item) ?? item).Distinct().ToList();
            passives = passives.Select(value => clone.GetClone(value) ?? value).Distinct().ToList();
        }

        SkillVisualProfile profile = bundle.VisualProfile ??
            CreateSubAsset<SkillVisualProfile>(
                $"{SafeName(prefab.name)}_VisualProfile",
                false);

        string displayName =
            !string.IsNullOrWhiteSpace(data?.CharacterName)
                ? data.CharacterName
                : prefab.name;

        bundle.ConfigureCore(
            DetectKind(prefab),
            displayName,
            data,
            adapter,
            loadout,
            profile);

        bundle.ConfigurePrefab(prefab);
        bundle.ConfigureLoadout(items, passives, true);
        ConfigurePresentationFromPrefab(prefab);

        if (loadout != null)
            MigrateLegacy(adapter, loadout);

        SynchronizeAll(true);
        DestroyNestedEditors();
    }

    private void ConfirmPack()
    {
        if (bundle?.CharacterPrefab == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog(
            "Bundle 패킹",
            "현재 Prefab이 참조하는 CharacterData, CombatLoadout, Legacy Adapter, " +
            "Skill, Effect, Visual, Passive, Item, VFX SO 그래프를 Bundle 내부 " +
            "Sub-Asset 복사본으로 만듭니다.\n\n원본 SO는 삭제되지 않습니다.",
            "패킹",
            "취소");

        if (confirmed)
            CapturePrefab(bundle.CharacterPrefab, true);
    }

    private void ConfigurePresentationFromPrefab(Character prefab)
    {
        Animator animator = prefab?.GetComponentInChildren<Animator>(true);
        SerializedObject so = new(bundle);
        so.Update();
        SetObject(so, "animatorController", animator?.runtimeAnimatorController);
        SetObject(so, "avatar", animator?.avatar);
        SetBool(so, "overrideAnimatorController", animator?.runtimeAnimatorController != null);
        SetBool(so, "overrideAvatar", animator?.avatar != null);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<ValidationMessage> Validate(CharacterAuthoringBundle value)
    {
        List<ValidationMessage> result = new();

        if (value.CharacterPrefab == null)
            result.Add(Error("Character Prefab이 없습니다."));

        if (value.CharacterData == null)
            result.Add(Error("CharacterData가 없습니다."));

        CharacterCombatLoadout loadout = value.CombatLoadout;

        if (loadout == null)
        {
            result.Add(Error("Modern CharacterCombatLoadout이 없습니다."));
        }
        else
        {
            ValidateCategory(result, loadout.NormalSkills, loadout.NormalSkillPool,
                ActionType.NormalAttack, CharacterCombatLoadout.NormalLimit, "일반공격");
            ValidateCategory(result, loadout.DuelSkills, loadout.DuelSkillPool,
                ActionType.Duel, CharacterCombatLoadout.DuelLimit, "결투");
            ValidateCategory(
                result,
                loadout.PreparationSkills,
                loadout.CommonPreparationPool.Concat(loadout.CharacterPreparationPool)
                    .Where(skill => skill != null).Distinct().ToList(),
                ActionType.Preparation,
                CharacterCombatLoadout.PreparationLimit,
                "도사림");
            ValidateCategory(result, loadout.PrestigeSkills, loadout.PrestigeSkillPool,
                ActionType.Prestige, CharacterCombatLoadout.PrestigeLimit, "위세");

            foreach (SkillDefinition skill in loadout.EnumerateAllDefinitions())
            {
                if (skill == null)
                    continue;

                if (skill.VisualDefinition == null)
                {
                    result.Add(Error($"{skill.name}: SkillVisualDefinition이 없습니다."));
                    continue;
                }

                if (!skill.VisualDefinition.HasTimelineCutscene)
                {
                    SkillVisualDefinition presentation =
                        skill.VisualDefinition;

                    string missing = string.Join(
                        ", ",
                        presentation.GetMissingRequirements());

                    result.Add(Error(
                        $"{skill.name}: Timeline-only 필수 구성 누락 ({missing})"));
                }
            }
        }

        if (value.CharacterData != null &&
            loadout != null &&
            value.CharacterData.CombatLoadout != loadout)
        {
            result.Add(Error("CharacterData.CombatLoadout과 Bundle Loadout이 다릅니다."));
        }

        if (value.Kind != CharacterAuthoringKind.Custom && value.SkillSet == null)
            result.Add(Error("Legacy Runtime Adapter가 없습니다."));

        if (value.CharacterPrefab != null)
        {
            foreach (string missing in CharacterPrefabAssemblyUtility.ValidatePrefab(value))
                result.Add(Warn(missing));
        }

        return result;
    }

    private static void ValidateCategory(
        ICollection<ValidationMessage> result,
        IReadOnlyList<SkillDefinition> equipped,
        IReadOnlyCollection<SkillDefinition> pool,
        ActionType type,
        int limit,
        string label)
    {
        if ((equipped?.Count ?? 0) > limit)
            result.Add(Error($"{label} 장착 한도 {limit}개를 초과합니다."));

        if (equipped == null)
            return;

        HashSet<SkillDefinition> unique = new();

        foreach (SkillDefinition skill in equipped)
        {
            if (skill == null)
            {
                result.Add(Warn($"{label} 장착 목록에 NULL이 있습니다."));
                continue;
            }

            if (!unique.Add(skill))
                result.Add(Warn($"{label}: {skill.name} 중복"));

            if (skill.ActionType != type)
                result.Add(Error($"{skill.name}: ActionType={skill.ActionType}, Expected={type}"));

            if (pool != null && pool.Count > 0 && !pool.Contains(skill))
                result.Add(Error($"{skill.name}: 장착되어 있지만 {label} 후보 목록에 없습니다."));
        }
    }

    private void DrawBuiltInMechanics(Character prefab)
    {
        string[] names = prefab switch
        {
            Olaf => new[] { "OlafMadnessMechanic", "OlafImmortalFuryMechanic" },
            EliteEnemy => new[] { "EliteEnemyMechanic", "EnemyPostureMechanic" },
            NormalEnemy => new[] { "NormalEnemyBloodScentMechanic" },
            _ => Array.Empty<string>()
        };

        EditorGUILayout.LabelField("Character Class 내장 Mechanic", EditorStyles.boldLabel);

        if (names.Length == 0)
            EditorGUILayout.LabelField("없음 — 아래 Passive/Augment로 구성");
        else
            foreach (string name in names) EditorGUILayout.LabelField("• " + name);
    }

    private void DrawPresentationAssets()
    {
        foreach (UnityEngine.Object asset in bundle.SupportingAssets)
        {
            if (asset is BattleVfxDefinition ||
                asset is PersistentBattleVfxDefinition ||
                asset is StatusEffectVisualDefinition)
            {
                DrawNested($"{asset.GetType().Name}: {asset.name}", asset, false);
            }
        }
    }

    private void CreateSupporting(Type type)
    {
        CreateSubAsset(
            type,
            ObjectNames.NicifyVariableName(type.Name),
            true);
        AssetDatabase.SaveAssets();
    }

    private void MarkGraphDirty()
    {
        EditorUtility.SetDirty(bundle);
        if (bundle.CharacterData != null) EditorUtility.SetDirty(bundle.CharacterData);
        if (bundle.CombatLoadout != null) EditorUtility.SetDirty(bundle.CombatLoadout);
        if (bundle.SkillSet != null) EditorUtility.SetDirty(bundle.SkillSet);
        if (bundle.VisualProfile != null) EditorUtility.SetDirty(bundle.VisualProfile);
        if (bundle.PresentationProfile != null)
            EditorUtility.SetDirty(bundle.PresentationProfile);

        foreach (SkillDefinition skill in bundle.EnumerateSkillDefinitions())
            if (skill != null) EditorUtility.SetDirty(skill);
    }

    private bool BeginSection(string key, string title, bool defaultValue)
    {
        bool expanded = folds.TryGetValue(key, out bool stored)
            ? stored
            : defaultValue;

        expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, title);
        folds[key] = expanded;

        if (!expanded)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(3f);
            return false;
        }

        EditorGUILayout.BeginVertical("box");
        return true;
    }

    private static void EndSection()
    {
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private static void DrawProperty(
        SerializedObject so,
        string name,
        string label = null)
    {
        SerializedProperty property = so?.FindProperty(name);

        if (property != null)
        {
            if (string.IsNullOrWhiteSpace(label))
                EditorGUILayout.PropertyField(property, true);
            else
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }
    }

    private void DrawNested(
        string title,
        UnityEngine.Object asset,
        bool defaultValue)
    {
        if (asset == null || asset == bundle)
            return;

        int id = asset.GetInstanceID();
        string key = $"nested:{id}:{title}";
        bool expanded = folds.TryGetValue(key, out bool stored)
            ? stored
            : defaultValue;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        expanded = EditorGUILayout.Foldout(expanded, title, true);

        if (GUILayout.Button("Ping", GUILayout.Width(48f)))
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        EditorGUILayout.EndHorizontal();
        folds[key] = expanded;

        if (expanded)
        {
            if (!nestedEditors.TryGetValue(id, out Editor editor) ||
                editor == null ||
                editor.target != asset)
            {
                if (editor != null) DestroyImmediate(editor);
                editor = Editor.CreateEditor(asset);
                nestedEditors[id] = editor;
            }

            EditorGUI.indentLevel++;
            editor.OnInspectorGUI();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawArrayObjects<T>(string title, string propertyName)
        where T : UnityEngine.Object
    {
        SerializedObject so = new(bundle);
        SerializedProperty property = so.FindProperty(propertyName);

        if (property == null || !property.isArray)
            return;

        for (int i = 0; i < property.arraySize; i++)
        {
            T value = property.GetArrayElementAtIndex(i).objectReferenceValue as T;
            DrawNested($"{title} #{i + 1}", value, false);
        }
    }

    private T CreateSubAsset<T>(string name, bool supporting)
        where T : ScriptableObject
    {
        return CreateSubAsset(typeof(T), name, supporting) as T;
    }

    private ScriptableObject CreateSubAsset(Type type, string name, bool supporting)
    {
        if (bundle == null ||
            type == null ||
            type.IsAbstract ||
            !typeof(ScriptableObject).IsAssignableFrom(type))
        {
            return null;
        }

        ScriptableObject asset = ScriptableObject.CreateInstance(type);
        asset.name = string.IsNullOrWhiteSpace(name)
            ? ObjectNames.NicifyVariableName(type.Name)
            : name;

        AssetDatabase.AddObjectToAsset(asset, bundle);

        if (supporting) bundle.RegisterSupportingAsset(asset);
        else bundle.RegisterIncludedAsset(asset);

        EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(bundle);
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(bundle));
        return asset;
    }

    private static T ReadReference<T>(UnityEngine.Object source, string name)
        where T : UnityEngine.Object
    {
        return source == null
            ? null
            : new SerializedObject(source).FindProperty(name)?.objectReferenceValue as T;
    }

    private static List<T> ReadList<T>(UnityEngine.Object source, string name)
        where T : UnityEngine.Object
    {
        List<T> result = new();
        if (source == null) return result;

        SerializedProperty property = new SerializedObject(source).FindProperty(name);
        if (property == null || !property.isArray) return result;

        for (int i = 0; i < property.arraySize; i++)
        {
            if (property.GetArrayElementAtIndex(i).objectReferenceValue is T value)
                result.Add(value);
        }

        return result;
    }

    private static void SetObject(
        SerializedObject so,
        string name,
        UnityEngine.Object value)
    {
        SerializedProperty property = so?.FindProperty(name);
        if (property != null) property.objectReferenceValue = value;
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty property = so?.FindProperty(name);
        if (property != null) property.boolValue = value;
    }

    private void SyncKind()
    {
        if (bundle?.CharacterPrefab == null)
            return;

        CharacterAuthoringKind detected = DetectKind(bundle.CharacterPrefab);

        if (bundle.Kind == detected)
            return;

        SerializedObject so = new(bundle);
        SerializedProperty kind = so.FindProperty("kind");

        if (kind != null)
        {
            kind.enumValueIndex = (int)detected;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bundle);
        }
    }

    private static CharacterAuthoringKind DetectKind(Character character) =>
        character switch
        {
            Olaf => CharacterAuthoringKind.Olaf,
            EliteEnemy => CharacterAuthoringKind.EliteEnemy,
            NormalEnemy => CharacterAuthoringKind.NormalEnemy,
            _ => CharacterAuthoringKind.Custom
        };

    private static string GetRoleName(Character character) =>
        character switch
        {
            Olaf => "플레이어블 — Olaf",
            EliteEnemy => "정예 적 — 부위형",
            NormalEnemy => "일반 적 — 단일 HP",
            Enemy => "Custom Enemy",
            null => "미지정",
            _ => "Playable / Custom"
        };

    private static string KoreanName(ActionType type) =>
        type switch
        {
            ActionType.NormalAttack => "일반공격",
            ActionType.Duel => "결투",
            ActionType.Preparation => "도사림",
            ActionType.Prestige => "위세",
            _ => type.ToString()
        };

    private static string SafeName(string value)
    {
        string safe = string.IsNullOrWhiteSpace(value) ? "Character" : value.Trim();

        foreach (char invalid in Path.GetInvalidFileNameChars())
            safe = safe.Replace(invalid, '_');

        return safe.Replace('/', '_').Replace('\\', '_');
    }

    private static bool IsPrefab(Character character, out string path)
    {
        path = character == null ? null : AssetDatabase.GetAssetPath(character);
        return !string.IsNullOrWhiteSpace(path) &&
               path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
    }

    private static string SuggestedFolder(string prefabPath)
    {
        string folder = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
        return string.IsNullOrWhiteSpace(folder) ? DefaultBundleFolder : folder;
    }

    private void DestroyNestedEditors()
    {
        foreach (Editor editor in nestedEditors.Values)
            if (editor != null) DestroyImmediate(editor);

        nestedEditors.Clear();
    }

    private static ValidationMessage Error(string text) =>
        new(MessageType.Error, text);

    private static ValidationMessage Warn(string text) =>
        new(MessageType.Warning, text);

    private readonly struct ValidationMessage
    {
        public readonly MessageType Type;
        public readonly string Text;

        public ValidationMessage(MessageType type, string text)
        {
            Type = type;
            Text = text;
        }
    }
}
#endif