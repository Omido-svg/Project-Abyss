using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Timeline;


/// <summary>
/// 스킬의 Presentation 단일 원본.
///
/// v7.0부터 Camera/Cutscene 중간 ScriptableObject를 거치지 않고
/// 이 에셋 하나가 5-Segment Timeline, Camera Rig, Hit/Announcement,
/// Camera Impact, VFX 호환 데이터와 이동 규칙을 모두 소유한다.
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

    [Header("Required Timeline Set")]
    public TimelineAsset ActionTimeline;
    public TimelineAsset ClashAttackTimeline;
    public TimelineAsset PartBreakTimeline;
    public TimelineAsset KillTimeline;
    public TimelineAsset ReturnTimeline;

    [Header("Required Camera Rig")]
    public GameObject CameraRigPrefab;

    [Header("Authoring Animation")]
    public AnimationClip AttackerAnimation;
    public AnimationClip TargetAnimation;

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
        PartBreakTimeline != null &&
        KillTimeline != null &&
        ReturnTimeline != null &&
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
        if (PartBreakTimeline == null) result.Add("PartBreak Timeline");
        if (KillTimeline == null) result.Add("Kill Timeline");
        if (ReturnTimeline == null) result.Add("Return Timeline");
        if (CameraRigPrefab == null) result.Add("Camera Rig Prefab");
        return result;
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
        MoveSettings ??= new CharacterActionMoveSettings();

        HitShake.Sanitize();
        foreach (SkillCameraImpactPulse pulse in ImpactPulses)
            pulse?.Sanitize();
    }
}
