using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class TMPKoreanFontBootstrap :
    MonoBehaviour
{
    private const string ExpectedPrimaryFontName =
        "TheOneLord SDF";

    private const string ExpectedFallbackFontName =
        "NotoSansKR Runtime SDF";

    private const string LegacyFallbackFontName =
        "NotoSansKR-VariableFont_wght SDF";

    private static readonly HashSet<int>
        PrewarmedFontAssets =
            new HashSet<int>();

    private static readonly HashSet<int>
        ReportedPrewarmFailures =
            new HashSet<int>();

#if UNITY_EDITOR
    // Project TMP_FontAsset을 Editor Play Mode에서 직접 동적으로 수정하면
    // TMP_EditorResourceManager의 EndOfFrame import와 경합할 수 있다.
    // Play Mode에서는 font/material/atlas를 메모리 전용 복제본으로 분리한다.
    private readonly Dictionary<int, TMP_FontAsset>
        editorPlayFontClones =
            new Dictionary<int, TMP_FontAsset>();

    private readonly List<UnityEngine.Object>
        editorPlayRuntimeObjects =
            new List<UnityEngine.Object>();
#endif

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        PrewarmedFontAssets.Clear();
        ReportedPrewarmFailures.Clear();
    }

    // 기존 Scene 직렬화 호환을 위해 필드 이름을 유지한다.
    // 이 필드는 TheOneLord SDF를 가리킨다.
    [SerializeField]
    private TMP_FontAsset koreanFontAsset;

    [Header("Korean Fallback")]
    [SerializeField]
    private TMP_FontAsset koreanFallbackFontAsset;

    // 기존 직렬화 호환 필드다.
    // 로드된 Scene의 모든 TMP_Text에 Primary 폰트를 적용한다.
    [SerializeField]
    private bool assignToLoadedNotoAndLiberationTexts =
        true;

    [SerializeField]
    private bool addAsGlobalFallback =
        true;

    [SerializeField]
    private bool prewarmLoadedSceneCharacters =
        true;

    [SerializeField]
    private bool logMissingGlyphWarning =
        true;

    [SerializeField, TextArea(2, 5)]
    private string requiredCharacters =
        "한글 테스트 위세 방어 공격 결투 도사림 체력 상태 부위 속도 에너지 " +
        "승률 피해량 합 굴림 턴 기세 빛 적 아군 스킬 패시브 상세 정보";

    private void Awake()
    {
        ResolveFontAssets();

#if UNITY_EDITOR
        if (Application.isPlaying)
            DetachEditorPlayModeFontAssets();
#endif

        if (koreanFontAsset == null)
        {
            Debug.LogError(
                "[TMPKoreanFontBootstrap] " +
                $"'{ExpectedPrimaryFontName}'를 찾지 못했습니다.",
                this);
            return;
        }

        RegisterFallbackFonts();

        TMP_Text[] loadedTexts =
            Resources.FindObjectsOfTypeAll<TMP_Text>();

        string prewarmCharacters =
            BuildPrewarmCharacters(
                loadedTexts);

        PrewarmPrimaryAndFallbacks(
            prewarmCharacters);

        if (assignToLoadedNotoAndLiberationTexts)
        {
            RepairLoadedTexts(
                loadedTexts);
        }
    }

    private void ResolveFontAssets()
    {
        TMP_FontAsset[] loadedFonts =
            Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

        if (koreanFontAsset == null)
        {
            koreanFontAsset =
                FindLoadedFont(
                    loadedFonts,
                    ExpectedPrimaryFontName);
        }

        if (koreanFallbackFontAsset == null)
        {
            koreanFallbackFontAsset =
                FindLoadedFont(
                    loadedFonts,
                    ExpectedFallbackFontName);
        }

        if (koreanFallbackFontAsset == null)
        {
            koreanFallbackFontAsset =
                FindLoadedFont(
                    loadedFonts,
                    LegacyFallbackFontName);
        }

        if (koreanFallbackFontAsset == null &&
            koreanFontAsset?.fallbackFontAssetTable != null)
        {
            foreach (TMP_FontAsset fallback
                     in koreanFontAsset
                         .fallbackFontAssetTable)
            {
                if (LooksLikeKoreanFallback(
                        fallback))
                {
                    koreanFallbackFontAsset =
                        fallback;
                    break;
                }
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor Play Mode에서 Project asset인 Dynamic TMP_FontAsset을 직접 사용하면
    /// 텍스트 렌더링 중 glyph/atlas 갱신이 원본 asset을 dirty 상태로 만들고,
    /// TMP_EditorResourceManager의 EndOfFrame ImportAsset과 경합할 수 있다.
    ///
    /// 따라서 Play Mode에서는 FontAsset + Material + AtlasTexture를 메모리 전용으로
    /// 복제하고, fallback graph도 project asset 대신 동일한 runtime clone을 사용한다.
    /// Build에서는 이 경로가 컴파일되지 않는다.
    /// </summary>
    private void DetachEditorPlayModeFontAssets()
    {
        TMP_FontAsset sourcePrimary =
            koreanFontAsset;

        TMP_FontAsset sourceFallback =
            koreanFallbackFontAsset;

        koreanFontAsset =
            GetOrCreateEditorPlayFontClone(
                sourcePrimary);

        koreanFallbackFontAsset =
            sourceFallback == sourcePrimary
                ? koreanFontAsset
                : GetOrCreateEditorPlayFontClone(
                    sourceFallback);

        if (koreanFontAsset == null ||
            koreanFallbackFontAsset == null ||
            koreanFallbackFontAsset == koreanFontAsset)
        {
            return;
        }

        koreanFontAsset.fallbackFontAssetTable ??=
            new List<TMP_FontAsset>();

        // 알려진 한국어 fallback은 항상 첫 번째로 둔다.
        koreanFontAsset.fallbackFontAssetTable.Remove(
            koreanFallbackFontAsset);
        koreanFontAsset.fallbackFontAssetTable.Insert(
            0,
            koreanFallbackFontAsset);
    }

    private TMP_FontAsset GetOrCreateEditorPlayFontClone(
        TMP_FontAsset source)
    {
        if (source == null ||
            !AssetDatabase.Contains(source))
        {
            return source;
        }

        int sourceId =
            source.GetInstanceID();

        if (editorPlayFontClones.TryGetValue(
                sourceId,
                out TMP_FontAsset existing))
        {
            return existing;
        }

        TMP_FontAsset clone =
            Instantiate(source);

        clone.name =
            source.name + " [Editor PlayMode]";
        // Scene TMP_Text가 이 runtime clone을 직접 참조한다.
        // HideAndDontSave에는 DontSaveInEditor가 포함되어 있어
        // Editor Window repaint/layout persistence 중 native assertion을 유발할 수 있다.
        // 프로젝트 Asset과 분리된 clone이라는 사실은 그대로 유지하고,
        // Build 저장만 금지한다. OnDestroy에서 명시적으로 정리한다.
        clone.hideFlags =
            HideFlags.DontSaveInBuild;

        // 먼저 등록해서 fallback graph가 순환 참조여도 재귀가 끝나게 한다.
        editorPlayFontClones.Add(
            sourceId,
            clone);
        editorPlayRuntimeObjects.Add(clone);

        Texture2D[] sourceAtlases =
            source.atlasTextures;

        if (sourceAtlases != null &&
            sourceAtlases.Length > 0)
        {
            Texture2D[] runtimeAtlases =
                new Texture2D[sourceAtlases.Length];

            for (int i = 0;
                 i < sourceAtlases.Length;
                 i++)
            {
                Texture2D sourceAtlas =
                    sourceAtlases[i];

                if (sourceAtlas == null)
                    continue;

                Texture2D atlasClone =
                    Instantiate(sourceAtlas);

                atlasClone.name =
                    sourceAtlas.name + " [Editor PlayMode]";
                atlasClone.hideFlags =
                    HideFlags.DontSaveInBuild;

                runtimeAtlases[i] =
                    atlasClone;

                editorPlayRuntimeObjects.Add(
                    atlasClone);
            }

            clone.atlasTextures =
                runtimeAtlases;
        }

        if (source.material != null)
        {
            Material materialClone =
                Instantiate(source.material);

            materialClone.name =
                source.material.name + " [Editor PlayMode]";
            materialClone.hideFlags =
                HideFlags.DontSaveInBuild;

            Texture2D[] cloneAtlases =
                clone.atlasTextures;

            if (cloneAtlases != null &&
                cloneAtlases.Length > 0 &&
                cloneAtlases[0] != null)
            {
                materialClone.mainTexture =
                    cloneAtlases[0];
            }

            clone.material =
                materialClone;

            editorPlayRuntimeObjects.Add(
                materialClone);
        }

        List<TMP_FontAsset> runtimeFallbacks =
            new List<TMP_FontAsset>();

        if (source.fallbackFontAssetTable != null)
        {
            foreach (TMP_FontAsset sourceFallback
                     in source.fallbackFontAssetTable)
            {
                TMP_FontAsset runtimeFallback =
                    GetOrCreateEditorPlayFontClone(
                        sourceFallback);

                if (runtimeFallback != null &&
                    runtimeFallback != clone &&
                    !runtimeFallbacks.Contains(
                        runtimeFallback))
                {
                    runtimeFallbacks.Add(
                        runtimeFallback);
                }
            }
        }

        clone.fallbackFontAssetTable =
            runtimeFallbacks;

        return clone;
    }

    private void OnDestroy()
    {
        // Editor PlayMode runtime copies만 파괴한다. Project asset에는 손대지 않는다.
        for (int i = editorPlayRuntimeObjects.Count - 1;
             i >= 0;
             i--)
        {
            UnityEngine.Object runtimeObject =
                editorPlayRuntimeObjects[i];

            if (runtimeObject != null)
                Destroy(runtimeObject);
        }

        editorPlayRuntimeObjects.Clear();
        editorPlayFontClones.Clear();
    }
#endif

    private static TMP_FontAsset FindLoadedFont(
        IEnumerable<TMP_FontAsset> fonts,
        string exactName)
    {
        if (fonts == null ||
            string.IsNullOrWhiteSpace(exactName))
        {
            return null;
        }

        foreach (TMP_FontAsset font in fonts)
        {
            if (font != null &&
                font.name == exactName)
            {
                return font;
            }
        }

        return null;
    }

    private static bool LooksLikeKoreanFallback(
        TMP_FontAsset font)
    {
        if (font == null)
            return false;

        string lowerName =
            font.name.ToLowerInvariant();

        return
            lowerName.Contains("notosanskr") ||
            lowerName.Contains("noto sans kr") ||
            lowerName.Contains("noto sans cjk kr") ||
            lowerName.Contains("korean") ||
            lowerName.Contains("nanum");
    }

    private void RegisterFallbackFonts()
    {
        if (koreanFontAsset == null)
            return;

#if UNITY_EDITOR
        // 정상적인 Play Mode 경로에서는 Awake가 project asset을 runtime clone으로
        // 분리한다. 혹시 분리에 실패해 여전히 project asset이면 절대 수정하지 않는다.
        if (Application.isPlaying &&
            AssetDatabase.Contains(koreanFontAsset))
        {
            return;
        }
#endif

        koreanFontAsset.fallbackFontAssetTable ??=
            new List<TMP_FontAsset>();

        if (koreanFallbackFontAsset != null &&
            koreanFallbackFontAsset != koreanFontAsset)
        {
            koreanFontAsset
                .fallbackFontAssetTable
                .Remove(
                    koreanFallbackFontAsset);

            koreanFontAsset
                .fallbackFontAssetTable
                .Insert(
                    0,
                    koreanFallbackFontAsset);
        }

#if UNITY_EDITOR
        // TMP_Settings 자체도 Project asset이다. Editor Play Mode에서는 runtime clone의
        // fallback table만 사용하고 Global Settings asset은 건드리지 않는다.
        if (Application.isPlaying)
            return;
#endif

        if (!addAsGlobalFallback)
            return;

        List<TMP_FontAsset> globalFallbacks =
            TMP_Settings.fallbackFontAssets;

        if (globalFallbacks == null)
            return;

        if (koreanFallbackFontAsset != null &&
            !globalFallbacks.Contains(
                koreanFallbackFontAsset))
        {
            globalFallbacks.Insert(
                0,
                koreanFallbackFontAsset);
        }

        foreach (TMP_FontAsset fallback
                 in koreanFontAsset
                     .fallbackFontAssetTable)
        {
            if (fallback != null &&
                fallback != koreanFontAsset &&
                !globalFallbacks.Contains(fallback))
            {
                globalFallbacks.Add(
                    fallback);
            }
        }
    }

    private string BuildPrewarmCharacters(
        TMP_Text[] loadedTexts)
    {
        if (!prewarmLoadedSceneCharacters)
        {
            return
                requiredCharacters ??
                string.Empty;
        }

        HashSet<char> characters =
            new HashSet<char>();

        AddCharacters(
            characters,
            requiredCharacters);

        if (loadedTexts != null)
        {
            foreach (TMP_Text text
                     in loadedTexts)
            {
                if (text == null ||
                    !text.gameObject.scene.IsValid())
                {
                    continue;
                }

                AddCharacters(
                    characters,
                    text.text);
            }
        }

        StringBuilder result =
            new StringBuilder(
                characters.Count);

        foreach (char character
                 in characters)
        {
            result.Append(character);
        }

        return result.ToString();
    }

    private void PrewarmPrimaryAndFallbacks(
        string characters)
    {
        if (koreanFontAsset == null ||
            string.IsNullOrEmpty(characters))
        {
            return;
        }

        int instanceId =
            koreanFontAsset.GetInstanceID();

        if (!PrewarmedFontAssets.Add(
                instanceId))
        {
            return;
        }

        string remaining =
            TryAddOrFilterMissing(
                koreanFontAsset,
                characters);

        if (!string.IsNullOrEmpty(remaining) &&
            koreanFontAsset.fallbackFontAssetTable != null)
        {
            foreach (TMP_FontAsset fallback
                     in koreanFontAsset
                         .fallbackFontAssetTable)
            {
                if (fallback == null ||
                    string.IsNullOrEmpty(remaining))
                {
                    continue;
                }

                remaining =
                    TryAddOrFilterMissing(
                        fallback,
                        remaining);
            }
        }

        if (!logMissingGlyphWarning ||
            string.IsNullOrEmpty(remaining) ||
            !ReportedPrewarmFailures.Add(
                instanceId))
        {
            return;
        }

        string preview =
            remaining.Length <= 24
                ? remaining
                : remaining.Substring(0, 24) + "…";

        Debug.LogWarning(
            "[TMPKoreanFontBootstrap] " +
            $"한국어 Fallback에서 {remaining.Length}개 문자를 찾지 못했습니다. " +
            $"예시: {preview}\n" +
            "Tools > Project Abyss > Repair TheOneLord Korean Fallback v4.7.5를 실행하세요.",
            koreanFontAsset);
    }

    private static string TryAddOrFilterMissing(
        TMP_FontAsset font,
        string characters)
    {
        if (font == null ||
            string.IsNullOrEmpty(characters))
        {
            return
                characters ??
                string.Empty;
        }

        // Dynamic Asset이어도 Source Font가 없는 손상된 에셋이면
        // TryAddCharacters가 Font Face 경고를 출력한다.
        // 이 경우 기존 Atlas만 검사하고 Editor Repair Tool에 맡긴다.
        if (font.atlasPopulationMode ==
                AtlasPopulationMode.Dynamic &&
            font.sourceFontFile != null)
        {
#if UNITY_EDITOR
            // Editor Play Mode에서 프로젝트 에셋인 TMP_FontAsset을 동적으로
            // 변경하면 TMP_EditorResourceManager의 EndOfFrame Import와 충돌해
            // "generated inconsistent result"가 발생할 수 있다.
            // 글리프 생성은 전용 Editor Repair 메뉴에서 Edit Mode에 수행한다.
            if (Application.isPlaying &&
                AssetDatabase.Contains(font))
            {
                return FilterMissingCharacters(
                    font,
                    characters);
            }
#endif

            font.isMultiAtlasTexturesEnabled =
                true;

            font.TryAddCharacters(
                characters,
                out string missingCharacters,
                true);

            return
                missingCharacters ??
                string.Empty;
        }

        return FilterMissingCharacters(
            font,
            characters);
    }

    private static string FilterMissingCharacters(
        TMP_FontAsset font,
        string characters)
    {
        if (font == null ||
            string.IsNullOrEmpty(characters))
        {
            return characters ?? string.Empty;
        }

        StringBuilder missing =
            new StringBuilder();

        foreach (char character in characters)
        {
            if (!font.HasCharacter(
                    character,
                    false,
                    false))
            {
                missing.Append(character);
            }
        }

        return missing.ToString();
    }

    private static void AddCharacters(
        ICollection<char> destination,
        string value)
    {
        if (destination == null ||
            string.IsNullOrEmpty(value))
        {
            return;
        }

        foreach (char character in value)
        {
            if (!char.IsControl(character))
            {
                destination.Add(character);
            }
        }
    }

    private void RepairLoadedTexts(
        TMP_Text[] loadedTexts)
    {
        if (loadedTexts == null ||
            koreanFontAsset == null)
        {
            return;
        }

        int changed = 0;

        foreach (TMP_Text text
                 in loadedTexts)
        {
            if (text == null ||
                !text.gameObject.scene.IsValid())
            {
                continue;
            }

            bool wasChanged =
                text.font != koreanFontAsset ||
                text.fontSharedMaterial !=
                koreanFontAsset.material;

            text.font =
                koreanFontAsset;

            if (koreanFontAsset.material != null)
            {
                text.fontSharedMaterial =
                    koreanFontAsset.material;
            }

            text.havePropertiesChanged =
                true;

            if (wasChanged)
                changed++;
        }

        if (changed > 0)
        {
            Debug.Log(
                "[TMPKoreanFontBootstrap] " +
                $"로드된 Scene TMP 텍스트 {changed}개에 " +
                $"'{koreanFontAsset.name}'를 적용했습니다.",
                this);
        }
    }
}