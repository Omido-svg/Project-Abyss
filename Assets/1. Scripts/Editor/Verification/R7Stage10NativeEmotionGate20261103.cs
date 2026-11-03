#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// R7-10 source-to-native parity, 21 deterministic offers, runtime route and
/// 13 explicitly unresolved 1029 values. EditMode only; no R9 or PlayMode.
/// </summary>
public static class R7Stage10NativeEmotionGate20261103
{
    public static void Run()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)
            throw new InvalidOperationException("R7-10 gate requires idle EditMode");
        int pass=0;
        void Check(string message,bool ok)
        {
            if(!ok)throw new InvalidOperationException("[R7-10] FAIL "+message);
            pass++;
        }

        var catalog=AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(
            "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset");
        Check("C01 original catalog source resolves",catalog!=null);
        Check("C02 exactly 63 source cards",catalog.Entries.Count==63);
        Check("C03 original catalog is unchanged",!EditorUtility.IsDirty(catalog));
        Check("C04 all source definitions load",catalog.Entries.All(x=>x!=null));
        Check("C05 source stable IDs distinct",
            catalog.Entries.Select(x=>x.AugmentId).Distinct().Count()==63);

        var sourceGuids=catalog.Entries.Select(x=>
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(x))).ToArray();
        Check("C06 all 63 original GUIDs distinct and nonempty",
            sourceGuids.Distinct().Count()==63&&
            sourceGuids.All(g=>g?.Length==32));

        R7NativeEmotionCatalog20261103.ResetCache();
        var native=R7NativeEmotionCatalog20261103.Registry;
        Check("C07 registry exactly 63",native.Count==63);
        Check("C08 native ready 50",
            native.Values.Count(x=>x.State==
                R7NativeEmotionGraphState20261103.NativeReady)==50);
        Check("C09 explicit value pending 13",
            native.Values.Count(x=>x.State==
                R7NativeEmotionGraphState20261103.CanonicalValuesPending)==13);
        Check("C10 independent native class cohort 33",
            native.Values.Count(x=>x is R7NativeEmotion33Graph20261103)==33);
        Check("C11 native common status/timer cohort 6",
            native.Values.Count(x=>x is R7NativeEmotionCommonGraph20261103)==6);
        Check("C12 native rulebreaker cohort 10",
            native.Values.Count(x=>x is R7NativeEmotionRulebreakerGraph20261103)==10);
        Check("C13 native immediate prestige cohort 1",
            native.Values.Count(x=>x is R7NativeEmotionPrototypeGraph20261103)==1);
        Check("C14 pending cohort 13",
            native.Values.Count(x=>x is R7NativeEmotionPendingGraph20261103)==13);
        Check("C15 rulebreaker operations exactly 11",
            native.Values.OfType<R7NativeEmotionRulebreakerGraph20261103>()
                .Sum(x=>x.Operations.Count)==11);

        var temp=new List<EmotionAugmentDefinition>();
        foreach(EmotionType emotion in Enum.GetValues(typeof(EmotionType)))
        for(int tier=1;tier<=3;tier++)
        {
            Check("canonical three deterministic offers "+emotion+" T"+tier,
                catalog.TryBuildCanonicalOffer(emotion,tier,temp,out _)&&
                temp.Count==3&&
                temp.All(a=>a.Emotion==emotion&&a.Tier==tier));
            Check("source and graph all linked "+emotion+" T"+tier,
                temp.All(a=>R7NativeEmotionCatalog20261103.TryResolve(a,out _)));
        }

        foreach(var source in catalog.Entries)
        {
            var p=AssetDatabase.GetAssetPath(source);
            var g=native[source.AugmentId];
            var gp=AssetDatabase.GetAssetPath(g);
            var originalGuid=AssetDatabase.AssetPathToGUID(p);
            Check("source GUID/name/tier/emotion "+source.AugmentId,
                g.Matches(source)&&g.SourceGuid==originalGuid&&
                g.CanonicalDesignStatus==source.DesignStatus&&
                !EditorUtility.IsDirty(source));
            Check("native asset and meta unchanged "+source.AugmentId,
                gp.StartsWith(
                    "Assets/2. Data/Resources/ProjectAbyss/NativeEmotions/")&&
                File.Exists(gp)&&File.Exists(gp+".meta")&&
                !EditorUtility.IsDirty(g)&&g.SchemaVersion==1&&
                AssetDatabase.AssetPathToGUID(gp)?.Length==32);
            var old=source.Effects?.FirstOrDefault();
            bool equal;
            if(g is R7NativeEmotion33Graph20261103 n33 &&
               old is Canonical0922Emotion33EffectDefinition x33)
            {
                equal=(int)n33.Kind==(int)x33.Kind&&
                    n33.Amount==x33.Amount&&
                    n33.SecondaryAmount==x33.SecondaryAmount&&
                    n33.Duration==x33.Duration&&
                    n33.MaximumTotal==x33.MaximumTotal&&
                    Mathf.Approximately(n33.Ratio,x33.Ratio)&&
                    Mathf.Approximately(n33.Multiplier,x33.Multiplier)&&
                    g.State==R7NativeEmotionGraphState20261103.NativeReady;
                Check("R7 independent 33 mechanic instantiated "+
                    source.AugmentId,n33.CreateMechanic(null)?.GetType().Name==
                    "R7NativeEmotion33Mechanic20261103");
            }
            else if(g is R7NativeEmotionCommonGraph20261103 nc &&
                    old is EmotionAugmentCanonical0922EffectDefinition xc)
            {
                equal=(int)nc.Kind==(int)xc.Kind&&
                    nc.PrimaryStatus==xc.PrimaryStatus&&
                    nc.PrimaryValue==xc.PrimaryValue&&
                    nc.PrimaryDuration==xc.PrimaryDuration&&
                    nc.HasSecondaryStatus==xc.HasSecondaryStatus&&
                    nc.SecondaryStatus==xc.SecondaryStatus&&
                    nc.SecondaryValue==xc.SecondaryValue&&
                    nc.SecondaryDuration==xc.SecondaryDuration&&
                    nc.GoldThreshold==xc.GoldThreshold&&
                    nc.BlockPrestige==xc.BlockPrestige&&
                    nc.Turns==xc.Turns&&
                    nc.Amount==xc.Amount&&
                    g.State==R7NativeEmotionGraphState20261103.NativeReady;
                Check("R7 independent common mechanic instantiated "+
                    source.AugmentId,nc.CreateMechanic(null)?.GetType().Name==
                    "R7NativeEmotionCommonMechanic20261103");
            }
            else if(g is R7NativeEmotionRulebreakerGraph20261103 nr)
            {
                var os=source.Effects.Cast<EmotionRulebreakerEffectDefinition>().ToArray();
                equal=nr.Operations.Count==os.Length&&
                    nr.Operations.Zip(os,(a,b)=>
                        a.Operation==b.Operation&&
                        a.TargetPartType==b.TargetPartType&&
                        a.AnyPart==b.AnyPart&&
                        a.Amount==b.Amount&&
                        Mathf.Approximately(a.Value,b.Value)&&
                        a.RequiredConsecutiveTurns==b.RequiredConsecutiveTurns&&
                        a.BooleanValue==b.BooleanValue).All(x=>x)&&
                    g.State==R7NativeEmotionGraphState20261103.NativeReady;
            }
            else if(g is R7NativeEmotionPrototypeGraph20261103 np &&
                    old is EmotionAugmentPrototypeEffectDefinition xp)
            {
                equal=np.GainEnergy==xp.GainEnergy&&
                    np.GainPrestige==xp.GainPrestige&&
                    Mathf.Approximately(np.RecoverStaggerRatio,xp.RecoverStaggerRatio)&&
                    np.RollBonus==xp.RollBonus&&
                    Mathf.Approximately(np.DamageDealtMultiplier,xp.DamageDealtMultiplier)&&
                    Mathf.Approximately(np.DamageTakenMultiplier,xp.DamageTakenMultiplier)&&
                    np.PositiveMomentumBonus==xp.PositiveMomentumBonus&&
                    Mathf.Approximately(np.PrestigeGainMultiplier,xp.PrestigeGainMultiplier)&&
                    g.State==R7NativeEmotionGraphState20261103.NativeReady;
            }
            else if(g is R7NativeEmotionPendingGraph20261103 pending)
            {
                equal=source.Effects?.Count==pending.LegacyEffectEntries&&
                    (source.Effects?.Count==0||
                     source.Effects.All(x=>
                        x is TempBalanceEmotionProxyEffectDefinition))&&
                    !string.IsNullOrWhiteSpace(pending.PendingReason)&&
                    g.State==
                        R7NativeEmotionGraphState20261103.CanonicalValuesPending;
            }
            else
                equal=false;
            Check("every native graph value/type equals source "+
                source.AugmentId,equal);
        }

        var first=catalog.Entries[0];
        var mirror=ScriptableObject.CreateInstance<EmotionAugmentDefinition>();
        try
        {
            mirror.AugmentId=first.AugmentId;
            mirror.Emotion=first.Emotion;
            mirror.Tier=first.Tier;
            mirror.DisplayName=first.DisplayName;
            mirror.name="Wrong source asset identity";
            Check("C16 same ID but different asset identity fails closed",
                !R7NativeEmotionCatalog20261103.TryResolve(mirror,out _));
            mirror.name=first.name;
            mirror.Tier=first.Tier==1?2:1;
            Check("C17 same ID but different tier fails closed",
                !R7NativeEmotionCatalog20261103.TryResolve(mirror,out _));
        }
        finally{UnityEngine.Object.DestroyImmediate(mirror);}

        var sourceFiles=Directory.GetFiles(Application.dataPath,"*.cs",
            SearchOption.AllDirectories);
        string Script(string filename)=>File.ReadAllText(
            sourceFiles.Single(x=>Path.GetFileName(x)==filename));
        string manager=Script("EmotionAugmentSystem.cs");
        string ui=Script("EmotionAugmentChoiceUI.cs");
        string nativeLogic=Script("R7NativeEmotionMechanics20261103.cs");
        Check("C18 manager uses only graph.Apply and graph.CreateMechanic",
            manager.Contains("graph.Apply(runtime)")&&
            manager.Contains("graph.CreateMechanic(runtime)")&&
            !manager.Contains("effect.Apply(runtime)")&&
            !manager.Contains("effect.CreateMechanic(runtime)"));
        Check("C19 manager refuses missing graph before taking choice",
            manager.Contains("TryResolve(selected,out _)")&&
            manager.Contains("return false;"));
        Check("C20 pending UI explicitly warns of no effect",
            ui.Contains("[정본 수치 미정]")&&
            ui.Contains("현재 전투 효과는 발동하지 않습니다."));
        Check("C21 independent engine contains no old SO definition reference",
            !nativeLogic.Contains("private readonly Canonical0922Emotion33EffectDefinition")&&
            !nativeLogic.Contains("private readonly EmotionAugmentCanonical0922EffectDefinition")&&
            !nativeLogic.Contains("TempBalanceEmotionProxyEffectDefinition"));
        Check("C22 original catalog deterministic GetCandidates",
            Script("EmotionAugmentCatalog.cs").Contains(
                "destination.Count != 3"));
        Check("C23 no active battle scene save and no PlayMode",
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==
                "Battle Test Scene"&&
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty);

        Debug.Log("[R7-10] 63 Emotion Native Graph Gate PASS="+pass+
            " FAIL=0; NATIVE_READY=50; DESIGN_PENDING=13; "+
            "7x3x3 deterministic; old cards/GUIDs untouched. "+
            "NO_R9 NO_PLAYMODE NO_SCENE_SAVE");
    }
}
#endif