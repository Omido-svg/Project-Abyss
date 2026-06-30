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

    private Camera mainCamera;
    private CameraController cameraController;

    private bool isSelected;

    //--------------------------------------------------

    private void Start()
    {
        if (character == null)
            character = GetComponent<Character>();

        if (outline == null)
            outline = GetComponent<OutlineController>();

        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        mainCamera = Camera.main;
        cameraController = FindFirstObjectByType<CameraController>();

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    //--------------------------------------------------

    private void Update()
    {
        if (popupRoot == null)
            return;

        HandleInput();

        if (!popupRoot.activeSelf)
            return;

        if (mainCamera != null)
        {
            popupRoot.transform.LookAt(
                popupRoot.transform.position +
                mainCamera.transform.rotation * Vector3.forward,
                mainCamera.transform.rotation * Vector3.up);
        }

        Refresh();
    }

    //--------------------------------------------------

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (IsMouseOverThisCharacter())
            {
                Select();
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            Deselect();
        }

        if (Input.GetKeyDown(KeyCode.Space))
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

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            return hit.transform == transform;
        }

        return false;
    }

    //--------------------------------------------------

    private void Select()
    {
        // 기존 선택 해제
        if (currentSelected != null && currentSelected != this)
        {
            currentSelected.Deselect();
        }

        currentSelected = this;
        isSelected = true;

        // BattleManager 동기화
        if (battleManager != null)
        {
            battleManager.SelectedCharacter = character;
        }

        if (popupRoot != null)
            popupRoot.SetActive(true);

        Refresh();

        outline?.EnableOutline();

        cameraController?.Focus(transform.position + Vector3.up * 2f);
    }

    //--------------------------------------------------

    private void Deselect()
    {
        if (!isSelected)
            return;

        isSelected = false;

        // BattleManager 해제
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
        if (battleManager == null ||
            battleManager.BattleContext == null)
            return;

        Character player = battleManager.BattleContext.Player;

        if (player == null)
        {
            Debug.Log("Player not found");
            return;
        }

        StatusPopup popup = player.GetComponent<StatusPopup>();

        if (popup != null)
        {
            popup.Select();
        }
    }

    //--------------------------------------------------

    private void Refresh()
    {
        if (character == null ||
            statusText == null)
            return;

        CurrentStatus c = character.CurrentStatus;
        RuntimeStatus r = character.RuntimeStatus;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine($"<b>{character.Data.CharacterName}</b>");
        sb.AppendLine();

        //--------------------------------
        // 전체 상태
        //--------------------------------
        sb.AppendLine($"<color=#FF4B4B><b>HP</b></color> : {character.CurrentHP}");
        sb.AppendLine($"<color=#C084FF><b>Prestige</b></color> : {r.currentPrestige}/{c.maxPrestige}");
        sb.AppendLine();

        //--------------------------------
        // Body Parts 정보
        //--------------------------------
        sb.AppendLine("<b>Body Parts</b>");

        foreach (BodyPart part in character.BodyParts)
        {
            sb.Append($"- {part.Type} ");

            if (part.IsBroken)
            {
                sb.Append("[BROKEN]");
            }
            else
            {
                sb.Append(
                    $"HP {part.PartHP}/{part.PartMaxHP} | " +
                    $"SPD {part.CurrentSpeed}"
                );

                if (part.CurrentSkill != null)
                {
                    sb.Append($" | SKILL {part.CurrentSkill.SkillName}");
                }
            }

            sb.AppendLine();
        }

        statusText.text = sb.ToString();
    }
}