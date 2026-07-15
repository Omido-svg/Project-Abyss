using System;
using System.Collections.Generic;

public enum CharacterTargetMode
{
    Auto,
    BodyParts,
    SingleHP
}

public sealed class CharacterTargetModel
{
    private readonly ICombatTargetModel model;

    public CharacterTargetModel(
        ICombatTargetModel model)
    {
        this.model =
            model ??
            new SingleHpTargetModel(1);
    }

    public ICombatTargetModel Model => model;

    public bool UsesBodyParts =>
        model.UsesBodyParts;

    public bool RequiresTargetPart =>
        model.RequiresTargetPart;

    public int CalculateInitialHp(
        Character owner)
    {
        return model.CalculateInitialHp(owner);
    }

    public int GetMaxHp(
        Character owner)
    {
        return model.GetMaxHp(owner);
    }

    public bool TrySetMaxHpForDebug(
        Character owner,
        int maxHp)
    {
        if (model is not SingleHpTargetModel singleHp)
            return false;

        return singleHp.SetMaxHpForDebug(
            maxHp);
    }

    public bool IsValidTargetPart(
        Character owner,
        BodyPart part,
        bool allowBrokenPart)
    {
        return model.IsValidTargetPart(
            owner,
            part,
            allowBrokenPart);
    }

    public bool IsStructureDestroyed(
        Character owner)
    {
        return model.IsStructureDestroyed(owner);
    }

    public IReadOnlyList<TargetPoint> GetTargetPoints(
        Character owner,
        bool includeBrokenParts)
    {
        return model.GetTargetPoints(
            owner,
            includeBrokenParts) ??
            Array.Empty<TargetPoint>();
    }

    public static ICombatTargetModel CreateDefault(
        Character owner,
        CharacterData data)
    {
        CharacterTargetMode mode =
            data == null
                ? CharacterTargetMode.Auto
                : data.TargetMode;

        switch (mode)
        {
            case CharacterTargetMode.BodyParts:
                return new BodyPartTargetModel();

            case CharacterTargetMode.SingleHP:
                return new SingleHpTargetModel(
                    data == null
                        ? 1
                        : data.SingleHpMax);
        }

        if (owner?.BodyParts != null &&
            owner.BodyParts.Count > 0)
        {
            return new BodyPartTargetModel();
        }

        return new SingleHpTargetModel(
            data == null
                ? 1
                : data.SingleHpMax);
    }
}