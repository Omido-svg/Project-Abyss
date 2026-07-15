using System.Collections.Generic;
using UnityEngine;

public sealed class BattleTargetButtonRegistry : MonoBehaviour
{
    [SerializeField]
    private BodyPartButtonRegistry bodyPartButtonRegistry;

    private readonly List<BodyPartButton> scratch = new();

    public BodyPartButtonRegistry BodyPartButtons =>
        bodyPartButtonRegistry;

    private void Awake()
    {
        EnsureRegistry();
    }

    public BodyPartButton FindButton(
        Character character,
        BodyPart part,
        bool requireActive = true)
    {
        EnsureRegistry();

        return bodyPartButtonRegistry?.Find(
            character,
            part,
            requireActive);
    }

    public void CopyValidTargetButtonsTo(
        Character target,
        TargetSelectionRule rule,
        List<BodyPartButton> destination)
    {
        if (destination == null)
            return;

        destination.Clear();

        EnsureRegistry();

        if (bodyPartButtonRegistry == null ||
            target == null ||
            target.IsDead)
        {
            return;
        }

        bodyPartButtonRegistry.CopyCharacterButtonsTo(
            target,
            scratch,
            requireActive: true);

        foreach (BodyPartButton button in scratch)
        {
            if (button == null)
                continue;

            if (!BattleTargetValidator.IsValid(
                    target,
                    button.BodyPart,
                    rule))
            {
                continue;
            }

            destination.Add(button);
        }
    }

    private void EnsureRegistry()
    {
        if (bodyPartButtonRegistry == null)
        {
            bodyPartButtonRegistry =
                FindFirstObjectByType<BodyPartButtonRegistry>();
        }
    }
}
