using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Timeline;


/// <summary>
/// 스킬의 Presentation 단일 원본.
///
/// v8.0부터 스킬은 공격자 중심 Action / ClashAttack Timeline만 소유한다.
/// 합 접근·대치·승패·재정렬은 CharacterPresentationProfile과 공통 합 진행자가,
/// 피격·부위 파괴·사망 반응은 타깃 자신의 Presentation Profile이 소유한다.
///
/// SkillDefinition은 게임플레이 원본, SkillVisualDefinition은 연출 원본으로
/// 책임을 두 단계로 고정한다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Visual/Skill Presentation Definition",
    fileName = "NewSkillPresentationDefinition")]
public class SkillVisualDefinition : ScriptableObject
{
    [SerializeField, HideInInspector]
    private int presentationSchemaVersion;

    [Header("Identity / Fallback")]
    [Tooltip("캐릭터 전용 연출이면 false. 기본 프로필 폴백으로 사용되지 않습니다.")]
    public bool AllowAsProfileFallback = false;

    [Header("Active Attacker Timeline Set")]
    [Tooltip("합이 아닌 일반 행동에서 재생하는 공격자 중심 Timeline입니다.")]
    public TimelineAsset ActionTimeline;

    [Tooltip("합 교환에서 승리한 뒤 재생하는 공격자 중심 Timeline입니다.")]
    public TimelineAsset ClashAttackTimeline;

    [Header("Legacy Timeline References (Inactive)")]
    [SerializeField, HideInInspector]
    private TimelineAsset PartBreakTimeline;

    [SerializeField, HideInInspector]
    private TimelineAsset KillTimeline;

    [SerializeField, HideInInspector]
    private TimelineAsset ReturnTimeline;

    [Header("Required Camera Rig")]
    public GameObject CameraRigPrefab;

    [Header("Authoring Animation")]
    public AnimationClip AttackerAnimation;

    [SerializeField, HideInInspector]
    private AnimationClip TargetAnimation;

    [Header("Target Reaction Request")]
    [Tooltip(
        "Hit Event가 타깃에게 요청할 의미 기반 피격 반응입니다. " +
        "실제 AnimationClip은 타깃 자신의 CharacterPresentationProfile이 선택합니다.")]
    public HitReactionKey TargetReaction =
        HitReactionKey.HeavyHit;

    [Header("Frame Authoring")]
    [Min(1f)] public double AuthoringFrameRate = 30d;
    [Min(1)] public int DefaultDurationFrames = 120;

    [Header("Pre/Post Timeline Staging")]
    [Tooltip("Timeline 시작 전에 공격자와 타깃을 마주보게 합니다.")]
    public bool PrepareFacing = true;

    [FormerlySerializedAs("PrepareLegacyMovement")]
    [Tooltip("Timeline 시작 전에 CharacterActionMover로 시작 위치를 준비합니다.")]
    public bool PrepareMovement;

    [FormerlySerializedAs("RestoreLegacyMovement")]
    [Tooltip("Timeline 전체 시퀀스가 끝난 뒤 기본 위치를 복구합니다.")]
    public bool RestoreMovement = true;

    [Tooltip("Timeline 전체 시퀀스가 끝난 뒤 Facing을 복구합니다.")]
    public bool RestoreFacing = true;

    [Header("Visual FX Authoring")]
    [Tooltip(
        "true이면 Hit/Vfx Event가 Legacy VFX Cue를 자동 재생하지 않습니다. " +
        "VFX와 Shader 효과는 Visual FX Timeline Track에서 명시적으로 배치합니다.")]
    public bool UseExplicitVisualFxTracks = true;

    [Header("Cleanup")]
    public bool RestoreOverview = true;
    public bool RestoreTimeScale = true;

    [Header("Editor Preview")]
    [SerializeField] private string previewScenePath;

    [Header("Clash")]
    public bool ShowsClashPower = false;
    public Color ClashColor = Color.yellow;
    public Color TieColor = new Color(1f, 0.45f, 0.1f);

    [Header("Hit / Damage")]
    public bool HasHitFrameDamage = true;

    [Header("Hit Damage Split")]
    public List<int> HitDamageWeights = new() { 1 };
    public bool DistributeDamageByHitCount = true;
    [Min(1)] public int ExpectedHitFrameCount = 1;

    [Header("Action Announcement")]
    public bool ShowsActionAnnouncement = true;
    [Min(0f)] public float ActionAnnouncementDuration = 0.75f;

    [Header("Camera Impact Pulses")]
    [Tooltip(
        "현재 활성 Skill Camera 위에 겹쳐 재생되는 FOV/Impulse 프리셋입니다. " +
        "위에서부터 검사하며 조건을 처음 만족한 Pulse 하나를 사용합니다.")]
    public List<SkillCameraImpactPulse> ImpactPulses = new();

    [Header("Camera Shake")]
    public bool UseHitCameraShake = false;
    public BattleCameraShakeSettings HitShake = new BattleCameraShakeSettings();

