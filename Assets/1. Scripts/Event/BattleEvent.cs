using System;

public class BattleEvent
{
    //-----------------------------------
    // 턴
    //-----------------------------------

    public event Action<int> OnTurnStart;
    public event Action<int> OnTurnEnd;

    public void RaiseTurnStart(int turn)
    {
        OnTurnStart?.Invoke(turn);
    }

    public void RaiseTurnEnd(int turn)
    {
        OnTurnEnd?.Invoke(turn);
    }

    //-----------------------------------
    // 행동
    //-----------------------------------

    public event Action<BattleAction> OnActionStart;
    public event Action<BattleAction> OnActionEnd;

    public void RaiseActionStart(BattleAction action)
    {
        OnActionStart?.Invoke(action);
    }

    public void RaiseActionEnd(BattleAction action)
    {
        OnActionEnd?.Invoke(action);
    }

    //-----------------------------------
    // 합
    //-----------------------------------

    public event Action<Character, Character> OnClashStart;
    public event Action<BattleAction, BattleAction> OnClashWin;
    public event Action<BattleAction, BattleAction> OnClashLose;

    public void RaiseClashStart(Character attacker, Character defender)
    {
        OnClashStart?.Invoke(attacker, defender);
    }

    public void RaiseClashWin(BattleAction winnerAction, BattleAction loserAction)
    {
        OnClashWin?.Invoke(winnerAction, loserAction);
    }

    public void RaiseClashLose(BattleAction loserAction, BattleAction winnerAction)
    {
        OnClashLose?.Invoke(loserAction, winnerAction);
    }

    //-----------------------------------
    // 데미지
    //-----------------------------------

    public event Action<Character, int> OnDamageTaken;
    public event Action<Character, int> OnDamageDealt;

    public void RaiseDamageTaken(Character target, int damage)
    {
        OnDamageTaken?.Invoke(target, damage);
    }

    public void RaiseDamageDealt(Character attacker, int damage)
    {
        OnDamageDealt?.Invoke(attacker, damage);
    }

    //-----------------------------------
    // 상태이상
    //-----------------------------------

    public event Action<Character, StatusEffect> OnStatusApplied;
    public event Action<Character, StatusEffect> OnStatusRemoved;

    public void RaiseStatusApplied(Character target, StatusEffect effect)
    {
        OnStatusApplied?.Invoke(target, effect);
    }

    public void RaiseStatusRemoved(Character target, StatusEffect effect)
    {
        OnStatusRemoved?.Invoke(target, effect);
    }

    //-----------------------------------
    // 부위
    //-----------------------------------

    public event Action<Character, BodyPart> OnBodyPartDestroyed;
    public event Action<Character, BodyPart> OnBodyPartRecovered;

    public void RaiseBodyPartDestroyed(Character target, BodyPart part)
    {
        OnBodyPartDestroyed?.Invoke(target, part);
    }

    public void RaiseBodyPartRecovered(Character target, BodyPart part)
    {
        OnBodyPartRecovered?.Invoke(target, part);
    }

    //-----------------------------------
    // 사망
    //-----------------------------------

    public event Action<Character> OnCharacterDeath;

    public void RaiseCharacterDeath(Character target)
    {
        OnCharacterDeath?.Invoke(target);
    }

    //-----------------------------------
    // 처치
    //-----------------------------------

    public event Action<Character, Character> OnKill;

    public void RaiseKill(Character killer, Character victim)
    {
        OnKill?.Invoke(killer, victim);
    }
}