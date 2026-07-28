using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.58f, 0.18f, 0.92f)]
[TrackBindingType(typeof(SkillCutsceneRuntimeContext))]
[TrackClipType(typeof(SkillShaderTimelineClip))]
public sealed class SkillShaderTimelineTrack : TrackAsset
{
    public override Playable CreateTrackMixer(
        PlayableGraph graph,
        GameObject owner,
        int inputCount)
    {
        ScriptPlayable<SkillShaderTimelineMixer> playable =
            ScriptPlayable<SkillShaderTimelineMixer>.Create(
                graph,
                inputCount);
        playable.GetBehaviour().TrackInstanceId = GetInstanceID();
        return playable;
    }
}

public sealed class SkillShaderTimelineMixer : PlayableBehaviour
{
    public int TrackInstanceId;

    private readonly List<SkillShaderTimelineContribution>
        contributions = new();
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
        contributions.Clear();

        int inputCount = playable.GetInputCount();

        for (int index = 0; index < inputCount; index++)
        {
            float weight = playable.GetInputWeight(index);

            if (weight <= 0.0001f)
                continue;

            Playable input = playable.GetInput(index);
            ScriptPlayable<SkillShaderTimelineBehaviour> typed =
                (ScriptPlayable<SkillShaderTimelineBehaviour>)input;
            SkillShaderTimelineClip asset =
                typed.GetBehaviour()?.Asset;

            if (asset?.Definition == null)
                continue;

            double duration = Math.Max(0.0001d, input.GetDuration());
            float normalized = Mathf.Clamp01(
                (float)(input.GetTime() / duration));

            contributions.Add(
                new SkillShaderTimelineContribution(
                    asset,
                    normalized,
                    weight));
        }

        context.UpdateShaderFxTrack(
            TrackInstanceId,
            contributions);
    }

    public override void OnGraphStop(Playable playable)
    {
        lastContext?.RemoveShaderFxTrack(TrackInstanceId);
        contributions.Clear();
        lastContext = null;
    }
}
