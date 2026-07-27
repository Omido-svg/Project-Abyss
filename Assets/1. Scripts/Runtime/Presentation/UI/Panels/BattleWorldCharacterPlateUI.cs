using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleWorldCharacterPlateUI : MonoBehaviour
{
    [SerializeField] private Character character;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Image hpFill;

    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 followLocalPosition;

    private bool hasHpOverride;
    private int hpOverride;

    public void Configure(
        Character target,
        Camera camera,
        TMP_Text displayName,
        TMP_Text hpValue,
        Image fill,
        Transform movementTarget,
        Vector3 localFollowPosition)
    {
        character = target;
        targetCamera = camera;
        nameText = displayName;
        hpText = hpValue;
        hpFill = fill;
        followTarget = movementTarget;
        followLocalPosition = localFollowPosition;

        ConfigureText(nameText, 29f, TextAlignmentOptions.Left);
        ConfigureText(hpText, 24f, TextAlignmentOptions.Right);
        ApplyFollowPosition();
        Refresh();
    }

    public void SetHpOverride(int hp)
    {
        hasHpOverride = true;
        hpOverride = Mathf.Max(0, hp);
        Refresh();
    }

    public void ClearHpOverride()
    {
        if (!hasHpOverride)
            return;

        hasHpOverride = false;
        Refresh();
    }

    public void RefreshNow()
    {
        Refresh();
    }

    private void Awake()
    {
        ConfigureText(nameText, 29f, TextAlignmentOptions.Left);
        ConfigureText(hpText, 24f, TextAlignmentOptions.Right);
        ResolveFollowTargetIfNeeded();
    }

    private void LateUpdate()
    {
        ResolveFollowTargetIfNeeded();
        ApplyFollowPosition();

        if (targetCamera == null ||
            !targetCamera.isActiveAndEnabled)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            // 위치는 캐릭터의 실제 이동 VisualRoot를 따르고,
            // 회전만 카메라와 일치시켜 월드 스페이스 UI가 항상 정면을 본다.
            transform.rotation = targetCamera.transform.rotation;
        }

        Refresh();
    }

    private void ResolveFollowTargetIfNeeded()
    {
        if (followTarget != null ||
            character == null)
        {
            return;
        }

        CharacterActionMover mover =
            character.GetComponent<CharacterActionMover>();

        followTarget =
            mover != null && mover.VisualRoot != null
                ? mover.VisualRoot
                : character.transform;

        followLocalPosition =
            followTarget.InverseTransformPoint(
                transform.position);
    }

    private void ApplyFollowPosition()
    {
        if (followTarget == null)
            return;

        transform.position =
            followTarget.TransformPoint(
                followLocalPosition);
    }

    private static void ConfigureText(
        TMP_Text text,
        float size,
        TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        text.richText = false;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.alignment = alignment;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private void Refresh()
    {
        if (character == null)
            return;

        int maximum = Mathf.Max(1, character.MaxCombatHP);
        int current = hasHpOverride
            ? Mathf.Clamp(hpOverride, 0, maximum)
            : Mathf.Clamp(character.CurrentHP, 0, maximum);

        if (nameText != null)
        {
            nameText.text =
                character.Data?.CharacterName ??
                character.name;
        }

        if (hpText != null)
            hpText.text = $"{current} / {maximum}";

        if (hpFill != null)
            hpFill.fillAmount = Mathf.Clamp01((float)current / maximum);

        gameObject.SetActive(!character.IsDead);
    }
}