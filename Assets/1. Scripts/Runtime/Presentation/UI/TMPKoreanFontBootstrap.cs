using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class TMPKoreanFontBootstrap : MonoBehaviour
{
    private const string ExpectedFontAssetName = "NotoSansKR-VariableFont_wght SDF";

    private static readonly HashSet<int> PrewarmedFontAssets = new();
    private static readonly HashSet<int> ReportedPrewarmFailures = new();

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        PrewarmedFontAssets.Clear();
        ReportedPrewarmFailures.Clear();
    }

    [SerializeField] private TMP_FontAsset koreanFontAsset;
    [SerializeField] private bool assignToLoadedNotoAndLiberationTexts = true;
    [SerializeField] private bool addAsGlobalFallback = true;
    [SerializeField] private bool prewarmLoadedSceneCharacters = true;
    [SerializeField, TextArea(2, 5)]
    private string requiredCharacters =
        "한글 테스트 위세 방어도 대상 없음 캐릭터 선택 공격 결투 도사림 체력 상태 부위 속도 에너지";

    private void Awake()
    {
        ResolveFontAsset();

        if (koreanFontAsset == null)
        {
            Debug.LogError(
                $"[TMPKoreanFontBootstrap] '{ExpectedFontAssetName}'를 찾지 못했습니다. " +
                "Inspector에서 Font Asset을 직접 지정하거나 Editor 복구 도구를 실행하세요.",
                this);
            return;
        }

        RegisterGlobalFallback();

        TMP_Text[] loadedTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        string prewarmCharacters = BuildPrewarmCharacters(loadedTexts);

        int fontInstanceId = koreanFontAsset.GetInstanceID();

        if (koreanFontAsset.atlasPopulationMode == TMPro.AtlasPopulationMode.Dynamic)
        {
            koreanFontAsset.isMultiAtlasTexturesEnabled = true;

            if (PrewarmedFontAssets.Add(fontInstanceId))
            {
                string charactersToAdd =
                    BuildCharactersMissingFromAtlas(prewarmCharacters);

                if (!string.IsNullOrEmpty(charactersToAdd) &&
                    !koreanFontAsset.TryAddCharacters(
                        charactersToAdd,
                        out string missingCharacters,
                        true) &&
                    !string.IsNullOrEmpty(missingCharacters) &&
                    ReportedPrewarmFailures.Add(fontInstanceId))
                {
                    Debug.LogWarning(
                        "[TMPKoreanFontBootstrap] 현재 Atlas와 Source Font 모두에서 찾지 못한 글자가 있습니다.\n" +
                        "실제 누락 문자: " + missingCharacters,
                        koreanFontAsset);
                }
            }
        }
        else if (ReportedPrewarmFailures.Add(fontInstanceId))
        {
            Debug.LogWarning(
                $"[TMPKoreanFontBootstrap] '{koreanFontAsset.name}'가 Static입니다. " +
                "Dynamic/Multi Atlas Font Asset으로 다시 생성하거나 설정을 변경하세요.",
                koreanFontAsset);
        }

        if (assignToLoadedNotoAndLiberationTexts)
            RepairLoadedTexts(loadedTexts);
    }

    private void ResolveFontAsset()
    {
        if (koreanFontAsset != null)
            return;

        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

        foreach (TMP_FontAsset font in loadedFonts)
        {
            if (font != null && font.name == ExpectedFontAssetName)
            {
                koreanFontAsset = font;
                return;
            }
        }
    }

    private void RegisterGlobalFallback()
    {
        if (!addAsGlobalFallback || koreanFontAsset == null)
            return;

        List<TMP_FontAsset> fallbacks = TMP_Settings.fallbackFontAssets;

        if (fallbacks != null && !fallbacks.Contains(koreanFontAsset))
            fallbacks.Insert(0, koreanFontAsset);
    }

    private string BuildPrewarmCharacters(TMP_Text[] loadedTexts)
    {
        if (!prewarmLoadedSceneCharacters)
            return requiredCharacters ?? string.Empty;

        HashSet<char> characters = new();
        AddCharacters(characters, requiredCharacters);

        foreach (TMP_Text text in loadedTexts)
        {
            if (text == null || !text.gameObject.scene.IsValid())
                continue;

            AddCharacters(characters, text.text);
        }

        StringBuilder result = new(characters.Count);

        foreach (char character in characters)
            result.Append(character);

        return result.ToString();
    }

    private string BuildCharactersMissingFromAtlas(string value)
    {
        if (string.IsNullOrEmpty(value) || koreanFontAsset == null)
            return string.Empty;

        StringBuilder missing = new();

        foreach (char character in value)
        {
            if (char.IsControl(character))
                continue;

            // 이미 현재 Atlas에 존재하는 글자는 TryAddCharacters에 다시 넘기지 않는다.
            // 기존 구현은 존재하는 글자까지 전부 재추가해 거대한 실패 목록을 만들 수 있었다.
            if (!koreanFontAsset.HasCharacter(character))
                missing.Append(character);
        }

        return missing.ToString();
    }

    private static void AddCharacters(HashSet<char> destination, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        foreach (char character in value)
        {
            if (!char.IsControl(character))
                destination.Add(character);
        }
    }

    private void RepairLoadedTexts(TMP_Text[] loadedTexts)
    {
        int changed = 0;

        foreach (TMP_Text text in loadedTexts)
        {
            if (text == null || !text.gameObject.scene.IsValid())
                continue;

            TMP_FontAsset current = text.font;
            string currentName = current != null ? current.name : string.Empty;

            bool shouldAssign =
                current == null ||
                current == koreanFontAsset ||
                currentName.Contains("NotoSansKR") ||
                currentName.Contains("LiberationSans");

            if (!shouldAssign)
                continue;

            if (text.font != koreanFontAsset)
            {
                text.font = koreanFontAsset;
                changed++;
            }

            if (koreanFontAsset.material != null && text.fontSharedMaterial != koreanFontAsset.material)
                text.fontSharedMaterial = koreanFontAsset.material;
        }

        if (changed > 0)
        {
            Debug.Log(
                $"[TMPKoreanFontBootstrap] 로드된 TMP 텍스트 {changed}개의 폰트를 " +
                $"'{koreanFontAsset.name}'로 정리했습니다.",
                this);
        }
    }
}