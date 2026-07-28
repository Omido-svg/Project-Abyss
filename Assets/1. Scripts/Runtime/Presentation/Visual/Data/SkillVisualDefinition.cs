using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Visual/Skill Visual Definition",
    fileName = "NewSkillVisualDefinition")]
public class SkillVisualDefinition : ScriptableObject
{
    [Header("Identity / Fallback")]
    [Tooltip("캐릭터 전용 연출이면 false. 기본 프로필 폴백으로 사용되지 않습니다.")]
    public bool AllowAsProfileFallback = true;

    [Header("Timeline Cutscene")]
    [Tooltip(
        "켜져 있고 Cutscene Definition에 해당 Timeline이 있으면 " +
        "기존 ActionType Trigger / Camera Shot 방식 대신 Timeline 연출을 사용합니다.")]
    public bool UseTimelineCutscene;

    public SkillCutsceneDefinition CutsceneDefinition;

    public bool HasTimelineCutscene =>
        UseTimelineCutscene &&
        CutsceneDefinition != null &&
        CutsceneDefinition.ActionTimeline != null;

    [Header("Camera")]
    public bool UsesTargetCamera = true;
    public bool ReturnCameraAfterAction = true;
    [Min(0f)] public float CameraArriveTimeout = 1.5f;
    public SkillCameraDefinition CameraDefinition;

    [Header("Clash Roll Camera")]
    [Min(0f)] public float ClashRollCameraLeadTime = 0.25f;

    [Header("Movement")]
    public bool MovesToTarget = true;
    public bool ReturnPositionAfterAction = true;
    public bool SkipMovementForSelfTarget = true;

    [Header("Facing")]
    public bool FaceEachOther = true;
    public bool ReturnFacingAfterAction = true;
    public bool SkipFacingForSelfTarget = true;

    [Header("Clash")]
    public bool ShowsClashPower = false;
    public Color ClashColor = Color.yellow;
    public Color TieColor = new Color(1f, 0.45f, 0.1f);

    [Header("Hit / Damage")]
    public bool HasHitFrameDamage = true;
    public bool ApplyDamageIfNoHitFrame = false;

    [Header("Hit Damage Split")]
    public List<int> HitDamageWeights = new() { 1 };
    public bool DistributeDamageByHitCount = true;
    [Min(1)] public int ExpectedHitFrameCount = 1;

    [Header("Action Announcement")]
    public bool ShowsActionAnnouncement = true;
    [Min(0f)] public float ActionAnnouncementDuration = 0.75f;

    [Header("Timing")]
    [Min(0f)] public float BeforeActionDelay = 0.15f;
    [Min(0f)] public float AfterActionDelay = 0.35f;
    [Min(0f)] public float AfterReturnDelay = 0.35f;

    [Header("Camera Shake")]
    public bool UseHitCameraShake = false;
    public BattleCameraShakeSettings HitShake = new BattleCameraShakeSettings();

    [Header("VFX")]
    public List<BattleVfxCue> VfxCues = new();

    [Header("Action Move")]
    public CharacterActionMoveSettings MoveSettings =
        new CharacterActionMoveSettings();

    [Header("Animator Validation")]
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

        if (HitDamageWeights == null)
            HitDamageWeights = new List<int> { 1 };

        if (VfxCues == null)
            VfxCues = new List<BattleVfxCue>();
    }
}
