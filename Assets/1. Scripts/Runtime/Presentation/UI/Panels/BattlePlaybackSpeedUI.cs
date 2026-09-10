using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 배속 1x / 2x / 3x 선택 UI.
/// 고정 Canvas/Panel/Button은 Scene에 미리 배치하고,
/// Play Mode에서는 위치/크기/Anchor를 변경하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlaybackSpeedUI : MonoBehaviour
{
    [Header("Scene-authored View")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Button speed1Button;
    [SerializeField] private Button speed2Button;
    [SerializeField] private Button speed3Button;
    [SerializeField] private Image speed1Image;
    [SerializeField] private Image speed2Image;
    [SerializeField] private Image speed3Image;
    [SerializeField] private TMP_Text speed1Label;
    [SerializeField] private TMP_Text speed2Label;
    [SerializeField] private TMP_Text speed3Label;

    private BattlePlaybackSpeedController controller;
    private bool controlsBound;
    private bool missingSceneViewLogged;

    public void Bind(BattlePlaybackSpeedController value)
    {
        if (controller == value)
        {
            ResolveSceneReferences();
            BindControls();
            Refresh();
            return;
        }

        UnbindController();
        controller = value;

        ResolveSceneReferences();
        BindControls();

        if (controller != null)
            controller.SpeedChanged += OnSpeedChanged;

        Refresh();
    }

    private void Awake()
    {
        ResolveSceneReferences();
        BindControls();

        if (controller == null)
        {
            controller =
                BattlePlaybackSpeedController.Instance ??
                FindFirstObjectByType<BattlePlaybackSpeedController>(
                    FindObjectsInactive.Include);
        }

        if (controller != null)
            controller.SpeedChanged += OnSpeedChanged;

        Refresh();
    }

    private void OnEnable()
    {
        ResolveSceneReferences();
        BindControls();
        Refresh();
    }

    private void OnDestroy()
    {
        UnbindController();
        UnbindControls();
    }

    private void UnbindController()
    {
        if (controller != null)
            controller.SpeedChanged -= OnSpeedChanged;
    }

    private bool ResolveSceneReferences()
    {
        if (rootCanvas == null)
            rootCanvas = GetComponent<Canvas>();

        if (panelRect == null)
            panelRect = transform.Find("BattleSpeedPanel") as RectTransform;

        ResolveButton(
            panelRect,
            "Speed_1x",
            ref speed1Button,
            ref speed1Image,
            ref speed1Label);

        ResolveButton(
            panelRect,
            "Speed_2x",
            ref speed2Button,
            ref speed2Image,
            ref speed2Label);

        ResolveButton(
            panelRect,
            "Speed_3x",
            ref speed3Button,
            ref speed3Image,
            ref speed3Label);

        bool valid =
            rootCanvas != null &&
            panelRect != null &&
            speed1Button != null &&
            speed2Button != null &&
            speed3Button != null &&
            speed1Image != null &&
            speed2Image != null &&
            speed3Image != null &&
            speed1Label != null &&
            speed2Label != null &&
            speed3Label != null;

        if (!valid && Application.isPlaying && !missingSceneViewLogged)
        {
            missingSceneViewLogged = true;
            Debug.LogWarning(
                "[BattlePlaybackSpeedUI] Scene-authored 배속 UI가 없습니다. " +
                "Editor 변환 도구를 실행하세요.",
                this);
        }
        else if (valid)
        {
            missingSceneViewLogged = false;
        }

        return valid;
    }

    private static void ResolveButton(
        RectTransform panel,
        string childName,
        ref Button button,
        ref Image image,
        ref TMP_Text label)
    {
        if (button != null &&
            image != null &&
            label != null)
        {
            return;
        }

        if (panel == null)
            return;

        Transform child = panel.Find(childName);
        if (child == null)
            return;

        button ??= child.GetComponent<Button>();
        image ??= child.GetComponent<Image>();
        label ??=
            child.Find("Label")?.GetComponent<TMP_Text>() ??
            child.GetComponentInChildren<TMP_Text>(true);
    }

    private void BindControls()
    {
        if (!ResolveSceneReferences() || controlsBound)
            return;

        speed1Button.onClick.AddListener(Set1x);
        speed2Button.onClick.AddListener(Set2x);
        speed3Button.onClick.AddListener(Set3x);
        controlsBound = true;
    }

    private void UnbindControls()
    {
        if (!controlsBound)
            return;

        if (speed1Button != null)
            speed1Button.onClick.RemoveListener(Set1x);
        if (speed2Button != null)
            speed2Button.onClick.RemoveListener(Set2x);
        if (speed3Button != null)
            speed3Button.onClick.RemoveListener(Set3x);

        controlsBound = false;
    }

    private void Set1x() => controller?.SetSpeed(1);
    private void Set2x() => controller?.SetSpeed(2);
    private void Set3x() => controller?.SetSpeed(3);

    private void OnSpeedChanged(
        BattlePlaybackSpeedMode mode,
        float multiplier)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (!ResolveSceneReferences())
            return;

        int selected =
            controller != null
                ? controller.CurrentLevel
                : 1;

        ApplyState(
            1,
            selected,
            speed1Image,
            speed1Label);
        ApplyState(
            2,
            selected,
            speed2Image,
            speed2Label);
        ApplyState(
            3,
            selected,
            speed3Image,
            speed3Label);
    }

    private static void ApplyState(
        int level,
        int selected,
        Image image,
        TMP_Text label)
    {
        bool active = level == selected;

        if (image != null)
        {
            image.color =
                active
                    ? ActiveColor
                    : InactiveColor;
        }

        if (label != null)
        {
            label.color =
                active
                    ? ActiveTextColor
                    : InactiveTextColor;
        }
    }

    private static Color ActiveColor =>
        new Color(0.82f, 0.67f, 0.22f, 0.98f);

    private static Color InactiveColor =>
        new Color(0.16f, 0.16f, 0.18f, 0.96f);

    private static Color ActiveTextColor =>
        new Color(0.05f, 0.05f, 0.05f, 1f);

    private static Color InactiveTextColor =>
        new Color(0.92f, 0.92f, 0.92f, 1f);

#if UNITY_EDITOR
    /// <summary>
    /// Editor 변환 도구에서 한 번만 호출한다.
    /// 생성 이후 RectTransform은 사용자가 직접 조정하며 Play Mode에서는 수정하지 않는다.
    /// </summary>
    public void EditorAuthorSceneView()
    {
        if (rootCanvas == null)
            rootCanvas = GetComponent<Canvas>();

        if (rootCanvas == null)
            rootCanvas = gameObject.AddComponent<Canvas>();

        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = 32000;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (panelRect == null)
            panelRect = transform.Find("BattleSpeedPanel") as RectTransform;

        if (panelRect == null)
        {
            GameObject panelObject = new(
                "BattleSpeedPanel",
                typeof(RectTransform),
                typeof(Image));
            panelObject.transform.SetParent(transform, false);
            panelRect = panelObject.GetComponent<RectTransform>();

            // 최초 생성용 초기값일 뿐이다. Play Mode에서는 이 값을 다시 쓰지 않는다.
            panelRect.anchorMin = Vector2.one;
            panelRect.anchorMax = Vector2.one;
            panelRect.pivot = Vector2.one;
            panelRect.anchoredPosition = new Vector2(-28f, -96f);
            panelRect.sizeDelta = new Vector2(220f, 56f);

            Image background = panelObject.GetComponent<Image>();
            background.color = new Color(0.055f, 0.055f, 0.065f, 0.92f);
            background.raycastTarget = false;
        }

        CreateEditorButton(0, 1);
        CreateEditorButton(1, 2);
        CreateEditorButton(2, 3);

        ResolveSceneReferences();
    }

    private void CreateEditorButton(int index, int level)
    {
        string name = $"Speed_{level}x";
        Transform existing = panelRect.Find(name);
        if (existing != null)
            return;

        GameObject buttonObject = new(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(panelRect, false);

        RectTransform rect =
            buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(8f + index * 70f, 0f);
        rect.sizeDelta = new Vector2(64f, 40f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = InactiveColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObject = new(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(rect, false);

        RectTransform labelRect =
            labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = $"x{level}";
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.color = InactiveTextColor;
        label.fontStyle = FontStyles.Bold;
    }
#endif
}