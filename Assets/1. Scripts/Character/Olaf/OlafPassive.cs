using UnityEngine;

public class OlafPassive : Passive
{
    private int MadnessStack;

    public OlafPassive(Character owner, BattleEvent battleEvent)
    {
        Initialize(owner, battleEvent);

        passiveName = "광기(패시브)";

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

    private void OnClashWin(
        BattleAction winnerAction,
        BattleAction loserAction)
    {
        if (winnerAction.Owner != owner)
            return;

        loserAction.TargetPart.AddStatus(new Bleeding(), owner);
    }

    private void OnBodyDestroyed(Character target, BodyPart part)
    {
        if (target != owner) return;

        MadnessStack++;
    }

    private void OnKill(Character killer, Character victim)
    {
        if (killer != owner) return;
        
    }
    
    public void AddMadness(int amount)
    {
       MadnessStack += Mathf.Min(MadnessStack + amount, 5);
    }

    public void ResetMadness()
    {
        MadnessStack = 0;
    }
}