using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public sealed class BattleCharacterPointerRouter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private BattleUIManager battleUiManager;
    [SerializeField] private BattleCharacterDetailPanelUI detailPanel;

    [Header("Picking")]
    [SerializeField] private LayerMask characterLayers = ~0;
    [SerializeField, Min(1f)] private float maximumRayDistance = 1000f;
    [SerializeField] private QueryTriggerInteraction triggerInteraction =
        QueryTriggerInteraction.Collide;

    [Header("Diagnostics")]
    [SerializeField] private bool logClickResolution = true;

    private readonly List<Candidate> candidates =
        new();

    private readonly List<RaycastResult> uiRaycastResults =
        new();

    private static int worldInputBlockedUntilFrame = -1;

    private Character hoveredCharacter;
    private Character selectedCharacter;

    public static BattleCharacterPointerRouter Instance
    {
        get;
        private set;
    }

    public Character HoveredCharacter => hoveredCharacter;
    public Character SelectedCharacter => selectedCharacter;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
        worldInputBlockedUntilFrame = -1;
    }

    /// <summary>
    /// UGUI Button의 onClick과 월드 클릭 Router가 같은 마우스 업/다운을
    /// 처리하지 않도록 몇 프레임 동안 월드 입력을 차단한다.
    /// </summary>
    public static void BlockWorldInputForFrames(
        int frameCount = 2)
    {
        int safeFrameCount =
            Mathf.Max(1, frameCount);

        worldInputBlockedUntilFrame =
            Mathf.Max(
                worldInputBlockedUntilFrame,
                Time.frameCount + safeFrameCount);
    }

    public static bool IsWorldInputTemporarilyBlocked =>
        Time.frameCount <= worldInputBlockedUntilFrame;

    public void Configure(
        Camera camera,
        BattleUIManager uiManager,
        BattleCharacterDetailPanelUI panel,
        bool enableLogs = true)
    {
        targetCamera = camera;
        battleUiManager = uiManager;
        detailPanel = panel;
        logClickResolution = enableLogs;
        ResolveReferences();
    }

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        ResolveReferences();
    }

    private void OnDisable()
    {
        SetHoveredCharacter(null);
        ClearSelectedCharacter();

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        ResolveReferences();

        if (BattlePlanningCameraController.IsCameraMovementActive)
        {
            SetHoveredCharacter(null);
            ClearSelectedCharacter();
            return;
        }

        if (BattlePresentationInteractionLock.IsLocked)
        {
            SetHoveredCharacter(null);
            ClearSelectedCharacter();
            return;
        }

        bool pointerOverEventSystemUi =
            IsPointerOverEventSystemUi(
                Input.mousePosition);

        bool pointerOverTestPanel =
            BattleTestScenarioSwitcher
                .IsPointerBlockedByRuntimePanel(
                    Input.mousePosition);

        bool pointerBlockedByUiAction =
            IsWorldInputTemporarilyBlocked;

        bool pointerOverUi =
            pointerOverEventSystemUi ||
            pointerOverTestPanel ||
            pointerBlockedByUiAction;

        string hoverDiagnostics =
            pointerBlockedByUiAction
                ? "WorldInputTemporarilyBlocked=True"
                : pointerOverTestPanel
                    ? "PointerOverTestPanel=True"
                    : pointerOverEventSystemUi
                        ? "PointerOverUI=True"
                        : string.Empty;

        Character nextHover =
            pointerOverUi
                ? null
                : ResolveCharacterUnderPointer(
                    out hoverDiagnostics,
                    includeDiagnostics:
                        Input.GetMouseButtonDown(0));

        SetHoveredCharacter(
            nextHover);

        if (Input.GetMouseButtonDown(1))
        {
            if (pointerOverUi)
                return;

            HandleRightClick();
            return;
        }

        if (!Input.GetMouseButtonDown(0) ||
            pointerOverUi)
        {
            return;
        }

        if (battleUiManager != null &&
            battleUiManager.InputMode !=
            BattleInputMode.SelectOwner)
        {
            return;
        }

        if (logClickResolution)
        {
            Debug.Log(
                "[CharacterPointerRouter][LEFT_CLICK]\n" +
                hoverDiagnostics,
                nextHover);
        }

        if (nextHover == null)
            return;

        OpenCharacterDetails(
            nextHover);
    }

    private bool IsPointerOverEventSystemUi(
        Vector2 screenPosition)
    {
        EventSystem eventSystem =
            EventSystem.current;

        if (eventSystem == null)
            return false;

        // StandaloneInputModule/InputSystemUIInputModule의 포인터 캐시를 먼저 사용한다.
        if (eventSystem.IsPointerOverGameObject())
            return true;

        // 런타임에 생성된 Canvas나 같은 프레임에 활성화된 Graphic은
        // 위 캐시에 아직 없을 수 있으므로 실제 GraphicRaycaster 결과도 검사한다.
        PointerEventData pointerData =
            new PointerEventData(eventSystem)
            {
                position = screenPosition
            };

        uiRaycastResults.Clear();
        eventSystem.RaycastAll(
            pointerData,
            uiRaycastResults);

        return uiRaycastResults.Count > 0;
    }

    private void HandleRightClick()
    {
        if (detailPanel != null &&
            detailPanel.IsVisible)
        {
            detailPanel.Hide();
        }

        ClearSelectedCharacter();
    }

    private void OpenCharacterDetails(
        Character target)
    {
        if (target == null)
            return;

        SetSelectedCharacter(
            target);

        bool opened =
            BattleCharacterWorldClickInspector
                .OpenDetailsForCharacter(
                    target,
                    detailPanel,
                    "BattleCharacterPointerRouter");

        if (!opened)
        {
            Debug.LogError(
                "[CharacterPointerRouter] 상세 패널을 열지 못했습니다. " +
                $"Target={GetCharacterLabel(target)}, " +
                $"InstanceId={target.GetInstanceID()}, " +
                $"Path={GetHierarchyPath(target.transform)}",
                target);
            return;
        }

        if (detailPanel != null &&
            detailPanel.CurrentCharacter != target)
        {
            Debug.LogError(
                "[CharacterPointerRouter] Show 직후 상세 패널 대상이 다릅니다. " +
                $"Requested={GetCharacterLabel(target)}#{target.GetInstanceID()}, " +
                $"Actual={GetCharacterLabel(detailPanel.CurrentCharacter)}#" +
                $"{(detailPanel.CurrentCharacter != null ? detailPanel.CurrentCharacter.GetInstanceID() : 0)}",
                detailPanel);
        }
    }

    private Character ResolveCharacterUnderPointer(
        out string diagnostics,
        bool includeDiagnostics)
    {
        diagnostics =
            string.Empty;

        Camera camera =
            GetActiveCamera();

        if (camera == null)
        {
            diagnostics =
                "Camera=NULL";
            return null;
        }

        Ray ray =
            camera.ScreenPointToRay(
                Input.mousePosition);

        RaycastHit[] hits =
            Physics.RaycastAll(
                ray,
                maximumRayDistance,
                characterLayers,
                triggerInteraction);

        candidates.Clear();

        Dictionary<Character, Candidate> byCharacter =
            new();

        foreach (RaycastHit hit
                 in hits)
        {
            if (hit.collider == null)
                continue;

            Character character =
                hit.collider.GetComponentInParent<Character>();

            if (character == null ||
                character.IsDead)
            {
                continue;
            }

            if (!byCharacter.TryGetValue(
                    character,
                    out Candidate candidate))
            {
                candidate =
                    new Candidate
                    {
                        Character = character,
                        ColliderName =
                            hit.collider.name,
                        RayDistance =
                            hit.distance
                    };

                byCharacter.Add(
                    character,
                    candidate);
            }
            else if (hit.distance <
                     candidate.RayDistance)
            {
                candidate.RayDistance =
                    hit.distance;

                candidate.ColliderName =
                    hit.collider.name;
            }
        }

        foreach (Candidate candidate
                 in byCharacter.Values)
        {
            PopulateVisualScore(
                candidate,
                camera,
                Input.mousePosition);

            candidates.Add(
                candidate);
        }

        // Collider가 모델과 어긋난 경우에도 실제 렌더러 화면 영역을
        // 클릭했다면 후보로 추가한다.
        Character[] allCharacters =
            FindObjectsByType<Character>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        foreach (Character character
                 in allCharacters)
        {
            if (character == null ||
                character.IsDead ||
                byCharacter.ContainsKey(character))
            {
                continue;
            }

            Candidate visualCandidate =
                new Candidate
                {
                    Character =
                        character,
                    ColliderName =
                        "<VISUAL_FALLBACK>",
                    RayDistance =
                        float.MaxValue
                };

            PopulateVisualScore(
                visualCandidate,
                camera,
                Input.mousePosition);

            if (!visualCandidate.PointerInsideVisual)
                continue;

            candidates.Add(
                visualCandidate);
        }

        candidates.Sort(
            CompareCandidates);

        Character selected =
            candidates.Count > 0
                ? candidates[0].Character
                : null;

        if (includeDiagnostics)
        {
            StringBuilder builder =
                new();

            builder.AppendLine(
                $"Mouse={Input.mousePosition}, " +
                $"Camera={camera.name}, " +
                $"PhysicsHits={hits.Length}, " +
                $"Candidates={candidates.Count}");

            builder.AppendLine(
                $"Selected={GetCharacterLabel(selected)}#" +
                $"{(selected != null ? selected.GetInstanceID() : 0)}");

            for (int index = 0;
                 index < candidates.Count;
                 index++)
            {
                Candidate candidate =
                    candidates[index];

                builder.AppendLine(
                    $"[{index}] " +
                    $"Character={GetCharacterLabel(candidate.Character)}#" +
                    $"{candidate.Character.GetInstanceID()}, " +
                    $"Collider={candidate.ColliderName}, " +
                    $"RayDistance={candidate.RayDistance:0.###}, " +
                    $"InsideVisual={candidate.PointerInsideVisual}, " +
                    $"ScreenDistance={candidate.ScreenDistance:0.###}, " +
                    $"VisualDepth={candidate.VisualDepth:0.###}, " +
                    $"Path={GetHierarchyPath(candidate.Character.transform)}");
            }

            diagnostics =
                builder.ToString();
        }

        return selected;
    }

    private static int CompareCandidates(
        Candidate left,
        Candidate right)
    {
        if (left.PointerInsideVisual !=
            right.PointerInsideVisual)
        {
            return left.PointerInsideVisual
                ? -1
                : 1;
        }

        int screenCompare =
            left.ScreenDistance.CompareTo(
                right.ScreenDistance);

        if (screenCompare != 0)
            return screenCompare;

        int rayCompare =
            left.RayDistance.CompareTo(
                right.RayDistance);

        if (rayCompare != 0)
            return rayCompare;

        return left.VisualDepth.CompareTo(
            right.VisualDepth);
    }

    private static void PopulateVisualScore(
        Candidate candidate,
        Camera camera,
        Vector2 pointer)
    {
        if (candidate?.Character == null ||
            camera == null)
        {
            return;
        }

        Renderer[] renderers =
            candidate.Character
                .GetComponentsInChildren<Renderer>(
                    true);

        bool hasPoint =
            false;

        Vector2 minimum =
            new(
                float.PositiveInfinity,
                float.PositiveInfinity);

        Vector2 maximum =
            new(
                float.NegativeInfinity,
                float.NegativeInfinity);

        float closestDepth =
            float.PositiveInfinity;

        foreach (Renderer renderer
                 in renderers)
        {
            if (renderer == null ||
                !renderer.enabled)
            {
                continue;
            }

            Bounds bounds =
                renderer.bounds;

            Vector3 centerScreen =
                camera.WorldToScreenPoint(
                    bounds.center);

            if (centerScreen.z <= 0f)
                continue;

            closestDepth =
                Mathf.Min(
                    closestDepth,
                    centerScreen.z);

            Vector3 extent =
                bounds.extents;

            for (int x = -1;
                 x <= 1;
                 x += 2)
            {
                for (int y = -1;
                     y <= 1;
                     y += 2)
                {
                    for (int z = -1;
                         z <= 1;
                         z += 2)
                    {
                        Vector3 world =
                            bounds.center +
                            Vector3.Scale(
                                extent,
                                new Vector3(x, y, z));

                        Vector3 screen =
                            camera.WorldToScreenPoint(
                                world);

                        if (screen.z <= 0f)
                            continue;

                        hasPoint = true;

                        Vector2 screenPoint =
                            new(
                                screen.x,
                                screen.y);

                        minimum =
                            Vector2.Min(
                                minimum,
                                screenPoint);

                        maximum =
                            Vector2.Max(
                                maximum,
                                screenPoint);
                    }
                }
            }
        }

        if (!hasPoint)
        {
            candidate.PointerInsideVisual =
                false;

            candidate.ScreenDistance =
                float.MaxValue;

            candidate.VisualDepth =
                float.MaxValue;

            return;
        }

        Rect screenRect =
            Rect.MinMaxRect(
                minimum.x,
                minimum.y,
                maximum.x,
                maximum.y);

        candidate.PointerInsideVisual =
            screenRect.Contains(
                pointer);

        candidate.ScreenDistance =
            Vector2.Distance(
                pointer,
                screenRect.center);

        candidate.VisualDepth =
            closestDepth;
    }

    private Camera GetActiveCamera()
    {
        if (targetCamera != null &&
            targetCamera.isActiveAndEnabled)
        {
            return targetCamera;
        }

        targetCamera =
            Camera.main;

        if (targetCamera == null)
        {
            targetCamera =
                FindFirstObjectByType<Camera>(
                    FindObjectsInactive.Exclude);
        }

        return targetCamera;
    }

    private void ResolveReferences()
    {
        GetActiveCamera();

        if (battleUiManager == null)
        {
            battleUiManager =
                FindFirstObjectByType<BattleUIManager>(
                    FindObjectsInactive.Include);
        }

        if (detailPanel == null)
        {
            detailPanel =
                FindFirstObjectByType<BattleCharacterDetailPanelUI>(
                    FindObjectsInactive.Include);
        }
    }

    private void SetHoveredCharacter(
        Character target)
    {
        if (hoveredCharacter == target)
            return;

        GetOutline(
            hoveredCharacter)?.SetHovered(false);

        hoveredCharacter =
            target;

        GetOutline(
            hoveredCharacter)?.SetHovered(true);
    }

    private void SetSelectedCharacter(
        Character target)
    {
        if (selectedCharacter == target)
        {
            GetOutline(
                target)?.SetSelected(true);
            return;
        }

        GetOutline(
            selectedCharacter)?.SetSelected(false);

        selectedCharacter =
            target;

        GetOutline(
            selectedCharacter)?.SetSelected(true);
    }

    private void ClearSelectedCharacter()
    {
        GetOutline(
            selectedCharacter)?.SetSelected(false);

        selectedCharacter =
            null;
    }

    public static void ClearPersistentSelection()
    {
        Instance?.ClearSelectedCharacter();
    }

    public static void SelectPersistentCharacter(
        Character target)
    {
        Instance?.SetSelectedCharacter(
            target);
    }

    private static OutlineController GetOutline(
        Character target)
    {
        return target != null
            ? target.GetComponent<OutlineController>()
            : null;
    }

    private static string GetCharacterLabel(
        Character target)
    {
        if (target == null)
            return "NULL";

        return target.Data?.CharacterName ??
               target.name;
    }

    public static string GetHierarchyPath(
        Transform target)
    {
        if (target == null)
            return "NULL";

        StringBuilder builder =
            new(target.name);

        Transform current =
            target.parent;

        while (current != null)
        {
            builder.Insert(
                0,
                current.name + "/");

            current =
                current.parent;
        }

        return builder.ToString();
    }

    [Serializable]
    private sealed class Candidate
    {
        public Character Character;
        public string ColliderName;
        public float RayDistance;
        public bool PointerInsideVisual;
        public float ScreenDistance;
        public float VisualDepth;
    }
}