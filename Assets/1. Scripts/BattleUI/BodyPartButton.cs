using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BodyPartButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private BattleUIManager uiManager;
    [SerializeField] private BattleManager battleManager;
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

        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

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
        EnsureReferences();

        if (owner == null || bodyPart == null)
        {
            if (buttonText != null)
                buttonText.text = "NULL";

            return;
        }

        // 캐릭터가 죽었거나 부위가 파괴되면 버튼 숨김
        if (owner.IsDead || bodyPart.IsBroken)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (buttonText == null)
            return;

        buttonText.richText = true;

        string partName = bodyPart.Type.ToString();
        string speedText = GetSpeedText();
        string selectedSkillText = GetSelectedSkillText();

        if (string.IsNullOrEmpty(selectedSkillText))
        {
            buttonText.text =
                $"{partName}\n" +
                $"{speedText}";
        }
        else
        {
            buttonText.text =
                $"{partName}\n" +
                $"{speedText}\n" +
                $"{selectedSkillText}";
        }
    }
    
    private string GetSpeedText()
    {
        int speed = -1;

        ActionSlot slot = GetCurrentSlot();

        if (slot != null)
        {
            speed = slot.Speed;
        }
        else if (battleManager != null &&
                battleManager.SpeedManager != null &&
                bodyPart != null)
        {
            speed = battleManager.SpeedManager.GetSpeed(bodyPart);
        }

        if (speed < 0)
            return "<color=#FFD700><b>SPD -</b></color>";

        return $"<color=#FFD700><b>SPD {speed}</b></color>";
    }

    //--------------------------------------------------
    private string GetSelectedSkillText()
    {
        ActionSlot slot = GetCurrentSlot();

        if (slot == null)
            return "";

        if (slot.Skill == null)
            return "";

        return $"<color=#FFFFFF><b>{slot.Skill.SkillName}</b></color>";
    }

    //--------------------------------------------------

    private ActionSlot GetCurrentSlot()
    {
        if (battleManager == null)
            return null;

        if (battleManager.ActionManager == null)
            return null;

        ActionSlot slot =
            battleManager.ActionManager.FindSlot(
                owner,
                bodyPart);

        if (slot != null)
            return slot;

        foreach (ActionSlot actionSlot in battleManager.ActionManager.Slots)
        {
            if (actionSlot == null)
                continue;

            if (actionSlot.Owner != owner)
                continue;

            if (actionSlot.Part == null || bodyPart == null)
                continue;

            if (actionSlot.Part.Type == bodyPart.Type)
                return actionSlot;
        }

        return null;
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

        Refresh();
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
    
    public void SetNormalColor()
    {
        EnsureReferences();

        if (buttonImage == null)
            return;

        if (bodyPart != null && bodyPart.IsWeakened)
        {
            buttonImage.color = weakenedColor;
            return;
        }

        buttonImage.color = originalColor;
    }

    public void SetOwnerSelectedColor()
    {
        EnsureReferences();

        if (buttonImage == null)
            return;

        buttonImage.color = ownerSelectedColor;
    }

    public void SetTargetSelectedColor()
    {
        EnsureReferences();

        if (buttonImage == null)
            return;

        buttonImage.color = targetSelectedColor;
    }
    
    public bool IsHiddenByBroken()
    {
        return bodyPart != null && bodyPart.IsBroken;
    }
}