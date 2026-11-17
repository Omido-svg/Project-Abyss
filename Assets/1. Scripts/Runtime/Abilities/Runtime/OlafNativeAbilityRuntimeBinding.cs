using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R7-5 direct Olaf effect executor. Runtime never reads effect-template
    /// operations/amounts/conditions, invokes legacy Apply/TryApply or delegates
    /// to the migration pilot adapter. OlafRulebreakerMechanic and
    /// OlafMadnessMechanic are authoritative gameplay domain services.
    /// </summary>
    public sealed class OlafNativeAbilityRuntimeBinding : IDisposable
    {
        private static Dictionary<string,OlafNativeAbilityDefinition> catalog;
        private static readonly HashSet<string> duplicates=new HashSet<string>(
            StringComparer.Ordinal);
        private bool disposed;
        private readonly HashSet<string> completed=new HashSet<string>(
            StringComparer.Ordinal);
        // Per-battle active regeneration caused by this specific Preparation,
        // not a generic global HasRegeneration check.
        private readonly Dictionary<Character,StatusEffect> bloodOathRegen=
            new Dictionary<Character,StatusEffect>();
        public int AppliedCount {get;private set;}
        public bool Enabled {get;set;}=true;
        private static Dictionary<string,OlafNativeAbilityDefinition> Catalog
        {
            get
            {
                if(catalog!=null)return catalog;
                var found=new Dictionary<string,OlafNativeAbilityDefinition>(
                    StringComparer.Ordinal);
                duplicates.Clear();
                foreach(var g in Resources.LoadAll<OlafNativeAbilityDefinition>(
                    "ProjectAbyss/NativeOlaf").Concat(
                    Resources.LoadAll<OlafNativeAbilityDefinition>(
                        "ProjectAbyss/NativeOlafCanon")))
                {
                    if(g==null||!g.IsValid)
                        continue;
                    if(!found.TryAdd(g.StableId,g))duplicates.Add(g.StableId);
                }
                foreach(var id in duplicates)found.Remove(id);
                catalog=found;
                return catalog;
            }
        }
        public static int RegisteredCount=>Catalog.Count;
        public static void ResetCatalogCache(){catalog=null;duplicates.Clear();}
        public static bool IsApprovedSource(SkillDefinition d) =>
            d!=null && Catalog.TryGetValue(d.SkillId,out var graph) && graph.SourceAssetName==d.name;

        // Two canonical bleed-explosion duel cards are handled by
        // OlafMadnessMechanic.OnExchangeResolved. Their old TempBalance
        // TargetBleeding entries must NOT add a second card effect.
        public static bool IsNativePrestigeSource(SkillDefinition d) =>
            d!=null && ((d.SkillId=="olaf.prestige.rune_carving" && d.name=="Olaf_R_01") ||
                        (d.SkillId=="olaf.prestige.berserk" && d.name=="Olaf_R_02") ||
                        (d.SkillId=="olaf.prestige.ragnarok" && d.name=="Olaf_R_03"));

        public static bool IsNativeExplosionSource(SkillDefinition d) =>
            d!=null && d.Rulebreaker?.Enabled==true &&
            d.Rulebreaker.ExplodeBleedingOncePerAction &&
            ((d.SkillId=="olaf.duel.standard" && d.name=="Olaf_D_01") ||
             (d.SkillId=="olaf.duel.single_cut" && d.name=="Olaf_D_03"));

        public static bool HasThirdRollDebuffPower(SkillDefinition d) =>
            IsApprovedSource(d) &&
            Catalog[d.SkillId].Operations.Any(e=>e.Operation==
                OlafNativeOperation.PowerPerTargetNegativeStatusOnThirdRoll &&
                e.RestrictToRoll&&e.RollNumber==3 &&
                e.Timing==SkillEffectTiming.OnExecute);

        public static bool HasExploitGapChain(SkillDefinition d) =>
            IsApprovedSource(d)&&Catalog[d.SkillId].Operations.Any(
                x=>x.Operation==
                    OlafNativeOperation.ChainExtraOneSidedAttackOnTransition);

        public static bool HasNoRetreatWeakenPower(SkillDefinition d) =>
            IsApprovedSource(d)&&
            Catalog[d.SkillId].Operations.Any(e=>e.Operation==
                OlafNativeOperation.PowerPerWeakenedOwnerPartEachRoll);

        public bool CanRoute(SkillDefinition d) =>
            Enabled&&!disposed&&IsApprovedSource(d);

        public IReadOnlyList<SkillEffectResult> Execute(SkillDefinition d,
            SkillEffectContext context,SkillEffectTiming timing)
        {
            if(!CanRoute(d))
                throw new InvalidOperationException("R7-5 native Olaf source unavailable");
            var graph=Catalog[d.SkillId];
            var results=new List<SkillEffectResult>(graph.Operations.Length);
            if(d.SkillId=="olaf.preparation.blood_oath" &&
               timing==SkillEffectTiming.OnExecute &&
               !CanApplyBloodOathPreparation(context?.Owner))
            {
                for(int j=0;j<graph.Operations.Length;j++)
                    results.Add(SkillEffectResult.ConditionFailed(
                        j<d.EffectEntries.Count?d.EffectEntries[j]?.Definition:null,
                        timing,context,"Requires HP below 70% and no active regeneration from this card."));
                return results;
            }
            for(int i=0;i<graph.Operations.Length;i++)
            {
                var op=graph.Operations[i];
                // UI diagnostic identity only. No effect-template Apply or
                // effect-template gameplay parameters are ever consulted.
                var diagnostic=i<d.EffectEntries.Count?
                    d.EffectEntries[i]?.Definition:null;
                if(op.Timing!=timing||
                   (op.RestrictToRoll&&(context==null||
                      context.RollNumber!=op.RollNumber)))
                {
                    results.Add(SkillEffectResult.NotScheduled(diagnostic,timing));
                    continue;
                }
                var action=context?.Action;
                if(action==null||context.Owner==null||
                    context.Owner.GetMechanic<OlafRulebreakerMechanic>()==null||
                    context.Owner.GetMechanic<OlafMadnessMechanic>()==null)
                {
                    results.Add(SkillEffectResult.Failed(diagnostic,timing,context,
                        "R7-5 native Olaf requires action and specialist services."));
                    continue;
                }
                // Do not apply the same source event twice. Event callback
                // repetition is distinct from another roll/exchange.
                var token=graph.SourceAssetGuid+"|"+action.ActionId+"|"+i+"|"+
                    timing+"|"+context.RollIndex+"|"+context.UseCountThisTurn;
                if(completed.Contains(token))
                {
                    results.Add(SkillEffectResult.NotScheduled(diagnostic,timing));
                    continue;
                }
                bool applied=context.BattleContext.Abilities.ExecuteSpecialist(graph.StableId,graph.SourceAssetGuid,i,context,true,_=>Apply(op,context));
                if(applied)
                {
                    completed.Add(token);
                    AppliedCount++;
                    if(d.SkillId=="olaf.preparation.blood_oath" &&
                       op.Operation==OlafNativeOperation.OwnerStatusImmediate && op.StatusId==StatusEffectId.Regeneration)
                        bloodOathRegen[context.Owner]=
                            (context.Owner.ResolveHealingPart(action.OwnerPart)?.StatusEffects ?? context.Owner.StatusEffects)?.LastOrDefault(
                                x=>x is RegenerationStatus);
                }
                results.Add(applied?SkillEffectResult.Applied(diagnostic,timing,context):
                    SkillEffectResult.ConditionFailed(diagnostic,timing,context,
                        "R7-5 specialist operation prerequisites not met."));
            }
            return results;
        }

        private bool CanApplyBloodOathPreparation(Character owner)
        {
            if(owner==null||owner.MaxCombatHP<=0||
               (long)owner.CurrentHP*10>= (long)owner.MaxCombatHP*7)return false;
            if(!bloodOathRegen.TryGetValue(owner,out var status)||status==null)
                return true;
            var statuses=status.OwnerPart?.StatusEffects ?? owner.StatusEffects;
            if(statuses!=null && statuses.Contains(status) && !status.IsExpired)
                return false;
            bloodOathRegen.Remove(owner);
            return true;
        }

        private static bool Apply(OlafNativeEntry op,
            SkillEffectContext context)
        {
            var owner=context.Owner;
            var target=context.Target;
            var part=context.TargetPart;
            var action=context.Action;
            var rule=owner.GetMechanic<OlafRulebreakerMechanic>();
            var madness=owner.GetMechanic<OlafMadnessMechanic>();
            int value=Mathf.Max(0,op.Amount);
            switch(op.Operation)
            {
                case OlafNativeOperation.TargetBleedingRandomOneOrTwo:
                    if(target==null||target.IsDead)return false;
                    rule.AddBleeding(target,part,UnityEngine.Random.Range(1,3),action);
                    return true;
                case OlafNativeOperation.GainEnergy:
                    if(value==0)return false;
                    owner.AddEnergy(value,CombatResourceChangeReason.SkillEffect,
                        action,action.Skill);
                    return true;
                case OlafNativeOperation.CalmMadnessHealAndPrestige:
                    if(owner.MaxCombatHP<=0||
                        (long)owner.CurrentHP*10>=(long)owner.MaxCombatHP*7||
                        !madness.TryConsumeMadness(2))return false;
                    owner.RestoreCurrentHP(30,action?.OwnerPart);
                    owner.AddPrestige(10);
                    return true;
                case OlafNativeOperation.DevourStaggerHeal:
                    if(owner.MaxCombatHP<=0||
                        (long)owner.CurrentHP*10>=(long)owner.MaxCombatHP*7)return false;
                    var stagger=owner.GetMechanic<StaggerGaugeMechanic>();
                    if(stagger==null||stagger.CurrentGauge<20||
                        stagger.SacrificeGauge(20)!=20)return false;
                    owner.RestoreCurrentHP(30,action?.OwnerPart);
                    return true;
                case OlafNativeOperation.OwnerStaggerDamage:
                    var ownStagger=owner.GetMechanic<StaggerGaugeMechanic>();
                    return ownStagger!=null&&ownStagger.SacrificeGauge(value)>0;
                case OlafNativeOperation.PowerPerWeakenedOwnerPartEachRoll:
                    // Recomputed when this exact roll is judged/damaged.
                    return true;
                case OlafNativeOperation.MadnessGainWithRagnarokLoss:
                    madness.AddMadness(
                        owner.GetMechanic<OlafImmortalFuryMechanic>()?.IsActive==true?3:1);
                    return true;
                case OlafNativeOperation.ChainExtraOneSidedAttackOnTransition:
                    // ClashManager extends the already resolved exchange by
                    // one-sided actions through its normal RNG/damage pipeline.
                    var dealt=context.DamageContext;
                    return dealt!=null&&(dealt.WasWeakened||
                        dealt.WasBroken||dealt.OpenedVulnerabilityWindow||
                        dealt.WasKilled);
                case OlafNativeOperation.ActivateAdjustedMaxSpeedIfLastStand:
                    if(!rule.CanUse(context.SkillDefinition))return false;
                    rule.ActivateCanonicalLastResistance();
                    return true;
                case OlafNativeOperation.ScheduleNextTurnMaxSlotSpeed:
                    return rule.ScheduleNextTurnMaximumSlotSpeed(action);
                case OlafNativeOperation.ProtectLowestHpOwnerPartForTurn:
                    return rule.ProtectLowestHpOwnerPart(action);
                case OlafNativeOperation.ConsumeOwnerBleedingForTurnPower:
                    int consumed=rule.ConsumeOwnerBleedingUpTo(4);
                    if(consumed==0)return false;
                    rule.GrantTurnPowerBonus(consumed>=3?2:1);
                    return true;
                case OlafNativeOperation.PowerPerTargetNegativeStatusOnThirdRoll:
                    // Pure per-roll power is evaluated by OlafRulebreakerMechanic.
                    // Never persist a flat bonus onto the whole BattleAction.
                    return target!=null&&!target.IsDead;
                case OlafNativeOperation.FortifyConsumeBleedingForStrength:
                    if(target==null||!rule.TryConsumeBleeding(target,part,2))return false;
                    return ApplyStatus(context,owner,null,StatusEffectId.Strength,1,1,false);
                case OlafNativeOperation.FortifyConsumeBleedingForSwift:
                    if(target==null||!rule.TryConsumeBleeding(target,part,2))return false;
                    return ApplyStatus(context,owner,null,StatusEffectId.Swift,1,2,false);
                case OlafNativeOperation.FortifyPrestigeIfBuffsPresent:
                    if(StatusEffectFactory.GetNumericTotal(owner,null,
                            StatusEffectId.Strength)<1 ||
                       StatusEffectFactory.GetNumericTotal(owner,null,
                            StatusEffectId.Swift)<1) return false;
                    owner.AddPrestige(10);
                    return true;
                case OlafNativeOperation.TargetBleeding:
                    if(target==null||target.IsDead||value==0)return false;
                    rule.AddBleeding(target,part,value,action);
                    return true;
                case OlafNativeOperation.OwnerBleeding:
                    if(value==0)return false;
                    // 1029 canonical self-bleed targets the skill slot's own part.
                    rule.AddBleeding(owner,action?.OwnerPart,value,action);
                    return true;
                case OlafNativeOperation.AddMadness:
                    if(value==0)return false;
                    madness.AddMadness(value);
                    return true;
                case OlafNativeOperation.OwnerBlock:
                    if(value==0)return false;
                    owner.AddBlock(value);
                    return true;
                case OlafNativeOperation.ClearTargetBlock:
                    if(target==null||target.IsDead)return false;
                    target.ClearBlock();
                    return true;
                case OlafNativeOperation.RandomTargetDebuffNextTurn:
                    if(target==null||target.IsDead)return false;
                    // 1029 canon: choose Rupture or Weakness now, never at
                    // next-turn materialization (legacy had a third option).
                    return ApplyStatus(context,target,part,
                        UnityEngine.Random.Range(0,2)==0?
                            StatusEffectId.Rupture:StatusEffectId.Weakness,1,1,true);
                case OlafNativeOperation.TargetStatusNextTurn:
                    return ApplyStatus(context,target,part,op.StatusId,
                        value,op.Duration,true);
                case OlafNativeOperation.OwnerStatusNextTurn:
                    return ApplyStatus(context,owner,null,op.StatusId,
                        value,op.Duration,true);
                case OlafNativeOperation.OwnerStatusImmediate:
                    return ApplyStatus(context,owner,null,op.StatusId,
                        value,op.Duration,false);
                case OlafNativeOperation.TargetStatusNextTurnIfBleedingAtLeast:
                    if(target==null||rule.GetBleeding(target,part)<op.MinimumBleeding)
                        return false;
                    return ApplyStatus(context,target,part,op.StatusId,
                        value,op.Duration,true);
                case OlafNativeOperation.StrengthByTargetBleedingBandNextTurn:
                    if(target==null)return false;
                    int amount=rule.GetBleeding(target,part);
                    return ApplyStatus(context,owner,null,StatusEffectId.Strength,
                        amount>=10?3:amount>=5?2:1,op.Duration,true);
                case OlafNativeOperation.BloodPriceStrengthByOwnerBleeding:
                    int own=rule.GetOwnerBleedingTotal();
                    return ApplyStatus(context,owner,null,StatusEffectId.Strength,
                        own>=10?3:own>=5?2:1,3,true);
                case OlafNativeOperation.ConsumeTargetBleedingForEnergy:
                    if(target==null||!rule.TryConsumeBleeding(target,part,
                       Mathf.Max(1,value)))return false;
                    owner.AddEnergy(2,CombatResourceChangeReason.SkillEffect,
                        action,action?.Skill);
                    return true;
                case OlafNativeOperation.PrepareBloodOathDuel:
                    rule.PrepareBloodOathDuel(action);
                    return true;
                case OlafNativeOperation.ResolveBloodOathDuelWin:
                    rule.ResolveBloodOathDuelWin(action,context.RollNumber);
                    return true;
                default:throw new InvalidOperationException(
                    "Unknown R7-5 native Olaf operation.");
            }
        }

        private static bool ApplyStatus(SkillEffectContext context,
            Character target,BodyPart part,StatusEffectId id,int amount,
            int duration,bool nextTurn)
        {
            if(target==null||target.IsDead||amount<=0||context?.Resolver==null)
                return false;
            var effect=nextTurn
                ?(StatusEffect)new DeferredStatusEffect(id,amount,
                    duration<0?StatusEffect.InfiniteDuration:Mathf.Max(1,duration))
                :StatusEffectFactory.CreateCanonical0922(id,amount,
                    duration<0?StatusEffect.InfiniteDuration:Mathf.Max(1,duration));
            if(effect==null)return false;
            var req=part==null?
                EffectRequest.CharacterStatus(context.Owner,target,effect,
                    context.Action,context.RollIndex,context.Timing,true):
                EffectRequest.BodyPartStatus(context.Owner,target,part,effect,
                    context.Action,context.RollIndex,context.Timing,true);
            return part==null?context.Resolver.ApplyCharacterStatus(req):
                context.Resolver.ApplyBodyPartStatus(req);
        }

        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            completed.Clear();
            bloodOathRegen.Clear();
            Enabled=false;
        }
    }
}
