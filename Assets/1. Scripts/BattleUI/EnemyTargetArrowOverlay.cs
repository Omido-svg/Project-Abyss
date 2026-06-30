using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyTargetArrowOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform arrowRoot;

    [Header("Arrow Style")]
    [SerializeField] private Color enemyArrowColor = Color.red;
    [SerializeField] private float lineWidth = 6f;
    [SerializeField] private float arrowHeadLength = 28f;
    [SerializeField] private float arrowHeadWidth = 6f;
    [SerializeField] private float arrowHeadAngle = 28f;

    [Header("Padding")]
    [SerializeField] private float startPadding = 20f;
    [SerializeField] private float endPadding = 28f;

    [Header("Option")]
    [SerializeField] private bool showEnemyToPlayerOnly = true;
    [SerializeField] private bool debugLog = false;

    private readonly List<ArrowView> arrowPool = new();

    private BodyPartButton[] cachedButtons;
    private Sprite whiteSprite;

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (arrowRoot == null)
            arrowRoot = GetComponent<RectTransform>();

        CreateWhiteSprite();
        RefreshButtons();
    }

    private void LateUpdate()
    {
        DrawEnemyTargetArrows();
    }

    private void RefreshButtons()
    {
        cachedButtons =
            FindObjectsByType<BodyPartButton>(FindObjectsSortMode.None);
    }

    private void DrawEnemyTargetArrows()
    {
        HideAll();

        if (battleManager == null)
            return;

        if (battleManager.ActionManager == null)
            return;

        if (battleManager.BattleContext == null)
            return;

        if (arrowRoot == null)
            return;

        Character player = battleManager.BattleContext.Player;

        if (player == null)
            return;

        RefreshButtons();

        int arrowIndex = 0;

        foreach (ActionSlot slot in battleManager.ActionManager.Slots)
        {
            if (slot == null)
                continue;

            if (slot.Owner == null)
                continue;

            if (slot.Part == null)
                continue;

            if (slot.TargetCharacter == null)
                continue;

            if (slot.TargetPart == null)
                continue;

            // 적이 플레이어를 타겟으로 하는 화살표만 표시
            if (showEnemyToPlayerOnly)
            {
                if (slot.Owner == player)
                    continue;

                if (slot.TargetCharacter != player)
                    continue;
            }

            BodyPartButton fromButton =
                FindButton(slot.Owner, slot.Part);

            BodyPartButton toButton =
                FindButton(slot.TargetCharacter, slot.TargetPart);

            if (fromButton == null || toButton == null)
            {
                if (debugLog)
                {
                    Debug.Log(
                        $"[EnemyTargetArrowOverlay] 버튼 못 찾음 / " +
                        $"From : {GetCharacterName(slot.Owner)} {slot.Part.Type}, " +
                        $"To : {GetCharacterName(slot.TargetCharacter)} {slot.TargetPart.Type}");
                }

                continue;
            }

            if (!TryGetLocalCenter(fromButton.RectTransform, out Vector2 start))
                continue;

            if (!TryGetLocalCenter(toButton.RectTransform, out Vector2 end))
                continue;

            ArrowView arrow = GetArrow(arrowIndex);
            arrowIndex++;

            DrawArrow(
                arrow,
                start,
                end,
                enemyArrowColor);
        }
    }

    private BodyPartButton FindButton(
        Character character,
        BodyPart part)
    {
        if (cachedButtons == null)
            return null;

        // 1차: 정확한 참조 비교
        foreach (BodyPartButton button in cachedButtons)
        {
            if (button == null)
                continue;

            if (!button.gameObject.activeInHierarchy)
                continue;

            if (button.Owner == character &&
                button.BodyPart == part)
            {
                return button;
            }
        }

        // 2차: BodyPart 참조가 다를 경우 PartType 기준
        foreach (BodyPartButton button in cachedButtons)
        {
            if (button == null)
                continue;

            if (!button.gameObject.activeInHierarchy)
                continue;

            if (button.Owner == character &&
                button.BodyPart != null &&
                button.BodyPart.Type == part.Type)
            {
                return button;
            }
        }

        return null;
    }

    private bool TryGetLocalCenter(
        RectTransform target,
        out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        if (target == null)
            return false;

        Camera cam = GetCanvasCamera();

        Vector3 worldCenter =
            target.TransformPoint(target.rect.center);

        Vector2 screenPoint =
            RectTransformUtility.WorldToScreenPoint(
                cam,
                worldCenter);

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            arrowRoot,
            screenPoint,
            cam,
            out localPoint);
    }

    private Camera GetCanvasCamera()
    {
        if (canvas == null)
            return null;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    private ArrowView GetArrow(int index)
    {
        while (arrowPool.Count <= index)
        {
            ArrowView arrow = CreateArrow();
            arrowPool.Add(arrow);
        }

        arrowPool[index].SetActive(true);
        return arrowPool[index];
    }

    private ArrowView CreateArrow()
    {
        GameObject root = new GameObject("Enemy Target Arrow");
        root.transform.SetParent(arrowRoot, false);

        RectTransform rootRect =
            root.AddComponent<RectTransform>();

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        ArrowView arrow = new ArrowView();

        arrow.Root = root;
        arrow.Line = CreateImage(root.transform, "Line");
        arrow.HeadLeft = CreateImage(root.transform, "Head Left");
        arrow.HeadRight = CreateImage(root.transform, "Head Right");

        return arrow;
    }

    private Image CreateImage(
        Transform parent,
        string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Image image = obj.AddComponent<Image>();
        image.sprite = whiteSprite;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        return image;
    }

    private void DrawArrow(
        ArrowView arrow,
        Vector2 start,
        Vector2 end,
        Color color)
    {
        Vector2 direction = end - start;

        if (direction.sqrMagnitude <= 0.001f)
        {
            arrow.SetActive(false);
            return;
        }

        float distance = direction.magnitude;
        direction.Normalize();

        Vector2 paddedStart =
            start + direction * startPadding;

        Vector2 paddedEnd =
            end - direction * endPadding;

        DrawSegment(
            arrow.Line,
            paddedStart,
            paddedEnd,
            lineWidth,
            color);

        Vector2 backDirection =
            -direction;

        Vector2 leftDirection =
            Rotate(backDirection, arrowHeadAngle);

        Vector2 rightDirection =
            Rotate(backDirection, -arrowHeadAngle);

        DrawSegment(
            arrow.HeadLeft,
            paddedEnd,
            paddedEnd + leftDirection * arrowHeadLength,
            arrowHeadWidth,
            color);

        DrawSegment(
            arrow.HeadRight,
            paddedEnd,
            paddedEnd + rightDirection * arrowHeadLength,
            arrowHeadWidth,
            color);
    }

    private void DrawSegment(
        Image image,
        Vector2 start,
        Vector2 end,
        float width,
        Color color)
    {
        if (image == null)
            return;

        RectTransform rect = image.rectTransform;

        Vector2 direction = end - start;
        float length = direction.magnitude;

        if (length <= 0.001f)
        {
            image.enabled = false;
            return;
        }

        image.enabled = true;
        image.color = color;

        Vector2 center =
            (start + end) * 0.5f;

        float angle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        rect.anchoredPosition = center;
        rect.sizeDelta = new Vector2(length, width);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Vector2 Rotate(
        Vector2 vector,
        float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;

        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos);
    }

    private void HideAll()
    {
        foreach (ArrowView arrow in arrowPool)
        {
            if (arrow != null)
                arrow.SetActive(false);
        }
    }

    private void CreateWhiteSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        whiteSprite = Sprite.Create(
            texture,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f));
    }

    private string GetCharacterName(Character character)
    {
        if (character == null)
            return "NULL";

        if (character.Data == null)
            return character.name;

        return character.Data.CharacterName;
    }

    private class ArrowView
    {
        public GameObject Root;
        public Image Line;
        public Image HeadLeft;
        public Image HeadRight;

        public void SetActive(bool active)
        {
            if (Root != null)
                Root.SetActive(active);
        }
    }
}