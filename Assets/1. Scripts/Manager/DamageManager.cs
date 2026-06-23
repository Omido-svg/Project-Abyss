public class DamageManager
{
    private readonly BattleContext battleContext;

    public DamageManager(BattleContext battleContext)
    {
        this.battleContext = battleContext;
    }

    //------------------------------------------------
    
    // 합, 일반공격 알아서 처리
    public void ApplyDamage(BattleAction action)
    {
        float damage = CalculateDamage(action);

        action.Target.TakeDamage(
            action.TargetPart,
            (int)damage);
    }

    //------------------------------------------------

    private float CalculateDamage(BattleAction action)
    {
        float damage = 0f;

        // 공격자의 공격력
        damage += action.Owner.CurrentStatus.attackLevel;

        // 스킬 위력
        damage += action.Skill.BasePower;

        // 방어자의 방어력
        damage -= action.Target.CurrentStatus.defenseLevel;

        // 공격자의 최종 피해 증가
        damage *= action.Owner.CurrentStatus.damageMultiplier;

        // 피해 감소
        damage *= (1f - action.Target.CurrentStatus.damageReduction);

        // 최소 데미지 보정
        if (damage < 1f)
            damage = 1f;

        return damage;
    }
}