#if UNITY_EDITOR
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class
    ProjectAbyssSkillCutscenePreviewBinder :
        MonoBehaviour
{
    [SerializeField]
    private SkillDefinition skill;

    [SerializeField]
    private SkillVisualDefinition definition;

    [SerializeField]
    private SkillCutsceneSegment segment =
        SkillCutsceneSegment.Action;

    [SerializeField]
    private Character attacker;

    [SerializeField]
    private Character target;

    [SerializeField]
    private SkillCutsceneCameraRig cameraRig;

    [SerializeField]
    private PlayableDirector director;

    [SerializeField]
    private SkillCutsceneRuntimeContext context;

    public SkillDefinition Skill =>
        skill;

    public SkillVisualDefinition Definition =>
        definition;

    public SkillCutsceneSegment Segment
    {
        get => segment;
        set
        {
            segment =
                value;

            BindNow();
        }
    }

    public PlayableDirector Director =>
        director;

    public SkillCutsceneRuntimeContext Context =>
        context;

    public void Configure(
        SkillDefinition newSkill,
        SkillVisualDefinition newDefinition,
        Character newAttacker,
        Character newTarget,
        SkillCutsceneCameraRig newCameraRig,
        PlayableDirector newDirector,
        SkillCutsceneRuntimeContext newContext)
    {
        skill =
            newSkill;

        definition =
            newDefinition;

        attacker =
            newAttacker;

        target =
            newTarget;

        cameraRig =
            newCameraRig;

        director =
            newDirector;

        context =
            newContext;

        EditorUtility.SetDirty(
            this);
    }

    private void OnEnable()
    {
        EditorApplication.delayCall +=
            BindWhenReady;
    }

    private void OnValidate()
    {
        EditorApplication.delayCall +=
            BindWhenReady;
    }

    private void OnDisable()
    {
        context?.Restore();
    }

    [ContextMenu("Bind Preview Timeline")]
    public void BindNow()
    {
        ResolveReferences();

        TimelineAsset timeline =
            definition?
                .GetTimeline(segment);

        if (timeline == null ||
            director == null ||
            context == null ||
            cameraRig == null)
        {
            return;
        }

        CinemachineBrain brain =
            Camera.main?
                .GetComponent<
                    CinemachineBrain>();

        if (brain == null)
        {
            brain =
                Object
                    .FindFirstObjectByType<
                        CinemachineBrain>(
                            FindObjectsInactive
                                .Include);
        }

        cameraRig.ConfigureBrain(
            brain);

        context.Configure(
            cameraRig,
            attacker,
            target,
            null,
            definition.FrameRate,
            definition.RestoreOverview,
            definition.RestoreTimeScale,
            HandlePreviewTimelineEvent,
            allowEditModeEvents: true);

        director.playableAsset =
            timeline;

        SkillCutsceneTimelineBinder.Bind(
            director,
            timeline,
            context);

        director.time =
            Mathf.Clamp(
                (float)director.time,
                0f,
                (float)timeline.duration);

        director.Evaluate();

        EditorUtility.SetDirty(
            director);
    }

    private void HandlePreviewTimelineEvent(
        SkillCutsceneEventClip clip)
    {
        if (clip == null ||
            (clip.EventType !=
                 SkillCutsceneEventType.Hit &&
             clip.EventType !=
                 SkillCutsceneEventType.TargetHitReaction))
        {
            return;
        }

        CharacterView targetView =
            target != null
                ? target.GetComponentInChildren<
                    CharacterView>(true)
                : null;

        if (targetView == null)
            return;

        targetView.PlayHitRestart();

        // Edit Mode에서는 Animator가 자동으로 다음 프레임을 평가하지 않으므로
        // 한 Authoring Frame만큼 직접 진행해 Hit Pose가 Scene/Game View에 보이게 한다.
        Animator targetAnimator =
            targetView.Animator;

        if (!Application.isPlaying &&
            targetAnimator != null &&
            targetAnimator.isActiveAndEnabled)
        {
            targetAnimator.Update(
                1f /
                Mathf.Max(
                    1f,
                    (float)definition.FrameRate));
        }

        SceneView.RepaintAll();
    }

    private void BindWhenReady()
    {
        if (this == null)
            return;

        BindNow();
    }

    private void ResolveReferences()
    {
        director ??=
            GetComponent<
                PlayableDirector>();

        context ??=
            GetComponent<
                SkillCutsceneRuntimeContext>();

        cameraRig ??=
            Object
                .FindFirstObjectByType<
                    SkillCutsceneCameraRig>(
                        FindObjectsInactive
                            .Include);

        if (attacker == null)
        {
            GameObject found =
                GameObject.Find(
                    "Preview_Attacker");

            attacker =
                found?
                    .GetComponentInChildren<
                        Character>(
                            true);
        }

        if (target == null)
        {
            GameObject found =
                GameObject.Find(
                    "Preview_Target");

            target =
                found?
                    .GetComponentInChildren<
                        Character>(
                            true);
        }
    }
}
#endif
