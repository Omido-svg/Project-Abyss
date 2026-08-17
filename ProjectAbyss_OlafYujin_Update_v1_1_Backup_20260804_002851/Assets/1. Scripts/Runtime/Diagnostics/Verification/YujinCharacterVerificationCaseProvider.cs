using System;
using System.Collections.Generic;
using System.Linq;

public sealed class YujinCharacterVerificationCaseProvider :
    ICharacterVerificationCaseProvider
{
    public const string SkillCoverage =
        "yujin.data.skill_coverage";

    public const string MechanicRegistration =
        "yujin.passive.mechanic_registration";

    public const string UnlimitedWeaponSwitch =
        "yujin.passive.unlimited_weapon_switch";

    public const string WeaponProfiles =
        "yujin.passive.weapon_profiles";

    public const string SenseTurnStart =
        "yujin.passive.sense_turn_start";

    public const string SenseReroll =
        "yujin.duel.sense_reroll";

    public const string MarkIgnition =
        "yujin.passive.mark_ignition_nakil";

    private static readonly string[] RequiredSkillIds =
    {
        YujinSkillIds.Inspection,
        YujinSkillIds.Breakfast,
        YujinSkillIds.Inscription,
        YujinSkillIds.Pursuit,
        YujinSkillIds.Capture,
        YujinSkillIds.Sentencing,
        YujinSkillIds.Brand,
        YujinSkillIds.JointLiability,
        YujinSkillIds.Retrial
    };

    public bool Supports(
        CharacterAuthoringBundle bundle,
        Character character)
    {
        return bundle?.Kind == CharacterAuthoringKind.Yujin ||
               character is Yujin;
    }

    public IEnumerable<CharacterVerificationCaseDefinition>
        CreateDefaultCases(
            CharacterAuthoringBundle bundle)
    {
        yield return CharacterVerificationCaseDefinition.Create(
            SkillCoverage,
            "유진 스킬 ID 전체 등록",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "일반 2, 결투 2, 도사림 2, 위세 3의 고유 SkillId가 데이터 그래프에 모두 존재하는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            MechanicRegistration,
            "유진 통합 메커닉 등록",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "무기·살수의 감·표식 메커닉 생성과 BattleEvent 구독을 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            UnlimitedWeaponSwitch,
            "무기 무제한 전환",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "같은 턴에 백우→적설→낙일→백우 전환과 WeaponChanged 이벤트를 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            WeaponProfiles,
            "무기 프로필 수치",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "백우·적설·낙일의 코인 수, 앞면 확률, 크리값, 표식량을 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            SenseTurnStart,
            "살수의 감 턴 시작 획득",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "TurnStart 이벤트 한 번에 살수의 감이 정확히 1 증가하는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            SenseReroll,
            "살수의 감 자동 재굴림",
            CharacterVerificationCategory.Duel,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "각인·추격 패배, 비크리티컬, 자동 사용 ON에서 감 1 소모와 1회 재굴림을 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            MarkIgnition,
            "표식 44·낙일 발화",
            CharacterVerificationCategory.Passive,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "표식 44 도달 시 표식 초기화와 낙일의 대상 부위 약화를 검사합니다.");
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
                VerifyMechanic(context),

            UnlimitedWeaponSwitch =>
                VerifyUnlimitedWeaponSwitch(context),

            WeaponProfiles =>
                VerifyWeaponProfiles(context),

            SenseTurnStart =>
                VerifySenseTurnStart(context),

            SenseReroll =>
                VerifySenseReroll(context),

            MarkIgnition =>
                VerifyMarkIgnition(context),

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
                "유진 고유 스킬 ID 9개 존재",
                "9/9 등록")
            : context.Fail(
                "유진 고유 스킬 ID 9개 존재",
                $"{9 - missing.Count}/9 등록",
                "누락:\n" + string.Join("\n", missing));
    }

    private static CharacterVerificationCaseResult
        VerifyMechanic(
            CharacterVerificationContext context)
    {
        Yujin yujin =
            context.Character as Yujin;

        YujinMechanic mechanic =
            yujin?.YujinMechanic;

        bool valid =
            mechanic != null &&
            mechanic.IsRegistered &&
            mechanic.Owner == yujin;

        return valid
            ? context.Pass(
                "YujinMechanic 생성·등록·Owner 연결",
                "Registered")
            : context.Fail(
                "YujinMechanic 생성·등록·Owner 연결",
                mechanic == null
                    ? "NULL"
                    : $"Registered={mechanic.IsRegistered}, " +
                      $"OwnerMatch={mechanic.Owner == yujin}");
    }

    private static CharacterVerificationCaseResult
        VerifyUnlimitedWeaponSwitch(
            CharacterVerificationContext context)
    {
        YujinMechanic mechanic =
            (context.Character as Yujin)
                ?.YujinMechanic;

        if (mechanic == null)
        {
            return context.Fail(
                "유진 무기 전환",
                "Mechanic 없음");
        }

        if (mechanic.CurrentWeapon !=
            YujinWeaponType.Baeku)
        {
            mechanic.TrySwitchWeapon(
                YujinWeaponType.Baeku);
        }

        int eventCount = 0;
        List<string> transitions =
            new List<string>();

        void OnChanged(
            YujinWeaponType previous,
            YujinWeaponType current)
        {
            eventCount++;
            transitions.Add(
                $"{previous}->{current}");
        }

        mechanic.WeaponChanged +=
            OnChanged;

        try
        {
            bool first =
                mechanic.TrySwitchWeapon(
                    YujinWeaponType.Jeokseol);

            bool second =
                mechanic.TrySwitchWeapon(
                    YujinWeaponType.Nakil);

            bool third =
                mechanic.TrySwitchWeapon(
                    YujinWeaponType.Baeku);

            bool valid =
                first &&
                second &&
                third &&
                eventCount == 3 &&
                mechanic.CurrentWeapon ==
                YujinWeaponType.Baeku;

            return valid
                ? context.Pass(
                    "동일 턴 3회 연속 전환과 이벤트 3회",
                    string.Join(", ", transitions))
                : context.Fail(
                    "동일 턴 3회 연속 전환과 이벤트 3회",
                    $"Results={first}/{second}/{third}, " +
                    $"Events={eventCount}, " +
                    $"Current={mechanic.CurrentWeapon}",
                    string.Join(", ", transitions));
        }
        finally
        {
            mechanic.WeaponChanged -=
                OnChanged;
        }
    }

    private static CharacterVerificationCaseResult
        VerifyWeaponProfiles(
            CharacterVerificationContext context)
    {
        YujinWeaponProfile baeku =
            YujinWeapons.Get(
                YujinWeaponType.Baeku);

        YujinWeaponProfile jeokseol =
            YujinWeapons.Get(
                YujinWeaponType.Jeokseol);

        YujinWeaponProfile nakil =
            YujinWeapons.Get(
                YujinWeaponType.Nakil);

        bool valid =
            baeku.CoinCount == 3 &&
            Math.Abs(
                baeku.FrontChance - 0.40f) <
            0.0001f &&
            baeku.CriticalValue == 12 &&
            baeku.BaseMarkAmount == 4 &&

            jeokseol.CoinCount == 2 &&
            Math.Abs(
                jeokseol.FrontChance - 0.30f) <
            0.0001f &&
            jeokseol.CriticalValue == 15 &&
            jeokseol.BaseMarkAmount == 6 &&

            nakil.CoinCount == 1 &&
            Math.Abs(
                nakil.FrontChance - 0.25f) <
            0.0001f &&
            nakil.CriticalValue == 19 &&
            nakil.BaseMarkAmount == 8;

        return valid
            ? context.Pass(
                "백우 3/40%/12/4, 적설 2/30%/15/6, 낙일 1/25%/19/8",
                "무기 프로필 일치")
            : context.Fail(
                "백우 3/40%/12/4, 적설 2/30%/15/6, 낙일 1/25%/19/8",
                $"Baeku={Describe(baeku)}, " +
                $"Jeokseol={Describe(jeokseol)}, " +
                $"Nakil={Describe(nakil)}");
    }

    private static CharacterVerificationCaseResult
        VerifySenseTurnStart(
            CharacterVerificationContext context)
    {
        YujinMechanic mechanic =
            (context.Character as Yujin)
                ?.YujinMechanic;

        if (mechanic == null)
        {
            return context.Fail(
                "TurnStart 살수의 감 획득",
                "Mechanic 없음");
        }

        CharacterVerificationReflection.TrySetField(
            mechanic,
            "sense",
            0);

        context.BattleContext
            ?._battleEvent
            ?.RaiseTurnStart(1);

        int afterFirst =
            mechanic.Sense;

        context.BattleContext
            ?._battleEvent
            ?.RaiseTurnStart(2);

        int afterSecond =
            mechanic.Sense;

        bool valid =
            afterFirst == 1 &&
            afterSecond == 2;

        return valid
            ? context.Pass(
                "TurnStart마다 감 +1",
                $"0→{afterFirst}→{afterSecond}")
            : context.Fail(
                "TurnStart마다 감 +1",
                $"0→{afterFirst}→{afterSecond}");
    }

    private static CharacterVerificationCaseResult
        VerifySenseReroll(
            CharacterVerificationContext context)
    {
        Yujin yujin =
            context.Character as Yujin;

        YujinMechanic mechanic =
            yujin?.YujinMechanic;

        Skill duelSkill =
            yujin?.RuntimeSkills?
                .FirstOrDefault(
                    skill =>
                        skill?.Definition != null &&
                        (skill.Definition.SkillId ==
                         YujinSkillIds.Inscription ||
                         skill.Definition.SkillId ==
                         YujinSkillIds.Pursuit));

        if (mechanic == null ||
            duelSkill == null)
        {
            return context.Fail(
                "살수의 감 재굴림 실행 조건",
                mechanic == null
                    ? "Mechanic 없음"
                    : "각인·추격 RuntimeSkill 없음");
        }

        if (!CharacterVerificationReflection.TrySetField(
                mechanic,
                "sense",
                1))
        {
            return context.Fail(
                "검증 Fixture에서 감 1 Seed",
                "sense 필드 접근 실패");
        }

        mechanic.AutoUseSense = true;

        ActionSlot mySlot =
            new ActionSlot
            {
                ActionId = 101,
                Owner = yujin,
                Part =
                    yujin.BodyParts?
                        .FirstOrDefault(),
                Skill = duelSkill,
                Speed = 5,
                UseCharacterRerollResource = true
            };

        BattleAction myAction =
            new BattleAction
            {
                Slot = mySlot,
                ClashPower = 5,
                LastRollResult =
                    new RollResult
                    {
                        FinalPower = 5,
                        ClashPower = 5,
                        IsCritical = false
                    }
            };

        BattleAction opponent =
            new BattleAction
            {
                Slot =
                    new ActionSlot
                    {
                        ActionId = 202,
                        Owner = yujin,
                        Skill = duelSkill,
                        Speed = 6
                    },
                ClashPower = 10,
                LastRollResult =
                    new RollResult
                    {
                        FinalPower = 10,
                        ClashPower = 10,
                        IsCritical = false
                    }
            };

        ExchangeRerollContext reroll =
            new ExchangeRerollContext(
                myAction,
                opponent,
                0,
                0);

        bool requested =
            mechanic.TryRequestExchangeReroll(
                reroll);

        int remain =
            mechanic.Sense;

        bool secondRequest =
            mechanic.TryRequestExchangeReroll(
                reroll);

        bool valid =
            requested &&
            remain == 0 &&
            !secondRequest;

        return valid
            ? context.Pass(
                "패배·비크리·자동 ON에서 감 1 소모 후 1회 재굴림",
                $"Requested={requested}, Sense={remain}, " +
                $"Second={secondRequest}")
            : context.Fail(
                "패배·비크리·자동 ON에서 감 1 소모 후 1회 재굴림",
                $"Requested={requested}, Sense={remain}, " +
                $"Second={secondRequest}");
    }

    private static CharacterVerificationCaseResult
        VerifyMarkIgnition(
            CharacterVerificationContext context)
    {
        Yujin yujin =
            context.Character as Yujin;

        YujinMechanic mechanic =
            yujin?.YujinMechanic;

        BodyPart targetPart =
            yujin?.BodyParts?
                .FirstOrDefault(
                    part =>
                        part != null &&
                        !part.IsBroken);

        if (mechanic == null ||
            targetPart == null)
        {
            return context.Fail(
                "표식 발화 실행 조건",
                mechanic == null
                    ? "Mechanic 없음"
                    : "대상 부위 없음");
        }

        if (mechanic.CurrentWeapon !=
            YujinWeaponType.Nakil)
        {
            mechanic.TrySwitchWeapon(
                YujinWeaponType.Nakil);
        }

        bool firstInvoke =
            CharacterVerificationReflection.TryInvoke(
                mechanic,
                "AddMark",
                new object[]
                {
                    targetPart,
                    43,
                    null
                },
                out _);

        int beforeIgnition =
            mechanic.GetMark(
                targetPart);

        bool secondInvoke =
            CharacterVerificationReflection.TryInvoke(
                mechanic,
                "AddMark",
                new object[]
                {
                    targetPart,
                    1,
                    null
                },
                out _);

        int afterIgnition =
            mechanic.GetMark(
                targetPart);

        bool valid =
            firstInvoke &&
            secondInvoke &&
            beforeIgnition == 43 &&
            afterIgnition == 0 &&
            targetPart.IsWeakened;

        return valid
            ? context.Pass(
                "표식 43→44 발화, 표식 0, 낙일 약화",
                $"Mark={beforeIgnition}→{afterIgnition}, " +
                $"State={targetPart.State}")
            : context.Fail(
                "표식 43→44 발화, 표식 0, 낙일 약화",
                $"Invoke={firstInvoke}/{secondInvoke}, " +
                $"Mark={beforeIgnition}→{afterIgnition}, " +
                $"State={targetPart.State}");
    }

    private static string Describe(
        YujinWeaponProfile profile)
    {
        return
            $"{profile.Type} " +
            $"{profile.CoinCount}/" +
            $"{profile.FrontChance:0.00}/" +
            $"{profile.CriticalValue}/" +
            $"{profile.BaseMarkAmount}";
    }
}