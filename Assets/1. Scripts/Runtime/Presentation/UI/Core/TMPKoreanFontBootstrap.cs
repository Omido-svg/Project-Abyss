using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

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
