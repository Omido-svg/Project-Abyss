using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Render-only view for one action index on a body part.
/// The presenter owns planning input and converts the runtime slot into text.
/// The existing Bind signature and serialized scene references are preserved.
/// </summary>
public sealed class ActionSlotButtonUI : MonoBehaviour, IPointerClickHandler
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

    private readonly ActionSlotButtonPresenter presenter = new();

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

    // Compatibility entry point used by BattleConfiguredSlotBoardUI and other callers.
    public void Bind(
        BattleUIManager manager,
        Character slotOwner,
        BodyPart part,
        int index,
        ActionSlot boundSlot,
        bool selected)
    {
        EnsureReferences();
        presenter.Bind(manager, slotOwner, part, index, boundSlot);
        Render(presenter.CreatePresentation(selected));
    }

    private void Render(ActionSlotButtonPresentation state)
    {
        if (label != null)
        {
            label.richText = true;
            label.text = state.Label;
        }

        if (background != null)
        {
            background.color = state.IsSelected
                ? selectedColor
                : state.HasAction
                    ? filledColor
                    : emptyColor;
        }

        if (button == null)
            return;

        // Preserve the previous Bind behavior for runtime-generated buttons.
        button.onClick.RemoveAllListeners();
        button.interactable = state.CanSelect;

        if (button.interactable)
            button.onClick.AddListener(SelectThisSlot);
    }

    private void SelectThisSlot()
    {
        presenter.OnPrimaryClick();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null ||
            eventData.button != PointerEventData.InputButton.Right)
        {
            return;
        }

        presenter.OnSecondaryClick();
    }

    private void EnsureReferences()
    {
        button ??= GetComponent<Button>();
        label ??= GetComponentInChildren<TMP_Text>(true);
        background ??= GetComponent<Image>();
    }
}
