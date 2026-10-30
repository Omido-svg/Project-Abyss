using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R5-W opt-in bridge: resolves deterministic SlotWeighted target allocation,
    /// then adapts it to the EXISTING AttackWeightTarget/ClashManager/DamageManager
    /// path. No new damage calculator, clash scoring or event dispatcher.
    /// Never falls back to random target selection after a failed opt-in.
    /// </summary>
    public static class R5SlotWeightedTargetBridge
    {
        public static IReadOnlyList<AttackWeightTarget> Resolve(
            BattleContext battle, BattleAction action)
        {
            TryResolve(battle, action, out var targets, out _);
            return targets;
        }

        public static bool TryResolve(BattleContext battle, BattleAction action,
            out IReadOnlyList<AttackWeightTarget> resolved, out string diagnostic)
        {
            var targets=new List<AttackWeightTarget>();
            diagnostic=string.Empty;
            resolved=targets.AsReadOnly();

            if(action?.Owner==null || action.Target==null || action.Skill==null ||
               action.Target.IsDead || action.Owner.IsDead)
            {
                diagnostic="Missing or invalid attacker/target/skill.";
                return false;
            }
            var primary=new AttackWeightTarget(0,action.Target,action.TargetPart,true);
            targets.Add(primary);
            resolved=targets.AsReadOnly();
            if(action.Skill.AttackWeight<=1)
                return true;

            if(battle==null)
            {
                diagnostic="BattleContext unavailable; main-only.";
                return false;
            }

            IReadOnlyList<Character> hostileSide=null;
            if(action.Owner==battle.Player && battle.Enemies!=null &&
               battle.Enemies.Contains(action.Target))
                hostileSide=battle.Enemies;
            else if(battle.Enemies!=null && battle.Enemies.Contains(action.Owner) &&
                    action.Target==battle.Player)
                hostileSide=new Character[]{battle.Player};

            if(hostileSide==null)
            {
                diagnostic="Unverified hostile-side relationship; main-only.";
                return false;
            }

            // Make a read-only planning projection for the *resolved* clash target,
            // not action.Slot's possibly stale declared target.
            // This projection is NOT inserted into ActionManager.
            var projection=new ActionSlot
            {
                ActionId=action.ActionId,
                Owner=action.Owner,
                TargetCharacter=action.Target,
                TargetPart=action.TargetPart,
                TargetSlot=null,
                Phase=ActionPhase.COMBAT
            };
            var snapshot=R4LiveSlotQuery.Capture(projection,hostileSide,
                null,planningRevision:0,effectiveWeight:action.Skill.AttackWeight);

            if(snapshot?.Selection?.Success!=true ||
               !snapshot.HasUnambiguousMain ||
               snapshot.Selection.Allocation?.DistinctHitTargets==null)
            {
                diagnostic="Slot allocation missing/ambiguous; main-only. "+
                    string.Join(";",snapshot?.Diagnostics ?? Array.Empty<string>());
                return false;
            }
            var hits=snapshot.Selection.Allocation.DistinctHitTargets;
            var mainKey=new HitTargetKey(R4LiveSlotQuery.ActorRuntimeId(action.Target),
                action.TargetPart?.PartId);
            if(hits.Count==0 || !hits[0].Equals(mainKey))
            {
                diagnostic="Original main hit no longer matches allocated primary.";
                return false;
            }

            var seen=new HashSet<HitTargetKey>{mainKey};
            int weightIndex=1;
            for(int i=1;i<hits.Count;i++)
            {
                var hit=hits[i];
                if(!seen.Add(hit))
                    continue;
                Character actor=null;
                foreach(var member in hostileSide)
                    if(member!=null && !member.IsDead &&
                       R4LiveSlotQuery.ActorRuntimeId(member)==hit.ActorRuntimeId)
                    {
                        actor=member;
                        break;
                    }
                if(actor==null)
                    continue;

                BodyPart part=null;
                if(actor.UsesBodyParts)
                {
                    if(string.IsNullOrWhiteSpace(hit.PartId))
                        continue;
                    var point=actor.GetTargetPoints(false).FirstOrDefault(p=>
                        p.IsValid && p.Character==actor &&
                        string.Equals(p.Part?.PartId,hit.PartId,StringComparison.Ordinal));
                    if(!point.IsValid || point.Part==null ||
                       point.Part.IsBroken || !actor.IsValidTargetPart(point.Part))
                        continue;
                    part=point.Part;
                }
                else if(!string.IsNullOrEmpty(hit.PartId))
                    continue;

                // One AttackWeightTarget per distinct actor/part; damage is
                // subsequently performed by ClashManager only if the roll hits.
                targets.Add(new AttackWeightTarget(weightIndex++,actor,part,false));
            }
            resolved=targets.AsReadOnly();
            return true;
        }
    }
}
