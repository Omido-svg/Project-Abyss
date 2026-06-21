public class OlafPassive : Passive
{
    private int madnessStack;

    public OlafPassive(Character owner) : base(owner)
    {
        passiveName = "광기";
    }

    public override void Register()
    {
        // BattleEvent.OnClashWin += OnClashWin;
        // BattleEvent.OnBodyPartDestroyed += OnBodyDestroyed;
        // BattleEvent.OnKill += OnKill;
    }

    public override void Unregister()
    {
        // BattleEvent.OnClashWin -= OnClashWin;
        // BattleEvent.OnBodyPartDestroyed -= OnBodyDestroyed;
        // BattleEvent.OnKill -= OnKill;
    }

    private void OnClashWin(Character winner, Character loser)
    {
        if (winner != owner) return;

        // 출혈 부여
    }

    private void OnBodyDestroyed(Character target, BodyPart part)
    {
        if (target != owner) return;

        madnessStack++;
    }

    private void OnKill(Character killer, Character victim)
    {
        if (killer != owner) return;

        // 부위 회복
    }
}