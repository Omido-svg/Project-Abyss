public class DamageContext
{
    public BattleAction Action;

    public Character Attacker;
    public Character Target;

    public BodyPart TargetPart;

    public int RawDamage;
    public int ModifiedDamage;
    public int FinalDamage;

    public bool CanBreakPart;
    public bool IsClashDamage;
    public bool TargetLostClash;

    public bool IsPrestigeClash;
}