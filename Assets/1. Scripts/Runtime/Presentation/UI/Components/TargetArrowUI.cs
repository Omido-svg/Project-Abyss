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
        public Image head;

        public void SetActive(bool active)
        {
            if (root != null && root.gameObject.activeSelf != active)
                root.gameObject.SetActive(active);
        }

        public void SetColor(Color color)
        {
            if (body != null)
                body.color = color;

            if (head != null)
                head.color = color;
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
    [SerializeField] private BattleScreenModeController screenModeController;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform arrowRoot;
    [SerializeField] private BodyPartButtonRegistry bodyPartButtonRegistry;
    [SerializeField] private BattleTargetButtonRegistry targetButtonRegistry;

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
    [SerializeField, Min(8f)] private float arrowHeadLength = 28f;
    [SerializeField, Min(8f)] private float arrowHeadWidth = 22f;

    [Header("Highlight")]
    [SerializeField] private Vector2 highlightPadding = new Vector2(12f, 12f);

    private readonly List<ArrowVisual> arrows = new();
    private readonly List<HighlightVisual> highlights = new();

    private readonly List<UIClashPair> clashPairs = new();

    private readonly HashSet<ActionSlot> clashSlots = new();
    private readonly HashSet<BodyPartButton> highlightButtons = new();

    private BattleVisualRequest currentVisualRequest;
    private BattleScreenModeController subscribedModeController;

    private static Sprite whiteSprite;
    private static Sprite arrowHeadSprite;

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        ResolveScreenModeController();

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

        EnsureButtonRegistries();
        EnsureArrowRoot();
    }

    private void OnEnable()
    {
        ResolveScreenModeController();
        SubscribeScreenModeController();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeScreenModeController();
        HideAll();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void ResolveScreenModeController()
    {
        if (screenModeController == null)
        {
            screenModeController =
                GetComponentInParent<BattleScreenModeController>();
        }

        if (screenModeController == null)
        {
            screenModeController =
                FindFirstObjectByType<BattleScreenModeController>(
                    FindObjectsInactive.Include);
        }

        SubscribeScreenModeController();
    }

    private void SubscribeScreenModeController()
    {
        if (subscribedModeController ==
            screenModeController)
        {
            return;
        }

        UnsubscribeScreenModeController();

        if (screenModeController == null)
            return;

        subscribedModeController =
            screenModeController;
        subscribedModeController.ModeChanged +=
            HandleScreenModeChanged;
    }

    private void UnsubscribeScreenModeController()
    {
        if (subscribedModeController == null)
            return;

        subscribedModeController.ModeChanged -=
            HandleScreenModeChanged;
        subscribedModeController = null;
    }

    private void HandleScreenModeChanged(
        BattleUiScreenMode mode)
    {
        if (mode == BattleUiScreenMode.CharacterDetails)
        {
            HideAll();
            return;
        }

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
        EnsureButtonRegistries();
        ResolveScreenModeController();

        // 감정 증강 3택 Overlay가 실제 화면을 덮는 동안에는
        // 배경의 계획/합/현재 행동 화살표가 선택 카드 위로 관통해 보이면 안 된다.
        if (EmotionAugmentChoiceUI.IsAnyChoiceOverlayBlockingBattleArrows)
        {
            HideAll();
            return;
        }

        // 캐릭터 상세 화면은 전투 계획을 읽는 화면이 아니므로
        // 계획/합/현재 행동 화살표와 버튼 강조를 모두 숨긴다.
        if (screenModeController?.CurrentMode ==
            BattleUiScreenMode.CharacterDetails)
        {
            HideAll();
            return;
        }

        if (battleManager == null ||
            battleManager.ActionManager == null ||
            battleManager.BattleContext == null ||
            arrowRoot == null)
        {
            HideAll();
            return;
        }

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
            {
                continue;
            }

            if (!IsPreparationSlot(slot))
                continue;

            if (!IsDrawablePreparationSlot(
                    slot,
                    player))
            {
                continue;
            }

            arrowIndex =
                DrawActionArrow(
                    arrowIndex,
                    slot,
                    preparationArrowColor);
        }

        return arrowIndex;
    }
    
    private bool IsDrawablePreparationSlot(
        ActionSlot slot,
        Character player)
    {
        if (slot?.Owner == null ||
            slot.TargetCharacter == null)
        {
            return false;
        }

        bool isPlayerAction =
            slot.Owner == player;

        if (isPlayerAction)
        {
            if (!showPlayerArrows)
                return false;
        }
        else
        {
            if (!showEnemyTargetArrows)
                return false;
        }

        BodyPartButton fromButton =
            FindButton(
                slot.Owner,
                slot.Part);

        BodyPartButton toButton =
            FindButton(
                slot.TargetCharacter,
                slot.TargetPart);

        if (fromButton == null ||
            toButton == null)
        {
            return false;
        }

        // 도사림은 자기 자신 또는 자기 부위를 대상으로 할 수 있다.
        if (slot.Owner ==
            slot.TargetCharacter)
        {
            return true;
        }

        // 상대를 대상으로 하는 특수 도사림을 허용할 경우에만 검사.
        return BattleTargetValidator.IsValid(
            slot.TargetCharacter,
            slot.TargetPart,
            TargetSelectionRule.StandardAttack);
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

        if (fromButton == null ||
            toButton == null)
        {
            return arrowIndex;
        }

        bool isSelfTarget =
            action.Owner == action.Target &&
            IsSamePart(
                action.OwnerPart,
                action.TargetPart);

        if (isSelfTarget ||
            fromButton == toButton)
        {
            return DrawSelfTargetArrow(
                arrowIndex,
                fromButton,
                color,
                action.ActionIndex,
                IsPlayerCharacter(action.Owner));
        }

        Vector2 start =
            GetLocalCenter(
                fromButton.RectTransform);

        Vector2 end =
            GetLocalCenter(
                toButton.RectTransform);

        Vector2 offset =
            GetActionIndexOffset(
                start,
                end,
                action.ActionIndex);

        start += offset;
        end += offset;

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

        Vector2 originalSourceStart = sourceStart;
        Vector2 originalOpponentStart = opponentStart;

        sourceStart += GetActionIndexOffset(
            originalSourceStart,
            originalOpponentStart,
            source.ActionIndex);

        opponentStart += GetActionIndexOffset(
            originalOpponentStart,
            originalSourceStart,
            opponent.ActionIndex);

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

        if (fromButton == null ||
            toButton == null)
        {
            return arrowIndex;
        }

        bool isSelfTarget =
            slot.Owner ==
            slot.TargetCharacter &&
            IsSamePart(
                slot.Part,
                slot.TargetPart);

        if (isSelfTarget ||
            fromButton == toButton)
        {
            return DrawSelfTargetArrow(
                arrowIndex,
                fromButton,
                color,
                slot.ActionIndex,
                IsPlayerCharacter(slot.Owner));
        }

        Vector2 start =
            GetLocalCenter(
                fromButton.RectTransform);

        Vector2 end =
            GetLocalCenter(
                toButton.RectTransform);

        Vector2 offset =
            GetActionIndexOffset(
                start,
                end,
                slot.ActionIndex);

        start += offset;
        end += offset;

        ArrowVisual arrow =
            GetArrow(arrowIndex);

        DrawArrow(
            arrow,
            start,
            end,
            color);

        return arrowIndex + 1;
    }
    
    private int DrawSelfTargetArrow(
        int arrowIndex,
        BodyPartButton button,
        Color color,
        int actionIndex,
        bool isPlayerOwner)
    {
        if (button == null ||
            button.RectTransform == null)
        {
            return arrowIndex;
        }

        GetLocalRect(
            button.RectTransform,
            out Vector2 center,
            out Vector2 size);

        float halfWidth =
            Mathf.Max(1f, size.x * 0.5f);

        float halfHeight =
            Mathf.Max(1f, size.y * 0.5f);

        float laneOffset =
            Mathf.Max(0, actionIndex) * 12f;

        Vector2 start;
        Vector2 end;

        if (isPlayerOwner)
        {
            // 플레이어 버튼은 전장 쪽(위)에서 버튼으로 내려온다.
            start =
                center +
                new Vector2(
                    halfWidth * 0.25f + laneOffset,
                    halfHeight + 58f + laneOffset);

            end =
                center +
                new Vector2(
                    halfWidth * 0.15f + laneOffset,
                    halfHeight - 4f);
        }
        else
        {
            // 적 버튼은 전장 쪽(아래)에서 버튼으로 올라간다.
            start =
                center +
                new Vector2(
                    -halfWidth * 0.25f - laneOffset,
                    -halfHeight - 58f - laneOffset);

            end =
                center +
                new Vector2(
                    -halfWidth * 0.15f - laneOffset,
                    -halfHeight + 4f);
        }

        ArrowVisual arrow =
            GetArrow(arrowIndex);

        DrawArrowToPoint(
            arrow,
            start,
            end,
            color);

        return arrowIndex + 1;
    }

    private bool IsPlayerCharacter(
        Character character)
    {
        return character != null &&
               battleManager != null &&
               battleManager.BattleContext != null &&
               battleManager.BattleContext.Player == character;
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

        Vector2 originalPlayerStart = playerStart;
        Vector2 originalEnemyStart = enemyStart;

        playerStart += GetActionIndexOffset(
            originalPlayerStart,
            originalEnemyStart,
            playerSlot.ActionIndex);

        enemyStart += GetActionIndexOffset(
            originalEnemyStart,
            originalPlayerStart,
            enemySlot.ActionIndex);

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

    private Vector2 GetActionIndexOffset(
        Vector2 start,
        Vector2 end,
        int actionIndex)
    {
        if (actionIndex <= 0)
            return Vector2.zero;

        Vector2 direction = end - start;

        if (direction.sqrMagnitude <= 0.01f)
            return Vector2.zero;

        direction.Normalize();

        Vector2 perpendicular =
            new Vector2(
                -direction.y,
                direction.x);

        int rank = (actionIndex + 1) / 2;
        float side =
            actionIndex % 2 == 1
                ? 1f
                : -1f;

        return perpendicular * rank * 8f * side;
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

        if (slots == null ||
            player == null ||
            battleManager == null ||
            battleManager.ClashBuilder == null)
        {
            return;
        }

        IReadOnlyList<ClashPair> corePairs =
            battleManager.ClashBuilder
                .BuildClashPreview(slots);

        if (corePairs == null)
            return;

        foreach (ClashPair pair in corePairs)
        {
            if (pair == null ||
                !pair.IsClash ||
                pair.First == null ||
                pair.Second == null)
            {
                continue;
            }

            result.Add(
                new UIClashPair(
                    pair.First,
                    pair.Second));
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

    private bool IsDrawableEnemySlot(
        ActionSlot slot,
        Character player)
    {
        if (!showEnemyTargetArrows ||
            slot?.Owner == null ||
            slot.TargetCharacter == null)
        {
            return false;
        }

        if (slot.Owner == player ||
            slot.TargetCharacter != player)
        {
            return false;
        }

        if (FindButton(
                slot.Owner,
                slot.Part) == null)
        {
            return false;
        }

        return BattleTargetValidator.IsValid(
            slot.TargetCharacter,
            slot.TargetPart,
            TargetSelectionRule.StandardAttack);
    }

    private bool IsDrawablePlayerSlot(
        ActionSlot slot,
        Character player)
    {
        if (!showPlayerArrows ||
            slot?.Owner == null ||
            slot.TargetCharacter == null)
        {
            return false;
        }

        if (slot.Owner != player ||
            slot.TargetCharacter == player)
        {
            return false;
        }

        if (FindButton(
                slot.Owner,
                slot.Part) == null)
        {
            return false;
        }

        return BattleTargetValidator.IsValid(
            slot.TargetCharacter,
            slot.TargetPart,
            TargetSelectionRule.StandardAttack);
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

        if (slot.ActionId > 0 &&
            action.ActionId > 0)
        {
            return slot.ActionId == action.ActionId;
        }

        return
            slot.Owner == action.Owner &&
            slot.Part == action.OwnerPart &&
            slot.ActionIndex == action.ActionIndex;
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

        BattleAction source =
            currentVisualRequest.SourceAction;

        BattleAction opponent =
            currentVisualRequest.OpponentAction;

        bool isClash =
            source != null &&
            opponent != null &&
            currentVisualRequest.ClashSteps != null &&
            currentVisualRequest.ClashSteps.Count > 0;

        if (isClash)
        {
            // 합은 두 행동 주체 부위만 노란색
            AddActionOwnerHighlightButton(
                source,
                highlightButtons);

            AddActionOwnerHighlightButton(
                opponent,
                highlightButtons);
        }
        else
        {
            // 일반 공격 / 도사림 / 위세는 행동 주체 부위만 노란색
            AddActionOwnerHighlightButton(
                source,
                highlightButtons);
        }

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
    
    private void AddActionOwnerHighlightButton(
        BattleAction action,
        HashSet<BodyPartButton> result)
    {
        if (action == null)
            return;

        BodyPartButton ownerButton =
            FindButton(
                action.Owner,
                action.OwnerPart);

        if (ownerButton != null)
            result.Add(ownerButton);
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

    private void EnsureButtonRegistries()
    {
        if (bodyPartButtonRegistry == null)
        {
            bodyPartButtonRegistry =
                FindFirstObjectByType<BodyPartButtonRegistry>();
        }

        if (targetButtonRegistry == null)
        {
            targetButtonRegistry =
                FindFirstObjectByType<BattleTargetButtonRegistry>();
        }
    }

    private BodyPartButton FindButton(
        Character character,
        BodyPart part)
    {
        if (character == null)
            return null;

        EnsureButtonRegistries();

        BodyPartButton button =
            targetButtonRegistry?.FindButton(
                character,
                part,
                requireActive: true);

        if (button != null)
            return button;

        return bodyPartButtonRegistry?.Find(
            character,
            part,
            requireActive: true);
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

        float availableLength =
            Vector2.Distance(start, end);

        float resolvedHeadLength =
            Mathf.Min(
                arrowHeadLength,
                Mathf.Max(8f, availableLength * 0.36f));

        float resolvedHeadWidth =
            Mathf.Min(
                arrowHeadWidth,
                Mathf.Max(8f, resolvedHeadLength * 0.82f));

        Vector2 bodyEnd =
            end - dir * resolvedHeadLength * 0.72f;

        if (Vector2.Dot(bodyEnd - start, dir) <= 1f)
            bodyEnd = start + dir * Mathf.Max(1f, availableLength * 0.25f);

        DrawSegment(
            arrow.body,
            start,
            bodyEnd,
            lineThickness);

        DrawArrowHead(
            arrow.head,
            end,
            dir,
            resolvedHeadLength,
            resolvedHeadWidth);
    }

    private void DrawArrowHead(
        Image image,
        Vector2 tip,
        Vector2 direction,
        float length,
        float width)
    {
        if (image == null || direction.sqrMagnitude <= 0.0001f)
            return;

        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = tip;
        rect.sizeDelta = new Vector2(length, width);
        rect.localEulerAngles =
            new Vector3(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
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
            head = CreateArrowHeadImage("Arrow Head", rootRect)
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

    private Image CreateArrowHeadImage(
        string objectName,
        Transform parent)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(parent, false);

        Image image = obj.AddComponent<Image>();
        image.sprite = GetArrowHeadSprite();
        image.color = Color.white;
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        // 생성되는 Sprite의 뾰족한 끝이 오른쪽에 있으므로 Pivot을 끝점에 둔다.
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        return image;
    }

    private Sprite GetArrowHeadSprite()
    {
        if (arrowHeadSprite != null)
            return arrowHeadSprite;

        const int width = 32;
        const int height = 32;

        Texture2D texture =
            new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false)
            {
                name = "Project Abyss UI Arrow Head",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSaveInBuild
            };

        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 solid = new Color32(255, 255, 255, 255);
        Color32[] pixels = new Color32[width * height];

        float centerY = (height - 1) * 0.5f;
        float maxHalfHeight = centerY;

        for (int x = 0; x < width; x++)
        {
            float normalized = x / (float)(width - 1);
            float halfHeight = (1f - normalized) * maxHalfHeight;

            for (int y = 0; y < height; y++)
            {
                pixels[y * width + x] =
                    Mathf.Abs(y - centerY) <= halfHeight
                        ? solid
                        : clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        arrowHeadSprite =
            Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(1f, 0.5f),
                32f);

        arrowHeadSprite.name = "Project Abyss UI Arrow Head Sprite";
        arrowHeadSprite.hideFlags = HideFlags.DontSaveInBuild;
        return arrowHeadSprite;
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
