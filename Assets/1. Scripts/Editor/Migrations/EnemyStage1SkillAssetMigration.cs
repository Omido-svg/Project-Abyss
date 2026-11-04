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
public static class EnemyStage1SkillAssetMigration
{
    private const string Root="Assets/2. Data/Resources/ProjectAbyss/Stage1Enemy1029";

    private readonly struct Entry
    {
        public readonly EnemySpecies1029 Species;
        public readonly EnemyAction1029 Action;
        public readonly int Rolls;
        public readonly string Display;
        public readonly bool Break;
        public Entry(EnemySpecies1029 species,EnemyAction1029 action,
            int rolls,string display,bool canBreak=false)
        {
            Species=species;Action=action;Rolls=rolls;
            Display=display;Break=canBreak;
        }
    }
    private static readonly Entry[] Confirmed =
    {
        new(EnemySpecies1029.Charge,EnemyAction1029.StrongDuel,3,"돌격병 강한 결투"),
        new(EnemySpecies1029.HeavyHit,EnemyAction1029.StrongDuel,2,"강타병 강한 결투"),
        new(EnemySpecies1029.Flurry,EnemyAction1029.StrongDuel,4,"난타병 강한 결투"),
        new(EnemySpecies1029.Gladiator,EnemyAction1029.BasicDuel,3,"검투병 기본 결투"),
        new(EnemySpecies1029.Gladiator,EnemyAction1029.StrongDuel,3,"검투병 강한 결투"),
        new(EnemySpecies1029.Pressure,EnemyAction1029.StrongDuel,3,"압박병 강한 압박 결투"),
        new(EnemySpecies1029.Shatter,EnemyAction1029.BasicDuel,2,"파쇄병 기본 결투"),
        new(EnemySpecies1029.Shatter,EnemyAction1029.StrongDuel,2,"파쇄병 파쇄 결투",true),
        new(EnemySpecies1029.Healer,EnemyAction1029.Normal,2,"회복병 일반 공격"),
        new(EnemySpecies1029.Healer,EnemyAction1029.BasicDuel,2,"회복병 기본 결투"),
        new(EnemySpecies1029.Healer,EnemyAction1029.HealPreparation,0,"회복병 회복 도사림"),
        new(EnemySpecies1029.Commander,EnemyAction1029.SupplyPreparation,0,"지휘관 보급 지시"),
        new(EnemySpecies1029.Bind,EnemyAction1029.Normal,3,"구속병 일반 공격"),
        new(EnemySpecies1029.Bind,EnemyAction1029.BasicDuel,3,"구속병 기본 결투"),
        new(EnemySpecies1029.Bind,EnemyAction1029.StrongDuel,2,"구속병 강한 결투"),
        new(EnemySpecies1029.Corrosion,EnemyAction1029.Normal,3,"침식병 일반 공격"),
        new(EnemySpecies1029.Corrosion,EnemyAction1029.BasicDuel,3,"침식병 기본 결투"),
        new(EnemySpecies1029.Corrosion,EnemyAction1029.StrongDuel,2,"침식병 강한 결투")
    };

    public static int ConfirmedCardCount=>Confirmed.Length;
    public static void Create()
    {
        if(EditorApplication.isPlaying)
            throw new InvalidOperationException("Create only in EditMode");
        if(Confirmed.Select(e=>EnemyStageOneSkillCatalog.StableId(
            e.Species,e.Action)).Distinct().Count()!=Confirmed.Length)
            throw new InvalidOperationException("Duplicate stage1 skill Id");
        string[] parts=Root.Split('/');
        string dir=parts[0];
        for(int i=1;i<parts.Length;i++)
        {
            string next=dir+"/"+parts[i];
            if(!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(dir,parts[i]);
            dir=next;
        }

        foreach(var entry in Confirmed)
        {
            string id=EnemyStageOneSkillCatalog.StableId(
                entry.Species,entry.Action);
            string path=Root+"/"+id+".asset";
            if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)!=null||
               File.Exists(path))
                throw new InvalidOperationException(
                    "R7-8 source exists: no overwrite: "+path);
        }

        int created=0;
        foreach(var entry in Confirmed)
        {
            string id=EnemyStageOneSkillCatalog.StableId(
                entry.Species,entry.Action);
            string path=Root+"/"+id+".asset";
            bool prep=entry.Action==EnemyAction1029.HealPreparation||
                entry.Action==EnemyAction1029.SupplyPreparation;
            var definition=ScriptableObject.CreateInstance<SkillDefinition>();
            definition.name=entry.Display;
            definition.SkillName=entry.Display;
            definition.ActionType=prep?ActionType.Preparation:
                entry.Action==EnemyAction1029.Normal
                    ?ActionType.NormalAttack:ActionType.Duel;
            definition.Color=prep?SkillColor.Unset:SkillColor.Red;
            definition.BasePowerRule=SkillBasePowerRule.CanonicalCurve;
            if(!EnemySpeciesPolicy.TryGet(
                entry.Species,out var species))
                throw new InvalidOperationException("Unknown species "+entry.Species);
            definition.BasePowerFlatAdjustment=prep?0:species.PowerAdjustment;
            // CanonicalCurve ignores BasePowerFlatAdjustment. Use the only
            // supported curve+offset rule for healer/commander/tank/debuffer.
            if(!prep&&species.PowerAdjustment!=0)
                definition.BasePowerRule=SkillBasePowerRule.CanonicalPlusFlat;
            definition.ExchangeRollCount=entry.Rolls>0?entry.Rolls:1;
            definition.Rolls.Clear();
            definition.ResolverType=SkillResolverType.Dice;
            definition.DiceMin=1;definition.DiceMax=8;
            definition.OverrideEnergyCost=true;
            definition.EnergyCost=EnemySpeciesPolicy.ActionEnergyCost(
                entry.Action);
            definition.OverrideCanClash=prep;
            definition.CanClashValue=!prep;
            definition.CanBreakPart=entry.Break;
            definition.BreakMode=entry.Break
                ?PartBreakMode.WeakenedOnly:PartBreakMode.None;
            definition.Description=
                "1029 §12.3 확정 규칙만 작성된 단계별 몬스터 스킬."
                +" 미정인 다른 스킬의 굴림·위력은 별도 데이터로 보류."
                +(prep?" 지원 도사림은 계획에서 즉시 적용.":"");
            var serialized=new SerializedObject(definition);
            var identity=serialized.FindProperty("skillId");
            if(identity==null)
                throw new InvalidOperationException("SkillDefinition.skillId not found");
            identity.stringValue=id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition,path);
            created++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EnemyStageOneSkillCatalog.ResetCache();
        Debug.Log("[R7-8 Stage1 Assets] CREATE="+created+" / 18 known-roll cards;"+
            " missing-roll cards NOT authored. EditOnly.");
    }

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