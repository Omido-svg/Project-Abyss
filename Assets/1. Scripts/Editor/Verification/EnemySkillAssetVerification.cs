#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// R7-8: creates ONLY 18 stage1 skills whose dice/cost are explicitly fixed
/// in 1029 §12.3. Does not invent missing normal/basic strong rolls.
/// Native effects are bound to exact stable ids and owner species.
/// </summary>
public static class EnemySkillAssetVerification
{
    private const string Root="Assets/2. Data/Resources/ProjectAbyss/Stage1Enemy1029";

    private readonly struct Entry
    {
        public readonly EnemySpecies Species;
        public readonly EnemyActionPattern Action;
        public readonly int Rolls;
        public readonly string Display;
        public readonly bool Break;
        public Entry(EnemySpecies species,EnemyActionPattern action,
            int rolls,string display,bool canBreak=false)
        {
            Species=species;Action=action;Rolls=rolls;
            Display=display;Break=canBreak;
        }
    }
    private static readonly Entry[] Confirmed =
    {
        new(EnemySpecies.Charge,EnemyActionPattern.StrongDuel,3,"돌격병 강한 결투"),
        new(EnemySpecies.HeavyHit,EnemyActionPattern.StrongDuel,2,"강타병 강한 결투"),
        new(EnemySpecies.Flurry,EnemyActionPattern.StrongDuel,4,"난타병 강한 결투"),
        new(EnemySpecies.Gladiator,EnemyActionPattern.BasicDuel,3,"검투병 기본 결투"),
        new(EnemySpecies.Gladiator,EnemyActionPattern.StrongDuel,3,"검투병 강한 결투"),
        new(EnemySpecies.Pressure,EnemyActionPattern.StrongDuel,3,"압박병 강한 압박 결투"),
        new(EnemySpecies.Shatter,EnemyActionPattern.BasicDuel,2,"파쇄병 기본 결투"),
        new(EnemySpecies.Shatter,EnemyActionPattern.StrongDuel,2,"파쇄병 파쇄 결투",true),
        new(EnemySpecies.Healer,EnemyActionPattern.Normal,2,"회복병 일반 공격"),
        new(EnemySpecies.Healer,EnemyActionPattern.BasicDuel,2,"회복병 기본 결투"),
        new(EnemySpecies.Healer,EnemyActionPattern.HealPreparation,0,"회복병 회복 도사림"),
        new(EnemySpecies.Commander,EnemyActionPattern.SupplyPreparation,0,"지휘관 보급 지시"),
        new(EnemySpecies.Bind,EnemyActionPattern.Normal,3,"구속병 일반 공격"),
        new(EnemySpecies.Bind,EnemyActionPattern.BasicDuel,3,"구속병 기본 결투"),
        new(EnemySpecies.Bind,EnemyActionPattern.StrongDuel,2,"구속병 강한 결투"),
        new(EnemySpecies.Corrosion,EnemyActionPattern.Normal,3,"침식병 일반 공격"),
        new(EnemySpecies.Corrosion,EnemyActionPattern.BasicDuel,3,"침식병 기본 결투"),
        new(EnemySpecies.Corrosion,EnemyActionPattern.StrongDuel,2,"침식병 강한 결투")
    };

    public static int ConfirmedCardCount=>Confirmed.Length;


    public static void Verify()
    {
        var available=Resources.LoadAll<SkillDefinition>(
            EnemyStageOneSkillCatalog.ResourcePath)
            .Where(x=>x!=null&&x.SkillId!=
                EnemyCounterReaction.CounterSkillId).ToArray();
        if(available.Length!=Confirmed.Length)
            throw new InvalidOperationException("Expected 18, found "+available.Length);
        foreach(var entry in Confirmed)
        {
            var d=EnemyStageOneSkillCatalog.Get(entry.Species,entry.Action);
            if(d==null||d.SkillId!=EnemyStageOneSkillCatalog.StableId(
                 entry.Species,entry.Action)||d.EffectEntries.Count!=0||
               d.Effects.Count!=0||
               d.EnergyCost!=EnemySpeciesPolicy.ActionEnergyCost(entry.Action)||
               !d.OverrideEnergyCost||
               d.EffectiveRollCount!=(entry.Rolls>0?entry.Rolls:1)||
               d.BreakMode!=(entry.Break?PartBreakMode.WeakenedOnly:PartBreakMode.None)||
               (EnemySpeciesPolicy.TryGet(entry.Species,out var spec)&&
                entry.Rolls>0&&spec.PowerAdjustment!=0&&
                (d.BasePowerRule!=SkillBasePowerRule.CanonicalPlusFlat||
                 PowerFormulaService.ResolveBasePower(d)!=
                    PowerFormulaService.GetEffectiveEnergyCost(d)*2+
                    (PowerFormulaService.TryGetCurveValue(d.EffectiveRollCount,
                         out var curve)?curve:0)+spec.PowerAdjustment)))
                throw new InvalidOperationException("Invalid stage1 skill "+entry.Display);
            string path=AssetDatabase.GetAssetPath(d);
            if(string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(path))||
               EditorUtility.IsDirty(d))
                throw new InvalidOperationException("Dirty/invalid GUID "+entry.Display);
        }
        Debug.Log("[R7-8 Stage1 Assets] PASS=18/18 native fixed-roll skills; EditOnly.");
    }
}
#endif