using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Canonical 0922/Enemy Encounter Catalog",
    fileName = "Canonical0922EnemyEncounterCatalog")]
public sealed class Canonical0922EnemyEncounterCatalog : ScriptableObject
{
    public const string ResourcesPath = "Canonical0922/EnemyEncounterCatalog";

    [Header("Normal encounter count")]
    [Range(0f, 1f)] public float ThreeEnemyProbability = 0.25f;
    [Range(0f, 1f)] public float FourEnemyProbability = 0.75f;

    [Header("Encounter-wide Duel quota")]
    [Range(0f, 1f)] public float OneDuelProbability = 0.25f;
    [Range(0f, 1f)] public float TwoDuelProbability = 0.50f;
    [Range(0f, 1f)] public float ThreeDuelProbability = 0.25f;

    [Header("Stage 1 normal enemy species counts (names/skills remain pending)")]
    [Min(0)] public int AttackerSpeciesCount = 3;
    [Min(0)] public int DuelistSpeciesCount = 3;
    [Min(0)] public int BufferSpeciesCount = 2;
    [Min(0)] public int TankSpeciesCount = 2;
    [Min(0)] public int DebufferSpeciesCount = 2;

    [Header("Canonical 20 normal encounter compositions")]
    public List<Canonical0922EnemyEncounterEntry> NormalEncounters = new();

    public void ResetToCanonical()
    {
        ThreeEnemyProbability = 0.25f;
        FourEnemyProbability = 0.75f;
        OneDuelProbability = 0.25f;
        TwoDuelProbability = 0.50f;
        ThreeDuelProbability = 0.25f;

        AttackerSpeciesCount = 3;
        DuelistSpeciesCount = 3;
        BufferSpeciesCount = 2;
        TankSpeciesCount = 2;
        DebufferSpeciesCount = 2;

        NormalEncounters ??= new List<Canonical0922EnemyEncounterEntry>();
        NormalEncounters.Clear();

        Add(4, "4-01", 0.075f, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Debuffer, EnemyRole0922.Debuffer);
        Add(4, "4-02", 0.075f, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Buffer, EnemyRole0922.Buffer);
        Add(4, "4-03", 0.075f, EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.Debuffer, EnemyRole0922.Buffer);
        Add(4, "4-04", 0.075f, EnemyRole0922.Tank, EnemyRole0922.Tank, EnemyRole0922.Debuffer, EnemyRole0922.Buffer);
        Add(4, "4-05", 0.075f, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot);
        Add(4, "4-06", 0.075f, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Buffer);
        Add(4, "4-07", 0.075f, EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Buffer);
        Add(4, "4-08", 0.075f, EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Debuffer);
        Add(4, "4-09", 0.075f, EnemyRole0922.Tank, EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot);
        Add(4, "4-10", 0.075f, EnemyRole0922.Tank, EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.Buffer);

        Add(3, "3-01", 0.025f, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot);
        Add(3, "3-02", 0.025f, EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.Buffer);
        Add(3, "3-03", 0.025f, EnemyRole0922.Tank, EnemyRole0922.Tank, EnemyRole0922.Buffer);
        Add(3, "3-04", 0.025f, EnemyRole0922.Tank, EnemyRole0922.DealerSlot, EnemyRole0922.Debuffer);
        Add(3, "3-05", 0.025f, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Debuffer);
        Add(3, "3-06", 0.025f, EnemyRole0922.DealerSlot, EnemyRole0922.DealerSlot, EnemyRole0922.Buffer);
        Add(3, "3-07", 0.025f, EnemyRole0922.Tank, EnemyRole0922.Tank, EnemyRole0922.Debuffer);
        Add(3, "3-08", 0.025f, EnemyRole0922.DealerSlot, EnemyRole0922.Debuffer, EnemyRole0922.Debuffer);
        Add(3, "3-09", 0.025f, EnemyRole0922.Tank, EnemyRole0922.Debuffer, EnemyRole0922.Debuffer);
        Add(3, "3-10", 0.025f, EnemyRole0922.Tank, EnemyRole0922.Tank, EnemyRole0922.DealerSlot);
    }

    private void Add(
        int count,
        string id,
        float probability,
        params EnemyRole0922[] roles)
    {
        NormalEncounters.Add(
            new Canonical0922EnemyEncounterEntry
            {
                Id = id,
                EnemyCount = count,
                OverallProbability = probability,
                Roles = new List<EnemyRole0922>(roles)
            });
    }
}
