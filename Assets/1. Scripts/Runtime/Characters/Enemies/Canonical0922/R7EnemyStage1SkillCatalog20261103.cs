using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authored 1029 stage1 enemy SKILL assets with confirmed roll counts only.
/// All cards in this catalog have stable GUIDs and no legacy EffectEntries.
/// Missing (미정) cards are NOT fabricated.
/// </summary>
public static class R7EnemyStage1SkillCatalog20261103
{
    public const string ResourcePath="ProjectAbyss/Stage1Enemy1029";
    private static Dictionary<string,SkillDefinition> cache;
    public static void ResetCache()=>cache=null;

    public static string SpeciesId(R7EnemySpecies1029 species)=>
        species switch
        {
            R7EnemySpecies1029.Charge=>"charge",
            R7EnemySpecies1029.HeavyHit=>"heavy_hit",
            R7EnemySpecies1029.Flurry=>"flurry",
            R7EnemySpecies1029.Gladiator=>"gladiator",
            R7EnemySpecies1029.Pressure=>"pressure",
            R7EnemySpecies1029.Shatter=>"shatter",
            R7EnemySpecies1029.Healer=>"healer",
            R7EnemySpecies1029.Commander=>"commander",
            R7EnemySpecies1029.HeavyGuard=>"heavy_guard",
            R7EnemySpecies1029.CounterGuard=>"counter_guard",
            R7EnemySpecies1029.Bind=>"bind",
            R7EnemySpecies1029.Corrosion=>"corrosion",
            _=>string.Empty
        };
    public static string StableId(R7EnemySpecies1029 species,
        R7EnemyAction1029 action)
    {
        string key=SpeciesId(species);
        if(string.IsNullOrEmpty(key))return string.Empty;
        string suffix=action switch
        {
            R7EnemyAction1029.Normal=>"normal",
            R7EnemyAction1029.BasicDuel=>"basic_duel",
            R7EnemyAction1029.StrongDuel=>"strong_duel",
            R7EnemyAction1029.CoverDuel=>"cover",
            R7EnemyAction1029.HealPreparation=>"heal_preparation",
            R7EnemyAction1029.SupplyPreparation=>"supply_preparation",
            _=>string.Empty
        };
        return string.IsNullOrEmpty(suffix)
            ?string.Empty:R7EnemyStage1Effect20261103.Prefix+key+"."+suffix;
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
                    !d.SkillId.StartsWith(R7EnemyStage1Effect20261103.Prefix,
                        StringComparison.Ordinal))continue;
                if(!map.TryAdd(d.SkillId,d))duplicates.Add(d.SkillId);
            }
            foreach(var id in duplicates)map.Remove(id);
            cache=map;
            return cache;
        }
    }
    public static int AuthoredCount=>Catalog.Count;

    public static SkillDefinition Get(R7EnemySpecies1029 species,
        R7EnemyAction1029 action)
    {
        var id=StableId(species,action);
        return !string.IsNullOrEmpty(id)&&Catalog.TryGetValue(id,out var d)?
            d:null;
    }
    public static bool HasAffordable(R7EnemySpecies1029 species,
        ActionType type,int cost,Func<string,bool> identity,
        int availableEnergy)
    {
        foreach(var action in new[]{
            R7EnemyAction1029.Normal,R7EnemyAction1029.BasicDuel,
            R7EnemyAction1029.StrongDuel,R7EnemyAction1029.CoverDuel,
            R7EnemyAction1029.HealPreparation,R7EnemyAction1029.SupplyPreparation})
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