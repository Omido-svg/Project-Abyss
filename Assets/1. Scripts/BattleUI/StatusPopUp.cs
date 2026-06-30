using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class StatusPopup : MonoBehaviour
{
    private static StatusPopup currentSelected;

    [Header("Reference")]
    [SerializeField] private Character character;
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private OutlineController outline;
    [SerializeField] private BattleManager battleManager;

    [Header("Input")]
    [SerializeField] private KeyCode selectPlayerKey = KeyCode.Space;
    [SerializeField] private int mouseButtonSelect = 0;
    [SerializeField] private int mouseButtonDeselect = 1;

    [Header("Popup")]
    [SerializeField] private Vector3 focusOffset = new Vector3(0f, 2f, 0f);

    private Camera mainCamera;
    private CameraController cameraController;

    private bool isSelected;

    //--------------------------------------------------

    private void Awake()
    {
        if (character == null)
            character = GetComponent<Character>();

        if (outline == null)
            outline = GetComponent<OutlineController>();
    }

    private void Start()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        mainCamera = Camera.main;
        cameraController = FindFirstObjectByType<CameraController>();

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    private void OnDisable()
    {
        if (currentSelected == this)
            currentSelected = null;

        isSelected = false;

        if (popupRoot != null)
            popupRoot.SetActive(false);

        outline?.DisableOutline();
    }

    //--------------------------------------------------

    private void Update()
    {
        HandleInput();

        if (popupRoot == null)
            return;

        if (!popupRoot.activeSelf)
            return;

        FaceCamera();

        Refresh();
    }

    //--------------------------------------------------

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(mouseButtonSelect))
        {
            if (IsMouseOverThisCharacter())
            {
                Select();
            }
        }

        if (Input.GetMouseButtonDown(mouseButtonDeselect))
        {
            Deselect();
        }

        if (Input.GetKeyDown(selectPlayerKey))
        {
            SelectPlayer();
        }
    }

    //--------------------------------------------------

    private bool IsMouseOverThisCharacter()
    {
        if (mainCamera == null)
            return false;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return false;

        StatusPopup popup =
            hit.transform.GetComponentInParent<StatusPopup>();

        return popup == this;
    }

    //--------------------------------------------------

    private void Select()
    {
        if (character == null)
            return;

        if (currentSelected != null && currentSelected != this)
        {
            currentSelected.Deselect();
        }

        currentSelected = this;
        isSelected = true;

        if (battleManager != null)
        {
            battleManager.SelectedCharacter = character;
        }

        if (popupRoot != null)
            popupRoot.SetActive(true);

        Refresh();

        outline?.EnableOutline();

        cameraController?.Focus(transform.position + focusOffset);
    }

    //--------------------------------------------------

    private void Deselect()
    {
        if (!isSelected)
            return;

        isSelected = false;

        if (battleManager != null &&
            battleManager.SelectedCharacter == character)
        {
            battleManager.SelectedCharacter = null;
        }

        if (popupRoot != null)
            popupRoot.SetActive(false);

        outline?.DisableOutline();

        cameraController?.Return();

        if (currentSelected == this)
            currentSelected = null;
    }

    //--------------------------------------------------

    private void SelectPlayer()
    {
        if (battleManager == null)
            return;

        if (battleManager.BattleContext == null)
            return;

        Character player = battleManager.BattleContext.Player;

        if (player == null)
        {
            Debug.Log("Player not found");
            return;
        }

        StatusPopup popup =
            player.GetComponent<StatusPopup>();

        if (popup != null)
        {
            popup.Select();
        }
    }

    //--------------------------------------------------

    private void FaceCamera()
    {
        if (mainCamera == null)
            return;

        popupRoot.transform.LookAt(
            popupRoot.transform.position +
            mainCamera.transform.rotation * Vector3.forward,
            mainCamera.transform.rotation * Vector3.up);
    }

    //--------------------------------------------------

    private void Refresh()
    {
        if (character == null)
            return;

        if (statusText == null)
            return;

        StringBuilder sb = new();

        AppendHeader(sb);
        AppendCharacterStatus(sb);
        AppendCharacterEffects(sb);
        AppendBodyParts(sb);

        statusText.text = sb.ToString();
    }

    //--------------------------------------------------
    // Header
    //--------------------------------------------------

    private void AppendHeader(StringBuilder sb)
    {
        string characterName =
            character.Data == null
                ? character.name
                : character.Data.CharacterName;

        sb.AppendLine($"<b><size=120%>{characterName}</size></b>");

        if (character.IsDead)
        {
            sb.AppendLine("<color=#FF4B4B><b>[DEAD]</b></color>");
        }

        sb.AppendLine();
    }

    //--------------------------------------------------
    // Character Status
    //--------------------------------------------------

    private void AppendCharacterStatus(StringBuilder sb)
    {
        CurrentStatus c = character.CurrentStatus;
        RuntimeStatus r = character.RuntimeStatus;

        sb.AppendLine("<b>[ Character ]</b>");

        sb.AppendLine(
            $"<color=#FF4B4B><b>HP</b></color> : " +
            $"{character.CurrentHP}");

        if (r != null && c != null)
        {
            sb.AppendLine(
                $"<color=#C084FF><b>Prestige</b></color> : " +
                $"{r.currentPrestige}/{c.maxPrestige}");

            sb.AppendLine(
                $"<color=#FFD966><b>Speed</b></color> : " +
                $"{c.minSpeed} ~ {c.maxSpeed}");

            sb.AppendLine(
                $"<color=#A7F3D0><b>Damage Mult</b></color> : " +
                $"{c.damageMultiplier:0.00}");
        }

        sb.AppendLine();
    }

    //--------------------------------------------------
    // Character Effects
    //--------------------------------------------------

    private void AppendCharacterEffects(StringBuilder sb)
    {
        sb.AppendLine("<b>[ Character Status Effects ]</b>");

        IReadOnlyList<StatusEffect> effects =
            character.StatusEffects;

        if (effects == null || effects.Count == 0)
        {
            sb.AppendLine("- None");
            sb.AppendLine();
            return;
        }

        foreach (StatusEffect effect in effects)
        {
            AppendEffectLine(sb, effect);
        }

        sb.AppendLine();
    }

    //--------------------------------------------------
    // Body Parts
    //--------------------------------------------------

    private void AppendBodyParts(StringBuilder sb)
    {
        sb.AppendLine("<b>[ Body Parts ]</b>");

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            AppendBodyPart(sb, part);
            sb.AppendLine();
        }
    }

    private void AppendBodyPart(
        StringBuilder sb,
        BodyPart part)
    {
        string stateColor = GetPartStateColor(part);

        sb.AppendLine(
            $"- <b>{part.Type}</b> " +
            $"<color={stateColor}>[{part.State}]</color>");

        sb.AppendLine(
            $"  HP : {part.PartHP:0}/{part.MaxPartHP:0}");

        int speed = GetPartSpeed(part);

        sb.AppendLine(
            $"  SPD : {speed}");

        ActionSlot slot = GetCurrentSlot(part);

        if (slot != null)
        {
            AppendSlotInfo(sb, slot);
        }
        else
        {
            sb.AppendLine("  SLOT : None");
        }

        AppendAvailableSkills(sb, part);
        AppendPartEffects(sb, part);
    }

    //--------------------------------------------------
    // Slot Info
    //--------------------------------------------------

    private void AppendSlotInfo(
        StringBuilder sb,
        ActionSlot slot)
    {
        string skillName =
            slot.Skill == null
                ? "NULL"
                : slot.Skill.SkillName;

        string targetName =
            slot.TargetCharacter == null ||
            slot.TargetCharacter.Data == null
                ? "NULL"
                : slot.TargetCharacter.Data.CharacterName;

        string targetPart =
            slot.TargetPart == null
                ? "NULL"
                : slot.TargetPart.Type.ToString();

        sb.AppendLine(
            $"  SLOT : {slot.Phase} / SPD {slot.Speed}");

        sb.AppendLine(
            $"  SKILL : {skillName}");

        sb.AppendLine(
            $"  TARGET : {targetName} / {targetPart}");
    }

    //--------------------------------------------------
    // Skills
    //--------------------------------------------------

    private void AppendAvailableSkills(
        StringBuilder sb,
        BodyPart part)
    {
        if (part.AvailableSkills == null ||
            part.AvailableSkills.Count == 0)
        {
            sb.AppendLine("  SKILLS : None");
            return;
        }

        sb.AppendLine("  SKILLS :");

        foreach (Skill skill in part.AvailableSkills)
        {
            if (skill == null)
                continue;

            string usable =
                character.CanUseSkill(part, skill)
                    ? "OK"
                    : "BLOCKED";

            sb.AppendLine(
                $"    - {skill.SkillName} " +
                $"[{skill.ActionType}] " +
                $"PWR {skill.MinPower}~{skill.MaxPower} " +
                $"({usable})");
        }
    }

    //--------------------------------------------------
    // Part Effects
    //--------------------------------------------------

    private void AppendPartEffects(
        StringBuilder sb,
        BodyPart part)
    {
        IReadOnlyList<StatusEffect> effects =
            part.StatusEffects;

        if (effects == null || effects.Count == 0)
        {
            sb.AppendLine("  EFFECTS : None");
            return;
        }

        sb.AppendLine("  EFFECTS :");

        foreach (StatusEffect effect in effects)
        {
            sb.Append("    ");
            AppendEffectLine(sb, effect);
        }
    }

    private void AppendEffectLine(
        StringBuilder sb,
        StatusEffect effect)
    {
        if (effect == null)
            return;

        string durationText =
            effect.Duration < 0
                ? "Permanent"
                : $"{effect.Duration}T";

        sb.AppendLine(
            $"- {effect.Name} " +
            $"Stack {effect.Stack} / {durationText}");
    }

    //--------------------------------------------------
    // Utility
    //--------------------------------------------------

    private int GetPartSpeed(BodyPart part)
    {
        if (battleManager == null)
            return 0;

        if (battleManager.SpeedManager == null)
            return 0;

        return battleManager.SpeedManager.GetSpeed(part);
    }

    private ActionSlot GetCurrentSlot(BodyPart part)
    {
        if (battleManager == null)
            return null;

        if (battleManager.ActionManager == null)
            return null;

        return battleManager.ActionManager.FindSlot(
            character,
            part);
    }

    private string GetPartStateColor(BodyPart part)
    {
        if (part == null)
            return "#FFFFFF";

        if (part.IsBroken)
            return "#FF4B4B";

        if (part.IsWeakened)
            return "#FFD966";

        return "#A7F3D0";
    }
}