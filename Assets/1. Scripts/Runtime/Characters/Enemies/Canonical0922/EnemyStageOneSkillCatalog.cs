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

    public static string SpeciesId(EnemySpecies1029 species)=>
        species switch
        {
            EnemySpecies1029.Charge=>"charge",
            EnemySpecies1029.HeavyHit=>"heavy_hit",
            EnemySpecies1029.Flurry=>"flurry",
            EnemySpecies1029.Gladiator=>"gladiator",
            EnemySpecies1029.Pressure=>"pressure",
            EnemySpecies1029.Shatter=>"shatter",
            EnemySpecies1029.Healer=>"healer",
            EnemySpecies1029.Commander=>"commander",
            EnemySpecies1029.HeavyGuard=>"heavy_guard",
            EnemySpecies1029.CounterGuard=>"counter_guard",
            EnemySpecies1029.Bind=>"bind",
            EnemySpecies1029.Corrosion=>"corrosion",
            _=>string.Empty
        };
    public static string StableId(EnemySpecies1029 species,
        EnemyAction1029 action)
    {
        string key=SpeciesId(species);
        if(string.IsNullOrEmpty(key))return string.Empty;
        string suffix=action switch
        {
            EnemyAction1029.Normal=>"normal",
            EnemyAction1029.BasicDuel=>"basic_duel",
            EnemyAction1029.StrongDuel=>"strong_duel",
            EnemyAction1029.CoverDuel=>"cover",
            EnemyAction1029.HealPreparation=>"heal_preparation",
            EnemyAction1029.SupplyPreparation=>"supply_preparation",
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

    public static SkillDefinition Get(EnemySpecies1029 species,
        EnemyAction1029 action)
    {
        var id=StableId(species,action);
        return !string.IsNullOrEmpty(id)&&Catalog.TryGetValue(id,out var d)?
            d:null;
    }
    public static bool HasAffordable(EnemySpecies1029 species,
        ActionType type,int cost,Func<string,bool> identity,
        int availableEnergy)
    {
        foreach(var action in new[]{
            EnemyAction1029.Normal,EnemyAction1029.BasicDuel,
            EnemyAction1029.StrongDuel,EnemyAction1029.CoverDuel,
            EnemyAction1029.HealPreparation,EnemyAction1029.SupplyPreparation})
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