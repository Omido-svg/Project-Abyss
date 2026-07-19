using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 동적 분석 패널의 표시 상태만 제어한다.
/// 패널 GameObject는 비활성화하지 않고 CanvasGroup으로 숨기므로,
/// 숨긴 동안에도 배치 분석 진행 상태와 결과 텍스트가 계속 갱신된다.
/// </summary>
public sealed class BattleAnalysisPanelToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleButtonLabel;

    [Header("Visibility")]
    [SerializeField] private bool startHidden = true;
    [SerializeField] private bool enableF8Shortcut = true;

    [Header("Labels")]
    [SerializeField] private string showLabel =
        "분석 패널 열기 [F8]";
    [SerializeField] private string hideLabel =
        "분석 패널 닫기 [F8]";

    private bool isVisible;
    private bool initialized;

    public bool IsVisible => isVisible;

    public void Configure(
        GameObject panel,
        CanvasGroup canvasGroup,
        Button button,
        TMP_Text buttonLabel,
        bool hiddenAtStart)
    {
        panelRoot = panel;
        panelCanvasGroup = canvasGroup;
        toggleButton = button;
        toggleButtonLabel = buttonLabel;
        startHidden = hiddenAtStart;
    }

    private void Awake()
    {
        ResolveReferences();
        BindButton();
        SetPanelVisible(!startHidden);
        initialized = true;
    }

    private void OnEnable()
    {
        if (!initialized)
            return;

        BindButton();
        ApplyVisibility();
    }

    private void OnDestroy()
    {
        UnbindButton();
    }

    private void Update()
    {
        if (!enableF8Shortcut)
            return;

        if (WasF8Pressed())
            TogglePanel();
    }

    public void TogglePanel()
    {
        SetPanelVisible(!isVisible);
    }

    public void ShowPanel()
    {
        SetPanelVisible(true);
    }

    public void HidePanel()
    {
        SetPanelVisible(false);
    }

    public void SetPanelVisible(bool visible)
    {
        ResolveReferences();
        isVisible = visible;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (panelRoot != null && !panelRoot.activeSelf)
            panelRoot.SetActive(true);

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = isVisible ? 1f : 0f;
            panelCanvasGroup.interactable = isVisible;
            panelCanvasGroup.blocksRaycasts = isVisible;
        }

        if (toggleButtonLabel != null)
        {
            toggleButtonLabel.text =
                isVisible
                    ? hideLabel
                    : showLabel;
        }
    }

    private void ResolveReferences()
    {
        if (panelRoot == null)
        {
            Transform panel =
                transform.Find("BattleAnalysisPanel");

            if (panel != null)
                panelRoot = panel.gameObject;
        }

        if (panelCanvasGroup == null && panelRoot != null)
        {
            panelCanvasGroup =
                panelRoot.GetComponent<CanvasGroup>();

            if (panelCanvasGroup == null)
            {
                panelCanvasGroup =
                    panelRoot.AddComponent<CanvasGroup>();
            }
        }

        if (toggleButton == null)
        {
            Transform button =
                transform.Find("BattleAnalysisToggleButton");

            if (button != null)
                toggleButton = button.GetComponent<Button>();
        }

        if (toggleButtonLabel == null && toggleButton != null)
        {
            toggleButtonLabel =
                toggleButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void BindButton()
    {
        UnbindButton();
        toggleButton?.onClick.AddListener(TogglePanel);
    }

    private void UnbindButton()
    {
        toggleButton?.onClick.RemoveListener(TogglePanel);
    }

    private static bool WasF8Pressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.f8Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F8);
#else
        return false;
#endif
    }
}
