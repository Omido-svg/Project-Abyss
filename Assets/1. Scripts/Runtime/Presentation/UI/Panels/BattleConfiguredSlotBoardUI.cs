using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 부위 버튼과 별개로 캐릭터의 실제 CharacterSlotConfig를 보여주는 슬롯 우선 UI.
/// 현재 전투 계획과 SlotId, 연결 부위, 속도, 비용을 한 곳에서 확인한다.
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
    private float nextRefresh;
    private string lastSignature;

    public void Configure(
        BattleManager manager,
        BattleUIManager uiManager,
        RectTransform contentRoot,
        Button template,
        TMP_Text emptyLabel)
    {
        battleManager = manager;
        battleUIManager = uiManager;
        content = contentRoot;
        buttonTemplate = template;
        emptyText = emptyLabel;
        Rebuild(force: true);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (battleManager != null)
            battleManager.BattlePrepared += HandleBattlePrepared;

        Rebuild(force: true);
    }

    private void OnDisable()
    {
        if (battleManager != null)
            battleManager.BattlePrepared -= HandleBattlePrepared;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh =
            Time.unscaledTime + refreshInterval;

        Rebuild(force: false);
    }

    private void HandleBattlePrepared(
        BattleContext _)
    {
        Rebuild(force: true);
    }

    private void ResolveReferences()
    {
        battleManager ??=
            FindFirstObjectByType<BattleManager>(
                FindObjectsInactive.Include);

        battleUIManager ??=
            FindFirstObjectByType<BattleUIManager>(
                FindObjectsInactive.Include);
    }

    private void Rebuild(bool force)
    {
        Character player =
            battleManager?.BattleContext?.Player;

        IReadOnlyList<CharacterSlotConfig> configs =
            player?.CombatRulesRuntime?
                .GetActiveSlotConfigs();

        string signature =
            BuildSignature(player, configs);

        if (!force &&
            string.Equals(
                signature,
                lastSignature,
                System.StringComparison.Ordinal))
        {
            return;
        }

        lastSignature = signature;
        ClearGenerated();

        if (content == null ||
            buttonTemplate == null ||
            player == null ||
            configs == null)
        {
            SetEmptyVisible(true);
            return;
        }

        int visibleCount = 0;

        foreach (CharacterSlotConfig config in configs)
        {
            if (config == null || !config.Enabled)
                continue;

            Button button =
                Instantiate(
                    buttonTemplate,
                    content);

            button.name =
                $"ConfiguredSlot_{config.SlotId}";
            button.gameObject.SetActive(true);

            TMP_Text label =
                button.GetComponentInChildren<TMP_Text>(true);

            BodyPart linkedPart =
                player.CombatRulesRuntime
                    .GetLinkedPart(config);

            ActionSlot planned =
                battleManager.ActionManager?
                    .FindSlot(
                        player,
                        config.SlotId);

            bool disabled =
                config.HasLinkedPart &&
                (linkedPart == null ||
                 linkedPart.IsBroken);

            if (label != null)
            {
                label.richText = true;
                label.text =
                    BuildLabel(
                        config,
                        linkedPart,
                        planned,
                        disabled);
            }

            CharacterSlotConfig capturedConfig = config;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(
                () => OpenSlot(
                    player,
                    capturedConfig));

            button.interactable =
                !disabled &&
                battleUIManager != null;

            generated.Add(button);
            visibleCount++;
        }

        SetEmptyVisible(visibleCount == 0);

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            content);
    }

    private void OpenSlot(
        Character owner,
        CharacterSlotConfig config)
    {
        if (owner == null ||
            config == null ||
            battleUIManager == null)
        {
            return;
        }

        BodyPart part =
            owner.CombatRulesRuntime?
                .GetLinkedPart(config);

        int actionIndex =
            owner.CombatRulesRuntime?
                .GetActionIndex(config) ?? -1;

        if (part == null)
        {
            Debug.LogWarning(
                $"[BattleConfiguredSlotBoardUI] " +
                $"{config.DisplayName}은 독립 슬롯입니다. " +
                "현재 캐릭터 입력기는 부위 연결 슬롯만 직접 편집할 수 있습니다.");
            return;
        }

        if (actionIndex < 0)
            return;

        battleUIManager.OpenOwnerActionSlot(
            owner,
            part,
            actionIndex);
    }

    private string BuildLabel(
        CharacterSlotConfig config,
        BodyPart linkedPart,
        ActionSlot planned,
        bool disabled)
    {
        string partText =
            config.HasLinkedPart
                ? linkedPart?.Type.ToString() ??
                  config.LinkedPartType.ToString()
                : "독립";

        string speedText =
            config.OverrideSpeedRange
                ? $"{config.MinSpeed}~{config.MaxSpeed}"
                : planned != null
                    ? planned.Speed.ToString()
                    : "기본";

        string skillText =
            planned?.Skill?.SkillName ??
            "행동 미선택";

        string targetText =
            planned?.TargetCharacter == null
                ? "-"
                : planned.TargetPart == null
                    ? planned.TargetCharacter.Data?.CharacterName ??
                      planned.TargetCharacter.name
                    : $"{planned.TargetCharacter.Data?.CharacterName ?? planned.TargetCharacter.name} " +
                      $"{planned.TargetPart.Type}";

        string costText =
            planned?.Skill == null
                ? "-"
                : planned.Skill.EnergyCost.ToString();

        string state =
            disabled
                ? "<color=#FCA5A5>행동 불능</color>"
                : planned == null
                    ? "<color=#C9D2E3>비어 있음</color>"
                    : "<color=#A7F3D0>계획 완료</color>";

        return
            $"<b>{config.DisplayName}</b>  " +
            $"<color=#C9D2E3>{config.SlotId}</color>\n" +
            $"{state} · 연결 {partText} · SPD {speedText}\n" +
            $"{skillText}  →  {targetText}  · 빛 {costText}";
    }

    private string BuildSignature(
        Character player,
        IReadOnlyList<CharacterSlotConfig> configs)
    {
        if (player == null || configs == null)
            return "NONE";

        System.Text.StringBuilder sb = new();

        sb.Append(player.GetInstanceID());
        sb.Append('|');
        sb.Append(player.CurrentEnergy);
        sb.Append('|');
        sb.Append(battleManager?.TurnManager?.CurrentTurn ?? 0);

        foreach (CharacterSlotConfig config in configs)
        {
            if (config == null)
                continue;

            sb.Append('|');
            sb.Append(config.SlotId);
            sb.Append(':');
            sb.Append(config.Enabled);

            ActionSlot slot =
                battleManager?.ActionManager?
                    .FindSlot(
                        player,
                        config.SlotId);

            sb.Append(':');
            sb.Append(slot?.ActionId ?? 0);
            sb.Append(':');
            sb.Append(slot?.Skill?.SkillName);
            sb.Append(':');
            sb.Append(slot?.TargetPart?.Type.ToString());

            BodyPart part =
                player.CombatRulesRuntime?
                    .GetLinkedPart(config);

            sb.Append(':');
            sb.Append(part?.State.ToString());
        }

        return sb.ToString();
    }

    private void SetEmptyVisible(bool visible)
    {
        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(visible);
            emptyText.text =
                visible
                    ? "사용 가능한 행동 슬롯이 없습니다."
                    : string.Empty;
        }
    }

    private void ClearGenerated()
    {
        for (int i = generated.Count - 1;
             i >= 0;
             i--)
        {
            Button button = generated[i];

            if (button == null)
                continue;

            button.gameObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(button.gameObject);
            else
                DestroyImmediate(button.gameObject);
        }

        generated.Clear();
    }

    private void OnDestroy()
    {
        ClearGenerated();
    }
}
