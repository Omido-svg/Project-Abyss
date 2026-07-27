using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Editor 또는 Development Build에서 F12로 여는 런타임 RNG 디버그 패널.
///
/// 선택한 캐릭터의 CharacterRandomDebugOverride를 Play Mode 중 즉시 수정하고,
/// 샘플 굴림 분포를 실제 전투 RNG와 분리해서 확인할 수 있다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleRandomDebugController :
    MonoBehaviour
{
    private const KeyCode ToggleKey =
        KeyCode.F12;

    private const float WindowMargin = 8f;
    private const float TitleBarHeight = 28f;

    [SerializeField]
    private BattleManager battleManager;

    [SerializeField]
    private bool showOnStart;

    [SerializeField, Min(0.1f)]
    private float participantRefreshInterval =
        0.5f;

    private readonly List<Character> characters =
        new List<Character>();

    private Rect windowRect =
        new Rect(
            18f,
            90f,
            430f,
            760f);

    private Vector2 scroll;
    private int selectedCharacterIndex;
    private int selectedSkillIndex;
    private bool isVisible;
    private float nextParticipantRefresh;
    private string previewText =
        "Preview 결과 없음";

    private string sampleText =
        "100회 샘플 결과 없음";

    private GUIStyle wrapLabelStyle;
    private GUIStyle titleStyle;

    private void Awake()
    {
        ResolveBattleManager();
        isVisible = showOnStart;
        RefreshParticipants(force: true);
    }

    private void OnEnable()
    {
        ResolveBattleManager();
        RefreshParticipants(force: true);
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (WasTogglePressed())
        {
            isVisible = !isVisible;
        }

        RefreshParticipants(force: false);
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (!isVisible)
            return;

        EnsureStyles();

        NormalizeWindowSize();
        ClampWindowToScreen();

        windowRect =
            GUI.Window(
                127047,
                windowRect,
                DrawWindow,
                "Random Resolver Debug  ·  F12");

        // GUI.DragWindow가 현재 프레임에 위치를 바꾼 뒤 바로 보정한다.
        // 상단 제목 바가 화면 위로 빠져 다시 잡을 수 없는 상태를 방지한다.
        ClampWindowToScreen();
    }

    private void DrawWindow(
        int id)
    {
        scroll =
            GUILayout.BeginScrollView(
                scroll);

        ResolveBattleManager();
        RefreshParticipants(force: false);

        if (battleManager == null)
        {
            GUILayout.Label(
                "BattleManager를 찾지 못했습니다.",
                wrapLabelStyle);

            GUILayout.EndScrollView();
            GUI.DragWindow();
            return;
        }

        if (characters.Count == 0)
        {
            GUILayout.Label(
                "현재 BattleContext에 캐릭터가 없습니다.",
                wrapLabelStyle);

            if (GUILayout.Button("참가자 다시 검색"))
                RefreshParticipants(force: true);

            GUILayout.EndScrollView();
            GUI.DragWindow();
            return;
        }

        DrawCharacterSelection();

        Character selected =
            GetSelectedCharacter();

        CharacterRandomDebugOverride profile =
            EnsureProfile(selected);

        if (profile == null)
        {
            GUILayout.Label(
                "CharacterRandomDebugOverride를 생성하지 못했습니다.",
                wrapLabelStyle);

            GUILayout.EndScrollView();
            GUI.DragWindow();
            return;
        }

        DrawMasterSettings(
            selected,
            profile);

        DrawResolverSettings(
            profile);

        DrawSkillAndPreview(
            selected,
            profile);

        GUILayout.Space(8f);

        GUILayout.Label(
            "실전 강제 ON 상태에서는 선택한 Resolver와 고정 패턴이 " +
            "현재 캐릭터의 실제 RollResult를 교체하므로 합 연출도 반드시 해당 결과를 사용합니다. " +
            "예: 친치로 + 아라시 선택 시 실제 눈은 N·N·N으로 고정됩니다. " +
            "1회 Preview와 100회 Sample은 전투 ActionSlot과 UnityEngine.Random 상태를 변경하지 않습니다.",
            wrapLabelStyle);

        GUILayout.EndScrollView();
        GUI.DragWindow(
            new Rect(
                0f,
                0f,
                10000f,
                28f));
    }

    private void DrawCharacterSelection()
    {
        GUILayout.Label(
            "캐릭터",
            titleStyle);

        string[] labels =
            characters
                .Select(
                    character =>
                        GetCharacterName(character))
                .ToArray();

        selectedCharacterIndex =
            GUILayout.SelectionGrid(
                Mathf.Clamp(
                    selectedCharacterIndex,
                    0,
                    labels.Length - 1),
                labels,
                2);

    }

    private void DrawMasterSettings(
        Character selected,
        CharacterRandomDebugOverride profile)
    {
        GUILayout.Space(6f);

        profile.OverrideEnabled =
            GUILayout.Toggle(
                profile.OverrideEnabled,
                "실전 굴림·합 연출에 Override 강제");

        DebugRandomResolverMode previousMode =
            profile.Mode;

        int selectedMode =
            GUILayout.Toolbar(
                (int)profile.Mode,
                new[]
                {
                    "원본",
                    "주사위",
                    "코인",
                    "슬롯",
                    "친치로"
                });

        profile.Mode =
            (DebugRandomResolverMode)
            Mathf.Clamp(
                selectedMode,
                0,
                4);

        if (profile.Mode != previousMode)
        {
            if (profile.Mode ==
                DebugRandomResolverMode.Original)
            {
                profile.OverrideEnabled = false;
            }
            else
            {
                // Resolver 탭을 고르는 순간 실제 다음 굴림에 적용되도록 한다.
                // 디버깅 중 Override 체크를 빠뜨려 연출을 못 보는 일을 방지한다.
                profile.OverrideEnabled = true;
                profile.ResetSequence();

                previewText =
                    $"{profile.Mode} 실전 Override ON · " +
                    "다음 실제 굴림과 합 연출에 강제 적용됩니다.";
            }
        }

        bool isForced =
            profile.OverrideEnabled &&
            profile.Mode !=
            DebugRandomResolverMode.Original;

        GUILayout.Label(
            isForced
                ? "● 실전 강제 ON · 선택 결과가 실제 RollResult와 합 연출에 사용됩니다."
                : "○ 실전 강제 OFF · 기존 SkillDefinition RNG를 사용합니다.",
            wrapLabelStyle);

        profile.UseSkillBasePower =
            GUILayout.Toggle(
                profile.UseSkillBasePower,
                "스킬 기본위력 사용");

        if (!profile.UseSkillBasePower)
        {
            profile.OverrideBasePower =
                DrawIntField(
                    "Override 기본위력",
                    profile.OverrideBasePower);
        }

        profile.DeterministicSequence =
            GUILayout.Toggle(
                profile.DeterministicSequence,
                "독립 Seed로 결과 재현");

        profile.Seed =
            DrawIntField(
                "Seed",
                profile.Seed);

        profile.LogActualRolls =
            GUILayout.Toggle(
                profile.LogActualRolls,
                "실제 전투 굴림 Console 로그");

        if (GUILayout.Button(
                "현재 설정으로 실전 연출 강제 ON"))
        {
            if (profile.Mode ==
                DebugRandomResolverMode.Original)
            {
                profile.Mode =
                    DebugRandomResolverMode.Dice;
            }

            profile.OverrideEnabled = true;
            profile.ResetSequence();

            previewText =
                $"{profile.Mode} 실전 Override ON · " +
                "현재 설정이 실제 합 굴림에 반복 적용됩니다.";
        }

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Sequence Reset"))
        {
            profile.ResetSequence();
            previewText =
                "Sequence를 0으로 초기화했습니다.";
        }

        if (GUILayout.Button("Override 해제"))
        {
            profile.OverrideEnabled = false;
            profile.Mode =
                DebugRandomResolverMode.Original;

            previewText =
                "실전 Override를 해제했습니다.";
        }

        GUILayout.EndHorizontal();

        GUILayout.Label(
            $"선택: {GetCharacterName(selected)}\n" +
            profile.GetSummary(),
            wrapLabelStyle);
    }

    private void DrawResolverSettings(
        CharacterRandomDebugOverride profile)
    {
        GUILayout.Space(8f);

        switch (profile.Mode)
        {
            case DebugRandomResolverMode.Dice:
                DrawDiceSettings(profile);
                break;

            case DebugRandomResolverMode.Coin:
                DrawCoinSettings(profile);
                break;

            case DebugRandomResolverMode.Slot:
                DrawSlotSettings(profile);
                break;

            case DebugRandomResolverMode.Chinchiro:
                DrawChinchiroSettings(profile);
                break;

            default:
                GUILayout.Label(
                    "원본: SkillDefinition / SkillRollData의 Resolver를 그대로 사용합니다.",
                    wrapLabelStyle);
                break;
        }

        profile.Sanitize();
    }

    private void DrawDiceSettings(
        CharacterRandomDebugOverride profile)
    {
        GUILayout.Label(
            "DICE",
            titleStyle);

        profile.DiceCount =
            DrawIntSlider(
                "주사위 개수",
                profile.DiceCount,
                1,
                8);

        profile.DiceMin =
            DrawIntField(
                "최소 눈",
                profile.DiceMin);

        profile.DiceMax =
            DrawIntField(
                "최대 눈",
                profile.DiceMax);

        profile.ForceDiceValue =
            GUILayout.Toggle(
                profile.ForceDiceValue,
                "눈 고정");

        if (profile.ForceDiceValue)
        {
            profile.ForcedDiceValue =
                DrawIntField(
                    "고정 눈",
                    profile.ForcedDiceValue);
        }
    }

    private void DrawCoinSettings(
        CharacterRandomDebugOverride profile)
    {
        GUILayout.Label(
            "COIN",
            titleStyle);

        profile.CoinCount =
            DrawIntSlider(
                "코인 개수",
                profile.CoinCount,
                1,
                8);

        profile.CoinFrontChance =
            DrawFloatSlider(
                "앞면 확률",
                profile.CoinFrontChance,
                0f,
                1f);

        profile.CoinFrontValue =
            DrawIntField(
                "앞면 가산값",
                profile.CoinFrontValue);

        profile.CoinBackValue =
            DrawIntField(
                "뒷면 가산값",
                profile.CoinBackValue);

        profile.CoinFrontIsCritical =
            GUILayout.Toggle(
                profile.CoinFrontIsCritical,
                "앞면이 Critical");

        profile.CoinPattern =
            (DebugCoinPattern)
            GUILayout.Toolbar(
                (int)profile.CoinPattern,
                new[]
                {
                    "랜덤",
                    "전부 앞",
                    "전부 뒤",
                    "앞부터 교대",
                    "뒤부터 교대",
                    "Custom"
                });

        if (profile.CoinPattern ==
            DebugCoinPattern.Custom)
        {
            GUILayout.Label(
                "Custom 패턴 · F/B, 1/0, 앞/뒤");

            profile.CustomCoinPattern =
                GUILayout.TextField(
                    profile.CustomCoinPattern ??
                    string.Empty);
        }
    }

    private void DrawSlotSettings(
        CharacterRandomDebugOverride profile)
    {
        GUILayout.Label(
            "SLOT",
            titleStyle);

        profile.SlotMinimum =
            DrawIntSlider(
                "릴 최소",
                profile.SlotMinimum,
                1,
                9);

        profile.SlotMaximum =
            DrawIntSlider(
                "릴 최대",
                profile.SlotMaximum,
                profile.SlotMinimum,
                9);

        profile.ForceSlotResult =
            GUILayout.Toggle(
                profile.ForceSlotResult,
                "릴 결과 고정");

        if (profile.ForceSlotResult)
        {
            profile.ForcedSlotA =
                DrawIntSlider(
                    "왼쪽 릴",
                    profile.ForcedSlotA,
                    profile.SlotMinimum,
                    profile.SlotMaximum);

            profile.ForcedSlotB =
                DrawIntSlider(
                    "오른쪽 릴",
                    profile.ForcedSlotB,
                    profile.SlotMinimum,
                    profile.SlotMaximum);
        }
    }

    private void DrawChinchiroSettings(
        CharacterRandomDebugOverride profile)
    {
        GUILayout.Label(
            "CHINCHIRO",
            titleStyle);

        DebugChinchiroPattern previousPattern =
            profile.ChinchiroPattern;

        profile.ChinchiroPattern =
            (DebugChinchiroPattern)
            GUILayout.Toolbar(
                (int)profile.ChinchiroPattern,
                new[]
                {
                    "랜덤",
                    "NONE",
                    "아라시",
                    "시고로",
                    "목",
                    "무역",
                    "히후미"
                });

        if (profile.ChinchiroPattern !=
            previousPattern)
        {
            profile.Mode =
                DebugRandomResolverMode.Chinchiro;

            profile.OverrideEnabled = true;
            profile.ResetSequence();

            previewText =
                profile.ChinchiroPattern ==
                DebugChinchiroPattern.Random
                    ? "친치로 Random 실전 Override ON"
                    : $"친치로 {profile.ChinchiroPattern} 실전 강제 예약 · " +
                      "다음 실제 굴림부터 해당 조합만 나옵니다.";
        }

        GUILayout.Label(
            profile.ChinchiroPattern ==
            DebugChinchiroPattern.Random
                ? "Random은 실제 확률로 굴립니다."
                : "고정 조합 선택 상태: Override가 자동으로 켜지며, " +
                  "해제할 때까지 모든 실제 친치로 굴림과 합 연출에 반복 적용됩니다.",
            wrapLabelStyle);

        if (profile.ChinchiroPattern ==
            DebugChinchiroPattern.Arashi)
        {
            profile.ChinchiroTripleFace =
                DrawIntSlider(
                    "아라시 눈",
                    profile.ChinchiroTripleFace,
                    1,
                    6);
        }

        if (profile.ChinchiroPattern ==
            DebugChinchiroPattern.Moku)
        {
            profile.ChinchiroMokuPairFace =
                DrawIntSlider(
                    "목 짝 눈",
                    profile.ChinchiroMokuPairFace,
                    1,
                    6);

            profile.ChinchiroMokuSingleFace =
                DrawIntSlider(
                    "나머지 눈",
                    profile.ChinchiroMokuSingleFace,
                    1,
                    6);

            profile.UsePairFaceAsMokuPower =
                GUILayout.Toggle(
                    profile.UsePairFaceAsMokuPower,
                    "짝 눈을 목 위력으로 사용");
        }

        profile.ChinchiroArashiPower =
            DrawIntField(
                "아라시 위력",
                profile.ChinchiroArashiPower);

        profile.ChinchiroShigoroPower =
            DrawIntField(
                "시고로 위력",
                profile.ChinchiroShigoroPower);

        profile.ChinchiroMokuPower =
            DrawIntField(
                "목 위력",
                profile.ChinchiroMokuPower);

        profile.ChinchiroBlankPower =
            DrawIntField(
                "무역 위력",
                profile.ChinchiroBlankPower);

        profile.ChinchiroHifumiPower =
            DrawIntField(
                "히후미 위력",
                profile.ChinchiroHifumiPower);

        profile.ChinchiroHifumiSelfDamage =
            DrawIntField(
                "히후미 자해",
                profile.ChinchiroHifumiSelfDamage);
    }

    private void DrawSkillAndPreview(
        Character character,
        CharacterRandomDebugOverride profile)
    {
        GUILayout.Space(8f);
        GUILayout.Label(
            "Preview / Sampling",
            titleStyle);

        IReadOnlyList<Skill> skills =
            character?.RuntimeSkills;

        if (skills == null ||
            skills.Count == 0)
        {
            GUILayout.Label(
                "초기화된 Runtime Skill이 없습니다.",
                wrapLabelStyle);
            return;
        }

        string[] skillNames =
            skills
                .Select(
                    skill =>
                        skill?.SkillName ??
                        "NULL")
                .ToArray();

        selectedSkillIndex =
            GUILayout.SelectionGrid(
                Mathf.Clamp(
                    selectedSkillIndex,
                    0,
                    skillNames.Length - 1),
                skillNames,
                2);

        Skill selectedSkill =
            skills[
                Mathf.Clamp(
                    selectedSkillIndex,
                    0,
                    skills.Count - 1)];

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("1회 Preview"))
        {
            RollResult preview =
                profile.CreatePreview(
                    selectedSkill,
                    0,
                    0);

            previewText =
                preview != null
                    ? preview.GetClashDetailDisplayText()
                    : "Preview 생성 실패";
        }

        if (GUILayout.Button("100회 Sample"))
        {
            sampleText =
                BuildSampleSummary(
                    profile,
                    selectedSkill,
                    100);
        }

        GUILayout.EndHorizontal();

        GUILayout.Label(
            previewText,
            wrapLabelStyle);

        GUILayout.TextArea(
            sampleText,
            GUILayout.MinHeight(100f));
    }

    private string BuildSampleSummary(
        CharacterRandomDebugOverride profile,
        Skill skill,
        int sampleCount)
    {
        Dictionary<string, int> counts =
            new Dictionary<string, int>();

        int totalPower = 0;
        int generated = 0;

        for (int index = 0;
             index < sampleCount;
             index++)
        {
            RollResult result =
                profile.CreatePreview(
                    skill,
                    0,
                    index);

            if (result == null)
                continue;

            string key =
                BuildSampleKey(
                    result);

            counts.TryGetValue(
                key,
                out int count);

            counts[key] = count + 1;
            totalPower += result.FinalPower;
            generated++;
        }

        if (generated == 0)
            return "Sample 생성 실패";

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            $"N={generated}, 평균 최종위력={(float)totalPower / generated:0.00}");

        foreach (KeyValuePair<string, int> pair
                 in counts
                     .OrderByDescending(item => item.Value)
                     .ThenBy(item => item.Key)
                     .Take(24))
        {
            builder.AppendLine(
                $"{pair.Key}: {pair.Value}회 " +
                $"({pair.Value * 100f / generated:0.0}%)");
        }

        return builder.ToString();
    }

    private static string BuildSampleKey(
        RollResult result)
    {
        if (result == null)
            return "NULL";

        switch (result.ResolverType)
        {
            case SkillResolverType.Coin:
            {
                string faces =
                    result.CoinFaces == null
                        ? "-"
                        : string.Concat(
                            result.CoinFaces.Select(
                                face =>
                                    face
                                        ? "F"
                                        : "B"));

                return
                    $"{faces} = {result.FinalPower}";
            }

            case SkillResolverType.Slot:
                return
                    $"{result.SlotA}×{result.SlotB} = " +
                    $"{result.FinalPower}";

            case SkillResolverType.Chinchiro:
                return
                    $"{result.ChinchiroCombination} = " +
                    $"{result.FinalPower}";

            default:
                return
                    $"{string.Join("+", result.DiceValues)} = " +
                    $"{result.FinalPower}";
        }
    }

    private void RefreshParticipants(
        bool force)
    {
        if (!force &&
            Time.unscaledTime <
            nextParticipantRefresh)
        {
            return;
        }

        nextParticipantRefresh =
            Time.unscaledTime +
            Mathf.Max(
                0.1f,
                participantRefreshInterval);

        ResolveBattleManager();

        List<Character> next =
            battleManager?.BattleContext?.AllCharacters ??
            new List<Character>();

        characters.Clear();

        foreach (Character character in next)
        {
            if (character == null ||
                characters.Contains(character))
            {
                continue;
            }

            characters.Add(character);
            EnsureProfile(character);
        }

        selectedCharacterIndex =
            Mathf.Clamp(
                selectedCharacterIndex,
                0,
                Mathf.Max(
                    0,
                    characters.Count - 1));
    }

    private CharacterRandomDebugOverride EnsureProfile(
        Character character)
    {
        if (character == null)
            return null;

        CharacterRandomDebugOverride profile =
            character.GetComponent<
                CharacterRandomDebugOverride>();

        if (profile == null)
        {
            profile =
                character.gameObject.AddComponent<
                    CharacterRandomDebugOverride>();
        }

        return profile;
    }

    private Character GetSelectedCharacter()
    {
        if (characters.Count == 0)
            return null;

        selectedCharacterIndex =
            Mathf.Clamp(
                selectedCharacterIndex,
                0,
                characters.Count - 1);

        return
            characters[
                selectedCharacterIndex];
    }

    private void ResolveBattleManager()
    {
        if (battleManager != null)
            return;

        battleManager =
            FindFirstObjectByType<
                BattleManager>(
                    FindObjectsInactive.Include);
    }

    private bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            Keyboard.current.f12Key.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return
            Input.GetKeyDown(
                ToggleKey);
