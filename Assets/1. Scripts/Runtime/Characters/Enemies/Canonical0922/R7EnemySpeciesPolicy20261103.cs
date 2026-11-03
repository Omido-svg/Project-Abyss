using System;
using UnityEngine;

/// <summary>
/// 1029 §12.3: fixed stage-1 normal enemy species and exact stated
/// independent per-actor action-choice weights. This is a PURE policy;
/// missing card rolls/power are not invented here.
/// </summary>
public enum R7EnemySpecies1029
{
    Unknown=-1,
    Charge=0,HeavyHit=1,Flurry=2,
    Gladiator=3,Pressure=4,Shatter=5,
    Healer=6,Commander=7,HeavyGuard=8,CounterGuard=9,
    Bind=10,Corrosion=11
}
public enum R7EnemyAction1029
{
    Normal=0,BasicDuel=1,StrongDuel=2,
    HealPreparation=3,SupplyPreparation=4,CoverDuel=5
}
public static class R7EnemySpeciesPolicy20261103
{
    public readonly struct Spec
    {
        public readonly R7EnemySpecies1029 Species;
        public readonly string TemporaryName;
        public readonly EnemyRole0922 Role;
        public readonly int StrongDuelRolls;
        public readonly int[] LightOneWeights; // normal, basic, strong
        public readonly int[] LightTwoWeights;
        public readonly int PowerAdjustment;
        public Spec(R7EnemySpecies1029 species,string name,EnemyRole0922 role,
            int strongRolls,int powerAdjustment,
            int normalOne,int basicOne,int normalTwo,int basicTwo,int strongTwo)
        {
            Species=species;TemporaryName=name;Role=role;
            StrongDuelRolls=strongRolls;PowerAdjustment=powerAdjustment;
            LightOneWeights=new[]{normalOne,basicOne,0};
            LightTwoWeights=new[]{normalTwo,basicTwo,strongTwo};
        }
    }
    public static readonly Spec[] All=
    {
        new Spec(R7EnemySpecies1029.Charge,"돌격병",EnemyRole0922.Attacker,3,0,40,60,15,35,50),
        new Spec(R7EnemySpecies1029.HeavyHit,"강타병",EnemyRole0922.Attacker,2,0,50,50,20,30,50),
        new Spec(R7EnemySpecies1029.Flurry,"난타병",EnemyRole0922.Attacker,4,0,30,70,10,35,55),
        new Spec(R7EnemySpecies1029.Gladiator,"검투병",EnemyRole0922.Duelist,3,0,20,80,10,55,35),
        new Spec(R7EnemySpecies1029.Pressure,"압박병",EnemyRole0922.Duelist,3,0,35,65,15,45,40),
        new Spec(R7EnemySpecies1029.Shatter,"파쇄병",EnemyRole0922.Duelist,2,0,45,55,20,25,55),
        new Spec(R7EnemySpecies1029.Healer,"회복병",EnemyRole0922.Buffer,0,-3,50,50,50,50,0),
        new Spec(R7EnemySpecies1029.Commander,"지휘관",EnemyRole0922.Buffer,0,-2,50,50,50,50,0),
        new Spec(R7EnemySpecies1029.HeavyGuard,"중장병",EnemyRole0922.Tank,0,-1,100,0,70,0,30),
        new Spec(R7EnemySpecies1029.CounterGuard,"반격병",EnemyRole0922.Tank,0,-1,100,0,40,0,60),
        new Spec(R7EnemySpecies1029.Bind,"구속병",EnemyRole0922.Debuffer,2,-1,35,65,15,45,40),
        new Spec(R7EnemySpecies1029.Corrosion,"침식병",EnemyRole0922.Debuffer,2,-1,35,65,15,40,45)
    };
    public static bool TryGet(R7EnemySpecies1029 species,out Spec spec)
    {
        foreach(var value in All)
            if(value.Species==species){spec=value;return true;}
        spec=default;return false;
    }
    public static R7EnemySpecies1029 ChooseSpecies(EnemyRole0922 role,
        float sample01,bool commanderAlreadyPresent=false)
    {
        var r=Mathf.Clamp01(sample01);
        return role switch
        {
            EnemyRole0922.Attacker => r < 1f/3f
                ?R7EnemySpecies1029.Charge
                :r<2f/3f?R7EnemySpecies1029.HeavyHit:R7EnemySpecies1029.Flurry,
            EnemyRole0922.Duelist => r < 1f/3f
                ?R7EnemySpecies1029.Gladiator
                :r<2f/3f?R7EnemySpecies1029.Pressure:R7EnemySpecies1029.Shatter,
            EnemyRole0922.Debuffer =>r<.5f
                ?R7EnemySpecies1029.Bind:R7EnemySpecies1029.Corrosion,
            EnemyRole0922.Buffer => commanderAlreadyPresent||r<.5f
                ?R7EnemySpecies1029.Healer:R7EnemySpecies1029.Commander,
            EnemyRole0922.Tank =>r<.5f
                ?R7EnemySpecies1029.HeavyGuard:R7EnemySpecies1029.CounterGuard,
            _=>R7EnemySpecies1029.Unknown
        };
    }
    /// <summary>
    /// Pure species choice. No world RNG. Uses only confirmed percentages,
    /// and never returns unavailable light-2 actions for light-0/1.
    /// Special support actions take precedence only when stated conditions hold.
    /// </summary>
    public static R7EnemyAction1029 ChooseAction(
        R7EnemySpecies1029 species,int light,float sample01,
        bool hasCover,bool coverCandidate,
        bool allyUnderHalfHp,bool hasSupplyTarget)
    {
        if(light<0)return R7EnemyAction1029.Normal;
        switch(species)
        {
            case R7EnemySpecies1029.Healer:
                if(light>=1&&allyUnderHalfHp)
                    return R7EnemyAction1029.HealPreparation;
                return light>=1&&sample01<.5f
                    ?R7EnemyAction1029.BasicDuel:R7EnemyAction1029.Normal;
            case R7EnemySpecies1029.Commander:
                if(light>=1&&hasSupplyTarget)
                {
                    if(sample01<.60f)
                        return R7EnemyAction1029.SupplyPreparation;
                    // 40% attack tail splits into Normal/Duel equally.
                    return sample01>=.80f
                        ?R7EnemyAction1029.BasicDuel:R7EnemyAction1029.Normal;
                }
                return light>=1&&sample01<.5f
                    ?R7EnemyAction1029.BasicDuel:R7EnemyAction1029.Normal;
            case R7EnemySpecies1029.HeavyGuard:
            case R7EnemySpecies1029.CounterGuard:
                if(!hasCover&&coverCandidate&&light>=1)
                    return R7EnemyAction1029.CoverDuel;
                if(hasCover&&light>=2 &&
                   sample01<(species==R7EnemySpecies1029.HeavyGuard?.30f:.60f))
                    return R7EnemyAction1029.StrongDuel;
                return R7EnemyAction1029.Normal;
        }
        if(light==0)return R7EnemyAction1029.Normal;
        if(!TryGet(species,out var spec))return R7EnemyAction1029.Normal;
        var weights=light>=2?spec.LightTwoWeights:spec.LightOneWeights;
        float percentage=Mathf.Clamp01(sample01)*100f;
        return percentage<weights[0]?R7EnemyAction1029.Normal:
            percentage<weights[0]+weights[1]
                ?R7EnemyAction1029.BasicDuel:
                R7EnemyAction1029.StrongDuel;
    }

