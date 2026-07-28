using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
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
/// 한 스킬의 단일 프레임 Authoring 원본이다.
/// 공격 Animation, Camera, Hit/VFX/Shake, 결과 분기와 복귀 타이밍은 모두 Timeline에서 정의한다.
/// 전투 판정은 Timeline 시작 전에 완료되어 있으며 Timeline은 확정 결과를 표시한다.
/// </summary>
[CreateAssetMenu(
    fileName = "NewSkillCutsceneDefinition",
    menuName = "Battle/Cutscene/Skill Cutscene Definition")]
public sealed class SkillCutsceneDefinition : ScriptableObject
{
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
        "true이면 Hit/Vfx Event가 SkillVisualDefinition의 Legacy VFX Cue를 자동 재생하지 않습니다. " +
        "VFX와 Shader 효과는 Visual FX Timeline Track에서 명시적으로 배치합니다.")]
    public bool UseExplicitVisualFxTracks = false;

    [Header("Cleanup")]
    public bool RestoreOverview = true;
    public bool RestoreTimeScale = true;

    [Header("Editor Preview")]
    [SerializeField] private string previewScenePath;

    public string PreviewScenePath => previewScenePath;

    public double FrameRate => System.Math.Max(1d, AuthoringFrameRate);

    public double DefaultDurationSeconds =>
        System.Math.Max(1, DefaultDurationFrames) / FrameRate;

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

#if UNITY_EDITOR
    public void SetPreviewScenePath(string value)
    {
        // Unity가 AssetDatabase/Scene 저장 중 managed wrapper를 교체하면
        // 이미 파괴된 ScriptableObject 인스턴스로 이 메서드가 호출될 수 있다.
        if (this == null)
            return;

        string normalized = value ?? string.Empty;
        if (string.Equals(
                previewScenePath,
                normalized,
                System.StringComparison.Ordinal))
        {
            return;
        }

        previewScenePath = normalized;

        if (this != null)
            UnityEditor.EditorUtility.SetDirty(this);
    }

    private void OnValidate()
    {
        AuthoringFrameRate = System.Math.Max(1d, AuthoringFrameRate);
        DefaultDurationFrames = Mathf.Max(1, DefaultDurationFrames);
    }
#endif
}
