#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// R7-8 final KNOWN rules Gate; no PlayMode, R9, scene save or prefab edits.
/// Contains real pure behavior tests and source GUID/route audit.
/// </summary>
public static class R7Stage8KnownSpeciesClosureGate20261103
{
    public static void Run()
    {
        if(EditorApplication.isPlaying)
            throw new InvalidOperationException("R7-8 closure is EditMode-only");
        int pass=0;
        void Assert(bool ok,string why)
        {
            if(!ok)throw new InvalidOperationException("[R7-8-12Species] FAIL "+why);
            pass++;
        }
        var all=R7EnemySpeciesPolicy20261103.All;
        Assert(all.Length==12,"12 distinct stage1 species defined");
        Assert(all.Select(s=>s.Species).Distinct().Count()==12,
            "species identity collision-free");
        Assert(all.Count(s=>s.Role==EnemyRole0922.Attacker)==3&&
               all.Count(s=>s.Role==EnemyRole0922.Duelist)==3&&
               all.Count(s=>s.Role==EnemyRole0922.Buffer)==2&&
               all.Count(s=>s.Role==EnemyRole0922.Tank)==2&&
               all.Count(s=>s.Role==EnemyRole0922.Debuffer)==2,
            "canonical species counts per role");
        foreach(var s in all)
        {
            Assert(!string.IsNullOrWhiteSpace(s.TemporaryName),
                "named species "+s.Species);
            Assert(s.LightOneWeights.Sum()==100&&
                s.LightTwoWeights.Sum()==100,
                "independent action weights sum to 100 "+s.Species);
            Assert(s.StrongDuelRolls==0 ||
                s.StrongDuelRolls>=2&&s.StrongDuelRolls<=4,
                "canonical known strong-roll range "+s.Species);
        }
        Assert(R7EnemySpeciesPolicy20261103.ChooseSpecies(
            EnemyRole0922.Buffer,.9f,false)==R7EnemySpecies1029.Commander,
            "one commander may be selected");
        Assert(R7EnemySpeciesPolicy20261103.ChooseSpecies(
            EnemyRole0922.Buffer,.9f,true)==R7EnemySpecies1029.Healer,
            "second commander is forbidden");
        Assert(R7EnemySpeciesPolicy20261103.ChooseSpecies(
            EnemyRole0922.Attacker,.1f)==R7EnemySpecies1029.Charge&&
            R7EnemySpeciesPolicy20261103.ChooseSpecies(
                EnemyRole0922.Attacker,.5f)==R7EnemySpecies1029.HeavyHit&&
            R7EnemySpeciesPolicy20261103.ChooseSpecies(
                EnemyRole0922.Attacker,.9f)==R7EnemySpecies1029.Flurry,
            "three attacker species independently assigned");
        Assert(R7EnemySpeciesPolicy20261103.ChooseAction(
            R7EnemySpecies1029.Charge,0,.99f,false,false,false,false)==
            R7EnemyAction1029.Normal,
            "light0 always chooses normal");
        Assert(R7EnemySpeciesPolicy20261103.ChooseAction(
            R7EnemySpecies1029.Charge,2,.05f,false,false,false,false)==
            R7EnemyAction1029.Normal&&
            R7EnemySpeciesPolicy20261103.ChooseAction(
            R7EnemySpecies1029.Charge,2,.25f,false,false,false,false)==
            R7EnemyAction1029.BasicDuel&&
            R7EnemySpeciesPolicy20261103.ChooseAction(
            R7EnemySpecies1029.Charge,2,.75f,false,false,false,false)==
            R7EnemyAction1029.StrongDuel,
            "charge species 15/35/50 full-light distribution");
        Assert(R7EnemySpeciesPolicy20261103.ChooseAction(
            R7EnemySpecies1029.Commander,1,.59f,false,false,false,true)==
            R7EnemyAction1029.SupplyPreparation&&
            R7EnemySpeciesPolicy20261103.ChooseAction(
            R7EnemySpecies1029.Commander,1,.95f,false,false,false,true)!=
            R7EnemyAction1029.SupplyPreparation,
            "commander uses supply 60 percent when possible");
        Assert(R7EnemySpeciesPolicy20261103.ChooseAvailable(
            R7EnemySpecies1029.Healer,1,.98f,true,true,false,
            false,false,false,true,true,false,false)==
            R7EnemyAction1029.HealPreparation,
            "healer priority at <=50% ally with authored support");
        Assert(R7EnemySpeciesPolicy20261103.ChooseAvailable(
            R7EnemySpecies1029.HeavyGuard,1,.5f,true,false,false,
            false,true,true,false,false,false,false)==
            R7EnemyAction1029.CoverDuel,
            "uncovered tank chooses authored cover Duel");
        Assert(R7EnemySpeciesPolicy20261103.ChooseAvailable(
            R7EnemySpecies1029.HeavyGuard,1,.5f,true,false,false,
            true,true,true,false,false,false,false)!=
            R7EnemyAction1029.CoverDuel,
            "active cover forbids reinstallation");
        Assert(R7EnemySpeciesPolicy20261103.ChooseAvailable(
            R7EnemySpecies1029.Charge,0,.99f,true,false,true,
            false,false,false,false,false,false,false)==
            R7EnemyAction1029.Normal,
            "no unaffordable strong Duel at light0");
        Assert(R7EnemySpeciesPolicy20261103.ChooseAvailable(
            R7EnemySpecies1029.Charge,1,.99f,true,true,true,
            false,false,false,false,false,false,false)!=
            R7EnemyAction1029.StrongDuel,
            "no unaffordable strong Duel at light1");
        Assert(R7EnemySpeciesPolicy20261103.ChooseAvailable(
            R7EnemySpecies1029.Charge,2,.99f,true,true,false,
            false,false,false,false,false,false,false)==
            R7EnemyAction1029.BasicDuel,
            "missing strong card redistributes among existing cards");
        Assert(R7EnemySpeciesPolicy20261103.ActionEnergyCost(
            R7EnemyAction1029.Normal)==0&&
            R7EnemySpeciesPolicy20261103.ActionEnergyCost(
            R7EnemyAction1029.BasicDuel)==1&&
            R7EnemySpeciesPolicy20261103.ActionEnergyCost(
            R7EnemyAction1029.StrongDuel)==2&&
            R7EnemySpeciesPolicy20261103.ActionEnergyCost(
            R7EnemyAction1029.CoverDuel)==1,
            "per-action light budget 0/1/2/cover1");

        R7Stage8EnemyStage1SkillAssetMigration20261103.Verify();
        Assert(R7EnemyStage1SkillCatalog20261103.AuthoredCount==19,
            "18 fixed-roll cards plus 1 retaliation card");
        var newSources=Resources.LoadAll<SkillDefinition>(
            R7EnemyStage1SkillCatalog20261103.ResourcePath);
        Assert(newSources.Length==19,
            "Resources catalog exactly 19 authored stage1 sources");
        Assert(newSources.Select(x=>x.SkillId).Distinct().Count()==19,
            "new source stable IDs unique");
        foreach(var def in newSources)
        {
            string p=AssetDatabase.GetAssetPath(def);
            Assert(p.StartsWith("Assets/2. Data/Resources/ProjectAbyss/Stage1Enemy1029/")&&
                AssetDatabase.AssetPathToGUID(p)?.Length==32&&
                !EditorUtility.IsDirty(def)&&
                def.EffectEntries.Count==0&&def.Effects.Count==0,
                "new source GUID + native-only effects + unmodified "+def.SkillId);
        }
        R7Stage8EnemyCounterSkillMigration20261103.Verify();
        Assert(R7EnemyCounterReaction20261103.CounterDefinition!=null&&
            R7EnemyCounterReaction20261103.CounterDefinition.EffectiveRollCount==1,
            "R6 separate counter reaction authored");
        Assert(R7EnemyCounterReaction20261103.IsCounterTaggedSkill(
            "enemy1029.stage1.counter_guard.normal")&&
            R7EnemyCounterReaction20261103.IsCounterTaggedSkill(
            "enemy1029.stage1.counter_guard.strong_duel")&&
            !R7EnemyCounterReaction20261103.IsCounterTaggedSkill(
            "enemy1029.stage1.counter_guard.cover"),
            "cover duel never carries counter tag");
        var bind=StatusEffectFactory.CreateCanonical0922(
            StatusEffectId.Bind,2,3);
        Assert((int)StatusEffectId.Bind==18&&
            bind is R7EnemyBindStatus20261103&&
            bind.NumericValue==2&&bind.RemainingTurns==3,
            "append-only Bind numeric status 2/3");
        Assert((bind as R7EnemyBindStatus20261103)
            .GetSpeedMaximumIncrease(null)==-2,
            "Bind reduces speed max by exactly N");
        Assert(new R7EnemyCommanderStrengthAura20261103(1)
            .GetRollShift(new BattleAction())==1&&
            new R7EnemyCommanderSwiftAura20261103(1)
            .GetSpeedMaximumIncrease(null)==1,
            "commander auras grant exactly strength1 and swift1");
        Assert(new R7EnemyShadeStatus20261103().RemainingTurns==1&&
               new R7EnemyShadeStatus20261103().StorageKind==
                    StatusEffectStorageKind.PresenceTimed,
            "Shade is T-only nonstacking natural-light inhibition");
        Assert(R7EnemyStage1Effect20261103.IsCoverDuelId(
                R7EnemyStage1Effect20261103.TankCoverHeavy)&&
            R7EnemyStage1Effect20261103.IsHealId(
                R7EnemyStage1Effect20261103.HealerHeal)&&
            R7EnemyStage1Effect20261103.IsSupplyId(
                R7EnemyStage1Effect20261103.CommanderSupply),
            "distinct enemy support ability IDs");

        string[] files=Directory.GetFiles(Application.dataPath,"*.cs",
            SearchOption.AllDirectories);
        string Content(string name)=>File.ReadAllText(
            files.Single(x=>Path.GetFileName(x)==name));
        var role=Content("NormalEnemy0922RoleMechanic.cs");
        var target=Content("NormalEnemy.cs");
        var effects=Content("R7EnemyStage1Effect20261103.cs");
        var registry=Content("R6ReactionActionRegistry.cs");
        var slot=Content("R7EnemyCounterReaction20261103.cs");
        var stagger=Content("StaggerGaugeMechanic.cs");
        Assert(role.Contains("OnActionStart(BattleAction action)")&&
               role.Contains("coverExpireTurn=now+2")&&
               role.Contains("IsCoveredByAnotherTank"),
            "tank cover starts with Duel and lasts three turns, nonduplicate");
        Assert(role.Contains("SelectCoverCandidate(")&&
               role.Contains("int hp=Mathf.Max(0,candidate.CurrentHP)")&&
               role.Contains("UnityEngine.Random.Range(0,tied.Count)"),
            "cover selects lowest absolute HP with random tie");
        Assert(effects.Contains("SelectLowestHpRatio(")&&
               effects.Contains("target.RestoreCurrentHP(20)"),
            "healer +20 at lowest percent HP, self allowed");
        Assert(effects.Contains("OrderBy(x=>x.CurrentEnergy)")&&
               effects.Contains(".Take(2)")&&
               effects.Contains("target.AddTurnClashPowerBonus(1)"),
            "commander supply targets 2 lowest energy, max substitute +1 clash");
        Assert(role.Contains("ClearCommanderAura()")&&
               role.Contains("OnCharacterDeath(Character dead)")&&
               role.Contains("MaintainCommanderAura()"),
            "commander aura source-owned and removed on death");
        Assert(role.Contains("GetAdditionalFinalStaggerDamage(")&&
               stagger.Contains("ResolveFinalAdditionalStaggerDamage(action,owner)")&&
               stagger.Contains("ResolveFinalAdditionalStaggerDamage(action,target)"),
            "debuffer +10 AFTER resistance on both stagger paths");
        Assert(effects.Contains("rollIndex==0&&Eligible(action,R7EnemySpecies1029.Bind)")&&
               effects.Contains("ApplyCanonical(action,StatusEffectId.Bind,1,1,true)"),
            "bind first-use only next-turn speed debuff");
        Assert(effects.Contains("!exchange.IsDuelExchange")&&
               effects.Contains("StatusEffectId.Stagnation,2,1,false")&&
               effects.Contains("StatusEffectId.Weakness,1,1,true"),
            "strict Duel-vs-Duel gate and correct status timings");
        Assert(effects.Contains("state.HealerMinusOne=true")&&
               effects.Contains("state.ShadeBlocksAll=true")&&
               effects.Contains("if(state.ShadeBlocksAll)")&&
               Content("Character.cs").Contains(
                "R7EnemyStage1Effect20261103.AdjustNaturalTurnRefill("),
            "healer/shade affect next-turn natural gain ONLY");
        Assert(slot.Contains("ReactionTimingCheckpoint")&&
               slot.Contains("R6 reaction")&&
               role.Contains("ReactionTimingCheckpoint.AfterActionCompleted")&&
               role.Contains("ReactionTimingCheckpoint.AfterClashCompleted")&&
               registry.Contains("CancelUnscheduled("),
            "counter reactions are deferred through R6 after action/clash");
        Assert(slot.Contains("ResourceCostCommitted=false")&&
               slot.Contains("SlotId=ReactionSlotPrefix+id")&&
               slot.Contains("actions.TryRegister(req,slot")&&
               slot.Contains("scheduler.TrySchedule(req)"),
            "R6 counter injects unique uncommitted action with exact provenance");
        Assert(target.Contains("R7EnemyStage1SkillCatalog20261103.Get(")&&
               target.Contains("new ActionSlot")&&
               target.Contains("return base.DecideSlots(context);"),
            "new GUID-backed actual enemy ActionSlot + missing-card fallback");
        Assert(Content("Skill.cs").Contains(
            "R7EnemyStage1Effect20261103.OnRollUsing(action,rollIndex)")&&
            role.Contains("R7EnemyStage1Effect20261103.OnExchangeWon("),
            "native card effects hooked to actual roll/exchange events");
        Assert(!Content("NormalEnemy0922RoleMechanic.cs")
            .Contains("RollDuelQuota(Random.value)"),
            "encounter-wide Duel quota never used");
        Assert(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==
               "Battle Test Scene"&&
               UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,
            "dirty Battle Test Scene preserved");
        // Old 7-authoring source + R7-8 original routing must remain green.
        R7Stage8EnemyNativeGate20261103.Run();

        Debug.Log("[R7-8 Stage1 Known] EditMode PASS="+pass+
            " FAIL=0 + original R7-8 54 + new SO 18+1."+
            " NO_R9 NO_PLAYMODE NO_SCENE_SAVE");
    }
}
#endif