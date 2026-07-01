using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TargetArrowUI : MonoBehaviour
{
    private class UIClashPair
    {
        public ActionSlot A;
        public ActionSlot B;

        public UIClashPair(ActionSlot a, ActionSlot b)
        {
            A = a;
            B = b;
        }

        public bool Contains(ActionSlot slot)
        {
            return A == slot || B == slot;
        }
    }
    
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
    [SerializeField] private Color preparationArrowColor = new Color(1f, 0.85f, 0f, 1f);

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
    
    private bool IsSamePart(BodyPart a, BodyPart b)
    {
        if (a == null || b == null)
            return false;

        if (a == b)
            return true;

        return a.Type == b.Type;
    }
    
    private bool IsPreparationSlot(ActionSlot slot)
    {
        if (slot == null)
            return false;

        if (slot.Phase == ActionPhase.FORESIGHT)
            return true;

        if (slot.Skill == null)
            return false;

        if (slot.Skill.ActionType == ActionType.Preparation)
            return true;

        if (!string.IsNullOrEmpty(slot.Skill.SkillName) &&
            slot.Skill.SkillName.Contains("도사림"))
            return true;

        return false;
    }
    
    private int DrawActionArrow(
        int arrowIndex,
        ActionSlot slot,
        Color color)
    {
        if (slot == null)
            return arrowIndex;

        BodyPartButton fromButton =
            FindButton(slot.Owner, slot.Part);

        BodyPartButton toButton =
            FindButton(slot.TargetCharacter, slot.TargetPart);

        if (fromButton == null || toButton == null)
            return arrowIndex;

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
            color);

        return arrowIndex + 1;
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

        List<UIClashPair> clashPairs =
            BuildUIClashPairs(slots, player);

        HashSet<ActionSlot> clashSlots = new();

        foreach (UIClashPair pair in clashPairs)
        {
            if (pair == null)
                continue;

            if (pair.A != null)
                clashSlots.Add(pair.A);

            if (pair.B != null)
                clashSlots.Add(pair.B);
        }

        int usedArrowCount = 0;

        // 1. 합이 아닌 일반 플레이어 화살표
        // 단, 도사림은 여기서 제외하고 나중에 노란색으로 따로 그림
        foreach (ActionSlot slot in slots)
        {
            if (clashSlots.Contains(slot))
                continue;

            if (IsPreparationSlot(slot))
                continue;

            if (!IsDrawablePlayerSlot(slot, player))
                continue;

            usedArrowCount =
                DrawActionArrow(
                    usedArrowCount,
                    slot,
                    playerArrowColor);
        }

        // 2. 합이 아닌 일반 적 화살표
        // 단, 도사림은 여기서 제외하고 나중에 노란색으로 따로 그림
        foreach (ActionSlot slot in slots)
        {
            if (clashSlots.Contains(slot))
                continue;

            if (IsPreparationSlot(slot))
                continue;

            if (!IsDrawableEnemySlot(slot, player))
                continue;

            usedArrowCount =
                DrawActionArrow(
                    usedArrowCount,
                    slot,
                    enemyClashArrowColor);
        }

        // 3. 도사림 / Preparation / FORESIGHT 화살표
        // 합 이전에 실행되는 행동이므로 노란색으로 표시
        foreach (ActionSlot slot in slots)
        {
            if (!IsPreparationSlot(slot))
                continue;

            bool drawable =
                IsDrawablePlayerSlot(slot, player) ||
                IsDrawableEnemySlot(slot, player);

            if (!drawable)
                continue;

            usedArrowCount =
                DrawActionArrow(
                    usedArrowCount,
                    slot,
                    preparationArrowColor);
        }

        // 4. 합 화살표는 마지막에 그림
        foreach (UIClashPair pair in clashPairs)
        {
            if (pair == null)
                continue;

            bool involvesPlayer =
                pair.A.Owner == player ||
                pair.B.Owner == player;

            if (!involvesPlayer)
                continue;

            usedArrowCount =
                DrawUIClashPair(
                    usedArrowCount,
                    pair,
                    player);
        }

        for (int i = usedArrowCount; i < arrows.Count; i++)
        {
            arrows[i].SetActive(false);
        }
    }
    
    private List<UIClashPair> BuildUIClashPairs(
        IReadOnlyList<ActionSlot> slots,
        Character player)
    {
        List<UIClashPair> result = new();

        if (slots == null || player == null)
            return result;

        List<ActionSlot> playerSlots = new();

        foreach (ActionSlot slot in slots)
        {
            if (!CanEnterClash(slot))
                continue;

            if (slot.Owner != player)
                continue;

            playerSlots.Add(slot);
        }

        HashSet<ActionSlot> usedPlayerSlots = new();
        HashSet<ActionSlot> usedEnemySlots = new();

        // 1단계: AI가 노린 부위로 그대로 맞대응한 경우를 최우선 처리
        foreach (ActionSlot playerSlot in playerSlots)
        {
            if (usedPlayerSlots.Contains(playerSlot))
                continue;

            ActionSlot enemySlot =
                FindEnemySlotSelectedByPlayer(
                    playerSlot,
                    slots,
                    player);

            if (enemySlot == null)
                continue;

            if (usedEnemySlots.Contains(enemySlot))
                continue;

            if (!CanEnterClash(enemySlot))
                continue;

            if (!IsExactMutual(playerSlot, enemySlot))
                continue;

            result.Add(
                new UIClashPair(
                    playerSlot,
                    enemySlot));

            usedPlayerSlots.Add(playerSlot);
            usedEnemySlots.Add(enemySlot);
        }

        // 2단계: 남은 플레이어 슬롯 중 속도가 높은 슬롯이 합을 뺏음
        playerSlots.Sort((a, b) => b.Speed.CompareTo(a.Speed));

        foreach (ActionSlot playerSlot in playerSlots)
        {
            if (usedPlayerSlots.Contains(playerSlot))
                continue;

            ActionSlot enemySlot =
                FindEnemySlotSelectedByPlayer(
                    playerSlot,
                    slots,
                    player);

            if (enemySlot == null)
                continue;

            if (usedEnemySlots.Contains(enemySlot))
                continue;

            if (!CanEnterClash(enemySlot))
                continue;

            if (!CanPlayerStealClash(playerSlot, enemySlot))
                continue;

            result.Add(
                new UIClashPair(
                    playerSlot,
                    enemySlot));

            usedPlayerSlots.Add(playerSlot);
            usedEnemySlots.Add(enemySlot);
        }

        return result;
    }
    
    private ActionSlot FindEnemySlotSelectedByPlayer(
        ActionSlot playerSlot,
        IReadOnlyList<ActionSlot> slots,
        Character player)
    {
        if (playerSlot == null)
            return null;

        if (slots == null)
            return null;

        if (playerSlot.Owner != player)
            return null;

        foreach (ActionSlot enemySlot in slots)
        {
            if (enemySlot == null)
                continue;

            if (enemySlot == playerSlot)
                continue;

            if (enemySlot.Owner == null ||
                enemySlot.Part == null)
                continue;

            // 적 슬롯만 찾음
            if (enemySlot.Owner == player)
                continue;

            // 플레이어가 클릭한 적 캐릭터인가?
            if (enemySlot.Owner != playerSlot.TargetCharacter)
                continue;

            // 플레이어가 클릭한 적 부위인가?
            if (!IsSamePart(enemySlot.Part, playerSlot.TargetPart))
                continue;

            return enemySlot;
        }

        return null;
    }
    
    private bool CanEnterClash(ActionSlot slot)
    {
        if (slot == null)
            return false;

        if (slot.Owner == null ||
            slot.Part == null ||
            slot.TargetCharacter == null ||
            slot.TargetPart == null)
            return false;

        if (slot.Phase != ActionPhase.COMBAT)
            return false;

        if (slot.Skill == null)
            return false;

        if (!slot.Skill.CanClash)
            return false;

        return true;
    }
    
    private bool IsExactMutual(
        ActionSlot playerSlot,
        ActionSlot enemySlot)
    {
        if (playerSlot == null || enemySlot == null)
            return false;

        return
            enemySlot.TargetCharacter == playerSlot.Owner &&
            IsSamePart(enemySlot.TargetPart, playerSlot.Part);
    }
    
    private bool CanPlayerStealClash(
        ActionSlot playerSlot,
        ActionSlot enemySlot)
    {
        if (playerSlot == null || enemySlot == null)
            return false;

        if (playerSlot.Speed <= enemySlot.Speed)
            return false;

        return true;
    }
    
    private int DrawUIClashPair(
        int arrowIndex,
        UIClashPair pair,
        Character player)
    {
        ActionSlot playerSlot =
            pair.A.Owner == player ? pair.A : pair.B;

        ActionSlot enemySlot =
            pair.A.Owner == player ? pair.B : pair.A;

        BodyPartButton playerButton =
            FindButton(playerSlot.Owner, playerSlot.Part);

        BodyPartButton enemyButton =
            FindButton(enemySlot.Owner, enemySlot.Part);

        if (playerButton == null || enemyButton == null)
            return arrowIndex;

        Vector2 playerStart =
            GetLocalCenter(playerButton.RectTransform);

        Vector2 enemyStart =
            GetLocalCenter(enemyButton.RectTransform);

        Vector2 center =
            (playerStart + enemyStart) * 0.5f;

        Vector2 playerDir =
            center - playerStart;

        Vector2 enemyDir =
            center - enemyStart;

        if (playerDir.sqrMagnitude <= 0.01f ||
            enemyDir.sqrMagnitude <= 0.01f)
        {
            return arrowIndex;
        }

        playerDir.Normalize();
        enemyDir.Normalize();

        float clashGap = 18f;

        Vector2 playerEnd =
            center - playerDir * clashGap;

        Vector2 enemyEnd =
            center - enemyDir * clashGap;

        ArrowVisual playerArrow =
            GetArrow(arrowIndex);

        DrawArrowToPoint(
            playerArrow,
            playerStart,
            playerEnd,
            playerArrowColor);

        arrowIndex++;

        if (showEnemySideOnClash)
        {
            ArrowVisual enemyArrow =
                GetArrow(arrowIndex);

            DrawArrowToPoint(
                enemyArrow,
                enemyStart,
                enemyEnd,
                enemyClashArrowColor);

            arrowIndex++;
        }

        return arrowIndex;
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
        
        if (arrow.root != null)
            arrow.root.SetAsLastSibling();

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