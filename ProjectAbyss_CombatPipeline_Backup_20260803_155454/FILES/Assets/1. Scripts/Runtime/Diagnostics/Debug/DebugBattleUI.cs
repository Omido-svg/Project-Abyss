using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DebugBattleUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private TMP_Text text;

    [Header("Root")]
    [SerializeField] private GameObject viewRoot;
    [SerializeField] private CanvasGroup viewCanvasGroup;

    [Header("Display Options")]
    [SerializeField] private bool showCharactersSummary = true;
    [SerializeField] private bool showSelectedCharacter = true;
    [SerializeField] private bool showAllActionSlots = true;
    [SerializeField] private bool showBodyPartSkills = true;
    [SerializeField] private bool showStatusEffects = true;
    
    [Header("Initial Visibility")]
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private bool clearSelectedCharacterOnStart = true;
    

    [Header("Behavior")]
    [SerializeField] private bool showOnlyWhenCharacterSelected = true;
    [SerializeField] private bool forceAllViewOptionsOnStart = true;

    [Header("Layout Options")]
    [SerializeField] private bool autoResizeTextHeight = true;
    [SerializeField] private bool compactMode = true;
    [SerializeField] private int maxActionSlotsToShow = 12;

    [Header("Update Options")]
    [SerializeField] private bool updateEveryFrame = false;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

    private readonly StringBuilder sb = new();
    
    private const string C_TITLE = "#FF4FA3";
    private const string C_SECTION = "#7DD3FC";
    private const string C_LABEL = "#FDE68A";
    private const string C_VALUE = "#FFFFFF";
    private const string C_GOOD = "#86EFAC";
    private const string C_WARN = "#FACC15";
    private const string C_BAD = "#F87171";
    private const string C_INFO = "#C4B5FD";
    private const string C_SKILL = "#93C5FD";
    private const string C_TARGET = "#FDA4AF";
    private const string C_MUTED = "#A1A1AA";

    private string ColorText(string color, string value)
    {
        return $"<color={color}>{value}</color>";
    }

    private string Bold(string value)
    {
        return $"<b>{value}</b>";
    }

    private string Label(string value)
    {
        return ColorText(C_LABEL, Bold(value));
    }

    private string ValueText(object value)
    {
        return ColorText(C_VALUE, value.ToString());
    }

    private string SectionTitle(string title)
    {
        return ColorText(
            C_SECTION,
            Bold($"========== {title} =========="));
    }

    private string SmallMuted(string value)
    {
        return ColorText(C_MUTED, value);
    }

    private float refreshTimer;
    private LayoutElement cachedLayoutElement;
    private string lastRenderedText = string.Empty;
    private bool hasVisibilityState;
    private bool lastVisibility;

    private static bool loggedUpdateEveryFrameCorrection;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        loggedUpdateEveryFrameCorrection = false;
    }

    //--------------------------------------------------

    private void OnValidate()
    {
        // 런타임에서 매번 고치는 대신 Scene/Prefab에 저장되는 값 자체를 안전하게 유지한다.
        if (updateEveryFrame)
            updateEveryFrame = false;
    }

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (text == null)
            text = GetComponentInChildren<TMP_Text>(true);

        if (text != null)
            cachedLayoutElement = text.GetComponent<LayoutElement>();

        // 씬에 저장된 예전 updateEveryFrame=true 값도 런타임에서 안전하게 차단한다.
        if (updateEveryFrame)
        {
            updateEveryFrame = false;

            if (!loggedUpdateEveryFrameCorrection &&
                !BattleSimulationRuntime.IsBatchSimulation)
            {
                loggedUpdateEveryFrameCorrection = true;

                Debug.LogWarning(
                    "[DebugBattleUI] updateEveryFrame를 비활성화했습니다. " +
                    "캐릭터 선택 중 TMP와 Layout을 매 프레임 재빌드하면 큰 프레임 저하가 발생합니다.",
                    this);
            }
        }

        if (viewRoot != null && viewCanvasGroup == null)
            viewCanvasGroup = viewRoot.GetComponent<CanvasGroup>();

        if (forceAllViewOptionsOnStart)
            SetAllViewOptions(true);

        SetupText();

        ClearTextIfNeeded();
        SetViewVisible(false);
    }

    private void Start()
    {
        if (hideOnStart)
        {
            if (clearSelectedCharacterOnStart &&
                battleManager != null)
            {
                battleManager.SelectedCharacter = null;
            }

            ClearTextIfNeeded();
            SetViewVisible(false);
            return;
        }

        RefreshNow();
    }

    private void Update()
    {
        if (hideOnStart &&
            showOnlyWhenCharacterSelected &&
            battleManager != null &&
            battleManager.SelectedCharacter == null)
        {
            ClearTextIfNeeded();
            SetViewVisible(false);
            return;
        }

        // updateEveryFrame는 이전 직렬화 데이터 호환용으로만 남긴다.
        // 실제 갱신은 최소 간격을 두어 TMP / Canvas / Layout 재빌드 폭주를 막는다.
        refreshTimer += Time.unscaledDeltaTime;

        float safeInterval = Mathf.Max(0.05f, refreshInterval);

        if (refreshTimer < safeInterval)
            return;

        refreshTimer = 0f;
        RefreshNow();
    }

    //--------------------------------------------------

    private void SetupText()
    {
        if (text == null)
            return;

        text.richText = true;

        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;

        text.alignment = TextAlignmentOptions.TopLeft;

        text.enableAutoSizing = false;

        // 줄 간격 조금 증가
        text.lineSpacing = 5f;
    }

    //--------------------------------------------------

    public void RefreshNow()
    {
        if (battleManager == null)
        {
            SetViewVisible(false);
            return;
        }

        if (text == null)
        {
            SetViewVisible(false);
            return;
        }

        BattleContext context =
            battleManager.BattleContext;

        if (context == null)
        {
            ClearTextIfNeeded();
            SetViewVisible(false);
            return;
        }

        Character selected =
            battleManager.SelectedCharacter;

        if (showOnlyWhenCharacterSelected && selected == null)
        {
            ClearTextIfNeeded();
            SetViewVisible(false);
            return;
        }

        SetViewVisible(true);

        Refresh(context);
    }
    
    private void SetViewVisible(bool visible)
    {
        if (hasVisibilityState && lastVisibility == visible)
            return;

        hasVisibilityState = true;
        lastVisibility = visible;

        if (viewCanvasGroup != null)
        {
            viewCanvasGroup.alpha = visible ? 1f : 0f;
            viewCanvasGroup.interactable = visible;
            viewCanvasGroup.blocksRaycasts = visible;
            return;
        }

        if (viewRoot != null)
        {
            // DebugBattleUI가 붙어 있는 자기 자신은 끄면 안 됨
            if (viewRoot == gameObject)
            {
                Debug.LogWarning(
                    "viewRoot가 DebugBattleUI 자기 자신입니다. " +
                    "SetActive(false)를 쓰면 Update가 멈춥니다. CanvasGroup을 사용하세요.");
                return;
            }

            if (viewRoot.activeSelf != visible)
                viewRoot.SetActive(visible);

            return;
        }

        if (text != null)
        {
            text.enabled = visible;
        }
    }

    private void SetAllViewOptions(bool value)
    {
        showCharactersSummary = value;
        showSelectedCharacter = value;
        showAllActionSlots = value;
        showBodyPartSkills = value;
        showStatusEffects = value;
    }

    [ContextMenu("Set All View Options On")]
    private void SetAllViewOptionsOn()
    {
        SetAllViewOptions(true);
    }

    private void Reset()
    {
        showCharactersSummary = true;
        showSelectedCharacter = true;
        showAllActionSlots = true;
        showBodyPartSkills = true;
        showStatusEffects = true;

        autoResizeTextHeight = true;
        compactMode = true;
        maxActionSlotsToShow = 12;

        updateEveryFrame = false;
        refreshInterval = 0.25f;

        showOnlyWhenCharacterSelected = true;
        forceAllViewOptionsOnStart = true;
    }
    //--------------------------------------------------

    private void Refresh(BattleContext context)
    {
        sb.Clear();

        AppendBattleHeader(context);

        if (showCharactersSummary)
            AppendCharactersSummary(context);

        if (showSelectedCharacter)
            AppendSelectedCharacter();

        if (showAllActionSlots)
            AppendAllActionSlots();

        ApplyTextIfChanged(sb.ToString());
    }

    //--------------------------------------------------
    // Layout
    //--------------------------------------------------

    private void ApplyTextIfChanged(string value)
    {
        value ??= string.Empty;

        if (text == null ||
            string.Equals(lastRenderedText, value, System.StringComparison.Ordinal))
        {
            return;
        }

        lastRenderedText = value;
        text.SetText(value);
        ResizeTextHeight(value);
    }

    private void ClearTextIfNeeded()
    {
        if (text == null ||
            string.IsNullOrEmpty(lastRenderedText) && string.IsNullOrEmpty(text.text))
        {
            return;
        }

        lastRenderedText = string.Empty;
        text.SetText(string.Empty);
    }

    private void ResizeTextHeight(string value)
    {
        if (!autoResizeTextHeight || text == null)
            return;

        RectTransform rect = text.rectTransform;

        if (rect == null)
            return;

        // ForceMeshUpdate + RectTransform 직접 변경을 매 프레임 반복하면
        // LayoutGroup / ContentSizeFitter와 연쇄 재빌드가 발생한다.
        // 문자열이 실제로 바뀐 경우에만 preferred size를 계산한다.
        float availableWidth = Mathf.Max(1f, rect.rect.width);
        float preferredHeight = Mathf.Max(
            text.GetPreferredValues(value, availableWidth, 0f).y + 30f,
            100f);

        if (cachedLayoutElement != null)
        {
            if (Mathf.Abs(cachedLayoutElement.preferredHeight - preferredHeight) > 0.5f)
                cachedLayoutElement.preferredHeight = preferredHeight;

            return;
        }

        if (Mathf.Abs(rect.rect.height - preferredHeight) > 0.5f)
        {
            rect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                preferredHeight);
        }
    }

    //--------------------------------------------------
    // Battle Header
    //--------------------------------------------------

    private void AppendBattleHeader(BattleContext context)
    {
        sb.AppendLine(
            ColorText(C_TITLE, Bold("<size=115%>BATTLE DEBUG</size>")));

        int turn = 0;
        bool running = false;

        if (battleManager.TurnManager != null)
        {
            turn = battleManager.TurnManager.CurrentTurn;
            running = battleManager.TurnManager.IsBattleRunning;
        }

        string runningColor =
            running ? C_GOOD : C_BAD;

        sb.AppendLine($"{Label("Turn")}    : {ValueText(turn)}");
        sb.AppendLine($"{Label("Running")} : {ColorText(runningColor, Bold(running.ToString()))}");

        if (battleManager.MomentumManager != null)
        {
            int momentum =
                battleManager.MomentumManager.CurrentMomentum;

            string momentumColor =
                momentum > 0 ? C_GOOD :
                momentum < 0 ? C_BAD :
                C_VALUE;

            sb.AppendLine(
                $"{Label("Momentum")} : {ColorText(momentumColor, Bold(momentum.ToString()))}");
        }

        sb.AppendLine();
    }

    //--------------------------------------------------
    // Characters Summary
    //--------------------------------------------------

    private void AppendCharactersSummary(BattleContext context)
    {
        sb.AppendLine(SectionTitle("CHARACTERS"));

        if (context.Player != null)
        {
            AppendCharacterSummary("Player", context.Player);
        }
        else
        {
            sb.AppendLine($"{Label("Player")} : {ColorText(C_BAD, "NULL")}");
        }

        if (context.Enemies == null ||
            context.Enemies.Count == 0)
        {
            sb.AppendLine($"{Label("Enemies")} : {SmallMuted("None")}");
        }
        else
        {
            sb.AppendLine(Label("Enemies"));

            foreach (Character enemy in context.Enemies)
            {
                if (enemy == null)
                    continue;

                AppendCharacterSummary("-", enemy);
            }
        }

        sb.AppendLine();
    }

    private void AppendCharacterSummary(
        string prefix,
        Character character)
    {
        if (character == null)
            return;

        string hpColor =
            character.IsDead ? C_BAD : C_GOOD;

        string deadText =
            character.IsDead
                ? $" {ColorText(C_BAD, Bold("[DEAD]"))}"
                : "";

        sb.AppendLine(
            $"{ColorText(C_INFO, Bold(prefix))} " +
            $"{Bold(GetCharacterName(character))} " +
            $"{Label("HP")} {ColorText(hpColor, Bold($"{GetCurrentHP(character)}/{GetMaxHP(character)}"))}" +
            $"{deadText}");
    }

    //--------------------------------------------------
    // Selected Character
    //--------------------------------------------------

    private void AppendSelectedCharacter()
    {
        sb.AppendLine(SectionTitle("SELECTED CHARACTER"));

        Character selected =
            battleManager.SelectedCharacter;

        if (selected == null)
        {
            sb.AppendLine(SmallMuted("None"));
            sb.AppendLine();
            return;
        }

        AppendCharacterDetail(selected);

        sb.AppendLine();
    }

    //--------------------------------------------------
    // Character Detail
    //--------------------------------------------------

    private void AppendCharacterDetail(Character character)
    {
        if (character == null)
            return;

        CurrentStatus current =
            character.CurrentStatus;

        RuntimeStatus runtime =
            character.RuntimeStatus;

        string deadColor =
            character.IsDead ? C_BAD : C_GOOD;

        sb.AppendLine($"{Label("Name")} : {ColorText(C_INFO, Bold(GetCharacterName(character)))}");
        sb.AppendLine($"{Label("Dead")} : {ColorText(deadColor, Bold(character.IsDead.ToString()))}");
        sb.AppendLine();

        AppendStatus(character, current, runtime);

        if (showStatusEffects)
            AppendCharacterStatusEffects(character);

        AppendBodyParts(character);
    }

    //--------------------------------------------------
    // Status
    //--------------------------------------------------

    private void AppendStatus(
        Character character,
        CurrentStatus current,
        RuntimeStatus runtime)
    {
        sb.AppendLine(ColorText(C_SECTION, Bold("[Status]")));

        sb.AppendLine(
            $"{Label("HP")}       : " +
            $"{ColorText(C_GOOD, Bold($"{GetCurrentHP(character)}/{GetMaxHP(character)}"))}");

        if (current == null)
        {
            sb.AppendLine(ColorText(C_BAD, "CurrentStatus : NULL"));
            sb.AppendLine();
            return;
        }

        if (runtime != null)
        {
            sb.AppendLine(
                $"{Label("Prestige")} : " +
                $"{ColorText(C_INFO, Bold($"{runtime.currentPrestige}/{current.maxPrestige}"))}");

            string blockColor =
                runtime.currentBlock > 0 ? C_WARN : C_MUTED;

            sb.AppendLine(
                $"{Label("Block")}    : " +
                $"{ColorText(blockColor, Bold(runtime.currentBlock.ToString()))}");
        }
        else
        {
            sb.AppendLine(ColorText(C_BAD, "RuntimeStatus : NULL"));
        }

        sb.AppendLine($"{Label("Speed")}    : {ValueText($"{current.minSpeed} ~ {current.maxSpeed}")}");
        sb.AppendLine($"{Label("ATK+")}     : {ColorText(C_GOOD, Bold(current.flatDamageBonus.ToString()))}");
        sb.AppendLine($"{Label("DamageM")}  : {ColorText(C_GOOD, Bold(current.damageMultiplier.ToString("0.00")))}");
        sb.AppendLine($"{Label("Defense")}  : {ColorText(C_WARN, Bold(current.defense.ToString()))}");
        sb.AppendLine($"{Label("Pierce")}   : {ColorText(C_TARGET, Bold($"{current.defensePenetrationRate * 100f:0.#}%"))}");
        sb.AppendLine($"{Label("PrestigeGainM")} : {ColorText(C_INFO, Bold(current.prestigeGainMultiplier.ToString("0.00")))}");

        sb.AppendLine();
    }

    //--------------------------------------------------
    // Status Effects
    //--------------------------------------------------

    private void AppendCharacterStatusEffects(Character character)
    {
        sb.AppendLine(ColorText(C_SECTION, Bold("[Character Effects]")));

        IReadOnlyList<StatusEffect> effects =
            character.StatusEffects;

        if (effects == null || effects.Count == 0)
        {
            sb.AppendLine(SmallMuted("None"));
            sb.AppendLine();
            return;
        }

        foreach (StatusEffect effect in effects)
        {
            AppendEffect(effect, "- ");
        }

        sb.AppendLine();
    }

    private void AppendEffect(
        StatusEffect effect,
        string prefix)
    {
        if (effect == null)
            return;

        string durationText =
            effect.Duration < 0
                ? "Permanent"
                : $"{effect.Duration}T";

        sb.AppendLine(
            $"{prefix}" +
            $"{ColorText(C_WARN, Bold(effect.Name))} " +
            $"{SmallMuted($"Stack {effect.Stack} / {durationText}")}");
    }

    private void AppendPartStatusEffects(BodyPart part)
    {
        if (!showStatusEffects)
            return;

        IReadOnlyList<StatusEffect> effects =
            part.StatusEffects;

        if (effects == null || effects.Count == 0)
        {
            sb.AppendLine("    Effects : None");
            return;
        }

        sb.AppendLine("    Effects :");

        foreach (StatusEffect effect in effects)
        {
            AppendEffect(effect, "      - ");
        }
    }

    //--------------------------------------------------
    // Body Parts
    //--------------------------------------------------

    private void AppendBodyParts(Character character)
    {
        sb.AppendLine(ColorText(C_SECTION, Bold("[Body Parts]")));

        if (character.BodyParts == null ||
            character.BodyParts.Count == 0)
        {
            sb.AppendLine(SmallMuted("None"));
            sb.AppendLine();
            return;
        }

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            AppendBodyPart(character, part);

            if (!compactMode)
                sb.AppendLine();
        }

        sb.AppendLine();
    }

    private void AppendBodyPart(
        Character character,
        BodyPart part)
    {
        string stateColor =
            part.State switch
            {
                BodyPartState.Normal => C_GOOD,
                BodyPartState.Weakened => C_WARN,
                BodyPartState.Broken => C_BAD,
                _ => C_VALUE
            };

        string usableColor =
            part.IsUsable ? C_GOOD : C_BAD;

        sb.AppendLine(
            $"- {ColorText(C_INFO, Bold(part.Type.ToString()))} " +
            $"{ColorText(stateColor, Bold($"[{part.State}]"))} " +
            $"{Label("HP")} {ColorText(C_GOOD, Bold($"{part.PartHP:0}/{part.MaxPartHP:0}"))} " +
            $"{Label("SPD")} {ColorText(C_WARN, Bold(GetPartSpeed(part).ToString()))} " +
            $"{Label("Usable")} {ColorText(usableColor, Bold(part.IsUsable.ToString()))}");

        ActionSlot slot =
            GetSlot(character, part);

        if (slot == null)
        {
            sb.AppendLine($"    {Label("Slot")} : {SmallMuted("None")}");
        }
        else
        {
            AppendSlotCompact(slot, "    ");
        }

        if (showBodyPartSkills)
        {
            AppendSkills(character, part);
        }

        AppendPartStatusEffects(part);
    }

    //--------------------------------------------------
    // Skills
    //--------------------------------------------------

    private void AppendSkills(
        Character character,
        BodyPart part)
    {
        IReadOnlyList<Skill> skills =
            part.AvailableSkills;

        if (skills == null || skills.Count == 0)
        {
            sb.AppendLine($"    {Label("Skills")} : {SmallMuted("None")}");
            return;
        }

        sb.AppendLine($"    {Label("Skills")}");

        foreach (Skill skill in skills)
        {
            if (skill == null)
                continue;

            bool canUse =
                character.CanUseSkill(part, skill);

            string usable =
                canUse
                    ? ColorText(C_GOOD, Bold("OK"))
                    : ColorText(C_BAD, Bold("BLOCKED"));

            string actionColor =
                GetActionTypeColor(skill.ActionType);

            sb.AppendLine(
                $"      - {ColorText(C_SKILL, Bold(skill.SkillName))} " +
                $"{ColorText(actionColor, Bold($"[{skill.ActionType}]"))} " +
                $"{Label("PWR")} {ColorText(C_WARN, Bold($"{skill.MinPower}~{skill.MaxPower}"))} " +
                $"{usable}");
        }
    }
    
    private string GetActionTypeColor(ActionType actionType)
    {
        return actionType switch
        {
            ActionType.NormalAttack => "#FFFFFF",
            ActionType.Duel => "#93C5FD",
            ActionType.Preparation => "#FACC15",
            ActionType.Prestige => "#C084FC",
            _ => C_VALUE
        };
    }

    //--------------------------------------------------
    // All Action Slots
    //--------------------------------------------------

    private void AppendAllActionSlots()
    {
        sb.AppendLine(SectionTitle("ACTION SLOTS"));

        if (battleManager.ActionManager == null)
        {
            sb.AppendLine(ColorText(C_BAD, "ActionManager : NULL"));
            sb.AppendLine();
            return;
        }

        IReadOnlyList<ActionSlot> slots =
            battleManager.ActionManager.Slots;

        if (slots == null || slots.Count == 0)
        {
            sb.AppendLine(SmallMuted("None"));
            sb.AppendLine();
            return;
        }

        int count =
            Mathf.Min(
                slots.Count,
                Mathf.Max(0, maxActionSlotsToShow));

        for (int i = 0; i < count; i++)
        {
            ActionSlot slot =
                slots[i];

            sb.AppendLine(ColorText(C_INFO, Bold($"[{i}]")));
            AppendSlot(slot, compactMode ? "  " : "");
            sb.AppendLine();
        }

        if (slots.Count > count)
        {
            sb.AppendLine(
                SmallMuted($"... and {slots.Count - count} more slots"));
        }

        sb.AppendLine();
    }

    private void AppendSlotCompact(
        ActionSlot slot,
        string indent)
    {
        if (slot == null)
        {
            sb.AppendLine($"{indent}{Label("Slot")} : {ColorText(C_BAD, "NULL")}");
            return;
        }

        string skillName =
            slot.Skill == null
                ? "NULL"
                : slot.Skill.SkillName;

        string targetName =
            GetCharacterName(slot.TargetCharacter);

        string targetPartName =
            slot.TargetPart == null
                ? "NULL"
                : slot.TargetPart.Type.ToString();

        string phaseColor =
            GetPhaseColor(slot.Phase);

        sb.AppendLine(
            $"{indent}{Label("Slot")} : " +
            $"{ColorText(C_SKILL, Bold(skillName))} / " +
            $"{ColorText(phaseColor, Bold(slot.Phase.ToString()))} / " +
            $"{Label("SPD")} {ColorText(C_WARN, Bold(slot.Speed.ToString()))} / " +
            $"{Label("Target")} {ColorText(C_TARGET, Bold($"{targetName} {targetPartName}"))}");
    }
    
    private string GetPhaseColor(ActionPhase phase)
    {
        return phase switch
        {
            ActionPhase.PRETURN => "#C084FC",
            ActionPhase.FORESIGHT => "#FACC15",
            ActionPhase.COMBAT => "#93C5FD",
            _ => C_VALUE
        };
    }

    private void AppendSlot(
        ActionSlot slot,
        string indent)
    {
        if (slot == null)
        {
            sb.AppendLine($"{indent}Slot : NULL");
            return;
        }

        if (compactMode)
        {
            AppendSlotCompact(slot, indent);
            return;
        }

        string ownerName =
            GetCharacterName(slot.Owner);

        string partName =
            slot.Part == null
                ? "NULL"
                : slot.Part.Type.ToString();

        string skillName =
            slot.Skill == null
                ? "NULL"
                : slot.Skill.SkillName;

        string targetName =
            GetCharacterName(slot.TargetCharacter);

        string targetPartName =
            slot.TargetPart == null
                ? "NULL"
                : slot.TargetPart.Type.ToString();

        string targetSlotText =
            "None";

        if (slot.TargetSlot != null)
        {
            string targetSlotOwner =
                GetCharacterName(slot.TargetSlot.Owner);

            string targetSlotPart =
                slot.TargetSlot.Part == null
                    ? "NULL"
                    : slot.TargetSlot.Part.Type.ToString();

            targetSlotText =
                $"{targetSlotOwner} / {targetSlotPart}";
        }

        sb.AppendLine($"{indent}Owner      : {ownerName}");
        sb.AppendLine($"{indent}Part       : {partName}");
        sb.AppendLine($"{indent}Skill      : {skillName}");
        sb.AppendLine($"{indent}Speed      : {slot.Speed}");
        sb.AppendLine($"{indent}Phase      : {slot.Phase}");
        sb.AppendLine($"{indent}Target     : {targetName}");
        sb.AppendLine($"{indent}TargetPart : {targetPartName}");
        sb.AppendLine($"{indent}TargetSlot : {targetSlotText}");
    }

    //--------------------------------------------------
    // Utility
    //--------------------------------------------------

    private ActionSlot GetSlot(
        Character character,
        BodyPart part)
    {
        if (battleManager == null)
            return null;

        if (battleManager.ActionManager == null)
            return null;

        return battleManager.ActionManager.FindSlot(
            character,
            part);
    }

    private int GetPartSpeed(BodyPart part)
    {
        if (battleManager == null)
            return 0;

        if (battleManager.SpeedManager == null)
            return 0;

        return battleManager.SpeedManager.GetSpeed(part);
    }

    private string GetCharacterName(Character character)
    {
        if (character == null)
            return "NULL";

        if (character.Data == null)
            return character.name;

        return character.Data.CharacterName;
    }

    private int GetCurrentHP(Character character)
    {
        if (character == null)
            return 0;

        if (character.RuntimeStatus == null)
            return 0;

        return character.RuntimeStatus.currentHP;
    }

    private int GetMaxHP(Character character)
    {
        if (character == null)
            return 0;

        if (character.BodyParts == null)
            return 0;

        int maxHP = 0;

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            maxHP += Mathf.RoundToInt(part.MaxPartHP);
        }

        return maxHP;
    }
}