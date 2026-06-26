public class OlafAmbushSkill : AmbushSkill
{
    public OlafAmbushSkill()
    {
        SkillName = "광기의 난도질(도사림)";

        BasePower = 12;

        Resolver = new CoinResolver(2);
    }

    public override void Execute(BattleAction action)
    {
        action.Target.TakeDamage(
            action.TargetPart,
            action.RollPower());

        // 자신의 랜덤 부위 하나 파괴
    }
}