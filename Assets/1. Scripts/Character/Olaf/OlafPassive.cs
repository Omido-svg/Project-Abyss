public class OlafPassive : Passive
{
    private int madnessStack;

    public OlafPassive(Character owner, BattleEvent battleEvent)
    {
        Initialize(owner, battleEvent);

        passiveName = "광기";

        Register();
    }

    public override void Register()
    {
        battleEvent.OnClashWin += OnClashWin;
        battleEvent.OnBodyPartDestroyed += OnBodyDestroyed;
        battleEvent.OnKill += OnKill;
    }

    public override void Unregister()
    {
        battleEvent.OnClashWin -= OnClashWin;
        battleEvent.OnBodyPartDestroyed -= OnBodyDestroyed;
        battleEvent.OnKill -= OnKill;
    }

    private void OnClashWin(Character winner, Character loser)
    {
        if (winner != owner) return;
        
        // 출혈 부여
        loser.AddStatus(new Bleeding(3), owner);
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
        owner.RecoverBrokenParts(1);
    }
}