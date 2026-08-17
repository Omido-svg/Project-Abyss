using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

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

    public const string DiceRuntimeResolution =
        "olaf.data.dice_runtime_resolution";

    public const string DiceVisualSides =
        "olaf.presentation.dice_visual_sides";

    public const string StandardExplosionLimit =
        "olaf.duel.standard_explosion_once_per_action";

    public const string OneSidedUniqueEffects =
        "olaf.duel.one_sided_unique_effects_disabled";

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
            DiceRuntimeResolution,
            "공용 BasePlusRoll 실제 굴림",
            CharacterVerificationCategory.Boundary,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "공용 CombatRollResolver가 BasePlusRoll에서는 BasePower+d8을 만들고 AbsoluteRange에서는 기존 최종 범위를 유지하는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            DiceVisualSides,
            "D6·D8 주사위 면수 연출",
            CharacterVerificationCategory.Presentation,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "1~6은 6각형·눈 표시, 1~8은 8각형·숫자 표시로 해석되고 실제 다각형 메시가 해당 면수로 생성되는지 검사합니다.");

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
            StandardExplosionLimit,
            "표준 출혈 폭발 행동당 1회",
            CharacterVerificationCategory.Duel,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "같은 표준 BattleAction에서는 출혈 폭발이 한 번만 발생하고 ActionEnd 이후 다시 허용되는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            OneSidedUniqueEffects,
            "표준·난도질 일방 고유효과 차단",
            CharacterVerificationCategory.Duel,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "일방 공격에서는 표준과 난도질의 결투 전용 출혈·광기 효과가 발동하지 않는지 검사합니다.");

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

            DiceRuntimeResolution =>
                VerifyDiceRuntimeResolution(context),

            DiceVisualSides =>
                VerifyDiceVisualSides(context),

            MechanicRegistration =>
                VerifyMechanics(context),

            MadnessBoundary =>
                VerifyMadnessBoundary(context),

            MadnessRollModifier =>
                VerifyMadnessRollModifier(context),

            StandardExplosionLimit =>
                VerifyStandardExplosionLimit(context),

            OneSidedUniqueEffects =>
                VerifyOneSidedUniqueEffects(context),

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
        VerifyDiceRuntimeResolution(
            CharacterVerificationContext context)
    {
        Olaf olaf =
            context.Character as Olaf;

        SkillDefinition definition =
            CharacterVerificationScenarioTools.FindDefinition(
                context.Bundle,
                OlafSkillIds.EnduringSlash);

        Skill skill =
            CharacterVerificationScenarioTools.FindRuntimeSkill(
                olaf,
                definition);

        if (skill == null)
        {
            return context.Fail(
                "공용 Dice Resolver 검증용 올라프 스킬 존재",
                "버티며 베기 RuntimeSkill 없음");
        }

        SkillRollData lowProbe =
            new SkillRollData
            {
                Index = 0,
                Type = CombatRollType.Attack,
                RngSource = RollRngSource.Dice,
                DiceMode = DicePowerMode.BasePlusRoll,
                MinPower = 1,
                MaxPower = 1
            };

        SkillRollData highProbe =
            new SkillRollData
            {
                Index = 0,
                Type = CombatRollType.Attack,
                RngSource = RollRngSource.Dice,
                DiceMode = DicePowerMode.BasePlusRoll,
                MinPower = 8,
                MaxPower = 8
            };

        SkillRollData legacyProbe =
            new SkillRollData
            {
                Index = 0,
                Type = CombatRollType.Attack,
                RngSource = RollRngSource.Dice,
                DiceMode = DicePowerMode.AbsoluteRange,
                MinPower = 6,
                MaxPower = 6
            };

        RollResult low =
            CombatRollResolver.Roll(
                skill,
                lowProbe,
                SkillResolverType.Dice);

        RollResult high =
            CombatRollResolver.Roll(
                skill,
                highProbe,
                SkillResolverType.Dice);

        RollResult legacy =
            CombatRollResolver.Roll(
                skill,
                legacyProbe,
                SkillResolverType.Dice);

        int basePower =
            skill.BasePower;

        bool lowValid =
            low != null &&
            low.BasePower == basePower &&
            low.RawValue == 1 &&
            low.ModifiedValue == 1 &&
            low.FinalPower == basePower + 1 &&
            low.DiceMin == 1 &&
            low.DiceMax == 1 &&
            low.DiceValues?.Count == 1 &&
            low.DiceValues[0] == 1;

        bool highValid =
            high != null &&
            high.BasePower == basePower &&
            high.RawValue == 8 &&
            high.ModifiedValue == 8 &&
            high.FinalPower == basePower + 8 &&
            high.DiceMin == 8 &&
            high.DiceMax == 8 &&
            high.DiceValues?.Count == 1 &&
            high.DiceValues[0] == 8;

        bool legacyValid =
            legacy != null &&
            legacy.BasePower == 0 &&
            legacy.RawValue == 6 &&
            legacy.ModifiedValue == 6 &&
            legacy.FinalPower == 6;

        string detail =
            $"Base={basePower}, " +
            $"BasePlusLow={DescribeRoll(low)}, " +
            $"BasePlusHigh={DescribeRoll(high)}, " +
            $"Absolute={DescribeRoll(legacy)}";

        return lowValid && highValid && legacyValid
            ? context.Pass(
                "BasePlusRoll=BasePower+RNG, AbsoluteRange=기존 최종 범위",
                detail)
            : context.Fail(
                "BasePlusRoll=BasePower+RNG, AbsoluteRange=기존 최종 범위",
                detail);
    }

    private static CharacterVerificationCaseResult
        VerifyDiceVisualSides(
            CharacterVerificationContext context)
    {
        const BindingFlags StaticPrivate =
            BindingFlags.Static |
            BindingFlags.NonPublic;

        const BindingFlags InstancePrivate =
            BindingFlags.Instance |
            BindingFlags.NonPublic;

        MethodInfo sideMethod =
            typeof(BattleResolverRollVisualUI).GetMethod(
                "GetDiceVisualSideCount",
                StaticPrivate,
                null,
                new[] { typeof(RollResult) },
                null);

        MethodInfo pipMethod =
            typeof(BattleResolverRollVisualUI).GetMethod(
                "UsesClassicSixSidedPips",
                StaticPrivate,
                null,
                new[] { typeof(RollResult) },
                null);

        MethodInfo populateMethod =
            typeof(DicePolygonGraphic).GetMethod(
                "OnPopulateMesh",
                InstancePrivate,
                null,
                new[] { typeof(VertexHelper) },
                null);

        if (sideMethod == null ||
            pipMethod == null ||
            populateMethod == null)
        {
            return context.Fail(
                "D6·D8 공용 연출 메서드 존재",
                $"Side={sideMethod != null}, Pips={pipMethod != null}, " +
                $"Mesh={populateMethod != null}");
        }

        RollResult d6 =
            new RollResult
            {
                ResolverType = SkillResolverType.Dice,
                DiceMin = 1,
                DiceMax = 6
            };

        RollResult d8 =
            new RollResult
            {
                ResolverType = SkillResolverType.Dice,
                DiceMin = 1,
                DiceMax = 8
            };

        int d6Sides =
            (int)sideMethod.Invoke(
                null,
                new object[] { d6 });

        int d8Sides =
            (int)sideMethod.Invoke(
                null,
                new object[] { d8 });

        bool d6Pips =
            (bool)pipMethod.Invoke(
                null,
                new object[] { d6 });

        bool d8Pips =
            (bool)pipMethod.Invoke(
                null,
                new object[] { d8 });

        GameObject root = null;
        VertexHelper d6Mesh = null;
        VertexHelper d8Mesh = null;

        int d6Vertices = 0;
        int d6Indices = 0;
        int d8Vertices = 0;
        int d8Indices = 0;

        try
        {
            root =
                new GameObject(
                    "[Verification] Dice Polygon",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(DicePolygonGraphic));

            root.hideFlags =
                HideFlags.HideAndDontSave;

            RectTransform rect =
                root.GetComponent<RectTransform>();

            rect.sizeDelta =
                new Vector2(100f, 100f);

            DicePolygonGraphic graphic =
                root.GetComponent<DicePolygonGraphic>();

            d6Mesh =
                new VertexHelper();

            graphic.SideCount = 6;
            populateMethod.Invoke(
                graphic,
                new object[] { d6Mesh });

            d6Vertices =
                d6Mesh.currentVertCount;

            d6Indices =
                d6Mesh.currentIndexCount;

            d8Mesh =
                new VertexHelper();

            graphic.SideCount = 8;
            populateMethod.Invoke(
                graphic,
                new object[] { d8Mesh });

            d8Vertices =
                d8Mesh.currentVertCount;

            d8Indices =
                d8Mesh.currentIndexCount;
        }
        finally
        {
            d6Mesh?.Dispose();
            d8Mesh?.Dispose();

            if (root != null)
            {
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(root);
#else
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(root);
                else
                    UnityEngine.Object.DestroyImmediate(root);
#endif
            }
        }

        bool valid =
            d6Sides == 6 &&
            d8Sides == 8 &&
            d6Pips &&
            !d8Pips &&
            d6Vertices == 7 &&
            d6Indices == 18 &&
            d8Vertices == 9 &&
            d8Indices == 24;

        string detail =
            $"D6={d6Sides}면/Pips={d6Pips}/Mesh={d6Vertices}v,{d6Indices}i, " +
            $"D8={d8Sides}면/Pips={d8Pips}/Mesh={d8Vertices}v,{d8Indices}i";

        return valid
            ? context.Pass(
                "1~6=D6 눈 연출, 1~8=D8 숫자 연출과 실제 다각형 메시",
                detail)
            : context.Fail(
                "1~6=D6 눈 연출, 1~8=D8 숫자 연출과 실제 다각형 메시",
                detail);
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
        VerifyStandardExplosionLimit(
            CharacterVerificationContext context)
    {
        Olaf olaf =
            context.Character as Olaf;

        Character target =
            context.OpponentCharacter;

        OlafMadnessMechanic mechanic =
            olaf?.MadnessMechanic;

        Skill standard =
            CharacterVerificationScenarioTools.FindRuntimeSkill(
                olaf,
                CharacterVerificationScenarioTools.FindDefinition(
                    context.Bundle,
                    OlafSkillIds.Standard));

        Skill opponentSkill =
            target?.RuntimeSkills?
                .FirstOrDefault(
                    item => item?.ActionType == ActionType.Duel);

        BodyPart ownerPart =
            CharacterVerificationScenarioTools.GetUsablePart(olaf);

        BodyPart targetPart =
            CharacterVerificationScenarioTools.GetUsablePart(target);

        if (mechanic == null ||
            standard == null ||
            opponentSkill == null ||
            targetPart == null)
        {
            return context.Fail(
                "표준 폭발 검증 Fixture 존재",
                $"Mechanic={mechanic != null}, Standard={standard != null}, " +
                $"OpponentDuel={opponentSkill != null}, Part={targetPart != null}");
        }

        CharacterVerificationScenarioTools.ResetCombatState(target);
        mechanic.SetMadnessForDebug(0);

        // 출혈 10 이상은 폭발 피해가 크므로 동일 행동의 두 번째 교환까지
        // 살아 있는 정상 부위에서 검증할 수 있도록 HP를 충분히 확장한다.
        if (target.RuntimeStatus != null)
            target.RuntimeStatus.currentHP = 1000;

        targetPart.State =
            BodyPartState.Normal;

        targetPart.PartHP = 1000f;

        BattleAction mine =
            CharacterVerificationScenarioTools.CreateAction(
                olaf,
                ownerPart,
                standard,
                target,
                targetPart,
                940001);

        BattleAction theirs =
            CharacterVerificationScenarioTools.CreateAction(
                target,
                targetPart,
                opponentSkill,
                olaf,
                ownerPart,
                940002);

        mine.CurrentRollType =
            CombatRollType.Attack;

        DamageContext hit =
            new DamageContext(
                DamageRequest.SkillPart(
                    mine,
                    1,
                    false))
            {
                AppliedDamage = 1,
                AppliedPartDamage = 1,
                WasApplied = true
            };

        ClashExchangeResult exchange =
            new ClashExchangeResult
            {
                FirstAction = mine,
                SecondAction = theirs,
                WinnerAction = mine,
                LoserAction = theirs,
                DamageContext = hit,
                IsDuelExchange = true
            };

        target.AddPartStatus(
            targetPart,
            new Bleeding(Bleeding.ExplosionThreshold),
            olaf);

        int startHp =
            target.CurrentHP;

        int startPartHp =
            Mathf.CeilToInt(targetPart.PartHP);

        context.BattleContext
            ?._battleEvent
            ?.RaiseExchangeResolved(exchange);

        int afterFirstHp =
            target.CurrentHP;

        int afterFirstPartHp =
            Mathf.CeilToInt(targetPart.PartHP);

        Bleeding afterFirst =
            target.GetPartStatus<Bleeding>(targetPart);

        target.AddPartStatus(
            targetPart,
            new Bleeding(Bleeding.ExplosionThreshold),
            olaf);

        context.BattleContext
            ?._battleEvent
            ?.RaiseExchangeResolved(exchange);

        int afterSecondHp =
            target.CurrentHP;

        int afterSecondPartHp =
            Mathf.CeilToInt(targetPart.PartHP);

        Bleeding afterSecond =
            target.GetPartStatus<Bleeding>(targetPart);

        int retainedStack =
            afterSecond?.Stack ?? 0;

        if (afterSecond != null)
        {
            target.RemovePartStatus(
                targetPart,
                afterSecond,
                StatusEffectRemoveReason.Manual);
        }

        context.BattleContext
            ?._battleEvent
            ?.RaiseActionEnd(mine);

        target.AddPartStatus(
            targetPart,
            new Bleeding(Bleeding.ExplosionThreshold),
            olaf);

        context.BattleContext
            ?._battleEvent
            ?.RaiseExchangeResolved(exchange);

        int afterResetHp =
            target.CurrentHP;

        int afterResetPartHp =
            Mathf.CeilToInt(targetPart.PartHP);

        Bleeding afterReset =
            target.GetPartStatus<Bleeding>(targetPart);

        bool firstExploded =
            afterFirst == null &&
            afterFirstHp < startHp &&
            afterFirstPartHp < startPartHp;

        bool secondSuppressed =
            retainedStack >=
                Bleeding.ExplosionThreshold &&
            afterSecondHp == afterFirstHp &&
            afterSecondPartHp == afterFirstPartHp;

        bool resetWorked =
            afterReset == null &&
            afterResetHp < afterSecondHp &&
            afterResetPartHp < afterSecondPartHp;

        string detail =
            $"HP={startHp}→{afterFirstHp}→{afterSecondHp}→{afterResetHp}, " +
            $"Part={startPartHp}→{afterFirstPartHp}→{afterSecondPartHp}→{afterResetPartHp}, " +
            $"SecondBleed={retainedStack}";

        return firstExploded && secondSuppressed && resetWorked
            ? context.Pass(
                "첫 교환 폭발·동일 행동 추가 폭발 차단·ActionEnd 후 재허용",
                detail)
            : context.Fail(
                "첫 교환 폭발·동일 행동 추가 폭발 차단·ActionEnd 후 재허용",
                detail,
                $"First={firstExploded}, SecondBlocked={secondSuppressed}, Reset={resetWorked}");
    }

    private static CharacterVerificationCaseResult
        VerifyOneSidedUniqueEffects(
            CharacterVerificationContext context)
    {
        Olaf olaf =
            context.Character as Olaf;

        Character target =
            context.OpponentCharacter;

        OlafMadnessMechanic mechanic =
            olaf?.MadnessMechanic;

        BodyPart ownerPart =
            CharacterVerificationScenarioTools.GetUsablePart(olaf);

        BodyPart targetPart =
            CharacterVerificationScenarioTools.GetUsablePart(target);

        Skill opponentSkill =
            target?.RuntimeSkills?
                .FirstOrDefault();

        if (mechanic == null ||
            targetPart == null ||
            opponentSkill == null)
        {
            return context.Fail(
                "일방 고유효과 검증 Fixture 존재",
                $"Mechanic={mechanic != null}, Part={targetPart != null}, " +
                $"OpponentSkill={opponentSkill != null}");
        }

        string[] ids =
        {
            OlafSkillIds.Standard,
            OlafSkillIds.Rend
        };

        List<string> details =
            new List<string>();

        bool allValid = true;

        for (int index = 0;
             index < ids.Length;
             index++)
        {
            string skillId =
                ids[index];

            Skill skill =
                CharacterVerificationScenarioTools.FindRuntimeSkill(
                    olaf,
                    CharacterVerificationScenarioTools.FindDefinition(
                        context.Bundle,
                        skillId));

            if (skill == null)
            {
                allValid = false;
                details.Add($"{skillId}=RuntimeSkill 없음");
                continue;
            }

            CharacterVerificationScenarioTools.ResetCombatState(target);
            mechanic.SetMadnessForDebug(0);

            BattleAction mine =
                CharacterVerificationScenarioTools.CreateAction(
                    olaf,
                    ownerPart,
                    skill,
                    target,
                    targetPart,
                    950001 + index * 2);

            BattleAction theirs =
                CharacterVerificationScenarioTools.CreateAction(
                    target,
                    targetPart,
                    opponentSkill,
                    olaf,
                    ownerPart,
                    950002 + index * 2);

            mine.CurrentRollType =
                CombatRollType.Attack;

            DamageContext hit =
                new DamageContext(
                    DamageRequest.SkillPart(
                        mine,
                        1,
                        false))
                {
                    AppliedDamage = 1,
                    AppliedPartDamage = 1,
                    WasApplied = true
                };

            ClashExchangeResult oneSided =
                new ClashExchangeResult
                {
                    FirstAction = mine,
                    SecondAction = theirs,
                    WinnerAction = mine,
                    LoserAction = theirs,
                    DamageContext = hit,
                    IsDuelExchange = true,
                    IsOneSided = true
                };

            context.BattleContext
                ?._battleEvent
                ?.RaiseExchangeResolved(oneSided);

            int bleeding =
                target.GetPartStatus<Bleeding>(targetPart)?.Stack ?? 0;

            int madness =
                mechanic.CurrentMadness;

            bool valid =
                bleeding == 0 &&
                madness == 0;

            allValid &= valid;
            details.Add(
                $"{skillId}: Bleeding={bleeding}, Madness={madness}");
        }

        string detail =
            string.Join(" / ", details);

        return allValid
            ? context.Pass(
                "표준·난도질 일방 공격에서 고유 출혈·광기 0",
                detail)
            : context.Fail(
                "표준·난도질 일방 공격에서 고유 출혈·광기 0",
                detail);
    }

    private static string DescribeRoll(
        RollResult result)
    {
        if (result == null)
            return "NULL";

        return
            $"Base={result.BasePower}, Raw={result.RawValue}, " +
            $"Final={result.FinalPower}, Dice={result.DiceMin}~{result.DiceMax}";
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