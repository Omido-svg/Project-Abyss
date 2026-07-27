public sealed class ActionSlotViewModel
{
    public ActionSlot Slot { get; private set; }

    public long ActionId { get; private set; }
    public int ActionIndex { get; private set; }

    public Character Owner { get; private set; }
    public BodyPart OwnerPart { get; private set; }

    public Character Target { get; private set; }
    public BodyPart TargetPart { get; private set; }

    public Skill Skill { get; private set; }

    public int Speed { get; private set; }
    public ActionPhase Phase { get; private set; }

    public string IndexText => $"#{ActionIndex + 1}";

    public string SkillText =>
        Skill == null
            ? "<없음>"
            : Skill.SkillName;

    public string TargetText
    {
        get
        {
            if (Target == null)
                return "대상 없음";

            string targetName =
                Target.Data == null
                    ? Target.name
                    : Target.Data.CharacterName;

            string partName =
                TargetPart == null
                    ? "SINGLE HP"
                    : TargetPart.Type.ToString();

            return $"{targetName} / {partName}";
        }
    }

    public static ActionSlotViewModel FromSlot(
        ActionSlot slot)
    {
        if (slot == null)
            return null;

        return new ActionSlotViewModel
        {
            Slot = slot,
            ActionId = slot.ActionId,
            ActionIndex = slot.ActionIndex,
            Owner = slot.Owner,
            OwnerPart = slot.Part,
            Target = slot.TargetCharacter,
            TargetPart = slot.TargetPart,
            Skill = slot.Skill,
            Speed = slot.Speed,
            Phase = slot.Phase
        };
    }
}
