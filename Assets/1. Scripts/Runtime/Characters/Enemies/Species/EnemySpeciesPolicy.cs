using System;
using UnityEngine;

/// <summary>
/// 1029 §12.3: fixed stage-1 normal enemy species and exact stated
/// independent per-actor action-choice weights. This is a PURE policy;
/// missing card rolls/power are not invented here.
/// </summary>
public enum EnemySpecies
{
    Unknown=-1,
    Charge=0,HeavyHit=1,Flurry=2,
    Gladiator=3,Pressure=4,Shatter=5,
    Healer=6,Commander=7,HeavyGuard=8,CounterGuard=9,
    Bind=10,Corrosion=11
}
public enum EnemyActionPattern
{
    Normal=0,BasicDuel=1,StrongDuel=2,
    HealPreparation=3,SupplyPreparation=4,CoverDuel=5
}
public static class EnemySpeciesPolicy
{
    public readonly struct Spec
    {
        public readonly EnemySpecies Species;
        public readonly string TemporaryName;
        public readonly EnemyRole Role;
        public readonly int StrongDuelRolls;
        public readonly int[] LightOneWeights; // normal, basic, strong
        public readonly int[] LightTwoWeights;
        public readonly int PowerAdjustment;
        public Spec(EnemySpecies species,string name,EnemyRole role,
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
        new Spec(EnemySpecies.Charge,"돌격병",EnemyRole.Attacker,3,0,40,60,15,35,50),
        new Spec(EnemySpecies.HeavyHit,"강타병",EnemyRole.Attacker,2,0,50,50,20,30,50),
        new Spec(EnemySpecies.Flurry,"난타병",EnemyRole.Attacker,4,0,30,70,10,35,55),
        new Spec(EnemySpecies.Gladiator,"검투병",EnemyRole.Duelist,3,0,20,80,10,55,35),
        new Spec(EnemySpecies.Pressure,"압박병",EnemyRole.Duelist,3,0,35,65,15,45,40),
        new Spec(EnemySpecies.Shatter,"파쇄병",EnemyRole.Duelist,2,0,45,55,20,25,55),
        new Spec(EnemySpecies.Healer,"회복병",EnemyRole.Buffer,0,-3,50,50,50,50,0),
        new Spec(EnemySpecies.Commander,"지휘관",EnemyRole.Buffer,0,-2,50,50,50,50,0),
        new Spec(EnemySpecies.HeavyGuard,"중장병",EnemyRole.Tank,0,-1,100,0,70,0,30),
        new Spec(EnemySpecies.CounterGuard,"반격병",EnemyRole.Tank,0,-1,100,0,40,0,60),
        new Spec(EnemySpecies.Bind,"구속병",EnemyRole.Debuffer,2,-1,35,65,15,45,40),
        new Spec(EnemySpecies.Corrosion,"침식병",EnemyRole.Debuffer,2,-1,35,65,15,40,45)
    };
    public static bool TryGet(EnemySpecies species,out Spec spec)
    {
        foreach(var value in All)
            if(value.Species==species){spec=value;return true;}
        spec=default;return false;
    }
    public static EnemySpecies ChooseSpecies(EnemyRole role,
        float sample01,bool commanderAlreadyPresent=false)
    {
        var r=Mathf.Clamp01(sample01);
        return role switch
        {
            EnemyRole.Attacker => r < 1f/3f
                ?EnemySpecies.Charge
                :r<2f/3f?EnemySpecies.HeavyHit:EnemySpecies.Flurry,
            EnemyRole.Duelist => r < 1f/3f
                ?EnemySpecies.Gladiator
                :r<2f/3f?EnemySpecies.Pressure:EnemySpecies.Shatter,
            EnemyRole.Debuffer =>r<.5f
                ?EnemySpecies.Bind:EnemySpecies.Corrosion,
            EnemyRole.Buffer => commanderAlreadyPresent||r<.5f
                ?EnemySpecies.Healer:EnemySpecies.Commander,
            EnemyRole.Tank =>r<.5f
                ?EnemySpecies.HeavyGuard:EnemySpecies.CounterGuard,
            _=>EnemySpecies.Unknown
        };
    }
    /// <summary>
    /// Pure species choice. No world RNG. Uses only confirmed percentages,
    /// and never returns unavailable light-2 actions for light-0/1.
    /// Special support actions take precedence only when stated conditions hold.
    /// </summary>
    public static EnemyActionPattern ChooseAction(
        EnemySpecies species,int light,float sample01,
        bool hasCover,bool coverCandidate,
        bool allyUnderHalfHp,bool hasSupplyTarget)
    {
        if(light<0)return EnemyActionPattern.Normal;
        switch(species)
        {
            case EnemySpecies.Healer:
                if(light>=1&&allyUnderHalfHp)
                    return EnemyActionPattern.HealPreparation;
                return light>=1&&sample01<.5f
                    ?EnemyActionPattern.BasicDuel:EnemyActionPattern.Normal;
            case EnemySpecies.Commander:
                if(light>=1&&hasSupplyTarget)
                {
                    if(sample01<.60f)
                        return EnemyActionPattern.SupplyPreparation;
                    // 40% attack tail splits into Normal/Duel equally.
                    return sample01>=.80f
                        ?EnemyActionPattern.BasicDuel:EnemyActionPattern.Normal;
                }
                return light>=1&&sample01<.5f
                    ?EnemyActionPattern.BasicDuel:EnemyActionPattern.Normal;
            case EnemySpecies.HeavyGuard:
            case EnemySpecies.CounterGuard:
                if(!hasCover&&coverCandidate&&light>=1)
                    return EnemyActionPattern.CoverDuel;
                if(hasCover&&light>=2 &&
                   sample01<(species==EnemySpecies.HeavyGuard?.30f:.60f))
                    return EnemyActionPattern.StrongDuel;
                return EnemyActionPattern.Normal;
        }
        if(light==0)return EnemyActionPattern.Normal;
        if(!TryGet(species,out var spec))return EnemyActionPattern.Normal;
        var weights=light>=2?spec.LightTwoWeights:spec.LightOneWeights;
        float percentage=Mathf.Clamp01(sample01)*100f;
        return percentage<weights[0]?EnemyActionPattern.Normal:
            percentage<weights[0]+weights[1]
                ?EnemyActionPattern.BasicDuel:
                EnemyActionPattern.StrongDuel;
    }

    /// <summary>
    /// Re-normalize among actually available authored cards. A missing
    /// strong Duel never silently selects a non-existent 2-light card.
    /// Canonical authoring can add that card without changing code.
    /// </summary>
    public static EnemyActionPattern ChooseAvailable(
        EnemySpecies species,int light,float sample01,
        bool hasNormal,bool hasBasic,bool hasStrong,
        bool coverActive,bool hasCoverCard,bool hasCoverCandidate,
        bool hasHealCard,bool hasInjuredAlly,
        bool hasSupplyCard,bool hasSupplyTarget)
    {
        float r=Mathf.Clamp01(sample01);
        if(species==EnemySpecies.Healer&&light>=1&&
            hasHealCard&&hasInjuredAlly)
            return EnemyActionPattern.HealPreparation;
        if(species==EnemySpecies.Commander&&light>=1&&
            hasSupplyCard&&hasSupplyTarget)
        {
            if(r<.6f)return EnemyActionPattern.SupplyPreparation;
            r=Mathf.Clamp01((r-.6f)/.4f);
        }
        if((species==EnemySpecies.HeavyGuard||
            species==EnemySpecies.CounterGuard)&&
            !coverActive&&hasCoverCandidate&&hasCoverCard&&light>=1)
            return EnemyActionPattern.CoverDuel;

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
            ?EnemyActionPattern.Normal:
            light>=1&&hasBasic?EnemyActionPattern.BasicDuel:
            light>=2&&hasStrong?EnemyActionPattern.StrongDuel:
            EnemyActionPattern.Normal;
        float draw=r*total;
        return draw<wNormal?EnemyActionPattern.Normal:
            draw<wNormal+wBasic?EnemyActionPattern.BasicDuel:
            EnemyActionPattern.StrongDuel;
    }

    public static int ActionEnergyCost(EnemyActionPattern choice) =>
        choice switch
        {
            EnemyActionPattern.Normal=>0,
            EnemyActionPattern.BasicDuel=>1,
            EnemyActionPattern.StrongDuel=>2,
            EnemyActionPattern.CoverDuel=>1,
            EnemyActionPattern.HealPreparation=>1,
            EnemyActionPattern.SupplyPreparation=>1,
            _=>int.MaxValue
        };
}