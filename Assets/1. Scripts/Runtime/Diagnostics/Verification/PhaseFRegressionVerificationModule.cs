using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Phase F에서 §1~§19 Traceability를 닫을 때 필요한 기존 P0 회귀 항목.
/// 새 규칙을 추가하지 않고 이미 구현된 D-05/D-07 계약을 독립 fixture에서 재검증한다.
/// </summary>
public sealed class PhaseFRegressionVerificationModule : IGameSystemVerificationModule
{
    public string ModuleId => "phase_f.regression";
    public int Order => 850;

    public IEnumerable<GameSystemVerificationCase> BuildCases()
    {
        yield return Runtime(
            "phasef.regression.pairing.last_challenger",
            "C-06",
            "P0 D-05 마지막 조준자가 합 획득",
            GameSystemVerificationCategory.Pairing,
            "같은 상대 공격 슬롯을 둘이 조준하면 ActionId가 큰 마지막 조준자만 합을 가져감",
            VerifyLastChallengerWins);

        yield return Runtime(
            "phasef.regression.heal.dual_pool",
            string.Empty,
            "P0 D-07 회복 Whole + 최저 부위 동시 적용",
            GameSystemVerificationCategory.Damage,
            "RestoreCurrentHP(N)은 Whole HP +N과 최저 비파괴 부위 +N을 동시에 적용하고 Weakened 상태를 풀지 않음",
            VerifyDualPoolHealing);
    }

    private static GameSystemVerificationCase Runtime(
        string id,
        string requirement,
        string name,
        GameSystemVerificationCategory category,
        string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> probe) =>
        new(
            id,
            requirement,
            name,
            category,
            GameSystemVerificationExecutionMode.IsolatedRuntime,
            expected,
            probe,
            true);

    private static bool Fixture(
        GameSystemVerificationContext context,
        out GameSystemVerificationFixture fixture,
        out GameSystemVerificationProbeResult failure)
    {
        if (context.TryGetFixture(out fixture, out string reason))
        {
            failure = null;
            return true;
        }

        failure = GameSystemVerificationProbeResult.Fail(reason);
        return false;
    }

    private static GameSystemVerificationProbeResult VerifyLastChallengerWins(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out GameSystemVerificationProbeResult fail))
            return fail;

        Skill playerSkill = f.Player?.RuntimeSkills?.FirstOrDefault(x => x != null && x.CanClash);
        Skill enemySkill = f.Enemy?.RuntimeSkills?.FirstOrDefault(x => x != null && x.CanClash);
        if (playerSkill == null || enemySkill == null)
            return GameSystemVerificationProbeResult.Fail("clashable player/enemy skill missing");

        ActionSlot incoming = new()
        {
            ActionId = 100,
            ActionIndex = 0,
            Owner = f.Enemy,
            Skill = enemySkill,
            Speed = 8,
            Phase = ActionPhase.COMBAT,
            TargetCharacter = f.Player
        };

        ActionSlot early = new()
        {
            ActionId = 101,
            ActionIndex = 0,
            Owner = f.Player,
            Skill = playerSkill,
            Speed = 9,
            Phase = ActionPhase.COMBAT,
            TargetCharacter = f.Enemy,
            TargetSlot = incoming
        };

        ActionSlot late = new()
        {
            ActionId = 102,
            ActionIndex = 1,
            Owner = f.Player,
            Skill = playerSkill,
            Speed = 1,
            Phase = ActionPhase.COMBAT,
            TargetCharacter = f.Enemy,
            TargetSlot = incoming
        };

        IReadOnlyList<ActionSlot> slots = new[] { early, late, incoming };
        ClashMatchPolicy policy = new(new ActionPhaseSorter());
        ActionSlot earlyMatch = policy.FindBestMatch(early, slots, new HashSet<ActionSlot>());
        ActionSlot lateMatch = policy.FindBestMatch(late, slots, new HashSet<ActionSlot>());

        bool pass = earlyMatch == null && ReferenceEquals(lateMatch, incoming);
        string actual = $"Early={(earlyMatch == null ? "ONE-SIDED" : "CLASH")}, Late={(ReferenceEquals(lateMatch, incoming) ? "CLASH" : "NONE")}";
        return pass
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(actual);
    }

    private static GameSystemVerificationProbeResult VerifyDualPoolHealing(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out GameSystemVerificationProbeResult fail))
            return fail;

        Character owner = f.Player;
        List<BodyPart> parts = owner?.BodyParts?.Where(x => x != null && !x.IsBroken).ToList();
        if (owner?.RuntimeStatus == null || parts == null || parts.Count == 0)
            return GameSystemVerificationProbeResult.Fail("player runtime status/non-broken parts missing");

        BodyPart selected = parts.FirstOrDefault(x => x.Type != PartType.HEAD) ?? parts[0];
        int selectedMax = Mathf.Max(10, Mathf.RoundToInt(selected.MaxPartHP));
        selected.SetDebugState(2, selectedMax, true, false);

        foreach (BodyPart part in parts)
        {
            if (ReferenceEquals(part, selected))
                continue;
            int max = Mathf.Max(10, Mathf.RoundToInt(part.MaxPartHP));
            part.SetDebugState(Mathf.Min(max, 9), max, false, false);
        }

        owner.RuntimeStatus.currentHP = Mathf.Max(1, owner.MaxCombatHP - 10);
        int wholeBefore = owner.CurrentHP;
        int partBefore = Mathf.RoundToInt(selected.PartHP);
        bool weakenedBefore = selected.IsWeakened;

        owner.RestoreCurrentHP(5);

        int wholeAfter = owner.CurrentHP;
        int partAfter = Mathf.RoundToInt(selected.PartHP);
        bool pass = wholeAfter == Mathf.Min(owner.MaxCombatHP, wholeBefore + 5) &&
                    partAfter == Mathf.Min(selectedMax, partBefore + 5) &&
                    weakenedBefore && selected.IsWeakened;

        string actual = $"Whole={wholeBefore}->{wholeAfter}, Part={partBefore}->{partAfter}, Weakened={weakenedBefore}->{selected.IsWeakened}";
        return pass
            ? GameSystemVerificationProbeResult.Pass(actual)
            : GameSystemVerificationProbeResult.Fail(actual);
    }
}
