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
///
/// 11-21 기준선 갱신 — 감사 파일 전부가 이 상수를 참조한다. 스킬 에셋을
/// 더하거나 지우면 여기 숫자만 고친다(에디터에서 센 값).
///   원본 166 = 옛 145 + 11-12 정본 적 26 + 그 밖 추가 − 노려보기 · 중복 정리 삭제
///   중복 skillId 0 (11-20 표준 · 블로토 · 환형 공격/수비를 에셋 하나로 합침)
/// </summary>
public static class SkillCorpusVerification
{
    public const int OriginalSkillCount=166;
    public const int Stage1NewSkillCount=19;
    public const int ExpectedTotalSkillCount=OriginalSkillCount+Stage1NewSkillCount;
    public const int OriginalMetadataOnlyCount=18;
    public const int NewStage1MetadataOnlyCount=4;
    public const int ExpectedDuplicateIdGroups=0;
    public const int ExpectedDuplicateIdAssets=0;

    // 네이티브 그래프 기준선(11-21 에디터 실측). 11-20 노려보기 · 중복 정리로
    // 리소스 2(common_preparation_glare · yujin_duel_block_retreat) · 상태 1(olaf_duel_single_cut) 삭제.
    // 11-21 빌야르 네이티브에 가려 실전에서 안 돌던 R5 그래프 19개 삭제(리소스 10 · 상태 5 · 게이지 4)
    // → 같은 카드에 그래프 두 개(별칭) 0.
    public const int NativeResourceCount=3;
    public const int NativeStatusCount=3;
    public const int NativeMixedCount=7;
    public const int NativeUniqueGaugeCount=1;
    public const int NativeSourceGraphCount=
        NativeResourceCount+NativeStatusCount+NativeMixedCount+NativeUniqueGaugeCount;
    public const int NativeSimpleGraphCount=
        NativeResourceCount+NativeStatusCount+NativeUniqueGaugeCount;
    public const int AuthoredNativeSchemaCount=NativeSourceGraphCount+1; // + 엘리트 전투 태세 준비 그래프
    public const int MetadataPilotCandidateCount=
        OriginalMetadataOnlyCount+NewStage1MetadataOnlyCount;
    public const int NativeGraphInventoryCount=70;   // 8개 Resources 폴더 전체
    public const int NativeGraphUniqueGuidCount=70;
    public const int NativeGraphAliasCount=0;
    public static string NativeFamilyLabel=>
        NativeResourceCount+"/"+NativeStatusCount+"/"+NativeMixedCount+"/"+NativeUniqueGaugeCount;
    public static string CensusLabel=>
        OriginalSkillCount+" original + "+Stage1NewSkillCount+" stage1 = "+
        ExpectedTotalSkillCount;

    public static bool IsExpectedDuplicateCensus(int groups,int assets) =>
        groups==ExpectedDuplicateIdGroups&&assets==ExpectedDuplicateIdAssets;

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
           original.Count(x=>ProjectAbyss.Abilities.SkillAbilityObserver
               .IsMetadataOnlyNormal(x.Data))!=OriginalMetadataOnlyCount||
           stage.Count(x=>ProjectAbyss.Abilities.SkillAbilityObserver
               .IsMetadataOnlyNormal(x.Data))!=NewStage1MetadataOnlyCount)
            return false;
        var collisions=all.GroupBy(x=>x.Data.SkillId,StringComparer.Ordinal)
            .Where(g=>g.Count()>1).ToArray();
        return IsExpectedDuplicateCensus(collisions.Length,
            collisions.Sum(g=>g.Count()));
    }

    public static void Require()
    {
        if(!IsExactLayeredCensus())
            throw new InvalidOperationException(
                "[R7-12] Unexpected SkillDefinition corpus: expected "+
                CensusLabel+", unique GUIDs, "+
                OriginalMetadataOnlyCount+"+"+NewStage1MetadataOnlyCount+
                " metadata-only cards and "+ExpectedDuplicateIdGroups+
                " duplicate-ID groups.");
    }

    public static void Run()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)
            throw new InvalidOperationException("Idle EditMode only");
        Require();
        Debug.Log("[R7-12 Corpus] RESULT PASS=10 FAIL=0 "+
            CensusLabel+"; Metadata"+OriginalMetadataOnlyCount+"+"+
            NewStage1MetadataOnlyCount+"; duplicate-ID groups "+
            ExpectedDuplicateIdGroups+"; no source SO dirty. "+
            "NO_PLAYMODE NO_SCENE_SAVE");
    }
}
#endif