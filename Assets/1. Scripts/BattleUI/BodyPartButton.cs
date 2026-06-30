using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BodyPartButton : MonoBehaviour
{
    [SerializeField] private BattleUIManager uiManager;
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private TMP_Text buttonText;

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
            buttonText.overflowMode = TextOverflowModes.Overflow;
            buttonText.enableWordWrapping = false;
        }

        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (uiManager == null)
            uiManager = FindFirstObjectByType<BattleUIManager>();
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

        if (buttonText == null)
            return;

        if (owner == null || bodyPart == null)
        {
            buttonText.text = "NULL";
            return;
        }

        string partName = bodyPart.Type.ToString();
        string skillName = GetDisplaySkillName();
        string stateText = GetStateText();

        buttonText.text =
            $"{partName}\n" +
            $"{skillName}\n" +
            $"{stateText}";
    }

    //--------------------------------------------------

    private string GetDisplaySkillName()
    {
        ActionSlot slot = GetCurrentSlot();

        if (slot != null && slot.Skill != null)
            return slot.Skill.SkillName;

        if (bodyPart.AvailableSkills != null &&
            bodyPart.AvailableSkills.Count > 0 &&
            bodyPart.AvailableSkills[0] != null)
        {
            return bodyPart.AvailableSkills[0].SkillName;
        }

        return "No Skill";
    }

    //--------------------------------------------------

    private string GetStateText()
    {
        if (bodyPart.IsBroken)
            return "[BROKEN]";

        if (bodyPart.IsWeakened)
            return "[WEAKENED]";

        return $"HP {bodyPart.PartHP:0}/{bodyPart.MaxPartHP:0}";
    }

    //--------------------------------------------------

    private ActionSlot GetCurrentSlot()
    {
        if (battleManager == null)
            return null;

        if (battleManager.ActionManager == null)
            return null;

        return battleManager.ActionManager.FindSlot(
            owner,
            bodyPart);
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
}