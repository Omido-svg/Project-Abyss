using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ProjectAbyss.Abilities
{
    public static class CombatCommandTrace
    {
        /// <summary>Actor handles are normalized by first appearance so replay hashes survive fresh Unity instances.</summary>
        public static string Hash(int seed,IReadOnlyList<CombatCommandAppliedRecord> records)
        {
            var actors=new Dictionary<long,int>();var text=new StringBuilder();
            int Actor(long id){if(id<=0)return 0;if(!actors.TryGetValue(id,out var n))actors[id]=n=actors.Count+1;return n;}
            void Add(object value)
            {
                string s=Convert.ToString(value,CultureInfo.InvariantCulture)??"";
                text.Append(s.Length).Append(':').Append(s).Append('|');
            }
            Add(seed);
            if(records!=null)foreach(var r in records)
            {
                var c=r.Request;var receipt=r.Receipt;var v=receipt.Values;
                Add(c.Ability.DefinitionId);Add(Actor(c.Ability.OwnerActorRuntimeId));Add(c.Ability.EntryIndex);
                Add(c.ActionId);Add(c.CauseEventId);Add(c.SourceCause.RootActionId);Add(c.SourceCause.ParentEventId);
                Add(c.SourceCause.EventId);Add(c.SourceCause.SourceAbilityId);Add(Actor(c.SourceCause.SourceActorId));Add((int)c.SourceCause.OriginKind);Add(c.SourceCause.SourceRollIndex);
                Add(Actor(c.Target.ActorRuntimeId));Add(c.Target.PartId);Add(c.Target.SlotId);
                Add((int)c.Kind);Add(c.RequestedAmount);Add(c.Phase);Add(c.Priority);Add(c.RegistrationOrder);Add(c.CommandSequence);
                Add(r.CommitSequence);Add((int)receipt.Outcome);Add(receipt.EffectCommitted);Add((int)receipt.FailedBecause);
                Add(receipt.AppliedAmount);Add(receipt.AbsorbedAmount);Add(v.HitLanded);Add(v.AppliedHp);Add(v.AppliedPartHp);
                Add(v.AppliedStagger);Add((int)v.PartTransition);Add(v.KilledActor);Add(v.OpenedVulnerabilityWindow);
                Add(v.ResourceBefore);Add(v.ResourceAfter);
            }
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","").ToLowerInvariant();
        }
    }
}
