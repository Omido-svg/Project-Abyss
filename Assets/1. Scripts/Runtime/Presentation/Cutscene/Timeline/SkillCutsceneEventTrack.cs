using System.Collections.Generic;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine;

public sealed class SkillCutsceneEventBehaviour :
    PlayableBehaviour
{
    public SkillCutsceneEventClip Asset;
}

[TrackColor(
    0.95f,
    0.3f,
    0.18f)]
[TrackBindingType(
    typeof(
        SkillCutsceneRuntimeContext))]
[TrackClipType(
    typeof(
        SkillCutsceneEventClip))]
public sealed class SkillCutsceneEventTrack :
    TrackAsset
{
    public override Playable CreateTrackMixer(
        PlayableGraph graph,
        GameObject owner,
        int inputCount)
    {
        return ScriptPlayable<
            SkillCutsceneEventMixer>
            .Create(
                graph,
                inputCount);
    }
}

public sealed class SkillCutsceneEventMixer :
    PlayableBehaviour
{
    private readonly HashSet<int>
        activeInputs =
            new HashSet<int>();

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

        int inputCount =
            playable.GetInputCount();

        for (int index = 0;
             index < inputCount;
             index++)
        {
            bool isActive =
                playable.GetInputWeight(
                    index) >
                0.0001f;

            if (!isActive)
            {
                activeInputs.Remove(
                    index);

                continue;
            }

            if (!activeInputs.Add(
                    index))
            {
                continue;
            }

            Playable input =
                playable.GetInput(index);

            ScriptPlayable<
                SkillCutsceneEventBehaviour>
                typed =
                    (ScriptPlayable<
                        SkillCutsceneEventBehaviour>)
                    input;

            SkillCutsceneEventClip asset =
                typed.GetBehaviour()?.Asset;

            if (asset != null)
            {
                context.EmitEvent(
                    asset);
            }
        }
    }

    public override void OnGraphStop(
        Playable playable)
    {
        activeInputs.Clear();
    }
}