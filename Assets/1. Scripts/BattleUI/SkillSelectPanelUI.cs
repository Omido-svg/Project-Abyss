using TMPro;
using UnityEngine;

public class SkillSelectPanelUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Optional Slot Header")]
    [SerializeField] private TMP_Text slotHeaderText;

    [Header("Skill Buttons")]
    [SerializeField] private SkillButtonUI normalAttackButton;
    [SerializeField] private SkillButtonUI duelButton;
    [SerializeField] private SkillButtonUI preparationButton;
    [SerializeField] private SkillButtonUI prestigeButton;

    private BattleUIManager uiManager;
    private BodyPart selectedPart;
    private int selectedActionIndex;
    private int maxActionSlots = 1;

    private void Awake()
    {
        EnsureReferences();
        Hide();
    }

    private void EnsureReferences()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Show(
        BattleUIManager manager,
        BodyPart part)
    {
        Show(manager, part, 0, 1);
    }

    public void Show(
        BattleUIManager manager,
        BodyPart part,
        int actionIndex,
        int maxSlots)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        EnsureReferences();

        uiManager = manager;
        selectedPart = part;
        maxActionSlots = Mathf.Max(1, maxSlots);
        selectedActionIndex =
            Mathf.Clamp(actionIndex, 0, maxActionSlots - 1);

        if (uiManager == null || selectedPart == null)
        {
            Hide();
            return;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (slotHeaderText != null)
        {
            slotHeaderText.text =
                $"행동 슬롯 #{selectedActionIndex + 1} / {maxActionSlots}";
        }

        BindSkillButton(normalAttackButton, ActionType.NormalAttack, "일반공격");
        BindSkillButton(duelButton, ActionType.Duel, "결투");
        BindSkillButton(preparationButton, ActionType.Preparation, "도사림");
        BindSkillButton(prestigeButton, ActionType.Prestige, "위세");

        transform.SetAsLastSibling();

        BattleDebugLog.SkillPanel(
            $"[SkillPanel] Show / Part={part.Type}, " +
            $"ActionIndex={selectedActionIndex}, MaxSlots={maxActionSlots}");
    }

    public void Hide()
    {
        selectedPart = null;
        selectedActionIndex = 0;
        maxActionSlots = 1;

        EnsureReferences();

        if (slotHeaderText != null)
            slotHeaderText.text = "";

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void BindSkillButton(
        SkillButtonUI skillButton,
        ActionType actionType,
        string defaultName)
    {
        if (skillButton == null)
            return;

        if (uiManager == null || selectedPart == null)
        {
            skillButton.Bind(
                $"{defaultName}\n<없음>",
                null,
                selectedActionIndex,
                false,
                null);
            return;
        }

        Skill skill =
            uiManager.FindSkillByActionType(
                selectedPart,
                actionType);

        bool usable =
            uiManager.IsSkillSelectable(
                selectedPart,
                skill);

        string label =
            skill == null
                ? $"{defaultName}\n<없음>"
                : !usable
                    ? $"{skill.SkillName}\n<비활성화>"
                    : skill.SkillName;

        skillButton.Bind(
            label,
            skill,
            selectedActionIndex,
            usable,
            uiManager.OnSkillSelectedFromPanel);
    }
}
