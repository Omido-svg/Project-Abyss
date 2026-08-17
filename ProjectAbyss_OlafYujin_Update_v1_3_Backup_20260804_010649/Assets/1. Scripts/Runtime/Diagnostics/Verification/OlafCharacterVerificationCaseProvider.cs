using System;
using System.Collections.Generic;
using System.Linq;

public sealed class OlafCharacterVerificationCaseProvider :
    ICharacterVerificationCaseProvider
{
    public const string SkillCoverage =
        "olaf.data.skill_coverage";

    public const string MechanicRegistration =
        "olaf.passive.mechanic_registration";

    public const string MadnessBoundary =
        "olaf.passive.madness_boundary";

    public const string MadnessRollModifier =
        "olaf.passive.madness_roll_modifier";

    public const string MadnessConsume =
        "olaf.prestige.madness_consume";

    public const string ImmortalFuryLifecycle =
        "olaf.prestige.backs_to_wall_lifecycle";

    private static readonly string[] RequiredSkillIds =
    {
        OlafSkillIds.EnduringSlash,
        OlafSkillIds.OverheadSmash,
        OlafSkillIds.WildHack,
        OlafSkillIds.Standard,
        OlafSkillIds.Rend,
        OlafSkillIds.Crouch,
        OlafSkillIds.Glare,
        OlafSkillIds.ShowOff,
        OlafSkillIds.BloomingWound,
        OlafSkillIds.BurstingMadness,
        OlafSkillIds.BacksToWall
    };

    public bool Supports(
        CharacterAuthoringBundle bundle,
        Character character)
    {
        return bundle?.Kind == CharacterAuthoringKind.Olaf ||
               character is Olaf;
    }

    public IEnumerable<CharacterVerificationCaseDefinition>
        CreateDefaultCases(
            CharacterAuthoringBundle bundle)
    {
        yield return CharacterVerificationCaseDefinition.Create(
            SkillCoverage,
            "올라프 스킬 ID 전체 등록",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "일반 3, 결투 2, 도사림 3, 위세 3의 고유 SkillId가 데이터 그래프에 모두 존재하는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            MechanicRegistration,
            "광기·배수진 메커닉 등록",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "OlafMadnessMechanic과 OlafImmortalFuryMechanic의 생성 및 BattleEvent 구독을 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            MadnessBoundary,
            "광기 경계값·만개",
            CharacterVerificationCategory.Boundary,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "광기 0~10 Clamp와 10에서 만개 상태가 되는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            MadnessRollModifier,
            "광기 판정 보정",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "광기 5당 판정 +1 규칙이 실제 Character.ModifyRoll 파이프라인에 적용되는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            MadnessConsume,
            "광기 소모 피해",
            CharacterVerificationCategory.Prestige,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "현재 광기 × 피해량 계산과 선택적 광기 소모를 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            ImmortalFuryLifecycle,
            "배수진 생존·턴 종료",
            CharacterVerificationCategory.Prestige,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "배수진 중 사망·부위 파괴 차단과 TurnEnd 해제를 검사합니다.");
    }

    public bool TryExecute(
        CharacterVerificationContext context,
        out CharacterVerificationCaseResult result)
    {
        result = context?.Definition?.CaseId switch
        {
            SkillCoverage =>
                VerifySkillCoverage(context),

            MechanicRegistration =>
                VerifyMechanics(context),

            MadnessBoundary =>
                VerifyMadnessBoundary(context),

            MadnessRollModifier =>
                VerifyMadnessRollModifier(context),

            MadnessConsume =>
                VerifyMadnessConsume(context),

            ImmortalFuryLifecycle =>
                VerifyImmortalFury(context),

            _ => null
        };

        return result != null;
    }

    private static CharacterVerificationCaseResult
        VerifySkillCoverage(
            CharacterVerificationContext context)
    {
        HashSet<string> existing =
            new HashSet<string>(
                context.Bundle
                    .EnumerateSkillDefinitions()
                    .Where(item => item != null)
                    .Select(item => item.SkillId),
                StringComparer.Ordinal);

        List<string> missing =
            RequiredSkillIds
                .Where(id => !existing.Contains(id))
                .ToList();

        return missing.Count == 0
            ? context.Pass(
                "올라프 고유 스킬 ID 11개 존재",
                "11/11 등록")
            : context.Fail(
                "올라프 고유 스킬 ID 11개 존재",
                $"{11 - missing.Count}/11 등록",
                "누락:\n" + string.Join("\n", missing));
    }

    private static CharacterVerificationCaseResult
        VerifyMechanics(
            CharacterVerificationContext context)
    {
        Olaf olaf =
            context.Character as Olaf;

        if (olaf == null)
        {
            return context.Fail(
                "Olaf Runtime Character",
                context.Character?.GetType().Name ?? "NULL");
        }

        OlafMadnessMechanic madness =
            olaf.MadnessMechanic;

        OlafImmortalFuryMechanic immortal =
            olaf.ImmortalFuryMechanic;

        bool valid =
            madness != null &&
            immortal != null &&
            madness.IsRegistered &&
            immortal.IsRegistered;

        return valid
            ? context.Pass(
                "광기·배수진 메커닉 생성 및 등록",
                "Madness=Registered, ImmortalFury=Registered")
            : context.Fail(
                "광기·배수진 메커닉 생성 및 등록",
                $"Madness={Describe(madness)}, " +
                $"ImmortalFury={Describe(immortal)}");
    }

    private static CharacterVerificationCaseResult
        VerifyMadnessBoundary(
            CharacterVerificationContext context)
    {
        Olaf olaf =
            context.Character as Olaf;

        OlafMadnessMechanic mechanic =
            olaf?.MadnessMechanic;

        if (mechanic == null)
        {
            return context.Fail(
                "OlafMadnessMechanic 존재",
                "NULL");
        }

        mechanic.SetMadnessForDebug(-100);
        bool lower =
            mechanic.CurrentMadness == 0 &&
            !mechanic.IsBlooming;

        mechanic.SetMadnessForDebug(999);
        bool upper =
            mechanic.CurrentMadness ==
            OlafMadnessMechanic.MaxMadnessValue &&
            mechanic.IsBlooming;

        mechanic.AddMadness(5);
        bool clamped =
            mechanic.CurrentMadness ==
            OlafMadnessMechanic.MaxMadnessValue;

        bool valid =
            lower &&
            upper &&
            clamped;

        return valid
            ? context.Pass(
                "광기 0~10 Clamp, 10에서 만개",
                $"Madness={mechanic.CurrentMadness}, " +
                $"Bloom={mechanic.IsBlooming}")
            : context.Fail(
                "광기 0~10 Clamp, 10에서 만개",
                $"Lower={lower}, Upper={upper}, Clamp={clamped}");
    }

    private static CharacterVerificationCaseResult
        VerifyMadnessRollModifier(
            CharacterVerificationContext context)
    {
        Olaf olaf =
            context.Character as Olaf;

        OlafMadnessMechanic mechanic =
            olaf?.MadnessMechanic;

        Skill skill =
            olaf?.RuntimeSkills?
                .FirstOrDefault(item => item != null);

        BodyPart part =
            olaf?.BodyParts?
                .FirstOrDefault(item => item != null);

        if (mechanic == null ||
            skill == null)
        {
            return context.Fail(
                "광기 판정 보정 실행 조건",
                mechanic == null
                    ? "Mechanic 없음"
                    : "RuntimeSkill 없음");
        }

        ActionSlot slot =
            new ActionSlot
            {
                ActionId = 1,
                Owner = olaf,
                Part = part,
                Skill = skill,
                Speed = 5
            };

        BattleAction action =
            new BattleAction
            {
                Slot = slot
            };

        mechanic.SetMadnessForDebug(0);
        int baseValue =
            olaf.ModifyRoll(
                action,
                10);

        mechanic.SetMadnessForDebug(5);
        int atFive =
            olaf.ModifyRoll(
                action,
                10);

        mechanic.SetMadnessForDebug(10);
        int atTen =
            olaf.ModifyRoll(
                action,
                10);

        bool valid =
            atFive == baseValue + 1 &&
            atTen == baseValue + 2;

        return valid
            ? context.Pass(
                "광기 5당 판정 +1",
                $"{baseValue}→{atFive}→{atTen}")
            : context.Fail(
                "광기 5당 판정 +1",
                $"{baseValue}→{atFive}→{atTen}");
    }

    private static CharacterVerificationCaseResult
        VerifyMadnessConsume(
            CharacterVerificationContext context)
    {
        OlafMadnessMechanic mechanic =
            (context.Character as Olaf)
                ?.MadnessMechanic;

        if (mechanic == null)
        {
            return context.Fail(
                "광기 피해 계산",
                "Mechanic 없음");
        }

        mechanic.SetMadnessForDebug(7);

        int previewDamage =
            mechanic.ConsumeMadnessForPrestigeDamage(
                null,
                5,
                false);

        int afterPreview =
            mechanic.CurrentMadness;

        int consumedDamage =
            mechanic.ConsumeMadnessForPrestigeDamage(
                null,
                5,
                true);

        int afterConsume =
            mechanic.CurrentMadness;

        bool valid =
            previewDamage == 35 &&
            afterPreview == 7 &&
            consumedDamage == 35 &&
            afterConsume == 0;

        return valid
            ? context.Pass(
                "광기 7 × 5 = 35, 미소모/소모 정책 정상",
                $"Preview={previewDamage}, " +
                $"Consume={consumedDamage}, " +
                $"Remain={afterConsume}")
            : context.Fail(
                "광기 7 × 5 = 35, 미소모/소모 정책 정상",
                $"Preview={previewDamage}/{afterPreview}, " +
                $"Consume={consumedDamage}/{afterConsume}");
    }

    private static CharacterVerificationCaseResult
        VerifyImmortalFury(
            CharacterVerificationContext context)
    {
        Olaf olaf =
            context.Character as Olaf;

        OlafImmortalFuryMechanic mechanic =
            olaf?.ImmortalFuryMechanic;

        BodyPart part =
            olaf?.BodyParts?
                .FirstOrDefault(item => item != null);

        if (mechanic == null)
        {
            return context.Fail(
                "OlafImmortalFuryMechanic 존재",
                "NULL");
        }

        mechanic.Activate(null);

        bool active =
            mechanic.IsActive &&
            !mechanic.CanOwnerDie() &&
            !mechanic.CanBreakOwnerPart(
                part,
                null);

        context.BattleContext
            ?._battleEvent
            ?.RaiseTurnEnd(1);

        bool released =
            !mechanic.IsActive &&
            mechanic.CanOwnerDie() &&
            mechanic.CanBreakOwnerPart(
                part,
                null);

        return active && released
            ? context.Pass(
                "배수진 중 생존·파괴 차단, TurnEnd 해제",
                "Active policy PASS / TurnEnd release PASS")
            : context.Fail(
                "배수진 중 생존·파괴 차단, TurnEnd 해제",
                $"Active={active}, Released={released}");
    }

    private static string Describe(
        CombatMechanic mechanic)
    {
        if (mechanic == null)
            return "NULL";

        return mechanic.IsRegistered
            ? "Registered"
            : "NotRegistered";
    }
}