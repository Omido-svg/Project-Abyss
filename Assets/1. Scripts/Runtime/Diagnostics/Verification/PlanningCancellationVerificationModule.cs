using System.Collections.Generic;
using System.Linq;

public sealed class PlanningCancellationVerificationModule :
    IGameSystemVerificationModule
{
    public string ModuleId =>
        "planning.cancellation";

    public int Order => 225;

    public IEnumerable<GameSystemVerificationCase> BuildCases()
    {
        yield return new GameSystemVerificationCase(
            "planning.cancel.explicit_energy_refund",
            "C-04",
            "명시적 Planning 취소는 commit Energy 환불",
            GameSystemVerificationCategory.Planning,
            GameSystemVerificationExecutionMode.IsolatedRuntime,
            "UI Cancel: 슬롯 제거 + 해당 슬롯 Energy 환불 / runtime Remove: 환불 없음",
            VerifyExplicitCancelRefund);

        yield return new GameSystemVerificationCase(
            "planning.cancel.preparation_reselectable",
            "C-03",
            "도사림 즉시 실행 후 취소·재선택 가능",
            GameSystemVerificationCategory.Planning,
            GameSystemVerificationExecutionMode.IsolatedRuntime,
            "Preparation commit -> Cancel -> Energy/Usage 복원 -> 동일 도사림 재선택 가능",
            VerifyPreparationCancelAndReselect);
    }

    private static bool TryFixture(
        GameSystemVerificationContext context,
        out GameSystemVerificationFixture fixture,
        out GameSystemVerificationProbeResult skip)
    {
        if (context.TryGetFixture(
                out fixture,
                out string reason))
        {
            skip = null;
            return true;
        }

        skip =
            GameSystemVerificationProbeResult.Skip(
                reason);
        return false;
    }

    private static GameSystemVerificationProbeResult
        VerifyExplicitCancelRefund(
            GameSystemVerificationContext context)
    {
        if (!TryFixture(
                context,
                out GameSystemVerificationFixture fixture,
                out GameSystemVerificationProbeResult skip))
        {
            return skip;
        }

        if (!TryFindPaidCombatSkill(
                fixture.Player,
                out BodyPart part,
                out Skill skill))
        {
            return GameSystemVerificationProbeResult.Skip(
                "빛 비용이 있는 선택 가능한 공격 스킬이 없습니다.");
        }

        BodyPart targetPart =
            fixture.Enemy.BodyParts?
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    !candidate.IsBroken);

        BattleActionPlanCommandService commands =
            new(
                fixture.ActionManager,
                fixture.SpeedManager);

        int energyBefore =
            fixture.Player.CurrentEnergy;

        ActionPlanAssignmentResult assigned =
            commands.TryAssign(
                new ActionPlanAssignmentRequest
                {
                    Owner = fixture.Player,
                    OwnerPart = part,
                    Skill = skill,
                    ActionIndex = 0,
                    Target = fixture.Enemy,
                    TargetPart = targetPart,
                    TargetRule =
                        TargetSelectionRule.StandardAttack
                });

        if (!assigned.Success ||
            assigned.Slot == null)
        {
            return GameSystemVerificationProbeResult.Fail(
                "Assign failed",
                assigned.FailureReason);
        }

        int energyAfterCommit =
            fixture.Player.CurrentEnergy;

        bool cancelled =
            commands.Cancel(
                fixture.Player,
                part,
                0,
                out string cancelFailure);

        int energyAfterCancel =
            fixture.Player.CurrentEnergy;

        bool removed =
            fixture.ActionManager.FindSlot(
                fixture.Player,
                part,
                0) == null;

        bool pass =
            cancelled &&
            removed &&
            energyAfterCommit ==
                energyBefore - skill.EnergyCost &&
            energyAfterCancel == energyBefore;

        string actual =
            $"Cancelled={cancelled}, Removed={removed}, " +
            $"Energy={energyBefore}->{energyAfterCommit}->{energyAfterCancel}, " +
            $"Cost={skill.EnergyCost}";

        return pass
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(
                actual,
                cancelFailure);
    }

    private static GameSystemVerificationProbeResult
        VerifyPreparationCancelAndReselect(
            GameSystemVerificationContext context)
    {
        if (!TryFixture(
                context,
                out GameSystemVerificationFixture fixture,
                out GameSystemVerificationProbeResult skip))
        {
            return skip;
        }

        if (!TryFindPreparation(
                fixture.Player,
                out BodyPart part,
                out Skill skill,
                out string choiceId))
        {
            return GameSystemVerificationProbeResult.Skip(
                "선택 가능한 도사림이 없습니다.");
        }

        BattleActionPlanCommandService commands =
            new(
                fixture.ActionManager,
                fixture.SpeedManager);

        int energyBefore =
            fixture.Player.CurrentEnergy;

        int usageBefore =
            skill.UseCountThisTurn;

        ActionPlanAssignmentRequest request =
            new ActionPlanAssignmentRequest
            {
                Owner = fixture.Player,
                OwnerPart = part,
                Skill = skill,
                ActionIndex = 0,
                Target = fixture.Player,
                TargetPart = part,
                TargetRule =
                    TargetSelectionRule.LivingPartOnly,
                PlanningChoiceId = choiceId
            };

        ActionPlanAssignmentResult first =
            commands.TryAssign(request);

        if (!first.Success ||
            first.Slot == null)
        {
            return GameSystemVerificationProbeResult.Fail(
                "First assign failed",
                first.FailureReason);
        }

        int energyAfterCommit =
            fixture.Player.CurrentEnergy;

        int usageAfterCommit =
            skill.UseCountThisTurn;

        bool cancelled =
            commands.Cancel(
                fixture.Player,
                part,
                0,
                out string cancelFailure);

        int energyAfterCancel =
            fixture.Player.CurrentEnergy;

        int usageAfterCancel =
            skill.UseCountThisTurn;

        ActionPlanAssignmentResult second =
            commands.TryAssign(request);

        bool reselected =
            second.Success &&
            second.Slot != null;

        // Probe가 다음 Case에 상태를 남기지 않도록 정리.
        if (reselected)
        {
            commands.Cancel(
                fixture.Player,
                part,
                0,
                out _);
        }

        // first.Slot은 Cancel 과정에서 PlanningEffectCommitted=false로 되므로
        // commit 여부는 usage/energy 변화로 검증한다.
        bool pass =
            cancelled &&
            energyAfterCommit ==
                energyBefore - skill.EnergyCost &&
            energyAfterCancel == energyBefore &&
            usageAfterCommit == usageBefore + 1 &&
            usageAfterCancel == usageBefore &&
            reselected;

        string actual =
            $"Skill={skill.SkillName}, Cancelled={cancelled}, Reselect={reselected}, " +
            $"Energy={energyBefore}->{energyAfterCommit}->{energyAfterCancel}, " +
            $"Usage={usageBefore}->{usageAfterCommit}->{usageAfterCancel}";

        return pass
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(
                actual,
                cancelFailure);
    }

    private static bool TryFindPaidCombatSkill(
        Character owner,
        out BodyPart part,
        out Skill skill)
    {
        part = null;
        skill = null;

        if (owner?.BodyParts == null)
            return false;

        foreach (BodyPart candidatePart
                 in owner.BodyParts)
        {
            if (candidatePart == null ||
                candidatePart.IsBroken)
            {
                continue;
            }

            IReadOnlyList<Skill> selectable =
                owner.GetSelectableSkills(
                    candidatePart,
                    0);

            Skill found =
                selectable?
                    .FirstOrDefault(candidate =>
                        candidate != null &&
                        candidate.EnergyCost > 0 &&
                        candidate.EnergyCost <=
                            owner.CurrentEnergy &&
                        (candidate.ActionType ==
                            ActionType.NormalAttack ||
                         candidate.ActionType ==
                            ActionType.Duel));

            if (found == null)
                continue;

            part = candidatePart;
            skill = found;
            return true;
        }

        return false;
    }

    private static bool TryFindPreparation(
        Character owner,
        out BodyPart part,
        out Skill skill,
        out string choiceId)
    {
        part = null;
        skill = null;
        choiceId = null;

        if (owner?.BodyParts == null)
            return false;

        foreach (BodyPart candidatePart
                 in owner.BodyParts)
        {
            if (candidatePart == null ||
                candidatePart.IsBroken)
            {
                continue;
            }

            IReadOnlyList<Skill> selectable =
                owner.GetSelectableSkills(
                    candidatePart,
                    0);

            if (selectable == null)
                continue;

            foreach (Skill candidate in selectable)
            {
                if (candidate == null ||
                    candidate.ActionType !=
                        ActionType.Preparation ||
                    candidate.EnergyCost >
                        owner.CurrentEnergy)
                {
                    continue;
                }

                ActionPlanningSkillContext planningContext =
                    new(
                        owner,
                        candidatePart,
                        candidate,
                        0,
                        owner.BattleContext?
                            .Services?
                            .ActionManager?
                            .Slots);

                IReadOnlyList<ActionPlanningChoiceOption> choices =
                    ActionPlanningMechanicPolicy
                        .GetPlanningChoices(
                            planningContext);

                string resolvedChoice =
                    choices != null &&
                    choices.Count > 0
                        ? choices[0]?.Id
                        : null;

                ActionPlanningSkillContext selectedContext =
                    new(
                        owner,
                        candidatePart,
                        candidate,
                        0,
                        owner.BattleContext?
                            .Services?
                            .ActionManager?
                            .Slots,
                        resolvedChoice);

                string blockReason =
                    ActionPlanningMechanicPolicy
                        .GetSkillSelectionBlockReason(
                            selectedContext);

                if (!string.IsNullOrWhiteSpace(
                        blockReason))
                {
                    continue;
                }

                part = candidatePart;
                skill = candidate;
                choiceId = resolvedChoice;
                return true;
            }
        }

        return false;
    }
}
