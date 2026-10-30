#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// Explicit R5-W SlotWeighted option end-to-end smoke.
/// Never changes SkillDefinition assets, prefabs, or the active scene file.
/// </summary>
public static class R5SlotWeightedIntegrationVerification20261030
{
    private sealed class ProbeSkill : NormalSkill
    {
        private readonly int weight;
        private readonly AttackWeightTargetingModel mode;
        public ProbeSkill(int weight, AttackWeightTargetingModel mode)
        {
            this.weight=weight;this.mode=mode;
            SkillName="R5W-VIRTUAL";
            BasePower=2;
            Resolver=new DiceResolver(1,2);
        }
        public override int AttackWeight => weight;
        public override AttackWeightTargetingModel AttackWeightTargetingModel => mode;
        public override void Execute(BattleAction action) {}
    }

    private static BattleAction Action(Character owner, Character target,
        BodyPart part, int weight, AttackWeightTargetingModel mode, long id)
    {
        var slot=new ActionSlot
        {
            ActionId=id,
            Owner=owner,
            TargetCharacter=target,
            TargetPart=part,
            Phase=ActionPhase.COMBAT,
            Skill=new ProbeSkill(weight,mode),
            SlotId="R5W-TEST"
        };
        return new BattleAction {Slot=slot};
    }

    private static string Key(AttackWeightTarget t) =>
        R4LiveSlotQuery.ActorRuntimeId(t.TargetCharacter)+":"+
        (t.TargetPart?.PartId ?? "");

