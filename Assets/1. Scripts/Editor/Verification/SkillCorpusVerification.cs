#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// R7-12 layered corpus truth: the original R7-0 145 skill assets still
/// coexist with 19 GUID-stable Stage1Enemy1029 cards authored in R7-8.
/// Old R7/R8 gates must not interpret those 19 as duplicated originals,
/// nor silently change their baseline from 145 to any arbitrary count.
/// </summary>
public static class SkillCorpusVerification
{
    public const int OriginalSkillCount=145;
    public const int Stage1NewSkillCount=19;
    public const int ExpectedTotalSkillCount=164;
    public const int OriginalMetadataOnlyCount=9;
    public const int NewStage1MetadataOnlyCount=4;

    public const string Stage1FolderPrefix=
        "Assets/2. Data/Resources/ProjectAbyss/Stage1Enemy1029/";

    public static bool IsExactLayeredCensus()
    {
        var all=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p=>!string.IsNullOrEmpty(p))
            .Select(p=>new {Path=p,
                Data=AssetDatabase.LoadAssetAtPath<SkillDefinition>(p),
                Guid=AssetDatabase.AssetPathToGUID(p)})
            .Where(x=>x.Data!=null).ToArray();
        if(all.Length!=ExpectedTotalSkillCount||
           all.Any(x=>x.Guid?.Length!=32||EditorUtility.IsDirty(x.Data))||
           all.Select(x=>x.Guid).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=
                ExpectedTotalSkillCount)
            return false;

        var stage=all.Where(x=>x.Path.StartsWith(Stage1FolderPrefix,
            StringComparison.Ordinal)).ToArray();
        var original=all.Where(x=>!x.Path.StartsWith(Stage1FolderPrefix,
            StringComparison.Ordinal)).ToArray();

        var stageResources=Resources.LoadAll<SkillDefinition>(
            EnemyStageOneSkillCatalog.ResourcePath);
        if(stage.Length!=Stage1NewSkillCount||
           stageResources.Length!=Stage1NewSkillCount||
           original.Length!=OriginalSkillCount||
           stage.Any(x=>x.Data.SkillId==null||
               !x.Data.SkillId.StartsWith("enemy1029.stage1.",
                   StringComparison.Ordinal)||
               x.Data.HasEffectEntries||
               x.Data.Effects?.Any(e=>e!=null)==true)||
           stage.Select(x=>x.Data.SkillId)
               .Distinct(StringComparer.Ordinal).Count()!=Stage1NewSkillCount||
           stageResources.Any(x=>!stage.Any(s=>ReferenceEquals(s.Data,x)))||
           original.Count(x=>ProjectAbyss.Abilities.SkillMigrationObserver
               .IsMetadataOnlyNormal(x.Data))!=OriginalMetadataOnlyCount||
           stage.Count(x=>ProjectAbyss.Abilities.SkillMigrationObserver
               .IsMetadataOnlyNormal(x.Data))!=NewStage1MetadataOnlyCount)
            return false;
        var collisions=all.GroupBy(x=>x.Data.SkillId,StringComparer.Ordinal)
            .Where(g=>g.Count()>1).ToArray();
        return collisions.Length==4&&collisions.Sum(g=>g.Count())==8;
    }

    public static void Require()
    {
        if(!IsExactLayeredCensus())
            throw new InvalidOperationException(
                "[R7-12] Unexpected SkillDefinition corpus: expected "+
                "145 legacy R7-0 originals + 19 R7-8 Stage1 1029 sources, "+
                "unique GUIDs and 9+4 metadata-only cards.");
    }

    public static void Run()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)
            throw new InvalidOperationException("Idle EditMode only");
        Require();
        Debug.Log("[R7-12 Corpus] RESULT PASS=10 FAIL=0 "+
            "Original145, Added19, Total164; Metadata9+4; "+
            "4 historical duplicate-ID groups GUID-resolved; no source SO dirty. "+
            "NO_PLAYMODE NO_SCENE_SAVE");
    }
}
#endif