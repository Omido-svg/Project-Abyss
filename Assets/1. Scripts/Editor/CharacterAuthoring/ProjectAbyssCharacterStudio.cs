#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 캐릭터 프리팹을 자동 생성하는 도구가 아니라,
/// 이미 존재하거나 새로 만든 Character Prefab 한 개를 중심으로
/// 데이터·스킬·패시브·연출을 한 EditorWindow에서 편집하는 단일 작업 공간이다.
/// </summary>
public sealed class ProjectAbyssCharacterStudio : EditorWindow
{
    private const string MenuPath =
        "Tools/Project Abyss/Character Studio";

    private const string DefaultBundleFolder =
        "Assets/2. Data/Characters";

    private CharacterAuthoringBundle bundle;
    private Character importPrefab;
    private bool packReferencedAssetsOnImport = true;
    private bool linkPrefabOnImport = true;
    private bool showAdvancedBundleData;
    private Vector2 scroll;

    private readonly Dictionary<int, Editor>
        nestedEditors = new();

    private readonly Dictionary<string, bool>
        sectionExpanded = new();

    [MenuItem(MenuPath, false, 2010)]
    public static void Open()
    {
        Open(null);
    }

    public static void Open(
        CharacterAuthoringBundle selectedBundle)
    {
        ProjectAbyssCharacterStudio window =
            GetWindow<ProjectAbyssCharacterStudio>(
                "Character Studio");

        window.minSize = new Vector2(560f, 650f);

        if (selectedBundle != null)
            window.bundle = selectedBundle;

        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        Selection.selectionChanged +=
            HandleSelectionChanged;

        TryUseSelection();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -=
            HandleSelectionChanged;

        DestroyNestedEditors();
    }

    private void HandleSelectionChanged()
    {
        TryUseSelection();
        Repaint();
    }

    private void TryUseSelection()
    {
        if (Selection.activeObject is
            CharacterAuthoringBundle selectedBundle)
        {
            bundle = selectedBundle;
            return;
        }

        Character selectedCharacter =
            ResolveCharacterFromSelection();

        if (selectedCharacter == null)
            return;

        CharacterAuthoringLink link =
            selectedCharacter.GetComponent<CharacterAuthoringLink>();

        if (link != null && link.Bundle != null)
        {
            bundle = link.Bundle;
            importPrefab = selectedCharacter;
            return;
        }

        importPrefab = selectedCharacter;
    }

    private static Character ResolveCharacterFromSelection()
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
        DrawStudioHeader();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawBundleSelector();

        if (bundle == null)
        {
            DrawStartPanel();
            EditorGUILayout.EndScrollView();
            return;
        }

        SyncKindFromPrefab(bundle);

        DrawQuickActions();
        DrawIdentityAndCore();
        DrawSkills();
        DrawPassivesAndBuild();
        DrawPresentation();
        DrawRoleSpecificSettings();
        DrawValidation();
        DrawAdvancedData();

