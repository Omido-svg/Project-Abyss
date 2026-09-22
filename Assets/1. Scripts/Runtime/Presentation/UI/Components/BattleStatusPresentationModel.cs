using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleStatusPresentationKind
{
    NumericTimed = 0,
    PresenceTimed = 1,
    Bespoke = 2
}

/// <summary>
/// 0922 Phase 11 상태 HUD용 읽기 전용 projection.
/// Runtime StatusEffect를 변경하지 않고 현재/다음 턴 표시값만 계산한다.
/// </summary>
public sealed class BattleStatusChipModel
{
    public StatusEffect Representative { get; internal set; }
    public Character ViewedCharacter { get; internal set; }
    public BattleStatusPresentationKind PresentationKind { get; internal set; }
    public string StableKey { get; internal set; }
    public int EntryCount { get; internal set; }

    public int CurrentTotalValue { get; internal set; }
    public int NextTurnProjectedValue { get; internal set; }

    public bool CurrentActive { get; internal set; }
    public bool NextTurnActive { get; internal set; }

    public int BespokeCurrentValue { get; internal set; }
    public int BespokeNextTurnValue { get; internal set; }

    /// <summary>
    /// finite일 때 현재 살아 있는 Entry 중 가장 긴 남은 턴.
    /// ∞가 하나라도 있으면 StatusEffect.InfiniteDuration(-1).
    /// </summary>
    public int MaxRemainingDuration { get; internal set; }

    public bool HasInfiniteDuration =>
        MaxRemainingDuration == StatusEffect.InfiniteDuration;

    public bool SupportsNextTurnProjection { get; internal set; }

    public bool HasProjectionChange
    {
        get
        {
            if (!SupportsNextTurnProjection)
                return false;

            return PresentationKind switch
            {
                BattleStatusPresentationKind.NumericTimed =>
                    CurrentTotalValue != NextTurnProjectedValue,
                BattleStatusPresentationKind.PresenceTimed =>
                    CurrentActive != NextTurnActive,
                BattleStatusPresentationKind.Bespoke =>
                    BespokeCurrentValue != BespokeNextTurnValue,
                _ => false
            };
        }
    }

    public string StateSignature =>
        $"{StableKey}|{EntryCount}|{CurrentTotalValue}|{NextTurnProjectedValue}|" +
        $"{CurrentActive}|{NextTurnActive}|{BespokeCurrentValue}|{BespokeNextTurnValue}|" +
        $"{MaxRemainingDuration}|{SupportsNextTurnProjection}";
}

