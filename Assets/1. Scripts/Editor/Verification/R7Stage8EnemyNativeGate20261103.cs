#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using ProjectAbyss.Abilities;

/// <summary>R7-8 scope-only EditMode audit, not R9 or PlayMode.</summary>
public static class R7Stage8EnemyNativeGate20261103
{
    public static void Run()
    {
        if(EditorApplication.isPlaying)
            throw new InvalidOperationException("R7-8 requires EditMode.");
        int ok=0;
        void Check(bool pass,string name)
        {
            if(!pass)throw new InvalidOperationException("[R7-8] FAIL "+name);
            ok++;
        }

        var sources=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path=>new{path,skill=AssetDatabase.LoadAssetAtPath<SkillDefinition>(path)})
            .Where(x=>x.skill!=null&&x.path.Contains("/Characters/Enemies/"))
            .OrderBy(x=>x.skill.SkillId).ToArray();
        Check(sources.Length==7,"E01 seven original enemy authored skills");
        Check(R7Enemy1029NativeContract20261103.All.Count==7,
            "E02 seven GUID-approved skill identities");

        var graphs=Resources.LoadAll<R7NativeMixedAbilityDefinition>(
            "ProjectAbyss/NativeMixed");
        Check(graphs.Length>=7,"E03 R7-4 source-native mixed graph catalog available");
        Check(R7Enemy1029NativeContract20261103.All.Count(x=>x.Preparation)==1,
            "E04 elite Preparation is a different preexisting native graph");
        Check(sources.Sum(x=>x.skill.EffectEntries?.Count??0)==11,
            "E05 authored entries: six mixed/eight operations plus three Preparation");
        foreach(var authoring in R7Enemy1029NativeContract20261103.All)
        {
            var matches=sources.Where(x=>x.skill.SkillId==authoring.Id).ToArray();
            Check(matches.Length==1&&matches[0].skill.name==authoring.AssetName,
                "original skill name/ID "+authoring.Id);
            var x=matches[0];
            Check(AssetDatabase.AssetPathToGUID(x.path)==authoring.SourceGuid&&
                  !EditorUtility.IsDirty(x.skill)&&
                  x.skill.ActionType==authoring.ActionType&&
                  x.skill.EffectEntries.Count==authoring.LegacyEntries&&
                  R7Enemy1029NativeContract20261103.IsApprovedSource(x.skill),
                "original GUID/type/entries/clean "+authoring.Id);
            if(authoring.Preparation)continue;
            var matchesNative=graphs.Where(g=>g.StableId==authoring.Id).ToArray();
            Check(matchesNative.Length==1&&matchesNative[0].IsValid&&
                  matchesNative[0].SchemaVersion==2&&
                  matchesNative[0].SourceAssetGuid==authoring.SourceGuid&&
                  matchesNative[0].SourceAssetName==authoring.AssetName&&
                  matchesNative[0].Operations.Length==authoring.LegacyEntries,
                "one native graph with exact immutable identity "+authoring.Id);
            Check(matchesNative[0].Operations.Select((op,i)=>op!=null&&
                  op.EntryIndex==i&&op.Trigger==AbilityEventKind.RollUsing)
                    .All(good=>good),
                "ordered on-roll native effect operations "+authoring.Id);
        }
        var prep=R7Enemy1029NativeContract20261103.All.Single(x=>x.Preparation);
        var prepSetup=Resources.Load<R7NativePreparationSetupDefinition>(
            "ProjectAbyss/NativePreparations/EliteCombatStance_Setup");
        var prepStatuses=Resources.Load<R7NativePreparationStatusGraph20261102>(
            "ProjectAbyss/NativePreparations/EliteCombatStance_Statuses");
        Check(prepSetup!=null&&prepSetup.IsValid&&
              prepSetup.StableId==prep.Id&&
              prepSetup.SourceAssetGuid==prep.SourceGuid&&
              prepStatuses!=null&&prepStatuses.IsValid&&
              prepStatuses.StableId==prep.Id&&
              prepStatuses.SourceAssetGuid==prep.SourceGuid,
              "E06 elite Preparation source-locked native graph pair");

        var battle=new BattleContext();
        var mixed=new R7ProductionLegacyStatusMigration(battle);
        var preparation=new R7ProductionPreparationStatusMigration(battle);
        try
        {
            Check(sources.Where(x=>x.skill.ActionType!=ActionType.Preparation)
                .All(x=>mixed.CanRoute(x.skill)),
                "E07 all six existing enemy native graphs can be dispatched");
            Check(sources.Where(x=>x.skill.ActionType==ActionType.Preparation)
                .All(x=>preparation.CanRoute(x.skill)),
                "E08 native elite Preparation is already routed");
        }
        finally
        {
            mixed.Dispose();
            preparation.Dispose();
        }

