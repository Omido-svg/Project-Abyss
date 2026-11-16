using ProjectAbyss.Abilities;

public readonly struct YujinMarkGainPolicy
{
    public readonly bool PreserveOverflow, RepeatIgnition;
    public readonly int ReapplyAfterBurst;
    public YujinMarkGainPolicy(bool preserveOverflow,bool repeatIgnition,int reapplyAfterBurst)
    {PreserveOverflow=preserveOverflow;RepeatIgnition=repeatIgnition;ReapplyAfterBurst=System.Math.Max(0,reapplyAfterBurst);}
}
/// <summary>Frozen ignition outcome policy. A repeated result reuses its weapon/enhancement and does not consume traps again.</summary>
public readonly struct YujinIgnitionProfile
{
    public readonly YujinWeaponType Weapon;
    public readonly bool Enhanced;
    public readonly int StaggerDamage, HpDamage, Weakness;
    public YujinIgnitionProfile(YujinWeaponType weapon,bool enhanced,YujinMarkRuleSettings rules)
    {
        Weapon=weapon;Enhanced=enhanced;StaggerDamage=rules.BaekuIgnitionStagger;
        HpDamage=rules.NakilIgnitionDamage>0?rules.NakilIgnitionDamage+(enhanced?rules.ResonanceNakilExtraDamage:0):0;
        Weakness=enhanced?rules.ResonanceWeakness:0;
    }
}
public readonly struct YujinMarkBurstResult
{
    public readonly EffectCause Cause, GainCause;
    public readonly CombatTargetRef Target;
    public readonly YujinIgnitionProfile Profile;
    public YujinMarkBurstResult(EffectCause cause,EffectCause gain,CombatTargetRef target,YujinIgnitionProfile profile)
    {Cause=cause;GainCause=gain;Target=target;Profile=profile;}
}