#else
        return false;
#endif
    }

    private void NormalizeWindowSize()
    {
        float availableWidth =
            Mathf.Max(
                220f,
                Screen.width -
                WindowMargin * 2f);

        float availableHeight =
            Mathf.Max(
                180f,
                Screen.height -
                WindowMargin * 2f);

        float minimumWidth =
            Mathf.Min(
                380f,
                availableWidth);

        float minimumHeight =
            Mathf.Min(
                520f,
                availableHeight);

        windowRect.width =
            Mathf.Clamp(
                windowRect.width,
                minimumWidth,
                availableWidth);

        windowRect.height =
            Mathf.Clamp(
                windowRect.height,
                minimumHeight,
                availableHeight);
    }

    private void ClampWindowToScreen()
    {
        float maximumX =
            Mathf.Max(
                WindowMargin,
                Screen.width -
                windowRect.width -
                WindowMargin);

        // 전체 창은 세로 화면 안에 맞추되, 해상도가 급변하더라도
        // 최소한 제목 바가 항상 잡을 수 있는 위치에 남도록 한다.
        float maximumY =
            Mathf.Max(
                WindowMargin,
                Screen.height -
                Mathf.Min(
                    windowRect.height,
                    TitleBarHeight) -
                WindowMargin);

        windowRect.x =
            Mathf.Clamp(
                windowRect.x,
                WindowMargin,
                maximumX);

        windowRect.y =
            Mathf.Clamp(
                windowRect.y,
                WindowMargin,
                maximumY);
    }

    private void EnsureStyles()
    {
        wrapLabelStyle ??=
            new GUIStyle(
                GUI.skin.label)
            {
                wordWrap = true,
                richText = true
            };

        titleStyle ??=
            new GUIStyle(
                GUI.skin.label)
            {
                fontStyle =
                    FontStyle.Bold,

                fontSize = 13
            };
    }

    private static int DrawIntField(
        string label,
        int value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(
            label,
            GUILayout.Width(160f));

        string text =
            GUILayout.TextField(
                value.ToString());

        GUILayout.EndHorizontal();

        return
            int.TryParse(
                text,
                out int parsed)
                ? parsed
                : value;
    }

    private static int DrawIntSlider(
        string label,
        int value,
        int minimum,
        int maximum)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(
            $"{label}: {value}",
            GUILayout.Width(160f));

        int result =
            Mathf.RoundToInt(
                GUILayout.HorizontalSlider(
                    value,
                    minimum,
                    maximum));

        GUILayout.EndHorizontal();
        return result;
    }

    private static float DrawFloatSlider(
        string label,
        float value,
        float minimum,
        float maximum)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(
            $"{label}: {value:0.00}",
            GUILayout.Width(160f));

        float result =
            GUILayout.HorizontalSlider(
                value,
                minimum,
                maximum);

        GUILayout.EndHorizontal();
        return result;
    }

    private static string GetCharacterName(
        Character character)
    {
        return
            character?.Data?.CharacterName ??
            character?.name ??
            "NULL";
    }
#endif
}
