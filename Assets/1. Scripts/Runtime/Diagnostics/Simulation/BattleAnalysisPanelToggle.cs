using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 동적 분석 패널의 표시 상태를 제어한다.
///
/// 시작 시 숨김이 설정되어 있으면 Scene에 저장된 activeSelf 값과 무관하게
/// 패널 GameObject 자체를 비활성화한다.
/// </summary>
[DefaultExecutionOrder(-1200)]
public sealed class BattleAnalysisPanelToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleButtonLabel;

    [Header("Visibility")]
    [SerializeField] private bool startHidden = true;
    [SerializeField] private bool deactivatePanelWhenHidden = true;
    [SerializeField] private bool hideInEditMode = true;
    [SerializeField] private bool enableF8Shortcut = true;

    [Header("Labels")]
    [SerializeField] private string showLabel =
        "분석 패널 열기 [F8]";
    [SerializeField] private string hideLabel =
        "분석 패널 닫기 [F8]";

    private bool isVisible;
    private bool initialStateApplied;
    private bool visibilityChangedByUser;
    private Coroutine initialStateRoutine;

#if UNITY_EDITOR
    private bool editorHideQueued;
#endif

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

        initialStateApplied = false;
        visibilityChangedByUser = false;

        if (Application.isPlaying)
            ApplyInitialState();
    }

    private void Reset()
    {
        startHidden = true;
        deactivatePanelWhenHidden = true;
        hideInEditMode = true;
    }

    private void Awake()
    {
        ResolveReferences(allowAddCanvasGroup: true);
        BindButton();
        ApplyInitialState();
    }

    private void OnEnable()
    {
        ResolveReferences(allowAddCanvasGroup: true);
        BindButton();

        if (!initialStateApplied)
            ApplyInitialState();
        else
            ApplyVisibility();

        if (Application.isPlaying &&
            startHidden &&
            !visibilityChangedByUser)
        {
            RestartInitialStateRoutine();
        }
    }

    private void Start()
    {
        if (startHidden &&
            !visibilityChangedByUser)
        {
            ApplyVisibilityState(
                visible: false,
                userInitiated: false);
        }

        RestartInitialStateRoutine();
    }

    private void OnDisable()
    {
        UnbindButton();

        if (initialStateRoutine != null)
        {
            StopCoroutine(initialStateRoutine);
            initialStateRoutine = null;
        }
    }

    private void OnDestroy()
    {
        UnbindButton();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying ||
            !hideInEditMode ||
            !startHidden ||
            editorHideQueued)
        {
            return;
        }

        editorHideQueued = true;
        EditorApplication.delayCall +=
            ApplyEditorDefaultHiddenState;
#endif
    }

#if UNITY_EDITOR
    private void ApplyEditorDefaultHiddenState()
    {
        editorHideQueued = false;

        if (this == null ||
            Application.isPlaying ||
            !hideInEditMode ||
            !startHidden)
        {
            return;
        }

        ResolveReferences(allowAddCanvasGroup: false);

        isVisible = false;

        if (panelCanvasGroup != null)
        {
            Undo.RecordObject(
                panelCanvasGroup,
                "Hide Battle Analysis Panel");

            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;

            EditorUtility.SetDirty(panelCanvasGroup);
        }

        if (panelRoot != null &&
            panelRoot.activeSelf)
        {
            Undo.RecordObject(
                panelRoot,
                "Hide Battle Analysis Panel");

            panelRoot.SetActive(false);
            EditorUtility.SetDirty(panelRoot);
        }

        if (toggleButtonLabel != null)
        {
            Undo.RecordObject(
                toggleButtonLabel,
                "Update Battle Analysis Toggle Label");

            toggleButtonLabel.text = showLabel;
            EditorUtility.SetDirty(toggleButtonLabel);
        }

        if (gameObject.scene.IsValid() &&
            gameObject.scene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(
                gameObject.scene);
        }
    }
#endif

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
        ApplyVisibilityState(
            visible,
            userInitiated: true);
    }

    private void ApplyInitialState()
    {
        ApplyVisibilityState(
            visible: !startHidden,
            userInitiated: false);

        initialStateApplied = true;
    }

    private void RestartInitialStateRoutine()
    {
        if (!Application.isPlaying)
            return;

        if (initialStateRoutine != null)
            StopCoroutine(initialStateRoutine);

        initialStateRoutine =
            StartCoroutine(EnforceInitialStateAfterSceneStart());
    }

    private IEnumerator EnforceInitialStateAfterSceneStart()
    {
        // 다른 컴포넌트의 Awake/Start가 패널을 다시 켜더라도 첫 프레임 끝에 재보정한다.
        yield return null;
        yield return new WaitForEndOfFrame();

        initialStateRoutine = null;

        if (startHidden &&
            !visibilityChangedByUser)
        {
            ApplyVisibilityState(
                visible: false,
                userInitiated: false);
        }
    }

    private void ApplyVisibilityState(
        bool visible,
        bool userInitiated)
    {
        ResolveReferences(allowAddCanvasGroup: true);

        if (userInitiated)
            visibilityChangedByUser = true;

        isVisible = visible;
        initialStateApplied = true;

        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (isVisible)
        {
            if (panelRoot != null &&
                !panelRoot.activeSelf)
            {
                panelRoot.SetActive(true);
            }

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 1f;
                panelCanvasGroup.interactable = true;
                panelCanvasGroup.blocksRaycasts = true;
            }
        }
        else
        {
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
            }

            if (deactivatePanelWhenHidden &&
                panelRoot != null &&
                panelRoot.activeSelf)
            {
                panelRoot.SetActive(false);
            }
        }

        if (toggleButtonLabel != null)
        {
            toggleButtonLabel.text =
                isVisible
                    ? hideLabel
                    : showLabel;
        }
    }

    private void ResolveReferences(
        bool allowAddCanvasGroup)
    {
        if (panelRoot == null)
        {
            Transform panel =
                transform.Find("BattleAnalysisPanel");

            if (panel != null)
                panelRoot = panel.gameObject;
        }

        if (panelCanvasGroup == null &&
            panelRoot != null)
        {
            panelCanvasGroup =
                panelRoot.GetComponent<CanvasGroup>();

            if (panelCanvasGroup == null &&
                allowAddCanvasGroup)
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

        if (toggleButtonLabel == null &&
            toggleButton != null)
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
