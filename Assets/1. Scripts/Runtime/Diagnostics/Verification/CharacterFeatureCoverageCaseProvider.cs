using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CharacterFeatureCoverageCaseProvider :
    ICharacterVerificationCaseProvider
{
    public const string Manifest =
        "coverage.manifest.closed_world";

    public const string Mechanics =
        "coverage.mechanics.forced_scenarios";

    public const string SkillPrefix =
        "coverage.skill.";

    public bool Supports(
        CharacterAuthoringBundle bundle,
        Character character)
    {
        return bundle != null;
    }

    public IEnumerable<CharacterVerificationCaseDefinition>
        CreateDefaultCases(
            CharacterAuthoringBundle bundle)
    {
        yield return CharacterVerificationCaseDefinition.Create(
            Manifest,
            "완전 커버리지 명세 폐쇄성",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "모든 SkillDefinition·Effect·필수 Mechanic이 검증 카탈로그에 등록됐는지 검사합니다. 새 기능은 검증기 추가 전까지 FAIL합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            Mechanics,
            "패시브·메커닉 강제 시나리오",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "필수 패시브와 메커닉의 이벤트 조건을 직접 발생시켜 정확한 상태 변화를 검사합니다.");

        foreach (SkillDefinition definition
                 in CharacterVerificationScenarioTools.GetDefinitions(bundle))
        {
            yield return CharacterVerificationCaseDefinition.Create(
                SkillPrefix + definition.SkillId,
                $"스킬 완전 시나리오: {definition.SkillName}",
                ToCategory(definition.ActionType),
                CharacterVerificationExecutionMode.IsolatedRuntime,
                "자원 거부/소비, 모든 굴림, 피해 대상, 모든 Effect Entry 조건을 결정론적으로 강제 실행합니다.");
        }
    }

    public bool TryExecute(
        CharacterVerificationContext context,
        out CharacterVerificationCaseResult result)
    {
        string caseId = context?.Definition?.CaseId;

        if (caseId == Manifest)
        {
            result = VerifyManifest(context);
            return true;
        }

        if (caseId == Mechanics)
        {
            result = CharacterBehaviorCoverageCatalog.Verify(context);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(caseId) &&
            caseId.StartsWith(
                SkillPrefix,
                StringComparison.Ordinal))
        {
            string skillId = caseId.Substring(SkillPrefix.Length);
            SkillDefinition definition =
                CharacterVerificationScenarioTools.FindDefinition(
                    context.Bundle,
                    skillId);

            result = CharacterVerificationForcedScenarioRunner.RunSkill(
                context,
                definition);
            return true;
        }

        result = null;
        return false;
    }

    private static CharacterVerificationCaseResult VerifyManifest(
        CharacterVerificationContext context)
    {
        List<string> failures = new List<string>();
        List<string> details = new List<string>();
        List<SkillDefinition> definitions =
            CharacterVerificationScenarioTools.GetDefinitions(
                context.Bundle);

        if (definitions.Count == 0)
            failures.Add("SkillDefinition이 0개입니다.");

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        int effectCount = 0;

        foreach (SkillDefinition definition in definitions)
        {
            if (!ids.Add(definition.SkillId))
                failures.Add($"중복 SkillId: {definition.SkillId}");

            if (definition.CreateRuntimeSkill() == null)
                failures.Add($"RuntimeSkill 생성 실패: {definition.SkillName}");

            foreach (SkillEffectEntry entry
                     in definition.EnumerateEffectEntries())
            {
                effectCount++;

                if (entry?.Definition == null)
                {
                    failures.Add($"{definition.SkillName}: NULL Effect Entry");
                    continue;
                }

                if (!CharacterVerificationForcedScenarioRunner
                        .IsEffectSupported(entry.Definition))
                {
                    failures.Add(
                        $"{definition.SkillName}: 미등록 Effect " +
                        entry.Definition.GetType().FullName);
                }

                if (entry.Definition.Conditions != null)
                {
                    foreach (SkillEffectCondition condition
                             in entry.Definition.Conditions)
                    {
                        if (condition?.Type == SkillEffectConditionType.Always &&
                            condition.Invert)
                        {
                            failures.Add(
                                $"{definition.SkillName}/{entry.Definition.name}: " +
                                "Invert Always는 절대 발동할 수 없습니다.");
                        }
                    }
                }
            }
        }

        IReadOnlyList<string> required =
            CharacterBehaviorCoverageCatalog.GetRequiredMechanicNames(
                context.Bundle);

        details.Add($"Skills={definitions.Count}");
        details.Add($"Effects={effectCount}");
        details.Add(
            "RequiredMechanics=" +
            (required.Count == 0
                ? "NONE"
                : string.Join(", ", required)));

        return failures.Count == 0
            ? context.Pass(
                "모든 기능이 폐쇄형 검증 카탈로그에 등록됨",
                $"PASS / Skills={definitions.Count}, Effects={effectCount}",
                string.Join("\n", details))
            : context.Fail(
                "모든 기능이 폐쇄형 검증 카탈로그에 등록됨",
                $"FAIL {failures.Count}",
                string.Join("\n", failures) +
                "\n\n" + string.Join("\n", details));
    }

    private static CharacterVerificationCategory ToCategory(
        ActionType actionType)
    {
        return actionType switch
        {
            ActionType.NormalAttack => CharacterVerificationCategory.NormalAttack,
            ActionType.Duel => CharacterVerificationCategory.Duel,
            ActionType.Preparation => CharacterVerificationCategory.Preparation,
            ActionType.Prestige => CharacterVerificationCategory.Prestige,
            _ => CharacterVerificationCategory.Boundary
        };
    }
}
