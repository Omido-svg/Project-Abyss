using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpeedManager
{
    private readonly BattleContext battleContext;

    public SpeedManager(BattleContext battleContext)
    {
        this.battleContext = battleContext;
    }

    // 턴 시작 시 모든 부위의 속도 굴림
    public void RollAllSpeed()
    {
        foreach (Character character in battleContext.AllCharacters)
        {
            RollCharacterSpeed(character);
        }
    }

    private void RollCharacterSpeed(Character character)
    {
        List<int> speeds = new();

        foreach (BodyPart part in character.BodyParts)
        {
            if (part.IsBroken)
                continue;

            speeds.Add(Random.Range(
                character.CurrentStatus.minSpeed,
                character.CurrentStatus.maxSpeed + 1));
        }

        speeds.Sort((a, b) => b.CompareTo(a));

        int index = 0;

        foreach (BodyPart part in character.BodyParts)
        {
            if (part.IsBroken)
            {
                part.CurrentSpeed = 0;
                continue;
            }

            part.CurrentSpeed = speeds[index++];
        }
    }
}