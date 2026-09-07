using System.Collections.Generic;

internal sealed class BattleVisualPlaybackState
{
    public BattleVisualPlaybackState(BattleVisualRequest request)
    {
        RootRequest = request;
        Request = request;
    }

    public BattleVisualRequest RootRequest { get; }
    public BattleVisualRequest Request { get; private set; }

    public List<int> HitDamages { get; } = new();
    public HashSet<BattleVfxInstance> SpawnedVfxInstances { get; } = new();
    public HashSet<string> PlayedVfxCueKeys { get; } = new();
    public List<BattleVisualHpOverrideTarget> HpOverrideTargets { get; } = new();
    public HashSet<Character> StaggerOverrideTargets { get; } = new();

    public Dictionary<CharacterActionMover, CharacterActionMoveSettings>
        StagedMoverSettings { get; } = new();

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

    // 전체 합에서 고정되는 첫 번째 참가자 기준 참조.
    public CharacterActionMover AttackerMover { get; set; }
    public CharacterActionMover TargetMover { get; set; }
    public CharacterView AttackerView { get; set; }
    public CharacterView TargetView { get; set; }
    public CharacterFacingController AttackerFacing { get; set; }
    public CharacterFacingController TargetFacing { get; set; }

    // 현재 교환에서 실제 공격 애니메이션을 재생 중인 View.
    public CharacterView ActiveActionView { get; set; }
    public CharacterView ActiveTargetView { get; set; }

    // 마지막 Hit Event가 재생한 타깃 반응은 정상 종료 cleanup에서 자르지 않습니다.
    // 다음 합 모션/피격 반응이 시작되면 CharacterView가 스스로 교체합니다.
    public CharacterView ActiveReactionView { get; set; }

    public void SetCurrentRequest(BattleVisualRequest request)
    {
        Request = request ?? RootRequest;
    }

    public void ResetToRootRequest()
    {
        Request = RootRequest;
        ActiveActionView = null;
        ActiveTargetView = null;
    }

    public void BeginCueScope()
    {
        PlayedVfxCueKeys.Clear();
    }

    public void TrackStagedMover(
        CharacterActionMover mover,
        CharacterActionMoveSettings settings)
    {
        if (mover == null)
            return;

        // 전체 합 진입에서 이미 추적한 Mover의 복귀 정책을
        // 개별 스킬의 이동 설정으로 덮어쓰지 않습니다.
        if (!StagedMoverSettings.ContainsKey(mover))
        {
            StagedMoverSettings.Add(
                mover,
                settings);
        }
    }

    public void ClearStagedMovers()
    {
        StagedMoverSettings.Clear();
    }

    public void TrackHpOverride(Character character, BodyPart part)
    {
        if (character == null)
            return;

        foreach (BattleVisualHpOverrideTarget target in HpOverrideTargets)
        {
            if (ReferenceEquals(target.Character, character) &&
                ReferenceEquals(target.Part, part))
            {
                return;
            }
        }

        HpOverrideTargets.Add(
            new BattleVisualHpOverrideTarget(character, part));
    }

    public void TrackStaggerOverride(Character character)
    {
        if (character != null)
            StaggerOverrideTargets.Add(character);
    }

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

internal readonly struct BattleVisualHpOverrideTarget
{
    public BattleVisualHpOverrideTarget(
        Character character,
        BodyPart part)
    {
        Character = character;
        Part = part;
    }

    public Character Character { get; }
    public BodyPart Part { get; }
}