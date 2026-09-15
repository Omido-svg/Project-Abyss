using System;
using System.Runtime.CompilerServices;

/// <summary>
/// 0915 C-15: 상태 저장 지점을 Character 또는 살아있는 BodyPart로 통일한다.
/// 부위가 없는 일반 몹은 항상 Character anchor이며 약화/파괴 조건은 false다.
/// </summary>
public readonly struct CombatStatusAnchor : IEquatable<CombatStatusAnchor>
{
    public Character Character { get; }
    public BodyPart Part { get; }

    public bool IsValid => Character != null;
    public bool IsPartAnchor => Part != null;
    public bool IsWeakened => Part?.IsWeakened == true;
    public bool IsBroken => Part?.IsBroken == true;

    /// <summary>
    /// C-15: 상태 피해가 저장된 anchor의 형태와 동일한 HP route를 사용한다.
    /// BodyPart anchor는 StatusPart, Character anchor는 Direct.
    /// Character anchor에서는 weaken/break semantics가 발생하지 않는다.
    /// </summary>
    public DamageType StatusDamageType =>
        IsPartAnchor
            ? DamageType.StatusPart
            : DamageType.Direct;

    private CombatStatusAnchor(Character character, BodyPart part)
    {
        Character = character;
        Part = part;
    }

    public static CombatStatusAnchor Resolve(
        Character character,
        BodyPart requestedPart)
    {
        if (character == null)
            return new CombatStatusAnchor(null, null);

        if (requestedPart == null ||
            requestedPart.IsBroken ||
            (requestedPart.Owner != null && requestedPart.Owner != character))
        {
            return new CombatStatusAnchor(character, null);
        }

        return new CombatStatusAnchor(character, requestedPart);
    }

    public bool Equals(CombatStatusAnchor other) =>
        ReferenceEquals(Character, other.Character) &&
        ReferenceEquals(Part, other.Part);

    public override bool Equals(object obj) =>
        obj is CombatStatusAnchor other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int characterHash = Character == null
                ? 0
                : RuntimeHelpers.GetHashCode(Character);
            int partHash = Part == null
                ? 0
                : RuntimeHelpers.GetHashCode(Part);
            return (characterHash * 397) ^ partHash;
        }
    }

    public static bool operator ==(
        CombatStatusAnchor left,
        CombatStatusAnchor right) =>
        left.Equals(right);

    public static bool operator !=(
        CombatStatusAnchor left,
        CombatStatusAnchor right) =>
        !left.Equals(right);
}
