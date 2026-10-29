using System.Collections.Generic;

/// <summary>
/// Collects UI invalidations independently of MonoBehaviour and rendering.
/// Drain() takes an immutable-for-callers snapshot before any view callbacks run.
/// Invalidations arriving during a refresh remain queued for the next Flush().
/// </summary>
public sealed class BattleUIRefreshBatch
{
    private readonly HashSet<Character> dirtyCharacters = new();
    private readonly Dictionary<BodyPart, Character> dirtyParts = new();
    private bool refreshAll;

    public bool HasPending => refreshAll || dirtyCharacters.Count > 0 || dirtyParts.Count > 0;

    public void MarkAll()
    {
        refreshAll = true;
        dirtyCharacters.Clear();
        dirtyParts.Clear();
    }

    public void MarkCharacter(Character character)
    {
        if (!refreshAll && character != null)
            dirtyCharacters.Add(character);
    }

    public void MarkPart(Character character, BodyPart part)
    {
        if (refreshAll)
            return;

        if (part == null)
        {
            MarkCharacter(character);
            return;
        }

        dirtyParts[part] = character != null ? character : part.Owner;
    }

    public BattleUIRefreshSnapshot Drain()
    {
        if (!HasPending)
            return BattleUIRefreshSnapshot.Empty;

        // Snapshot first, then clear. A callback can freely queue new changes.
        var snapshot = new BattleUIRefreshSnapshot(
            refreshAll,
            new List<Character>(dirtyCharacters),
            new List<KeyValuePair<BodyPart, Character>>(dirtyParts));

        refreshAll = false;
        dirtyCharacters.Clear();
        dirtyParts.Clear();
        return snapshot;
    }
}

/// <summary>UI invalidations captured for one rendering pass.</summary>
public sealed class BattleUIRefreshSnapshot
{
    public static readonly BattleUIRefreshSnapshot Empty = new(
        false,
        new List<Character>(0),
        new List<KeyValuePair<BodyPart, Character>>(0));

    public bool RefreshAll { get; }
    public IReadOnlyList<Character> Characters { get; }
    public IReadOnlyList<KeyValuePair<BodyPart, Character>> Parts { get; }

    public BattleUIRefreshSnapshot(
        bool refreshAll,
        IReadOnlyList<Character> characters,
        IReadOnlyList<KeyValuePair<BodyPart, Character>> parts)
    {
        RefreshAll = refreshAll;
        Characters = characters;
        Parts = parts;
    }
}
