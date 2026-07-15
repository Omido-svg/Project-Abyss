using System.Collections.Generic;

public interface ICombatTargetModel
{
    bool UsesBodyParts { get; }
    bool RequiresTargetPart { get; }

    int CalculateInitialHp(Character owner);
    int GetMaxHp(Character owner);

    bool IsValidTargetPart(
        Character owner,
        BodyPart part,
        bool allowBrokenPart);

    bool IsStructureDestroyed(Character owner);

    IReadOnlyList<TargetPoint> GetTargetPoints(
        Character owner,
        bool includeBrokenParts);
}
