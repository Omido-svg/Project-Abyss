using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Visual/Skill Visual Definition",
    fileName = "NewSkillVisualDefinition")]
public class SkillVisualDefinition : ScriptableObject
{
    [Header("Identity / Fallback")]
    [Tooltip("캐릭터 전용 연출이면 false. 기본 프로필 폴백으로 사용되지 않습니다.")]
    public bool AllowAsProfileFallback = false;

    [Header("Required Timeline Cutscene")]
    [Tooltip(
        "모든 전투 스킬은 SkillCutsceneDefinition을 필수로 사용합니다. " +
        "Animator Trigger / Animation Event 기반 공격 재생은 더 이상 런타임 경로가 아닙니다.")]
    public SkillCutsceneDefinition CutsceneDefinition;

    public bool HasTimelineCutscene =>
        CutsceneDefinition != null &&
        CutsceneDefinition.HasCompleteTimelineSet;

    [Header("Timeline Event Camera Impact")]
    [Tooltip(
        "CameraImpactPulse Event Clip이 사용할 줌/흔들림 설정입니다. " +
        "SkillCameraDefinition.Shots는 더 이상 런타임에서 사용하지 않습니다.")]
    public SkillCameraDefinition CameraDefinition;

    [Header("Legacy Migration Source (Hidden)")]
    [HideInInspector] public bool UsesTargetCamera = true;
    [HideInInspector] public bool ReturnCameraAfterAction = true;
    [HideInInspector, Min(0f)] public float CameraArriveTimeout = 1.5f;
    [HideInInspector, Min(0f)] public float ClashRollCameraLeadTime = 0.25f;
    [HideInInspector] public bool MovesToTarget = true;
    [HideInInspector] public bool ReturnPositionAfterAction = true;
    [HideInInspector] public bool SkipMovementForSelfTarget = true;
    [HideInInspector] public bool FaceEachOther = true;
    [HideInInspector] public bool ReturnFacingAfterAction = true;
    [HideInInspector] public bool SkipFacingForSelfTarget = true;

    [Header("Clash")]
    public bool ShowsClashPower = false;
    public Color ClashColor = Color.yellow;
    public Color TieColor = new Color(1f, 0.45f, 0.1f);

    [Header("Hit / Damage")]
    public bool HasHitFrameDamage = true;

    [Tooltip(
        "Timeline Hit Event가 누락된 경우 자동으로 끝에서 피해를 표시하지 않습니다. " +
        "누락은 제작 오류로 취급합니다.")]
    [HideInInspector]
    public bool ApplyDamageIfNoHitFrame = false;

    [Header("Hit Damage Split")]
    public List<int> HitDamageWeights = new() { 1 };
    public bool DistributeDamageByHitCount = true;
    [Min(1)] public int ExpectedHitFrameCount = 1;

    [Header("Action Announcement")]
    public bool ShowsActionAnnouncement = true;
    [Min(0f)] public float ActionAnnouncementDuration = 0.75f;

    [Header("Legacy Timing Source (Hidden)")]
    [HideInInspector, Min(0f)] public float BeforeActionDelay = 0.15f;
    [HideInInspector, Min(0f)] public float AfterActionDelay = 0.35f;
    [HideInInspector, Min(0f)] public float AfterReturnDelay = 0.35f;

    [Header("Camera Shake")]
    public bool UseHitCameraShake = false;
    public BattleCameraShakeSettings HitShake = new BattleCameraShakeSettings();

    [Header("VFX")]
    public List<BattleVfxCue> VfxCues = new();

    [Header("Action Move")]
    public CharacterActionMoveSettings MoveSettings =
        new CharacterActionMoveSettings();

    [Header("Animator State Validation")]
    [Tooltip(
        "Idle / Hit / Dead 같은 상태 표현용 Animator State만 기록하세요. " +
        "공격 State는 Timeline Animation Track이 직접 Clip을 재생하므로 필요하지 않습니다.")]
    public List<string> RequiredAnimatorStates = new();

    private void OnValidate()
    {
        CameraArriveTimeout = Mathf.Max(0f, CameraArriveTimeout);
        ClashRollCameraLeadTime = Mathf.Max(0f, ClashRollCameraLeadTime);
        ActionAnnouncementDuration = Mathf.Max(0f, ActionAnnouncementDuration);
        BeforeActionDelay = Mathf.Max(0f, BeforeActionDelay);
        AfterActionDelay = Mathf.Max(0f, AfterActionDelay);
        AfterReturnDelay = Mathf.Max(0f, AfterReturnDelay);
        ExpectedHitFrameCount = Mathf.Max(1, ExpectedHitFrameCount);
        ApplyDamageIfNoHitFrame = false;

        if (HitDamageWeights == null)
            HitDamageWeights = new List<int> { 1 };

        if (VfxCues == null)
            VfxCues = new List<BattleVfxCue>();
    }
}
