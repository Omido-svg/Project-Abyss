using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class StatusPopup : MonoBehaviour
{
    private static StatusPopup currentSelected;
    private static StatusPopup pointerRaycastPopup;
    private static int pointerRaycastFrame = -1;

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
    [SerializeField, Min(0.1f)] private float focusDistance = 5f;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

    private Camera mainCamera;
    private BattleCameraDirector battleCameraDirector;
    private CameraController cameraController;

    private bool isSelected;
    private float nextRefreshTime;
    private readonly StringBuilder statusBuilder = new(512);
    private string lastRenderedText = string.Empty;

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
        battleCameraDirector = FindFirstObjectByType<BattleCameraDirector>();
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

        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime =
                Time.unscaledTime + refreshInterval;

            Refresh();
        }
    }

    //--------------------------------------------------

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(mouseButtonSelect))
        {
            if (!IsPointerOverUi() &&
                IsMouseOverThisCharacter())
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

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }

    //--------------------------------------------------

    private bool IsMouseOverThisCharacter()
    {
        if (pointerRaycastFrame == Time.frameCount)
            return pointerRaycastPopup == this;

        pointerRaycastFrame = Time.frameCount;
        pointerRaycastPopup = null;

        Camera activeCamera =
            GetMainCamera();

        if (activeCamera == null)
            return false;

        Ray ray =
            activeCamera.ScreenPointToRay(Input.mousePosition);

        RaycastHit[] hits =
            Physics.RaycastAll(
                ray,
                Mathf.Infinity,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

        float closestDistance = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            StatusPopup popup =
                hit.transform.GetComponentInParent<StatusPopup>();

            if (popup == null ||
                hit.distance >= closestDistance)
            {
                continue;
            }

            pointerRaycastPopup = popup;
            closestDistance = hit.distance;
        }

        return pointerRaycastPopup == this;
    }

    //--------------------------------------------------

    public void Show()
    {
        Select();
    }

    private void Select()
    {
        if (character == null)
            return;

        if (currentSelected != null && currentSelected != this)
        {
            currentSelected.DeselectWithoutCameraReturn();
        }

        currentSelected = this;
        isSelected = true;

        if (battleManager != null)
        {
            battleManager.SelectedCharacter = character;
        }

        if (popupRoot != null)
            popupRoot.SetActive(true);

        nextRefreshTime = 0f;
        Refresh();
        nextRefreshTime = Time.unscaledTime + refreshInterval;

        outline?.EnableOutline();

        FocusCamera();
    }
    
    private void DeselectWithoutCameraReturn()
    {
        isSelected = false;

        if (battleManager != null &&
            battleManager.SelectedCharacter == character)
        {
            battleManager.SelectedCharacter = null;
        }

        if (popupRoot != null)
            popupRoot.SetActive(false);

        outline?.DisableOutline();
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

        ReturnCamera();

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
        Camera activeCamera =
            GetMainCamera();

        if (activeCamera == null)
            return;

        popupRoot.transform.LookAt(
            popupRoot.transform.position +
            activeCamera.transform.rotation * Vector3.forward,
            activeCamera.transform.rotation * Vector3.up);
    }

    //--------------------------------------------------

    private void Refresh()
    {
        if (character == null || statusText == null)
            return;

        statusBuilder.Clear();

        AppendHeader(statusBuilder);
        AppendCompactCharacterStatus(statusBuilder);
        AppendCompactEffects(statusBuilder);
        AppendCompactBodyParts(statusBuilder);

        string nextText = statusBuilder.ToString();

        if (string.Equals(lastRenderedText, nextText, StringComparison.Ordinal))
            return;

        lastRenderedText = nextText;
        statusText.SetText(nextText);
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

    private List<ActionSlot> GetCurrentSlots(BodyPart part)
    {
        if (battleManager?.ActionManager == null ||
            character == null)
        {
            return new List<ActionSlot>();
        }

        return battleManager.ActionManager.FindSlots(
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

        List<ActionSlot> slots =
            GetCurrentSlots(part);

        sb.AppendLine(
            $"- <b>{part.Type}</b> " +
            $"<color={stateColor}>[{part.State}]</color> " +
            $"HP {part.PartHP:0}/{part.MaxPartHP:0} " +
            $"SPD {speed}");

        if (slots.Count == 0)
        {
            sb.AppendLine("  Skill : -");
        }
        else
        {
            foreach (ActionSlot slot in slots)
            {
                if (slot == null)
                    continue;

                string skillName =
                    slot.Skill == null
                        ? "-"
                        : slot.Skill.SkillName;

                string targetName =
                    slot.TargetCharacter == null
                        ? "대상 없음"
                        : slot.TargetCharacter.Data == null
                            ? slot.TargetCharacter.name
                            : slot.TargetCharacter.Data.CharacterName;

                string targetPartName =
                    slot.TargetPart == null
                        ? "SINGLE HP"
                        : slot.TargetPart.Type.ToString();

                sb.AppendLine(
                    $"  [#{slot.ActionIndex + 1}] {skillName} " +
                    $"→ {targetName}/{targetPartName}");
            }
        }

        sb.AppendLine(
            $"  Status: {partEffects}");
    }
    
    private CameraController GetCameraController()
    {
        if (cameraController != null)
            return cameraController;

        if (CameraController.Instance != null)
        {
            cameraController = CameraController.Instance;
            return cameraController;
        }

        cameraController = FindFirstObjectByType<CameraController>();
        return cameraController;
    }

    private Camera GetMainCamera()
    {
        if (mainCamera != null &&
            mainCamera.isActiveAndEnabled)
        {
            return mainCamera;
        }

        mainCamera = Camera.main;
        return mainCamera;
    }

    private BattleCameraDirector GetBattleCameraDirector()
    {
        if (battleCameraDirector != null)
            return battleCameraDirector;

        battleCameraDirector =
            FindFirstObjectByType<BattleCameraDirector>();

        return battleCameraDirector;
    }

    private void FocusCamera()
    {
        Vector3 focusPosition =
            transform.position + focusOffset;

        BattleCameraDirector director =
            GetBattleCameraDirector();

        if (director != null)
        {
            director.Focus(
                focusPosition,
                focusDistance);

            return;
        }

        GetCameraController()?.Focus(focusPosition);
    }

    private void ReturnCamera()
    {
        BattleCameraDirector director =
            GetBattleCameraDirector();

        if (director != null)
        {
            director.ReturnFromInteraction();
            return;
        }

        GetCameraController()?.Return();
    }
}