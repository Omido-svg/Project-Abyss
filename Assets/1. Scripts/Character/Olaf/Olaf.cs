using System.Collections.Generic;
using UnityEngine;

public class Olaf : Character
{
    public int MadnessStack { get; private set; }

    //------------------------------------------------

    private readonly List<BodyPart> bodyParts = new()
    {
        new BodyPart(PartType.HEAD, 6, 10, 20),
        new BodyPart(PartType.LEFT_HAND, 4, 8, 20),
        new BodyPart(PartType.RIGHT_HAND, 4, 8, 20),
        new BodyPart(PartType.LEGS, 3, 7, 20)
    };

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;

    //------------------------------------------------

    private void Awake()
    {
        SkillPool = new List<Skill>()
        {
            new OlafNormalAttack(),
            new OlafDuelSkill(),
            new OlafAmbushSkill(),
            new OlafPrestigeSkill()
        };
    }

    //------------------------------------------------
    // 상태 초기화
    public override void Initialize(BattleEvent battleEvent)
    {
        base.Initialize(battleEvent);

        passive = new OlafPassive(this, battleEvent);
    }

    //------------------------------------------------

    public void AddMadness(int amount)
    {
        MadnessStack = Mathf.Min(MadnessStack + amount, 5);
    }

    public void ResetMadness()
    {
        MadnessStack = 0;
    }

    //------------------------------------------------

    public override void Die()
    {
        base.Die();

        Debug.Log($"{Data.CharacterName} 사망");
    }
}