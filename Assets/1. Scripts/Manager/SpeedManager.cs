using System.Collections.Generic;

public class SpeedManager
{

    private readonly BattleContext battleContext;

    public SpeedManager(BattleContext battleContext)
    {
        this.battleContext = battleContext;
    }

    //------------------------------------------------

    /// 턴 시작 시 모든 부위의 속도 굴림
    public void RollAllSpeed()
    {
        foreach (Character character in battleContext.AllCharacters)
        {
            RollCharacterSpeed(character);
        }
    }

    //------------------------------------------------

    private void RollCharacterSpeed(Character character)
    {
        foreach (BodyPart part in character.BodyParts)
        {
            if (part.IsBroken)
                continue;

            part.RollSpeed();
        }
    }
}