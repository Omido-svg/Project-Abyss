using System;
using System.Collections.Generic;

/// <summary>
/// Y-07 및 향후 카드가 공유하는 전투 내 지연 트리거 큐.
/// - 특정 Runtime trigger가 오면 ConsumeTriggered로 발동
/// - N턴 후 자동 발동은 AdvanceTurn 결과로 반환
/// - duration만 가진 항목은 만료 시 조용히 제거
/// 수치/효과 payload는 호출자가 소유하므로 기획 (미정)을 하드코딩하지 않는다.
/// </summary>
public sealed class DelayedEffectTriggerEntry
{
    public long Id { get; internal set; }
    public CombatStatusAnchor Anchor { get; internal set; }
    public string TriggerKey { get; internal set; }
    public int DurationTurns { get; internal set; }
    public int AutoTriggerAfterTurns { get; internal set; }
    public int ElapsedTurns { get; internal set; }
    public object Payload { get; internal set; }
    public BattleAction SourceAction { get; internal set; }
}

public sealed class DelayedEffectTriggerQueue
{
    private readonly List<DelayedEffectTriggerEntry> entries = new();
    private long nextId = 1;

    public int Count => entries.Count;

    public long Schedule(
        CombatStatusAnchor anchor,
        string triggerKey,
        int durationTurns,
        int autoTriggerAfterTurns,
        object payload,
        BattleAction sourceAction = null)
    {
        if (!anchor.IsValid || string.IsNullOrWhiteSpace(triggerKey))
            return 0;

        DelayedEffectTriggerEntry entry = new()
        {
            Id = nextId++,
            Anchor = anchor,
            TriggerKey = triggerKey.Trim(),
            DurationTurns = Math.Max(0, durationTurns),
            AutoTriggerAfterTurns = Math.Max(0, autoTriggerAfterTurns),
            ElapsedTurns = 0,
            Payload = payload,
            SourceAction = sourceAction
        };

        entries.Add(entry);
        return entry.Id;
    }

    public IReadOnlyList<DelayedEffectTriggerEntry> ConsumeTriggered(
        CombatStatusAnchor anchor,
        string triggerKey)
    {
        List<DelayedEffectTriggerEntry> fired = new();

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            DelayedEffectTriggerEntry entry = entries[i];
            if (!entry.Anchor.Equals(anchor) ||
                !string.Equals(entry.TriggerKey, triggerKey, StringComparison.Ordinal))
            {
                continue;
            }

            fired.Add(entry);
            entries.RemoveAt(i);
        }

        fired.Reverse();
        return fired;
    }

    public IReadOnlyList<DelayedEffectTriggerEntry> AdvanceTurn()
    {
        List<DelayedEffectTriggerEntry> autoFired = new();

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            DelayedEffectTriggerEntry entry = entries[i];

            // Target-bound delayed effects must not survive their target.
            // Keep this generic so character-specific mechanics do not each grow cleanup branches.
            if (!entry.Anchor.IsValid ||
                entry.Anchor.Character.IsDead ||
                entry.Anchor.IsBroken)
            {
                entries.RemoveAt(i);
                continue;
            }

            entry.ElapsedTurns++;

            if (entry.AutoTriggerAfterTurns > 0 &&
                entry.ElapsedTurns >= entry.AutoTriggerAfterTurns)
            {
                autoFired.Add(entry);
                entries.RemoveAt(i);
                continue;
            }

            if (entry.DurationTurns > 0 &&
                entry.ElapsedTurns >= entry.DurationTurns)
            {
                entries.RemoveAt(i);
            }
        }

        autoFired.Reverse();
        return autoFired;
    }

    public int RemoveForCharacter(Character character)
    {
        if (character == null)
            return 0;

        int removed = 0;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(entries[i].Anchor.Character, character))
                continue;

            entries.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    public int RemoveForPart(Character character, BodyPart part)
    {
        if (character == null || part == null)
            return 0;

        int removed = 0;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            CombatStatusAnchor anchor = entries[i].Anchor;
            if (!ReferenceEquals(anchor.Character, character) ||
                !ReferenceEquals(anchor.Part, part))
            {
                continue;
            }

            entries.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    public void Clear() => entries.Clear();
}