    [Header("Legacy VFX Compatibility")]
    [Tooltip(
        "UseExplicitVisualFxTracks=false인 이전 에셋의 호환용입니다. " +
        "신규 스킬은 Timeline VFX Track을 사용하세요.")]
    public List<BattleVfxCue> VfxCues = new();

    [Header("Action Move")]
    public CharacterActionMoveSettings MoveSettings =
        new CharacterActionMoveSettings();

    [Header("Animator State Validation")]
    [Tooltip(
        "Idle / Hit / Dead 같은 상태 표현용 Animator State만 기록하세요. " +
        "공격 State는 Timeline Animation Track이 직접 Clip을 재생하므로 필요하지 않습니다.")]
    public List<string> RequiredAnimatorStates = new();

    public int PresentationSchemaVersion => presentationSchemaVersion;

    public string PreviewScenePath => previewScenePath;

    public double FrameRate => Math.Max(1d, AuthoringFrameRate);

    public double DefaultDurationSeconds =>
        Math.Max(1, DefaultDurationFrames) / FrameRate;

    public bool HasTimelineCutscene => HasCompleteTimelineSet;

    public bool HasCompleteTimelineSet =>
        ActionTimeline != null &&
        ClashAttackTimeline != null &&
        CameraRigPrefab != null;

    public TimelineAsset GetTimeline(SkillCutsceneSegment segment)
    {
        return segment switch
        {
            SkillCutsceneSegment.ClashAttack => ClashAttackTimeline,
            SkillCutsceneSegment.PartBreak => PartBreakTimeline,
            SkillCutsceneSegment.Kill => KillTimeline,
            SkillCutsceneSegment.Return => ReturnTimeline,
            _ => ActionTimeline
        };
    }

    public bool HasTimeline(SkillCutsceneSegment segment) =>
        GetTimeline(segment) != null;

    public List<string> GetMissingRequirements()
    {
        List<string> result = new();
        if (ActionTimeline == null) result.Add("Action Timeline");
        if (ClashAttackTimeline == null) result.Add("ClashAttack Timeline");
        if (CameraRigPrefab == null) result.Add("Camera Rig Prefab");
        return result;
    }

    /// <summary>
    /// 기존 데이터 손실 방지와 마이그레이션 검사만을 위한 참조입니다.
    /// 런타임 재생 시퀀스에는 포함되지 않습니다.
    /// </summary>
    public IEnumerable<TimelineAsset> EnumerateLegacyTimelines()
    {
        if (PartBreakTimeline != null) yield return PartBreakTimeline;
        if (KillTimeline != null) yield return KillTimeline;
        if (ReturnTimeline != null) yield return ReturnTimeline;
    }

    public SkillCameraImpactPulse FindImpactPulse(
        SkillCameraImpactTiming timing,
        int hitIndex,
        int exchangeIndex,
        int damage,
        bool isCritical,
        bool brokePart,
        bool wasKilled,
        bool isClash,
        bool isOneSided)
    {
        if (ImpactPulses == null)
            return null;

        foreach (SkillCameraImpactPulse pulse in ImpactPulses)
        {
            if (pulse == null)
                continue;

            if (pulse.Matches(
                    timing,
                    hitIndex,
                    exchangeIndex,
                    damage,
                    isCritical,
                    brokePart,
                    wasKilled,
                    isClash,
                    isOneSided))
            {
                return pulse;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    public void SetPreviewScenePath(string value)
    {
        if (this == null)
            return;

        string normalized = value ?? string.Empty;
        if (string.Equals(previewScenePath, normalized, StringComparison.Ordinal))
            return;

        previewScenePath = normalized;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    public void MarkPresentationSchemaVersion(int version)
    {
        presentationSchemaVersion = Mathf.Max(0, version);
        UnityEditor.EditorUtility.SetDirty(this);
    }

#endif

    private void OnValidate()
    {
        AuthoringFrameRate = Math.Max(1d, AuthoringFrameRate);
        DefaultDurationFrames = Mathf.Max(1, DefaultDurationFrames);

        ActionAnnouncementDuration = Mathf.Max(0f, ActionAnnouncementDuration);
        ExpectedHitFrameCount = Mathf.Max(1, ExpectedHitFrameCount);

        HitDamageWeights ??= new List<int> { 1 };
        ImpactPulses ??= new List<SkillCameraImpactPulse>();
        VfxCues ??= new List<BattleVfxCue>();
        RequiredAnimatorStates ??= new List<string>();
        HitShake ??= new BattleCameraShakeSettings();

        if (!HasHitFrameDamage)
            TargetReaction = HitReactionKey.None;
        MoveSettings ??= new CharacterActionMoveSettings();

        HitShake.Sanitize();
        foreach (SkillCameraImpactPulse pulse in ImpactPulses)
            pulse?.Sanitize();
    }
}