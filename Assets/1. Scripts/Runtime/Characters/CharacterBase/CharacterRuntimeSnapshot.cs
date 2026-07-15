using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BodyPartRuntimeSnapshot
{
    public PartType PartType;
    public int CurrentHP;
    public int MaxHP;
    public BodyPartState State;
    public int StatusCount;

    public static BodyPartRuntimeSnapshot Capture(
        BodyPart part)
    {
        if (part == null)
            return null;

        return new BodyPartRuntimeSnapshot
        {
            PartType = part.Type,
            CurrentHP = Mathf.Max(
                0,
                Mathf.RoundToInt(part.PartHP)),
            MaxHP = Mathf.Max(
                0,
                Mathf.RoundToInt(part.MaxPartHP)),
            State = part.State,
            StatusCount =
                part.StatusEffects?.Count ?? 0
        };
    }
}

[Serializable]
public sealed class CharacterRuntimeSnapshot
{
    public string CharacterName;
    public int InitializationVersion;

    public bool UsesBodyParts;
    public bool IsDead;

    public int CurrentHP;
    public int MaxHP;
    public int CurrentBlock;
    public int CurrentPrestige;

    public int CharacterStatusCount;

    public Dictionary<string, int> CustomResources = new();
    public List<BodyPartRuntimeSnapshot> BodyParts = new();

    public static CharacterRuntimeSnapshot Capture(
        Character character)
    {
        if (character == null)
            return null;

        CharacterRuntimeSnapshot result = new()
        {
            CharacterName =
                character.Data?.CharacterName ??
                character.name,
            InitializationVersion =
                character.CombatState?
                    .InitializationVersion ?? 0,
            UsesBodyParts = character.UsesBodyParts,
            IsDead = character.IsDead,
            CurrentHP = character.CurrentHP,
            MaxHP = character.MaxCombatHP,
            CurrentBlock =
                character.RuntimeStatus?
                    .currentBlock ?? 0,
            CurrentPrestige =
                character.RuntimeStatus?
                    .currentPrestige ?? 0,
            CharacterStatusCount =
                character.StatusEffects?.Count ?? 0,
            CustomResources =
                character.Resources?
                    .CaptureValues() ??
                new Dictionary<string, int>()
        };

        if (character.BodyParts == null)
            return result;

        foreach (BodyPart part in character.BodyParts)
        {
            BodyPartRuntimeSnapshot snapshot =
                BodyPartRuntimeSnapshot.Capture(part);

            if (snapshot != null)
                result.BodyParts.Add(snapshot);
        }

        return result;
    }
}
