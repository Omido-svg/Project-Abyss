using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TargetArrowUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform arrowRoot;

    [Header("Draw Option")]
    [SerializeField] private bool showPlayerArrows = true;
    [SerializeField] private bool showEnemyTargetArrows = true;
    [SerializeField] private bool showEnemySideOnClash = true;

    [Header("Color")]
    [SerializeField] private Color playerArrowColor = Color.cyan;
    [SerializeField] private Color enemyClashArrowColor = Color.red;

    [Header("Line")]
    [SerializeField] private float lineThickness = 5f;
    [SerializeField] private float startPadding = 25f;
    [SerializeField] private float endPadding = 25f;

    [Header("Arrow Head")]
    [SerializeField] private float arrowHeadLength = 28f;
    [SerializeField] private float arrowHeadAngle = 35f;

    private readonly List<ArrowVisual> arrows = new();
    private BodyPartButton[] cachedButtons;

    private static Sprite whiteSprite;

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

        EnsureArrowRoot();
        CacheButtons();
    }

    private void LateUpdate()
    {
        Refresh();
    }
    
    private bool IsMutualClash(ActionSlot slot)
    {
        return FindOpposingClashSlot(slot) != null;
    }

    private ActionSlot FindOpposingClashSlot(ActionSlot slot)
    {
        if (slot == null)
            return null;

        if (battleManager == null)
            return null;

        if (battleManager.ActionManager == null)
            return null;

        if (slot.Owner == null ||
            slot.Part == null ||
            slot.TargetCharacter == null ||
            slot.TargetPart == null)
            return null;

        if (slot.Phase != ActionPhase.COMBAT)
            return null;

        if (slot.Skill == null || !slot.Skill.CanClash)
            return null;

        foreach (ActionSlot other in battleManager.ActionManager.Slots)
        {
            if (other == null)
                continue;

            if (other == slot)
                continue;

            if (other.Owner == null ||
                other.Part == null ||
                other.TargetCharacter == null ||
                other.TargetPart == null)
                continue;

            if (other.Phase != ActionPhase.COMBAT)
                continue;

            if (other.Skill == null || !other.Skill.CanClash)
                continue;

            // 내가 공격하는 대상의 행동 슬롯인가?
            if (other.Owner != slot.TargetCharacter)
                continue;

            if (!IsSamePart(other.Part, slot.TargetPart))
                continue;

            // 그 대상이 다시 나의 같은 부위를 공격하는가?
            if (other.TargetCharacter != slot.Owner)
                continue;

            if (!IsSamePart(other.TargetPart, slot.Part))
                continue;

            return other;
        }

        return null;
    }

    private bool IsSamePart(BodyPart a, BodyPart b)
    {
        if (a == null || b == null)
            return false;

        if (a == b)
            return true;

        return a.Type == b.Type;
    }

    private void EnsureArrowRoot()
    {
        if (arrowRoot != null)
            return;

        if (canvas == null)
        {
            Debug.LogWarning("TargetArrowUI : Canvas가 없습니다.");
            return;
        }

        GameObject rootObj = new GameObject("Target Arrow UI Root");
        rootObj.transform.SetParent(canvas.transform, false);

        arrowRoot = rootObj.AddComponent<RectTransform>();

        arrowRoot.anchorMin = Vector2.zero;
        arrowRoot.anchorMax = Vector2.one;
        arrowRoot.offsetMin = Vector2.zero;
        arrowRoot.offsetMax = Vector2.zero;
        arrowRoot.pivot = new Vector2(0.5f, 0.5f);

        CanvasGroup canvasGroup = rootObj.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // 화살표를 UI 위에 보이게 함
        arrowRoot.SetAsLastSibling();
    }

    private void CacheButtons()
    {
        cachedButtons =
            FindObjectsByType<BodyPartButton>(
                FindObjectsSortMode.None);
    }

    public void Refresh()
    {
        if (battleManager == null ||
            battleManager.ActionManager == null ||
            battleManager.BattleContext == null ||
            arrowRoot == null)
        {
            HideAll();
            return;
        }

        if (cachedButtons == null || cachedButtons.Length == 0)
            CacheButtons();

        Character player = battleManager.BattleContext.Player;

        if (player == null)
        {
            HideAll();
            return;
        }

        IReadOnlyList<ActionSlot> slots =
            battleManager.ActionManager.Slots;

        int usedArrowCount = 0;

        HashSet<ActionSlot> alreadyDrawnEnemyClashSlots =
            new HashSet<ActionSlot>();

        // 1. 플레이어 화살표 먼저 그림
        foreach (ActionSlot slot in slots)
        {
            if (!IsDrawablePlayerSlot(slot, player))
                continue;

            BodyPartButton playerButton =
                FindButton(slot.Owner, slot.Part);

            BodyPartButton targetButton =
                FindButton(slot.TargetCharacter, slot.TargetPart);

            if (playerButton == null || targetButton == null)
                continue;

            bool isClash =
                IsMutualClash(slot);

            if (isClash)
            {
                usedArrowCount =
                    DrawClashArrows(
                        usedArrowCount,
                        slot,
                        playerButton,
                        targetButton);

                ActionSlot opposingSlot =
                    FindOpposingClashSlot(slot);

                if (opposingSlot != null)
                    alreadyDrawnEnemyClashSlots.Add(opposingSlot);
            }
            else
            {
                usedArrowCount =
                    DrawNormalPlayerArrow(
                        usedArrowCount,
                        playerButton,
                        targetButton);
            }
        }

        // 2. 적이 플레이어를 타겟팅하는 빨간 화살표 그림
        foreach (ActionSlot slot in slots)
        {
            if (!IsDrawableEnemySlot(slot, player))
                continue;

            // 이미 합 화살표로 그린 적 슬롯이면 중복으로 그리지 않음
            if (alreadyDrawnEnemyClashSlots.Contains(slot))
                continue;

            BodyPartButton enemyButton =
                FindButton(slot.Owner, slot.Part);

            BodyPartButton playerTargetButton =
                FindButton(slot.TargetCharacter, slot.TargetPart);

            if (enemyButton == null || playerTargetButton == null)
                continue;

            usedArrowCount =
                DrawNormalEnemyArrow(
                    usedArrowCount,
                    enemyButton,
                    playerTargetButton);
        }

        for (int i = usedArrowCount; i < arrows.Count; i++)
        {
            arrows[i].SetActive(false);
        }
    }
    
    private bool IsDrawableEnemySlot(
        ActionSlot slot,
        Character player)
    {
        if (!showEnemyTargetArrows)
            return false;

        if (slot == null)
            return false;

        if (slot.Owner == null ||
            slot.Part == null ||
            slot.TargetCharacter == null ||
            slot.TargetPart == null)
            return false;

        // 적이 플레이어를 타겟팅하는 경우
        if (slot.Owner == player)
            return false;

        if (slot.TargetCharacter != player)
            return false;

        return true;
    }
    
    private int DrawNormalEnemyArrow(
        int arrowIndex,
        BodyPartButton fromButton,
        BodyPartButton toButton)
    {
        Vector2 start =
            GetLocalCenter(fromButton.RectTransform);

        Vector2 end =
            GetLocalCenter(toButton.RectTransform);

        ArrowVisual arrow =
            GetArrow(arrowIndex);

        DrawArrow(
            arrow,
            start,
            end,
            enemyClashArrowColor);

        return arrowIndex + 1;
    }

    private bool IsDrawablePlayerSlot(
        ActionSlot slot,
        Character player)
    {
        if (!showPlayerArrows)
            return false;

        if (slot == null)
            return false;

        if (slot.Owner == null ||
            slot.Part == null ||
            slot.TargetCharacter == null ||
            slot.TargetPart == null)
            return false;

        // 플레이어가 만든 행동만 그림
        if (slot.Owner != player)
            return false;

        // 자기 자신을 향하는 행동은 제외
        if (slot.TargetCharacter == player)
            return false;

        return true;
    }

    private int DrawNormalPlayerArrow(
        int arrowIndex,
        BodyPartButton fromButton,
        BodyPartButton toButton)
    {
        Vector2 start =
            GetLocalCenter(fromButton.RectTransform);

        Vector2 end =
            GetLocalCenter(toButton.RectTransform);

        ArrowVisual arrow =
            GetArrow(arrowIndex);

        DrawArrow(
            arrow,
            start,
            end,
            playerArrowColor);

        return arrowIndex + 1;
    }

    private int DrawClashArrows(
        int arrowIndex,
        ActionSlot playerSlot,
        BodyPartButton playerButton,
        BodyPartButton targetButton)
    {
        ActionSlot enemySlot =
            FindOpposingClashSlot(playerSlot);

        if (enemySlot == null)
        {
            return DrawNormalPlayerArrow(
                arrowIndex,
                playerButton,
                targetButton);
        }

        BodyPartButton enemyButton =
            FindButton(enemySlot.Owner, enemySlot.Part);

        if (enemyButton == null)
        {
            return DrawNormalPlayerArrow(
                arrowIndex,
                playerButton,
                targetButton);
        }

        Vector2 playerStart =
            GetLocalCenter(playerButton.RectTransform);

        Vector2 enemyStart =
            GetLocalCenter(enemyButton.RectTransform);

        // 두 버튼 중심 사이의 충돌점
        Vector2 clashPoint =
            (playerStart + enemyStart) * 0.5f;

        ArrowVisual playerArrow =
            GetArrow(arrowIndex);

        DrawArrowToPoint(
            playerArrow,
            playerStart,
            clashPoint,
            playerArrowColor);

        arrowIndex++;

        if (showEnemySideOnClash)
        {
            ArrowVisual enemyArrow =
                GetArrow(arrowIndex);

            DrawArrowToPoint(
                enemyArrow,
                enemyStart,
                clashPoint,
                enemyClashArrowColor);

            arrowIndex++;
        }

        return arrowIndex;
    }

    private BodyPartButton FindButton(
        Character character,
        BodyPart part)
    {
        if (character == null || part == null)
            return null;

        if (cachedButtons == null)
            return null;

        foreach (BodyPartButton button in cachedButtons)
        {
            if (button == null)
                continue;

            if (!button.gameObject.activeInHierarchy)
                continue;

            if (button.Owner != character)
                continue;

            if (button.BodyPart == part)
                return button;

            // BodyPart 참조가 다를 때 대비
            if (button.BodyPart != null &&
                button.BodyPart.Type == part.Type)
            {
                return button;
            }
        }

        return null;
    }

    private Vector2 GetLocalCenter(RectTransform rect)
    {
        if (rect == null)
            return Vector2.zero;

        Camera cam = null;

        if (canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera;
        }

        Vector3 worldCenter =
            rect.TransformPoint(rect.rect.center);

        Vector2 screenPoint =
            RectTransformUtility.WorldToScreenPoint(
                cam,
                worldCenter);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            arrowRoot,
            screenPoint,
            cam,
            out Vector2 localPoint);

        return localPoint;
    }

    private void DrawArrowToPoint(
        ArrowVisual arrow,
        Vector2 start,
        Vector2 end,
        Color color)
    {
        DrawArrow(
            arrow,
            start,
            end,
            color,
            false);
    }

    private void DrawArrow(
        ArrowVisual arrow,
        Vector2 start,
        Vector2 end,
        Color color)
    {
        DrawArrow(
            arrow,
            start,
            end,
            color,
            true);
    }

    private void DrawArrow(
        ArrowVisual arrow,
        Vector2 start,
        Vector2 end,
        Color color,
        bool useEndPadding)
    {
        Vector2 dir =
            end - start;

        if (dir.sqrMagnitude <= 0.01f)
        {
            arrow.SetActive(false);
            return;
        }

        dir.Normalize();

        start += dir * startPadding;

        if (useEndPadding)
            end -= dir * endPadding;

        arrow.SetActive(true);
        arrow.SetColor(color);

        DrawSegment(
            arrow.body,
            start,
            end,
            lineThickness);

        Vector2 leftDir =
            Rotate(-dir, arrowHeadAngle);

        Vector2 rightDir =
            Rotate(-dir, -arrowHeadAngle);

        Vector2 leftEnd =
            end + leftDir * arrowHeadLength;

        Vector2 rightEnd =
            end + rightDir * arrowHeadLength;

        DrawSegment(
            arrow.headLeft,
            end,
            leftEnd,
            lineThickness);

        DrawSegment(
            arrow.headRight,
            end,
            rightEnd,
            lineThickness);
    }

    private void DrawSegment(
        Image image,
        Vector2 start,
        Vector2 end,
        float thickness)
    {
        if (image == null)
            return;

        RectTransform rect =
            image.rectTransform;

        Vector2 diff =
            end - start;

        float length =
            diff.magnitude;

        rect.anchoredPosition =
            start + diff * 0.5f;

        rect.sizeDelta =
            new Vector2(length, thickness);

        float angle =
            Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

        rect.localEulerAngles =
            new Vector3(0f, 0f, angle);
    }

    private Vector2 Rotate(
        Vector2 v,
        float degree)
    {
        float rad =
            degree * Mathf.Deg2Rad;

        float cos =
            Mathf.Cos(rad);

        float sin =
            Mathf.Sin(rad);

        return new Vector2(
            v.x * cos - v.y * sin,
            v.x * sin + v.y * cos);
    }

    private ArrowVisual GetArrow(int index)
    {
        while (arrows.Count <= index)
        {
            arrows.Add(CreateArrowVisual());
        }

        return arrows[index];
    }

    private ArrowVisual CreateArrowVisual()
    {
        GameObject root =
            new GameObject("Target Arrow");

        root.transform.SetParent(arrowRoot, false);

        RectTransform rootRect =
            root.AddComponent<RectTransform>();

        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;

        ArrowVisual visual =
            new ArrowVisual();

        visual.root = rootRect;
        visual.body = CreateLineImage("Body", rootRect);
        visual.headLeft = CreateLineImage("Head Left", rootRect);
        visual.headRight = CreateLineImage("Head Right", rootRect);

        return visual;
    }

    private Image CreateLineImage(
        string objectName,
        Transform parent)
    {
        GameObject obj =
            new GameObject(objectName);

        obj.transform.SetParent(parent, false);

        Image image =
            obj.AddComponent<Image>();

        image.sprite = GetWhiteSprite();
        image.color = Color.white;
        image.raycastTarget = false;

        RectTransform rect =
            image.rectTransform;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        return image;
    }

    private Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D texture =
            new Texture2D(1, 1);

        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        whiteSprite =
            Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f));

        return whiteSprite;
    }

    private void HideAll()
    {
        foreach (ArrowVisual arrow in arrows)
        {
            arrow.SetActive(false);
        }
    }

    private class ArrowVisual
    {
        public RectTransform root;
        public Image body;
        public Image headLeft;
        public Image headRight;

        public void SetActive(bool active)
        {
            if (root != null)
                root.gameObject.SetActive(active);
        }

        public void SetColor(Color color)
        {
            if (body != null)
                body.color = color;

            if (headLeft != null)
                headLeft.color = color;

            if (headRight != null)
                headRight.color = color;
        }
    }
}