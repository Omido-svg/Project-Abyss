using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 플레이어의 캐릭터 고유 자원을 표시한다.
/// 유진은 무기 선택, 살수의 감 자동 사용, 무기별 전환 연출을 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterMechanicHudUI : MonoBehaviour
{
    [Header("Legacy — retired by 2026-08-17 UI revision")]
    [SerializeField] private bool enableLegacyCharacterPanel = false;

    [SerializeField]
    private BattleManager battleManager;

    [SerializeField]
    private TMP_Text summaryText;

    [SerializeField]
    private GameObject yujinControls;

    [SerializeField]
    private Button baekuButton;

    [SerializeField]
    private Button jeokseolButton;

    [SerializeField]
    private Button nakilButton;

    [SerializeField]
    private Toggle autoSenseToggle;

    [Header("Yujin Weapon Presentation")]
    [SerializeField]
    private RectTransform weaponSelectionFrame;

    [SerializeField]
    private Image weaponSelectionFrameImage;

    [SerializeField]
    private RectTransform weaponAnnouncementRoot;

    [SerializeField]
    private CanvasGroup weaponAnnouncementGroup;

    [SerializeField]
    private Image weaponAnnouncementBackground;

    [SerializeField]
    private TMP_Text weaponAnnouncementText;

    [Header("Panel Presentation")]
    [SerializeField]
    private TMP_Text panelTitleText;

    [SerializeField]
    private TMP_Text autoSenseHelpText;

    [SerializeField]
    private Image panelHeaderImage;

    private CharacterMechanicHudPanelView panelView;
    private YujinWeaponHudView yujinWeaponView;
    private ICharacterMechanicHudPresenter olafPresenter;
    private ICharacterMechanicHudPresenter yujinPresenter;
    private ICharacterMechanicHudPresenter activePresenter;

    private void Awake()
    {
        if (!enableLegacyCharacterPanel)
        {
            gameObject.SetActive(false);
            return;
        }

        ResolveBattleManager();
        EnsureView();
        RebuildPresenters();
    }

    private void OnEnable()
    {
        ResolveBattleManager();

        if (enableLegacyCharacterPanel &&
            olafPresenter == null)
        {
            EnsureView();
            RebuildPresenters();
        }
    }

    private void OnDisable()
    {
        DeactivatePresenter();
    }

    private void OnDestroy()
    {
        DeactivatePresenter();
        yujinWeaponView?.Dispose();
        yujinWeaponView = null;
    }

    private void Update()
    {
        Character player =
            battleManager?.BattleContext?.Player;

        if (player == null)
        {
            DeactivatePresenter();
            panelView?.SetPresentation(
                "캐릭터 고유 정보",
                false);
            panelView?.SetSummary(
                "캐릭터 고유 정보: 전투 준비 중");
            return;
        }

        ICharacterMechanicHudPresenter presenter =
            ResolvePresenter(player);

        if (presenter == null)
        {
            DeactivatePresenter();
            panelView?.SetPresentation(
                $"{player.Data?.CharacterName ?? player.name}  ·  전용 HUD",
                false);
            panelView?.SetSummary(
                $"{player.Data?.CharacterName ?? player.name}\n" +
                "전용 HUD 없음");
            return;
        }

        if (!ReferenceEquals(activePresenter, presenter))
        {
            DeactivatePresenter();
            activePresenter = presenter;
        }

        activePresenter.Present(player);
    }

    private ICharacterMechanicHudPresenter ResolvePresenter(
        Character player)
    {
        if (olafPresenter?.CanPresent(player) == true)
            return olafPresenter;

        if (yujinPresenter?.CanPresent(player) == true)
            return yujinPresenter;

        return null;
    }

    private void DeactivatePresenter()
    {
        activePresenter?.Deactivate();
        activePresenter = null;
    }

    private void RebuildPresenters()
    {
        DeactivatePresenter();
        yujinWeaponView?.Dispose();

        panelView =
            new CharacterMechanicHudPanelView(
                transform as RectTransform,
                summaryText,
                panelTitleText,
                yujinControls);

        yujinWeaponView =
            new YujinWeaponHudView(
                baekuButton,
                jeokseolButton,
                nakilButton,
                autoSenseToggle,
                weaponSelectionFrame,
                weaponSelectionFrameImage,
                weaponAnnouncementRoot,
                weaponAnnouncementGroup,
                weaponAnnouncementBackground,
                weaponAnnouncementText);

        olafPresenter =
            new OlafMechanicHudPresenter(
                panelView,
                () => battleManager);

        yujinPresenter =
            new YujinMechanicHudPresenter(
                panelView,
                yujinWeaponView,
                () => battleManager);
    }

    private void ResolveBattleManager()
    {
        if (battleManager == null)
        {
            battleManager =
                FindFirstObjectByType<BattleManager>();
        }
    }

    private void EnsureView()
    {
        RectTransform root =
            transform as RectTransform;

        if (root == null)
        {
            Debug.LogError(
                "[CharacterMechanicHudUI] RectTransform이 필요합니다.",
                this);
            enabled = false;
            return;
        }

        // 화면 왼쪽 아래 고정 패널.
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.anchoredPosition = new Vector2(14f, 18f);
        root.sizeDelta = new Vector2(430f, 340f);

        Image background =
            GetComponent<Image>();

        if (background == null)
            background = gameObject.AddComponent<Image>();

        background.color =
            new Color(0.025f, 0.040f, 0.060f, 0.94f);
        background.raycastTarget = false;

        UnityEngine.UI.Outline outline =
            GetComponent<UnityEngine.UI.Outline>();

        if (outline == null)
        {
            outline =
                gameObject.AddComponent<UnityEngine.UI.Outline>();
        }

        outline.effectColor =
            new Color(0.20f, 0.56f, 0.82f, 0.58f);
        outline.effectDistance =
            new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        EnsurePanelHeader(root);
        EnsureSummary(root);
        EnsureYujinControls(root);
        ApplySharedFont();
        EnsureWeaponPresentation();

    }

    private void EnsurePanelHeader(
        RectTransform root)
    {
        Transform existing =
            root.Find("PanelHeader");

        RectTransform headerRect;

        if (existing == null)
        {
            GameObject header =
                new GameObject(
                    "PanelHeader",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

            headerRect =
                header.GetComponent<RectTransform>();

            headerRect.SetParent(root, false);
        }
        else
        {
            headerRect =
                existing as RectTransform;
        }

        headerRect.anchorMin =
            new Vector2(0f, 1f);
        headerRect.anchorMax =
            new Vector2(1f, 1f);
        headerRect.pivot =
            new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta =
            new Vector2(0f, 38f);

        panelHeaderImage =
            headerRect.GetComponent<Image>();

        panelHeaderImage.color =
            new Color(0.055f, 0.16f, 0.25f, 0.96f);
        panelHeaderImage.raycastTarget = false;

        Transform titleExisting =
            headerRect.Find("Title");

        if (titleExisting == null)
        {
            panelTitleText =
                CreateText(
                    "Title",
                    headerRect,
                    Vector2.zero,
                    Vector2.zero,
                    18f);
        }
        else
        {
            panelTitleText =
                titleExisting.GetComponent<TMP_Text>();
        }

        RectTransform titleRect =
            panelTitleText.transform as RectTransform;

        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.pivot =
            new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = Vector2.zero;

        panelTitleText.fontStyle =
            FontStyles.Bold;
        panelTitleText.alignment =
            TextAlignmentOptions.Left;
        panelTitleText.margin =
            new Vector4(14f, 0f, 8f, 0f);
        panelTitleText.color =
            new Color(0.90f, 0.96f, 1f, 1f);
        panelTitleText.textWrappingMode =
            TextWrappingModes.NoWrap;
        panelTitleText.raycastTarget = false;
    }

    private void EnsureSummary(
        RectTransform root)
    {
        if (summaryText == null)
        {
            Transform existing =
                root.Find("Summary");

            summaryText =
                existing != null
                    ? existing.GetComponent<TMP_Text>()
                    : CreateText(
                        "Summary",
                        root,
                        Vector2.zero,
                        Vector2.zero,
                        16f);
        }

        RectTransform summaryRect =
            summaryText.transform as RectTransform;

        summaryRect.anchorMin =
            new Vector2(0f, 1f);
        summaryRect.anchorMax =
            new Vector2(1f, 1f);
        summaryRect.pivot =
            new Vector2(0.5f, 1f);
        summaryRect.anchoredPosition =
            new Vector2(0f, -48f);
        summaryRect.sizeDelta =
            new Vector2(-24f, 174f);

        summaryText.fontSize = 16f;
        summaryText.enableAutoSizing = true;
        summaryText.fontSizeMin = 12f;
        summaryText.fontSizeMax = 16f;
        summaryText.richText = true;
        summaryText.textWrappingMode =
            TextWrappingModes.Normal;
        summaryText.overflowMode =
            TextOverflowModes.Ellipsis;
        summaryText.alignment =
            TextAlignmentOptions.TopLeft;
        summaryText.lineSpacing = -2f;
        summaryText.raycastTarget = false;
    }

    private void EnsureYujinControls(
        RectTransform root)
    {
        if (yujinControls == null)
        {
            yujinControls =
                new GameObject(
                    "YujinControls",
                    typeof(RectTransform));

            yujinControls.transform.SetParent(
                root,
                false);
        }

        RectTransform controls =
            yujinControls.transform as RectTransform;

        controls.anchorMin =
            new Vector2(0f, 0f);
        controls.anchorMax =
            new Vector2(1f, 0f);
        controls.pivot =
            new Vector2(0.5f, 0f);
        controls.anchoredPosition =
            new Vector2(0f, 12f);
        controls.sizeDelta =
            new Vector2(-24f, 108f);

        if (baekuButton == null)
        {
            baekuButton =
                CreateButton(
                    "BaekuButton",
                    "백우",
                    controls,
                    Vector2.zero,
                    YujinWeaponHudView.BaekuBase);
        }

        if (jeokseolButton == null)
        {
            jeokseolButton =
                CreateButton(
                    "JeokseolButton",
                    "적설",
                    controls,
                    Vector2.zero,
                    YujinWeaponHudView.JeokseolBase);
        }

        if (nakilButton == null)
        {
            nakilButton =
                CreateButton(
                    "NakilButton",
                    "낙일",
                    controls,
                    Vector2.zero,
                    YujinWeaponHudView.NakilBase);
        }

        ConfigureWeaponButton(
            baekuButton,
            new Vector2(0f, 66f),
            YujinWeaponHudView.BaekuBase);

        ConfigureWeaponButton(
            jeokseolButton,
            new Vector2(128f, 66f),
            YujinWeaponHudView.JeokseolBase);

        ConfigureWeaponButton(
            nakilButton,
            new Vector2(256f, 66f),
            YujinWeaponHudView.NakilBase);

        if (autoSenseToggle == null)
        {
            autoSenseToggle =
                CreateToggle(
                    controls,
                    Vector2.zero);
        }

        ConfigureAutoSenseToggle(
            controls);
    }

    private static void ConfigureWeaponButton(
        Button button,
        Vector2 anchoredPosition,
        Color baseColor)
    {
        RectTransform rect =
            button?.transform as RectTransform;

        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition =
            anchoredPosition;
        rect.sizeDelta =
            new Vector2(118f, 34f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image image =
            button.GetComponent<Image>();

        if (image != null)
            image.color = baseColor;

        TMP_Text label =
            button.GetComponentInChildren<TMP_Text>(
                true);

        if (label != null)
        {
            label.fontSize = 16f;
            label.fontStyle = FontStyles.Bold;
            label.alignment =
                TextAlignmentOptions.Center;
            label.textWrappingMode =
                TextWrappingModes.NoWrap;
            label.raycastTarget = false;
        }
    }

    private void ConfigureAutoSenseToggle(
        RectTransform controls)
    {
        if (autoSenseToggle == null)
            return;

        RectTransform toggleRect =
            autoSenseToggle.transform as RectTransform;

        toggleRect.anchorMin = Vector2.zero;
        toggleRect.anchorMax = Vector2.zero;
        toggleRect.pivot = Vector2.zero;
        toggleRect.anchoredPosition =
            new Vector2(0f, 18f);
        toggleRect.sizeDelta =
            new Vector2(224f, 36f);

        Image clickArea =
            autoSenseToggle.GetComponent<Image>();

        if (clickArea == null)
        {
            clickArea =
                autoSenseToggle.gameObject
                    .AddComponent<Image>();
        }

        clickArea.color =
            new Color(1f, 1f, 1f, 0.001f);
        clickArea.raycastTarget = true;

        Transform backgroundTransform =
            toggleRect.Find("Background");

        Image track =
            backgroundTransform != null
                ? backgroundTransform.GetComponent<Image>()
                : null;

        if (track != null)
        {
            RectTransform trackRect =
                track.transform as RectTransform;

            trackRect.anchorMin =
                new Vector2(0f, 0.5f);
            trackRect.anchorMax =
                new Vector2(0f, 0.5f);
            trackRect.pivot =
                new Vector2(0f, 0.5f);
            trackRect.anchoredPosition =
                new Vector2(2f, 0f);
            trackRect.sizeDelta =
                new Vector2(42f, 22f);

            track.color =
                new Color(0.12f, 0.18f, 0.23f, 1f);
            track.raycastTarget = false;
        }

        Image checkmark =
            backgroundTransform != null
                ? backgroundTransform
                    .Find("Checkmark")
                    ?.GetComponent<Image>()
                : null;

        if (checkmark != null)
        {
            RectTransform checkRect =
                checkmark.transform as RectTransform;

            checkRect.anchorMin =
                new Vector2(0.16f, 0.20f);
            checkRect.anchorMax =
                new Vector2(0.84f, 0.80f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            checkmark.color =
                new Color(0.36f, 0.90f, 0.64f, 1f);
            checkmark.raycastTarget = false;
        }

        TMP_Text label =
            toggleRect.Find("Label")
                ?.GetComponent<TMP_Text>();

        if (label != null)
        {
            RectTransform labelRect =
                label.transform as RectTransform;

            labelRect.anchorMin =
                new Vector2(0f, 0f);
            labelRect.anchorMax =
                new Vector2(0f, 1f);
            labelRect.pivot =
                new Vector2(0f, 0.5f);
            labelRect.anchoredPosition =
                new Vector2(52f, 0f);
            labelRect.sizeDelta =
                new Vector2(170f, 0f);

            label.text =
                "다음 각인·추격 감 사용";
            label.fontSize = 14f;
            label.fontStyle = FontStyles.Bold;
            label.alignment =
                TextAlignmentOptions.Left;
            label.textWrappingMode =
                TextWrappingModes.NoWrap;
            label.raycastTarget = false;
        }

        autoSenseToggle.targetGraphic =
            track != null
                ? track
                : clickArea;
        autoSenseToggle.graphic = checkmark;
        autoSenseToggle.transition =
            Selectable.Transition.ColorTint;

        Navigation navigation =
            autoSenseToggle.navigation;
        navigation.mode = Navigation.Mode.None;
        autoSenseToggle.navigation = navigation;

        Transform helpExisting =
            controls.Find("AutoSenseHelp");

        if (helpExisting == null)
        {
            autoSenseHelpText =
                CreateText(
                    "AutoSenseHelp",
                    controls,
                    Vector2.zero,
                    Vector2.zero,
                    12f);
        }
        else
        {
            autoSenseHelpText =
                helpExisting.GetComponent<TMP_Text>();
        }

        RectTransform helpRect =
            autoSenseHelpText.transform as RectTransform;

        helpRect.anchorMin = Vector2.zero;
        helpRect.anchorMax = Vector2.zero;
        helpRect.pivot = Vector2.zero;
        helpRect.anchoredPosition =
            new Vector2(230f, 10f);
        helpRect.sizeDelta =
            new Vector2(174f, 48f);

        autoSenseHelpText.text =
            "각인·추격 선택 전에 설정합니다.\n선택값은 해당 행동에 저장됩니다.";
        autoSenseHelpText.fontSize = 11.5f;
        autoSenseHelpText.color =
            new Color(0.68f, 0.76f, 0.82f, 1f);
        autoSenseHelpText.alignment =
            TextAlignmentOptions.BottomLeft;
        autoSenseHelpText.textWrappingMode =
            TextWrappingModes.Normal;
        autoSenseHelpText.raycastTarget = false;
    }

    private void ApplySharedFont()
    {
        TMP_FontAsset sharedFont =
            summaryText?.font;

        if (sharedFont == null)
        {
            sharedFont =
                baekuButton
                    ?.GetComponentInChildren<TMP_Text>(true)
                    ?.font;
        }

        if (sharedFont == null)
            return;

        if (panelTitleText != null)
            panelTitleText.font = sharedFont;

        if (autoSenseHelpText != null)
            autoSenseHelpText.font = sharedFont;

        TMP_Text toggleLabel =
            autoSenseToggle
                ?.GetComponentInChildren<TMP_Text>(true);

        if (toggleLabel != null)
            toggleLabel.font = sharedFont;

        TMP_Text baekuLabel =
            baekuButton
                ?.GetComponentInChildren<TMP_Text>(true);

        TMP_Text jeokseolLabel =
            jeokseolButton
                ?.GetComponentInChildren<TMP_Text>(true);

        TMP_Text nakilLabel =
            nakilButton
                ?.GetComponentInChildren<TMP_Text>(true);

        if (baekuLabel != null)
            baekuLabel.font = sharedFont;

        if (jeokseolLabel != null)
            jeokseolLabel.font = sharedFont;

        if (nakilLabel != null)
            nakilLabel.font = sharedFont;
    }

    private void EnsureWeaponPresentation()
    {
        RectTransform root =
            transform as RectTransform;

        RectTransform controls =
            yujinControls?.transform as RectTransform;

        if (root == null ||
            controls == null)
        {
            return;
        }

        if (weaponSelectionFrame == null)
        {
            GameObject frame =
                new GameObject(
                    "WeaponSelectionFrame",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

            weaponSelectionFrame =
                frame.GetComponent<RectTransform>();

            weaponSelectionFrame.SetParent(
                controls,
                false);

            weaponSelectionFrame.anchorMin =
                new Vector2(0f, 0f);
            weaponSelectionFrame.anchorMax =
                new Vector2(0f, 0f);
            weaponSelectionFrame.pivot =
                new Vector2(0f, 0f);
            weaponSelectionFrame.sizeDelta =
                new Vector2(96f, 30f);

            weaponSelectionFrameImage =
                frame.GetComponent<Image>();

            weaponSelectionFrameImage.color =
                YujinWeaponHudView.BaekuAccent;

            weaponSelectionFrameImage.raycastTarget = false;
            frame.transform.SetAsFirstSibling();
        }
        else if (weaponSelectionFrameImage == null)
        {
            weaponSelectionFrameImage =
                weaponSelectionFrame.GetComponent<Image>();
        }

        if (weaponSelectionFrame != null)
        {
            weaponSelectionFrame.anchorMin = Vector2.zero;
            weaponSelectionFrame.anchorMax = Vector2.zero;
            weaponSelectionFrame.pivot = Vector2.zero;
            weaponSelectionFrame.sizeDelta =
                new Vector2(118f, 34f);
        }

        if (weaponSelectionFrameImage != null)
        {
            weaponSelectionFrameImage.color =
                new Color(
                    weaponSelectionFrameImage.color.r,
                    weaponSelectionFrameImage.color.g,
                    weaponSelectionFrameImage.color.b,
                    0.34f);
        }

        if (weaponAnnouncementRoot == null)
        {
            GameObject announcement =
                new GameObject(
                    "WeaponAnnouncement",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup));

            weaponAnnouncementRoot =
                announcement.GetComponent<RectTransform>();

            weaponAnnouncementRoot.SetParent(
                root,
                false);

            weaponAnnouncementRoot.anchorMin =
                new Vector2(0f, 1f);
            weaponAnnouncementRoot.anchorMax =
                new Vector2(0f, 1f);
            weaponAnnouncementRoot.pivot =
                new Vector2(0f, 1f);
            weaponAnnouncementRoot.anchoredPosition =
                new Vector2(12f, -218f);
            weaponAnnouncementRoot.sizeDelta =
                new Vector2(406f, 34f);

            weaponAnnouncementBackground =
                announcement.GetComponent<Image>();

            weaponAnnouncementGroup =
                announcement.GetComponent<CanvasGroup>();

            weaponAnnouncementText =
                CreateText(
                    "Label",
                    weaponAnnouncementRoot,
                    Vector2.zero,
                    weaponAnnouncementRoot.sizeDelta,
                    16f);

            RectTransform labelRect =
                weaponAnnouncementText.transform
                    as RectTransform;

            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot =
                new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;

            weaponAnnouncementText.alignment =
                TextAlignmentOptions.Center;

            weaponAnnouncementGroup.interactable = false;
            weaponAnnouncementGroup.blocksRaycasts = false;
            weaponAnnouncementGroup.alpha = 0f;
            announcement.SetActive(false);
        }
        else
        {
            if (weaponAnnouncementBackground == null)
            {
                weaponAnnouncementBackground =
                    weaponAnnouncementRoot.GetComponent<Image>();
            }

            if (weaponAnnouncementGroup == null)
            {
                weaponAnnouncementGroup =
                    weaponAnnouncementRoot.GetComponent<CanvasGroup>();
            }

            if (weaponAnnouncementText == null)
            {
                weaponAnnouncementText =
                    weaponAnnouncementRoot
                        .Find("Label")
                        ?.GetComponent<TMP_Text>();
            }
        }

        if (weaponAnnouncementRoot != null)
        {
            weaponAnnouncementRoot.anchorMin =
                new Vector2(0f, 1f);
            weaponAnnouncementRoot.anchorMax =
                new Vector2(0f, 1f);
            weaponAnnouncementRoot.pivot =
                new Vector2(0f, 1f);
            weaponAnnouncementRoot.anchoredPosition =
                new Vector2(12f, -218f);
            weaponAnnouncementRoot.sizeDelta =
                new Vector2(406f, 34f);
        }

        if (weaponAnnouncementText != null)
        {
            RectTransform labelRect =
                weaponAnnouncementText.transform
                    as RectTransform;

            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot =
                new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;
            weaponAnnouncementText.alignment =
                TextAlignmentOptions.Center;
        }
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize)
    {
        GameObject value =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        RectTransform rect =
            value.GetComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text =
            value.GetComponent<TextMeshProUGUI>();

        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment =
            TextAlignmentOptions.TopLeft;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;

        return text;
    }

    private static Button CreateButton(
        string objectName,
        string label,
        Transform parent,
        Vector2 anchoredPosition,
        Color baseColor)
    {
        GameObject value =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));

        RectTransform rect =
            value.GetComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(96f, 30f);

        value.GetComponent<Image>().color =
            baseColor;

        TMP_Text text =
            CreateText(
                "Label",
                rect,
                Vector2.zero,
                rect.sizeDelta,
                16f);

        RectTransform textRect =
            text.transform as RectTransform;

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        text.text = label;
        text.alignment =
            TextAlignmentOptions.Center;

        return value.GetComponent<Button>();
    }

    private static Toggle CreateToggle(
        Transform parent,
        Vector2 anchoredPosition)
    {
        GameObject value =
            new GameObject(
                "AutoSenseToggle",
                typeof(RectTransform),
                typeof(Image),
                typeof(Toggle));

        RectTransform rect =
            value.GetComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(224f, 36f);

        Image clickArea =
            value.GetComponent<Image>();

        clickArea.color =
            new Color(1f, 1f, 1f, 0.001f);
        clickArea.raycastTarget = true;

        GameObject background =
            new GameObject(
                "Background",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        RectTransform bgRect =
            background.GetComponent<RectTransform>();

        bgRect.SetParent(rect, false);
        bgRect.anchorMin = new Vector2(0f, 0.5f);
        bgRect.anchorMax = new Vector2(0f, 0.5f);
        bgRect.pivot = new Vector2(0f, 0.5f);
        bgRect.anchoredPosition = new Vector2(2f, 0f);
        bgRect.sizeDelta = new Vector2(42f, 22f);

        Image bgImage =
            background.GetComponent<Image>();

        bgImage.color =
            new Color(0.12f, 0.18f, 0.23f, 1f);
        bgImage.raycastTarget = false;

        GameObject checkmark =
            new GameObject(
                "Checkmark",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        RectTransform checkRect =
            checkmark.GetComponent<RectTransform>();

        checkRect.SetParent(bgRect, false);
        checkRect.anchorMin = new Vector2(0.16f, 0.20f);
        checkRect.anchorMax = new Vector2(0.84f, 0.80f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;

        Image checkImage =
            checkmark.GetComponent<Image>();

        checkImage.color =
            new Color(0.36f, 0.90f, 0.64f, 1f);
        checkImage.raycastTarget = false;

        TMP_Text label =
            CreateText(
                "Label",
                rect,
                Vector2.zero,
                Vector2.zero,
                14f);

        RectTransform labelRect =
            label.transform as RectTransform;

        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(52f, 0f);
        labelRect.sizeDelta = new Vector2(170f, 0f);

        label.text = "다음 각인·추격 감 사용";
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;

        Toggle toggle =
            value.GetComponent<Toggle>();

        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;

        return toggle;
    }

}
