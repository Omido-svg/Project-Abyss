using TMPro;
using UnityEngine;

public class StatusPopup : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Character character;
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private OutlineController outline;

    private Camera mainCamera;
    private CameraController cameraController;

    private bool isSelected;

    //--------------------------------------------------

    private void Awake()
    {
        if (character == null)
            character = GetComponent<Character>();

        mainCamera = Camera.main;
        cameraController = FindFirstObjectByType<CameraController>();

        outline = GetComponent<OutlineController>();

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
        // 좌클릭 → 선택 + 카메라 확대
        if (Input.GetMouseButtonDown(0))
        {
            if (IsMouseOverThisCharacter())
            {
                Select();
            }
        }

        // 우클릭 → 해제 + 원래 위치
        if (Input.GetMouseButtonDown(1))
        {
            Deselect();
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
        isSelected = true;
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
        popupRoot.SetActive(false);

        outline?.DisableOutline();

        cameraController?.Return();
    }

    //--------------------------------------------------

    private void Refresh()
    {
        BaseStatus b = character.BaseStatus;
        RuntimeStatus r = character.RuntimeStatus;

        statusText.text =
            $"<b>{character.CharacterName}</b>\n\n" +

            $"<color=#FF4B4B>HP</color> : {r.currentHP}/{b.maxHP}\n" +

            $"<color=#FF9A3C>ATK</color> : {b.attackLevel}\n" +
            $"<color=#4FA3FF>DEF</color> : {b.defenseLevel}\n\n" +

            $"<color=#FFD84D>Stagger</color> : {r.currentStagger}/{b.maxStagger}\n" +
            $"<color=#C084FF>Prestige</color> : {r.currentPrestige}/{b.maxPrestige}\n" +
            $"<color=#4FE3FF>Mentality</color> : {r.currentMentality}\n";
    }
}