public static class BattleStatusPresentationProjector
{
    private readonly struct GroupKey : IEquatable<GroupKey>
    {
        public readonly Type EffectType;
        public readonly BodyPart OwnerPart;
        public readonly StatusEffectStorageKind StorageKind;
        public readonly string SemanticId;

        public GroupKey(StatusEffect effect)
        {
            EffectType = effect?.GetType();
            OwnerPart = effect?.OwnerPart;
            StorageKind = effect?.StorageKind ?? StatusEffectStorageKind.Bespoke;
            SemanticId = BattleStatusPresentationProjector.GetSemanticId(effect);
        }

        public bool Equals(GroupKey other) =>
            EffectType == other.EffectType &&
            object.ReferenceEquals(OwnerPart, other.OwnerPart) &&
            StorageKind == other.StorageKind &&
            string.Equals(SemanticId, other.SemanticId, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is GroupKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EffectType != null ? EffectType.GetHashCode() : 0;
                hash = hash * 397 ^ (OwnerPart != null ? OwnerPart.GetHashCode() : 0);
                hash = hash * 397 ^ (int)StorageKind;
                hash = hash * 397 ^ (SemanticId != null ? StringComparer.Ordinal.GetHashCode(SemanticId) : 0);
                return hash;
            }
        }
    }

    public static List<BattleStatusChipModel> Build(Character owner)
    {
        List<StatusEffect> effects = new();

        if (owner?.StatusEffects != null)
            effects.AddRange(owner.StatusEffects);

        if (owner?.BodyParts != null)
        {
            foreach (BodyPart part in owner.BodyParts)
            {
                if (part?.StatusEffects != null)
                    effects.AddRange(part.StatusEffects);
            }
        }

        return BuildFromEffects(owner, effects);
    }

    /// <summary>
    /// Pure projection entry point. 전달받은 StatusEffect의 Stack/Duration을 절대 변경하지 않는다.
    /// </summary>
    public static List<BattleStatusChipModel> BuildFromEffects(
        Character viewedCharacter,
        IReadOnlyList<StatusEffect> effects)
    {
        Dictionary<GroupKey, List<StatusEffect>> groups = new();

        if (effects != null)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                StatusEffect effect = effects[i];
                if (effect == null || effect.IsExpired)
                    continue;

                GroupKey key = new(effect);
                if (!groups.TryGetValue(key, out List<StatusEffect> entries))
                {
                    entries = new List<StatusEffect>();
                    groups.Add(key, entries);
                }

                entries.Add(effect);
            }
        }

        List<BattleStatusChipModel> result = new(groups.Count);

        foreach (KeyValuePair<GroupKey, List<StatusEffect>> pair in groups)
        {
            List<StatusEffect> entries = pair.Value;
            if (entries == null || entries.Count == 0)
                continue;

            StatusEffect representative = entries[0];
            BattleStatusChipModel model = new()
            {
                Representative = representative,
                ViewedCharacter = viewedCharacter,
                PresentationKind = ToPresentationKind(representative.StorageKind),
                StableKey = BuildStableKey(representative),
                EntryCount = entries.Count,
                CurrentActive = true,
                MaxRemainingDuration = GetMaxRemainingDuration(entries)
            };

            switch (model.PresentationKind)
            {
                case BattleStatusPresentationKind.NumericTimed:
                    BuildNumeric(model, entries);
                    break;

                case BattleStatusPresentationKind.PresenceTimed:
                    BuildPresence(model, entries);
                    break;

                default:
                    BuildBespoke(model, entries);
                    break;
            }

            result.Add(model);
        }

        result.Sort(CompareModels);
        return result;
    }

    private static void BuildNumeric(
        BattleStatusChipModel model,
        IReadOnlyList<StatusEffect> entries)
    {
        int current = 0;
        int next = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            StatusEffect effect = entries[i];
            int value = Mathf.Max(0, effect?.NumericValue ?? 0);
            current += value;

            if (SurvivesCurrentTurnEnd(effect))
                next += value;
        }

        model.CurrentTotalValue = Mathf.Max(0, current);
        model.NextTurnProjectedValue = Mathf.Max(0, next);
        model.CurrentActive = model.CurrentTotalValue > 0;
        model.NextTurnActive = model.NextTurnProjectedValue > 0;
        model.SupportsNextTurnProjection = true;
    }

    private static void BuildPresence(
        BattleStatusChipModel model,
        IReadOnlyList<StatusEffect> entries)
    {
        bool current = false;
        bool next = false;

        for (int i = 0; i < entries.Count; i++)
        {
            StatusEffect effect = entries[i];
            if (effect == null || effect.IsExpired)
                continue;

            current = true;
            if (SurvivesCurrentTurnEnd(effect))
                next = true;
        }

        model.CurrentTotalValue = 0;
        model.NextTurnProjectedValue = 0;
        model.CurrentActive = current;
        model.NextTurnActive = next;
        model.SupportsNextTurnProjection = true;
    }

    private static void BuildBespoke(
        BattleStatusChipModel model,
        IReadOnlyList<StatusEffect> entries)
    {
        int current = 0;
        int next = 0;

        if (model.Representative is Bleeding)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                int stack = Mathf.Max(0, entries[i]?.Stack ?? 0);
                current += stack;
                next += Mathf.Max(0, stack - 1);
            }

            model.SupportsNextTurnProjection = false;
        }
        else
        {
            for (int i = 0; i < entries.Count; i++)
                current += Mathf.Max(0, entries[i]?.Stack ?? 0);

            next = current;
            model.SupportsNextTurnProjection = false;
        }

        model.BespokeCurrentValue = Mathf.Max(0, current);
        model.BespokeNextTurnValue = Mathf.Max(0, next);
        model.CurrentActive = true;
        model.NextTurnActive = true;
    }

    private static int GetMaxRemainingDuration(
        IReadOnlyList<StatusEffect> entries)
    {
        int max = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            StatusEffect effect = entries[i];
            if (effect == null || effect.IsExpired)
                continue;

            if (effect.IsInfiniteDuration)
                return StatusEffect.InfiniteDuration;

            max = Mathf.Max(max, effect.RemainingTurns);
        }

        return max;
    }

    private static bool SurvivesCurrentTurnEnd(StatusEffect effect)
    {
        if (effect == null || effect.IsExpired)
            return false;

        if (effect.IsInfiniteDuration)
            return true;

        return effect.DurationPolicy switch
        {
            StatusEffectDurationPolicy.TurnEnd => effect.RemainingTurns > 1,
            StatusEffectDurationPolicy.TurnStart => effect.RemainingTurns > 0,
            StatusEffectDurationPolicy.Permanent => true,
            _ => !effect.IsExpired
        };
    }

    private static BattleStatusPresentationKind ToPresentationKind(
        StatusEffectStorageKind kind) =>
        kind switch
        {
            StatusEffectStorageKind.NumericTimed =>
                BattleStatusPresentationKind.NumericTimed,
            StatusEffectStorageKind.PresenceTimed =>
                BattleStatusPresentationKind.PresenceTimed,
            _ => BattleStatusPresentationKind.Bespoke
        };

    private static string BuildStableKey(StatusEffect effect)
    {
        if (effect == null)
            return "status:null";

        string scope =
            effect.OwnerPart != null
                ? $"part:{effect.OwnerPart.PartId ?? effect.OwnerPart.Type.ToString()}"
                : "character";

        return $"{effect.StorageKind}:{effect.GetType().FullName}:{GetSemanticId(effect)}:{scope}";
    }

    private static string GetSemanticId(StatusEffect effect)
    {
        if (effect == null)
            return "null";

        if (effect is IUniqueKeywordStatus unique &&
            !string.IsNullOrWhiteSpace(unique.UniqueKeywordId))
        {
            return unique.UniqueKeywordId;
        }

        if (effect is DeferredStatusEffect deferred)
            return $"deferred:{deferred.DeferredStatusId}";

        if (!string.IsNullOrWhiteSpace(effect.EffectName))
            return effect.EffectName;

        return effect.Name ?? effect.GetType().Name;
    }

    private static int CompareModels(
        BattleStatusChipModel left,
        BattleStatusChipModel right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;

        int kind = left.PresentationKind.CompareTo(right.PresentationKind);
        if (kind != 0)
            return kind;

        string leftName = BattleStatusUiText.GetDisplayName(left.Representative);
        string rightName = BattleStatusUiText.GetDisplayName(right.Representative);
        int name = string.Compare(leftName, rightName, StringComparison.Ordinal);
        if (name != 0)
            return name;

        return string.Compare(left.StableKey, right.StableKey, StringComparison.Ordinal);
    }
}
