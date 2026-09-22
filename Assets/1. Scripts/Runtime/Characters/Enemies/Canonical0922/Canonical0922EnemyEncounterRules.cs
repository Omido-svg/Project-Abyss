using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyRole0922
{
    DealerSlot = 0,
    Attacker = 1,
    Duelist = 2,
    Debuffer = 3,
    Buffer = 4,
    Tank = 5
}

[Serializable]
public sealed class Canonical0922EnemyEncounterEntry
{
    public string Id;
    [Range(3, 4)] public int EnemyCount;
    [Range(0f, 1f)] public float OverallProbability;
    public List<EnemyRole0922> Roles = new();
}

public readonly struct EnemyDuelQuotaPlan0922
{
    public EnemyDuelQuotaPlan0922(int requested, int assigned, bool insufficientCandidates)
    {
        Requested = requested;
        Assigned = assigned;
        InsufficientCandidates = insufficientCandidates;
    }

    public int Requested { get; }
    public int Assigned { get; }
    public bool InsufficientCandidates { get; }
}

public static class Canonical0922EnemyEncounterRules
{
    public const int ThreeEnemyHp = 135;
    public const int FourEnemyHp = 101;
    public const int NormalEnemyStaggerMax = 100;
    public const int NormalEnemyRollCount = 3;

    private static Canonical0922EnemyEncounterCatalog cachedCatalog;

    public static Canonical0922EnemyEncounterCatalog Catalog
    {
        get
        {
            if (cachedCatalog == null)
                cachedCatalog = Resources.Load<Canonical0922EnemyEncounterCatalog>(Canonical0922EnemyEncounterCatalog.ResourcesPath);
            return cachedCatalog;
        }
    }

    public static int RollEnemyCount(float sample01)
    {
        float p3 = Catalog != null ? Catalog.ThreeEnemyProbability : 0.25f;
        return Mathf.Clamp01(sample01) < p3 ? 3 : 4;
    }

    public static int RollDuelQuota(float sample01)
    {
        float p1 = Catalog != null ? Catalog.OneDuelProbability : 0.25f;
        float p2 = Catalog != null ? Catalog.TwoDuelProbability : 0.50f;
        float value = Mathf.Clamp01(sample01);
        if (value < p1) return 1;
        if (value < p1 + p2) return 2;
        return 3;
    }

    public static int ResolveNormalEnemyHp(BattleContext context, int fallback)
    {
        if (context?.Enemies == null)
            return Mathf.Max(1, fallback);

        int normalCount = 0;
        int aliveOrConfigured = 0;
        foreach (Character enemy in context.Enemies)
        {
            if (enemy == null)
                continue;
            aliveOrConfigured++;
            if (enemy is NormalEnemy)
                normalCount++;
        }

        if (normalCount != aliveOrConfigured)
            return Mathf.Max(1, fallback);

        if (normalCount == 3) return ThreeEnemyHp;
        if (normalCount == 4) return FourEnemyHp;
        return Mathf.Max(1, fallback);
    }

    public static Canonical0922EnemyEncounterEntry GetComposition(int enemyCount, float sample01)
    {
        IReadOnlyList<Canonical0922EnemyEncounterEntry> source = Catalog?.NormalEncounters;
        if (source == null || source.Count == 0)
        {
            Canonical0922EnemyEncounterCatalog temp = ScriptableObject.CreateInstance<Canonical0922EnemyEncounterCatalog>();
            temp.ResetToCanonical();
            Canonical0922EnemyEncounterEntry result = PickByCount(temp.NormalEncounters, enemyCount, sample01);
            UnityEngine.Object.Destroy(temp);
            return result;
        }

        return PickByCount(source, enemyCount, sample01);
    }

    private static Canonical0922EnemyEncounterEntry PickByCount(
        IReadOnlyList<Canonical0922EnemyEncounterEntry> source,
        int enemyCount,
        float sample01)
    {
        List<Canonical0922EnemyEncounterEntry> matches = new();
        for (int i = 0; i < source.Count; i++)
        {
            Canonical0922EnemyEncounterEntry entry = source[i];
            if (entry != null && entry.EnemyCount == enemyCount)
                matches.Add(entry);
        }

        if (matches.Count == 0)
            return null;

        int index = Mathf.Clamp(
            Mathf.FloorToInt(Mathf.Clamp01(sample01) * matches.Count),
            0,
            matches.Count - 1);
        return matches[index];
    }

    public static EnemyRole0922 ResolveDealerSlot(float sample01) =>
        Mathf.Clamp01(sample01) < 0.5f
            ? EnemyRole0922.Attacker
            : EnemyRole0922.Duelist;

    public static float GetDuelWeight(EnemyRole0922 role, bool hasLivingAlly)
    {
        return role switch
        {
            EnemyRole0922.Attacker => 0.50f,
            EnemyRole0922.Duelist => 0.75f,
            EnemyRole0922.Debuffer => 0.50f,
            EnemyRole0922.Buffer => hasLivingAlly ? 0f : 0.25f,
            EnemyRole0922.Tank => 0.50f,
            _ => 0f
        };
    }

    public static float GetNormalWeight(EnemyRole0922 role, bool hasLivingAlly)
    {
        return role switch
        {
            EnemyRole0922.Attacker => 0.50f,
            EnemyRole0922.Duelist => 0.25f,
            EnemyRole0922.Debuffer => 0.50f,
            EnemyRole0922.Buffer => hasLivingAlly ? 0f : 0.75f,
            EnemyRole0922.Tank => 0.50f,
            _ => 0f
        };
    }

    public static float GetPreparationWeight(EnemyRole0922 role, bool hasLivingAlly) =>
        role == EnemyRole0922.Buffer && hasLivingAlly ? 1f : 0f;

    public static SkillColor RollPreferredColor(EnemyRole0922 role, bool hasLivingAlly, float sample01)
    {
        float r = Mathf.Clamp01(sample01);
        return role switch
        {
            EnemyRole0922.Attacker => SkillColor.Red,
            EnemyRole0922.Duelist => SkillColor.Red,
            EnemyRole0922.Debuffer => r < 0.75f ? SkillColor.Blue : SkillColor.Red,
            EnemyRole0922.Buffer => hasLivingAlly
                ? SkillColor.Unset
                : (r < 0.50f ? SkillColor.Red : SkillColor.Blue),
            EnemyRole0922.Tank => r < 0.50f ? SkillColor.Red : SkillColor.Blue,
            _ => SkillColor.Unset
        };
    }

    public static int GetCoverPriority(float hpRate, EnemyRole0922 candidateRole)
    {
        if (Mathf.Clamp01(hpRate) <= 0.50f)
            return 0;
        if (candidateRole == EnemyRole0922.Buffer)
            return 1;
        return 2;
    }

    public static int GetCoverPriority(Character candidate, EnemyRole0922 candidateRole)
    {
        if (candidate == null || candidate.IsDead)
            return int.MaxValue;

        float hpRate = candidate.MaxCombatHP <= 0
            ? 1f
            : (float)candidate.CurrentHP / candidate.MaxCombatHP;

        return GetCoverPriority(hpRate, candidateRole);
    }

    public static bool IsCanonicalNormalEncounterCount(int count) => count == 3 || count == 4;
}
