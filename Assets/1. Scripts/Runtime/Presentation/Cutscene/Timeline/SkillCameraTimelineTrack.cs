using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed class SkillCameraTimelineBehaviour :
    PlayableBehaviour
{
    public SkillCameraTimelineClip Asset;
}

[TrackColor(
    0.18f,
    0.55f,
    0.95f)]
[TrackBindingType(
    typeof(
        SkillCutsceneRuntimeContext))]
[TrackClipType(
    typeof(
        SkillCameraTimelineClip))]
public sealed class SkillCameraTimelineTrack :
    TrackAsset
{
    public override Playable CreateTrackMixer(
        PlayableGraph graph,
        GameObject owner,
        int inputCount)
    {
        return ScriptPlayable<
            SkillCameraTimelineMixer>
            .Create(
                graph,
                inputCount);
    }
}

public sealed class SkillCameraTimelineMixer :
    PlayableBehaviour
{
    private bool[] activeInputs =
        Array.Empty<bool>();

    private SkillCutsceneRuntimeContext
        lastContext;

    public override void ProcessFrame(
        Playable playable,
        FrameData info,
        object playerData)
    {
        SkillCutsceneRuntimeContext context =
            playerData as
                SkillCutsceneRuntimeContext;

        if (context == null)
            return;

        lastContext =
            context;

        int inputCount =
            playable.GetInputCount();

        if (activeInputs.Length !=
            inputCount)
        {
            activeInputs =
                new bool[inputCount];
        }

        int newestInput =
            -1;

        float newestWeight =
            0f;

        for (int index = 0;
             index < inputCount;
             index++)
        {
            float weight =
                playable.GetInputWeight(
                    index);

            bool isActive =
                weight > 0.0001f;

            Playable input =
                playable.GetInput(index);

            ScriptPlayable<
                SkillCameraTimelineBehaviour>
                typed =
                    (ScriptPlayable<
                        SkillCameraTimelineBehaviour>)
                    input;

            SkillCameraTimelineClip asset =
                typed.GetBehaviour()?.Asset;

            if (asset != null &&
                isActive)
            {
                context.UpdateCameraPose(
                    asset,
                    info.deltaTime);

                if (!activeInputs[index])
                {
                    newestInput =
                        index;

                    newestWeight =
                        weight;
                }
                else if (
                    newestInput < 0 &&
                    weight > newestWeight)
                {
                    newestInput =
                        index;

                    newestWeight =
                        weight;
                }
            }

            activeInputs[index] =
                isActive;
        }

        if (newestInput < 0)
            return;

        Playable newestPlayable =
            playable.GetInput(
                newestInput);

        ScriptPlayable<
            SkillCameraTimelineBehaviour>
            newestTyped =
                (ScriptPlayable<
                    SkillCameraTimelineBehaviour>)
                newestPlayable;

        SkillCameraTimelineClip newestAsset =
            newestTyped
                .GetBehaviour()
                ?.Asset;

        if (newestAsset != null)
        {
            context.ActivateCameraClip(
                newestAsset);
        }
    }

    public override void OnGraphStop(
        Playable playable)
    {
        if (lastContext != null)
        {
            lastContext
                .NotifyCameraTrackStopped();
        }

        Array.Clear(
            activeInputs,
            0,
            activeInputs.Length);

        lastContext =
            null;
    }
}