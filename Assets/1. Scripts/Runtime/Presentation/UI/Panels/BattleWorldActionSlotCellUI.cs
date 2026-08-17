using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 머리 위 ActionSlot 셀.
/// - 플레이어 셀: 클릭하면 해당 부위/ActionIndex의 스킬 패널을 연다.
/// - 적 셀: 대상 선택 중 클릭/드롭하면 정확한 TargetSlot을 타깃 의도로 저장한다.
/// - Hover/Select/Pending Target 상태를 애니메이션으로 표현한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleWorldActionSlotCellUI : MonoBehaviour,
    IPointerClickHandler,
    IDropHandler,
    IPointerEnterHandler,
    IPointerExitHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private static readonly List<BattleWorldActionSlotCellUI> ActiveCells = new();
    /// <summary>
    /// 현재 마우스가 올라가 있는 적 ActionSlot 셀.
    /// Target 선택 중 간이 합 승률 프리뷰가 이 값을 사용한다.
    /// </summary>
    public static BattleWorldActionSlotCellUI HoveredTargetCell { get; private set; }


    [Header("Interaction Animation")]
    [SerializeField, Min(0.01f)] private float animationSpeed = 12f;
    [SerializeField, Range(1f, 1.5f)] private float hoverScale = 1.07f;
    [SerializeField, Range(1f, 1.5f)] private float selectedScale = 1.11f;
    [SerializeField, Min(0.1f)] private float pendingPulseSeconds = 1.25f;

    private BattleUIManager manager;
    private Character owner;
    private BodyPart part;
    private int actionIndex;
    private ActionSlot targetSlot;
    private ActionSlot plannedSlot;
    private TMP_Text speedText;
    private TMP_Text skillText;
    private Image background;
    private Outline outline;
    private RectTransform selfRect;
    private Color normalColor;
    private Color currentColor;
    private Vector3 currentScale = Vector3.one;
    private bool pointerInside;

    private static readonly Color HoverTint = new(1f, 1f, 1f, 1f);
    private static readonly Color SelectedTint = new(1f, 0.77f, 0.20f, 1f);
    private static readonly Color PendingTint = new(1f, 0.88f, 0.24f, 1f);
    private static readonly Color AssignedSkillTint = new(0.16f, 0.58f, 0.88f, 1f);
    private static readonly Color AssignedTargetTint = new(1f, 0.36f, 0.22f, 1f);

    public Character Owner => owner;
    public BodyPart Part => part;
    public int ActionIndex => actionIndex;
    public ActionSlot TargetSlot => targetSlot;
    public ActionSlot PlannedSlot => plannedSlot;
    public RectTransform RectTransform => selfRect;
    public bool IsPlanningCell => targetSlot == null;
    public bool IsTargetCell => targetSlot != null;

    private void Awake()
    {
        selfRect = transform as RectTransform;
        outline = GetComponent<Outline>();
        currentScale = Vector3.one;
    }

    private void OnEnable()
    {
        if (!ActiveCells.Contains(this))
            ActiveCells.Add(this);
    }

    private void OnDisable()
    {
        ActiveCells.Remove(this);

        if (HoveredTargetCell == this)
            HoveredTargetCell = null;
    }

    private void OnDestroy()
    {
        ActiveCells.Remove(this);

        if (HoveredTargetCell == this)
            HoveredTargetCell = null;
    }

    public void ConfigurePlanning(
        BattleUIManager uiManager,
        Character character,
        BodyPart bodyPart,
        int index,
        ActionSlot existingPlannedSlot,
        TMP_Text speed,
        TMP_Text skillName,
        Image image,
        Outline cellOutline)
    {
        manager = uiManager;
        owner = character;
        part = bodyPart;
        actionIndex = Mathf.Max(0, index);
        targetSlot = null;
        plannedSlot = existingPlannedSlot;
        speedText = speed;
        skillText = skillName;
        background = image;
        outline = cellOutline != null ? cellOutline : GetComponent<Outline>();
        selfRect ??= transform as RectTransform;
        normalColor = background != null ? background.color : Color.white;
        currentColor = normalColor;
        RefreshLabel();
        ApplyImmediateVisual();
    }

    public void ConfigureTarget(
        BattleUIManager uiManager,
        ActionSlot slot,
        TMP_Text speed,
        TMP_Text skillName,
        Image image,
        Outline cellOutline)
    {
        manager = uiManager;
        targetSlot = slot;
        plannedSlot = null;
        owner = slot?.Owner;
        part = slot?.Part;
        actionIndex = slot?.ActionIndex ?? 0;
        speedText = speed;
        skillText = skillName;
        background = image;
        outline = cellOutline != null ? cellOutline : GetComponent<Outline>();
        selfRect ??= transform as RectTransform;
        normalColor = background != null ? background.color : Color.white;
        currentColor = normalColor;
        RefreshLabel();
        ApplyImmediateVisual();
    }

    private void LateUpdate()
    {
        if (manager == null)
            manager = FindFirstObjectByType<BattleUIManager>(FindObjectsInactive.Include);

        // SpeedManager의 턴 시작 굴림은 UI 셀이 만들어진 뒤 갱신될 수도 있으므로
        // 빈 슬롯도 매 프레임 현재 턴 속도를 동기화한다.
        RefreshPlanningSpeedText();

        bool selected = IsSelected();
        bool pending = IsPendingTargetSelection();
        bool assignedSkill = IsSkillAssigned();
        bool assignedTarget = IsAssignedTarget();

        float scale = pointerInside
            ? hoverScale
            : selected || assignedTarget
                ? selectedScale
                : 1f;

        if (pending)
        {
            float pulse =
                0.5f +
                0.5f * Mathf.Sin(
                    Time.unscaledTime *
                    Mathf.PI * 2f /
                    Mathf.Max(0.1f, pendingPulseSeconds));

            scale = Mathf.Lerp(selectedScale, selectedScale + 0.035f, pulse);
        }

        Vector3 wantedScale = Vector3.one * scale;
        float t = 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);
        currentScale = Vector3.Lerp(currentScale, wantedScale, t);

        if (selfRect != null)
            selfRect.localScale = currentScale;

        Color wantedColor = normalColor;

        // 플레이어 계획 슬롯에 스킬이 배치되어 있으면 빈 슬롯과 즉시 구분되도록
        // 안정적인 청색 계열을 기본 상태로 사용한다. Pending/Select/Hover가 그 위를 덮는다.
        if (assignedSkill)
            wantedColor = Color.Lerp(normalColor, AssignedSkillTint, 0.58f);

        if (assignedTarget)
            wantedColor = Color.Lerp(normalColor, AssignedTargetTint, 0.22f);

        if (selected)
            wantedColor = Color.Lerp(normalColor, SelectedTint, 0.32f);

        if (pointerInside)
            wantedColor = Color.Lerp(wantedColor, HoverTint, 0.24f);

        if (pending)
        {
            float pulse =
                0.5f +
                0.5f * Mathf.Sin(
                    Time.unscaledTime *
                    Mathf.PI * 2f /
                    Mathf.Max(0.1f, pendingPulseSeconds));

            wantedColor = Color.Lerp(
                Color.Lerp(normalColor, PendingTint, 0.28f),
                Color.Lerp(normalColor, PendingTint, 0.62f),
                pulse);
        }

        currentColor = Color.Lerp(currentColor, wantedColor, t);
        if (background != null)
            background.color = currentColor;

        if (outline != null)
        {
            Color outlineColor = Color.black;
            float alpha = pending
                ? 0.95f
                : selected || assignedTarget
                    ? 0.80f
                    : assignedSkill
                        ? 0.68f
                        : pointerInside
                            ? 0.62f
                            : 0.46f;

            if (assignedSkill && !pending && !selected)
            {
                outlineColor = new Color(0.18f, 0.72f, 1f, alpha);
            }
            else
            {
                outlineColor.a = alpha;
            }

            outline.effectColor = outlineColor;
            outline.effectDistance = pending || selected
                ? new Vector2(3f, -3f)
                : new Vector2(2f, -2f);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager == null || eventData == null)
            return;

        // 플레이어의 이미 배치된 슬롯은 우클릭 즉시 취소한다.
        // 스킬 패널을 다시 열지 않고 ActionManager에서 해당 ActionIndex만 제거한다.
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (targetSlot == null &&
                plannedSlot?.Skill != null &&
                owner != null &&
                part != null)
            {
                BattleSkillDragContext.Clear();
                manager.RemoveActionSlot(
                    owner,
                    part,
                    actionIndex);
            }

            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (targetSlot != null)
        {
            // 스킬 카드 선택 후에는 적의 정확한 행동 슬롯을 클릭해서 타깃을 지정한다.
            // 합/일방공격 여부는 속도 규칙에 따라 별도로 계산된다.
            if (manager.IsSelectingTarget)
                manager.TryAssignSelectedSkillToActionSlot(targetSlot);

            return;
        }

        if (owner == null || part == null)
            return;

        manager.SelectOwnerSlotAndOpenSkills(owner, part, actionIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 아군 머리 위에 이미 배치된 전투 슬롯만 슬롯→슬롯 드래그의 시작점이 된다.
        if (targetSlot != null || plannedSlot == null || plannedSlot.Skill == null)
            return;

        BattleSkillDragContext.BeginSlot(plannedSlot);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 월드 슬롯 자체는 이동하지 않는다.
        // BattleWorldSlotArrowOverlayUI가 SourceCell -> Mouse 화살표를 표시한다.
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (BattleSkillDragContext.PlannedSlot == plannedSlot)
            BattleSkillDragContext.Clear();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (manager == null || targetSlot == null || !BattleSkillDragContext.HasPayload)
            return;

        if (BattleSkillDragContext.HasSlotPayload)
        {
            manager.TryRetargetPlannedActionSlot(
                BattleSkillDragContext.PlannedSlot,
                targetSlot);
            return;
        }

        if (BattleSkillDragContext.HasSkillPayload)
        {
            manager.TryAssignDraggedSkillToActionSlot(
                BattleSkillDragContext.Skill,
                BattleSkillDragContext.ActionIndex,
                targetSlot);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;

        if (targetSlot != null)
            HoveredTargetCell = this;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;

        if (HoveredTargetCell == this)
            HoveredTargetCell = null;
    }

    private bool IsSelected()
    {
        if (manager == null)
            return false;

        if (targetSlot == null)
        {
            return manager.IsWorldPlanningSlotSelected(
                owner,
                part,
                actionIndex);
        }

        return manager.IsWorldTargetSlotAssigned(targetSlot);
    }

    private bool IsPendingTargetSelection()
    {
        return targetSlot == null &&
               manager != null &&
               manager.IsWorldPlanningSlotPendingTarget(
                   owner,
                   part,
                   actionIndex);
    }

    private bool IsSkillAssigned()
    {
        return targetSlot == null &&
               plannedSlot?.Skill != null;
    }

    private bool IsAssignedTarget()
    {
        return targetSlot != null &&
               manager != null &&
               manager.IsWorldTargetSlotAssigned(targetSlot);
    }

    private void ApplyImmediateVisual()
    {
        selfRect ??= transform as RectTransform;
        currentScale = Vector3.one;
        currentColor = normalColor;

        if (selfRect != null)
            selfRect.localScale = currentScale;

        if (background != null)
            background.color = currentColor;
    }

    private void RefreshPlanningSpeedText()
    {
        if (targetSlot != null ||
            speedText == null ||
            owner == null)
        {
            return;
        }

        int displaySpeed =
            plannedSlot?.Skill != null
                ? plannedSlot.Speed
                : manager?.GetWorldPlanningSlotSpeed(
                    owner,
                    part) ?? 0;

        speedText.gameObject.SetActive(true);
        speedText.text = displaySpeed.ToString();
    }

    private void RefreshLabel()
    {
        if (speedText != null)
        {
            speedText.text = string.Empty;
            speedText.gameObject.SetActive(false);
        }

        if (skillText == null)
            return;

        if (targetSlot != null)
        {
            if (speedText != null)
            {
                speedText.gameObject.SetActive(true);
                speedText.text = targetSlot.Speed.ToString();
            }

            skillText.text = targetSlot.Skill?.SkillName ?? "행동";
            return;
        }

        // 플레이어 슬롯은 스킬 지정 여부와 무관하게 속도를 항상 표시한다.
        RefreshPlanningSpeedText();

        string partName = part?.Type switch
        {
            PartType.HEAD => "머리",
            PartType.LEFT_HAND => "왼팔",
            PartType.RIGHT_HAND => "오른팔",
            PartType.LEGS => "다리",
            _ => "슬롯"
        };

        if (plannedSlot?.Skill != null)
        {
            skillText.text = plannedSlot.TargetSlot != null
                ? $"{plannedSlot.Skill.SkillName}  →"
                : plannedSlot.Skill.SkillName;
            return;
        }

        skillText.text = $"{partName}\n{actionIndex + 1}";
    }

    public static bool TryGetPlanningCell(
        Character character,
        BodyPart bodyPart,
        int index,
        out BattleWorldActionSlotCellUI cell)
    {
        for (int i = ActiveCells.Count - 1; i >= 0; i--)
        {
            BattleWorldActionSlotCellUI candidate = ActiveCells[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;

            if (!candidate.IsPlanningCell ||
                candidate.owner != character ||
                candidate.actionIndex != index ||
                !IsSamePart(candidate.part, bodyPart))
            {
                continue;
            }

            cell = candidate;
            return true;
        }

        cell = null;
        return false;
    }

    public static bool TryGetPlanningCell(
        ActionSlot slot,
        out BattleWorldActionSlotCellUI cell)
    {
        if (slot == null)
        {
            cell = null;
            return false;
        }

        return TryGetPlanningCell(
            slot.Owner,
            slot.Part,
            slot.ActionIndex,
            out cell);
    }

    public static bool TryGetTargetCell(
        ActionSlot slot,
        out BattleWorldActionSlotCellUI cell)
    {
        if (slot == null)
        {
            cell = null;
            return false;
        }

        for (int i = ActiveCells.Count - 1; i >= 0; i--)
        {
            BattleWorldActionSlotCellUI candidate = ActiveCells[i];
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.IsTargetCell)
                continue;

            ActionSlot candidateSlot = candidate.targetSlot;
            if (candidateSlot == slot ||
                candidateSlot != null &&
                candidateSlot.ActionId == slot.ActionId &&
                candidateSlot.Owner == slot.Owner)
            {
                cell = candidate;
                return true;
            }
        }

        cell = null;
        return false;
    }

    /// <summary>
    /// TargetSlot이 아직 없는 일반 계획/AI 의도 화살표가 BodyPart 단위 목적지를 찾을 때 사용한다.
    /// 같은 부위에 슬롯이 여러 개면 가장 낮은 ActionIndex의 계획 슬롯을 대표점으로 사용한다.
    /// </summary>
    public static bool TryGetPlanningCell(
        Character character,
        BodyPart bodyPart,
        out BattleWorldActionSlotCellUI cell)
    {
        BattleWorldActionSlotCellUI best = null;

        for (int i = ActiveCells.Count - 1; i >= 0; i--)
        {
            BattleWorldActionSlotCellUI candidate = ActiveCells[i];

            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.IsPlanningCell ||
                candidate.owner != character ||
                !IsSamePartOrBothNull(candidate.part, bodyPart))
            {
                continue;
            }

            if (best == null ||
                candidate.actionIndex < best.actionIndex)
            {
                best = candidate;
            }
        }

        cell = best;
        return cell != null;
    }

    /// <summary>
    /// 정확한 TargetSlot이 없는 기존/AI 계획을 월드 슬롯에 투영하기 위한 BodyPart 단위 검색.
    /// 같은 부위에 적 슬롯이 여러 개면 속도가 높은 슬롯, 그 다음 낮은 ActionIndex를 대표점으로 사용한다.
    /// 실제 합은 ClashPreview가 정확한 ActionSlot 쌍을 별도로 표시한다.
    /// </summary>
    public static bool TryGetTargetCell(
        Character character,
        BodyPart bodyPart,
        out BattleWorldActionSlotCellUI cell)
    {
        BattleWorldActionSlotCellUI best = null;

        for (int i = ActiveCells.Count - 1; i >= 0; i--)
        {
            BattleWorldActionSlotCellUI candidate = ActiveCells[i];

            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.IsTargetCell ||
                candidate.owner != character ||
                !IsSamePartOrBothNull(candidate.part, bodyPart))
            {
                continue;
            }

            if (best == null)
            {
                best = candidate;
                continue;
            }

            int candidateSpeed =
                candidate.targetSlot?.Speed ??
                int.MinValue;

            int bestSpeed =
                best.targetSlot?.Speed ??
                int.MinValue;

            if (candidateSpeed > bestSpeed ||
                candidateSpeed == bestSpeed &&
                candidate.actionIndex < best.actionIndex)
            {
                best = candidate;
            }
        }

        cell = best;
        return cell != null;
    }

    public Vector2 GetScreenCenter(Camera camera)
    {
        selfRect ??= transform as RectTransform;
        if (selfRect == null)
            return Vector2.zero;

        Vector3 worldCenter = selfRect.TransformPoint(selfRect.rect.center);
        Camera useCamera = camera != null ? camera : Camera.main;
        if (useCamera == null)
            return Vector2.zero;

        Vector3 screen = useCamera.WorldToScreenPoint(worldCenter);
        return new Vector2(screen.x, screen.y);
    }

    private static bool IsSamePartOrBothNull(
        BodyPart a,
        BodyPart b)
    {
        if (a == null || b == null)
            return a == null && b == null;

        return IsSamePart(a, b);
    }

    private static bool IsSamePart(BodyPart a, BodyPart b)
    {
        if (a == b)
            return true;

        if (a == null || b == null)
            return false;

        return a.Owner == b.Owner && a.Type == b.Type;
    }
}