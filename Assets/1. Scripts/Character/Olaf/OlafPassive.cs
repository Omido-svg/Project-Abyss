using UnityEngine;

public class OlafPassive : Passive
{
    private int madness;

    public int Madness => madness;

    public int MaxMadness => 3;

    public OlafPassive()
    {
        PassiveName = "광전사의 광기";
    }

    protected override void OnRegister()
    {
        battleEvent.OnClashWin += OnClashWin;
        battleEvent.OnDamageDealt += OnDamageDealt;
    }

    protected override void OnUnregister()
    {
        battleEvent.OnClashWin -= OnClashWin;
        battleEvent.OnDamageDealt -= OnDamageDealt;
    }

    private void OnClashWin(
        BattleAction winnerAction,
        BattleAction loserAction)
    {
        if (winnerAction == null)
            return;

        if (winnerAction.Owner != owner)
            return;

        AddMadness(1);
    }

    private void OnDamageDealt(
        Character attacker,
        int damage)
    {
        if (attacker != owner)
            return;

        // 필요하면 피해를 줄 때마다 광기 증가
        // AddMadness(1);
    }

    public void AddMadness(int amount)
    {
        madness += amount;

        if (madness > MaxMadness)
            madness = MaxMadness;

        Debug.Log($"{owner.Data.CharacterName} 광기 증가 : {madness}/{MaxMadness}");
    }

    public void ResetMadness()
    {
        madness = 0;

        Debug.Log($"{owner.Data.CharacterName} 광기 초기화");
    }

    public bool IsMaxMadness()
    {
        return madness >= MaxMadness;
    }
}