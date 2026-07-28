using UnityEngine;
using UnityEngine.Timeline;

public enum SkillCutsceneSegment
{
    Action = 0,
    ClashAttack = 1,
    PartBreak = 2,
    Kill = 3,
    Return = 4
}

/// <summary>
/// 한 스킬의 프레임 기반 Timeline 연출 묶음이다.
///
/// Timeline은 애니메이션·카메라 클립·오디오 편집을 담당하고,
/// Project Abyss 전용 Track은 런타임 Attacker/Target 바인딩,
/// Hit/VFX/Shake/TimeScale 이벤트를 담당한다.
/// </summary>
[CreateAssetMenu(
    fileName = "NewSkillCutsceneDefinition",
    menuName = "Battle/Cutscene/Skill Cutscene Definition")]
public sealed class SkillCutsceneDefinition :
    ScriptableObject
{
    [Header("Main Timeline")]
    public TimelineAsset ActionTimeline;

    [Tooltip(
        "합 승자가 실제 공격 애니메이션을 재생할 때 사용합니다. " +
        "비어 있으면 Action Timeline을 재사용합니다.")]
    public TimelineAsset ClashAttackTimeline;

    [Header("Optional Result Segments")]
    public TimelineAsset PartBreakTimeline;
    public TimelineAsset KillTimeline;
    public TimelineAsset ReturnTimeline;

    [Header("Camera Rig")]
    [Tooltip(
        "CM_Overview, CM_Follow, CM_Impact처럼 스킬 전용 " +
        "CinemachineCamera들을 담은 Prefab입니다.")]
    public GameObject CameraRigPrefab;

    [Header("Authoring Animation")]
    [Tooltip(
        "Studio가 기본 Attacker Animation Track을 만들 때 사용합니다.")]
    public AnimationClip AttackerAnimation;

    [Tooltip(
        "선택 사항입니다. 타깃 반응 Animation Track의 기본 Clip입니다.")]
    public AnimationClip TargetAnimation;

    [Header("Frame Authoring")]
    [Min(1f)]
    public double AuthoringFrameRate = 30d;

    [Min(1)]
    public int DefaultDurationFrames = 120;

    [Header("Legacy Preparation")]
    [Tooltip(
        "Timeline 시작 전에 기존 FacingController로 공격자와 타깃을 마주보게 합니다.")]
    public bool PrepareFacing = true;

    [Tooltip(
        "Timeline 시작 전에 기존 CharacterActionMover의 스킬 시작 위치 이동을 사용합니다. " +
        "Timeline Animation이 이동까지 전부 담당한다면 끄세요.")]
    public bool PrepareLegacyMovement;

    [Tooltip(
        "Timeline 종료 후 기존 CharacterActionMover를 기본 위치로 돌립니다.")]
    public bool RestoreLegacyMovement = true;

    [Tooltip(
        "Timeline 종료 후 기존 Facing을 복구합니다.")]
    public bool RestoreFacing = true;

    [Header("Automatic Presentation")]
    [Tooltip(
        "기존 OnActionStart / BeforeAttack / AfterAction / OnKill VFX를 " +
        "Timeline 외부에서도 자동 재생합니다.")]
    public bool UseLegacyAutomaticVfx = true;

    [Tooltip(
        "Hit Event Clip이 하나도 실행되지 않았을 때 남은 피해를 종료 시점에 표시합니다.")]
    public bool ApplyMissingHitFallback = true;

    [Tooltip(
        "Timeline 종료 후 기본 전투 Overview 카메라로 복귀합니다.")]
    public bool RestoreOverview = true;

    [Tooltip(
        "Timeline에서 TimeScale을 바꿨다면 종료 시 원래 값으로 복구합니다.")]
    public bool RestoreTimeScale = true;

    [Header("Editor Preview")]
    [SerializeField]
    private string previewScenePath;

    public string PreviewScenePath =>
        previewScenePath;

    public double FrameRate =>
        System.Math.Max(
            1d,
            AuthoringFrameRate);

    public double DefaultDurationSeconds =>
        System.Math.Max(
            1,
            DefaultDurationFrames) /
        FrameRate;

    public TimelineAsset GetTimeline(
        SkillCutsceneSegment segment)
    {
        return segment switch
        {
            SkillCutsceneSegment.ClashAttack =>
                ClashAttackTimeline != null
                    ? ClashAttackTimeline
                    : ActionTimeline,

            SkillCutsceneSegment.PartBreak =>
                PartBreakTimeline,

            SkillCutsceneSegment.Kill =>
                KillTimeline,

            SkillCutsceneSegment.Return =>
                ReturnTimeline,

            _ =>
                ActionTimeline
        };
    }

    public bool HasTimeline(
        SkillCutsceneSegment segment)
    {
        return GetTimeline(segment) != null;
    }

#if UNITY_EDITOR
    public void SetPreviewScenePath(
        string value)
    {
        previewScenePath =
            value ?? string.Empty;

        UnityEditor.EditorUtility.SetDirty(
            this);
    }

    private void OnValidate()
    {
        AuthoringFrameRate =
            System.Math.Max(
                1d,
                AuthoringFrameRate);

        DefaultDurationFrames =
            Mathf.Max(
                1,
                DefaultDurationFrames);
    }
#endif
}
