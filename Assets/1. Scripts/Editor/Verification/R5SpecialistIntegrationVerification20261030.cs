#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// R5 opt-in specialist integration smoke. Mutations occur only in Play Mode;
/// no prefabs, SO, or scenes are saved. Stop Play Mode to reset actors.
/// </summary>
public static class R5SpecialistIntegrationVerification20261030
{
    private static CombatCommandEnvelope Cmd(
        string id, AbilityInstance ability, CombatCommandKind kind,
        long action, long cause, Character target, BodyPart part, int amount)
    {
        return new CombatCommandEnvelope(id,ability.Identity,kind,action,cause,
            new CombatTargetRef(R4LiveSlotQuery.ActorRuntimeId(target),
                part?.PartId, null),amount,0,0,0,0);
    }

    private static AbilityEventContext Ev(long action, long cause, Character owner) =>
        new AbilityEventContext(AbilityEventKind.RollUsing,action,cause,
            R4LiveSlotQuery.ActorRuntimeId(owner),0,false,false);

    [MenuItem("Game System Verification/R5 - Specialist Real Services (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)
            throw new InvalidOperationException("R5 real specialist smoke requires Play Mode.");
        var mgr=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var ctx=mgr?.BattleContext;
        var player=ctx?.Player;
        var normal=ctx?.Enemies?.FirstOrDefault(c=>c!=null&&!c.IsDead&&!c.UsesBodyParts);
        var elite=ctx?.Enemies?.FirstOrDefault(c=>c!=null&&!c.IsDead&&c.UsesBodyParts);
        var elitePart=elite?.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part;
        if(player==null||normal==null||elitePart==null ||
           ctx.ResolveDamageManager()==null || ctx.EffectResolver==null)
            throw new InvalidOperationException("Battle test roster/services unavailable.");

        int pass=0,fail=0;
        void Check(string name,bool ok,string detail="")
        {
            if(ok){pass++;Debug.Log("[R5-Real] PASS "+name+" "+detail);}
            else{fail++;Debug.LogError("[R5-Real] FAIL "+name+" "+detail);}
        }
        var backend=new R5CombatSpecialistBackend(ctx);
        var svc=new CombatCommandApplyService();
        AbilityInstance NewAbility() => new AbilityInstance(
            new AbilityDefinitionRef("R5-REAL-VERIFY","runtime-test"),
            new AbilityIdentity("R5-REAL-VERIFY",
                R4LiveSlotQuery.ActorRuntimeId(player),"SLOT",0));

        Check("S01 services available and unsupported kind rejected",
            backend.CanApply(CombatCommandKind.DealDamage) &&
            backend.CanApply(CombatCommandKind.ApplyCharacterStatus) &&
            backend.CanApply(CombatCommandKind.ChangeResource) &&
            !backend.CanApply(CombatCommandKind.ScheduleAbility));

        // A unique runtime-only key: no content/asset definitions are changed.
        const string resourceKey="R5_SPECIALIST_TEST_ONLY";
        SkillResourceAccess.Set(player,resourceKey,3);
        int beforeResource=SkillResourceAccess.Get(player,resourceKey);
        var a1=NewAbility();var e1=Ev(19001,29001,player);
        var c1=Cmd("r5-real-resource",a1,CombatCommandKind.ChangeResource,
            e1.ActionId,e1.CauseId,player,null,2);
        backend.RegisterResourceChange(c1,player,resourceKey);
        var r1=svc.Apply(a1,e1,new[]{c1},backend);
        int afterResource=SkillResourceAccess.Get(player,resourceKey);
        Check("S02 resource specialist reads actual delta",
            afterResource-beforeResource==2 &&
            r1.CommittedCount==1 && r1.Records[0].Receipt.AppliedAmount==2,
            "before="+beforeResource+" after="+afterResource);
        var retry=svc.Apply(a1,e1,new[]{c1},backend);
        Check("S03 duplicate source command does not apply again",
            SkillResourceAccess.Get(player,resourceKey)==afterResource &&
            retry.Records[0].Receipt.Outcome==CombatCommandOutcome.SkippedDuplicate);

        var a2=NewAbility();var e2=Ev(19002,29002,player);
        var c2=Cmd("r5-real-character-status",a2,CombatCommandKind.ApplyCharacterStatus,
            e2.ActionId,e2.CauseId,normal,null,1);
        var effect2=StatusEffectFactory.Create(StatusEffectId.Burn,1,2);
        backend.RegisterCharacterStatus(c2,EffectRequest.CharacterStatus(
            player,normal,effect2));
        var r2=svc.Apply(a2,e2,new[]{c2},backend);
        Check("S04 character status delegated to BattleEffectResolver",
            r2.Records.Count==1 &&
            r2.Records[0].Receipt.Detail.Contains("BattleEffectResolver") &&
            (r2.CommittedCount==1)==
              StatusEffectFactory.HasStatus(normal,null,StatusEffectId.Burn,true,false),
            "outcome="+r2.Records[0].Receipt.Outcome);

        var a3=NewAbility();var e3=Ev(19003,29003,player);
        var c3=Cmd("r5-real-part-status",a3,CombatCommandKind.ApplyPartStatus,
            e3.ActionId,e3.CauseId,elite,elitePart,1);
        var effect3=StatusEffectFactory.Create(StatusEffectId.Burn,1,2);
        backend.RegisterPartStatus(c3,EffectRequest.BodyPartStatus(player,elite,elitePart,effect3));
        var r3=svc.Apply(a3,e3,new[]{c3},backend);
        Check("S05 part status delegated with exact part target",
            r3.Records.Count==1 &&
            r3.Records[0].Receipt.Detail.Contains("BattleEffectResolver") &&
            (r3.CommittedCount==1)==StatusEffectFactory.HasStatus(
                elite,elitePart,StatusEffectId.Burn,false,true),
            "outcome="+r3.Records[0].Receipt.Outcome);

        var a4=NewAbility();var e4=Ev(19004,29004,player);
        var c4=Cmd("r5-real-damage",a4,CombatCommandKind.DealDamage,
            e4.ActionId,e4.CauseId,normal,null,1);
        var request4=DamageRequest.Direct(player,normal,1);
        backend.RegisterDamage(c4,request4);
        int beforeHp=normal.CurrentHP;
        var r4=svc.Apply(a4,e4,new[]{c4},backend);
        int afterHp=normal.CurrentHP;
        Check("S06 damage goes through DamageManager.Result",
            r4.Records.Count==1 &&
            r4.Records[0].Receipt.Detail.Contains("DamageManager.Result") &&
            r4.Records[0].Receipt.AppliedAmount>=0,
            "hp="+beforeHp+"->"+afterHp+";outcome="+r4.Records[0].Receipt.Outcome);
        Check("S07 damage receipt matches observed HP or absorption",
            afterHp<=beforeHp &&
            (beforeHp-afterHp<=r4.Records[0].Receipt.AppliedAmount ||
             r4.Records[0].Receipt.AbsorbedAmount>0));
        var againDamage=svc.Apply(a4,e4,new[]{c4},backend);
        Check("S08 duplicate damage command cannot call specialist twice",
            normal.CurrentHP==afterHp &&
            againDamage.Records[0].Receipt.Outcome==CombatCommandOutcome.SkippedDuplicate);

        var a5=NewAbility();var e5=Ev(19005,29005,player);
        var invalid=Cmd("r5-misbound",a5,CombatCommandKind.DealDamage,
            e5.ActionId,e5.CauseId,player,null,1);
        bool rejectedBinding=false;
        try{backend.RegisterDamage(invalid,DamageRequest.Direct(player,normal,1));}
        catch(ArgumentException){rejectedBinding=true;}
        Check("S09 mismatch target blocked before specialist call",rejectedBinding);

        var noPayload=Cmd("r5-no-payload",a5,CombatCommandKind.ChangeResource,
            e5.ActionId,e5.CauseId,player,null,1);
        var reject=svc.Apply(a5,e5,new[]{noPayload},backend);
        Check("S10 unbound payload is rejected without mutation",
            reject.CommittedCount==0 && reject.Records[0].Receipt.Outcome==CombatCommandOutcome.Rejected);

        Check("S11 at most one activation recorded per successful command",
            a1.RuntimeState.SuccessfulActivationCount==1 &&
            a4.RuntimeState.SuccessfulActivationCount<=1);
        backend.ClearBindings();
        Check("S12 backend cleanup releases request bindings",
            backend.BoundCommandCount==0);

        Debug.Log("[R5-Real] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R5 specialist real service verification failed "+fail);
    }
}
#endif
