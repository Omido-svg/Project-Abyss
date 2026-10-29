using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thin Unity View for character-configured action slots.
/// Preserves existing serialized fields and Configure signature.
/// </summary>
public sealed class BattleConfiguredSlotBoardUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleUIManager battleUIManager;
    [SerializeField] private RectTransform content;
    [SerializeField] private Button buttonTemplate;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.15f;

    private readonly List<Button> generated = new();
    private readonly BattleConfiguredSlotBoardPresenter presenter = new();
    private BattleManager subscribedBattleManager;
    private float nextRefresh;
    private string lastSignature;

    public void Configure(BattleManager manager, BattleUIManager uiManager,
        RectTransform contentRoot, Button template, TMP_Text emptyLabel)
    {
        battleManager = manager;
        battleUIManager = uiManager;
        content = contentRoot;
        buttonTemplate = template;
        emptyText = emptyLabel;
        if (isActiveAndEnabled) SubscribeCurrentBattleManager();
        Rebuild(force: true);
    }

    private void Awake() { ResolveReferences(); }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeCurrentBattleManager();
        Rebuild(force: true);
    }

    private void OnDisable() { UnsubscribeBattleManager(); }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;
        Rebuild(force: false);
    }

    private void HandleBattlePrepared(BattleContext _) { Rebuild(force: true); }

    private void ResolveReferences()
    {
        battleManager ??= FindFirstObjectByType<BattleManager>(FindObjectsInactive.Include);
        battleUIManager ??= FindFirstObjectByType<BattleUIManager>(FindObjectsInactive.Include);
    }

    private void SubscribeCurrentBattleManager()
    {
        if (ReferenceEquals(subscribedBattleManager, battleManager)) return;
        UnsubscribeBattleManager();
        subscribedBattleManager = battleManager;
        if (subscribedBattleManager != null)
            subscribedBattleManager.BattlePrepared += HandleBattlePrepared;
    }

    private void UnsubscribeBattleManager()
    {
        if (subscribedBattleManager != null)
            subscribedBattleManager.BattlePrepared -= HandleBattlePrepared;
        subscribedBattleManager = null;
    }

    private void Rebuild(bool force)
    {
        BattleConfiguredSlotBoardPresentation snapshot = presenter.Project(battleManager);
        if (!force && string.Equals(snapshot.Signature, lastSignature,
                System.StringComparison.Ordinal)) return;

        lastSignature = snapshot.Signature;
        ClearGenerated();
        if (content == null || buttonTemplate == null || snapshot.Items.Count == 0)
        {
            SetEmptyVisible(true);
            return;
        }

        foreach (BattleConfiguredSlotItem item in snapshot.Items)
        {
            Button button = Instantiate(buttonTemplate, content);
            button.name = $"ConfiguredSlot_{item.SlotId}";
            button.gameObject.SetActive(true);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.richText = true;
                label.text = item.Label;
            }

            BattleConfiguredSlotItem captured = item;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => presenter.TryOpenSlot(battleUIManager, captured));
            button.interactable = item.CanSelect && battleUIManager != null;
            generated.Add(button);
        }
        SetEmptyVisible(false);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void SetEmptyVisible(bool visible)
    {
        if (emptyText == null) return;
        emptyText.gameObject.SetActive(visible);
        emptyText.text = visible ? "사용 가능한 행동 슬롯이 없습니다." : string.Empty;
    }

    private void ClearGenerated()
    {
        for (int i = generated.Count - 1; i >= 0; i--)
        {
            Button button = generated[i];
            if (button == null) continue;
            button.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(button.gameObject);
            else DestroyImmediate(button.gameObject);
        }
        generated.Clear();
    }

    private void OnDestroy()
    {
        UnsubscribeBattleManager();
        ClearGenerated();
    }
}
