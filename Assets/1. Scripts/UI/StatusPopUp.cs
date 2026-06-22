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

    private void Awake()
    {
        if (character == null)
            character = GetComponent<Character>();

        if (outline == null)
            outline = GetComponent<OutlineController>();

        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        mainCamera = Camera.main;
        cameraController = FindFirstObjectByType<CameraController>();

        popupRoot.SetActive(false);
    }

    //--------------------------------------------------

    private void Update()
    {
        HandleInput();

        if (!popupRoot.activeSelf)
            return;

        popupRoot.transform.LookAt(
            popupRoot.transform.position +
            mainCamera.transform.rotation * Vector3.forward,
            mainCamera.transform.rotation * Vector3.up);

        Refresh();
    }

    //--------------------------------------------------

    private void HandleInput()
    {
        // 좌클릭
        if (Input.GetMouseButtonDown(0))
        {
            if (IsMouseOverThisCharacter())
            {
                Select();
            }
        }

        // 우클릭
        if (Input.GetMouseButtonDown(1))
        {
            Deselect();
        }

        // Space → Player 선택
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SelectPlayer();
        }
    }

    //--------------------------------------------------

    private bool IsMouseOverThisCharacter()
    {
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
        if (currentSelected != null && currentSelected != this)
        {
            currentSelected.Deselect();
        }

        currentSelected = this;

        isSelected = true;

        // ★ BattleManager에 현재 선택된 캐릭터 저장
        if (battleManager != null)
        {
            battleManager.SelectedCharacter = character;
        }

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

        // ★ 현재 선택된 캐릭터였다면 해제
        if (battleManager != null &&
            battleManager.SelectedCharacter == character)
        {
            battleManager.SelectedCharacter = null;
        }

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

        Character player = battleManager.BattleContext.Player;

        if (player == null)
        {
            Debug.Log("Player 찾지 못함");
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
        BaseStatus b = character.BaseStatus;
        RuntimeStatus r = character.RuntimeStatus;

        statusText.text =
            $"<b>{character.CharacterName}</b>\n\n" +

            $"<color=#FF4B4B><b>HP</b></color> : {r.currentHP}/{b.maxHP}\n" +

            $"<color=#FF9A3C><b>ATK</b></color> : {b.attackLevel}\n" +
            $"<color=#4FA3FF><b>DEF</b></color> : {b.defenseLevel}\n\n" +

            $"<color=#FFD84D><b>Stagger</b></color> : {r.currentStagger}/{b.maxStagger}\n" +
            $"<color=#C084FF><b>Prestige</b></color> : {r.currentPrestige}/{b.maxPrestige}\n" +
            $"<color=#4FE3FF><b>Mentality</b></color> : {r.currentMentality}";
    }
}