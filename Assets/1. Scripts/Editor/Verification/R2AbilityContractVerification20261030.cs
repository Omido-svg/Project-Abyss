#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R2AbilityContractVerification20261030
{
    [MenuItem("Game System Verification/R2 - Contract Isolation (Edit Mode)")]
    public static void Verify()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Run R2 contract verification in Edit Mode.");

        int pass = 0; int fail = 0;
        void Check(string id, bool value)
        {
            if (value) { pass++; Debug.Log("[R2-Contract] PASS " + id); }
            else { fail++; Debug.LogError("[R2-Contract] FAIL " + id); }
        }

        var shared = new AbilityDefinitionRef("skill:sample", "stable-guid");
        var identityA = new AbilityIdentity(shared.StableDefinitionId, 101, "LEFT", 0);
        var identityB = new AbilityIdentity(shared.StableDefinitionId, 102, "LEFT", 0);
        var a = new AbilityInstance(shared, identityA);
        var b = new AbilityInstance(shared, identityB);
        var eventKey = new ActivationKey(55, AbilityEventKind.RollUsing, 0);
        a.RuntimeState.Commit(eventKey, new AbilityExecutionResult(1, 1, null));

        Check("different ActorRuntimeId -> distinct identities", !identityA.Equals(identityB));
        Check("shared definition -> independent activation memory",
            a.RuntimeState.SuccessfulActivationCount == 1 &&
            b.RuntimeState.SuccessfulActivationCount == 0 &&
            !a.RuntimeState.CanActivate(eventKey) && b.RuntimeState.CanActivate(eventKey));
        a.RuntimeState.Commit(new ActivationKey(56, AbilityEventKind.RollUsing, 0),
            new AbilityExecutionResult(1, 0, "not applied"));
        Check("planned-only command is NOT committed",
            a.RuntimeState.SuccessfulActivationCount == 1);
        a.RuntimeState.ResetAt(AbilityLifetimeBoundary.Battle);
        Check("battle boundary clears activation memory", a.RuntimeState.SuccessfulActivationCount == 0);

        var slotA = new CombatSlotRef(201, "ARM-1", "HEAD", 1);
        var slotB = new CombatSlotRef(201, "ARM-2", "HEAD", 1);
        var hitA = new HitTargetKey(201, "HEAD");
        var hitB = new HitTargetKey(201, "HEAD");
        Check("distinct slots can map to one physical hit target",
            !slotA.Equals(slotB) && hitA.Equals(hitB));
        var allocation = new AttackWeightAllocation(88, 1, 2,
            new[] { new SelectedSlotAllocation(slotA, true, 1, 1, 0, 0),
                    new SelectedSlotAllocation(slotB, false, 1, 1, 1, 0) },
            new[] { hitA });
        Check("weight selection and hit target are distinct",
            allocation.SelectedSlots.Count == 2 && allocation.DistinctHitTargets.Count == 1);

        var reactionA = new ReactionRequest(1, 55, 50, 44, 33, 101,
            "skill:X", ReactionTimingCheckpoint.AfterExchangeSettled, 1, 0);
        var reactionB = new ReactionRequest(2, 55, 50, 44, 33, 101,
            "skill:X", ReactionTimingCheckpoint.AfterExchangeSettled, 1, 1);
        Check("two reactions retain distinct IDs and parent/root lineage",
            reactionA.RequestId != reactionB.RequestId &&
            reactionA.ParentActionId == reactionB.ParentActionId &&
            reactionA.RootActionId == reactionB.RootActionId);

        Debug.Log("[R2-Contract] RESULT PASS=" + pass + " FAIL=" + fail);
        if(fail>0)throw new Exception("R2 contract checks failed: " + fail);
    }
}
#endif
