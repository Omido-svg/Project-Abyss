using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authored 1029 stage1 enemy SKILL assets with confirmed roll counts only.
/// All cards in this catalog have stable GUIDs and no legacy EffectEntries.
/// Missing (미정) cards are NOT fabricated.
/// </summary>
public static class EnemyStageOneSkillCatalog
{
    public const string ResourcePath="ProjectAbyss/Stage1Enemy1029";
    private static Dictionary<string,SkillDefinition> cache;
    public static void ResetCache()=>cache=null;

    public static string SpeciesId(EnemySpecies species)=>
        species switch
        {
            EnemySpecies.Charge=>"charge",
            EnemySpecies.HeavyHit=>"heavy_hit",
            EnemySpecies.Flurry=>"flurry",
            EnemySpecies.Gladiator=>"gladiator",
            EnemySpecies.Pressure=>"pressure",
            EnemySpecies.Shatter=>"shatter",
            EnemySpecies.Healer=>"healer",
            EnemySpecies.Commander=>"commander",
            EnemySpecies.HeavyGuard=>"heavy_guard",
            EnemySpecies.CounterGuard=>"counter_guard",
            EnemySpecies.Bind=>"bind",
            EnemySpecies.Corrosion=>"corrosion",
            _=>string.Empty
        };
    public static string StableId(EnemySpecies species,
        EnemyActionPattern action)
    {
        string key=SpeciesId(species);
        if(string.IsNullOrEmpty(key))return string.Empty;
        string suffix=action switch
        {
            EnemyActionPattern.Normal=>"normal",
            EnemyActionPattern.BasicDuel=>"basic_duel",
            EnemyActionPattern.StrongDuel=>"strong_duel",
            EnemyActionPattern.CoverDuel=>"cover",
            EnemyActionPattern.HealPreparation=>"heal_preparation",
            EnemyActionPattern.SupplyPreparation=>"supply_preparation",
            _=>string.Empty
        };
        return string.IsNullOrEmpty(suffix)
            ?string.Empty:EnemyStageOneEffect.Prefix+key+"."+suffix;
    }
    private static Dictionary<string,SkillDefinition> Catalog
    {
        get
        {
            if(cache!=null)return cache;
            var map=new Dictionary<string,SkillDefinition>(StringComparer.Ordinal);
            var duplicates=new HashSet<string>(StringComparer.Ordinal);
            foreach(var d in Resources.LoadAll<SkillDefinition>(ResourcePath))
            {
                if(d==null||d.SkillId==null||
                    !d.SkillId.StartsWith(EnemyStageOneEffect.Prefix,
                        StringComparison.Ordinal))continue;
                if(!map.TryAdd(d.SkillId,d))duplicates.Add(d.SkillId);
            }
            foreach(var id in duplicates)map.Remove(id);
            cache=map;
            return cache;
        }
    }
    public static int AuthoredCount=>Catalog.Count;

    public static SkillDefinition Get(EnemySpecies species,
        EnemyActionPattern action)
    {
        var id=StableId(species,action);
        return !string.IsNullOrEmpty(id)&&Catalog.TryGetValue(id,out var d)?
            d:null;
    }
    public static bool HasAffordable(EnemySpecies species,
        ActionType type,int cost,Func<string,bool> identity,
        int availableEnergy)
    {
        foreach(var action in new[]{
            EnemyActionPattern.Normal,EnemyActionPattern.BasicDuel,
            EnemyActionPattern.StrongDuel,EnemyActionPattern.CoverDuel,
            EnemyActionPattern.HealPreparation,EnemyActionPattern.SupplyPreparation})
        {
            var d=Get(species,action);
            if(d==null||d.ActionType!=type||!d.OverrideEnergyCost||
               d.EnergyCost!=cost||d.EnergyCost>availableEnergy||
               identity!=null&&!identity(d.SkillId))continue;
            return true;
        }
        return false;
    }
}