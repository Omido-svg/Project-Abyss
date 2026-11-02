#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// R7-3 exact four source migration. Copies authored single-effect semantics
/// into NEW native SOs, never modifying legacy skills or their meta files.
/// No scene or PlayMode involved; all source GUIDs are immutable.
/// </summary>
public static class R7Stage3SingleEffectMigration20261102
{
    private struct Source
    {
        public string Guid;
        public string SkillId;
        public string Name;
        public string NativeAsset;
        public Source(string guid,string id,string name,string native)
        {Guid=guid;SkillId=id;Name=name;NativeAsset=native;}
    }
    private static readonly Source[] Sources={
        new Source("b785616cbce19214b8ad54bbd1b879ae",
            "olaf.duel.hold_on","Olaf_D_06","Olaf_D_06_Rupture"),
        new Source("4b7777e1d031bed4d834c47ac2b47cd8",
            "olaf.normal.choke","Olaf_N_14","Olaf_N_14_Pain"),
        new Source("148f3f1d401e1714f972f67064272da2",
            "olaf.normal.block_way","Olaf_N_15","Olaf_N_15_Block"),
        new Source("fb67fe25a3cf5af4bb383e8d7bfe492a",
            "yujin.normal.step_back","Yujin_N_04","Yujin_N_04_Block")
    };
    private const string Folder="Assets/2. Data/Resources/ProjectAbyss/NativeSingles";

    public static string AssetPath(int i)=>Folder+"/"+Sources[i].NativeAsset+".asset";
    public static int SourceCount=>Sources.Length;

    private static SkillDefinition GetSource(Source x)
    {
        var path=AssetDatabase.GUIDToAssetPath(x.Guid);
        var d=AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        if(d==null||d.SkillId!=x.SkillId||d.name!=x.Name||
            d.EffectEntries?.Count!=1||d.Effects?.Any(e=>e!=null)==true)
            throw new InvalidOperationException("R7-3 source identity changed: "+x.SkillId);
        var entry=d.EffectEntries[0];
        if(entry?.Definition==null||
            entry.RestrictToRoll||(entry.Conditions?.Count??0)!=0||
            (entry.Definition.Conditions?.Count??0)!=0||
            !string.IsNullOrEmpty(entry.UpgradeKey))
            throw new InvalidOperationException("R7-3 source has unsupported gates: "+x.SkillId);
        if(entry.Overrides!=null&&
            entry.Overrides.GetType().GetFields(
                System.Reflection.BindingFlags.Public|
                System.Reflection.BindingFlags.Instance)
                .Any(f=>f.FieldType==typeof(bool)&&f.Name.StartsWith("Override",
                    StringComparison.Ordinal)&&(bool)f.GetValue(entry.Overrides)))
            throw new InvalidOperationException("R7-3 source has active overrides: "+x.SkillId);
        return d;
    }

    private static void Fill(Source src,SkillDefinition skill,
        R7NativeSingleEffectDefinition20261102 dest)
    {
        var e=skill.EffectEntries[0];
        dest.SchemaVersion=1;
        dest.StableId=src.SkillId;
        dest.SourceAssetGuid=src.Guid;
        dest.SourceAssetName=src.Name;
        dest.Trigger=e.EffectiveTiming==SkillEffectTiming.OnExecute
            ?AbilityEventKind.RollUsing:
            e.EffectiveTiming==SkillEffectTiming.OnExchangeWin
                ?AbilityEventKind.ExchangeWon:AbilityEventKind.AttackResolutionStarting;
        dest.RestrictToRoll=e.RestrictToRoll;
        dest.RollNumber=e.RollNumber>0?e.RollNumber:1;
        dest.Duration=1;
        dest.NextTurn=false;
        dest.Target=R7SingleTarget.Owner;
        if(e.Definition is Olaf0922Phase7SkillEffectDefinition olaf)
        {
            switch(olaf.Operation)
            {
                case Olaf0922Phase7EffectOperation.TargetStatusNextTurn:
                    dest.Kind=R7SingleEffectKind.ApplyCanonicalStatus;
                    dest.Target=R7SingleTarget.CurrentTarget;
                    dest.NextTurn=true;
                    dest.Status=olaf.StatusId;
                    dest.Amount=olaf.Amount;
                    dest.Duration=olaf.Duration;
                    break;
                case Olaf0922Phase7EffectOperation.OwnerBlock:
                    dest.Kind=R7SingleEffectKind.GainBlock;
                    dest.Amount=olaf.Amount;
                    break;
                default:
                    throw new InvalidOperationException("Not a single common effect: "+src.SkillId);
            }
        }
        else if(e.Definition is Yujin0916SkillEffectDefinition yujin &&
            yujin.Operation==Yujin0916EffectOperation.StepBackBlock)
        {
            // Historical Yujin0916.Apply uses a literal AddBlock(10), NOT its
            // serialized Amount=1. This copies the actual implemented behavior.
            dest.Kind=R7SingleEffectKind.GainBlock;
            dest.Amount=10;
        }
        else throw new InvalidOperationException("Unsupported source template "+src.SkillId);
        if(!dest.IsValid)
            throw new InvalidOperationException("Invalid single-effect native graph "+src.SkillId);
    }

