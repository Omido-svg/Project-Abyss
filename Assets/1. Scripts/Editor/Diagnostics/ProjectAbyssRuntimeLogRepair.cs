#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class ProjectAbyssRuntimeLogRepair
{
    private const string MenuPath =
        "Tools/Project Abyss/Diagnostics/Repair Runtime Log Issues v1.2.2";

    private const string PrimaryFontPath =
        "Assets/7. Fonts/TheOneLord SDF.asset";

    private const string FallbackFontPath =
        "Assets/7. Fonts/NotoSansKR-VariableFont_wght SDF.asset";

    private static readonly string[] TextExtensions =
    {
        ".cs", ".asset", ".prefab", ".unity", ".json", ".txt", ".uss", ".uxml"
    };

    [MenuItem(MenuPath, false, 2100)]
    public static void RepairAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Runtime Log Repair",
                "Play Mode를 종료한 뒤 실행하세요.",
                "확인");
            return;
        }

        try
        {
            int fontGlyphs = RepairKoreanFallback();
            int prefabCount = RepairTestPrefabAnimatorReferences();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Runtime Log Repair v1.2.2",
                "복구 완료\n\n" +
                $"- 한국어 Fallback 요청 문자: {fontGlyphs}개\n" +
                $"- Animator 참조 확인 Prefab: {prefabCount}개\n\n" +
                "다시 Play한 뒤 Console을 확인하세요.",
                "확인");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Runtime Log Repair 실패",
                exception.Message,
                "확인");
        }
    }

    private static int RepairKoreanFallback()
    {
        TMP_FontAsset primary =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                PrimaryFontPath);

        TMP_FontAsset fallback =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                FallbackFontPath);

        if (primary == null)
        {
            throw new InvalidOperationException(
                $"Primary TMP Font를 찾지 못했습니다: {PrimaryFontPath}");
        }

        if (fallback == null)
        {
            throw new InvalidOperationException(
                $"한국어 Fallback TMP Font를 찾지 못했습니다: {FallbackFontPath}");
        }

        Font sourceFont =
            fallback.sourceFontFile != null
                ? fallback.sourceFontFile
                : FindKoreanSourceFont();

        if (sourceFont == null)
        {
            throw new InvalidOperationException(
                "NotoSansKR 원본 Font(.ttf/.otf)를 찾지 못했습니다. " +
                "Fallback Font Asset의 Source Font File을 먼저 연결하세요.");
        }

        Undo.RecordObject(primary, "Repair Korean TMP Fallback");
        Undo.RecordObject(fallback, "Repair Korean TMP Fallback");

        SerializedObject fallbackObject =
            new SerializedObject(fallback);

        SerializedProperty sourceFontProperty =
            fallbackObject.FindProperty("m_SourceFontFile");

        if (sourceFontProperty != null)
        {
            sourceFontProperty.objectReferenceValue =
                sourceFont;
        }

        SerializedProperty sourceGuidProperty =
            fallbackObject.FindProperty("m_SourceFontFileGUID");

        if (sourceGuidProperty != null)
        {
            sourceGuidProperty.stringValue =
                AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(sourceFont));
        }

        fallbackObject.ApplyModifiedPropertiesWithoutUndo();

        fallback.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fallback.isMultiAtlasTexturesEnabled = true;

        primary.fallbackFontAssetTable ??=
            new List<TMP_FontAsset>();

        primary.fallbackFontAssetTable.Remove(fallback);
        primary.fallbackFontAssetTable.Insert(0, fallback);

        string characters =
            CollectProjectKoreanCharacters();

        fallback.TryAddCharacters(
            characters,
            out string missing,
            true);

        EditorUtility.SetDirty(primary);
        EditorUtility.SetDirty(fallback);

        if (!string.IsNullOrEmpty(missing))
        {
            string preview =
                missing.Length <= 48
                    ? missing
                    : missing.Substring(0, 48) + "…";

            Debug.LogWarning(
                "[RuntimeLogRepair] 원본 폰트에도 없는 문자가 남았습니다: " +
                preview,
                fallback);
        }

        return characters.Length;
    }

    private static Font FindKoreanSourceFont()
    {
        string[] guids =
            AssetDatabase.FindAssets("NotoSansKR t:Font");

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            Font font =
                AssetDatabase.LoadAssetAtPath<Font>(path);

            if (font != null)
                return font;
        }

        guids =
            AssetDatabase.FindAssets("Noto Sans KR t:Font");

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            Font font =
                AssetDatabase.LoadAssetAtPath<Font>(path);

            if (font != null)
                return font;
        }

        return null;
    }

    private static string CollectProjectKoreanCharacters()
    {
        HashSet<char> characters =
            new HashSet<char>();

        for (char value = ' '; value <= '~'; value++)
            characters.Add(value);

        string[] paths =
            AssetDatabase.GetAllAssetPaths();

        foreach (string assetPath in paths)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !HasTextExtension(assetPath))
            {
                continue;
            }

            string absolutePath =
                Path.GetFullPath(assetPath);

            if (!File.Exists(absolutePath))
                continue;

            try
            {
                string text =
                    File.ReadAllText(absolutePath);

                foreach (char character in text)
                {
                    if (IsKoreanOrCommonPunctuation(character))
                        characters.Add(character);
                }
            }
            catch (IOException)
            {
                // 잠긴 파일은 건너뛴다.
            }
            catch (UnauthorizedAccessException)
            {
                // 읽을 수 없는 파일은 건너뛴다.
            }
        }

        char[] result =
            new char[characters.Count];

        characters.CopyTo(result);
        Array.Sort(result);
        return new string(result);
    }

    private static bool HasTextExtension(
        string path)
    {
        string extension =
            Path.GetExtension(path);

        foreach (string candidate in TextExtensions)
        {
            if (string.Equals(
                    extension,
                    candidate,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsKoreanOrCommonPunctuation(
        char value)
    {
        return
            (value >= '\u1100' && value <= '\u11FF') ||
            (value >= '\u3130' && value <= '\u318F') ||
            (value >= '\uAC00' && value <= '\uD7A3') ||
            (value >= '\u3000' && value <= '\u303F') ||
            (value >= '\u2000' && value <= '\u206F') ||
            value == '·' || value == '→' || value == '…';
    }

    private static int RepairTestPrefabAnimatorReferences()
    {
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/2. Data/TestEncounters/Animation/TestCharacter.controller");

        CharacterPresentationProfile presentation =
            AssetDatabase.LoadAssetAtPath<CharacterPresentationProfile>(
                "Assets/2. Data/TestEncounters/Animation/TestCharacterPresentation.asset");

        if (controller == null)
        {
            Debug.LogWarning(
                "[RuntimeLogRepair] TestCharacter.controller가 없어 " +
                "Prefab Animator 참조 복구를 건너뜁니다.");
            return 0;
        }

        string[] prefabPaths =
        {
            "Assets/2. Data/TestEncounters/Prefabs/Yujin_Test.prefab",
            "Assets/2. Data/TestEncounters/Prefabs/NormalEnemy_Test.prefab",
            "Assets/2. Data/TestEncounters/Prefabs/BossEnemy_Test.prefab"
        };

        int repaired = 0;

        foreach (string prefabPath in prefabPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                continue;

            GameObject root =
                PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                Animator animator =
                    root.GetComponentInChildren<Animator>(true);

                Character character =
                    root.GetComponent<Character>();

                CharacterView view =
                    root.GetComponent<CharacterView>();

                if (animator == null ||
                    character == null ||
                    view == null)
                {
                    Debug.LogWarning(
                        $"[RuntimeLogRepair] 필수 컴포넌트 누락: {prefabPath}");
                    continue;
                }

                animator.runtimeAnimatorController =
                    controller;

                SerializedObject viewObject =
                    new SerializedObject(view);

                viewObject.FindProperty("character").objectReferenceValue =
                    character;

                viewObject.FindProperty("animator").objectReferenceValue =
                    animator;

                if (presentation != null)
                {
                    viewObject.FindProperty("presentationProfile")
                        .objectReferenceValue = presentation;
                }

                viewObject.ApplyModifiedPropertiesWithoutUndo();

                CharacterAuthoringLink link =
                    root.GetComponent<CharacterAuthoringLink>();

                if (link?.Bundle != null)
                {
                    SerializedObject bundleObject =
                        new SerializedObject(link.Bundle);

                    bundleObject.FindProperty("animatorController")
                        .objectReferenceValue = controller;

                    bundleObject.FindProperty("overrideAnimatorController")
                        .boolValue = true;

                    if (presentation != null)
                    {
                        bundleObject.FindProperty("presentationProfile")
                            .objectReferenceValue = presentation;
                    }

                    bundleObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(link.Bundle);
                }

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath);

                repaired++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return repaired;
    }
}
#endif
