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

    [Header("Button Colors")]
    [SerializeField] private Color ownerSelectedColor = new(0.25f, 0.65f, 1f, 1f);
    [SerializeField] private Color targetSelectedColor = new(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color weakenedColor = new(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color brokenColor = new(0.45f, 0.15f, 0.15f, 1f);

    private Color originalColor;
    private bool originalColorSaved;

    private Character owner;
    private BodyPart bodyPart;
    private RectTransform rectTransform;
    private Button button;

    private bool hasHpOverride;
    private int hpOverrideValue;

    public Character Owner => owner;
    public BodyPart BodyPart => bodyPart;
    public RectTransform RectTransform => rectTransform;

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
        EnsureReferences();
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

        registry?.NotifyBindingChanged(this);
        Refresh();
    }

    public void Refresh()
    {
        EnsureReferences();
        uiManager?.RefreshButton(this);
    }

    public void ApplyViewModel(BodyPartButtonViewModel viewModel)
    {
        EnsureReferences();

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

        if (button != null)
            button.interactable = viewModel.Interactable;

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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        EnsureReferences();
        uiManager?.OnBodyPartRightClicked(owner, bodyPart);
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

    private string GetOwnerName()
    {
        if (owner == null)
            return "NULL";

        return owner.Data == null
            ? owner.name
            : owner.Data.CharacterName;
    }
}