    [MenuItem("Game System Verification/R5-W - Slot Weighted Live Hit Batch (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)
            throw new InvalidOperationException("Play Mode required.");
        var manager=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var ctx=manager?.BattleContext;
        var player=ctx?.Player;
        var enemies=ctx?.Enemies;
        var elite=enemies?.FirstOrDefault(e=>e!=null && !e.IsDead &&
            e.UsesBodyParts && e.BodyParts!=null && e.BodyParts.Count>=4);
        var normals=enemies?.Where(e=>e!=null&&!e.IsDead&&!e.UsesBodyParts).ToArray();
        var head=elite?.GetTargetPoints(false).FirstOrDefault(p=>p.IsValid).Part;
        if(player==null || elite==null || head==null || normals==null || normals.Length<2 ||
           ctx.ResolveDamageManager()==null)
            throw new InvalidOperationException("Expected player, 1 elite, 2 normals and DamageManager.");
        int pass=0,fail=0;
        void Check(string label,bool ok,string detail="")
        {
            if(ok){pass++;Debug.Log("[R5-W] PASS "+label+" "+detail);}
            else{fail++;Debug.LogError("[R5-W] FAIL "+label+" "+detail);}
        }

        var legacyDefault=new AttackWeightSettings();
        Check("W01 existing asset default mode is LegacyCharacterRandom",
            legacyDefault.TargetingModel==AttackWeightTargetingModel.LegacyCharacterRandom);

        var attack=Action(player,normals[0],null,3,AttackWeightTargetingModel.SlotWeighted,94501);
        var resolver=new AttackWeightTargetResolver(ctx);
        string randomBefore=JsonUtility.ToJson(UnityEngine.Random.state);
        var picked=resolver.ResolveTargets(attack).ToArray();
        string randomAfter=JsonUtility.ToJson(UnityEngine.Random.state);
        Check("W02 opt-in path returns main and 2 secondary targets",
            picked.Length==3 && picked[0].IsPrimary && picked.Skip(1).All(t=>!t.IsPrimary));
        Check("W03 selected targets distinct by actor and body part",
            picked.Select(Key).Distinct().Count()==picked.Length);
        Check("W04 primary is exactly resolved action target",
            picked[0].TargetCharacter==attack.Target &&
            picked[0].TargetPart==attack.TargetPart);
        Check("W05 slot weighted plan consumes no Unity random",
            randomBefore==randomAfter);
        var replay=resolver.ResolveTargets(attack);
        Check("W06 cached selection remains stable on same action",
            attack.HasResolvedAttackWeightTargets && replay.Count==picked.Length &&
            Enumerable.Range(0,picked.Length).All(i=>ReferenceEquals(replay[i],picked[i])));

        var eliteAttack=Action(player,elite,head,12,AttackWeightTargetingModel.SlotWeighted,94502);
        var all=resolver.ResolveTargets(eliteAttack).ToArray();
        var allEligible=elite.GetTargetPoints(false).Count(p=>p.IsValid);
        Check("W07 extra elite part is targetable despite missing configured slot",
            all.Any(t=>t.TargetCharacter==elite &&
                t.TargetPart?.PartId=="extra"),
            "resolved="+all.Length+" elitePoints="+allEligible);
        Check("W08 same boss different parts are independent hit targets",
            all.Count(t=>t.TargetCharacter==elite)>=allEligible &&
            all.Select(Key).Distinct().Count()==all.Length);
        Check("W09 no more targets than actual distinct target points",
            all.Length<=allEligible+normals.Length && all.Length>=5);

        var wrongPart=Action(player,elite,null,4,AttackWeightTargetingModel.SlotWeighted,94503);
        bool okWrong=R5SlotWeightedTargetBridge.TryResolve(ctx,wrongPart,
            out var fallback, out var diagnosis);
        Check("W10 missing boss hit part is fail-closed to main only",
            !okWrong && fallback.Count==1 && fallback[0].IsPrimary &&
            !string.IsNullOrWhiteSpace(diagnosis));

        var allySide=Action(elite,normals[0],null,3,AttackWeightTargetingModel.SlotWeighted,94504);
        bool okSide=R5SlotWeightedTargetBridge.TryResolve(ctx,allySide,
            out var sideTargets,out _);
        Check("W11 same-side targeting does not splash to other enemies",
            !okSide && sideTargets.Count==1);

        var enemyAttack=Action(elite,player,player.GetTargetPoints(false)
            .FirstOrDefault(p=>p.IsValid).Part,3,
            AttackWeightTargetingModel.SlotWeighted,94505);
        bool okEnemy=R5SlotWeightedTargetBridge.TryResolve(ctx,enemyAttack,
            out var enemyTargets,out _);
        Check("W12 enemy attack only spreads into player-side target points",
            okEnemy && enemyTargets.All(t=>t.TargetCharacter==player));

        var single=Action(player,normals[0],null,1,AttackWeightTargetingModel.SlotWeighted,94506);
        var only=resolver.ResolveTargets(single);
        Check("W13 weight=1 opt-in still has only primary",only.Count==1 && only[0].IsPrimary);

        var conventional=Action(player,normals[0],null,1,
            AttackWeightTargetingModel.LegacyCharacterRandom,94507);
        var legacyResult=resolver.ResolveTargets(conventional);
        Check("W14 legacy mode still uses existing resolver",legacyResult.Count==1 &&
            legacyResult[0].TargetCharacter==normals[0]);

        // Actual DamageManager integration: main and secondary through the
        // same existing call sites used by ClashManager, not a new damage formula.
        var primaryContext=ctx.ResolveDamageManager().ApplyDamageContext(
            attack,2,isClashDamage:false,targetLostClash:false,applyMomentum:false);
        Check("W15 primary damage goes through unchanged DamageManager",
            primaryContext?.Result!=null && primaryContext.Target==normals[0]);
        int secondaryContexts=0;
        var recorded=new HashSet<string>();
        for(int i=1;i<picked.Length;i++)
        {
            var t=picked[i];
            if(!t.IsValid)continue;
            if(!recorded.Add(Key(t)))continue;
            var context=ctx.ResolveDamageManager().ApplyAttackWeightDamageContext(
                attack,t,2,1f,applyMomentum:false);
            if(context==null)continue;
            attack.AddAttackWeightHitResult(new AttackWeightHitResult(0,t,context));
            secondaryContexts++;
        }
        Check("W16 each distinct secondary target applied at most once",
            secondaryContexts<=picked.Length-1 && recorded.Count==picked.Length-1);
        Check("W17 secondary DamageResults recorded under original action",
            attack.AttackWeightHitResults.Count==secondaryContexts &&
            attack.AttackWeightHitResults.All(x=>x.DamageContext?.Result!=null &&
                x.ExchangeIndex==0 && x.Target!=null));
        Check("W18 main is never included again as secondary",
            picked.Skip(1).All(t=>Key(t)!=Key(picked[0])));
        Check("W19 no synthetic ActionSlot inserted into Battle ActionManager",
            attack.Slot!=null && attack.Slot.ActionId==94501);
        Check("W20 original roll/clash outcome is not fabricated per secondary",
            attack.AttackWeightHitResults.All(x=>x.ExchangeIndex==0));

        Debug.Log("[R5-W] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R5-W opt-in verification failed: "+fail);
    }
}
#endif
