public class OlafAmbushSkill : Skill
{
    public OlafAmbushSkill()
    {
        SkillName = "광기의 난도질";

        SkillType = SkillType.AMBUSH;

        BasePower = 12;

        Resolver = new CoinResolver(owner, 2);
    }

    public override void Execute(BattleAction action)
    {
        action.Target.TakeDamage(
            action.TargetPart,
            Roll());

        // 자신의 랜덤 부위 하나 파괴
    }
}