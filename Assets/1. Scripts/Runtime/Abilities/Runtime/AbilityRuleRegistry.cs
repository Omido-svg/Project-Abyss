using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProjectAbyss.Abilities
{
    public interface IReadOnlyScopedAbilityState
    {
        long Get(AbilityOwnerScope scope,string key);
    }
    public interface IReadOnlyScopedAbilityContext : IReadOnlyAbilityExecutionContext
    {
        IReadOnlyScopedAbilityState State { get; }
    }
    public readonly struct AbilityOperationSpec
    {
        public readonly string TypeId, Key;
        public readonly int Amount, Duration, SecondaryAmount;
        public readonly AbilityOwnerScope StateScope;
        public readonly IAbilityRuleSettings Settings;
        internal AbilityOperationSpec(string id,IAbilityRuleSettings settings)
        {TypeId=id;Key="";Amount=0;Duration=0;SecondaryAmount=0;StateScope=AbilityOwnerScope.Actor;Settings=settings;}
        private AbilityOperationSpec(AbilityOperationSpec source,int amount)
        {TypeId=source.TypeId;Key=source.Key;Amount=amount;Duration=source.Duration;SecondaryAmount=source.SecondaryAmount;StateScope=source.StateScope;Settings=source.Settings;}
        public AbilityOperationSpec WithAmount(int amount)=>new(this,amount);
        public AbilityOperationSpec(AbilityOperationData data)
        {
            TypeId=data.TypeId??"";Key=data.Key??"";Amount=data.Amount;
            Duration=data.Duration;SecondaryAmount=data.SecondaryAmount;StateScope=data.StateScope;
            Settings=data.Settings==null?null:data.Settings is IAbilityRuleSettingsDefinition definition
                ?definition.CaptureSettings():throw new ArgumentException("Settings must provide an immutable compiled snapshot");
        }
    }
    public readonly struct AbilityRuleCommand
    {
        public readonly AbilityOperationSpec Operation;
        public readonly CombatTargetRef Target;
        public readonly EffectCause Cause;
        public AbilityRuleCommand(AbilityOperationSpec operation,CombatTargetRef target,EffectCause cause)
        {Operation=operation;Target=target;Cause=cause;}
    }
    public interface IAbilityRuleCommandSink { void Enqueue(AbilityRuleCommand command); }
    public interface IAbilityRuleHandler
    {
        // Planning receives immutable observations and a read-only state view, never Character/HP services.
        void Plan(AbilityRuleReadContext context,AbilityOperationSpec operation,
            IReadOnlyList<CombatTargetRef> targets,IAbilityRuleCommandSink sink);
        CombatCommandReceipt Commit(AbilityRuleCommitContext context,AbilityRuleCommand command);
        string Validate(AbilityOperationSpec operation);
    }
    public sealed class AbilityRuleReadContext : IReadOnlyScopedAbilityContext, IConditionSnapshotContext,
        IReadOnlyDamageAbilityContext
    {
        public AbilityExecutionFrame Frame { get; }
        public AbilityIdentity Ability=>Frame.Ability;
        public AbilityEventContext Event=>Frame.ToEventContext();
        public ConditionSnapshot Snapshot=>Frame.Snapshot;
        public IReadOnlyScopedAbilityState State=>Frame.State;
        public bool HasCommittedDamageResult { get; }
        public AppliedDamageEvidence DamageEvidence { get; }
        public IReadOnlyList<CombatTargetRef> Allies { get; }
        public IReadOnlyList<CombatTargetRef> Enemies { get; }
        public IReadOnlyList<CombatTargetRef> EnemyParts { get; internal set; }
        internal AbilityRuleReadContext(AbilityExecutionFrame frame,IReadOnlyList<CombatTargetRef> allies,
            IReadOnlyList<CombatTargetRef> enemies,AppliedDamageEvidence? damage=null)
        {Frame=frame;Allies=allies;Enemies=enemies;HasCommittedDamageResult=damage.HasValue;DamageEvidence=damage??default;}
    }
    public sealed class AbilityRuleCommitContext
    {
        public BattleContext Battle { get; }
        public BattleAbilityRuntime Runtime { get; }
        public AbilityExecutionFrame Frame { get; }
        public Character Source { get; }
        public Character Target { get; }
        public BodyPart Part { get; }
        public string RuleId { get; }
        internal AbilityRuleCommitContext(BattleAbilityRuntime runtime,AbilityExecutionFrame frame,
            Character source,Character target,BodyPart part,string ruleId)
        {Runtime=runtime;Battle=runtime.Battle;Frame=frame;Source=source;Target=target;Part=part;RuleId=ruleId;}
    }
    /// <summary>One registration point for independently authored handlers, predicates and target selectors.</summary>
    public sealed class AbilityRuleRegistry
    {
        private readonly Dictionary<string,IAbilityRuleHandler> operations=new(StringComparer.Ordinal);
        private readonly Dictionary<string,Func<AbilityPredicateData,ICombatCondition>> predicates=new(StringComparer.Ordinal);
        private readonly Dictionary<string,Func<AbilityRuleReadContext,IReadOnlyList<CombatTargetRef>>> selectors=new(StringComparer.Ordinal);
        public IReadOnlyCollection<string> PredicateIds=>new ReadOnlyCollection<string>(new List<string>(predicates.Keys));
        public IReadOnlyCollection<string> SelectorIds=>new ReadOnlyCollection<string>(new List<string>(selectors.Keys));
        public IReadOnlyCollection<string> OperationIds=>new ReadOnlyCollection<string>(new List<string>(operations.Keys));
        public void RegisterOperation(string id,IAbilityRuleHandler handler)
        {if(string.IsNullOrWhiteSpace(id)||handler==null||!operations.TryAdd(id,handler))throw new ArgumentException("Duplicate/invalid operation: "+id);}
        public void RegisterPredicate(string id,Func<AbilityPredicateData,ICombatCondition> factory)
        {if(string.IsNullOrWhiteSpace(id)||factory==null||!predicates.TryAdd(id,factory))throw new ArgumentException("Duplicate/invalid predicate: "+id);}
        public void RegisterSelector(string id,Func<AbilityRuleReadContext,IReadOnlyList<CombatTargetRef>> selector)
        {if(string.IsNullOrWhiteSpace(id)||selector==null||!selectors.TryAdd(id,selector))throw new ArgumentException("Duplicate/invalid selector: "+id);}
        public IAbilityRuleHandler Operation(string id)=>operations.TryGetValue(id??"",out var handler)?handler:
            throw new InvalidOperationException("Unregistered operation: "+id);
        public IReadOnlyList<CombatTargetRef> Select(string id,AbilityRuleReadContext view)=>
            selectors.TryGetValue(id??"",out var select)?select(view):throw new InvalidOperationException("Unregistered target selector: "+id);
        public bool HasSelector(string id)=>id!=null&&selectors.ContainsKey(id);
        public CombatConditionNode Compile(AbilityConditionData data)
        {return Compile(data,new HashSet<AbilityConditionData>());}
        private CombatConditionNode Compile(AbilityConditionData data,HashSet<AbilityConditionData> path)
        {
            if(data==null)return CombatConditionNode.All();
            if(!path.Add(data))throw new ArgumentException("Cyclic condition tree.");
            try
            {
                if(data.Kind==CombatConditionNodeKind.Predicate)
                {
                    if(data.Predicate==null||!predicates.TryGetValue(data.Predicate.TypeId??"",out var factory))
                        throw new ArgumentException("Unregistered predicate: "+data.Predicate?.TypeId);
                    var d=data.Predicate;
                    return CombatConditionNode.Leaf(factory(new AbilityPredicateData{TypeId=d.TypeId,Key=d.Key,Value=d.Value,Subject=d.Subject,
                        Scope=d.Scope,Fact=d.Fact,Comparison=d.Comparison,StateScope=d.StateScope}));
                }
                var children=new List<CombatConditionNode>();
                foreach(var child in data.Children??Array.Empty<AbilityConditionData>())
                {if(child==null)throw new ArgumentException("Null condition child.");children.Add(Compile(child,path));}
                return data.Kind switch
                {
                    CombatConditionNodeKind.All=>CombatConditionNode.All(children.ToArray()),
                    CombatConditionNodeKind.Any=>CombatConditionNode.Any(children.ToArray()),
                    CombatConditionNodeKind.Not when children.Count==1=>CombatConditionNode.Not(children[0]),
                    _=>throw new ArgumentException("Invalid condition shape.")
                };
            }
            finally{path.Remove(data);}
        }
        public IReadOnlyList<string> Validate(AbilityRuleSetDefinition definition)
        {
            var issues=new List<string>();
            if(definition==null){issues.Add("MISSING_DEFINITION");return issues;}
            if(definition.SchemaVersion!=1||string.IsNullOrWhiteSpace(definition.StableId)||
                string.IsNullOrWhiteSpace(definition.SourceAssetGuid))issues.Add("INVALID_IDENTITY_SCHEMA");
            if(definition.Rules==null||definition.Rules.Length==0)issues.Add("RULES_REQUIRED");
            if(!Enum.IsDefined(typeof(AbilityRuleSourceKind),definition.SourceKind)||string.IsNullOrWhiteSpace(definition.SourceAssetName))issues.Add("SOURCE_IDENTITY_REQUIRED");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var rule in definition.Rules??Array.Empty<AbilityRuleData>())
            {
                if(rule==null){issues.Add("NULL_RULE");continue;}
                if(string.IsNullOrWhiteSpace(rule.RuleId)||!ids.Add(rule.RuleId))issues.Add("DUPLICATE_OR_EMPTY_RULE_ID");
                if(!Enum.IsDefined(typeof(AbilityEventKind),rule.Trigger))issues.Add("INVALID_TRIGGER");
                if(rule.Lifetime==null||rule.Lifetime.FullCooldownTurns<0)issues.Add("INVALID_LIFETIME");
                if(rule.Operations==null||rule.Operations.Length==0)issues.Add("OPERATIONS_REQUIRED");
                if(rule.Policy?.Snapshot==AbilitySnapshotPolicy.LockAtScheduleCreation&&!rule.ScheduledOnly)issues.Add("SCHEDULE_SNAPSHOT_REQUIRES_SCHEDULED_RULE");
                if(rule.Lifetime?.OncePerCounterExecution==true && rule.Policy?.OwnerScope!=AbilityOwnerScope.CounterExecution)issues.Add("COUNTER_LIFETIME_REQUIRES_COUNTER_SCOPE");
                if(rule.Policy==null)issues.Add("MISSING_POLICY");else foreach(var issue in rule.Policy.Validate(rule.Trigger))issues.Add(rule.RuleId+": "+issue);
                if(definition.SourceKind==AbilityRuleSourceKind.Skill && rule.Trigger==AbilityEventKind.DamageSettled && !rule.ScheduledOnly)
                {
                    if(rule.Policy?.DamageConditionOrigin==AbilityDamageConditionOrigin.Unspecified)issues.Add("DEFERRED_CARD_ORIGIN_REQUIRED: "+rule.RuleId);
                    if(!rule.RequiresDuelGate)issues.Add("DEFERRED_CARD_REQUIRES_DUEL_GATE: "+rule.RuleId);
                }
                if(rule.Policy?.DamageConditionOrigin!=null && rule.Policy.DamageConditionOrigin!=AbilityDamageConditionOrigin.Unspecified &&
                   (definition.SourceKind!=AbilityRuleSourceKind.Skill || rule.ScheduledOnly))issues.Add("DEFERRED_CARD_ORIGIN_REQUIRES_SKILL_RULE: "+rule.RuleId);
                if(!HasSelector(rule.TargetSelectorId))issues.Add("UNKNOWN_SELECTOR: "+rule.TargetSelectorId);
                try{Compile(rule.Condition);}catch(Exception e){issues.Add(e.Message);}
                foreach(var op in rule.Operations??Array.Empty<AbilityOperationData>())
                    try{if(op==null)throw new ArgumentException("Null operation");
                        if(!Enum.IsDefined(typeof(AbilityOwnerScope),op.StateScope))throw new ArgumentException("INVALID_OPERATION_SCOPE");
                        if(op.TypeId=="yujin.growth"&&rule.Policy?.Snapshot!=AbilitySnapshotPolicy.LockAtActionStart)throw new ArgumentException("GROWTH_REQUIRES_ACTION_START_SNAPSHOT");
                        var issue=Operation(op.TypeId).Validate(new AbilityOperationSpec(op));if(issue!=null)issues.Add(issue);}
                    catch(Exception e){issues.Add(e.Message);}
            }
            return issues.AsReadOnly();
        }
    }
}
