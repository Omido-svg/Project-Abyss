using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(1f, 0.45f, 0.08f)]
[TrackBindingType(typeof(SkillCutsceneRuntimeContext))]
[TrackClipType(typeof(SkillVfxTimelineClip))]
public sealed class SkillVfxTimelineTrack : TrackAsset
{
    public override Playable CreateTrackMixer(
        PlayableGraph graph,
        GameObject owner,
        int inputCount)
    {
        ScriptPlayable<SkillVfxTimelineMixer> playable =
            ScriptPlayable<SkillVfxTimelineMixer>.Create(
                graph,
                inputCount);
        playable.GetBehaviour().TrackInstanceId = GetInstanceID();
        return playable;
    }
}

public sealed class SkillVfxTimelineMixer : PlayableBehaviour
{
    public int TrackInstanceId;

    private bool[] activeInputs = Array.Empty<bool>();
    private SkillCutsceneRuntimeContext lastContext;

    public override void ProcessFrame(
        Playable playable,
        FrameData info,
        object playerData)
    {
        SkillCutsceneRuntimeContext context =
            playerData as SkillCutsceneRuntimeContext;

        if (context == null)
            return;

        lastContext = context;
        int inputCount = playable.GetInputCount();

        if (activeInputs.Length != inputCount)
            activeInputs = new bool[inputCount];

        for (int index = 0; index < inputCount; index++)
        {
            Playable input = playable.GetInput(index);
            float weight = playable.GetInputWeight(index);
            bool isActive = weight > 0.0001f;

            ScriptPlayable<SkillVfxTimelineBehaviour> typed =
                (ScriptPlayable<SkillVfxTimelineBehaviour>)input;
            SkillVfxTimelineClip asset =
                typed.GetBehaviour()?.Asset;

            if (asset == null)
            {
                activeInputs[index] = false;
                continue;
            }

            int clipKey = asset.GetInstanceID();

            if (isActive)
            {
                double duration = Math.Max(
                    0.0001d,
                    input.GetDuration());
                float normalized = Mathf.Clamp01(
                    (float)(input.GetTime() / duration));

                if (!activeInputs[index])
                {
                    context.BeginTimelineVfx(
                        TrackInstanceId,
                        clipKey,
                        asset,
                        (float)duration);
                }

                context.UpdateTimelineVfx(
                    TrackInstanceId,
                    clipKey,
                    asset,
                    normalized,
                    weight);
            }
            else if (activeInputs[index])
            {
                context.EndTimelineVfx(
                    TrackInstanceId,
                    clipKey,
                    asset);
            }

            activeInputs[index] = isActive;
        }
    }

    public override void OnGraphStop(Playable playable)
    {
        lastContext?.StopTimelineVfxTrack(TrackInstanceId);
        Array.Clear(activeInputs, 0, activeInputs.Length);
        lastContext = null;
    }
}
