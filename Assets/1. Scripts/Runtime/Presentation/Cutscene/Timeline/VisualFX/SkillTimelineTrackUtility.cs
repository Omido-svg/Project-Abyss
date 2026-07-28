using System.Collections.Generic;
using UnityEngine.Timeline;

public static class SkillTimelineTrackUtility
{
    public static IEnumerable<TrackAsset> EnumerateAllTracks(
        TimelineAsset timeline)
    {
        if (timeline == null)
            yield break;

        HashSet<TrackAsset> visited = new();

        foreach (TrackAsset root in timeline.GetRootTracks())
        {
            foreach (TrackAsset track in EnumerateRecursive(root, visited))
                yield return track;
        }
    }

    private static IEnumerable<TrackAsset> EnumerateRecursive(
        TrackAsset track,
        HashSet<TrackAsset> visited)
    {
        if (track == null || !visited.Add(track))
            yield break;

        yield return track;

        foreach (TrackAsset child in track.GetChildTracks())
        {
            foreach (TrackAsset descendant in EnumerateRecursive(child, visited))
                yield return descendant;
        }
    }
}
