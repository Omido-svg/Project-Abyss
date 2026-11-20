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
        private static readonly HashSet<string> declaredSources=new(StringComparer.Ordinal);
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
                    if(graph==null)continue;
                    declaredSources.Add(graph.StableId+"|"+graph.SourceAssetName);
                    if(!graph.IsValid)continue;
                    if(!found.TryAdd(graph.StableId,graph))duplicates.Add(graph.StableId);
                }
                foreach(var duplicate in duplicates)found.Remove(duplicate);
                catalog=found;return found;
            }
        }
        public static void ResetCatalogCache(){catalog=null;declaredSources.Clear();}
        public static bool IsKnownSource(SkillDefinition d) =>
            d!=null && IsDeclared(d);
        private static bool IsDeclared(SkillDefinition d){_=Catalog;return declaredSources.Contains(d.SkillId+"|"+d.name);}
        public static bool IsRegisteredNativeSource(SkillDefinition d) =>
            IsKnownSource(d)&&Catalog.TryGetValue(d.SkillId,out var graph)&&
            graph.SourceAssetName==d.name;
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
                var diagnostic=d.EffectEntries!=null && i<d.EffectEntries.Count?d.EffectEntries[i]?.Definition:null;
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
                bool applied=context.BattleContext.Abilities.ExecuteSpecialist(graph.StableId,graph.SourceAssetGuid,i,context,true,_=>Apply(op,context));
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
                    damage?.BrokePart==true || (context?.Target?.IsSingleHpTarget==true && damage?.WasKilled==true),
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
                            // 18.3 정조준 적설 = 다음 턴 쇠약(11-20, 구 골절).
                            return Defer(c,target,part,StatusEffectId.Weakness,
                                1);
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
                    return y.TransferTargetMark(target,part,c.Action);
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
                    return y.SharpenWeapon(y.MarkRules.SharpenMarkBonus);
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
                // ── 11-20 명성 9장 규칙서 반영(18.3 · 18.4 · 18.5) ──
                case YujinNativeOperation.WantedPerRollRider:
                    return ApplyWanted(op,c,y);
                case YujinNativeOperation.FamiliarHandWinRider:
                    if(target==null)return false;
                    switch(y.CurrentWeapon)
                    {
                        case YujinWeaponType.Baeku:
                            var gauge=target.GetMechanic<StaggerGaugeMechanic>();
                            return gauge!=null&&amount>0&&gauge.SacrificeGauge(amount)>0;
                        case YujinWeaponType.Jeokseol:
                            y.GrantMark(target,part,Math.Max(1,op.AlternativeAmount),c.Action);return true;
                        default:
                            var hit=DamageRequest.Custom(
                                part==null?DamageType.Direct:DamageType.SkillPart,
                                owner,target,part,Math.Max(1,op.AlternativeAmount),1f,
                                canBreakPart:false,applyMomentum:false,
                                applyGuard:true,sourceAction:c.Action);
                            c.BattleContext?.ResolveDamageManager()?.ApplyDamageContext(hit);
                            return true;
                    }
                case YujinNativeOperation.FinishWeaponSenseEnergy:
                {
                    var dealt=c.DamageContext;
                    bool met=y.CurrentWeapon switch
                    {
                        YujinWeaponType.Baeku=>dealt?.WasKilled==true||c.KillContext?.Victim!=null,
                        YujinWeaponType.Jeokseol=>dealt?.OpenedVulnerabilityWindow==true,
                        _=>dealt?.WasBroken==true // 낙일 타격은 전부 처형 권한 — 이 굴림이 부위를 파괴 = 처형 성공
                    };
                    if(!met)return false;
                    if(amount>0)y.GrantSense(amount);
                    if(op.AlternativeAmount>0)
                        owner.AddEnergy(op.AlternativeAmount,CombatResourceChangeReason.SkillEffect,c.Action,c.Action.Skill);
                    return true;
                }
                case YujinNativeOperation.SpendSenseForMark:
                    if(target==null||amount<=0||!y.TrySpendSense(Math.Max(1,op.AlternativeAmount)))return false;
                    y.GrantMark(target,part,amount,c.Action);return true;
                case YujinNativeOperation.SenseByFervorLevel:
                {
                    var fervor=c.BattleContext?.Services?.FervorManager;
                    int level=fervor==null?0:(ReferenceEquals(owner,c.BattleContext.Player)?fervor.FervorLevel:fervor.GetFervorLevel(owner));
                    if(level<=0)return false;
                    y.GrantSense(level);return true;
                }
                case YujinNativeOperation.SpendSenseForBlock:
                    if(amount<=0||!y.TrySpendSense(Math.Max(1,op.AlternativeAmount)))return false;
                    owner.AddBlock(amount);return true;
                case YujinNativeOperation.OwnerSwiftNextTurn:
                    return Defer(c,owner,null,StatusEffectId.Swift,Math.Max(1,amount),Math.Max(1,op.Duration));
                case YujinNativeOperation.TargetEnergyCapDownNextTurn:
                {
                    if(target==null||target.IsDead||amount<=0)return false;
                    var queue=c.BattleContext?.TurnStartEffects;
                    if(queue==null)return false;
                    var resolver=c.Resolver;var action=c.Action;var to=target;
                    int value=amount,turns=Math.Max(1,op.Duration);
                    queue.EnqueueLocked(to,null,()=>
                    {
                        if(to==null||to.IsDead||resolver==null)return;
                        resolver.ApplyCharacterStatus(EffectRequest.CharacterStatus(
                            owner,to,new EnergyCapDownStatus(value,turns),action,0,SkillEffectTiming.OnTurnStart,true));
                    });
                    return true;
                }
                case YujinNativeOperation.MomentumPush:
                    if(amount<=0)return false;
                    c.BattleContext?.ResolveMomentumManager()?.ApplySkillShift(owner,amount);
                    return true;
                default:throw new InvalidOperationException(
                    "Unknown R7-6 native Yujin operation.");
            }
        }

        /// <summary>
        /// 18.3 현상수배 — 굴림 위치마다 다른 효과, 그 굴림이 이겼을 때만. 모두 「다음 턴」.
        /// 백우 1 표식 +N(모든 적) · 2 지정 1/1(모든 적) · 3 자신 신속 1/1 / 적설 1 쇠약 · 2 속박(모든 적) / 낙일 1 균열(모든 적).
        /// 표식 · 지정을 넣을 부위 = 이긴 순간 그 적의 현재 표식이 가장 높은 생존 부위(동률 무작위).
        /// </summary>
        private static bool ApplyWanted(YujinNativeEntry op,SkillEffectContext c,YujinMechanic y)
        {
            int roll=c.RollNumber;
            int status=Math.Max(1,op.AlternativeAmount);
            int turns=Math.Max(1,op.Duration);
            bool any=false;
            switch(y.CurrentWeapon)
            {
                case YujinWeaponType.Baeku:
                    if(roll==3)return Defer(c,c.Owner,null,StatusEffectId.Swift,status,turns);
                    if(roll!=1&&roll!=2)return false;
                    foreach(var enemy in Enemies(c))
                    {
                        var part=HighestMarkPart(y,enemy);
                        if(roll==1)any|=y.QueueMarkForNextTurn(enemy,part,Math.Max(1,op.Amount),c.Action);
                        else
                        {
                            var queue=c.BattleContext?.TurnStartEffects;
                            if(queue==null)continue;
                            var resolver=c.Resolver;var owner=c.Owner;var action=c.Action;var to=enemy;var at=part;
                            queue.EnqueueLocked(to,at,()=>
                            {
                                if(to==null||to.IsDead||resolver==null)return;
                                var designation=new YujinDesignationStatus(status,turns);
                                if(at==null||at.IsBroken)
                                    resolver.ApplyCharacterStatus(EffectRequest.CharacterStatus(owner,to,designation,action,1,SkillEffectTiming.OnTurnStart,true));
                                else
                                    resolver.ApplyBodyPartStatus(EffectRequest.BodyPartStatus(owner,to,at,designation,action,1,SkillEffectTiming.OnTurnStart,true));
                            });
                            any=true;
                        }
                    }
                    return any;
                case YujinWeaponType.Jeokseol:
                    if(roll!=1&&roll!=2)return false;
                    foreach(var enemy in Enemies(c))
                        any|=Defer(c,enemy,null,roll==1?StatusEffectId.Weakness:StatusEffectId.Bind,status,turns);
                    return any;
                default:
                    if(roll!=1)return false;
                    foreach(var enemy in Enemies(c))
                        any|=Defer(c,enemy,null,StatusEffectId.Rupture,status,turns);
                    return any;
            }
        }

        private static BodyPart HighestMarkPart(YujinMechanic y,Character enemy)
        {
            if(enemy==null||enemy.IsSingleHpTarget||enemy.BodyParts==null)return null;
            var alive=enemy.BodyParts.Where(p=>p!=null&&!p.IsBroken).ToList();
            if(alive.Count==0)return null;
            int best=alive.Max(p=>y.GetMark(enemy,p));
            var top=alive.Where(p=>y.GetMark(enemy,p)==best).ToList();
            return top[UnityEngine.Random.Range(0,top.Count)];
        }

        public void Dispose()
        {
            if(disposed)return;
            disposed=true;Enabled=false;committed.Clear();
        }
    }
}