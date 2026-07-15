using System.Collections.Generic;

internal sealed class BattleVisualPlaybackState
{
    public BattleVisualPlaybackState(BattleVisualRequest request)
    {
        Request = request;
    }

    public BattleVisualRequest Request { get; }

    public List<int> HitDamages { get; } = new();
    public HashSet<BattleVfxInstance> SpawnedVfxInstances { get; } = new();
    public HashSet<string> PlayedVfxCueKeys { get; } = new();

    public int VisualHpStart { get; set; }
    public int VisualHpFinal { get; set; }
    public int VisualDamageAccumulated { get; set; }

    public bool HasVisualHpOverride { get; set; }
    public bool HasBegun { get; set; }
    public bool IsCancellationRequested { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsTargetArrowBound { get; set; }
    public bool IsMomentumDisplayLocked { get; set; }
    public bool IsAnnouncementVisible { get; set; }
    public bool HasFloatingTextActivity { get; set; }
    public bool HasCameraActivity { get; set; }
    public bool ShouldRestoreAttackerPosition { get; set; }
    public bool ShouldRestoreFacing { get; set; }
    public bool IsCleanedUp { get; set; }

    public CharacterActionMover AttackerMover { get; set; }
    public CharacterView AttackerView { get; set; }
    public CharacterView TargetView { get; set; }
    public CharacterFacingController AttackerFacing { get; set; }
    public CharacterFacingController TargetFacing { get; set; }

    public void TrackVfx(BattleVfxInstance instance)
    {
        if (instance != null)
            SpawnedVfxInstances.Add(instance);
    }

    public bool TryMarkVfxCue(string runtimeKey)
    {
        return string.IsNullOrEmpty(runtimeKey) ||
               PlayedVfxCueKeys.Add(runtimeKey);
    }
}
