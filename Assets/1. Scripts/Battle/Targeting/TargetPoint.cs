using System;

[Serializable]
public readonly struct TargetPoint
{
    public TargetPoint(
        Character character,
        BodyPart part)
    {
        Character = character;
        Part = part;
    }

    public Character Character { get; }
    public BodyPart Part { get; }

    public bool IsValid =>
        Character != null;

    public bool IsCharacterTarget =>
        Character != null &&
        Part == null;

    public bool IsBodyPartTarget =>
        Character != null &&
        Part != null;

    public static TargetPoint ForCharacter(
        Character character)
    {
        return new TargetPoint(
            character,
            null);
    }

    public static TargetPoint ForBodyPart(
        Character character,
        BodyPart part)
    {
        return new TargetPoint(
            character,
            part);
    }

    public override string ToString()
    {
        if (Character == null)
            return "NULL TARGET";

        string characterName =
            Character.Data == null
                ? Character.name
                : Character.Data.CharacterName;

        return Part == null
            ? $"{characterName} (Single HP)"
            : $"{characterName} ({Part.Type})";
    }
}
