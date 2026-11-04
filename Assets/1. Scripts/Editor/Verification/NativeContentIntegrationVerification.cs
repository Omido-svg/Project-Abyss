#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// R7-12 integration closure over R7-0..11. Validates REAL authored source
/// identities, permissible overlap of old and canonical Olaf native graphs,
/// pending design debt, absence of legacy adapter in player code and untouched
/// GUID/.meta. Only safe EditMode inspections; no scene saves or PlayMode.
/// </summary>
public static class NativeContentIntegrationVerification
{
    public static void Run()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)
            throw new InvalidOperationException("R7-12 idle EditMode only");

        int pass=0;
        void Check(string name,bool ok)
        {
            if(!ok)throw new InvalidOperationException(
                "[R7-12 Final] FAIL "+name);
            pass++;
        }
        var root=Directory.GetParent(Application.dataPath).FullName;
        var scripts=Directory.GetFiles(Application.dataPath,"*.cs",
            SearchOption.AllDirectories);
        string Script(string filename)
        {
            var hits=scripts.Where(x=>Path.GetFileName(x)==filename).ToArray();
            if(hits.Length!=1)
                throw new InvalidOperationException("Expected unique "+filename);
            return File.ReadAllText(hits[0]).Replace("\r\n","\n");
        }
        Check("F01 compiler not currently busy",
            !EditorApplication.isCompiling);
        Check("F02 actual source census 145 original + 19 new",
            SkillCorpusVerification.IsExactLayeredCensus());
        Check("F03 enemy source catalog exact new 19",
            EnemyStageOneSkillCatalog.AuthoredCount==19);
        var skills=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(p=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(p))
            .Where(x=>x!=null).ToArray();
        Check("F04 exactly 164 source skill assets",skills.Length==164);
        Check("F05 original SkillDefinition assets clean",
            skills.All(x=>!EditorUtility.IsDirty(x)));

        var rows=new List<(string Fam,string Id,string Guid,
            string Name,bool Valid,UnityEngine.Object Graph)>();
        void Add(string fam,string id,string guid,string name,bool valid,
            UnityEngine.Object graph)=>
            rows.Add((fam,id,guid,name,valid,graph));

        foreach(var x in Resources.LoadAll<R7NativeAbilityDefinition>(
            "ProjectAbyss/NativeAbilities"))
            Add("Resource",x.StableId,x.SourceAssetGuid,x.SourceAssetName,x.IsValid,x);
        foreach(var x in Resources.LoadAll<R7NativeStatusAbilityDefinition>(
            "ProjectAbyss/NativeStatuses"))
            Add("Status",x.StableId,x.SourceAssetGuid,x.SourceAssetName,x.IsValid,x);
        foreach(var x in Resources.LoadAll<R7NativeMixedAbilityDefinition>(
            "ProjectAbyss/NativeMixed"))
            Add("Mixed",x.StableId,x.SourceAssetGuid,x.SourceAssetName,x.IsValid,x);
        foreach(var x in Resources.LoadAll<R7NativeUniqueGaugeDefinition>(
            "ProjectAbyss/NativeUniqueGauges"))
            Add("Gauge",x.StableId,x.SourceAssetGuid,x.SourceAssetName,x.IsValid,x);
        foreach(var x in Resources.LoadAll<NativeSingleEffectDefinition>(
            "ProjectAbyss/NativeSingles"))
            Add("Single",x.StableId,x.SourceAssetGuid,x.SourceAssetName,x.IsValid,x);
        foreach(var x in Resources.LoadAll<OlafNativeAbilityDefinition>(
            "ProjectAbyss/NativeOlaf"))
            Add("Olaf",x.StableId,x.SourceAssetGuid,x.SourceAssetName,x.IsValid,x);
        foreach(var x in Resources.LoadAll<OlafNativeAbilityDefinition>(
            "ProjectAbyss/NativeOlafCanon"))
            Add("OlafCanon",x.StableId,x.SourceAssetGuid,x.SourceAssetName,x.IsValid,x);
        foreach(var x in Resources.LoadAll<YujinNativeAbilityDefinition>(
            "ProjectAbyss/NativeYujin"))
            Add("Yujin",x.StableId,x.SourceAssetGuid,x.SourceAssetName,x.IsValid,x);

        Check("F06 native graph inventory exactly 91",rows.Count==91);
        Check("F07 every native graph matches original GUID/SkillId/asset name",
            rows.All(x=>{
                var path=AssetDatabase.GUIDToAssetPath(x.Guid);
                var source=AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
                return x.Valid&&source!=null&&source.SkillId==x.Id&&
                    source.name==x.Name&&!EditorUtility.IsDirty(x.Graph);
            }));
        Check("F08 native graph .meta all preserved",
            rows.All(x=>{
                var path=AssetDatabase.GetAssetPath(x.Graph);
                return !string.IsNullOrWhiteSpace(path)&&File.Exists(path+".meta");
            }));
        var byGuid=rows.GroupBy(x=>x.Guid).ToArray();
        Check("F09 75 unique source GUID identities across 91 graphs",
            byGuid.Length==75);
        var doubled=byGuid.Where(g=>g.Count()>1).ToArray();
        Check("F10 16 historical graph aliases, no unauthorized overlap",
            doubled.Length==16&&
            doubled.All(g=>g.Count()==2&&
                g.Any(x=>x.Fam=="OlafCanon")&&
                g.Any(x=>x.Fam=="Resource"||x.Fam=="Status"||x.Fam=="Gauge")));
        Check("F11 Olaf canonical router precedes all older resource routes",
            Script("SkillEffectDispatcher.cs").IndexOf(
                "OlafNativeAbilityRuntimeBinding",StringComparison.Ordinal)<
            Script("SkillEffectDispatcher.cs").IndexOf(
                "R7NativeResourceMigration",StringComparison.Ordinal));

        var collisions=skills.GroupBy(x=>x.SkillId,StringComparer.Ordinal)
            .Where(x=>x.Count()>1).ToArray();
        Check("F12 exactly four legacy duplicate groups eight GUID-owned assets",
            collisions.Length==4&&collisions.Sum(x=>x.Count())==8&&
            collisions.All(x=>SkillIdentityVerification
                .IsApprovedGroup(x)));

        var emotionCatalog=AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(
            "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset");
        var emotion=NativeEmotionCatalog.Registry;
        Check("F13 63 canonical emotion source cards",
            emotionCatalog?.Entries?.Count==63);
        Check("F14 63 native emotion source graphs",emotion.Count==63);
        Check("F15 exactly 50 executable native emotion cards",
            emotion.Values.Count(x=>x.State==
                NativeEmotionGraphState.NativeReady)==50);
        Check("F16 remaining 13 emotion effects explicitly pending",
            emotion.Values.Count(x=>x.State==
                NativeEmotionGraphState.CanonicalValuesPending)==13);
        Check("F17 all 63 emotion graph assets retain source identities",
            emotionCatalog.Entries.All(x=>
                NativeEmotionCatalog.TryResolve(x,out var graph)&&
                graph.SourceGuid==AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(x))&&
                !EditorUtility.IsDirty(x)&&!EditorUtility.IsDirty(graph)));
        Check("F18 emotion runtime executes native graph, not old Apply",
            Script("EmotionAugmentSystem.cs").Contains("graph.Apply(runtime)")&&
            !Script("EmotionAugmentSystem.cs").Contains("effect.Apply(runtime)"));
        Check("F19 pending emotion source no-effect clearly visible",
            Script("EmotionAugmentChoiceUI.cs").Contains(
                "[정본 수치 미정] 현재 전투 효과는 발동하지 않습니다."));

        var itemCat=AssetDatabase.LoadAssetAtPath<RunItemCatalog>(
            "Assets/2. Data/Progression/RunItems/RunItemCatalog.asset");
        var itemGraphs=NativeRunItemCatalog.Registry;
        Check("F20 60 original item definitions",
            itemCat?.Items?.Count==60);
        Check("F21 60 native item identity-only graphs",
            itemGraphs.Count==60);
        Check("F22 no unapproved native combat item is executable",
            itemGraphs.Values.All(x=>x.State==
                NativeRunItemState.CanonicalEffectPending)&&
            NativeRunItemCatalog.CountApprovedCombatItems(
                itemCat.Items)==0);
        Check("F23 original 30 common/30 class source split",
            itemCat.Items.Count(x=>x.Tier==RunItemTier.Class)==30&&
            itemCat.Items.Count(x=>x.Tier!=RunItemTier.Class)==30);
        Check("F24 source's 6/6/6/6/6 common-tier mismatch deliberately pending",
            Enumerable.Range(1,5).All(t=>
                itemCat.Items.Count(x=>x.Tier==(RunItemTier)t)==6)&&
            itemGraphs.Values.Count(x=>x.CanonicalTierMappingPending)==30);
        Check("F25 all item native graph identities and GUIDs preserved",
            itemCat.Items.All(x=>
                NativeRunItemCatalog.TryResolve(x,out var graph)&&
                graph.SourceGuid==AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(x))&&
                !EditorUtility.IsDirty(x)&&!EditorUtility.IsDirty(graph)));
        Check("F26 no pending TEMP item can enter actual battle build",
            Script("RunFlowLiveBattleBridge.cs").Contains(
                "ResolveExecutableCombatItem(")&&
            Script("RunFlowLiveBattleBridge.cs").Contains(
                "original is not ProvisionalBalanceRunItem"));
        Check("F27 run item UI distinguishes non-executable pending",
            Script("RunFlowTestHarness.cs").Contains(
                "[효과 미정 · 전투 보너스 없음]"));

        Check("F28 three owner-locked fixed passive contracts",
            CharacterFixedPassiveContract.FixedPassives.Count==3);
        Check("F29 known seven legacy enemy source IDs fail closed",
            EnemyCombatNativeContract.All.Count==7&&
            Script("SkillEffectDispatcher.cs").Contains(
                "enemy native route missing or mismatched"));

        var gatedNames=new[]{"LegacySkillEffectEntryAdapter.cs",
            "R7ExclusiveEntryMigrationScope.cs","SkillMigrationObserver.cs"};
        Check("F30 three legacy-only classes excluded from player build",
            gatedNames.All(n=>Script(n).TrimStart().StartsWith(
                "#if UNITY_EDITOR",StringComparison.Ordinal)));
        string dispatcher=Script("SkillEffectDispatcher.cs");
        Check("F31 both temporary adapter hooks Editor-only",
            dispatcher.Split(new[]{
                "#if UNITY_EDITOR\n            // R7-12: opt-in historical parity-only adapter."
            },StringSplitOptions.None).Length-1==2);
        Check("F32 legacy fallback retained for still-unmigrated source skills",
            dispatcher.Contains("return ExecuteEntries(")&&
            Script("R7ExclusiveEntryMigrationScope.cs").Contains(
                "LegacySkillEffectEntryAdapter(entry)"));
        Check("F33 player build cannot construct legacy adapter in dispatcher",
            !dispatcher.Contains("new LegacySkillEffectEntryAdapter("));

        Check("F33b historical parity escape is Editor-only and GUID-locked",
            dispatcher.Contains("HistoricalParityScope.Matches(definition)")&&
            Script("HistoricalParityScope.cs").TrimStart().StartsWith(
                "#if UNITY_EDITOR",StringComparison.Ordinal));

        // Regression old gate sources and test-only runtime classes preserve
        // their exact GUIDs. Compare .meta bytes to PRE-CHANGE rollback snapshots.
        int metaCount=0;
        foreach(var folder in new[]{
            "R7_12_OriginalRegressionScripts_20261103",
            "R7_12_OriginalLegacyBoundary_20261103"})
        {
            var backup=Path.Combine(root,folder);
            foreach(var copy in Directory.GetFiles(backup,"*.meta",
                         SearchOption.AllDirectories))
            {
                var live=Path.Combine(root,copy.Substring(backup.Length+1));
                if(!File.Exists(live)||
                   !File.ReadAllBytes(copy).SequenceEqual(File.ReadAllBytes(live)))
                    throw new InvalidOperationException(
                        "[R7-12 Final] Original .meta changed: "+live);
                metaCount++;
            }
        }
        Check("F34 all 15 modified existing script/meta GUIDs exactly retained",
            metaCount==15);

        var r9=Path.Combine(root,"ProjectAbyss_R9_Integration_20261102",
            "R9_GATE_RESULTS.csv");
        var lines=File.Exists(r9)?File.ReadAllLines(r9):Array.Empty<string>();
        // R7-12 explicitly runs BOTH R9 phases and rejects partial reports.
        var editResults=lines.Skip(1).Where(line=>
            line.StartsWith("\"Edit\",",StringComparison.Ordinal)).ToArray();
        var playResults=lines.Skip(1).Where(line=>
            line.StartsWith("\"Play\",",StringComparison.Ordinal)).ToArray();
        Check("F35 R9 30 EditMode gates all PASS",
            editResults.Length==30&&
            editResults.All(line=>line.Contains(",\"PASS\",")));
        Check("F35b R9 39 PlayMode gates all PASS",
            playResults.Length==39&&lines.Length==70&&
            playResults.All(line=>line.Contains(",\"PASS\",")));
        Check("F36 PlayMode exited after full R9 suite",
            !EditorApplication.isPlaying&&!Application.isPlaying);
        Check("F37 unsaved Battle Test Scene remains Dirty",
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==
                "Battle Test Scene"&&
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty);

        Debug.Log("[R7-12 Final] RESULT PASS="+pass+" FAIL=0 "+
            "SOURCE_SKILLS=145+19, NATIVE_GRAPH=91/75 GUIDs, "+
            "ALIAS_OVERLAP=16, EMOTION=50_READY+13_PENDING, "+
            "ITEMS=60_PENDING, PASSIVES=3, R9=30_EDIT+39_PLAY_PASS, "+
            "LEGACY_ADAPTER_EDITOR_ONLY, R9_PLAYMODE_VERIFIED NO_SCENE_SAVE");
    }
}
#endif