    [MenuItem("Game System Verification/R7-3 - Stage Four Native Single Effects (Edit Only)")]
    public static void Stage()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("EditMode only");
        if(!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/2. Data/Resources/ProjectAbyss","NativeSingles");
        // Preflight all four before creating any new asset.
        var source=Sources.Select(GetSource).ToArray();
        for(int i=0;i<Sources.Length;i++)
        {
            var verify=ScriptableObject.CreateInstance<R7NativeSingleEffectDefinition20261102>();
            try {Fill(Sources[i],source[i],verify);}
            finally {UnityEngine.Object.DestroyImmediate(verify);}
            if(AssetDatabase.LoadMainAssetAtPath(AssetPath(i))!=null)
                throw new InvalidOperationException("Existing native asset; refuse overwrite "+AssetPath(i));
        }
        for(int i=0;i<Sources.Length;i++)
        {
            var graph=ScriptableObject.CreateInstance<R7NativeSingleEffectDefinition20261102>();
            Fill(Sources[i],source[i],graph);
            AssetDatabase.CreateAsset(graph,AssetPath(i));
        }
        AssetDatabase.SaveAssets();
        Verify();
        Debug.Log("[R7-3] Created 4 new native single-effect SO assets.");
    }

    [MenuItem("Game System Verification/R7-3 - Verify Native Single Effects (Edit Only)")]
    public static void Verify()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("EditMode only");
        int pass=0;
        void Check(bool ok,string id)
        {
            if(!ok)throw new InvalidOperationException("R7-3 FAIL "+id);
            pass++;
        }
        for(int i=0;i<Sources.Length;i++)
        {
            var source=GetSource(Sources[i]);
            var actual=AssetDatabase.LoadAssetAtPath<R7NativeSingleEffectDefinition20261102>(
                AssetPath(i));
            Check(actual!=null&&actual.IsValid,"graph "+i);
            var expect=ScriptableObject.CreateInstance<R7NativeSingleEffectDefinition20261102>();
            try
            {
                Fill(Sources[i],source,expect);
                Check(actual.StableId==expect.StableId&&
                    actual.SourceAssetGuid==expect.SourceAssetGuid&&
                    actual.SourceAssetName==expect.SourceAssetName&&
                    actual.Kind==expect.Kind&&actual.Target==expect.Target&&
                    actual.Status==expect.Status&&actual.Amount==expect.Amount&&
                    actual.Duration==expect.Duration&&
                    actual.NextTurn==expect.NextTurn&&
                    actual.Trigger==expect.Trigger&&
                    actual.RestrictToRoll==expect.RestrictToRoll&&
                    actual.RollNumber==expect.RollNumber,"data parity "+i);
            }
            finally{UnityEngine.Object.DestroyImmediate(expect);}
            Check(!EditorUtility.IsDirty(source)&&
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source))==
                    Sources[i].Guid,"source stable "+i);
        }
        var live=Resources.LoadAll<R7NativeSingleEffectDefinition20261102>(
            "ProjectAbyss/NativeSingles");
        Check(live.Length==4&&live.All(x=>x.IsValid),"runtime resource registry 4");
        Check(R7ProductionSingleEffectMigration20261102.ApprovedSkillCount==4,
            "four production routes");
        Debug.Log("[R7-3] PASS="+pass+" FAIL=0 4 native singles (EditMode only)");
    }
}
#endif