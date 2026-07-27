using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 한 부위가 보유한 ActionIndex 하나를 표시하는 런타임 버튼이다.
/// 빈 슬롯도 선택 가능하며, 우클릭은 이미 등록된 행동만 제거한다.
/// </summary>
public sealed class ActionSlotButtonUI :
    MonoBehaviour,
    IPointerClickHandler
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image background;

    [Header("Selection Colors")]
    [SerializeField]
    private Color selectedColor = new(0.28f, 0.55f, 0.95f, 1f);

    [SerializeField]
    private Color filledColor = new(0.24f, 0.24f, 0.30f, 1f);

    [SerializeField]
    private Color emptyColor = new(0.13f, 0.13f, 0.16f, 0.92f);

    private BattleUIManager uiManager;
    private Character owner;
    private BodyPart ownerPart;
    private ActionSlot slot;
    private int actionIndex;

    private void Awake()
    {
        EnsureReferences();
    }

    public void ConfigureReferences(
        Button configuredButton,
        TMP_Text configuredLabel,
        Image configuredBackground = null)
    {
        button = configuredButton;
        label = configuredLabel;
        background = configuredBackground;
        EnsureReferences();
    }

    public void Bind(
        BattleUIManager manager,
        Character slotOwner,
        BodyPart part,
        int index,
        ActionSlot boundSlot,
        bool selected)
    {
        EnsureReferences();

        uiManager = manager;
        owner = slotOwner;
        ownerPart = part;
        actionIndex = Mathf.Max(0, index);
        slot = boundSlot;

        if (label != null)
        {
            label.richText = true;
            label.text = BuildLabel(selected);
        }

        if (background != null)
        {
            background.color = selected
                ? selectedColor
                : slot != null
                    ? filledColor
                    : emptyColor;
        }

        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.interactable =
            uiManager != null &&
            owner != null &&
            ownerPart != null;

        if (button.interactable)
        {
            button.onClick.AddListener(
                SelectThisSlot);
        }
    }

    private string BuildLabel(bool selected)
    {
        string marker = selected
            ? "<color=#93C5FD>▶</color> "
            : string.Empty;

        string indexText =
            $"{marker}<b>행동 #{actionIndex + 1}</b>";

        if (slot == null)
        {
            return
                $"{indexText}\n" +
                "<color=#A1A1AA>빈 슬롯</color>";
        }

        ActionSlotViewModel model =
            ActionSlotViewModel.FromSlot(slot);

        string skillText =
            model?.SkillText ?? "<없음>";

        string targetText =
            model?.TargetText ?? "대상 없음";

        return
            $"{indexText}\n" +
            $"{skillText}\n" +
            $"<size=80%>{targetText}</size>";
    }

    private void SelectThisSlot()
    {
        uiManager?.OpenOwnerActionSlot(
            owner,
            ownerPart,
            actionIndex);
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (eventData.button !=
            PointerEventData.InputButton.Right)
        {
            return;
        }

        if (slot == null)
            return;

        uiManager?.RemoveActionSlotAndContinueEditing(
            owner,
            ownerPart,
            actionIndex);
    }

    private void EnsureReferences()
    {
        button ??= GetComponent<Button>();
        label ??= GetComponentInChildren<TMP_Text>(true);
        background ??= GetComponent<Image>();
    }
}
