using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// SkillCutsceneDefinition을 실제 전투의 BattleVisualRequest에 바인딩해 재생한다.
///
/// 전투 판정은 기존 ClashManager / DamageManager가 이미 끝낸 상태이며,
/// Timeline Event Track은 계산된 Hit/VFX/Shake를 어느 프레임에 보여줄지만 결정한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillCutsceneDirector :
    MonoBehaviour
{
    [SerializeField]
    private BattleCinemachineRig
        battleCameraRig;

    [SerializeField]
    private bool logDebug;

    private bool cancelRequested;
    private GameObject runtimeHost;
    private GameObject runtimeRigObject;
    private PlayableDirector playableDirector;
    private SkillCutsceneRuntimeContext context;

    public bool IsPlaying =>
        playableDirector != null &&
        playableDirector.state ==
            PlayState.Playing;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void OnDestroy()
    {
        Cancel();
    }

    public IEnumerator PlaySequence(
        BattleVisualRequest request,
        SkillCutsceneDefinition definition,
        bool isClashAttack,
        float playbackSpeed,
        Action<
            SkillCutsceneEventClip>
            onEvent)
    {
        if (request == null ||
            definition == null)
        {
            yield break;
        }

        TimelineAsset primary =
            definition.GetTimeline(
                isClashAttack
                    ? SkillCutsceneSegment
                        .ClashAttack
                    : SkillCutsceneSegment
                        .Action);

        if (primary == null ||
            definition.CameraRigPrefab ==
            null)
        {
            yield break;
        }

        cancelRequested =
            false;

        if (!CreateRuntime(
                request,
                definition,
                onEvent))
        {
            yield break;
        }

        List<TimelineAsset> sequence =
            BuildSequence(
                request,
                definition,
                primary);

        try
        {
            foreach (TimelineAsset timeline
                     in sequence)
            {
                if (cancelRequested ||
                    timeline == null)
                {
                    break;
                }

                yield return PlayTimeline(
                    timeline,
                    playbackSpeed);
            }
        }
        finally
        {
            CleanupRuntime(
                definition);
        }
    }

    public void Cancel()
    {
        cancelRequested =
            true;

        if (playableDirector != null)
        {
            playableDirector.Stop();
        }

        CleanupRuntime(
            null);
    }

    private bool CreateRuntime(
        BattleVisualRequest request,
        SkillCutsceneDefinition definition,
        Action<
            SkillCutsceneEventClip>
            onEvent)
    {
        CleanupRuntime(
            null);

        ResolveReferences();

        runtimeHost =
            new GameObject(
                $"SkillCutscene_Runtime_" +
                $"{definition.name}");

        runtimeHost.transform.SetParent(
            transform,
            false);

        playableDirector =
            runtimeHost.AddComponent<
                PlayableDirector>();

        context =
            runtimeHost.AddComponent<
                SkillCutsceneRuntimeContext>();

        runtimeRigObject =
            Instantiate(
                definition.CameraRigPrefab,
                runtimeHost.transform);

        runtimeRigObject.name =
            definition.CameraRigPrefab.name +
            "_Runtime";

        SkillCutsceneCameraRig rig =
            runtimeRigObject.GetComponent<
                SkillCutsceneCameraRig>() ??
            runtimeRigObject
                .GetComponentInChildren<
                    SkillCutsceneCameraRig>(
                        true);

        if (rig == null)
        {
            Debug.LogError(
                "[SkillCutsceneDirector] " +
                "CameraRigPrefab에 " +
                "SkillCutsceneCameraRig이 없습니다.",
                definition);

            CleanupRuntime(
                definition);

            return false;
        }

        CinemachineBrain brain =
            battleCameraRig?.Brain;

        if (brain == null)
        {
            brain =
                Camera.main?
                    .GetComponent<
                        CinemachineBrain>();
        }

        if (brain == null)
        {
            brain =
                FindFirstObjectByType<
                    CinemachineBrain>(
                        FindObjectsInactive
                            .Include);
        }

        rig.ConfigureBrain(
            brain);

        context.Configure(
            rig,
            request.Attacker,
            request.Target,
            request.TargetPart,
            definition.FrameRate,
            definition.RestoreOverview,
            definition.RestoreTimeScale,
            onEvent,
            request);

        playableDirector.extrapolationMode =
            DirectorWrapMode.None;

        playableDirector.timeUpdateMode =
            DirectorUpdateMode.GameTime;

        return true;
    }

    private IEnumerator PlayTimeline(
        TimelineAsset timeline,
        float playbackSpeed)
    {
        if (timeline == null ||
            playableDirector == null ||
            context == null)
        {
            yield break;
        }

        playableDirector.Stop();

        playableDirector.playableAsset =
            timeline;

        SkillCutsceneTimelineBinder.Bind(
            playableDirector,
            timeline,
            context);

        playableDirector.time =
            0d;

        playableDirector.Evaluate();
        playableDirector.Play();

        float safeSpeed =
            Mathf.Max(
                0.01f,
                playbackSpeed);

        if (playableDirector.playableGraph
            .IsValid())
        {
            int rootCount =
                playableDirector
                    .playableGraph
                    .GetRootPlayableCount();

            for (int index = 0;
                 index < rootCount;
                 index++)
            {
                playableDirector
                    .playableGraph
                    .GetRootPlayable(index)
                    .SetSpeed(
                        safeSpeed);
            }
        }

        if (logDebug)
        {
            Debug.Log(
                "[SkillCutsceneDirector] " +
                $"Play Timeline={timeline.name}, " +
                $"Duration={timeline.duration:0.###}, " +
                $"Speed={safeSpeed:0.##}",
                this);
        }

        while (!cancelRequested &&
               playableDirector != null &&
               playableDirector.state ==
                   PlayState.Playing)
        {
            yield return null;
        }
    }

    private static List<TimelineAsset>
        BuildSequence(
            BattleVisualRequest request,
            SkillCutsceneDefinition definition,
            TimelineAsset primary)
    {
        List<TimelineAsset> result =
            new List<TimelineAsset>
            {
                primary
            };

        if (request.BrokePart &&
            definition.PartBreakTimeline !=
            null)
        {
            result.Add(
                definition.PartBreakTimeline);
        }

        if (request.WasKilled)
        {
            if (definition.KillTimeline !=
                null)
            {
                result.Add(
                    definition.KillTimeline);
            }

            return result;
        }

        if (definition.ReturnTimeline !=
            null)
        {
            result.Add(
                definition.ReturnTimeline);
        }

        return result;
    }

    private void ResolveReferences()
    {
        if (battleCameraRig == null)
        {
            battleCameraRig =
                FindFirstObjectByType<
                    BattleCinemachineRig>(
                        FindObjectsInactive
                            .Include);
        }
    }

    private void CleanupRuntime(
        SkillCutsceneDefinition definition)
    {
        if (context != null)
        {
            context.Restore();
        }

        if (playableDirector != null)
        {
            playableDirector.Stop();
        }

        if (runtimeHost != null)
        {
            if (Application.isPlaying)
            {
                Destroy(
                    runtimeHost);
            }
            else
            {
                DestroyImmediate(
                    runtimeHost);
            }
        }
        else if (runtimeRigObject != null)
        {
            if (Application.isPlaying)
            {
                Destroy(
                    runtimeRigObject);
            }
            else
            {
                DestroyImmediate(
                    runtimeRigObject);
            }
        }

        runtimeHost =
            null;

        runtimeRigObject =
            null;

        playableDirector =
            null;

        context =
            null;
    }
}