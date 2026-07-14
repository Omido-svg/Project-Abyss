using System;

public enum BattleLifecycleState
{
    Created,
    Ready,
    Running,
    Resolving,
    Ended,
    Faulted,
    Disposed
}

public sealed class BattleLifecycleGuard : IDisposable
{
    private int nextResolutionToken;
    private int activeResolutionToken;

    public BattleLifecycleState State { get; private set; } =
        BattleLifecycleState.Created;

    public Exception LastFailure { get; private set; }

    public bool IsDisposed =>
        State == BattleLifecycleState.Disposed;

    public bool IsBattleRunning =>
        State == BattleLifecycleState.Running ||
        State == BattleLifecycleState.Resolving;

    public bool IsResolving =>
        State == BattleLifecycleState.Resolving;

    public bool CanStartTurn =>
        State == BattleLifecycleState.Running;

    public bool MarkReady()
    {
        if (State != BattleLifecycleState.Created)
            return false;

        State = BattleLifecycleState.Ready;
        LastFailure = null;
        return true;
    }

    public bool TryStartBattle()
    {
        if (State != BattleLifecycleState.Ready)
            return false;

        State = BattleLifecycleState.Running;
        LastFailure = null;
        return true;
    }

    public bool TryBeginResolution(
        out int resolutionToken)
    {
        resolutionToken = 0;

        if (State != BattleLifecycleState.Running)
            return false;

        nextResolutionToken++;

        if (nextResolutionToken <= 0)
            nextResolutionToken = 1;

        activeResolutionToken = nextResolutionToken;
        resolutionToken = activeResolutionToken;
        State = BattleLifecycleState.Resolving;
        return true;
    }

    public bool IsResolutionCurrent(
        int resolutionToken)
    {
        return State == BattleLifecycleState.Resolving &&
               resolutionToken > 0 &&
               activeResolutionToken == resolutionToken;
    }

    public void CompleteResolution(
        int resolutionToken)
    {
        if (!IsResolutionCurrent(resolutionToken))
            return;

        activeResolutionToken = 0;
        State = BattleLifecycleState.Running;
    }

    public void EndBattle()
    {
        if (State == BattleLifecycleState.Disposed ||
            State == BattleLifecycleState.Ended)
        {
            return;
        }

        activeResolutionToken = 0;
        nextResolutionToken++;
        State = BattleLifecycleState.Ended;
    }

    public void Fault(Exception exception)
    {
        if (State == BattleLifecycleState.Disposed)
            return;

        LastFailure = exception;
        activeResolutionToken = 0;
        nextResolutionToken++;
        State = BattleLifecycleState.Faulted;
    }

    public void Dispose()
    {
        if (State == BattleLifecycleState.Disposed)
            return;

        activeResolutionToken = 0;
        nextResolutionToken++;
        State = BattleLifecycleState.Disposed;
    }
}
