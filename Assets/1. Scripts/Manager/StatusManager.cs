using System.Collections.Generic;

public class StatusManager
{
    BattleContext _battleContext;
    public StatusManager(BattleContext _battleContext)
    {
        this._battleContext = _battleContext;
    }
    
    // 상태이상 추가
    public void AddStatus(Character target, StatusEffect effect)
    {
        target.AddStatus(effect);

        effect.OnApply(target);
    }

    // 턴 시작
    public void ProcessTurnStart()
    {
        List<Character> characters = _battleContext.AllCharacters;
        
        foreach (Character character in characters)
        {
            foreach (StatusEffect effect in character.StatusEffects)
            {
                effect.OnTurnStart(character);
            }
        }
    }

    // 턴 종료
    public void ProcessTurnEnd()
    {
        List<Character> characters = _battleContext.AllCharacters;
        foreach (Character character in characters)
        {
            RemoveExpiredStatus(character);

            foreach (StatusEffect effect in character.StatusEffects)
            {
                effect.OnTurnEnd(character);

                effect.DecreaseDuration();
            }

            RemoveExpiredStatus(character);
        }
    }

    //------------------------------------------------
    private void RemoveExpiredStatus(Character character)
    {
        List<StatusEffect> removeList = new();

        foreach (StatusEffect effect in character.StatusEffects)
        {
            if (!effect.IsExpired())
                continue;

            effect.OnRemove(character);

            removeList.Add(effect);
        }

        foreach (StatusEffect effect in removeList)
        {
            character.RemoveStatus(effect);
        }
    }
}