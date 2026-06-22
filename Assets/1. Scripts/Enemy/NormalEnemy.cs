using System.Collections.Generic;
using UnityEngine;

public class NormalEnemy : Character
{
    private List<BodyPart> bodyParts = new()
    {
        new BodyPart(PartsType.Body, 6, 20)
    };

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;

    //--------------------------------

    public override void Initialize(BattleEvent battleEvent)
    {
        base.Initialize(battleEvent);

        // 일반 몬스터는 패시브가 없다고 가정
        passive = null;
    }

    //--------------------------------

    private void Awake()
    {
        CharacterName = "Normal Enemy";

        BaseStatus = new BaseStatus(
            maxHP: 50,
            attackLevel: 1,
            defenseLevel: 1);

        RuntimeStatus = new RuntimeStatus(BaseStatus);

        SkillSet = new SkillSet()
        {
            NormalAttack = new EnemyNormalAttack(),
            DuelSkill = new EnemyDuelSkill(),
            AmbushSkill = null,
            PrestigeSkill = null
        };
    }

    //--------------------------------

    public override void Die()
    {
        base.Die();

        Debug.Log($"{CharacterName} 사망");
    }
}