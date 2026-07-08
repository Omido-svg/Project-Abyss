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

    private class HighlightVisual
    {
        public RectTransform root;
        public Image image;

        public void SetActive(bool active)
        {
            if (root != null)
                root.gameObject.SetActive(active);
        }

        public void SetColor(Color color)
        {
            if (image != null)
                image.color = color;
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

    [Header("Current Action Display")]
    [SerializeField] private bool focusOnlyCurrentActionDuringVisual = true;
    [SerializeField] private bool highlightCurrentActionButtons = true;
    [SerializeField] private bool drawCurrentActionArrow = true;

    [Header("Color")]
    [SerializeField] private Color playerArrowColor = Color.cyan;
    [SerializeField] private Color enemyClashArrowColor = Color.red;
    [SerializeField] private Color preparationArrowColor = new Color(0.65f, 0.25f, 1f, 1f);
    [SerializeField] private Color currentActionArrowColor = new Color(1f, 0.9f, 0.1f, 1f);
    [SerializeField] private Color activeActionHighlightColor = new Color(1f, 0.9f, 0.1f, 0.45f);

    [Header("Line")]
    [SerializeField] private float lineThickness = 5f;
    [SerializeField] private float startPadding = 25f;
    [SerializeField] private float endPadding = 25f;

    [Header("Arrow Head")]
    [SerializeField] private float arrowHeadLength = 28f;
    [SerializeField] private float arrowHeadAngle = 35f;

    [Header("Highlight")]
    [SerializeField] private Vector2 highlightPadding = new Vector2(12f, 12f);

    private readonly List<ArrowVisual> arrows = new();
    private readonly List<HighlightVisual> highlights = new();

    private readonly List<UIClashPair> clashPairs = new();
    private readonly List<ActionSlot> playerClashCandidateSlots = new();

    private readonly HashSet<ActionSlot> clashSlots = new();
    private readonly HashSet<ActionSlot> usedPlayerSlots = new();
    private readonly HashSet<ActionSlot> usedEnemySlots = new();
    private readonly HashSet<BodyPartButton> highlightButtons = new();

    private BodyPartButton[] cachedButtons;
    private BattleVisualRequest currentVisualRequest;

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

    public void SetCurrentVisualRequest(BattleVisualRequest request)
    {
        currentVisualRequest = request;
    }

    public void ClearCurrentVisualRequest(BattleVisualRequest request)
    {
        if (currentVisualRequest == request)
            currentVisualRequest = null;
    }

    public void ClearCurrentVisualRequest()
    {
        currentVisualRequest = null;
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

        Character player =
            battleManager.BattleContext.Player;

        if (player == null)
        {
            HideAll();
            return;
        }

        IReadOnlyList<ActionSlot> slots =
            battleManager.ActionManager.Slots;

        BuildUIClashPairs(
            slots,
            player,
            clashPairs);

        BuildClashSlotSet(
            clashPairs,
            clashSlots);

        bool hasCurrentVisual =
            currentVisualRequest != null;

        int usedHighlightCount = 0;

        if (hasCurrentVisual &&
            highlightCurrentActionButtons)
        {
            usedHighlightCount =
                DrawCurrentVisualHighlights();
        }

        HideUnusedHighlights(
            usedHighlightCount);

        int usedArrowCount = 0;

        if (hasCurrentVisual &&
            focusOnlyCurrentActionDuringVisual)
        {
            if (drawCurrentActionArrow)
            {
                usedArrowCount =
                    DrawCurrentVisualArrows(
                        usedArrowCount);
            }

            HideUnusedArrows(
                usedArrowCount);

            return;
        }

        usedArrowCount =
            DrawNormalPlayerArrows(
                slots,
                player,
                usedArrowCount,
                hasCurrentVisual);

        usedArrowCount =
            DrawNormalEnemyArrows(
                slots,
                player,
                usedArrowCount,
                hasCurrentVisual);

        usedArrowCount =
            DrawPreparationArrows(
                slots,
                player,
                usedArrowCount,
                hasCurrentVisual);

        usedArrowCount =
            DrawClashArrows(
                clashPairs,
                player,
                usedArrowCount,
                hasCurrentVisual);

        if (hasCurrentVisual &&
            drawCurrentActionArrow)
        {
            usedArrowCount =
                DrawCurrentVisualArrows(
                    usedArrowCount);
        }

        HideUnusedArrows(
            usedArrowCount);
    }

    private int DrawNormalPlayerArrows(
        IReadOnlyList<ActionSlot> slots,
        Character player,
        int arrowIndex,
        bool hasCurrentVisual)
    {
        if (slots == null)
            return arrowIndex;

        foreach (ActionSlot slot in slots)
        {
            if (hasCurrentVisual &&
                IsCurrentVisualSlot(slot))
                continue;

            if (clashSlots.Contains(slot))
                continue;

            if (IsPreparationSlot(slot))
                continue;

            if (!IsDrawablePlayerSlot(slot, player))
                continue;

            arrowIndex =
                DrawActionArrow(
                    arrowIndex,
                    slot,
                    playerArrowColor);
        }

        return arrowIndex;
    }

    private int DrawNormalEnemyArrows(
        IReadOnlyList<ActionSlot> slots,
        Character player,
        int arrowIndex,
        bool hasCurrentVisual)
    {
        if (slots == null)
            return arrowIndex;

        foreach (ActionSlot slot in slots)
        {
            if (hasCurrentVisual &&
                IsCurrentVisualSlot(slot))
                continue;

            if (clashSlots.Contains(slot))
                continue;

            if (IsPreparationSlot(slot))
                continue;

            if (!IsDrawableEnemySlot(slot, player))
                continue;

            arrowIndex =
                DrawActionArrow(
                    arrowIndex,
                    slot,
                    enemyClashArrowColor);
        }

        return arrowIndex;
    }

    private int DrawPreparationArrows(
        IReadOnlyList<ActionSlot> slots,
        Character player,
        int arrowIndex,
        bool hasCurrentVisual)
    {
        if (slots == null)
            return arrowIndex;

        foreach (ActionSlot slot in slots)
        {
            if (hasCurrentVisual &&
                IsCurrentVisualSlot(slot))
                continue;

            if (!IsPreparationSlot(slot))
                continue;

            bool drawable =
                IsDrawablePlayerSlot(slot, player) ||
                IsDrawableEnemySlot(slot, player);

            if (!drawable)
                continue;

            arrowIndex =
                DrawActionArrow(
                    arrowIndex,
                    slot,
                    preparationArrowColor);
        }

        return arrowIndex;
    }

    private int DrawClashArrows(
        List<UIClashPair> pairs,
        Character player,
        int arrowIndex,
        bool hasCurrentVisual)
    {
        if (pairs == null)
            return arrowIndex;

        foreach (UIClashPair pair in pairs)
        {
            if (pair == null ||
                pair.A == null ||
                pair.B == null)
                continue;

            if (hasCurrentVisual &&
                IsCurrentVisualPair(pair))
                continue;

            bool involvesPlayer =
                pair.A.Owner == player ||
                pair.B.Owner == player;

            if (!involvesPlayer)
                continue;

            arrowIndex =
                DrawUIClashPair(
                    arrowIndex,
                    pair,
                    player);
        }

        return arrowIndex;
    }

    private int DrawCurrentVisualArrows(int arrowIndex)
    {
        if (currentVisualRequest == null)
            return arrowIndex;

        BattleAction source =
            currentVisualRequest.SourceAction;

        BattleAction opponent =
            currentVisualRequest.OpponentAction;

        if (source == null)
            return arrowIndex;

        bool isClash =
            opponent != null &&
            currentVisualRequest.ClashSteps != null &&
            currentVisualRequest.ClashSteps.Count > 0;

        if (isClash)
        {
            return DrawCurrentClashArrows(
                arrowIndex,
                source,
                opponent);
        }

        return DrawBattleActionArrow(
            arrowIndex,
            source,
            currentActionArrowColor);
    }

    private int DrawBattleActionArrow(
        int arrowIndex,
        BattleAction action,
        Color color)
    {
        if (action == null)
            return arrowIndex;

        BodyPartButton fromButton =
            FindButton(
                action.Owner,
                action.OwnerPart);

        BodyPartButton toButton =
            FindButton(
                action.Target,
                action.TargetPart);

        if (fromButton == null || toButton == null)
            return arrowIndex;

        Vector2 start =
            GetLocalCenter(
                fromButton.RectTransform);

        Vector2 end =
            GetLocalCenter(
                toButton.RectTransform);

        ArrowVisual arrow =
            GetArrow(arrowIndex);

        DrawArrow(
            arrow,
            start,
            end,
            color);

        return arrowIndex + 1;
    }

    private int DrawCurrentClashArrows(
        int arrowIndex,
        BattleAction source,
        BattleAction opponent)
    {
        if (source == null || opponent == null)
            return arrowIndex;

        BodyPartButton sourceButton =
            FindButton(
                source.Owner,
                source.OwnerPart);

        BodyPartButton opponentButton =
            FindButton(
                opponent.Owner,
                opponent.OwnerPart);

        if (sourceButton == null || opponentButton == null)
            return arrowIndex;

        Vector2 sourceStart =
            GetLocalCenter(
                sourceButton.RectTransform);

        Vector2 opponentStart =
            GetLocalCenter(
                opponentButton.RectTransform);

        return DrawTwoArrowsToCenter(
            arrowIndex,
            sourceStart,
            opponentStart,
            currentActionArrowColor,
            currentActionArrowColor,
            true);
    }

    private int DrawActionArrow(
        int arrowIndex,
        ActionSlot slot,
        Color color)
    {
        if (slot == null)
            return arrowIndex;

        BodyPartButton fromButton =
            FindButton(
                slot.Owner,
                slot.Part);

        BodyPartButton toButton =
            FindButton(
                slot.TargetCharacter,
                slot.TargetPart);

        if (fromButton == null || toButton == null)
            return arrowIndex;

        Vector2 start =
            GetLocalCenter(
                fromButton.RectTransform);

        Vector2 end =
            GetLocalCenter(
                toButton.RectTransform);

        ArrowVisual arrow =
            GetArrow(arrowIndex);

        DrawArrow(
            arrow,
            start,
            end,
            color);

        return arrowIndex + 1;
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
            FindButton(
                playerSlot.Owner,
                playerSlot.Part);

        BodyPartButton enemyButton =
            FindButton(
                enemySlot.Owner,
                enemySlot.Part);

        if (playerButton == null || enemyButton == null)
            return arrowIndex;

        Vector2 playerStart =
            GetLocalCenter(
                playerButton.RectTransform);

        Vector2 enemyStart =
            GetLocalCenter(
                enemyButton.RectTransform);

        if (showEnemySideOnClash)
        {
            return DrawTwoArrowsToCenter(
                arrowIndex,
                playerStart,
                enemyStart,
                playerArrowColor,
                enemyClashArrowColor,
                true);
        }

        return DrawSingleArrowToCenter(
            arrowIndex,
            playerStart,
            enemyStart,
            playerArrowColor);
    }

    private int DrawSingleArrowToCenter(
        int arrowIndex,
        Vector2 start,
        Vector2 otherStart,
        Color color)
    {
        Vector2 center =
            (start + otherStart) * 0.5f;

        Vector2 dir =
            center - start;

        if (dir.sqrMagnitude <= 0.01f)
            return arrowIndex;

        dir.Normalize();

        float clashGap = 18f;

        Vector2 end =
            center - dir * clashGap;

        ArrowVisual arrow =
            GetArrow(arrowIndex);

        DrawArrowToPoint(
            arrow,
            start,
            end,
            color);

        return arrowIndex + 1;
    }

    private int DrawTwoArrowsToCenter(
        int arrowIndex,
        Vector2 firstStart,
        Vector2 secondStart,
        Color firstColor,
        Color secondColor,
        bool drawSecond)
    {
        Vector2 center =
            (firstStart + secondStart) * 0.5f;

        Vector2 firstDir =
            center - firstStart;

        Vector2 secondDir =
            center - secondStart;

        if (firstDir.sqrMagnitude <= 0.01f ||
            secondDir.sqrMagnitude <= 0.01f)
            return arrowIndex;

        firstDir.Normalize();
        secondDir.Normalize();

        float clashGap = 18f;

        Vector2 firstEnd =
            center - firstDir * clashGap;

        Vector2 secondEnd =
            center - secondDir * clashGap;

        ArrowVisual firstArrow =
            GetArrow(arrowIndex);

        DrawArrowToPoint(
            firstArrow,
            firstStart,
            firstEnd,
            firstColor);

        arrowIndex++;

        if (drawSecond)
        {
            ArrowVisual secondArrow =
                GetArrow(arrowIndex);

            DrawArrowToPoint(
                secondArrow,
                secondStart,
                secondEnd,
                secondColor);

            arrowIndex++;
        }

        return arrowIndex;
    }

    private void BuildUIClashPairs(
        IReadOnlyList<ActionSlot> slots,
        Character player,
        List<UIClashPair> result)
    {
        result.Clear();
        playerClashCandidateSlots.Clear();
        usedPlayerSlots.Clear();
        usedEnemySlots.Clear();

        if (slots == null || player == null)
            return;

        foreach (ActionSlot slot in slots)
        {
            if (!CanEnterClash(slot))
                continue;

            if (slot.Owner != player)
                continue;

            playerClashCandidateSlots.Add(slot);
        }

        foreach (ActionSlot playerSlot in playerClashCandidateSlots)
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

        playerClashCandidateSlots.Sort(
            (a, b) => b.Speed.CompareTo(a.Speed));

        foreach (ActionSlot playerSlot in playerClashCandidateSlots)
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
    }

    private void BuildClashSlotSet(
        List<UIClashPair> pairs,
        HashSet<ActionSlot> result)
    {
        result.Clear();

        if (pairs == null)
            return;

        foreach (UIClashPair pair in pairs)
        {
            if (pair == null)
                continue;

            if (pair.A != null)
                result.Add(pair.A);

            if (pair.B != null)
                result.Add(pair.B);
        }
    }

    private ActionSlot FindEnemySlotSelectedByPlayer(
        ActionSlot playerSlot,
        IReadOnlyList<ActionSlot> slots,
        Character player)
    {
        if (playerSlot == null ||
            slots == null ||
            playerSlot.Owner != player)
        {
            return null;
        }

        foreach (ActionSlot enemySlot in slots)
        {
            if (enemySlot == null)
                continue;

            if (enemySlot == playerSlot)
                continue;

            if (enemySlot.Owner == null ||
                enemySlot.Part == null)
                continue;

            if (enemySlot.Owner == player)
                continue;

            if (enemySlot.Owner != playerSlot.TargetCharacter)
                continue;

            if (!IsSamePart(
                    enemySlot.Part,
                    playerSlot.TargetPart))
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

        return slot.Skill.CanClash;
    }

    private bool IsExactMutual(
        ActionSlot playerSlot,
        ActionSlot enemySlot)
    {
        if (playerSlot == null || enemySlot == null)
            return false;

        return
            enemySlot.TargetCharacter == playerSlot.Owner &&
            IsSamePart(
                enemySlot.TargetPart,
                playerSlot.Part);
    }

    private bool CanPlayerStealClash(
        ActionSlot playerSlot,
        ActionSlot enemySlot)
    {
        if (playerSlot == null || enemySlot == null)
            return false;

        return playerSlot.Speed > enemySlot.Speed;
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

        if (slot.Owner == player)
            return false;

        return slot.TargetCharacter == player;
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

        if (slot.Owner != player)
            return false;

        return slot.TargetCharacter != player;
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

        return
            !string.IsNullOrEmpty(slot.Skill.SkillName) &&
            slot.Skill.SkillName.Contains("도사림");
    }

    private bool IsCurrentVisualSlot(ActionSlot slot)
    {
        if (slot == null ||
            currentVisualRequest == null)
        {
            return false;
        }

        return
            IsSameActionSlot(
                slot,
                currentVisualRequest.SourceAction) ||
            IsSameActionSlot(
                slot,
                currentVisualRequest.OpponentAction);
    }

    private bool IsSameActionSlot(
        ActionSlot slot,
        BattleAction action)
    {
        if (slot == null || action == null)
            return false;

        if (action.Slot == slot)
            return true;

        if (slot.Owner != action.Owner)
            return false;

        if (slot.Part != action.OwnerPart)
            return false;

        if (slot.TargetCharacter != action.Target)
            return false;

        if (slot.TargetPart != action.TargetPart)
            return false;

        if (slot.Skill != action.Skill)
            return false;

        return true;
    }

    private bool IsCurrentVisualPair(UIClashPair pair)
    {
        if (pair == null)
            return false;

        return
            IsCurrentVisualSlot(pair.A) ||
            IsCurrentVisualSlot(pair.B);
    }

    private bool IsSamePart(
        BodyPart a,
        BodyPart b)
    {
        if (a == null || b == null)
            return false;

        if (a == b)
            return true;

        return a.Type == b.Type;
    }

    private int DrawCurrentVisualHighlights()
    {
        if (currentVisualRequest == null)
            return 0;

        highlightButtons.Clear();

        AddActionHighlightButtons(
            currentVisualRequest.SourceAction,
            highlightButtons);

        AddActionHighlightButtons(
            currentVisualRequest.OpponentAction,
            highlightButtons);

        int usedCount = 0;

        foreach (BodyPartButton button in highlightButtons)
        {
            if (button == null)
                continue;

            HighlightVisual visual =
                GetHighlight(usedCount);

            DrawHighlight(
                visual,
                button.RectTransform,
                activeActionHighlightColor);

            usedCount++;
        }

        return usedCount;
    }

    private void AddActionHighlightButtons(
        BattleAction action,
        HashSet<BodyPartButton> result)
    {
        if (action == null)
            return;

        BodyPartButton ownerButton =
            FindButton(
                action.Owner,
                action.OwnerPart);

        BodyPartButton targetButton =
            FindButton(
                action.Target,
                action.TargetPart);

        if (ownerButton != null)
            result.Add(ownerButton);

        if (targetButton != null)
            result.Add(targetButton);
    }

    private void DrawHighlight(
        HighlightVisual visual,
        RectTransform targetRect,
        Color color)
    {
        if (visual == null ||
            targetRect == null)
            return;

        GetLocalRect(
            targetRect,
            out Vector2 center,
            out Vector2 size);

        visual.SetActive(true);
        visual.SetColor(color);

        if (visual.root == null)
            return;

        visual.root.anchoredPosition = center;
        visual.root.sizeDelta = size + highlightPadding;
        visual.root.SetAsFirstSibling();
    }

    private void GetLocalRect(
        RectTransform rect,
        out Vector2 center,
        out Vector2 size)
    {
        center = Vector2.zero;
        size = Vector2.zero;

        if (rect == null || arrowRoot == null)
            return;

        Camera cam =
            GetCanvasCamera();

        Vector3[] corners =
            new Vector3[4];

        rect.GetWorldCorners(corners);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            arrowRoot,
            RectTransformUtility.WorldToScreenPoint(cam, corners[0]),
            cam,
            out Vector2 bottomLeft);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            arrowRoot,
            RectTransformUtility.WorldToScreenPoint(cam, corners[2]),
            cam,
            out Vector2 topRight);

        center =
            (bottomLeft + topRight) * 0.5f;

        size =
            new Vector2(
                Mathf.Abs(topRight.x - bottomLeft.x),
                Mathf.Abs(topRight.y - bottomLeft.y));
    }

    private BodyPartButton FindButton(
        Character character,
        BodyPart part)
    {
        if (character == null ||
            part == null ||
            cachedButtons == null)
        {
            return null;
        }

        for (int i = 0; i < cachedButtons.Length; i++)
        {
            BodyPartButton button =
                cachedButtons[i];

            if (button == null)
                continue;

            if (!button.gameObject.activeInHierarchy)
                continue;

            if (button.Owner != character)
                continue;

            if (button.BodyPart == part)
                return button;

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

        Camera cam =
            GetCanvasCamera();

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

    private Camera GetCanvasCamera()
    {
        if (canvas == null)
            return null;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
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
        if (arrow == null)
            return;

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
            Rotate(
                -dir,
                arrowHeadAngle);

        Vector2 rightDir =
            Rotate(
                -dir,
                -arrowHeadAngle);

        DrawSegment(
            arrow.headLeft,
            end,
            end + leftDir * arrowHeadLength,
            lineThickness);

        DrawSegment(
            arrow.headRight,
            end,
            end + rightDir * arrowHeadLength,
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
            new Vector2(
                length,
                thickness);

        rect.localEulerAngles =
            new Vector3(
                0f,
                0f,
                Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
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
            arrows.Add(
                CreateArrowVisual());
        }

        return arrows[index];
    }

    private HighlightVisual GetHighlight(int index)
    {
        while (highlights.Count <= index)
        {
            highlights.Add(
                CreateHighlightVisual());
        }

        return highlights[index];
    }

    private ArrowVisual CreateArrowVisual()
    {
        GameObject root =
            new GameObject("Target Arrow");

        root.transform.SetParent(
            arrowRoot,
            false);

        RectTransform rootRect =
            root.AddComponent<RectTransform>();

        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;

        return new ArrowVisual
        {
            root = rootRect,
            body = CreateLineImage("Body", rootRect),
            headLeft = CreateLineImage("Head Left", rootRect),
            headRight = CreateLineImage("Head Right", rootRect)
        };
    }

    private HighlightVisual CreateHighlightVisual()
    {
        GameObject root =
            new GameObject("Current Action Highlight");

        root.transform.SetParent(
            arrowRoot,
            false);

        RectTransform rootRect =
            root.AddComponent<RectTransform>();

        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;

        Image image =
            root.AddComponent<Image>();

        image.sprite = GetWhiteSprite();
        image.color = activeActionHighlightColor;
        image.raycastTarget = false;

        return new HighlightVisual
        {
            root = rootRect,
            image = image
        };
    }

    private Image CreateLineImage(
        string objectName,
        Transform parent)
    {
        GameObject obj =
            new GameObject(objectName);

        obj.transform.SetParent(
            parent,
            false);

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

        texture.SetPixel(
            0,
            0,
            Color.white);

        texture.Apply();

        whiteSprite =
            Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f));

        return whiteSprite;
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

        GameObject rootObj =
            new GameObject("Target Arrow UI Root");

        rootObj.transform.SetParent(
            canvas.transform,
            false);

        arrowRoot =
            rootObj.AddComponent<RectTransform>();

        arrowRoot.anchorMin = Vector2.zero;
        arrowRoot.anchorMax = Vector2.one;
        arrowRoot.offsetMin = Vector2.zero;
        arrowRoot.offsetMax = Vector2.zero;
        arrowRoot.pivot = new Vector2(0.5f, 0.5f);

        CanvasGroup canvasGroup =
            rootObj.AddComponent<CanvasGroup>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        arrowRoot.SetAsLastSibling();
    }

    private void CacheButtons()
    {
        cachedButtons =
            FindObjectsByType<BodyPartButton>(
                FindObjectsSortMode.None);
    }

    private void HideUnusedArrows(int usedArrowCount)
    {
        for (int i = usedArrowCount; i < arrows.Count; i++)
        {
            arrows[i].SetActive(false);
        }
    }

    private void HideUnusedHighlights(int usedHighlightCount)
    {
        for (int i = usedHighlightCount; i < highlights.Count; i++)
        {
            highlights[i].SetActive(false);
        }
    }

    private void HideAll()
    {
        HideUnusedArrows(0);
        HideUnusedHighlights(0);
    }
}