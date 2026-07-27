using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BossPhaseController
{
    private readonly Character owner;
    private readonly List<BossPhaseData> phases;

    public BossPhaseData CurrentPhase { get; private set; }
    public int CurrentPhaseIndex { get; private set; } = -1;

    public event Action<BossPhaseData, BossPhaseData> PhaseChanged;

    public BossPhaseController(
        Character owner,
        IReadOnlyList<BossPhaseData> source)
    {
        this.owner = owner;
        phases = new List<BossPhaseData>();

        if (source == null)
            return;

        foreach (BossPhaseData phase in source)
        {
            if (phase != null)
                phases.Add(phase);
        }
    }

    public bool Evaluate(
        BattleContext context,
        int currentTurn)
    {
        int nextIndex = ResolvePhaseIndex(
            context,
            currentTurn);

        if (nextIndex == CurrentPhaseIndex)
            return false;

        BossPhaseData previous = CurrentPhase;

        CurrentPhaseIndex = nextIndex;
        CurrentPhase =
            nextIndex >= 0 && nextIndex < phases.Count
                ? phases[nextIndex]
                : null;

        PhaseChanged?.Invoke(previous, CurrentPhase);

        Debug.Log(
            $"[BOSS PHASE] {owner?.Data?.CharacterName ?? owner?.name} / " +
            $"{previous?.DisplayName ?? "NONE"} -> " +
            $"{CurrentPhase?.DisplayName ?? "NONE"}");

        return true;
    }

    private int ResolvePhaseIndex(
        BattleContext context,
        int currentTurn)
    {
        int result = -1;

        for (int i = 0; i < phases.Count; i++)
        {
            BossPhaseData phase = phases[i];

            if (phase == null ||
                !phase.IsSatisfied(
                    owner,
                    context,
                    currentTurn))
            {
                continue;
            }

            result = i;
        }

        return result;
    }
}
