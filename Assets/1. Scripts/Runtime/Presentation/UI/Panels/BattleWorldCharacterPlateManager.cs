using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleWorldCharacterPlateManager : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleUIManager battleUiManager;
    [SerializeField] private BattleCharacterDetailPanelUI detailPanel;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool showPlayerPlate;
    [SerializeField] private Vector3 worldOffset = new(0f, 0.48f, 0f);
    [SerializeField] private float canvasScale = 0.0062f;
    [SerializeField] private BattleWorldSlotArrowOverlayUI slotArrowOverlay;
    [SerializeField] private BattleActionOrderRailUI actionOrderRail;

    private readonly List<GameObject> generated = new();
    private readonly Dictionary<Character, BattleWorldCharacterPlateUI> plates = new();

    private Transform resolutionOriginalParent;
    private int resolutionOriginalSiblingIndex = -1;
    private bool resolutionReparented;
    private bool resolutionPresentationActive;

    public void Configure(
        BattleManager manager,
        BattleUIManager uiManager,
        BattleCharacterDetailPanelUI panel,
        Camera camera)
    {
        battleManager = manager;
        battleUiManager = uiManager;
        detailPanel = panel;
        targetCamera = camera;
        EnsurePlanningPresentation();
    }

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (battleUiManager == null)
            battleUiManager = FindFirstObjectByType<BattleUIManager>();

        if (detailPanel == null)
        {
            detailPanel =
                FindFirstObjectByType<BattleCharacterDetailPanelUI>(
                    FindObjectsInactive.Include);
        }

        targetCamera ??= Camera.main;
        EnsurePlanningPresentation();

        // 2026-08-17 전술 UI에서는 플레이어 슬롯도 머리 위에서 직접 드래그해야 한다.
        // 과거 Scene에 showPlayerPlate=false가 직렬화되어 있어도 런타임 계약을 우선한다.
        showPlayerPlate = true;

        if (battleManager != null)
            battleManager.BattlePrepared += OnBattlePrepared;
    }

    private void Start()
    {
        if (battleManager?.BattleContext != null)
            Build(battleManager.BattleContext);
    }

    private void OnDestroy()
    {
        if (battleManager != null)
            battleManager.BattlePrepared -= OnBattlePrepared;

        Clear();
    }

    private void OnBattlePrepared(BattleContext context)
    {
        Build(context);
    }


    /// <summary>
    /// Resolution 중에는 캐릭터 월드 HUD(HP/흐트러짐/고유 게이지)는 유지하되
    /// 머리 위 행동 슬롯/계획 보조 UI는 숨긴다.
    /// UI Root 아래에 배치된 Scene도 부모 비활성화의 영향을 받지 않도록
    /// 필요할 때 Manager 자체를 Scene Root로 임시 분리한다.
    /// </summary>
    public void PreserveForResolution(
        Transform battleUiRoot)
    {
        resolutionPresentationActive = true;
        SetActionSlotStripsHidden(true);
        slotArrowOverlay?.SetResolutionHidden(true);

        if (resolutionReparented ||
            battleUiRoot == null ||
            !transform.IsChildOf(battleUiRoot))
        {
            return;
        }

        resolutionOriginalParent =
            transform.parent;

        resolutionOriginalSiblingIndex =
            transform.GetSiblingIndex();

        transform.SetParent(
            null,
            true);

        resolutionReparented = true;
    }

    public void RestoreAfterResolution()
    {
        resolutionPresentationActive = false;
        SetActionSlotStripsHidden(false);
        slotArrowOverlay?.SetResolutionHidden(false);

        if (!resolutionReparented)
            return;

        Transform originalParent =
            resolutionOriginalParent;

        if (originalParent != null)
        {
            transform.SetParent(
                originalParent,
                true);

            int lastIndex =
                Mathf.Max(
                    0,
                    originalParent.childCount - 1);

            transform.SetSiblingIndex(
                Mathf.Clamp(
                    resolutionOriginalSiblingIndex,
                    0,
                    lastIndex));
        }

        resolutionOriginalParent = null;
        resolutionOriginalSiblingIndex = -1;
        resolutionReparented = false;
    }

    public void Build(BattleContext context)
    {
        if (context == null)
            return;

        EnsurePlanningPresentation();
        Clear();

        if (showPlayerPlate && context.Player != null)
            CreateForCharacter(context.Player);

        if (context.Enemies != null)
        {
            foreach (Character enemy in context.Enemies)
            {
                if (enemy != null)
                    CreateForCharacter(enemy);
            }
        }

    }

    private void EnsurePlanningPresentation()
    {
        EnsureSlotArrowOverlay();
        EnsureActionOrderRail();
    }

    private void EnsureActionOrderRail()
    {
        if (actionOrderRail == null)
            actionOrderRail = GetComponent<BattleActionOrderRailUI>();

        if (actionOrderRail == null)
            actionOrderRail = gameObject.AddComponent<BattleActionOrderRailUI>();

        actionOrderRail.Configure(
            battleManager,
            battleUiManager,
            targetCamera);
    }

    private void EnsureSlotArrowOverlay()
    {
        if (slotArrowOverlay == null)
            slotArrowOverlay = GetComponent<BattleWorldSlotArrowOverlayUI>();

        if (slotArrowOverlay == null)
            slotArrowOverlay = gameObject.AddComponent<BattleWorldSlotArrowOverlayUI>();

        slotArrowOverlay.Configure(
            battleManager,
            battleUiManager,
            targetCamera);
    }

    private void CreateForCharacter(Character character)
    {
        if (character == null)
            return;

        BattleCharacterWorldClickInspector inspector =
            character.GetComponent<BattleCharacterWorldClickInspector>();

        if (inspector == null)
            inspector = character.gameObject.AddComponent<BattleCharacterWorldClickInspector>();

        inspector.Configure(
            character,
            battleUiManager,
            detailPanel);

        GameObject root =
            new(
                "BattleWorldInfoPlate",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(BattleWorldCharacterPlateUI));

        Transform followTarget =
            ResolvePlateFollowTarget(character);

        // UI를 캐릭터 VisualRoot의 자식으로 두면 모델 회전/스케일을 상속해
        // 카메라 billboard 정렬이 흔들릴 수 있다. Battle World UI 아래에 독립시킨다.
        root.transform.SetParent(
            transform,
            false);

        Vector3 initialWorldPosition =
            FindWorldTop(character) +
            worldOffset;

        Vector3 followLocalPosition =
            followTarget.InverseTransformPoint(
                initialWorldPosition);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 92f);
        // 초기값만 주고 실제 화면상 크기는 BattleWorldCharacterPlateUI가
        // 현재 Camera 투영에 맞춰 LateUpdate에서 정규화한다.
        rect.localScale = Vector3.one * canvasScale;
        rect.position = initialWorldPosition;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = targetCamera;
        canvas.sortingOrder = 30;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 20f;

        Image background =
            CreateImage(
                "Background",
                rect,
                new Color(0.035f, 0.045f, 0.065f, 0.94f));

        Stretch(background.rectTransform);

        TMP_Text name =
            CreateText(
                "Name",
                rect,
                29f,
                TextAlignmentOptions.Left);

        SetRect(
            name.rectTransform,
            new Vector2(0.04f, 0.53f),
            new Vector2(0.72f, 0.94f));

        TMP_Text hp =
            CreateText(
                "Hp",
                rect,
                24f,
                TextAlignmentOptions.Right);

        SetRect(
            hp.rectTransform,
            new Vector2(0.70f, 0.53f),
            new Vector2(0.96f, 0.94f));

        Image hpTrack =
            CreateImage(
                "HpTrack",
                rect,
                new Color(0.12f, 0.12f, 0.14f, 1f));

        SetRect(
            hpTrack.rectTransform,
            new Vector2(0.04f, 0.16f),
            new Vector2(0.96f, 0.39f));

        Image hpDamageTrail =
            CreateImage(
                "HpDamageTrail",
                hpTrack.rectTransform,
                new Color(1f, 0.72f, 0.18f, 1f));

        Stretch(hpDamageTrail.rectTransform);
        hpDamageTrail.type = Image.Type.Simple;
        hpDamageTrail.preserveAspect = false;
        hpDamageTrail.rectTransform.pivot = new Vector2(0f, 0.5f);

        Image hpFill =
            CreateImage(
                "HpFill",
                hpTrack.rectTransform,
                new Color(0.86f, 0.16f, 0.16f, 1f));

        Stretch(hpFill.rectTransform);
        hpFill.type = Image.Type.Simple;
        hpFill.preserveAspect = false;
        hpFill.rectTransform.pivot = new Vector2(0f, 0.5f);

        BattleWorldCharacterPlateUI plate =
            root.GetComponent<BattleWorldCharacterPlateUI>();

        plate.Configure(
            character,
            targetCamera,
            name,
            hp,
            hpFill,
            hpDamageTrail,
            followTarget,
            followLocalPosition);

        BattleWorldActionSlotStripUI strip =
            root.GetComponent<BattleWorldActionSlotStripUI>();

        strip?.SetResolutionHidden(
            resolutionPresentationActive);

        plates[character] = plate;
        generated.Add(root);
    }

    public void SetVisualHpOverride(
        Character character,
        int hp,
        bool forceImmediate = false)
    {
        if (character == null)
            return;

        if (plates.TryGetValue(character, out BattleWorldCharacterPlateUI plate) &&
            plate != null)
        {
            plate.SetHpOverride(
                hp,
                forceImmediate);
        }
    }

    public void SetVisualHpOverrideAtHit(
        Character character,
        int hp)
    {
        if (character == null)
            return;

        if (plates.TryGetValue(character, out BattleWorldCharacterPlateUI plate) &&
            plate != null)
        {
            plate.SetHpOverrideAtHit(hp);
        }
    }

    public void ClearVisualHpOverride(Character character)
    {
        if (character == null)
            return;

        if (plates.TryGetValue(character, out BattleWorldCharacterPlateUI plate) &&
            plate != null)
        {
            plate.ClearHpOverride();
        }
    }

    public void SetVisualStaggerOverride(
        Character character,
        int currentGauge,
        bool vulnerable)
    {
        if (character == null)
            return;

        if (!plates.TryGetValue(
                character,
                out BattleWorldCharacterPlateUI plate) ||
            plate == null)
        {
            return;
        }

        BattleWorldUniqueGaugeUI gauges =
            plate.GetComponent<BattleWorldUniqueGaugeUI>();

        gauges?.SetStaggerVisualOverride(
            currentGauge,
            vulnerable);
    }

    public void SetVisualStaggerOverrideAtHit(
        Character character,
        int currentGauge,
        bool vulnerable)
    {
        if (character == null)
            return;

        if (!plates.TryGetValue(
                character,
                out BattleWorldCharacterPlateUI plate) ||
            plate == null)
        {
            return;
        }

        BattleWorldUniqueGaugeUI gauges =
            plate.GetComponent<BattleWorldUniqueGaugeUI>();

        gauges?.SetStaggerVisualOverrideAtHit(
            currentGauge,
            vulnerable);
    }

    public void ClearVisualStaggerOverride(
        Character character)
    {
        if (character == null)
            return;

        if (!plates.TryGetValue(
                character,
                out BattleWorldCharacterPlateUI plate) ||
            plate == null)
        {
            return;
        }

        BattleWorldUniqueGaugeUI gauges =
            plate.GetComponent<BattleWorldUniqueGaugeUI>();

        gauges?.ClearStaggerVisualOverride();
    }

    public void RefreshCharacter(Character character)
    {
        if (character == null)
            return;

        if (plates.TryGetValue(character, out BattleWorldCharacterPlateUI plate) &&
            plate != null)
        {
            plate.RefreshNow();

            BattleWorldUniqueGaugeUI gauges =
                plate.GetComponent<BattleWorldUniqueGaugeUI>();

            gauges?.RefreshNow();
        }
    }

    private static Transform ResolvePlateFollowTarget(
        Character character)
    {
        if (character == null)
            return null;

        CharacterActionMover mover =
            character.GetComponent<CharacterActionMover>();

        if (mover != null &&
            mover.VisualRoot != null)
        {
            return mover.VisualRoot;
        }

        CharacterView view =
            BattleCameraTargetResolver.GetView(
                character);

        if (view != null)
            return view.transform;

        return character.transform;
    }

    private static Vector3 FindWorldTop(Character character)
    {
        Renderer[] renderers =
            character.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
            return character.transform.position + Vector3.up * 2f;

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return new Vector3(
            bounds.center.x,
            bounds.max.y,
            bounds.center.z);
    }

    private void SetActionSlotStripsHidden(
        bool hidden)
    {
        foreach (GameObject target in generated)
        {
            if (target == null)
                continue;

            BattleWorldActionSlotStripUI strip =
                target.GetComponent<BattleWorldActionSlotStripUI>();

            strip?.SetResolutionHidden(hidden);
        }
    }

    private void Clear()
    {
        plates.Clear();

        foreach (GameObject target in generated)
        {
            if (target != null)
                Destroy(target);
        }

        generated.Clear();
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Color color)
    {
        GameObject target =
            new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        target.transform.SetParent(parent, false);

        Image image = target.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject target =
            new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        target.transform.SetParent(parent, false);

        TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 min,
        Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}