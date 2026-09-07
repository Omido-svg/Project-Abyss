using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattlePortraitButtonView : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Image frameImage;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text initialsText;
    [SerializeField] private TMP_Text partText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text skillText;
    [SerializeField] private TMP_Text rollPatternText;
    [SerializeField] private Button detailButton;
    [SerializeField] private RectTransform rollPatternContainer;

    private readonly List<GameObject> generatedRollIndicators =
        new();

    private string lastRollPatternSignature =
        string.Empty;

    [Header("Roll Pattern")]
    [SerializeField] private Color attackRollColor =
        new(0.94f, 0.27f, 0.27f, 1f);

    [SerializeField] private Color defenseRollColor =
        new(0.38f, 0.65f, 0.98f, 1f);

    [SerializeField, Range(1, 8)]
    private int maximumVisibleRolls = 8;

    [SerializeField, Range(1, 3)]
    private int maximumVisibleSkillPatterns = 2;

    [Header("Colors")]
    [SerializeField] private Color idleColor =
        new(0.12f, 0.16f, 0.22f, 0.98f);

    [SerializeField] private Color ownerSelectedColor =
        new(0.15f, 0.55f, 1f, 1f);

    [SerializeField] private Color targetSelectedColor =
        new(1f, 0.22f, 0.18f, 1f);

    [SerializeField] private Color weakenedColor =
        new(1f, 0.72f, 0.1f, 1f);

    [SerializeField] private Color brokenColor =
        new(0.45f, 0.08f, 0.08f, 1f);

    private BodyPartButton sourceButton;
    private BattleCharacterDetailPanelUI detailPanel;
    private Character displayedOwner;
    private bool isReleasing;

    public void Configure(
        Image frame,
        Image portrait,
        TMP_Text initials,
        TMP_Text part,
        TMP_Text hp,
        TMP_Text speed,
        TMP_Text skill,
        Button inspectButton)
    {
        frameImage = frame;
        portraitImage = portrait;
        initialsText = initials;
        partText = part;
        hpText = hp;
        speedText = speed;
        skillText = skill;
        detailButton = inspectButton;
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        frameImage ??= GetComponent<Image>();

        if (detailPanel == null)
        {
            detailPanel =
                FindFirstObjectByType<BattleCharacterDetailPanelUI>(
                    FindObjectsInactive.Include);
        }

        ConfigureText(
            initialsText,
            18f,
            TextAlignmentOptions.Center,
            false,
            richText: false);

        ConfigureText(
            partText,
            20f,
            TextAlignmentOptions.Left,
            false,
            richText: false);

        ConfigureText(
            hpText,
            16f,
            TextAlignmentOptions.Left,
            false,
            richText: false);

        ConfigureText(
            speedText,
            16f,
            TextAlignmentOptions.Left,
            false,
            richText: false);

        ConfigureText(
            skillText,
            12f,
            TextAlignmentOptions.Left,
            true,
            richText: false);

        EnsureRollPatternText();
        EnsureRollPatternContainer();
        ConfigureRollPatternLayout();

        // 전체 카드 Button은 BodyPartButton이 전투 선택과 우클릭 취소를 담당한다.
        // 자동으로 상세 패널 Listener를 추가하면 같은 입력에서 상세 화면이 열리므로
        // 과거 Listener만 제거하고 별도 Inspect 버튼이 없는 현재 구조에서는 바인딩하지 않는다.
        if (detailButton != null)
            detailButton.onClick.RemoveListener(OpenDetails);
    }

    public void Apply(
        BodyPartButton source,
        BodyPartButtonViewModel viewModel)
    {
        if (isReleasing)
            return;

        sourceButton = source;
        ResolveReferences();

        Character owner =
            source != null
                ? source.Owner
                : null;

        displayedOwner = owner;

        BodyPart part =
            source != null
                ? source.BodyPart
                : null;

        Sprite portrait =
            owner?.Data?.Portrait;

        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
            portraitImage.preserveAspect = true;
        }

        if (initialsText != null)
        {
            bool shouldShowInitial =
                portrait == null;

            if (initialsText.gameObject.activeSelf != shouldShowInitial)
                initialsText.gameObject.SetActive(shouldShowInitial);

            string fallbackName =
                BuildFallbackName(owner);

            initialsText.text =
                fallbackName;

            initialsText.fontSize =
                GetFallbackNameFontSize(fallbackName);

            initialsText.fontStyle =
                FontStyles.Bold;
        }

        if (partText != null)
        {
            partText.text =
                part == null
                    ? "본체"
                    : GetPartName(part.Type);
        }

        if (hpText != null)
        {
            hpText.text =
                ToPlainSingleLine(
                    viewModel.HpText,
                    "HP -");
        }

        if (speedText != null)
        {
            speedText.text =
                ToPlainSingleLine(
                    viewModel.SpeedText,
                    "속도 -");
        }

        if (skillText != null)
        {
            string skill =
                string.IsNullOrWhiteSpace(
                    viewModel.SkillText)
                    ? "미지정"
                    : CompactSkillName(
                        viewModel.SkillText);

            skillText.text = skill;
        }

        ApplyRollPattern(
            viewModel.AssignedSkills);

        if (frameImage != null)
        {
            frameImage.color =
                viewModel.IsTargetSelected
                    ? targetSelectedColor
                    : viewModel.IsOwnerSelected
                        ? ownerSelectedColor
                        : viewModel.IsBroken
                            ? brokenColor
                            : viewModel.IsWeakened
                                ? weakenedColor
                                : idleColor;
        }

        if (detailButton != null)
            detailButton.interactable = owner != null;
    }

    public void PrepareForDestruction()
    {
        if (isReleasing)
            return;

        isReleasing = true;
        sourceButton = null;
        displayedOwner = null;
        ClearGeneratedRollIndicators();
        lastRollPatternSignature = string.Empty;

        if (detailButton != null)
            detailButton.onClick.RemoveListener(OpenDetails);
    }

    private void OnDestroy()
    {
        PrepareForDestruction();
    }

    public void OpenDetails()
    {
        // 전체 결투 현황 카드는 전투 선택 전용이다.
        // 상세 보기는 3D 캐릭터 Pointer Router가 담당한다.
        // 과거 Scene/Prefab의 persistent UnityEvent가 남아 있어도
        // 이 메서드는 상세 패널을 열지 않는다.
    }


    private void EnsureRollPatternText()
    {
        if (rollPatternText != null ||
            isReleasing)
        {
            return;
        }

        Transform existing =
            transform.Find("RollPattern");

        if (existing != null)
        {
            rollPatternText =
                existing.GetComponent<TMP_Text>();
        }

        if (rollPatternText != null)
            return;

        GameObject textObject =
            new("RollPattern", typeof(RectTransform));

        textObject.transform.SetParent(
            transform,
            false);

        TextMeshProUGUI generatedText =
            textObject.AddComponent<TextMeshProUGUI>();

        generatedText.raycastTarget = false;
        generatedText.text = string.Empty;

        if (skillText != null &&
            skillText.font != null)
        {
            generatedText.font =
                skillText.font;
        }

        rollPatternText = generatedText;
    }

    private void ConfigureRollPatternLayout()
    {
        if (skillText != null &&
            skillText.rectTransform != null)
        {
            RectTransform skillRect =
                skillText.rectTransform;

            skillRect.anchorMin =
                new Vector2(0.055f, 0.025f);

            skillRect.anchorMax =
                new Vector2(0.79f, 0.145f);

            skillRect.offsetMin =
                Vector2.zero;

            skillRect.offsetMax =
                Vector2.zero;
        }

        if (rollPatternText != null)
        {
            rollPatternText.text =
                string.Empty;

            if (rollPatternText.gameObject.activeSelf)
            {
                rollPatternText.gameObject.SetActive(
                    false);
            }
        }

        if (rollPatternContainer == null)
            return;

        rollPatternContainer.anchorMin =
            new Vector2(0.055f, 0.14f);

        rollPatternContainer.anchorMax =
            new Vector2(0.79f, 0.255f);

        rollPatternContainer.offsetMin =
            Vector2.zero;

        rollPatternContainer.offsetMax =
            Vector2.zero;
    }


    private void ApplyRollPattern(
        IReadOnlyList<Skill> skills)
    {
        EnsureRollPatternContainer();

        if (rollPatternContainer == null)
            return;

        string signature =
            BuildRollPatternSignature(
                skills);

        if (string.Equals(
                signature,
                lastRollPatternSignature,
                System.StringComparison.Ordinal))
        {
            return;
        }

        lastRollPatternSignature =
            signature;

        ClearGeneratedRollIndicators();

        if (string.IsNullOrEmpty(
                signature))
        {
            rollPatternContainer.gameObject.SetActive(
                false);
            return;
        }

        int visibleLimit =
            Mathf.Max(
                1,
                maximumVisibleSkillPatterns);

        int createdSkillCount =
            0;

        for (int skillIndex = 0;
             skills != null &&
             skillIndex < skills.Count &&
             createdSkillCount < visibleLimit;
             skillIndex++)
        {
            Skill skill =
                skills[skillIndex];

            if (!BattleSkillUiText
                    .ShouldShowClashRollPattern(
                        skill))
            {
                continue;
            }

            CreateSkillPatternGroup(
                skill,
                createdSkillCount);

            createdSkillCount++;
        }

        bool shouldShow =
            generatedRollIndicators.Count > 0;

        rollPatternContainer.gameObject.SetActive(
            shouldShow);
    }


    private void EnsureRollPatternContainer()
    {
        if (rollPatternContainer != null ||
            isReleasing)
        {
            return;
        }

        Transform existing =
            transform.Find(
                "RollPatternSquares");

        if (existing != null)
        {
            rollPatternContainer =
                existing as RectTransform;
        }

        if (rollPatternContainer == null)
        {
            GameObject root =
                new(
                    "RollPatternSquares",
                    typeof(RectTransform),
                    typeof(HorizontalLayoutGroup));

            root.transform.SetParent(
                transform,
                false);

            rollPatternContainer =
                root.GetComponent<RectTransform>();
        }

        HorizontalLayoutGroup layout =
            rollPatternContainer
                .GetComponent<HorizontalLayoutGroup>();

        if (layout == null)
        {
            layout =
                rollPatternContainer
                    .gameObject
                    .AddComponent<HorizontalLayoutGroup>();
        }

        layout.padding =
            new RectOffset(0, 0, 0, 0);

        layout.spacing =
            7f;

        layout.childAlignment =
            TextAnchor.MiddleLeft;

        layout.childControlWidth =
            true;

        layout.childControlHeight =
            true;

        layout.childForceExpandWidth =
            false;

        layout.childForceExpandHeight =
            false;

        rollPatternContainer.gameObject.SetActive(
            false);
    }

    private void CreateSkillPatternGroup(
        Skill skill,
        int skillIndex)
    {
        if (!BattleSkillUiText
                .ShouldShowClashRollPattern(
                    skill) ||
            rollPatternContainer == null)
        {
            return;
        }

        GameObject groupObject =
            new(
                $"SkillPattern_{skillIndex + 1}",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));

        groupObject.transform.SetParent(
            rollPatternContainer,
            false);

        generatedRollIndicators.Add(
            groupObject);

        HorizontalLayoutGroup groupLayout =
            groupObject.GetComponent<
                HorizontalLayoutGroup>();

        groupLayout.padding =
            new RectOffset(0, 0, 0, 0);

        groupLayout.spacing =
            2f;

        groupLayout.childAlignment =
            TextAnchor.MiddleLeft;

        groupLayout.childControlWidth =
            true;

        groupLayout.childControlHeight =
            true;

        groupLayout.childForceExpandWidth =
            false;

        groupLayout.childForceExpandHeight =
            false;

        SkillDefinition definition =
            skill.Definition;

        int sourceCount =
            definition?.Rolls != null &&
            definition.Rolls.Count > 0
                ? definition.Rolls.Count
                : skill.ExchangeRollCount;

        int visibleCount =
            Mathf.Min(
                Mathf.Max(
                    0,
                    sourceCount),
                Mathf.Max(
                    1,
                    maximumVisibleRolls));

        LayoutElement groupElement =
            groupObject.GetComponent<
                LayoutElement>();

        groupElement.preferredHeight =
            10f;

        groupElement.preferredWidth =
            visibleCount > 0
                ? visibleCount * 10f +
                  Mathf.Max(
                      0,
                      visibleCount - 1) * 2f
                : 0f;

        for (int rollIndex = 0;
             rollIndex < visibleCount;
             rollIndex++)
        {
            CombatRollType type =
                ResolveRollType(
                    skill,
                    rollIndex);

            CreateRollSquare(
                groupObject.transform,
                type,
                rollIndex);
        }
    }

    private void CreateRollSquare(
        Transform parent,
        CombatRollType type,
        int rollIndex)
    {
        GameObject squareObject =
            new(
                $"Roll_{rollIndex + 1}_{type}",
                typeof(RectTransform),
                typeof(Image),
                typeof(LayoutElement));

        squareObject.transform.SetParent(
            parent,
            false);

        Image image =
            squareObject.GetComponent<Image>();

        image.raycastTarget =
            false;

        image.color =
            type == CombatRollType.Stagger
                ? defenseRollColor
                : attackRollColor;

        LayoutElement element =
            squareObject.GetComponent<
                LayoutElement>();

        element.minWidth =
            9f;

        element.preferredWidth =
            9f;

        element.minHeight =
            9f;

        element.preferredHeight =
            9f;
    }

    private static CombatRollType ResolveRollType(
        Skill skill,
        int rollIndex)
    {
        SkillDefinition definition =
            skill?.Definition;

        if (definition?.Rolls != null &&
            rollIndex >= 0 &&
            rollIndex < definition.Rolls.Count &&
            definition.Rolls[rollIndex] != null)
        {
            return definition.Rolls[rollIndex].Type;
        }

        return CombatRollType.Attack;
    }

    private string BuildRollPatternSignature(
        IReadOnlyList<Skill> skills)
    {
        if (skills == null ||
            skills.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder =
            new();

        int visibleLimit =
            Mathf.Max(
                1,
                maximumVisibleSkillPatterns);

        int visibleSkillCount =
            0;

        for (int skillIndex = 0;
             skillIndex < skills.Count &&
             visibleSkillCount < visibleLimit;
             skillIndex++)
        {
            Skill skill =
                skills[skillIndex];

            if (!BattleSkillUiText
                    .ShouldShowClashRollPattern(
                        skill))
            {
                continue;
            }

            builder.Append(
                skill.GetHashCode());

            builder.Append(':');

            int count =
                skill.Definition?.Rolls != null &&
                skill.Definition.Rolls.Count > 0
                    ? skill.Definition.Rolls.Count
                    : skill.ExchangeRollCount;

            int visibleCount =
                Mathf.Min(
                    Mathf.Max(
                        0,
                        count),
                    Mathf.Max(
                        1,
                        maximumVisibleRolls));

            for (int rollIndex = 0;
                 rollIndex < visibleCount;
                 rollIndex++)
            {
                builder.Append(
                    (int)ResolveRollType(
                        skill,
                        rollIndex));
            }

            builder.Append('|');

            visibleSkillCount++;
        }

        return builder.ToString();
    }

    private void ClearGeneratedRollIndicators()
    {
        if (rollPatternContainer != null)
        {
            for (int index =
                     rollPatternContainer.childCount - 1;
                 index >= 0;
                 index--)
            {
                Transform child =
                    rollPatternContainer.GetChild(index);

                if (child == null)
                    continue;

                child.gameObject.SetActive(
                    false);

                Destroy(
                    child.gameObject);
            }
        }

        generatedRollIndicators.Clear();
    }

    private string BuildRollPatternText(
        IReadOnlyList<Skill> skills)
    {
        if (skills == null ||
            skills.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder =
            new();

        int visibleLimit =
            Mathf.Max(
                1,
                maximumVisibleSkillPatterns);

        int visibleSkillCount =
            0;

        int totalClashSkillCount =
            0;

        for (int skillIndex = 0;
             skillIndex < skills.Count;
             skillIndex++)
        {
            Skill skill =
                skills[skillIndex];

            if (!BattleSkillUiText
                    .ShouldShowClashRollPattern(
                        skill))
            {
                continue;
            }

            totalClashSkillCount++;

            if (visibleSkillCount >=
                visibleLimit)
            {
                continue;
            }

            if (builder.Length > 0)
                builder.Append("  ");

            if (totalClashSkillCount > 1)
            {
                builder.Append("<size=80%>#");
                builder.Append(
                    totalClashSkillCount);
                builder.Append("</size> ");
            }

            AppendRollSquares(
                builder,
                skill);

            visibleSkillCount++;
        }

        if (totalClashSkillCount >
            visibleSkillCount)
        {
            builder.Append("  +");
            builder.Append(
                totalClashSkillCount -
                visibleSkillCount);
        }

        return builder.ToString();
    }

    private void AppendRollSquares(
        StringBuilder builder,
        Skill skill)
    {
        if (builder == null ||
            !BattleSkillUiText
                .ShouldShowClashRollPattern(
                    skill))
        {
            return;
        }

        SkillDefinition definition =
            skill.Definition;

        int sourceCount =
            definition?.Rolls != null &&
            definition.Rolls.Count > 0
                ? definition.Rolls.Count
                : skill.ExchangeRollCount;

        int visibleCount =
            Mathf.Min(
                Mathf.Max(
                    0,
                    sourceCount),
                Mathf.Max(
                    1,
                    maximumVisibleRolls));

        for (int rollIndex = 0;
             rollIndex < visibleCount;
             rollIndex++)
        {
            CombatRollType type =
                definition?.Rolls != null &&
                rollIndex < definition.Rolls.Count &&
                definition.Rolls[rollIndex] != null
                    ? definition.Rolls[rollIndex].Type
                    : CombatRollType.Attack;

            Color color =
                type == CombatRollType.Stagger
                    ? defenseRollColor
                    : attackRollColor;

            builder.Append("<color=#");
            builder.Append(
                ColorUtility.ToHtmlStringRGB(
                    color));
            builder.Append(">■</color>");

            if (rollIndex <
                visibleCount - 1)
            {
                builder.Append(
                    '\u2009');
            }
        }

        if (sourceCount > visibleCount)
            builder.Append("…");
    }

    private static void ConfigureText(
        TMP_Text text,
        float size,
        TextAlignmentOptions alignment,
        bool ellipsis,
        bool richText)
    {
        if (text == null)
            return;

        text.richText = richText;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.alignment = alignment;

        text.overflowMode =
            ellipsis
                ? TextOverflowModes.Ellipsis
                : TextOverflowModes.Overflow;

        text.margin =
            Vector4.zero;
    }

    private static string NormalizeSingleLine(
        string value,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value
            .Replace("\\n", " ")
            .Replace("\n", " ")
            .Replace("<b>", string.Empty)
            .Replace("</b>", string.Empty)
            .Trim();
    }

    private static string ToPlainSingleLine(
        string value,
        string fallback)
    {
        return StripRichTextTags(
                NormalizeSingleLine(
                    value,
                    fallback))
            .Trim();
    }

    private static string CompactSkillName(
        string value)
    {
        string compact =
            StripRichTextTags(
                    NormalizeSingleLine(
                        value,
                        "미지정"))
                .Replace(
                    "(일반공격)",
                    string.Empty)
                .Replace(
                    "(일반 공격)",
                    string.Empty)
                .Replace(
                    "(결투)",
                    string.Empty)
                .Replace(
                    "(도사림)",
                    string.Empty)
                .Replace(
                    "(위세)",
                    string.Empty)
                .Trim();

        const int maxCharacters = 15;

        if (compact.Length > maxCharacters)
        {
            compact =
                compact.Substring(
                    0,
                    maxCharacters - 1) +
                "…";
        }

        return compact;
    }

    private static string StripRichTextTags(
        string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        StringBuilder builder =
            new(value.Length);

        bool insideTag =
            false;

        foreach (char character in value)
        {
            if (character == '<')
            {
                insideTag = true;
                continue;
            }

            if (character == '>' &&
                insideTag)
            {
                insideTag = false;
                continue;
            }

            if (!insideTag)
                builder.Append(character);
        }

        return builder.ToString();
    }

    private static string BuildFallbackName(
        Character character)
    {
        string name =
            character?.Data?.CharacterName ??
            character?.name ??
            "?";

        if (string.IsNullOrWhiteSpace(name))
            return "?";

        string compact =
            name.Replace(
                    " ",
                    string.Empty)
                .Replace(
                    "\n",
                    string.Empty)
                .Replace(
                    "\r",
                    string.Empty)
                .Trim();

        const int maxCharacters = 6;

        return compact.Length <= maxCharacters
            ? compact
            : compact.Substring(
                  0,
                  maxCharacters - 1) +
              "…";
    }

    private static float GetFallbackNameFontSize(
        string value)
    {
        int length =
            string.IsNullOrEmpty(value)
                ? 1
                : value.Length;

        return length switch
        {
            <= 2 => 22f,
            3 => 18f,
            4 => 16f,
            _ => 14f
        };
    }

    private static string GetPartName(
        PartType type)
    {
        return type switch
        {
            PartType.HEAD => "머리",
            PartType.LEFT_HAND => "왼손",
            PartType.RIGHT_HAND => "오른손",
            PartType.LEGS => "다리",
            _ => type.ToString()
        };
    }
}