        EditorGUILayout.EndScrollView();
    }

    private void DrawStudioHeader()
    {
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField(
            "Project Abyss Character Studio",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "프리팹 종류를 선택해 자동 생성하는 도구가 아닙니다. " +
            "Character Prefab과 Character Bundle을 연결한 뒤, " +
            "스탯·스킬·효과·패시브·Animator·카메라·VFX를 이 창 하나에서 편집합니다.",
            MessageType.Info);
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
        EditorGUILayout.Space(12f);

        EditorGUILayout.LabelField(
            "시작",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "가장 빠른 사용법: Project 창에서 Character Prefab을 선택하면 아래 필드에 자동으로 들어옵니다. " +
            "이미 CharacterAuthoringLink가 연결된 Prefab이라면 해당 Bundle이 바로 열립니다.",
            MessageType.None);

        if (GUILayout.Button(
                "빈 Character Bundle 만들기",
                GUILayout.Height(32f)))
        {
            CreateEmptyBundle();
        }

        EditorGUILayout.Space(12f);

        EditorGUILayout.LabelField(
            "기존 Character Prefab 가져오기",
            EditorStyles.boldLabel);

        importPrefab =
            (Character)EditorGUILayout.ObjectField(
                "Character Prefab",
                importPrefab,
                typeof(Character),
                false);

        packReferencedAssetsOnImport =
            EditorGUILayout.ToggleLeft(
                "참조 중인 Project Abyss SO를 Bundle 내부 Sub-Asset으로 복사",
                packReferencedAssetsOnImport);

        linkPrefabOnImport =
            EditorGUILayout.ToggleLeft(
                "Prefab에 CharacterAuthoringLink를 연결",
                linkPrefabOnImport);

        EditorGUILayout.HelpBox(
            "이 작업은 선택한 Prefab의 실제 Character 타입을 자동 판별합니다. " +
            "플레이어블/일반 적/정예 적을 별도로 선택하지 않으며, " +
            "Prefab 자체를 새 타입으로 생성하지 않습니다.",
            MessageType.None);

        using (new EditorGUI.DisabledScope(importPrefab == null))
        {
            if (GUILayout.Button(
                    "이 Prefab으로 Character Studio 시작",
                    GUILayout.Height(34f)))
            {
                CreateBundleFromPrefab(importPrefab);
            }
        }
    }

    private void DrawQuickActions()
    {
        Character prefab = bundle.CharacterPrefab;

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField(
            "현재 작업 대상",
            EditorStyles.boldLabel);

        EditorGUILayout.LabelField(
            "역할",
            GetRoleDisplayName(prefab));

        EditorGUILayout.LabelField(
            "Runtime Adapter",
            bundle.Kind.ToString());

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Prefab 참조 다시 읽기"))
            CaptureCurrentPrefab(packIntoBundle: false);

        if (GUILayout.Button("현재 참조를 Bundle에 패킹"))
            ConfirmAndPackCurrentPrefab();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Prefab Link 적용/복구"))
            ApplyBundleLinkToCurrentPrefab(null);

        if (GUILayout.Button("누락 Core 에셋 생성"))
            EnsureMissingCoreAssets();

        if (GUILayout.Button("저장"))
        {
            EditorUtility.SetDirty(bundle);
            AssetDatabase.SaveAssets();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawIdentityAndCore()
    {
        if (!BeginSection("identity", "1. 캐릭터 / 전투 스탯", true))
            return;

        SerializedObject serialized =
            new SerializedObject(bundle);

        serialized.Update();

        DrawProperty(serialized, "displayName");
        DrawProperty(serialized, "authoringNotes");
        DrawProperty(serialized, "characterPrefab");
        DrawProperty(serialized, "characterData");
        DrawProperty(serialized, "skillSet");

        bool changed =
            serialized.ApplyModifiedProperties();

        if (changed)
        {
            SyncKindFromPrefab(bundle);
            EditorUtility.SetDirty(bundle);
        }

        DrawNestedObject(
            "CharacterData — HP / 속도 / 공격 / 방어 / 자원",
            bundle.CharacterData,
            true);

        DrawNestedObject(
            "SkillSet — 런타임 스킬 슬롯 연결",
            bundle.SkillSet,
            false);

        EndSection();
    }

    private void DrawSkills()
    {
        if (!BeginSection("skills", "2. 스킬 / 효과 / 연출", true))
            return;

        List<SkillDefinition> skills =
            CollectSkillDefinitions(bundle.SkillSet)
                .OrderBy(skill => skill.ActionType)
                .ThenBy(skill => skill.SkillName)
                .ToList();

        if (skills.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "SkillSet에서 SkillDefinition을 찾지 못했습니다. " +
                "누락 Core 에셋 생성 또는 SkillSet 직접 연결을 사용하세요.",
                MessageType.Warning);
        }

        for (int i = 0; i < skills.Count; i++)
        {
            SkillDefinition skill = skills[i];

            if (skill == null)
                continue;

            DrawSkillCard(skill, i);
        }

        EndSection();
    }

    private void DrawSkillCard(
        SkillDefinition skill,
        int index)
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();

        string title =
            $"{index + 1}. [{skill.ActionType}] " +
            $"{(string.IsNullOrWhiteSpace(skill.SkillName) ? skill.name : skill.SkillName)}";

        EditorGUILayout.LabelField(
            title,
            EditorStyles.boldLabel);

        if (GUILayout.Button("Ping", GUILayout.Width(50f)))
        {
            Selection.activeObject = skill;
            EditorGUIUtility.PingObject(skill);
        }

        if (skill.VisualDefinition == null &&
            GUILayout.Button("Visual 생성", GUILayout.Width(86f)))
        {
            CreateVisualForSkill(skill);
        }

        if (GUILayout.Button("Effect 추가", GUILayout.Width(82f)))
        {
            ShowCreateSubAssetMenu<SkillEffectDefinition>(
                type => AddEffectToSkill(skill, type));
        }

        EditorGUILayout.EndHorizontal();

        DrawNestedObject(
            "스킬 수치 / 굴림 / 비용 / 조건",
            skill,
            true);

        if (skill.Effects != null &&
            skill.Effects.Count > 0)
        {
            EditorGUILayout.LabelField(
                "스킬 효과",
                EditorStyles.boldLabel);

            for (int effectIndex = 0;
                 effectIndex < skill.Effects.Count;
                 effectIndex++)
            {
                SkillEffectDefinition effect =
                    skill.Effects[effectIndex];

                DrawNestedObject(
                    $"Effect #{effectIndex + 1}",
                    effect,
                    false);
            }
        }

        SkillVisualDefinition visual =
            skill.VisualDefinition;

        DrawNestedObject(
            "Skill Visual — 이동 / 타격 / VFX / Animator",
            visual,
            visual != null);

        DrawNestedObject(
            "Skill Camera — Shot 순서 / 복귀",
            visual?.CameraDefinition,
            false);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawPassivesAndBuild()
    {
        if (!BeginSection("passives", "3. 패시브 / 아이템 / 증강", true))
            return;

        DrawBuiltInMechanicSummary(bundle.CharacterPrefab);

        SerializedObject serialized =
            new SerializedObject(bundle);

        serialized.Update();

        SerializedProperty overrideLoadout =
            serialized.FindProperty("overrideLoadout");

        SerializedProperty items =
            serialized.FindProperty("equippedItems");

        SerializedProperty augments =
            serialized.FindProperty("equippedAugments");

        if (overrideLoadout != null)
            EditorGUILayout.PropertyField(overrideLoadout);

        EditorGUILayout.HelpBox(
            "캐릭터 고유 패시브를 데이터 에셋으로 만들 때는 CharacterAugment 파생 SO를 사용하세요. " +
            "CreateMechanic(s)에서 CombatMechanic을 생성하므로 기존 이벤트·스탯·부위 수정 기능을 그대로 사용할 수 있습니다.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("패시브/증강 Sub-Asset 추가"))
        {
            ShowCreateSubAssetMenu<CharacterAugment>(
                type => AddObjectToBundleArray(
                    "equippedAugments",
                    type,
                    registerSupporting: true));
        }

        if (GUILayout.Button("아이템 Sub-Asset 추가"))
        {
            ShowCreateSubAssetMenu<CharacterItem>(
                type => AddObjectToBundleArray(
                    "equippedItems",
                    type,
                    registerSupporting: true));
        }

        EditorGUILayout.EndHorizontal();

        if (items != null)
            EditorGUILayout.PropertyField(items, includeChildren: true);

        if (augments != null)
            EditorGUILayout.PropertyField(augments, includeChildren: true);

        serialized.ApplyModifiedProperties();

        DrawReferencedArrayEditors<CharacterItem>(
            "아이템 상세",
            "equippedItems");

        DrawReferencedArrayEditors<CharacterAugment>(
            "패시브 / 증강 상세",
            "equippedAugments");

        EndSection();
    }

    private void DrawPresentation()
    {
        if (!BeginSection("presentation", "4. 공통 연출 / Animator / VFX", true))
            return;

        SerializedObject serialized =
            new SerializedObject(bundle);

        serialized.Update();

        DrawProperty(serialized, "visualProfile");
        DrawProperty(serialized, "animatorController");
        DrawProperty(serialized, "overrideAnimatorController");
        DrawProperty(serialized, "avatar");
        DrawProperty(serialized, "overrideAvatar");

        serialized.ApplyModifiedProperties();

        DrawNestedObject(
            "기본 SkillVisualProfile",
            bundle.VisualProfile,
            false);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Battle VFX 추가"))
        {
            AddConcreteSupportingAsset(
                typeof(BattleVfxDefinition));
        }

        if (GUILayout.Button("Persistent VFX 추가"))
        {
            AddConcreteSupportingAsset(
                typeof(PersistentBattleVfxDefinition));
        }

        if (GUILayout.Button("Status Visual 추가"))
        {
            AddConcreteSupportingAsset(
                typeof(StatusEffectVisualDefinition));
        }

        EditorGUILayout.EndHorizontal();

        DrawPresentationSupportingAssets();
        DrawPrefabPresentationSummary(bundle.CharacterPrefab);

        EndSection();
    }

    private void DrawRoleSpecificSettings()
    {
        Character prefab = bundle.CharacterPrefab;

        if (prefab is not NormalEnemy &&
            prefab is not EliteEnemy)
        {
            return;
        }

        if (!BeginSection("role", "5. 적 종류별 설정", true))
            return;

        SerializedObject serialized =
            new SerializedObject(bundle);

        serialized.Update();

        if (prefab is NormalEnemy)
        {
            DrawProperty(serialized, "overrideNormalEnemySingleHp");
            DrawProperty(serialized, "normalEnemySingleMaxHp");
        }

        if (prefab is EliteEnemy)
        {
            DrawProperty(serialized, "useElitePostureRotation");
            DrawProperty(serialized, "elitePostureSettings");
        }

        serialized.ApplyModifiedProperties();
        EndSection();
    }

    private void DrawValidation()
    {
        if (!BeginSection("validation", "6. 검증", true))
            return;

        List<ValidationMessage> messages =
            ValidateBundle(bundle);

        int errorCount =
            messages.Count(message =>
                message.Type == MessageType.Error);

        int warningCount =
            messages.Count(message =>
                message.Type == MessageType.Warning);

        if (messages.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "현재 Bundle의 핵심 참조와 스킬/연출 연결이 유효합니다.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField(
                $"오류 {errorCount} / 경고 {warningCount}",
                EditorStyles.boldLabel);

            foreach (ValidationMessage message in messages)
            {
                EditorGUILayout.HelpBox(
                    message.Text,
                    message.Type);
            }
        }

        if (GUILayout.Button("모든 관련 에셋 저장"))
        {
            EditorUtility.SetDirty(bundle);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EndSection();
    }

    private void DrawAdvancedData()
    {
        showAdvancedBundleData =
            EditorGUILayout.Foldout(
                showAdvancedBundleData,
                "Advanced: Bundle 내부 목록",
                true);

        if (!showAdvancedBundleData)
            return;

        SerializedObject serialized =
            new SerializedObject(bundle);

        serialized.Update();
        DrawProperty(serialized, "includedAssets");
        DrawProperty(serialized, "supportingAssets");
        serialized.ApplyModifiedProperties();
    }

    private bool BeginSection(
        string key,
        string title,
        bool defaultExpanded)
    {
        bool expanded =
            sectionExpanded.TryGetValue(
                key,
                out bool stored)
                ? stored
                : defaultExpanded;

        expanded =
            EditorGUILayout.BeginFoldoutHeaderGroup(
                expanded,
                title);

        sectionExpanded[key] = expanded;

        if (!expanded)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4f);
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
        SerializedObject serialized,
        string propertyName)
    {
        SerializedProperty property =
            serialized?.FindProperty(propertyName);

        if (property != null)
            EditorGUILayout.PropertyField(property, true);
    }

    private void DrawNestedObject(
        string title,
        UnityEngine.Object asset,
        bool defaultExpanded)
    {
        if (asset == null || asset == bundle)
            return;

        int id = asset.GetInstanceID();
        string key = $"nested:{id}:{title}";

        bool expanded =
            sectionExpanded.TryGetValue(
                key,
                out bool stored)
                ? stored
                : defaultExpanded;

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();

        expanded =
            EditorGUILayout.Foldout(
                expanded,
                title,
                true);

        if (GUILayout.Button("Ping", GUILayout.Width(48f)))
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        EditorGUILayout.EndHorizontal();

        sectionExpanded[key] = expanded;

        if (expanded)
        {
            if (!nestedEditors.TryGetValue(
                    id,
                    out Editor editor) ||
                editor == null ||
                editor.target != asset)
            {
                if (editor != null)
                    DestroyImmediate(editor);

                editor = Editor.CreateEditor(asset);
                nestedEditors[id] = editor;
            }

            EditorGUI.indentLevel++;
            editor.OnInspectorGUI();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawReferencedArrayEditors<T>(
        string title,
        string propertyName)
        where T : UnityEngine.Object
    {
        SerializedObject serialized =
            new SerializedObject(bundle);

        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null ||
            !property.isArray ||
            property.arraySize == 0)
        {
            return;
        }

        EditorGUILayout.LabelField(
            title,
            EditorStyles.boldLabel);

        for (int i = 0; i < property.arraySize; i++)
        {
            T value =
                property.GetArrayElementAtIndex(i)
                    .objectReferenceValue as T;

            DrawNestedObject(
                $"{title} #{i + 1}",
                value,
                false);
        }
    }

    private void DrawBuiltInMechanicSummary(
        Character prefab)
    {
        string[] mechanics = prefab switch
        {
            Olaf => new[]
            {
                "광전사의 광기 — 교환 출혈, 결투 패배/부위 파괴 광기, 고유 파괴",
                "불사의 분노 — 사망 방지, 추가 행동 슬롯, 턴 종료 자해"
            },
            EliteEnemy => new[]
            {
                "엘리트 본능 — 합 승리/부위 상태 변화 위세 반응",
                "적 자세 로테이션 — 자세별 COMBAT 슬롯과 기세 성향"
            },
            NormalEnemy => new[]
            {
                "피 냄새 — 합 승리 시 출혈 적용"
            },
            _ => Array.Empty<string>()
        };

        EditorGUILayout.LabelField(
            "코드 내장 메커닉",
            EditorStyles.boldLabel);

        if (mechanics.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "이 Prefab 타입에서 자동 등록되는 내장 CombatMechanic이 확인되지 않습니다. " +
                "데이터형 패시브는 아래 CharacterAugment 목록으로 추가할 수 있습니다.",
                MessageType.None);
            return;
        }

        foreach (string mechanic in mechanics)
            EditorGUILayout.LabelField("• " + mechanic, EditorStyles.wordWrappedLabel);
    }

    private void DrawPresentationSupportingAssets()
    {
        IReadOnlyList<UnityEngine.Object> assets =
            bundle.SupportingAssets;

        if (assets == null)
            return;

        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is BattleVfxDefinition ||
                asset is PersistentBattleVfxDefinition ||
                asset is StatusEffectVisualDefinition)
            {
                DrawNestedObject(
                    $"{asset.GetType().Name}: {asset.name}",
                    asset,
                    false);
            }
        }
    }

    private static void DrawPrefabPresentationSummary(
        Character prefab)
    {
        if (prefab == null)
            return;

        Animator animator =
            prefab.GetComponentInChildren<Animator>(true);

        CharacterView view =
            prefab.GetComponent<CharacterView>() ??
            prefab.GetComponentInChildren<CharacterView>(true);

        CharacterCameraPointSet points =
            prefab.GetComponentInChildren<CharacterCameraPointSet>(true);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            "Prefab 연출 구성",
            EditorStyles.boldLabel);

        EditorGUILayout.ObjectField(
            "Character Prefab",
            prefab,
            typeof(Character),
            false);

        EditorGUILayout.ObjectField(
            "Animator",
            animator,
            typeof(Animator),
            true);

        EditorGUILayout.ObjectField(
            "CharacterView",
            view,
            typeof(CharacterView),
            true);

        EditorGUILayout.ObjectField(
            "Camera Point Set",
            points,
            typeof(CharacterCameraPointSet),
            true);
    }

    private void CreateEmptyBundle()
    {
        string path =
            EditorUtility.SaveFilePanelInProject(
                "Create Character Bundle",
                "NewCharacterBundle",
                "asset",
                "Character Studio에서 편집할 Bundle 위치를 선택하세요.",
                DefaultBundleFolder);

        if (string.IsNullOrWhiteSpace(path))
            return;

        CharacterAuthoringBundle created =
            ScriptableObject.CreateInstance<CharacterAuthoringBundle>();

        created.name =
            Path.GetFileNameWithoutExtension(path);

        AssetDatabase.CreateAsset(created, path);
        AssetDatabase.SaveAssets();

        bundle = created;
        Selection.activeObject = created;
        EditorGUIUtility.PingObject(created);
    }

    private void CreateBundleFromPrefab(
        Character prefab)
    {
        if (!IsPrefabAsset(prefab, out string prefabPath))
        {
            EditorUtility.DisplayDialog(
                "Character Studio",
                "Project 창의 Character Prefab Asset을 선택해야 합니다.",
                "확인");
            return;
        }

        string defaultName =
            $"{prefab.name}_CharacterBundle";

        string path =
            EditorUtility.SaveFilePanelInProject(
                "Create Character Bundle From Prefab",
                defaultName,
                "asset",
                "Bundle 저장 위치를 선택하세요.",
                GetSuggestedFolder(prefabPath));

        if (string.IsNullOrWhiteSpace(path))
            return;

        CharacterAuthoringBundle created =
            ScriptableObject.CreateInstance<CharacterAuthoringBundle>();

        created.name =
            Path.GetFileNameWithoutExtension(path);

        AssetDatabase.CreateAsset(created, path);
        bundle = created;

        CapturePrefabIntoBundle(
            prefab,
            packReferencedAssetsOnImport,
            linkPrefabOnImport);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = bundle;
        EditorGUIUtility.PingObject(bundle);
    }

    private void CaptureCurrentPrefab(
        bool packIntoBundle)
    {
        if (bundle?.CharacterPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Character Studio",
                "먼저 Bundle의 Character Prefab을 연결하세요.",
                "확인");
            return;
        }

        CapturePrefabIntoBundle(
            bundle.CharacterPrefab,
            packIntoBundle,
            linkPrefab: true);
    }

    private void ConfirmAndPackCurrentPrefab()
    {
        if (bundle?.CharacterPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Character Studio",
                "먼저 Bundle의 Character Prefab을 연결하세요.",
                "확인");
            return;
        }

        bool confirmed =
            EditorUtility.DisplayDialog(
                "Bundle 패킹",
                "현재 Prefab이 참조하는 Project Abyss ScriptableObject 그래프를 " +
                "Bundle 내부 Sub-Asset 복사본으로 만들고 Prefab 참조를 복사본으로 연결합니다.\n\n" +
                "원본 SO는 삭제되지 않습니다.",
                "패킹",
                "취소");

        if (confirmed)
            CaptureCurrentPrefab(packIntoBundle: true);
    }

    private void CapturePrefabIntoBundle(
        Character prefab,
        bool packIntoBundle,
        bool linkPrefab)
    {
        if (bundle == null || prefab == null)
            return;

        if (!IsPrefabAsset(prefab, out _))
            return;

        CharacterAuthoringCloneUtility cloneUtility = null;

        CharacterData data = prefab.Data;
        ScriptableObject skillSet =
            ReadObjectReference<ScriptableObject>(
                prefab,
                "skillSet");

        if (packIntoBundle)
        {
            cloneUtility =
                new CharacterAuthoringCloneUtility(bundle);

            cloneUtility.CapturePrefab(prefab.gameObject);

            if (data != null)
                cloneUtility.CloneRoot(data);

            if (skillSet != null)
                cloneUtility.CloneRoot(skillSet);

            cloneUtility.FinalizeClones();

            data =
                cloneUtility.GetClone(data) ?? data;

            skillSet =
                cloneUtility.GetClone(skillSet) ?? skillSet;
        }

        SkillVisualProfile profile =
            bundle.VisualProfile;

        if (profile == null)
        {
            profile = CreateSubAsset<SkillVisualProfile>(
                bundle,
                $"{SanitizeName(prefab.name)}_VisualProfile",
                supporting: false);
        }

        ConfigureVisualProfileFromSkillSet(
            profile,
            skillSet);

        string displayName =
            data != null &&
            !string.IsNullOrWhiteSpace(data.CharacterName)
                ? data.CharacterName
                : prefab.name;

        bundle.ConfigureCore(
            DetectKind(prefab),
            displayName,
            data,
            skillSet,
            profile);

        bundle.ConfigurePrefab(prefab);

        ConfigurePackedLoadout(
            bundle,
            prefab,
            cloneUtility);

        ConfigureCharacterSpecificSettings(
            bundle,
            prefab);

        ConfigurePresentationFromPrefab(
            bundle,
            prefab);

        if (linkPrefab)
        {
            ApplyBundleLinkToCurrentPrefab(
                cloneUtility);
        }

        EditorUtility.SetDirty(bundle);
        AssetDatabase.SaveAssets();
        DestroyNestedEditors();
    }

    private void ApplyBundleLinkToCurrentPrefab(
        CharacterAuthoringCloneUtility cloneUtility)
    {
        if (bundle?.CharacterPrefab == null)
            return;

        Character prefab = bundle.CharacterPrefab;

        if (!IsPrefabAsset(prefab, out string path))
        {
            EditorUtility.DisplayDialog(
                "Character Studio",
                "Bundle에 연결된 Character가 Prefab Asset이 아닙니다.",
                "확인");
            return;
        }

        GameObject root =
            PrefabUtility.LoadPrefabContents(path);

        if (root == null)
            return;

        try
        {
            Character character =
                root.GetComponentInChildren<Character>(true);

            if (character == null)
                return;

            cloneUtility?.RemapPrefab(root);

            CharacterAuthoringLink link =
                character.GetComponent<CharacterAuthoringLink>();

            if (link == null)
            {
                link =
                    character.gameObject
                        .AddComponent<CharacterAuthoringLink>();
            }

            link.Configure(bundle);

            PrefabUtility.SaveAsPrefabAsset(
                root,
                path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.ImportAsset(path);

        GameObject prefabAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(path);

        Character refreshed =
            prefabAsset?.GetComponentInChildren<Character>(true);

        if (refreshed != null)
            bundle.ConfigurePrefab(refreshed);

        EditorUtility.SetDirty(bundle);
        AssetDatabase.SaveAssets();
    }

    private void EnsureMissingCoreAssets()
    {
        if (bundle == null)
            return;

        Character prefab = bundle.CharacterPrefab;
        CharacterAuthoringKind kind = DetectKind(prefab);
        string safeName = SanitizeName(
            string.IsNullOrWhiteSpace(bundle.DisplayName)
                ? bundle.name
                : bundle.DisplayName);

        CharacterData data = bundle.CharacterData;
        ScriptableObject skillSet = bundle.SkillSet;
        SkillVisualProfile profile = bundle.VisualProfile;

        if (data == null)
        {
            data = CreateSubAsset<CharacterData>(
                bundle,
                $"{safeName}_CharacterData",
                supporting: false);

            data.CharacterName = bundle.DisplayName;
            data.TargetMode = prefab is NormalEnemy
                ? CharacterTargetMode.SingleHP
                : CharacterTargetMode.BodyParts;
            data.SingleHpMax = 50;
        }

        if (skillSet == null)
        {
            skillSet = kind switch
            {
                CharacterAuthoringKind.Olaf =>
                    CreateSubAsset<OlafSkillSet>(
                        bundle,
                        $"{safeName}_SkillSet",
                        supporting: false),

                CharacterAuthoringKind.EliteEnemy =>
                    CreateSubAsset<EliteEnemySkillSet>(
                        bundle,
                        $"{safeName}_SkillSet",
                        supporting: false),

                CharacterAuthoringKind.NormalEnemy =>
                    CreateSubAsset<NormalEnemySkillSet>(
                        bundle,
                        $"{safeName}_SkillSet",
                        supporting: false),

                _ => null
            };
        }

        if (profile == null)
        {
            profile = CreateSubAsset<SkillVisualProfile>(
                bundle,
                $"{safeName}_VisualProfile",
                supporting: false);
        }

        if (skillSet != null)
        {
            EnsureSkillSlot(
                skillSet,
                "NormalAttack",
                ActionType.NormalAttack,
                safeName,
                profile);

            EnsureSkillSlot(
                skillSet,
                "DuelSkill",
                ActionType.Duel,
                safeName,
                profile);

            if (skillSet is not NormalEnemySkillSet)
            {
                EnsureSkillSlot(
                    skillSet,
                    "PreparationSkill",
                    ActionType.Preparation,
                    safeName,
                    profile);
            }

            EnsureSkillSlot(
                skillSet,
                "PrestigeSkill",
                ActionType.Prestige,
                safeName,
                profile);
        }

        bundle.ConfigureCore(
            kind,
            bundle.DisplayName,
            data,
            skillSet,
            profile);

        EditorUtility.SetDirty(bundle);
        EditorUtility.SetDirty(data);

        if (skillSet != null)
            EditorUtility.SetDirty(skillSet);

        if (profile != null)
            EditorUtility.SetDirty(profile);

        AssetDatabase.SaveAssets();
        DestroyNestedEditors();
    }

    private void EnsureSkillSlot(
        ScriptableObject skillSet,
        string propertyName,
        ActionType actionType,
        string safeName,
        SkillVisualProfile profile)
    {
        SerializedObject serialized =
            new SerializedObject(skillSet);

        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null ||
            property.propertyType !=
            SerializedPropertyType.ObjectReference)
        {
            return;
        }

        SkillDefinition skill =
            property.objectReferenceValue as SkillDefinition;

        if (skill == null)
        {
            skill = CreateSubAsset<SkillDefinition>(
                bundle,
                $"{safeName}_{actionType}_Skill",
                supporting: false);

            skill.SkillName =
                $"{bundle.DisplayName} {GetKoreanActionName(actionType)}";
            skill.ActionType = actionType;
            skill.BasePower = 1;
            skill.ExchangeRollCount = 3;
            skill.ResolverType = SkillResolverType.Dice;
            skill.DiceMin = 1;
            skill.DiceMax = 6;
            skill.CanBreakPart =
                actionType != ActionType.Preparation;
            skill.GainPrestige =
                actionType != ActionType.Preparation;

            property.objectReferenceValue = skill;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skillSet);
        }

        if (skill.VisualDefinition == null)
            CreateVisualForSkill(skill);

        AssignProfileVisual(
            profile,
            actionType,
            skill.VisualDefinition);
    }

    private void CreateVisualForSkill(
        SkillDefinition skill)
    {
        if (bundle == null || skill == null)
            return;

        string baseName =
            SanitizeName(
                string.IsNullOrWhiteSpace(skill.SkillName)
                    ? skill.name
                    : skill.SkillName);

        SkillCameraDefinition camera =
            CreateSubAsset<SkillCameraDefinition>(
                bundle,
                $"{baseName}_Camera",
                supporting: false);

        SkillVisualDefinition visual =
            CreateSubAsset<SkillVisualDefinition>(
                bundle,
                $"{baseName}_Visual",
                supporting: false);

        visual.AllowAsProfileFallback = false;
        visual.CameraDefinition = camera;
        skill.VisualDefinition = visual;

        AssignProfileVisual(
            bundle.VisualProfile,
            skill.ActionType,
            visual);

        EditorUtility.SetDirty(skill);
        EditorUtility.SetDirty(visual);
        EditorUtility.SetDirty(camera);
        AssetDatabase.SaveAssets();
        DestroyNestedEditors();
    }

    private void AddEffectToSkill(
        SkillDefinition skill,
        Type effectType)
    {
        if (skill == null ||
            effectType == null)
        {
            return;
        }

        SkillEffectDefinition effect =
            CreateSubAsset(
                bundle,
                effectType,
                $"{skill.name}_{ObjectNames.NicifyVariableName(effectType.Name)}",
                supporting: true) as SkillEffectDefinition;

        if (effect == null)
            return;

        Undo.RecordObject(
            skill,
            "Add Skill Effect");

        skill.Effects ??=
            new List<SkillEffectDefinition>();

        skill.Effects.Add(effect);

        EditorUtility.SetDirty(skill);
        AssetDatabase.SaveAssets();
    }

    private void AddObjectToBundleArray(
        string propertyName,
        Type type,
        bool registerSupporting)
    {
        if (bundle == null || type == null)
            return;

        ScriptableObject created =
            CreateSubAsset(
                bundle,
                type,
                ObjectNames.NicifyVariableName(type.Name),
                registerSupporting);

        if (created == null)
            return;

        SerializedObject serialized =
            new SerializedObject(bundle);

        serialized.Update();

        SerializedProperty array =
            serialized.FindProperty(propertyName);

        if (array == null || !array.isArray)
            return;

        int index = array.arraySize;
        array.InsertArrayElementAtIndex(index);
        array.GetArrayElementAtIndex(index)
            .objectReferenceValue = created;

        SerializedProperty overrideLoadout =
            serialized.FindProperty("overrideLoadout");

        if (overrideLoadout != null)
            overrideLoadout.boolValue = true;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bundle);
        AssetDatabase.SaveAssets();
    }

    private void AddConcreteSupportingAsset(
        Type type)
    {
        if (type == null ||
            type.IsAbstract ||
            !typeof(ScriptableObject).IsAssignableFrom(type))
        {
            return;
        }

        CreateSubAsset(
            bundle,
            type,
            ObjectNames.NicifyVariableName(type.Name),
            supporting: true);

        AssetDatabase.SaveAssets();
    }

    private void ShowCreateSubAssetMenu<T>(
        Action<Type> onSelected)
        where T : ScriptableObject
    {
        GenericMenu menu = new();
        bool added = false;

        IEnumerable<Type> types =
            TypeCache.GetTypesDerivedFrom<T>()
                .Where(type =>
                    type != null &&
                    !type.IsAbstract &&
                    !type.IsGenericType &&
                    typeof(ScriptableObject)
                        .IsAssignableFrom(type))
                .OrderBy(type => type.FullName);

        foreach (Type type in types)
        {
            added = true;
            Type captured = type;

            string menuName =
                string.IsNullOrWhiteSpace(type.FullName)
                    ? type.Name
                    : type.FullName.Replace('.', '/');

            menu.AddItem(
                new GUIContent(menuName),
                false,
                () => onSelected?.Invoke(captured));
        }

        if (!added)
        {
            menu.AddDisabledItem(
                new GUIContent("사용 가능한 구체 타입 없음"));
        }

        menu.ShowAsContext();
    }

    private static List<SkillDefinition>
        CollectSkillDefinitions(
            ScriptableObject skillSet)
    {
        List<SkillDefinition> result = new();
        HashSet<SkillDefinition> unique = new();

        if (skillSet == null)
            return result;

        SerializedObject serialized =
            new SerializedObject(skillSet);

        SerializedProperty property =
            serialized.GetIterator();

        bool enterChildren = true;

        while (property.Next(enterChildren))
        {
            enterChildren = true;

            if (property.propertyType !=
                SerializedPropertyType.ObjectReference)
            {
                continue;
            }

            if (property.objectReferenceValue is not
                SkillDefinition definition)
            {
                continue;
            }

            if (unique.Add(definition))
                result.Add(definition);
        }

        return result;
    }

    private static void ConfigureVisualProfileFromSkillSet(
        SkillVisualProfile profile,
        ScriptableObject skillSet)
    {
        if (profile == null || skillSet == null)
            return;

        foreach (SkillDefinition definition in
                 CollectSkillDefinitions(skillSet))
        {
            if (definition?.VisualDefinition == null)
                continue;

            AssignProfileVisual(
                profile,
                definition.ActionType,
                definition.VisualDefinition);
        }

        EditorUtility.SetDirty(profile);
    }

    private static void AssignProfileVisual(
        SkillVisualProfile profile,
        ActionType actionType,
        SkillVisualDefinition visual)
    {
        if (profile == null || visual == null)
            return;

        switch (actionType)
        {
            case ActionType.NormalAttack:
                profile.NormalAttackVisual = visual;
                break;
            case ActionType.Duel:
                profile.DuelVisual = visual;
                break;
            case ActionType.Preparation:
                profile.PreparationVisual = visual;
                break;
            case ActionType.Prestige:
                profile.PrestigeVisual = visual;
                break;
        }

        EditorUtility.SetDirty(profile);
    }

    private static void ConfigurePackedLoadout(
        CharacterAuthoringBundle targetBundle,
        Character sourceCharacter,
        CharacterAuthoringCloneUtility cloneUtility)
    {
        List<CharacterItem> sourceItems =
            ReadObjectReferenceList<CharacterItem>(
                sourceCharacter,
                "equippedItems");

        List<CharacterAugment> sourceAugments =
            ReadObjectReferenceList<CharacterAugment>(
                sourceCharacter,
                "equippedAugments");

        List<CharacterItem> items = new();
        List<CharacterAugment> augments = new();

        foreach (CharacterItem source in sourceItems)
        {
            CharacterItem value =
                cloneUtility?.GetClone(source) ?? source;

            if (value != null && !items.Contains(value))
                items.Add(value);
        }

        foreach (CharacterAugment source in sourceAugments)
        {
            CharacterAugment value =
                cloneUtility?.GetClone(source) ?? source;

            if (value != null && !augments.Contains(value))
                augments.Add(value);
        }

        targetBundle.ConfigureLoadout(
            items,
            augments,
            shouldOverride: true);
    }

    private static void ConfigureCharacterSpecificSettings(
        CharacterAuthoringBundle targetBundle,
        Character sourceCharacter)
    {
        if (sourceCharacter is NormalEnemy)
        {
            SerializedObject serialized =
                new SerializedObject(sourceCharacter);

            int singleMaxHp =
                serialized.FindProperty("singleMaxHP")
                    ?.intValue ?? 50;

            targetBundle.ConfigureNormalEnemy(
                singleMaxHp,
                shouldOverride: true);
        }

        if (sourceCharacter is not EliteEnemy)
            return;

        SerializedObject elite =
            new SerializedObject(sourceCharacter);

        bool usePosture =
            elite.FindProperty("usePostureRotation")
                ?.boolValue ?? true;

        SerializedProperty posture =
            elite.FindProperty("postureSettings");

        EnemyPostureSettings settings =
            new EnemyPostureSettings();

        if (posture != null)
        {
            CopyInt(posture, "MinimumTurns", value => settings.MinimumTurns = value);
            CopyInt(posture, "MaximumTurns", value => settings.MaximumTurns = value);
            CopyInt(posture, "NormalAttackSlotLimit", value => settings.NormalAttackSlotLimit = value);
            CopyInt(posture, "CrouchingAttackSlotLimit", value => settings.CrouchingAttackSlotLimit = value);
            CopyInt(posture, "OffensiveAttackSlotLimit", value => settings.OffensiveAttackSlotLimit = value);
            CopyInt(posture, "ExpectedMomentumDriftPerTurn", value => settings.ExpectedMomentumDriftPerTurn = value);
        }

        targetBundle.ConfigureEliteEnemy(
            usePosture,
            settings);
    }

    private static void ConfigurePresentationFromPrefab(
        CharacterAuthoringBundle targetBundle,
        Character prefab)
    {
        Animator animator =
            prefab?.GetComponentInChildren<Animator>(true);

        SerializedObject serialized =
            new SerializedObject(targetBundle);

        serialized.Update();

        SerializedProperty controller =
            serialized.FindProperty("animatorController");

        SerializedProperty avatar =
            serialized.FindProperty("avatar");

        SerializedProperty overrideController =
            serialized.FindProperty("overrideAnimatorController");

        SerializedProperty overrideAvatar =
            serialized.FindProperty("overrideAvatar");

        if (controller != null)
            controller.objectReferenceValue = animator?.runtimeAnimatorController;

        if (avatar != null)
            avatar.objectReferenceValue = animator?.avatar;

        if (overrideController != null)
            overrideController.boolValue = animator?.runtimeAnimatorController != null;

        if (overrideAvatar != null)
            overrideAvatar.boolValue = animator?.avatar != null;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<ValidationMessage> ValidateBundle(
        CharacterAuthoringBundle targetBundle)
    {
        List<ValidationMessage> result = new();

        if (targetBundle == null)
            return result;

        Character prefab = targetBundle.CharacterPrefab;

        if (prefab == null)
        {
            result.Add(new ValidationMessage(
                MessageType.Error,
                "Character Prefab이 없습니다."));
        }
        else if (!targetBundle.IsCompatibleWith(
                     prefab,
                     out string reason))
        {
            result.Add(new ValidationMessage(
                MessageType.Error,
                reason));
        }

        if (targetBundle.CharacterData == null)
        {
            result.Add(new ValidationMessage(
                MessageType.Error,
                "CharacterData가 없습니다."));
        }

        if (targetBundle.SkillSet == null)
        {
            result.Add(new ValidationMessage(
                MessageType.Warning,
                "SkillSet이 없습니다. 스킬을 사용하지 않는 Custom Character가 아니라면 연결이 필요합니다."));
        }

        foreach (SkillDefinition skill in
                 CollectSkillDefinitions(targetBundle.SkillSet))
        {
            if (skill.VisualDefinition == null)
            {
                result.Add(new ValidationMessage(
                    MessageType.Warning,
                    $"{skill.name}: SkillVisualDefinition이 없습니다."));
                continue;
            }

            if (skill.VisualDefinition.UsesTargetCamera &&
                skill.VisualDefinition.CameraDefinition == null)
            {
                result.Add(new ValidationMessage(
                    MessageType.Warning,
                    $"{skill.name}: Target Camera를 사용하지만 CameraDefinition이 없습니다."));
            }
        }

        if (prefab != null)
        {
            CharacterAuthoringLink link =
                prefab.GetComponent<CharacterAuthoringLink>();

            if (link == null || link.Bundle != targetBundle)
            {
                result.Add(new ValidationMessage(
                    MessageType.Warning,
                    "Prefab의 CharacterAuthoringLink가 없거나 다른 Bundle을 참조합니다."));
            }

            if (prefab.GetComponentInChildren<Animator>(true) == null)
            {
                result.Add(new ValidationMessage(
                    MessageType.Warning,
                    "Prefab 하위에 Animator가 없습니다."));
            }

            if (prefab.GetComponentInChildren<CharacterView>(true) == null &&
                prefab.GetComponent<CharacterView>() == null)
            {
                result.Add(new ValidationMessage(
                    MessageType.Warning,
                    "Prefab에서 CharacterView를 찾지 못했습니다."));
            }
        }

        return result;
    }

    private static void SyncKindFromPrefab(
        CharacterAuthoringBundle targetBundle)
    {
        if (targetBundle == null ||
            targetBundle.CharacterPrefab == null)
        {
            return;
        }

        CharacterAuthoringKind detected =
            DetectKind(targetBundle.CharacterPrefab);

        if (targetBundle.Kind == detected)
            return;

        SerializedObject serialized =
            new SerializedObject(targetBundle);

        SerializedProperty kind =
            serialized.FindProperty("kind");

        if (kind == null)
            return;

        kind.enumValueIndex = (int)detected;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(targetBundle);
    }

    private static CharacterAuthoringKind DetectKind(
        Character character)
    {
        return character switch
        {
            Olaf => CharacterAuthoringKind.Olaf,
            EliteEnemy => CharacterAuthoringKind.EliteEnemy,
            NormalEnemy => CharacterAuthoringKind.NormalEnemy,
            _ => CharacterAuthoringKind.Custom
        };
    }

    private static string GetRoleDisplayName(
        Character character)
    {
        return character switch
        {
            NormalEnemy => "일반 적 — 단일 HP",
            EliteEnemy => "정예 적 — 부위형",
            Enemy => "적 — Custom Runtime",
            Olaf => "플레이어블 캐릭터 — Olaf Runtime",
            null => "미지정",
            _ => "플레이어블/Custom Character"
        };
    }

    private static bool IsPrefabAsset(
        Character character,
        out string path)
    {
        path = character == null
            ? null
            : AssetDatabase.GetAssetPath(character);

        return !string.IsNullOrWhiteSpace(path) &&
               path.EndsWith(
                   ".prefab",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSuggestedFolder(
        string prefabPath)
    {
        if (string.IsNullOrWhiteSpace(prefabPath))
            return DefaultBundleFolder;

        string folder =
            Path.GetDirectoryName(prefabPath)
                ?.Replace('\\', '/');

        return string.IsNullOrWhiteSpace(folder)
            ? DefaultBundleFolder
            : folder;
    }

    private static T ReadObjectReference<T>(
        UnityEngine.Object source,
        string propertyName)
        where T : UnityEngine.Object
    {
        if (source == null)
            return null;

        SerializedObject serialized =
            new SerializedObject(source);

        return serialized.FindProperty(propertyName)
            ?.objectReferenceValue as T;
    }

    private static List<T> ReadObjectReferenceList<T>(
        UnityEngine.Object source,
        string propertyName)
        where T : UnityEngine.Object
    {
        List<T> result = new();

        if (source == null)
            return result;

        SerializedObject serialized =
            new SerializedObject(source);

        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null || !property.isArray)
            return result;

        for (int i = 0; i < property.arraySize; i++)
        {
            T value =
                property.GetArrayElementAtIndex(i)
                    .objectReferenceValue as T;

            if (value != null)
                result.Add(value);
        }

        return result;
    }

    private static T CreateSubAsset<T>(
        CharacterAuthoringBundle targetBundle,
        string assetName,
        bool supporting)
        where T : ScriptableObject
    {
        return CreateSubAsset(
            targetBundle,
            typeof(T),
            assetName,
            supporting) as T;
    }

    private static ScriptableObject CreateSubAsset(
        CharacterAuthoringBundle targetBundle,
        Type type,
        string assetName,
        bool supporting)
    {
        if (targetBundle == null ||
            type == null ||
            type.IsAbstract ||
            !typeof(ScriptableObject).IsAssignableFrom(type))
        {
            return null;
        }

        ScriptableObject asset =
            ScriptableObject.CreateInstance(type);

        asset.name =
            string.IsNullOrWhiteSpace(assetName)
                ? ObjectNames.NicifyVariableName(type.Name)
                : assetName;

        AssetDatabase.AddObjectToAsset(
            asset,
            targetBundle);

        if (supporting)
            targetBundle.RegisterSupportingAsset(asset);
        else
            targetBundle.RegisterIncludedAsset(asset);

        EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(targetBundle);
        AssetDatabase.ImportAsset(
            AssetDatabase.GetAssetPath(targetBundle));

        return asset;
    }

    private static void CopyInt(
        SerializedProperty parent,
        string childName,
        Action<int> setter)
    {
        SerializedProperty child =
            parent?.FindPropertyRelative(childName);

        if (child != null)
            setter?.Invoke(child.intValue);
    }

    private static string SanitizeName(
        string value)
    {
        string safe = string.IsNullOrWhiteSpace(value)
            ? "Character"
            : value.Trim();

        foreach (char invalid in
                 Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalid, '_');
        }

        return safe.Replace('/', '_').Replace('\\', '_');
    }

    private static string GetKoreanActionName(
        ActionType actionType)
    {
        return actionType switch
        {
            ActionType.NormalAttack => "일반공격",
            ActionType.Duel => "결투",
            ActionType.Preparation => "도사림",
            ActionType.Prestige => "위세",
            _ => actionType.ToString()
        };
    }

    private void DestroyNestedEditors()
    {
        foreach (Editor editor in nestedEditors.Values)
        {
            if (editor != null)
                DestroyImmediate(editor);
        }

        nestedEditors.Clear();
    }

    private readonly struct ValidationMessage
    {
        public readonly MessageType Type;
        public readonly string Text;

        public ValidationMessage(
            MessageType type,
            string text)
        {
            Type = type;
            Text = text;
        }
    }
}
#endif
