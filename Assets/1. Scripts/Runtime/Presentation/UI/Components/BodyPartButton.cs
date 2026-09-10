using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BodyPartButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private BattleUIManager uiManager;
    [SerializeField] private BodyPartButtonRegistry registry;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private Image buttonImage;
    [SerializeField] private BattlePortraitButtonView portraitView;

    [Header("Button Colors")]
    [SerializeField] private Color ownerSelectedColor = new(0.25f, 0.65f, 1f, 1f);
    [SerializeField] private Color targetSelectedColor = new(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color weakenedColor = new(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color brokenColor = new(0.45f, 0.15f, 0.15f, 1f);

    private Color originalColor;
    private bool originalColorSaved;

    [SerializeField, HideInInspector]
    private bool isTemplate;

    [SerializeField, HideInInspector]
    private bool isRuntimeGenerated;

    [SerializeField, HideInInspector]
    private BattleParticipantSide participantSide;

    [SerializeField, HideInInspector]
    private int participantIndex = -1;

    private Character owner;
    private BodyPart bodyPart;
    private RectTransform rectTransform;
    private Button button;

    private bool hasHpOverride;
    private int hpOverrideValue;
    private bool isReleasing;

    public Character Owner => owner;
    public BodyPart BodyPart => bodyPart;
    public RectTransform RectTransform => rectTransform;

    public bool IsTemplate => isTemplate;
    public bool IsRuntimeGenerated => isRuntimeGenerated;
    public BattleParticipantSide ParticipantSide => participantSide;
    public int ParticipantIndex => participantIndex;

    public bool IsCharacterTargetButton =>
        owner != null && bodyPart == null;

    public bool HasHpOverride => hasHpOverride;
    public int HpOverrideValue => hpOverrideValue;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        if (isReleasing)
            return;

        EnsureReferences();

        if (!isTemplate)
            registry?.Register(this);
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        registry?.Unregister(this);
    }

    private void EnsureReferences()
    {
        rectTransform ??= GetComponent<RectTransform>();
        button ??= GetComponent<Button>();
        buttonText ??= GetComponentInChildren<TMP_Text>(true);
        buttonImage ??= GetComponent<Image>();
        portraitView ??= GetComponent<BattlePortraitButtonView>();

        if (buttonText != null)
        {
            buttonText.richText = true;
            buttonText.overflowMode = TextOverflowModes.Overflow;
        }

        if (uiManager == null)
            uiManager = FindFirstObjectByType<BattleUIManager>();

        if (registry == null)
            registry = FindFirstObjectByType<BodyPartButtonRegistry>();

        if (buttonImage != null && !originalColorSaved)
        {
            originalColor = buttonImage.color;
            originalColorSaved = true;
        }
    }

    public void Bind(Character owner, BodyPart bodyPart)
    {
        EnsureReferences();

        bool bindingChanged =
            this.owner != owner ||
            this.bodyPart != bodyPart;

        this.owner = owner;
        this.bodyPart = bodyPart;

        if (bindingChanged)
        {
            hasHpOverride = false;
            hpOverrideValue = 0;
        }

        if (button == null)
        {
            Debug.LogWarning("BodyPartButton : Button 컴포넌트가 없습니다.");
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);

        if (!isTemplate)
            registry?.NotifyBindingChanged(this);

        Refresh();
    }

    public void ConfigureAsTemplate()
    {
        EnsureReferences();

        isTemplate = true;
        isRuntimeGenerated = false;
        participantIndex = -1;

        owner = null;
        bodyPart = null;

        registry?.Unregister(this);

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    public void ConfigureRuntime(
        BattleUIManager manager,
        BodyPartButtonRegistry targetRegistry,
        BattleParticipantSide side,
        int index)
    {
        isTemplate = false;
        isRuntimeGenerated = true;
        participantSide = side;
        participantIndex = index;

        if (manager != null)
            uiManager = manager;

        if (targetRegistry != null)
            registry = targetRegistry;

        EnsureReferences();
    }

    public void Refresh()
    {
        if (isReleasing)
            return;

        EnsureReferences();
        uiManager?.RefreshButton(this);
    }

    public void ApplyViewModel(BodyPartButtonViewModel viewModel)
    {
        if (isReleasing)
            return;

        EnsureReferences();

        if (button != null)
            button.interactable = viewModel.Interactable;

        if (portraitView != null)
        {
            portraitView.Apply(this, viewModel);
            return;
        }

        if (buttonText != null)
        {
            string slotLine = string.IsNullOrEmpty(viewModel.SlotText)
                ? ""
                : "\n" + viewModel.SlotText;

            string skillLine = string.IsNullOrEmpty(viewModel.SkillText)
                ? ""
                : "\n" + viewModel.SkillText;

            buttonText.text =
                viewModel.PartText + "\n" +
                viewModel.HpText + "\n" +
                viewModel.SpeedText +
                slotLine +
                skillLine;
        }

        ApplyColor(viewModel);
    }

    private void ApplyColor(BodyPartButtonViewModel viewModel)
    {
        if (buttonImage == null)
            return;

        if (viewModel.IsTargetSelected)
        {
            buttonImage.color = targetSelectedColor;
            return;
        }

        if (viewModel.IsOwnerSelected)
        {
            buttonImage.color = ownerSelectedColor;
            return;
        }

        if (viewModel.IsBroken)
        {
            buttonImage.color = brokenColor;
            return;
        }

        if (viewModel.IsWeakened)
        {
            buttonImage.color = weakenedColor;
            return;
        }

        buttonImage.color = originalColor;
    }

    private void OnClick()
    {
        if (BattlePlanningCameraController.IsCameraMovementActive)
            return;

        EnsureReferences();

        if (uiManager == null || owner == null)
            return;

        string targetText = bodyPart == null
            ? "SINGLE_HP"
            : bodyPart.Type.ToString();

        BattleDebugLog.UIInput(
            $"[BUTTON CLICK] {GetOwnerName()} / {targetText}");

        uiManager.OnBodyPartClicked(owner, bodyPart);
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (BattlePlanningCameraController.IsCameraMovementActive)
            return;

        if (eventData == null ||
            eventData.button !=
            PointerEventData.InputButton.Right)
        {
            return;
        }

        // 같은 GameObject의 Button/Portrait View로 우클릭이 전파되어
        // 상세 패널이 열리지 않도록 이 이벤트를 여기서 소비한다.
        eventData.Use();

        EnsureReferences();
        uiManager?.OnBodyPartRightClicked(
            owner,
            bodyPart);
    }


    public bool IsHiddenByBroken()
    {
        return bodyPart != null && bodyPart.IsBroken;
    }

    public void SetHpOverride(int hp)
    {
        hasHpOverride = true;
        hpOverrideValue = Mathf.Max(0, hp);
        Refresh();
    }

    public void ClearHpOverride()
    {
        if (!hasHpOverride)
            return;

        hasHpOverride = false;
        Refresh();
    }

    public void ReleaseForDestruction()
    {
        if (isReleasing)
            return;

        isReleasing = true;

        registry?.Unregister(this);

        if (button != null)
            button.onClick.RemoveListener(OnClick);

        portraitView?.PrepareForDestruction();

        owner = null;
        bodyPart = null;
        uiManager = null;
        registry = null;
    }

    private void OnDestroy()
    {
        ReleaseForDestruction();
    }

    private string GetOwnerName()
    {
        if (owner == null)
            return "NULL";

        return owner.Data == null
            ? owner.name
            : owner.Data.CharacterName;
    }
}