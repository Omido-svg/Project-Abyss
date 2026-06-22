using System.Collections.Generic;
using UnityEngine;

public class Olaf : Character
{
    public int MadnessStack { get; private set; }
    
    private List<BodyPart> bodyParts = new()
    {
        new BodyPart(PartsType.HEAD, 7, 20),
        new BodyPart(PartsType.Body, 6, 20),
        new BodyPart(PartsType.HANDS, 5, 20),
        new BodyPart(PartsType.LEGS, 4, 20)
    };
    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;
    
    public override void Initialize(BattleEvent battleEvent)
    {
        base.Initialize(battleEvent);

        passive = new OlafPassive(this, battleEvent);
    }

    private void Awake()
    {
        CharacterName = "Olaf";

        BaseStatus = new BaseStatus(100, 1, 1);
        RuntimeStatus = new RuntimeStatus(BaseStatus);

        SkillSet = new SkillSet
        {
            NormalAttack = new OlafNormalAttack(),
            DuelSkill = new OlafDuelSkill(),
            AmbushSkill = new OlafAmbushSkill(),
            PrestigeSkill = new OlafPrestigeSkill()
        };
    }

    public void AddMadness(int amount)
    {
        MadnessStack = Mathf.Min(MadnessStack + amount, 5);
    }

    public void ResetMadness()
    {
        MadnessStack = 0;
    }

    public override void Die()
    {
        base.Die();
        Debug.Log("올라프 사망");
    }
}