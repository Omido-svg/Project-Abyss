using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R5 specialist adapter, opt-in only. It must never be registered in parallel
    /// with legacy SkillEffectDispatcher for the same authored effect.
    /// Each bound command carries an explicitly validated request payload.
    /// Existing DamageManager / BattleEffectResolver / SkillResourceAccess are
    /// the sole write authorities; no alternative damage calculation exists.
    /// </summary>
    public sealed class R5CombatSpecialistBackend : ICombatCommandBackend
    {
        private enum BindingType { Damage, CharacterStatus, PartStatus, Resource }
        private sealed class Binding
        {
            public BindingType Type;
            public DamageRequest Damage;
            public EffectRequest StatusRequest;
            public Character ResourceOwner;
            public string ResourceKey;
        }

        private readonly BattleContext battle;
        private readonly Dictionary<string, Binding> bindings =
            new Dictionary<string, Binding>(StringComparer.Ordinal);

        public R5CombatSpecialistBackend(BattleContext battle)
        {
            this.battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public int BoundCommandCount => bindings.Count;

        public bool CanApply(CombatCommandKind kind)
        {
            switch (kind)
            {
                case CombatCommandKind.DealDamage:
                    return battle.ResolveDamageManager() != null;
                case CombatCommandKind.ApplyCharacterStatus:
                case CombatCommandKind.ApplyPartStatus:
                    return battle.EffectResolver != null;
                case CombatCommandKind.ChangeResource:
                    return true;
                default:
                    return false; // unsupported commands must not be faked
            }
        }

        private static long Id(Character actor) => R4LiveSlotQuery.ActorRuntimeId(actor);

        private static bool TargetMatches(CombatCommandEnvelope command,
            Character actor, BodyPart part)
        {
            if (actor == null || Id(actor) == 0 ||
                command.Target.ActorRuntimeId != Id(actor))
                return false;
            if (part != null && part.Owner != actor)
                return false;
            return string.Equals(command.Target.PartId,
                part?.PartId ?? string.Empty, StringComparison.Ordinal);
        }

        private void Register(CombatCommandEnvelope command, Binding binding,
            Character target, BodyPart part)
        {
            if (string.IsNullOrWhiteSpace(command.CommandId) ||
                command.Kind == CombatCommandKind.Unknown ||
                !TargetMatches(command, target, part))
                throw new ArgumentException("R5 command identity/target does not match bound request.");

            if (bindings.ContainsKey(command.CommandId))
                throw new InvalidOperationException("Command ID already bound: " + command.CommandId);
            bindings.Add(command.CommandId, binding);
        }

        public void RegisterDamage(CombatCommandEnvelope command, DamageRequest request)
        {
            if (command.Kind != CombatCommandKind.DealDamage ||
                command.RequestedAmount != request.Damage ||
                request.Damage < 0 || request.RawPower < 0)
                throw new ArgumentException("Damage command kind/amount mismatch.");
            Register(command, new Binding { Type=BindingType.Damage, Damage=request },
                request.TargetCharacter, request.TargetPart);
        }

        public void RegisterCharacterStatus(CombatCommandEnvelope command, EffectRequest request)
        {
            if (command.Kind != CombatCommandKind.ApplyCharacterStatus ||
                request?.StatusEffect == null || request.TargetPart != null ||
                command.RequestedAmount <= 0)
                throw new ArgumentException("Invalid character-status payload.");
            Register(command, new Binding
            {
                Type=BindingType.CharacterStatus,StatusRequest=request
            },request.TargetCharacter,null);
        }

        public void RegisterPartStatus(CombatCommandEnvelope command, EffectRequest request)
        {
            if (command.Kind != CombatCommandKind.ApplyPartStatus ||
                request?.StatusEffect == null || request.TargetPart == null ||
                command.RequestedAmount <= 0)
                throw new ArgumentException("Invalid part-status payload.");
            Register(command, new Binding
            {
                Type=BindingType.PartStatus,StatusRequest=request
            },request.TargetCharacter,request.TargetPart);
        }

        public void RegisterResourceChange(CombatCommandEnvelope command,
            Character owner, string resourceKey)
        {
            if (command.Kind != CombatCommandKind.ChangeResource ||
                string.IsNullOrWhiteSpace(resourceKey))
                throw new ArgumentException("Invalid resource request.");
            Register(command, new Binding
            {
                Type=BindingType.Resource,ResourceOwner=owner,
                ResourceKey=resourceKey
            },owner,null);
        }

        public CombatCommandReceipt Apply(CombatCommandEnvelope command)
        {
            if (!bindings.TryGetValue(command.CommandId, out Binding b))
                return CombatCommandReceipt.Reject("No command payload registered.");
            if (!CanApply(command.Kind))
                return CombatCommandReceipt.Reject("Specialist service unavailable.");
            if (b.Type == BindingType.Damage && command.Kind != CombatCommandKind.DealDamage ||
                b.Type == BindingType.CharacterStatus && command.Kind != CombatCommandKind.ApplyCharacterStatus ||
                b.Type == BindingType.PartStatus && command.Kind != CombatCommandKind.ApplyPartStatus ||
                b.Type == BindingType.Resource && command.Kind != CombatCommandKind.ChangeResource)
                return CombatCommandReceipt.Reject("Registered payload kind mismatch.");

            switch (b.Type)
            {
                case BindingType.Damage:
                {
                    DamageRequest request = b.Damage;
                    if (!TargetMatches(command, request.TargetCharacter, request.TargetPart) ||
                        request.Damage != command.RequestedAmount)
                        return CombatCommandReceipt.Reject("Damage request no longer matches command.");
                    DamageContext result=battle.ResolveDamageManager().ApplyDamageContext(request);
                    if (result?.Result == null)
                        return CombatCommandReceipt.Reject("DamageManager rejected the request.");
                    int applied=Math.Max(0,result.AppliedDamage);
                    int stagger=Math.Max(0,result.StaggerDamageApplied);
                    bool committed=result.WasApplied || applied>0 || stagger>0 ||
                        result.WasKilled || result.WasWeakened || result.WasBroken;
                    return new CombatCommandReceipt(
                        committed ? CombatCommandOutcome.Applied :
                        result.GuardAbsorbed>0 ? CombatCommandOutcome.Absorbed :
                        CombatCommandOutcome.Rejected,
                        applied,Math.Max(0,result.GuardAbsorbed),committed,
                        "DamageManager.Result; hp="+result.AppliedHpDamage+
                        ",part="+result.AppliedPartDamage+",stagger="+stagger);
                }
                case BindingType.CharacterStatus:
                {
                    EffectRequest request=b.StatusRequest;
                    if (!TargetMatches(command,request.TargetCharacter,null))
                        return CombatCommandReceipt.Reject("Status target no longer matches.");
                    bool success=battle.EffectResolver.ApplyCharacterStatus(request);
                    return new CombatCommandReceipt(success ?
                        CombatCommandOutcome.Applied : CombatCommandOutcome.Rejected,
                        0,0,success,"BattleEffectResolver.ApplyCharacterStatus");
                }
                case BindingType.PartStatus:
                {
                    EffectRequest request=b.StatusRequest;
                    if (!TargetMatches(command,request.TargetCharacter,request.TargetPart))
                        return CombatCommandReceipt.Reject("Part status target no longer matches.");
                    bool success=battle.EffectResolver.ApplyBodyPartStatus(request);
                    return new CombatCommandReceipt(success ?
                        CombatCommandOutcome.Applied : CombatCommandOutcome.Rejected,
                        0,0,success,"BattleEffectResolver.ApplyBodyPartStatus");
                }
                case BindingType.Resource:
                {
                    Character owner=b.ResourceOwner;
                    if (!TargetMatches(command,owner,null) || owner.IsDead)
                        return CombatCommandReceipt.Reject("Resource owner no longer valid.");
                    int before=SkillResourceAccess.Get(owner,b.ResourceKey);
                    int after=SkillResourceAccess.Modify(owner,b.ResourceKey,command.RequestedAmount);
                    int delta=Math.Abs(after-before);
                    return new CombatCommandReceipt(delta>0 ?
                        CombatCommandOutcome.Applied : CombatCommandOutcome.Rejected,
                        delta,0,delta>0,"SkillResourceAccess.Modify; before="+before+
                        ",after="+after);
                }
            }
            return CombatCommandReceipt.Reject("Unsupported registered binding.");
        }

        // Battle-scope lifecycle owned by the caller / ability composition root.
        public void ClearBindings() => bindings.Clear();
    }
}
