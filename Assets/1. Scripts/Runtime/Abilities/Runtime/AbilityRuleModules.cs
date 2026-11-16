using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AbilityRuleModuleAttribute : Attribute
    {
        public string Id { get; }
        public AbilityRuleModuleAttribute(string id){Id=id;}
    }
    public interface IAbilityRuleModule { void Register(AbilityRuleRegistry registry); }
    public static class AbilityRuleModules
    {
        private static readonly SortedDictionary<string,Func<IAbilityRuleModule>> factories=new(StringComparer.Ordinal);
        private static bool discovered;
        public static void RegisterFactory(string id,Func<IAbilityRuleModule> factory)
        {if(string.IsNullOrWhiteSpace(id)||factory==null||!factories.TryAdd(id,factory))throw new ArgumentException("Duplicate rule module: "+id);}
        public static void Configure(AbilityRuleRegistry registry)
        {
            if(!discovered)
            {
                discovered=true;
                // One-time module discovery, never a per-turn/per-skill reflection scan.
                foreach(var type in typeof(AbilityRuleModules).Assembly.GetTypes().OrderBy(t=>t.FullName,StringComparer.Ordinal))
                {
                    var attribute=(AbilityRuleModuleAttribute)Attribute.GetCustomAttribute(type,typeof(AbilityRuleModuleAttribute));
                    if(attribute==null)continue;
                    if(type.IsAbstract||!typeof(IAbilityRuleModule).IsAssignableFrom(type)||type.GetConstructor(Type.EmptyTypes)==null)
                        throw new InvalidOperationException("Invalid ability module: "+type.FullName);
                    var captured=type;RegisterFactory(attribute.Id,()=> (IAbilityRuleModule)Activator.CreateInstance(captured));
                }
            }
            foreach(var factory in factories.Values)factory().Register(registry);
        }
    }
    public static class AbilityTimingMap
    {
        public static AbilityEventKind Event(SkillEffectTiming timing)=>timing switch
        {
            SkillEffectTiming.OnAttackResolutionStarting=>AbilityEventKind.AttackResolutionStarting,
            SkillEffectTiming.OnExecute=>AbilityEventKind.RollUsing,
            SkillEffectTiming.OnExchangeWin=>AbilityEventKind.ExchangeWon,
            SkillEffectTiming.OnExchangeLose=>AbilityEventKind.ExchangeLost,
            SkillEffectTiming.OnClashEnd=>AbilityEventKind.ClashCompleted,
            SkillEffectTiming.OnPreparationActivated=>AbilityEventKind.PreparationActivated,
            SkillEffectTiming.AfterDamage or SkillEffectTiming.OnPartWeakened or SkillEffectTiming.OnPartBroken or SkillEffectTiming.OnKill or SkillEffectTiming.OnCritical=>AbilityEventKind.DamageSettled,
            _=>throw new ArgumentException("Timing is not a canonical ability boundary: "+timing)
        };
        public static bool TryEvent(SkillEffectTiming timing,out AbilityEventKind kind)
        {try{kind=Event(timing);return true;}catch(ArgumentException){kind=default;return false;}}
    }
}