        // No global quota and no forced Duel for all enemies.
        Check(!R7Enemy1029NativeContract20261103.ChooseIndependentDuel(
            .75f,.80f,true,true,false),
            "E09 duel is not forced for a sufficient-light high-cost action");
        Check(R7Enemy1029NativeContract20261103.ChooseIndependentDuel(
            .75f,.70f,true,true,false),
            "E10 independent preference can select Duel");
        Check(!R7Enemy1029NativeContract20261103.ChooseIndependentDuel(
            1f,0f,true,true,true),
            "E11 real support instruction supersedes regular Duel");
        Check(!R7Enemy1029NativeContract20261103.ChooseIndependentDuel(
            1f,0f,false,true,false),
            "E12 unavailable/unaffordable Duel never becomes a legal action");
        Check(R7Enemy1029NativeContract20261103.ChooseIndependentDuel(
            0f,1f,true,false,false),
            "E13 when only an affordable Duel exists choose that skill");
        Check(R7Enemy1029NativeContract20261103.RoleHp(EnemyRole0922.Buffer)==70&&
              R7Enemy1029NativeContract20261103.RoleHp(EnemyRole0922.Debuffer)==80&&
              R7Enemy1029NativeContract20261103.RoleHp(EnemyRole0922.Attacker)==90&&
              R7Enemy1029NativeContract20261103.RoleHp(EnemyRole0922.Duelist)==90&&
              R7Enemy1029NativeContract20261103.RoleHp(EnemyRole0922.Tank)==105,
              "E14 1029 normal enemy role-specific HP");
        Check(Canonical0922EnemyEncounterRules.RollEnemyCount(.249f)==3&&
              Canonical0922EnemyEncounterRules.RollEnemyCount(.251f)==4,
              "E15 3/4 enemy spawn probability remains 25/75");

        // Existing test SOs are not rewritten to a fictional 12-species pool.
        // A new authored skill may have 2/3/4 independent rolls at runtime.
        foreach(int count in new[]{2,3,4})
        {
            var temp=ScriptableObject.CreateInstance<SkillDefinition>();
            try
            {
                temp.SkillName="R7-8 transient roll metadata test";
                temp.ActionType=ActionType.Duel;
                temp.ExchangeRollCount=count;
                temp.Rolls.Clear();
                var runtimeType=typeof(NormalEnemy).Assembly.GetType(
                    "NormalEnemyRuntimeSkill");
                var factory=runtimeType?.GetMethod("Create",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static);
                var runtime=factory?.Invoke(null,new object[]{temp}) as Skill;
                Check(runtime!=null&&runtime.ExchangeRollCount==count,
                    "E16 roll count comes from skill definition, not 3 constant "+count);
            }
            finally { UnityEngine.Object.DestroyImmediate(temp); }
        }

        var sourceFiles=Directory.GetFiles(Application.dataPath,"*.cs",
            SearchOption.AllDirectories);
        string Read(string n)=>File.ReadAllText(sourceFiles.Single(
            x=>Path.GetFileName(x)==n));
        string dispatcher=Read("SkillEffectDispatcher.cs");
        string coordinator=Read("NormalEnemy0922RoleMechanic.cs");
        string normal=Read("NormalEnemy.cs");
        Check(dispatcher.Contains(
            "R7Enemy1029NativeContract20261103.HasKnownSkillId(")&&
            dispatcher.Contains("legacy fallback forbidden."),
            "E17 native enemy route cannot fall back to legacy TryApply");
        Check(!coordinator.Contains("RollDuelQuota(Random.value)")&&
              coordinator.Contains("R7EnemySpeciesPolicy20261103.ChooseAvailable(")&&
              coordinator.Contains("new EnemyDuelQuotaPlan0922(0,actualDuels,false)"),
              "E18 actual normal-enemy coordinator has no global duel allocation");
        Check(normal.Contains("skill.CanUseByResource(this)")&&
              coordinator.Contains("x.CanUseByResource(enemy)"),
              "E19 runtime AI does not choose unaffordable action");
        Check(coordinator.Contains("return color == SkillColor.Red;"),
              "E20 stage-1 authored normal attacks always RED");
        Check(normal.Contains("RuntimeDefinition?.EffectiveRollCount"),
              "E21 runtime roll count is not hard-coded to three");
        Check(coordinator.Contains("preserveCurrentHpRatio:true"),
              "E22 role HP remap conserves current health fraction");
        Check(!normal.Contains("new SkillEffectEntry")&&
              !coordinator.Contains("SkillEffectEntry.TryApply"),
              "E23 no new enemy legacy authoring adapter");
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==
              "Battle Test Scene"&&
              UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,
              "E24 unsaved Battle Test Scene remains Dirty");

        var enemyHpRules=Read("Canonical0922EnemyEncounterRules.cs");
        Check(!enemyHpRules.Contains("if (normalCount == 3)")&&
              !enemyHpRules.Contains("if (normalCount == 4)")&&
              Canonical0922EnemyEncounterRules.ResolveNormalEnemyHp(null,77)==77,
              "E25 1029 encounter size never normalizes 3/4-enemy HP");
        Check(AssetDatabase.FindAssets("t:CharacterData")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path=>path.Contains("/Enemies/NormalEnemy/CharacterData/"))
            .Select(path=>AssetDatabase.LoadAssetAtPath<CharacterData>(path))
            .Count(d=>d!=null&&d.maxEnergy==3)==4,
            "E26 all four authored legacy normal enemies start with 3 light");
        Debug.Log("[R7-8] Enemy native source and independent 1029 role gate PASS="+
            ok+" FAIL=0; 7 sources, 6 existing NativeMixed graphs + 1 Preparation pair."+
            " NO_R9 NO_PLAYMODE");
    }
}
#endif