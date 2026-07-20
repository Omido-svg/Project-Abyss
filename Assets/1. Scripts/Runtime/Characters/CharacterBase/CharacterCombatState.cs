using System.Collections.Generic;

public enum CharacterCombatPhase
{
    Uninitialized,
    Initializing,
    Ready,
    Dead,
    Disposed
}

public sealed class CharacterCombatState
{
    private readonly List<DamageContext> damageResolutionStack = new();

    public CharacterCombatPhase Phase { get; private set; } =
        CharacterCombatPhase.Uninitialized;

    public int InitializationVersion { get; private set; }

    public bool IsInitialized =>
        Phase == CharacterCombatPhase.Ready ||
        Phase == CharacterCombatPhase.Dead;

    public bool IsInitializing =>
        Phase == CharacterCombatPhase.Initializing;

    public bool IsDead =>
        Phase == CharacterCombatPhase.Dead;

    public bool IsDisposed =>
        Phase == CharacterCombatPhase.Disposed;

    public bool IsDamageResolutionInProgress =>
        damageResolutionStack.Count > 0;

    public DamageContext ActiveDamageContext =>
        damageResolutionStack.Count == 0
            ? null
            : damageResolutionStack[
                damageResolutionStack.Count - 1];

    public int TurnClashPowerBonus { get; private set; }

    public void BeginInitialization()
    {
        damageResolutionStack.Clear();
        TurnClashPowerBonus = 0;
        InitializationVersion++;
        Phase = CharacterCombatPhase.Initializing;
    }

    public void CompleteInitialization()
    {
        if (Phase == CharacterCombatPhase.Disposed)
            return;

        Phase = CharacterCombatPhase.Ready;
    }

    public void FailInitialization()
    {
        damageResolutionStack.Clear();
        Phase = CharacterCombatPhase.Uninitialized;
    }

    public void MarkAlive()
    {
        if (Phase == CharacterCombatPhase.Disposed)
            return;

        if (Phase != CharacterCombatPhase.Initializing)
            Phase = CharacterCombatPhase.Ready;
    }

    public void MarkDead()
    {
        if (Phase == CharacterCombatPhase.Disposed)
            return;

        Phase = CharacterCombatPhase.Dead;
    }

    public void BeginDamageResolution(
        DamageContext context)
    {
        if (context == null || IsDisposed)
            return;

        damageResolutionStack.Add(context);
    }

    public void EndDamageResolution(
        DamageContext context)
    {
        if (context == null ||
            damageResolutionStack.Count == 0)
        {
            return;
        }

        int lastIndex =
            damageResolutionStack.Count - 1;

        if (damageResolutionStack[lastIndex] == context)
        {
            damageResolutionStack.RemoveAt(lastIndex);
            return;
        }

        // 중첩 피해 중 예외적인 순서로 종료되더라도
        // 다른 활성 DamageContext를 잃지 않도록 해당 항목만 제거한다.
        for (int i = lastIndex; i >= 0; i--)
        {
            if (damageResolutionStack[i] != context)
                continue;

            damageResolutionStack.RemoveAt(i);
            break;
        }
    }

    public void ClearDamageResolution()
    {
        damageResolutionStack.Clear();
    }

    public void AddTurnClashPowerBonus(int amount)
    {
        TurnClashPowerBonus += amount;
    }

    public void ClearTurnModifiers()
    {
        TurnClashPowerBonus = 0;
    }

    public void Dispose()
    {
        damageResolutionStack.Clear();
        TurnClashPowerBonus = 0;
        Phase = CharacterCombatPhase.Disposed;
    }
}
