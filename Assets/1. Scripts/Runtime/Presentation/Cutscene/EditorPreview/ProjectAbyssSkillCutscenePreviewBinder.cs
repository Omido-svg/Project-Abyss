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
    private SkillCutsceneDefinition definition;

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

    public SkillCutsceneDefinition Definition =>
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
        SkillCutsceneDefinition newDefinition,
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
            null);

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