    /// <summary>
    /// Re-normalize among actually available authored cards. A missing
    /// strong Duel never silently selects a non-existent 2-light card.
    /// Canonical authoring can add that card without changing code.
    /// </summary>
    public static R7EnemyAction1029 ChooseAvailable(
        R7EnemySpecies1029 species,int light,float sample01,
        bool hasNormal,bool hasBasic,bool hasStrong,
        bool coverActive,bool hasCoverCard,bool hasCoverCandidate,
        bool hasHealCard,bool hasInjuredAlly,
        bool hasSupplyCard,bool hasSupplyTarget)
    {
        float r=Mathf.Clamp01(sample01);
        if(species==R7EnemySpecies1029.Healer&&light>=1&&
            hasHealCard&&hasInjuredAlly)
            return R7EnemyAction1029.HealPreparation;
        if(species==R7EnemySpecies1029.Commander&&light>=1&&
            hasSupplyCard&&hasSupplyTarget)
        {
            if(r<.6f)return R7EnemyAction1029.SupplyPreparation;
            r=Mathf.Clamp01((r-.6f)/.4f);
        }
        if((species==R7EnemySpecies1029.HeavyGuard||
            species==R7EnemySpecies1029.CounterGuard)&&
            !coverActive&&hasCoverCandidate&&hasCoverCard&&light>=1)
            return R7EnemyAction1029.CoverDuel;

        int wNormal=0,wBasic=0,wStrong=0;
        if(TryGet(species,out var spec))
        {
            var weights=light>=2?spec.LightTwoWeights:spec.LightOneWeights;
            wNormal=weights[0];wBasic=weights[1];wStrong=weights[2];
        }
        if(light==0){wNormal=100;wBasic=0;wStrong=0;}
        if(light==1)wStrong=0;
        if(!hasNormal)wNormal=0;
        if(!hasBasic)wBasic=0;
        if(!hasStrong)wStrong=0;
        var total=wNormal+wBasic+wStrong;
        if(total<=0)return hasNormal
            ?R7EnemyAction1029.Normal:
            light>=1&&hasBasic?R7EnemyAction1029.BasicDuel:
            light>=2&&hasStrong?R7EnemyAction1029.StrongDuel:
            R7EnemyAction1029.Normal;
        float draw=r*total;
        return draw<wNormal?R7EnemyAction1029.Normal:
            draw<wNormal+wBasic?R7EnemyAction1029.BasicDuel:
            R7EnemyAction1029.StrongDuel;
    }

    public static int ActionEnergyCost(R7EnemyAction1029 choice) =>
        choice switch
        {
            R7EnemyAction1029.Normal=>0,
            R7EnemyAction1029.BasicDuel=>1,
            R7EnemyAction1029.StrongDuel=>2,
            R7EnemyAction1029.CoverDuel=>1,
            R7EnemyAction1029.HealPreparation=>1,
            R7EnemyAction1029.SupplyPreparation=>1,
            _=>int.MaxValue
        };
}