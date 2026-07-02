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
        AppendCompactCharacterStatus(sb);
        AppendCompactEffects(sb);
        AppendCompactBodyParts(sb);

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

        if (character.IsDead)
        {
            sb.AppendLine(
                $"<b><size=120%>{characterName}</size></b> " +
                "<color=#FF4B4B><b>[DEAD]</b></color>");
        }
        else
        {
            sb.AppendLine(
                $"<b><size=120%>{characterName}</size></b>");
        }

        sb.AppendLine();
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
    
    //--------------------------------------------------
    // Compact Character Status
    //--------------------------------------------------

    private void AppendCompactCharacterStatus(StringBuilder sb)
    {
        CurrentStatus c = character.CurrentStatus;
        RuntimeStatus r = character.RuntimeStatus;

        int hp = character.CurrentHP;

        sb.Append($"HP {hp}");

        if (r != null && c != null)
        {
            sb.Append($" / 위세 {r.currentPrestige}/{c.maxPrestige}");

            if (r.currentBlock > 0)
            {
                sb.Append($" / 방어도 {r.currentBlock}");
            }
        }

        sb.AppendLine();
        sb.AppendLine();
    }

    //--------------------------------------------------
    // Compact Effects
    //--------------------------------------------------

    private void AppendCompactEffects(StringBuilder sb)
    {
        sb.Append("<b>Status</b> : ");

        string effectNames =
            GetEffectNames(character.StatusEffects);

        if (string.IsNullOrEmpty(effectNames))
        {
            sb.AppendLine("None");
        }
        else
        {
            sb.AppendLine(effectNames);
        }

        sb.AppendLine();
    }

    private string GetEffectNames(
        IReadOnlyList<StatusEffect> effects)
    {
        if (effects == null || effects.Count == 0)
            return "";

        List<string> names = new();

        foreach (StatusEffect effect in effects)
        {
            if (effect == null)
                continue;

            if (string.IsNullOrEmpty(effect.Name))
                continue;

            names.Add(effect.Name);
        }

        if (names.Count == 0)
            return "";

        return string.Join(", ", names);
    }

    //--------------------------------------------------
    // Compact Body Parts
    //--------------------------------------------------

    private void AppendCompactBodyParts(StringBuilder sb)
    {
        sb.AppendLine("<b>Parts</b>");

        if (character.BodyParts == null)
        {
            sb.AppendLine("None");
            return;
        }

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            AppendCompactBodyPart(sb, part);
        }
    }

    private void AppendCompactBodyPart(
        StringBuilder sb,
        BodyPart part)
    {
        string stateColor =
            GetPartStateColor(part);

        int speed =
            GetPartSpeed(part);

        string partEffects =
            GetEffectNames(part.StatusEffects);

        if (string.IsNullOrEmpty(partEffects))
            partEffects = "-";

        ActionSlot slot =
            GetCurrentSlot(part);

        string skillName = "-";

        if (slot != null && slot.Skill != null)
            skillName = slot.Skill.SkillName;

        sb.AppendLine(
            $"- <b>{part.Type}</b> " +
            $"<color={stateColor}>[{part.State}]</color> " +
            $"HP {part.PartHP:0}/{part.MaxPartHP:0} " +
            $"SPD {speed}");

        sb.AppendLine(
            $"  Skill : {skillName}");

        sb.AppendLine(
            $"  Status: {partEffects}");
    }
}