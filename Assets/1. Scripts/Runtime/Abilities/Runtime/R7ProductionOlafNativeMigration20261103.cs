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
    public sealed class R7ProductionOlafNativeMigration20261103 : IDisposable
    {
        private static Dictionary<string,R7NativeOlafAbilityDefinition20261103> catalog;
        private static readonly HashSet<string> approvedGuids=new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "bc09ab10e9afa2945a34d422441e647e", // D02
            "9e93fab440e48294cb184588a4f2e9d1", // D05
            "a8bb7b1ceb09d084395e7c7ea88b38d2", // D11
            "0fb4c392cd50a4547adfc852c934c1f5", // D12
            "78d15429a8e99f6489a5c464de0c4d80", // D14
            "47a2fe6af1581a94ebaa12be5b727898", // D15
            "b69cc12447cfd154995ce6525ab5fa65", // D16
            "8d36ea252e9b77048a3f3782221187ce", // D17
            "10e00c86a15bc59429d6c376fb80607d", // D19
            "87b77b20013e7f54f9ed7113454ae450", // N01
            "318da5529382900449c8dc37169b108f", // N11
            "25a5e287c9914aa48a25a674b8684384", // N13
            "e9563d3d13d91be4bbda1bdadcc71a98", // N16
            "df26a5c142a37e044aaafae9c21bde0b", // P02
            "81f9a22a9097f324eb6a55014054e5ba", // N02
            "8058cfe6fb011f34294f3e3abd8d203c", // N04
            "0ccd4e460d9ad4142a73432b46c320c0", // N05
            "c4c19befd70a20d4591d5000990287b2", // N07
            "a49339629c1382f41936cea19629fe41", // N08
            "1ecdb0f93b9ad3e4a921d7534ba253c5", // D04
            "c870a452bdc56294ab616730fb329c09", // D07
            "6cbaebefc3fa7744b8a5e45d09de0c67", // D08
            "3d1ed9490a24613499ef57d2638343d2", // P03
            "b5db1d965d47c1f4194171f654a26d50", // P04
            "0807b31f4b2fcda46bc9037e2b5af04f", // P05
            "858496a7d4d0b7742b15c3f86703c801", // P06
            "6b081b4a77b26e04a8d89ff2c4daeed9", // N10
            "78ae264f7be5e81478fc11a8de548904", // N12
            "99837cafaea66dd4ea77de4b1a3e703a", // P07
            "ec186728b972c2f40a7c7ae15aaa18f9"  // D21
        };
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
        private static Dictionary<string,R7NativeOlafAbilityDefinition20261103> Catalog
        {
            get
            {
                if(catalog!=null)return catalog;
                var found=new Dictionary<string,R7NativeOlafAbilityDefinition20261103>(
                    StringComparer.Ordinal);
                duplicates.Clear();
                foreach(var g in Resources.LoadAll<R7NativeOlafAbilityDefinition20261103>(
                    "ProjectAbyss/NativeOlaf").Concat(
                    Resources.LoadAll<R7NativeOlafAbilityDefinition20261103>(
                        "ProjectAbyss/NativeOlafCanon")))
                {
                    if(g==null||!g.IsValid||!approvedGuids.Contains(g.SourceAssetGuid))
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
            d!=null && Catalog.TryGetValue(d.SkillId,out var g) &&
            d.name==g.SourceAssetName &&
            (g.Operations.Length==d.EffectEntries?.Count ||
             (d.SkillId=="olaf.duel.fortify" && g.Operations.Length==3 &&
              d.EffectEntries?.Count==1) ||
             (d.SkillId=="olaf.normal.aim_weakness" &&
              g.Operations.Length==3 && d.EffectEntries?.Count==2) ||
             (d.SkillId.StartsWith("olaf.",StringComparison.Ordinal) &&
              approvedGuids.Contains(g.SourceAssetGuid) &&
              g.Operations.Length>=1));

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
                R7OlafNativeOperation20261103.PowerPerTargetNegativeStatusOnThirdRoll &&
                e.RestrictToRoll&&e.RollNumber==3 &&
                e.Timing==SkillEffectTiming.OnExecute);

        public static bool HasExploitGapChain(SkillDefinition d) =>
            IsApprovedSource(d)&&Catalog[d.SkillId].Operations.Any(
                x=>x.Operation==
                    R7OlafNativeOperation20261103.ChainExtraOneSidedAttackOnTransition);

        public static bool HasNoRetreatWeakenPower(SkillDefinition d) =>
            IsApprovedSource(d)&&
            Catalog[d.SkillId].Operations.Any(e=>e.Operation==
                R7OlafNativeOperation20261103.PowerPerWeakenedOwnerPartEachRoll);

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
                bool applied=Apply(op,context);
                if(applied)
                {
                    completed.Add(token);
                    AppliedCount++;
                    if(d.SkillId=="olaf.preparation.blood_oath" &&
                       op.Operation==R7OlafNativeOperation20261103.OwnerStatusImmediate)
                        bloodOathRegen[context.Owner]=
                            context.Owner.StatusEffects?.FirstOrDefault(
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
            if(owner.StatusEffects!=null &&
               owner.StatusEffects.Contains(status) && !status.IsExpired)
                return false;
            bloodOathRegen.Remove(owner);
            return true;
        }

        private static bool Apply(R7OlafNativeEntry20261103 op,
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
                case R7OlafNativeOperation20261103.TargetBleedingRandomOneOrTwo:
                    if(target==null||target.IsDead)return false;
                    rule.AddBleeding(target,part,UnityEngine.Random.Range(1,3),action);
                    return true;
                case R7OlafNativeOperation20261103.GainEnergy:
                    if(value==0)return false;
                    owner.AddEnergy(value,CombatResourceChangeReason.SkillEffect,
                        action,action.Skill);
                    return true;
                case R7OlafNativeOperation20261103.CalmMadnessHealAndPrestige:
                    if(owner.MaxCombatHP<=0||
                        (long)owner.CurrentHP*10>=(long)owner.MaxCombatHP*7||
                        !madness.TryConsumeMadness(2))return false;
                    owner.RestoreCurrentHP(30);
                    owner.AddPrestige(10);
                    return true;
                case R7OlafNativeOperation20261103.DevourStaggerHeal:
                    if(owner.MaxCombatHP<=0||
                        (long)owner.CurrentHP*10>=(long)owner.MaxCombatHP*7)return false;
                    var stagger=owner.GetMechanic<StaggerGaugeMechanic>();
                    if(stagger==null||stagger.CurrentGauge<20||
                        stagger.SacrificeGauge(20)!=20)return false;
                    owner.RestoreCurrentHP(30);
                    return true;
                case R7OlafNativeOperation20261103.OwnerStaggerDamage:
                    var ownStagger=owner.GetMechanic<StaggerGaugeMechanic>();
                    return ownStagger!=null&&ownStagger.SacrificeGauge(value)>0;
                case R7OlafNativeOperation20261103.PowerPerWeakenedOwnerPartEachRoll:
                    // Recomputed when this exact roll is judged/damaged.
                    return true;
                case R7OlafNativeOperation20261103.MadnessGainWithRagnarokLoss:
                    madness.AddMadness(
                        owner.GetMechanic<OlafImmortalFuryMechanic>()?.IsActive==true?3:1);
                    return true;
                case R7OlafNativeOperation20261103.ChainExtraOneSidedAttackOnTransition:
                    // ClashManager extends the already resolved exchange by
                    // one-sided actions through its normal RNG/damage pipeline.
                    var dealt=context.DamageContext;
                    return dealt!=null&&(dealt.WasWeakened||
                        dealt.WasBroken||dealt.OpenedVulnerabilityWindow||
                        dealt.WasKilled);
                case R7OlafNativeOperation20261103.ActivateAdjustedMaxSpeedIfLastStand:
                    if(!rule.CanUse(context.SkillDefinition))return false;
                    rule.ActivateCanonicalLastResistance();
                    return true;
                case R7OlafNativeOperation20261103.ScheduleNextTurnMaxSlotSpeed:
                    return rule.ScheduleNextTurnMaximumSlotSpeed(action);
                case R7OlafNativeOperation20261103.ProtectLowestHpOwnerPartForTurn:
                    return rule.ProtectLowestHpOwnerPart(action);
                case R7OlafNativeOperation20261103.ConsumeOwnerBleedingForTurnPower:
                    int consumed=rule.ConsumeOwnerBleedingUpTo(4);
                    if(consumed==0)return false;
                    rule.GrantTurnPowerBonus(consumed>=3?2:1);
                    return true;
                case R7OlafNativeOperation20261103.PowerPerTargetNegativeStatusOnThirdRoll:
                    // Pure per-roll power is evaluated by OlafRulebreakerMechanic.
                    // Never persist a flat bonus onto the whole BattleAction.
                    return target!=null&&!target.IsDead;
                case R7OlafNativeOperation20261103.FortifyConsumeBleedingForStrength:
                    if(target==null||!rule.TryConsumeBleeding(target,part,2))return false;
                    return ApplyStatus(context,owner,null,StatusEffectId.Strength,1,1,false);
                case R7OlafNativeOperation20261103.FortifyConsumeBleedingForSwift:
                    if(target==null||!rule.TryConsumeBleeding(target,part,2))return false;
                    return ApplyStatus(context,owner,null,StatusEffectId.Swift,1,2,false);
                case R7OlafNativeOperation20261103.FortifyPrestigeIfBuffsPresent:
                    if(StatusEffectFactory.GetNumericTotal(owner,null,
                            StatusEffectId.Strength)<1 ||
                       StatusEffectFactory.GetNumericTotal(owner,null,
                            StatusEffectId.Swift)<1) return false;
                    owner.AddPrestige(10);
                    return true;
                case R7OlafNativeOperation20261103.TargetBleeding:
                    if(target==null||target.IsDead||value==0)return false;
                    rule.AddBleeding(target,part,value,action);
                    return true;
                case R7OlafNativeOperation20261103.OwnerBleeding:
                    if(value==0)return false;
                    // 1029 canonical self-bleed targets the skill slot's own part.
                    rule.AddBleeding(owner,action?.OwnerPart,value,action);
                    return true;
                case R7OlafNativeOperation20261103.AddMadness:
                    if(value==0)return false;
                    madness.AddMadness(value);
                    return true;
                case R7OlafNativeOperation20261103.OwnerBlock:
                    if(value==0)return false;
                    owner.AddBlock(value);
                    return true;
                case R7OlafNativeOperation20261103.ClearTargetBlock:
                    if(target==null||target.IsDead)return false;
                    target.ClearBlock();
                    return true;
                case R7OlafNativeOperation20261103.RandomTargetDebuffNextTurn:
                    if(target==null||target.IsDead)return false;
                    // 1029 canon: choose Rupture or Weakness now, never at
                    // next-turn materialization (legacy had a third option).
                    return ApplyStatus(context,target,part,
                        UnityEngine.Random.Range(0,2)==0?
                            StatusEffectId.Rupture:StatusEffectId.Weakness,1,1,true);
                case R7OlafNativeOperation20261103.TargetStatusNextTurn:
                    return ApplyStatus(context,target,part,op.StatusId,
                        value,op.Duration,true);
                case R7OlafNativeOperation20261103.OwnerStatusNextTurn:
                    return ApplyStatus(context,owner,null,op.StatusId,
                        value,op.Duration,true);
                case R7OlafNativeOperation20261103.OwnerStatusImmediate:
                    return ApplyStatus(context,owner,null,op.StatusId,
                        value,op.Duration,false);
                case R7OlafNativeOperation20261103.TargetStatusNextTurnIfBleedingAtLeast:
                    if(target==null||rule.GetBleeding(target,part)<op.MinimumBleeding)
                        return false;
                    return ApplyStatus(context,target,part,op.StatusId,
                        value,op.Duration,true);
                case R7OlafNativeOperation20261103.StrengthByTargetBleedingBandNextTurn:
                    if(target==null)return false;
                    int amount=rule.GetBleeding(target,part);
                    return ApplyStatus(context,owner,null,StatusEffectId.Strength,
                        amount>=10?3:amount>=5?2:1,op.Duration,true);
                case R7OlafNativeOperation20261103.BloodPriceStrengthByOwnerBleeding:
                    int own=rule.GetOwnerBleedingTotal();
                    return ApplyStatus(context,owner,null,StatusEffectId.Strength,
                        own>=10?3:own>=5?2:1,3,true);
                case R7OlafNativeOperation20261103.ConsumeTargetBleedingForEnergy:
                    if(target==null||!rule.TryConsumeBleeding(target,part,
                       Mathf.Max(1,value)))return false;
                    owner.AddEnergy(2,CombatResourceChangeReason.SkillEffect,
                        action,action?.Skill);
                    return true;
                case R7OlafNativeOperation20261103.PrepareBloodOathDuel:
                    rule.PrepareBloodOathDuel(action);
                    return true;
                case R7OlafNativeOperation20261103.ResolveBloodOathDuelWin:
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