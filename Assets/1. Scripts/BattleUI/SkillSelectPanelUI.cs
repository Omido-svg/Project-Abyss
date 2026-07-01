using UnityEngine;

public class SkillSelectPanelUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Skill Buttons")]
    [SerializeField] private SkillButtonUI normalAttackButton;
    [SerializeField] private SkillButtonUI duelButton;
    [SerializeField] private SkillButtonUI preparationButton;
    [SerializeField] private SkillButtonUI prestigeButton;

    private BattleUIManager uiManager;
    private BodyPart selectedPart;

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
        // 중요:
        // 오브젝트가 꺼져 있던 상태라면 SetActive(true) 순간 Awake가 실행될 수 있음.
        // 그래서 selectedPart 저장보다 SetActive(true)가 먼저 와야 함.
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        EnsureReferences();

        uiManager = manager;
        selectedPart = part;

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

        BindSkillButton(
            normalAttackButton,
            ActionType.NormalAttack,
            "일반공격");

        BindSkillButton(
            duelButton,
            ActionType.Duel,
            "결투");

        BindSkillButton(
            preparationButton,
            ActionType.Preparation,
            "도사림");

        BindSkillButton(
            prestigeButton,
            ActionType.Prestige,
            "위세");

        transform.SetAsLastSibling();

        Debug.Log(
            $"[SkillPanel] Show / Part : {selectedPart.Type}");
    }

    public void Hide()
    {
        selectedPart = null;

        EnsureReferences();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        // 중요:
        // 여기서 gameObject.SetActive(false) 하지 말 것.
        // 꺼버리면 다음 Show 때 Awake/Hide 타이밍 문제가 다시 생길 수 있음.
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

        string label;

        if (skill == null)
        {
            label = $"{defaultName}\n<없음>";
        }
        else if (!usable)
        {
            label = $"{skill.SkillName}\n<비활성화>";
        }
        else
        {
            label = skill.SkillName;
        }

        skillButton.Bind(
            label,
            skill,
            usable,
            uiManager.OnSkillSelectedFromPanel);
    }
}