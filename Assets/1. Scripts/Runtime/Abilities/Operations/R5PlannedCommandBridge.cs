using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R5 migration bridge: converts a read-only AbilityRunner plan into
    /// explicit, cause-addressable requests. Conversion does NOT execute
    /// commands or register a specialist payload. That second step is
    /// mandatory and prevents string-based intents from writing on their own.
    /// </summary>
    public static class R5PlannedCommandBridge
    {
        public static IReadOnlyList<CombatCommandEnvelope> Materialize(
            AbilityInstance ability, AbilityEventContext context,
            AbilityRunner.PlannedExecution planned, string batchId,
            int phase, int priority = 0, int registrationOrder = 0)
        {
            if(ability == null)throw new ArgumentNullException(nameof(ability));
            if(planned == null)throw new ArgumentNullException(nameof(planned));
            if(string.IsNullOrWhiteSpace(batchId))
                throw new ArgumentException("Batch ID must be provided.", nameof(batchId));
            if(context.ActionId <= 0 || context.CauseId <= 0 ||
               context.OwnerActorRuntimeId != ability.Identity.OwnerActorRuntimeId)
                throw new InvalidOperationException("Materialization requires exact action/cause/owner IDs.");
            if(planned.Result.AppliedCommands != 0)
                throw new InvalidOperationException("A plan cannot already claim applied gameplay.");

            var materialized=new List<CombatCommandEnvelope>();
            for(int i=0;i<planned.Commands.Count;i++)
            {
                var intent=planned.Commands[i];
                if(!Enum.TryParse(intent.OperationKind, false,
                    out CombatCommandKind kind) ||
                   kind == CombatCommandKind.Unknown ||
                   !Enum.IsDefined(typeof(CombatCommandKind),kind))
                    throw new InvalidOperationException("Unknown explicit operation kind: "+intent.OperationKind);
                if(intent.Target.ActorRuntimeId <= 0)
                    throw new InvalidOperationException("No valid target ActorRuntimeId for planned command.");

                var commandId = batchId + ":" + i.ToString("D4");
                materialized.Add(new CombatCommandEnvelope(
                    commandId, ability.Identity, kind, context.ActionId,
                    context.CauseId, intent.Target,intent.Magnitude,
                    phase,priority,registrationOrder,i));
            }
            return materialized.AsReadOnly();
        }
    }

    /// <summary>
    /// R5 first opt-in data operation using only target refs and an amount.
    /// The caller must later bind the generated envelope to a specific
    /// Character and ResourceKey on the real specialist backend.
    /// </summary>
    public sealed class ChangeResourceOperation : IEffectOperation
    {
        private readonly int delta;
        public ChangeResourceOperation(int delta) { this.delta=delta; }

        public void Plan(IReadOnlyAbilityContext context,
            IReadOnlyList<CombatTargetRef> targets, ICombatCommandSink sink)
        {
            if(context == null || targets == null || sink == null)
                return;
            foreach(var target in targets)
                if(target.ActorRuntimeId > 0 && delta != 0)
                    sink.Enqueue(new CombatCommandIntent(
                        nameof(CombatCommandKind.ChangeResource),target,delta));
        }
    }
}
