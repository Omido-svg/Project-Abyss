using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R7-6 direct Yujin per-battle runtime. No legacy EffectDefinition.Apply,
    /// SkillEffectEntry.TryApply or adapter execution. Original definitions
    /// remain only for result UI metadata and rollback.
    /// Existing YujinMechanic is authoritative for mark, 44 ignition, weapon,
    /// sense, delayed triggers, and turn/battle lifetime.
    /// </summary>
    public sealed class YujinNativeAbilityRuntimeBinding:IDisposable
    {
        private static Dictionary<string,YujinNativeAbilityDefinition> catalog;
        private static readonly HashSet<string> knownKeys=new HashSet<string>(
            new[]{
                "yujin.duel.designate_target|Yujin_D_03",
                "yujin.duel.into_shadows|Yujin_D_04",
                "yujin.duel.shake_up|Yujin_D_05",
                "yujin.duel.cut_throat|Yujin_D_06",
                "yujin.duel.spread_rumor|Yujin_D_07",
                "yujin.duel.probe_weakness|Yujin_D_08",
                "yujin.duel.trap|Yujin_D_11",
                "yujin.duel.deadline|Yujin_D_12",
                "yujin.duel.all_targets|Yujin_D_13",
                "yujin.duel.period|Yujin_D_14",
                "yujin.duel.o.advance_timing|Yujin_D_15",
                "yujin.duel.p.finish_it|Yujin_D_16",
                "yujin.normal.ledger_cleanup|Yujin_N_03",
                "yujin.normal.aim|Yujin_N_05",
                "yujin.normal.confirm_kill|Yujin_N_06",
                "yujin.normal.familiar_hand|Yujin_N_07",
                "yujin.normal.ace_in_the_hole|Yujin_N_08",
                "yujin.normal.wanted|Yujin_N_09",
                "yujin.preparation.wait_chance|Yujin_P_05",
                "yujin.preparation.hold_breath|Yujin_P_06",
                "yujin.preparation.sharpen|Yujin_P_07"
            },StringComparer.Ordinal);
        private readonly HashSet<string> committed=new HashSet<string>(StringComparer.Ordinal);
        private bool disposed;
        public bool Enabled{get;set;}=true;
        public int AppliedCount{get;private set;}
        public int RegisteredCount=>Catalog.Count;
        private static Dictionary<string,YujinNativeAbilityDefinition> Catalog
        {
            get
            {
                if(catalog!=null)return catalog;
                var found=new Dictionary<string,YujinNativeAbilityDefinition>(
                    StringComparer.Ordinal);
                var duplicates=new HashSet<string>(StringComparer.Ordinal);
                foreach(var graph in Resources.LoadAll<YujinNativeAbilityDefinition>(
                    "ProjectAbyss/NativeYujin"))
                {
                    if(graph==null||!graph.IsValid||
                       !knownKeys.Contains(graph.StableId+"|"+graph.SourceAssetName))continue;
                    if(!found.TryAdd(graph.StableId,graph))duplicates.Add(graph.StableId);
                }
                foreach(var duplicate in duplicates)found.Remove(duplicate);
                catalog=found;return found;
            }
        }
        public static void ResetCatalogCache()=>catalog=null;
        public static bool IsKnownSource(SkillDefinition d) =>
            d!=null&&knownKeys.Contains(d.SkillId+"|"+d.name);
        public static bool IsRegisteredNativeSource(SkillDefinition d) =>
            IsKnownSource(d)&&Catalog.TryGetValue(d.SkillId,out var graph)&&
            graph.SourceAssetName==d.name&&graph.Operations.Length==d.EffectEntries?.Count;
        public bool CanRoute(SkillDefinition d) =>
            Enabled&&!disposed&&IsRegisteredNativeSource(d);

        public IReadOnlyList<SkillEffectResult> Execute(SkillDefinition d,
            SkillEffectContext context,SkillEffectTiming timing)
        {
            if(!CanRoute(d))
                throw new InvalidOperationException(
                    "R7-6 authored Yujin source has no valid native graph.");
            var graph=Catalog[d.SkillId];
            var list=new List<SkillEffectResult>(graph.Operations.Length);
            for(int i=0;i<graph.Operations.Length;i++)
            {
                var op=graph.Operations[i];
                var diagnostic=d.EffectEntries[i]?.Definition;
                if(op.Timing!=timing||(op.RestrictToRoll&&
                    context?.RollNumber!=op.RollNumber)||
                    (context?.IsOneSided==true&&
                      (timing==SkillEffectTiming.OnExchangeWin||
                       timing==SkillEffectTiming.OnExchangeLose)))
                {
                    list.Add(SkillEffectResult.NotScheduled(diagnostic,timing));
                    continue;
                }
                if(context?.Owner==null||context.Action==null||
                    context.Owner.GetMechanic<YujinMechanic>()==null)
                {
                    list.Add(SkillEffectResult.Failed(diagnostic,timing,context,
                        "R7-6 requires YujinMechanic and valid action."));
                    continue;
                }
                if(!ConditionMet(op.Condition,context))
                {
                    list.Add(SkillEffectResult.ConditionFailed(diagnostic,
                        timing,context,"R7-6 native event condition was not met."));
                    continue;
                }
                string token=graph.SourceAssetGuid+"|"+context.Action.ActionId+"|"+i+
                    "|"+timing+"|"+context.RollIndex+"|"+context.UseCountThisTurn;
                if(committed.Contains(token))
                {
                    list.Add(SkillEffectResult.NotScheduled(diagnostic,timing));
                    continue;
                }
                bool applied=Apply(op,context);
                if(applied){committed.Add(token);AppliedCount++;}
                list.Add(applied?SkillEffectResult.Applied(diagnostic,timing,context):
                    SkillEffectResult.ConditionFailed(diagnostic,timing,context,
                        "R7-6 native operation prerequisites were not met."));
            }
            return list;
        }

        private static bool ConditionMet(YujinCondition type,
            SkillEffectContext context)
        {
            var damage=context?.DamageContext;
            return type switch
            {
                YujinCondition.None=>true,
                YujinCondition.ThisHitWeakened=>damage?.WasWeakened==true,
                YujinCondition.ThisHitBroke=>damage?.WasBroken==true ||
                    damage?.BrokePart==true,
                YujinCondition.ThisHitKilled=>
                    damage?.WasKilled==true||context?.KillContext?.Victim!=null,
                YujinCondition.ThisHitWeakenedOrBroke=>
                    damage?.WasWeakened==true||damage?.WasBroken==true||
                    damage?.BrokePart==true,
                YujinCondition.Critical=>
                    context?.RollResult?.IsCritical==true||damage?.WasCritical==true,
                _=>false
            };
        }

        private static IEnumerable<Character> Enemies(SkillEffectContext c)
        {
            var b=c.BattleContext;
            if(b==null)return Array.Empty<Character>();
            if(ReferenceEquals(c.Owner,b.Player))
                return b.Enemies?.Where(x=>x!=null&&!x.IsDead).ToArray()??
                    Array.Empty<Character>();
            return b.Player!=null&&!b.Player.IsDead?
                new[]{b.Player}:Array.Empty<Character>();
        }

        private static bool ApplyStatus(SkillEffectContext c,Character to,
            BodyPart part,StatusEffect status)
        {
            if(to==null||to.IsDead||status==null||c.Resolver==null)return false;
            var request=part==null
                ?EffectRequest.CharacterStatus(c.Owner,to,status,c.Action,c.RollIndex,c.Timing,true)
                :EffectRequest.BodyPartStatus(c.Owner,to,part,status,c.Action,c.RollIndex,c.Timing,true);
            return part==null?c.Resolver.ApplyCharacterStatus(request):
                c.Resolver.ApplyBodyPartStatus(request);
        }
        private static bool Defer(SkillEffectContext c,Character to,
            BodyPart part,StatusEffectId id,int amount,int duration=1)=>
            amount>0&&ApplyStatus(c,to,part,
                new DeferredStatusEffect(id,amount,Mathf.Max(1,duration)));

        private static bool Apply(YujinNativeEntry op,
            SkillEffectContext c)
        {
            var owner=c.Owner;
            var target=c.Target;
            var part=c.TargetPart;
            var y=owner.GetMechanic<YujinMechanic>();
            int amount=op.Amount;
            switch(op.Operation)
            {
                case YujinNativeOperation.ConsumeTargetMarkForEnergy:
                    if(target==null||amount<=0||!y.TryConsumeMark(target,part,amount))
                        return false;
                    owner.AddEnergy(1,CombatResourceChangeReason.SkillEffect,c.Action,c.Action.Skill);
                    return true;
                case YujinNativeOperation.AddOwnerBlock:
                    if(amount<=0)return false;
                    owner.AddBlock(amount);return true;
                case YujinNativeOperation.AimWeaponCriticalRider:
                    if(target==null||!ConditionMet(YujinCondition.Critical,c))
                        return false;
                    switch(y.CurrentWeapon)
                    {
                        case YujinWeaponType.Baeku:
                            y.GrantMark(target,part,amount,c.Action);return true;
                        case YujinWeaponType.Jeokseol:
                            return Defer(c,target,part,StatusEffectId.Fracture,1);
                        case YujinWeaponType.Nakil:
                            var request=DamageRequest.Custom(
                                part==null?DamageType.Direct:DamageType.SkillPart,
                                owner,target,part,op.AlternativeAmount>0?op.AlternativeAmount:amount,1f,
                                canBreakPart:false,applyMomentum:false,
                                applyGuard:true,sourceAction:c.Action);
                            return c.DamageManager?.ApplyDamageContext(request)!=null;
                    }
                    return false;
                case YujinNativeOperation.DesignationMomentumRider:
                    if(target==null||y.GetDesignationTotal(target,part)<=0)return false;
                    c.BattleContext?.ResolveMomentumManager()?.ApplySkillShift(owner,amount);
                    return true;
                case YujinNativeOperation.SenseAfterSwitch:
                    if(!y.WeaponChangedThisTurn||amount<=0)return false;
                    y.GrantSense(amount);return true;
                case YujinNativeOperation.SpendSenseForForcedFront:
                    int cost=y.GetAceInTheHoleSenseCost();
                    if(!y.TrySpendSense(cost))return false;
                    y.GrantForcedFrontCharges(y.CurrentWeaponProfile.CoinCount);
                    return true;
                case YujinNativeOperation.WantedWeaponDeferredRider:
                    var kind=y.CurrentWeapon switch{
                        YujinWeaponType.Baeku=>StatusEffectId.Weakness,
                        YujinWeaponType.Jeokseol=>StatusEffectId.Fracture,
                        _=>StatusEffectId.Rupture
                    };
                    bool added=false;
                    foreach(var enemy in Enemies(c))
                        added|=Defer(c,enemy,null,kind,Mathf.Max(1,amount));
                    return added;
                case YujinNativeOperation.ApplyDesignation:
                    return ApplyStatus(c,target,part,new YujinDesignationStatus(
                        Math.Max(1,amount),Math.Max(1,op.Duration)));
                case YujinNativeOperation.GainSense:
                    if(amount<=0)return false;
                    y.GrantSense(amount);return true;
                case YujinNativeOperation.CutThroatMark:
                    if(target==null||amount<=0)return false;
                    y.GrantMark(target,part,amount,c.Action);return true;
                case YujinNativeOperation.SpreadRumorMark:
                    bool spread=false;
                    foreach(var enemy in Enemies(c))
                    {
                        if(enemy==target)continue; // 1029 excludes just-hit enemy
                        if(enemy.IsSingleHpTarget||enemy.BodyParts==null)
                        {
                            y.GrantMark(enemy,null,amount,c.Action);
                            spread=true;continue;
                        }
                        foreach(var p in enemy.BodyParts)
                        {
                            if(p==null||p.IsBroken)continue;
                            y.GrantMark(enemy,p,amount,c.Action);
                            spread=true;
                        }
                    }
                    return spread;
                case YujinNativeOperation.WeaponDebuff:
                    if(target==null)return false;
                    return Defer(c,target,part,y.CurrentWeapon switch
                    {
                        YujinWeaponType.Baeku=>StatusEffectId.Weakness,
                        YujinWeaponType.Jeokseol=>StatusEffectId.Fracture,
                        _=>StatusEffectId.Rupture
                    },Mathf.Max(1,amount));
                case YujinNativeOperation.RegisterExplosionTrap:
                    return target!=null&&y.RegisterMarkTrap(
                        target,part,Mathf.Max(1,amount),c.Action)>0;
                case YujinNativeOperation.RegisterResonanceTrap:
                    return target!=null&&y.RegisterMarkDeadline(
                        target,part,Mathf.Max(1,op.Duration),2,c.Action)>0;
                case YujinNativeOperation.DesignateAllEnemies:
                    bool any=false;
                    foreach(var enemy in Enemies(c))
                        any|=ApplyStatus(c,enemy,null,
                            new YujinDesignationStatus(
                                Math.Max(1,amount),Math.Max(1,op.Duration)));
                    return any;
                case YujinNativeOperation.PeriodSenseRider:
                    if(y.CurrentWeapon==YujinWeaponType.Nakil)
                    {
                        if(c.DamageContext?.BrokePart!=true ||
                           c.DamageContext.WasCritical!=true)return false;
                    }
                    else if(c.DamageContext?.WasKilled!=true &&
                            c.KillContext?.Victim==null)return false;
                    y.GrantSense(amount);return true;
                case YujinNativeOperation.ConsumeMarkForBlock:
                    if(amount<=0||!y.TryConsumeAnyMarkForPreparation(amount))return false;
                    owner.AddBlock(20);return true;
                case YujinNativeOperation.ConsumeMarkForSwift:
                    return amount>0&&y.TryConsumeAnyMarkForPreparation(amount)&&
                        Defer(c,owner,null,StatusEffectId.Swift,1);
                case YujinNativeOperation.ForceMarkIgnition:
                    return target!=null&&y.ForceMarkIgnition(
                        target,part,c.Action).Applied;
                case YujinNativeOperation.SpendSenseBundlesForPower:
                    // 1029: only complete weapon-sized bundles are spent.
                    // Any remainder is preserved.
                    int bundle=Math.Max(1,y.CurrentWeaponProfile.CoinCount);
                    int groups=y.Sense/bundle;
                    if(groups<=0||!y.TrySpendSense(groups*bundle))return false;
                    c.Action.RulebreakerFlatPowerBonus+=groups*2;
                    return true;
                default:throw new InvalidOperationException(
                    "Unknown R7-6 native Yujin operation.");
            }
        }

        public void Dispose()
        {
            if(disposed)return;
            disposed=true;Enabled=false;committed.Clear();
        }
    }
}