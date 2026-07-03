using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BodyPartButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private BattleUIManager uiManager;
    [SerializeField] private TMP_Text buttonText;
    
    [SerializeField] private Image buttonImage;
    
    [Header("Button Colors")]
    [SerializeField] private Color ownerSelectedColor = new Color(0.25f, 0.65f, 1f, 1f);
    [SerializeField] private Color targetSelectedColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color weakenedColor = new Color(1f, 0.85f, 0.1f, 1f);

    private Color originalColor;
    private bool originalColorSaved;

    private Character owner;
    private BodyPart bodyPart;
    private RectTransform rectTransform;
    private Button button;

    public Character Owner => owner;
    public BodyPart BodyPart => bodyPart;
    public RectTransform RectTransform => rectTransform;

    //--------------------------------------------------

    private void Awake()
    {
        EnsureReferences();
    }

    private void Start()
    {
        Refresh();
    }

    private void EnsureReferences()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (button == null)
            button = GetComponent<Button>();

        if (buttonText == null)
            buttonText = GetComponentInChildren<TMP_Text>(true);

        if (buttonText != null)
        {
            buttonText.richText = true;
            buttonText.overflowMode = TMPro.TextOverflowModes.Overflow;
        }

        if (uiManager == null)
            uiManager = FindFirstObjectByType<BattleUIManager>();
            
        if (buttonImage == null)
            buttonImage = GetComponent<Image>();

        if (buttonImage != null && !originalColorSaved)
        {
            originalColor = buttonImage.color;
            originalColorSaved = true;
        }
    }

    //--------------------------------------------------

    public void Bind(
        Character owner,
        BodyPart bodyPart)
    {
        EnsureReferences();

        this.owner = owner;
        this.bodyPart = bodyPart;

        if (button == null)
        {
            Debug.LogWarning("BodyPartButton : Button 컴포넌트가 없습니다.");
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);

        Refresh();
    }

    //--------------------------------------------------

    public void Refresh()
    {
        if (uiManager == null)
            uiManager = FindFirstObjectByType<BattleUIManager>();

        if (uiManager == null)
            return;

        uiManager.RefreshBodyPartUI(bodyPart);
    }
    
    public void ApplyViewModel(BodyPartButtonViewModel viewModel)
    {
        EnsureReferences();

        if (buttonText != null)
        {
            string skillLine =
                string.IsNullOrEmpty(viewModel.SkillText)
                    ? ""
                    : "\n" + viewModel.SkillText;

            buttonText.text =
                viewModel.PartText + "\n" +
                viewModel.HpText + "\n" +
                viewModel.SpeedText +
                skillLine;
        }

        if (button != null)
        {
            button.interactable = viewModel.Interactable;
        }

        ApplyColor(viewModel);
    }

    private void ApplyColor(BodyPartButtonViewModel viewModel)
    {
        EnsureReferences();

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

        if (viewModel.IsWeakened)
        {
            buttonImage.color = weakenedColor;
            return;
        }

        buttonImage.color = originalColor;
    }

    //--------------------------------------------------

    private void OnClick()
    {
        EnsureReferences();

        if (uiManager == null)
        {
            Debug.LogWarning("BodyPartButton : uiManager가 연결되어 있지 않습니다.");
            return;
        }

        if (owner == null || bodyPart == null)
        {
            Debug.LogWarning("BodyPartButton : owner 또는 bodyPart가 없습니다.");
            return;
        }

        Debug.Log(
            $"[BUTTON CLICK] {owner.Data.CharacterName} / {bodyPart.Type}");

        uiManager.OnBodyPartClicked(owner, bodyPart);
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        EnsureReferences();

        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (uiManager == null)
            return;

        uiManager.OnBodyPartRightClicked(owner, bodyPart);
    }
    
    public bool IsHiddenByBroken()
    {
        return bodyPart != null && bodyPart.IsBroken;
    }
}