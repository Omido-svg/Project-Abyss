using System.Text;
using UnityEngine;

/// <summary>
/// Scene/Prefab에 미리 배치한 고정 전투 HUD의 중앙 참조점.
/// 고정 HUD는 런타임 생성하지 않으며, 최초 1회 Registry를 찾은 뒤 static cache를 재사용한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleSceneHudRegistry : MonoBehaviour
{
    [SerializeField] private GameplayV5ProgressionHudUI progressionHud;
    [SerializeField] private EmotionAugmentChoiceUI emotionAugmentChoiceUi;
    [SerializeField] private BattleActionOrderRailUI actionOrderRail;
    [SerializeField] private BattleWorldSlotArrowOverlayUI worldSlotArrowOverlay;
    [SerializeField] private BattlePlaybackSpeedUI playbackSpeedUi;
    [SerializeField] private MomentumScrollbarUI momentumScrollbarUi;

    private static BattleSceneHudRegistry instance;
    private bool missingReferenceLogged;

    public static BattleSceneHudRegistry Instance => instance;

    public GameplayV5ProgressionHudUI ProgressionHud => progressionHud;
    public EmotionAugmentChoiceUI EmotionAugmentChoiceUi => emotionAugmentChoiceUi;
    public BattleActionOrderRailUI ActionOrderRail => actionOrderRail;
    public BattleWorldSlotArrowOverlayUI WorldSlotArrowOverlay => worldSlotArrowOverlay;
    public BattlePlaybackSpeedUI PlaybackSpeedUi => playbackSpeedUi;
    public MomentumScrollbarUI MomentumScrollbarUi => momentumScrollbarUi;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        RegisterInstance();
        ValidateRuntimeReferences();
    }

    private void OnEnable()
    {
        RegisterInstance();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void RegisterInstance()
    {
        if (instance == null || instance == this)
        {
            instance = this;
            return;
        }

        Debug.LogError(
            $"[BattleSceneHudRegistry] Registry가 둘 이상 존재합니다. " +
            $"Current={instance.name}, Duplicate={name}",
            this);
    }

    /// <summary>
    /// 호환용 탐색 진입점. 최초 1회만 Scene 탐색하며 이후에는 Instance cache를 반환한다.
    /// </summary>
    public static BattleSceneHudRegistry Find()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<BattleSceneHudRegistry>(
            FindObjectsInactive.Include);

        return instance;
    }

    public bool HasAllRequiredReferences =>
        progressionHud != null &&
        emotionAugmentChoiceUi != null &&
        actionOrderRail != null &&
        worldSlotArrowOverlay != null &&
        playbackSpeedUi != null &&
        momentumScrollbarUi != null;

    public string GetMissingReferenceSummary()
    {
        StringBuilder builder = new();

        AppendMissing(builder, progressionHud, nameof(progressionHud));
        AppendMissing(builder, emotionAugmentChoiceUi, nameof(emotionAugmentChoiceUi));
        AppendMissing(builder, actionOrderRail, nameof(actionOrderRail));
        AppendMissing(builder, worldSlotArrowOverlay, nameof(worldSlotArrowOverlay));
        AppendMissing(builder, playbackSpeedUi, nameof(playbackSpeedUi));
        AppendMissing(builder, momentumScrollbarUi, nameof(momentumScrollbarUi));

        return builder.ToString();
    }

    private void ValidateRuntimeReferences()
    {
        if (HasAllRequiredReferences)
        {
            missingReferenceLogged = false;
            return;
        }

        if (missingReferenceLogged)
            return;

        missingReferenceLogged = true;
        Debug.LogError(
            "[BattleSceneHudRegistry] Scene-authored 고정 HUD 참조가 누락되었습니다: " +
            GetMissingReferenceSummary() +
            ". 런타임 fallback 생성/전역 탐색으로 숨기지 않습니다. " +
            "Tools > Project Abyss > UI > Convert Current Battle Scene To Scene-Authored HUD 또는 Validate 메뉴로 복구하세요.",
            this);
    }

    private static void AppendMissing(
        StringBuilder builder,
        Object value,
        string fieldName)
    {
        if (value != null)
            return;

        if (builder.Length > 0)
            builder.Append(", ");

        builder.Append(fieldName);
    }

#if UNITY_EDITOR
    public void EditorAssign(
        GameplayV5ProgressionHudUI progression,
        EmotionAugmentChoiceUI choice,
        BattleActionOrderRailUI orderRail,
        BattleWorldSlotArrowOverlayUI slotArrowOverlay,
        BattlePlaybackSpeedUI playbackSpeed,
        MomentumScrollbarUI momentum)
    {
        progressionHud = progression;
        emotionAugmentChoiceUi = choice;
        actionOrderRail = orderRail;
        worldSlotArrowOverlay = slotArrowOverlay;
        playbackSpeedUi = playbackSpeed;
        momentumScrollbarUi = momentum;
    }
#endif
}
