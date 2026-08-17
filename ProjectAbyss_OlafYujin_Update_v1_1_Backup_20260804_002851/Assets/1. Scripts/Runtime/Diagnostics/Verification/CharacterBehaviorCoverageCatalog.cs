using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CharacterBehaviorCoverageCatalog
{
    private static readonly HashSet<Type> SupportedMechanics =
        new HashSet<Type>
        {
            typeof(OlafMadnessMechanic),
            typeof(OlafImmortalFuryMechanic),
            typeof(OlafBloodyAxeMechanic),
            typeof(YujinMechanic),
            typeof(EliteEnemyMechanic),
            typeof(EnemyPostureMechanic),
            typeof(NormalEnemyBloodScentMechanic)
        };

    public static bool IsSupported(
        Type mechanicType)
    {
        if (mechanicType == null)
            return false;

        return SupportedMechanics.Any(
            supported => supported.IsAssignableFrom(mechanicType));
    }

    public static IReadOnlyList<string> GetRequiredMechanicNames(
        CharacterAuthoringBundle bundle)
    {
        List<string> required = new List<string>();

        switch (bundle?.Kind)
        {
            case CharacterAuthoringKind.Olaf:
                required.Add(nameof(OlafMadnessMechanic));
                required.Add(nameof(OlafImmortalFuryMechanic));
                break;

            case CharacterAuthoringKind.Yujin:
                required.Add(nameof(YujinMechanic));
                break;

            case CharacterAuthoringKind.EliteEnemy:
                required.Add(nameof(EliteEnemyMechanic));
                if (bundle.UseElitePostureRotation)
                    required.Add(nameof(EnemyPostureMechanic));
                break;

            case CharacterAuthoringKind.NormalEnemy:
                required.Add(nameof(NormalEnemyBloodScentMechanic));
                break;
        }

        return required;
    }

    public static CharacterVerificationCaseResult Verify(
        CharacterVerificationContext context)
    {
        Character owner = context?.Character;
        Character target = context?.OpponentCharacter;

        if (owner == null || target == null)
        {
            return context?.Fail(
                "Owner와 Target Fixture 존재",
                "NULL");
        }

        List<string> failures = new List<string>();
        List<string> observations = new List<string>();
        IReadOnlyList<CombatMechanic> mechanics = owner.Mechanics;

        IReadOnlyList<string> required =
            GetRequiredMechanicNames(context.Bundle);

        foreach (string name in required)
        {
            bool found = mechanics.Any(
                item => item != null &&
                        item.GetType().Name == name);

            if (!found)
                failures.Add($"필수 메커닉 누락: {name}");
        }

        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic == null)
                continue;

            if (!IsSupported(mechanic.GetType()))
            {
                failures.Add(
                    $"행동 시나리오가 등록되지 않은 메커닉: {mechanic.GetType().FullName}");
                continue;
            }

            if (!mechanic.IsInitialized ||
                !mechanic.IsRegistered ||
                mechanic.Owner != owner)
            {
                failures.Add(
                    $"{mechanic.GetType().Name}: 초기화/등록/Owner 연결 실패");
                continue;
            }

            try
            {
                VerifyOne(
                    context,
                    mechanic,
                    failures,
                    observations);
            }
            catch (Exception exception)
            {
                failures.Add(
                    $"{mechanic.GetType().Name}: {exception.GetType().Name} - {exception.Message}");
            }
        }

        try
        {
            VerifyNamedSkillContracts(
                context,
                failures,
                observations);
        }
        catch (Exception exception)
        {
            failures.Add(
                $"고유 스킬 계약: {exception.GetType().Name} - {exception.Message}");
        }

        if (failures.Count == 0)
        {
            return context.Pass(
                "모든 필수 패시브 메커닉의 강제 이벤트 시나리오 통과",
                $"PASS / Mechanics={mechanics.Count}",
                string.Join("\n", observations));
        }

        return context.Fail(
            "모든 필수 패시브 메커닉의 강제 이벤트 시나리오 통과",
            $"FAIL {failures.Count}",
            string.Join("\n", failures) +
            "\n\nObservations\n" +
            string.Join("\n", observations));
    }

    private static void VerifyOne(
        CharacterVerificationContext context,
        CombatMechanic mechanic,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        switch (mechanic)
        {
            case OlafMadnessMechanic madness:
                VerifyOlafMadness(context, madness, failures, observations);
                break;

            case OlafImmortalFuryMechanic immortal:
                VerifyOlafImmortal(context, immortal, failures, observations);
                break;

            case OlafBloodyAxeMechanic bloodyAxe:
                VerifyBloodyAxe(context, bloodyAxe, failures, observations);
                break;

            case YujinMechanic yujin:
                VerifyYujin(context, yujin, failures, observations);
                break;

            case EliteEnemyMechanic elite:
                VerifyElite(context, elite, failures, observations);
                break;

            case EnemyPostureMechanic posture:
                VerifyPosture(context, posture, failures, observations);
                break;

            case NormalEnemyBloodScentMechanic bloodScent:
                VerifyBloodScent(context, bloodScent, failures, observations);
                break;
        }
    }

    private static void VerifyOlafMadness(
        CharacterVerificationContext context,
        OlafMadnessMechanic mechanic,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        Character owner = context.Character;
        BattleAction probe = CharacterVerificationScenarioTools.CreateAction(
            owner,
            CharacterVerificationScenarioTools.GetUsablePart(owner),
            owner.RuntimeSkills.FirstOrDefault(),
            context.OpponentCharacter,
            CharacterVerificationScenarioTools.GetUsablePart(context.OpponentCharacter));

        DamageContext damage = new DamageContext(
            DamageRequest.Direct(
                context.OpponentCharacter,
                owner,
                10,
                probe));

        int[] values = { 0, 5, 10 };
        foreach (int value in values)
        {
            mechanic.SetMadnessForDebug(value);
            int expectedBonus = value / 5;
            int roll = mechanic.ModifyRoll(probe, 10);
            int taken = mechanic.ModifyDamageTaken(damage, 10);

            if (roll != 10 + expectedBonus ||
                taken != 10 + expectedBonus)
            {
                failures.Add(
                    $"OlafMadness {value}: Roll={roll}, DamageTaken={taken}, " +
                    $"Expected={10 + expectedBonus}");
            }
        }

        mechanic.SetMadnessForDebug(0);
        observations.Add("OlafMadness: 0/5/10 판정·피해 보정 PASS");

        VerifyOlafDuelExchange(
            context,
            mechanic,
            OlafSkillIds.Standard,
            shouldRequireWin: true,
            failures,
            observations);

        VerifyOlafDuelExchange(
            context,
            mechanic,
            OlafSkillIds.Rend,
            shouldRequireWin: false,
            failures,
            observations);

        int beforeBreak = mechanic.CurrentMadness;
        BodyPart targetPart =
            CharacterVerificationScenarioTools.GetUsablePart(
                context.OpponentCharacter);
        context.BattleContext._battleEvent.RaiseBodyPartDestroyed(
            context.OpponentCharacter,
            targetPart);

        if (mechanic.CurrentMadness !=
            Mathf.Min(OlafMadnessMechanic.MaxMadnessValue, beforeBreak + 2))
        {
            failures.Add("Olaf 부위 파괴 이벤트가 광기 +2를 주지 않았습니다.");
        }
        else
        {
            observations.Add("Olaf 부위 파괴 광기 +2 PASS");
        }
    }

    private static void VerifyOlafDuelExchange(
        CharacterVerificationContext context,
        OlafMadnessMechanic mechanic,
        string skillId,
        bool shouldRequireWin,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        Character owner = context.Character;
        Character target = context.OpponentCharacter;
        SkillDefinition definition =
            CharacterVerificationScenarioTools.FindDefinition(
                context.Bundle,
                skillId);
        Skill skill =
            CharacterVerificationScenarioTools.FindRuntimeSkill(
                owner,
                definition);
        Skill opponentSkill = target.RuntimeSkills
            .FirstOrDefault(item => item?.ActionType == ActionType.Duel) ??
            target.RuntimeSkills.FirstOrDefault();

        if (skill == null || opponentSkill == null)
        {
            failures.Add($"Olaf Duel 시나리오 스킬 누락: {skillId}");
            return;
        }

        BodyPart ownerPart = CharacterVerificationScenarioTools.GetUsablePart(owner);
        BodyPart targetPart = CharacterVerificationScenarioTools.GetUsablePart(target);
        CharacterVerificationScenarioTools.ResetCombatState(target);
        mechanic.SetMadnessForDebug(0);

        BattleAction mine = CharacterVerificationScenarioTools.CreateAction(
            owner, ownerPart, skill, target, targetPart, 910001);
        BattleAction theirs = CharacterVerificationScenarioTools.CreateAction(
            target, targetPart, opponentSkill, owner, ownerPart, 910002);
        mine.CurrentRollType = CombatRollType.Attack;

        DamageContext damage = new DamageContext(
            DamageRequest.SkillPart(mine, 1, false))
        {
            AppliedDamage = 1,
            AppliedPartDamage = 1,
            WasApplied = true
        };

        ClashExchangeResult exchange = new ClashExchangeResult
        {
            FirstAction = mine,
            SecondAction = theirs,
            WinnerAction = shouldRequireWin ? mine : theirs,
            LoserAction = shouldRequireWin ? theirs : mine,
            DamageContext = shouldRequireWin ? damage : null,
            IsDuelExchange = true
        };

        context.BattleContext._battleEvent.RaiseExchangeResolved(exchange);

        int bleeding = target.GetPartStatus<Bleeding>(targetPart)?.Stack ?? 0;
        bool valid = skillId == OlafSkillIds.Standard
            ? bleeding >= 2 && mechanic.CurrentMadness == 1
            : bleeding >= 1 && mechanic.CurrentMadness == 1;

        if (!valid)
        {
            failures.Add(
                $"Olaf {skillId} 교환 효과 불일치: Bleeding={bleeding}, " +
                $"Madness={mechanic.CurrentMadness}");
        }
        else
        {
            observations.Add(
                $"Olaf {skillId}: Bleeding={bleeding}, Madness=1 PASS");
        }
    }

    private static void VerifyOlafImmortal(
        CharacterVerificationContext context,
        OlafImmortalFuryMechanic mechanic,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        Character owner = context.Character;
        BodyPart part = CharacterVerificationScenarioTools.GetUsablePart(owner);
        BattleAction action = CharacterVerificationScenarioTools.CreateAction(
            owner,
            part,
            owner.RuntimeSkills.FirstOrDefault(),
            context.OpponentCharacter,
            CharacterVerificationScenarioTools.GetUsablePart(context.OpponentCharacter));

        mechanic.Activate(action);
        DamageContext damage = new DamageContext(
            DamageRequest.Direct(
                context.OpponentCharacter,
                owner,
                999,
                action));
        damage.TargetPart = part;

        int limited = mechanic.ModifyDamageTaken(damage, 999);
        bool activeValid =
            mechanic.IsActive &&
            !mechanic.CanOwnerDie() &&
            !mechanic.CanBreakOwnerPart(part, action) &&
            limited <= Mathf.Max(0, owner.CurrentHP - 1);

        context.BattleContext._battleEvent.RaiseTurnEnd(1);

        if (!activeValid || mechanic.IsActive)
        {
            failures.Add(
                $"Olaf 배수진 불일치: ActiveValid={activeValid}, " +
                $"Released={!mechanic.IsActive}, Limited={limited}");
        }
        else
        {
            observations.Add("Olaf 배수진 피해 제한·파괴/사망 차단·턴 종료 해제 PASS");
        }
    }

    private static void VerifyBloodyAxe(
        CharacterVerificationContext context,
        OlafBloodyAxeMechanic mechanic,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        Character owner = context.Character;
        Character target = context.OpponentCharacter;
        Skill skill = owner.RuntimeSkills
            .FirstOrDefault(item => item?.ActionType == ActionType.NormalAttack);
        BodyPart ownerPart = CharacterVerificationScenarioTools.GetUsablePart(owner);
        BodyPart targetPart = CharacterVerificationScenarioTools.GetUsablePart(target);

        if (skill == null)
        {
            failures.Add("피 묻은 도끼 검증용 일반공격이 없습니다.");
            return;
        }

        CharacterVerificationScenarioTools.ResetCombatState(target);
        BattleAction action = CharacterVerificationScenarioTools.CreateAction(
            owner, ownerPart, skill, target, targetPart, 920001);
        action.FinalPower = 3;
        action.HasRolled = true;

        DamageContext damage = new DamageContext(
            DamageRequest.SkillPart(action, 3, false))
        {
            AppliedDamage = 3,
            AppliedPartDamage = 3,
            WasApplied = true
        };

        DamageEventResult eventResult = DamageEventResult.FromContext(damage);
        context.BattleContext._battleEvent.RaiseDamageEventResolved(eventResult);
        int first = targetPart == null
            ? target.GetStatus<Bleeding>()?.Stack ?? 0
            : target.GetPartStatus<Bleeding>(targetPart)?.Stack ?? 0;

        context.BattleContext._battleEvent.RaiseDamageEventResolved(eventResult);
        int second = targetPart == null
            ? target.GetStatus<Bleeding>()?.Stack ?? 0
            : target.GetPartStatus<Bleeding>(targetPart)?.Stack ?? 0;

        context.BattleContext._battleEvent.RaiseActionEnd(action);
        context.BattleContext._battleEvent.RaiseDamageEventResolved(eventResult);
        int third = targetPart == null
            ? target.GetStatus<Bleeding>()?.Stack ?? 0
            : target.GetPartStatus<Bleeding>(targetPart)?.Stack ?? 0;

        if (first <= 0 || second != first || third <= second)
        {
            failures.Add(
                $"피 묻은 도끼 once-per-action 불일치: {first}/{second}/{third}");
        }
        else
        {
            observations.Add("피 묻은 도끼 양수 일반공격·행동당 1회·ActionEnd 해제 PASS");
        }
    }

    private static void VerifyYujin(
        CharacterVerificationContext context,
        YujinMechanic mechanic,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        int beforeSense = mechanic.Sense;
        context.BattleContext._battleEvent.RaiseTurnStart(1);
        if (mechanic.Sense != beforeSense + 1)
            failures.Add("유진 TurnStart 살수의 감 +1 실패");

        YujinWeaponType[] weapons =
        {
            YujinWeaponType.Baeku,
            YujinWeaponType.Jeokseol,
            YujinWeaponType.Nakil
        };

        foreach (YujinWeaponType weapon in weapons)
        {
            if (mechanic.CurrentWeapon != weapon)
                mechanic.TrySwitchWeapon(weapon);

            int inspectionFront = mechanic.GetSkillPower(
                YujinSkillIds.Inspection, true);
            int inspectionBack = mechanic.GetSkillPower(
                YujinSkillIds.Inspection, false);
            int duelFront = mechanic.GetSkillPower(
                YujinSkillIds.Inscription, true);
            int duelBack = mechanic.GetSkillPower(
                YujinSkillIds.Inscription, false);

            int expectedInspectionFront = weapon switch
            {
                YujinWeaponType.Baeku => 22,
                YujinWeaponType.Jeokseol => 28,
                _ => 43
            };
            int expectedDuelFront = weapon switch
            {
                YujinWeaponType.Baeku => 23,
                YujinWeaponType.Jeokseol => 29,
                _ => 44
            };

            if (inspectionFront != expectedInspectionFront ||
                inspectionBack != 14 ||
                duelFront != expectedDuelFront ||
                duelBack != 15)
            {
                failures.Add(
                    $"유진 {weapon} 위력표 불일치: " +
                    $"I={inspectionFront}/{inspectionBack}, " +
                    $"D={duelFront}/{duelBack}");
            }
        }

        VerifyYujinMarkIgnitions(
            context,
            mechanic,
            failures,
            observations);

        VerifyYujinPursuit(
            context,
            mechanic,
            failures,
            observations);

        observations.Add("유진 3무기 앞/뒷면 위력표 및 TurnStart 감 PASS");
    }

    private static void VerifyYujinMarkIgnitions(
        CharacterVerificationContext context,
        YujinMechanic mechanic,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        Character owner = context.Character;
        Character target = context.OpponentCharacter;
        List<BodyPart> parts = target.BodyParts?
            .Where(item => item != null)
            .Take(3)
            .ToList() ?? new List<BodyPart>();

        if (parts.Count < 3)
        {
            failures.Add("유진 3무기 표식 발화 검증에 대상 부위 3개가 필요합니다.");
            return;
        }

        SkillDefinition definition =
            CharacterVerificationScenarioTools.FindDefinition(
                context.Bundle,
                YujinSkillIds.Inspection);
        Skill skill = CharacterVerificationScenarioTools.FindRuntimeSkill(
            owner,
            definition);
        if (skill == null)
        {
            failures.Add("유진 표식 발화용 검분 RuntimeSkill이 없습니다.");
            return;
        }

        YujinWeaponType[] weapons =
        {
            YujinWeaponType.Baeku,
            YujinWeaponType.Jeokseol,
            YujinWeaponType.Nakil
        };

        for (int index = 0; index < weapons.Length; index++)
        {
            YujinWeaponType weapon = weapons[index];
            BodyPart part = parts[index];
            part.Recover();
            target.RemoveAllPartStatuses(
                part,
                StatusEffectRemoveReason.Manual);

            if (mechanic.CurrentWeapon != weapon)
                mechanic.TrySwitchWeapon(weapon);

            int momentumBefore =
                context.BattleContext.ResolveMomentumManager()?.CurrentMomentum ?? 0;

            BattleAction action = CharacterVerificationScenarioTools.CreateAction(
                owner,
                CharacterVerificationScenarioTools.GetUsablePart(owner),
                skill,
                target,
                part,
                930000 + index);
            action.CurrentRollType = CombatRollType.Attack;
            action.LastRollResult = new RollResult
            {
                FinalPower = 10,
                IsCritical = true
            };
            action.HasRolled = true;

            DamageContext damage = new DamageContext(
                DamageRequest.SkillPart(action, 1, false))
            {
                AppliedDamage = 1,
                AppliedPartDamage = 1,
                WasApplied = true
            };

            int amount = Mathf.Max(1, mechanic.CurrentWeaponProfile.BaseMarkAmount);
            int repeats = Mathf.CeilToInt(
                YujinMechanic.MarkIgnitionThreshold / (float)amount);

            for (int i = 0; i < repeats; i++)
            {
                context.BattleContext._battleEvent.RaiseExchangeResolved(
                    new ClashExchangeResult
                    {
                        FirstAction = action,
                        SecondAction = CharacterVerificationScenarioTools.CreateAction(
                            target,
                            part,
                            target.RuntimeSkills.FirstOrDefault(),
                            owner,
                            action.OwnerPart,
                            939000 + i),
                        WinnerAction = action,
                        DamageContext = damage
                    });
            }

            bool valid = weapon switch
            {
                YujinWeaponType.Baeku =>
                    (context.BattleContext.ResolveMomentumManager()?.CurrentMomentum ?? 0) >=
                    momentumBefore + 10,
                YujinWeaponType.Jeokseol =>
                    target.GetPartStatus<SealedPartStatus>(part) != null,
                _ => part.IsWeakened || part.IsBroken
            };

            if (!valid)
                failures.Add($"유진 {weapon} 표식 44 발화 효과 실패");
            else
                observations.Add($"유진 {weapon} 표식 44 발화 PASS");
        }
    }

    private static void VerifyYujinPursuit(
        CharacterVerificationContext context,
        YujinMechanic mechanic,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        Character owner = context.Character;
        Character target = context.OpponentCharacter;
        SkillDefinition definition =
            CharacterVerificationScenarioTools.FindDefinition(
                context.Bundle,
                YujinSkillIds.Pursuit);
        Skill skill = CharacterVerificationScenarioTools.FindRuntimeSkill(
            owner,
            definition);

        if (skill == null)
        {
            failures.Add("유진 추격 RuntimeSkill이 없습니다.");
            return;
        }

        YujinWeaponType[] weapons =
        {
            YujinWeaponType.Baeku,
            YujinWeaponType.Jeokseol,
            YujinWeaponType.Nakil
        };

        int[] expectedHitCaps =
        {
            1,
            2,
            5
        };

        for (int index = 0;
             index < weapons.Length;
             index++)
        {
            YujinWeaponType weapon = weapons[index];
            int expectedHits = expectedHitCaps[index];

            if (mechanic.CurrentWeapon != weapon &&
                !mechanic.TrySwitchWeapon(weapon))
            {
                failures.Add(
                    $"유진 추격 {weapon} 무기 전환 실패");
                continue;
            }

            CharacterVerificationScenarioTools.ResetCombatState(target);

            // 추격은 추가 코인을 굴릴 때마다 살수의 감을 1 소비한다.
            // 각 무기의 상한(백우1/적설2/낙일5)을 전부 실행할 수 있도록
            // 독립 시나리오마다 충분한 감을 공급한다.
            for (int senseIndex = 0;
                 senseIndex < expectedHits + 1;
                 senseIndex++)
            {
                context.BattleContext._battleEvent.RaiseTurnStart(
                    100 + index * 10 + senseIndex);
            }

            BodyPart targetPart =
                CharacterVerificationScenarioTools.GetUsablePart(target);

            // 낙일 추격은 최대 5회의 고위력 추가타를 연속 적용한다.
            // 기본 180 HP 부위를 그대로 사용하면 마지막 추가타 도중 대상이
            // 사망하면서 YujinMechanic.OnKillResolved가 살수의 감을 1 되돌려준다.
            // 그러면 실제로는 코인마다 1씩 정상 소비했어도 순감소량만 보고
            // 5 소비가 아닌 4 소비로 오판할 수 있다. 이 검증은 추격 자체의
            // 코인 상한과 소비 계약을 격리하는 테스트이므로, 대상 부위만
            // 현재 전체 HP 이상으로 확장해 처치/부위 약화 보상을 차단한다.
            if (targetPart != null)
            {
                targetPart.PartHP = Mathf.Max(
                    targetPart.PartHP,
                    target.CurrentHP);
            }

            BattleAction action =
                CharacterVerificationScenarioTools.CreateAction(
                    owner,
                    CharacterVerificationScenarioTools.GetUsablePart(owner),
                    skill,
                    target,
                    targetPart,
                    935001 + index);

            int before = targetPart == null
                ? target.CurrentHP
                : Mathf.CeilToInt(targetPart.PartHP);

            int senseBefore = mechanic.Sense;

            bool[] allFrontSequence =
                Enumerable.Repeat(
                        true,
                        expectedHits)
                    .Concat(new[] { false })
                    .ToArray();

            int hits =
                mechanic.ResolvePursuitExtraCoinsForVerification(
                    action,
                    allFrontSequence);

            int senseAfter = mechanic.Sense;

            int after = targetPart == null
                ? target.CurrentHP
                : Mathf.CeilToInt(targetPart.PartHP);

            int consumedSense =
                senseBefore - senseAfter;

            if (hits != expectedHits ||
                consumedSense != expectedHits ||
                after >= before)
            {
                failures.Add(
                    $"유진 추격 {weapon} 상한 불일치: " +
                    $"ExpectedHits={expectedHits}, Hits={hits}, " +
                    $"Sense={senseBefore}->{senseAfter}, " +
                    $"HP={before}->{after}");
                continue;
            }

            observations.Add(
                $"유진 추격 {weapon} 상한 {expectedHits}Hit PASS " +
                $"/ Sense {senseBefore}->{senseAfter} " +
                $"/ HP {before}->{after}");
        }
    }

    private static void VerifyNamedSkillContracts(
        CharacterVerificationContext context,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        switch (context?.Bundle?.Kind)
        {
            case CharacterAuthoringKind.Olaf:
                VerifyOlafNamedSkills(
                    context,
                    failures,
                    observations);
                break;

            case CharacterAuthoringKind.Yujin:
                VerifyYujinNamedSkills(
                    context,
                    failures,
                    observations);
                break;
        }
    }

    private static void VerifyOlafNamedSkills(
        CharacterVerificationContext context,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        Olaf owner = context.Character as Olaf;
        Character target = context.OpponentCharacter;
        OlafMadnessMechanic madness = owner?.MadnessMechanic;

        if (owner == null || target == null || madness == null)
        {
            failures.Add("올라프 고유 스킬 검증 Fixture가 없습니다.");
            return;
        }

        BodyPart targetPart =
            CharacterVerificationScenarioTools.GetUsablePart(target);

        BattleAction crouch = CreateNamedAction(
            context,
            OlafSkillIds.Crouch,
            936001);
        if (crouch == null)
        {
            failures.Add("올라프 웅크리기 RuntimeSkill이 없습니다.");
        }
        else
        {
            CharacterVerificationScenarioTools.ResetCombatState(owner);
            crouch.Skill.Execute(crouch);

            if ((owner.RuntimeStatus?.currentBlock ?? 0) != 12)
                failures.Add("올라프 웅크리기 방어도는 정확히 12여야 합니다.");
            else
                observations.Add("올라프 웅크리기 방어도 12 PASS");
        }

        BattleAction glare = CreateNamedAction(
            context,
            OlafSkillIds.Glare,
            936002);
        if (glare == null)
        {
            failures.Add("올라프 노려보기 RuntimeSkill이 없습니다.");
        }
        else
        {
            int before = owner.TurnClashPowerBonus;
            glare.Skill.Execute(glare);
            int after = owner.TurnClashPowerBonus;

            if (after != before + 1)
                failures.Add($"올라프 노려보기 합 위력 불일치: {before}->{after}");
            else
                observations.Add("올라프 노려보기 이번 턴 합 위력 +1 PASS");
        }

        BattleAction showOff = CreateNamedAction(
            context,
            OlafSkillIds.ShowOff,
            936003);
        if (showOff == null)
        {
            failures.Add("올라프 가오잡기 RuntimeSkill이 없습니다.");
        }
        else
        {
            CharacterVerificationScenarioTools.ResetCombatState(owner);
            madness.SetMadnessForDebug(0);
            int beforeWeak = CountWeakenedParts(owner);
            showOff.Skill.Execute(showOff);
            int afterWeak = CountWeakenedParts(owner);

            if (madness.CurrentMadness != 2 ||
                afterWeak != beforeWeak + 1)
            {
                failures.Add(
                    $"올라프 가오잡기 불일치: Madness={madness.CurrentMadness}, " +
                    $"Weakened={beforeWeak}->{afterWeak}");
            }
            else
            {
                observations.Add("올라프 가오잡기 자가 부위 약화 1개·광기 +2 PASS");
            }
        }

        BattleAction blooming = CreateNamedAction(
            context,
            OlafSkillIds.BloomingWound,
            936004);
        if (blooming == null || targetPart == null)
        {
            failures.Add("올라프 만개하는 상처 RuntimeSkill/대상 부위가 없습니다.");
        }
        else
        {
            CharacterVerificationScenarioTools.ResetCombatState(target);
            madness.SetMadnessForDebug(0);
            blooming.Skill.Execute(blooming);
            int atZero =
                target.GetPartStatus<Bleeding>(targetPart)?.Stack ?? 0;

            CharacterVerificationScenarioTools.ResetCombatState(target);
            madness.SetMadnessForDebug(10);
            blooming.Skill.Execute(blooming);
            int atTen =
                target.GetPartStatus<Bleeding>(targetPart)?.Stack ?? 0;

            if (atZero != 3 || atTen != 5)
                failures.Add($"올라프 만개하는 상처 출혈 불일치: {atZero}/{atTen}");
            else
                observations.Add("올라프 만개하는 상처 광기 0=출혈3, 광기10=출혈5 PASS");
        }

        BattleAction bursting = CreateNamedAction(
            context,
            OlafSkillIds.BurstingMadness,
            936005);
        if (bursting == null || targetPart == null)
        {
            failures.Add("올라프 터뜨리는 광기 RuntimeSkill/대상 부위가 없습니다.");
        }
        else
        {
            CharacterVerificationScenarioTools.ResetCombatState(target);
            target.GetMechanic<OlafMadnessMechanic>()
                ?.SetMadnessForDebug(0);
            madness.SetMadnessForDebug(4);
            target.AddPartStatus(
                targetPart,
                new Bleeding(3),
                owner);

            int before = Mathf.CeilToInt(targetPart.PartHP);
            bursting.Skill.Execute(bursting);
            int after = Mathf.CeilToInt(targetPart.PartHP);
            int expectedRaw = 6 * 5 + 3;

            if (madness.CurrentMadness != 6 ||
                before - after != expectedRaw)
            {
                failures.Add(
                    $"올라프 터뜨리는 광기 불일치: Madness={madness.CurrentMadness}, " +
                    $"Damage={before - after}, Expected={expectedRaw}");
            }
            else
            {
                observations.Add("올라프 터뜨리는 광기 광기+2·(광기×5+출혈) 피해 PASS");
            }
        }

        BattleAction backsToWall = CreateNamedAction(
            context,
            OlafSkillIds.BacksToWall,
            936006);
        OlafImmortalFuryMechanic immortal = owner.ImmortalFuryMechanic;
        if (backsToWall == null || immortal == null)
        {
            failures.Add("올라프 배수진 RuntimeSkill/Mechanic이 없습니다.");
        }
        else
        {
            context.BattleContext._battleEvent.RaiseTurnEnd(900);
            backsToWall.Skill.Execute(backsToWall);

            if (!immortal.IsActive)
                failures.Add("올라프 배수진 Execute가 불사 상태를 활성화하지 않았습니다.");
            else
                observations.Add("올라프 배수진 Execute 활성화 PASS");

            context.BattleContext._battleEvent.RaiseTurnEnd(901);
        }
    }

    private static void VerifyYujinNamedSkills(
        CharacterVerificationContext context,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        Yujin owner = context.Character as Yujin;
        Character target = context.OpponentCharacter;
        YujinMechanic mechanic = owner?.YujinMechanic;
        BodyPart targetPart =
            CharacterVerificationScenarioTools.GetUsablePart(target);

        if (owner == null ||
            target == null ||
            mechanic == null ||
            targetPart == null)
        {
            failures.Add("유진 고유 스킬 검증 Fixture가 없습니다.");
            return;
        }

        if (mechanic.CurrentWeapon != YujinWeaponType.Baeku)
            mechanic.TrySwitchWeapon(YujinWeaponType.Baeku);

        BattleAction capture = CreateNamedAction(
            context,
            YujinSkillIds.Capture,
            937001);
        if (capture == null)
        {
            failures.Add("유진 포착 RuntimeSkill이 없습니다.");
        }
        else
        {
            context.BattleContext._battleEvent.RaiseTurnEnd(910);
            float before = mechanic.CurrentFrontChance;
            capture.Skill.Execute(capture);
            float active = mechanic.CurrentFrontChance;
            context.BattleContext._battleEvent.RaiseTurnEnd(911);
            float reset = mechanic.CurrentFrontChance;

            if (Mathf.Abs(active - (before + 0.10f)) > 0.0001f ||
                Mathf.Abs(reset - before) > 0.0001f)
            {
                failures.Add(
                    $"유진 포착 앞면 확률 불일치: {before:0.00}->{active:0.00}->{reset:0.00}");
            }
            else
            {
                observations.Add("유진 포착 앞면 확률 +10%·TurnEnd 초기화 PASS");
            }
        }

        BattleAction sentencing = CreateNamedAction(
            context,
            YujinSkillIds.Sentencing,
            937002);
        if (sentencing == null)
        {
            failures.Add("유진 양형 RuntimeSkill이 없습니다.");
        }
        else
        {
            context.BattleContext._battleEvent.RaiseTurnEnd(912);
            int before = mechanic.GetSkillPower(
                YujinSkillIds.Inscription,
                false);
            sentencing.Skill.Execute(sentencing);
            int active = mechanic.GetSkillPower(
                YujinSkillIds.Inscription,
                false);
            context.BattleContext._battleEvent.RaiseTurnEnd(913);
            int reset = mechanic.GetSkillPower(
                YujinSkillIds.Inscription,
                false);

            if (active != before + 2 || reset != before)
            {
                failures.Add(
                    $"유진 양형 뒷면 위력 불일치: {before}->{active}->{reset}");
            }
            else
            {
                observations.Add("유진 양형 뒷면 위력 +2·TurnEnd 초기화 PASS");
            }
        }

        VerifyYujinHitSkillContracts(
            context,
            mechanic,
            failures,
            observations);

        VerifyYujinPrestigeContracts(
            context,
            mechanic,
            failures,
            observations);
    }

    private static void VerifyYujinHitSkillContracts(
        CharacterVerificationContext context,
        YujinMechanic mechanic,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        Character owner = context.Character;
        Character target = context.OpponentCharacter;
        BodyPart targetPart =
            CharacterVerificationScenarioTools.GetUsablePart(target);

        if (targetPart == null)
            return;

        CharacterVerificationScenarioTools.TryClearPrivateCollection(
            mechanic,
            "marks");
        CharacterVerificationScenarioTools.TrySetPrivateField(
            mechanic,
            "sense",
            0);

        RaiseYujinWinningExchange(
            context,
            YujinSkillIds.Inspection,
            ActionType.NormalAttack,
            938001);
        int inspection = mechanic.GetMark(targetPart);
        int expectedInspection = mechanic.CurrentWeaponProfile.BaseMarkAmount;

        if (inspection != expectedInspection)
        {
            failures.Add(
                $"유진 검분 표식 불일치: {inspection}, Expected={expectedInspection}");
        }
        else
        {
            observations.Add("유진 검분 적중 표식 기본량 PASS");
        }

        int senseBefore = mechanic.Sense;
        RaiseYujinWinningExchange(
            context,
            YujinSkillIds.Breakfast,
            ActionType.NormalAttack,
            938002);
        if (mechanic.Sense != senseBefore + 1)
            failures.Add("유진 조식 적중 살수의 감 +1 실패");
        else
            observations.Add("유진 조식 적중 살수의 감 +1 PASS");

        CharacterVerificationScenarioTools.TryClearPrivateCollection(
            mechanic,
            "marks");
        RaiseYujinWinningExchange(
            context,
            YujinSkillIds.Inscription,
            ActionType.Duel,
            938003);
        int inscription = mechanic.GetMark(targetPart);
        int expectedInscription =
            mechanic.CurrentWeaponProfile.BaseMarkAmount * 2;

        if (inscription != expectedInscription)
        {
            failures.Add(
                $"유진 각인 결투 승리 표식 불일치: {inscription}, " +
                $"Expected={expectedInscription}");
        }
        else
        {
            observations.Add("유진 각인 결투 승리 표식 2배 PASS");
        }
    }

    private static void VerifyYujinPrestigeContracts(
        CharacterVerificationContext context,
        YujinMechanic mechanic,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        Character owner = context.Character;
        Character target = context.OpponentCharacter;
        BodyPart targetPart =
            CharacterVerificationScenarioTools.GetUsablePart(target);

        CharacterVerificationScenarioTools.TryClearPrivateCollection(
            mechanic,
            "marks");

        BattleAction brand = CreateNamedAction(
            context,
            YujinSkillIds.Brand,
            939101);
        if (brand == null || targetPart == null)
        {
            failures.Add("유진 낙인 RuntimeSkill/대상 부위가 없습니다.");
        }
        else
        {
            brand.Skill.Execute(brand);
            int immediate = mechanic.GetMark(targetPart);
            int expected = mechanic.CurrentWeaponProfile.CriticalValue;

            RaiseYujinWinningExchange(
                context,
                YujinSkillIds.Inspection,
                ActionType.NormalAttack,
                939102);
            int afterHit = mechanic.GetMark(targetPart);

            int expectedAfterHit =
                expected +
                mechanic.CurrentWeaponProfile.BaseMarkAmount +
                3;

            if (immediate != expected || afterHit != expectedAfterHit)
            {
                failures.Add(
                    $"유진 낙인 표식 불일치: Immediate={immediate}, " +
                    $"AfterHit={afterHit}, Expected={expected}/{expectedAfterHit}");
            }
            else
            {
                observations.Add("유진 낙인 즉시 치명 수치 표식·후속 적중 +3 PASS");
            }

            context.BattleContext._battleEvent.RaiseTurnEnd(920);
        }

        BattleAction joint = CreateNamedAction(
            context,
            YujinSkillIds.JointLiability,
            939201);
        if (joint == null)
        {
            failures.Add("유진 연대책임 RuntimeSkill이 없습니다.");
        }
        else
        {
            context.BattleContext._battleEvent.RaiseTurnEnd(921);
            joint.Skill.Execute(joint);

            BodyPartBreakEventContext breakContext =
                BodyPartBreakEventContext.External(
                    owner,
                    target,
                    targetPart,
                    joint);

            context.BattleContext._battleEvent.RaiseBodyPartDestroyed(
                breakContext);
            context.BattleContext._battleEvent.RaiseKill(
                KillEventContext.External(
                    owner,
                    target,
                    joint));
            context.BattleContext._battleEvent.RaiseBodyPartDestroyed(
                breakContext);
            context.BattleContext._battleEvent.RaiseKill(
                KillEventContext.External(
                    owner,
                    target,
                    joint));

            int charges = 0;
            while (charges < 10 && mechanic.TryConsumeForcedFront())
                charges++;

            if (charges != 4)
                failures.Add($"유진 연대책임 강제 앞면 최대치 불일치: {charges}/4");
            else
                observations.Add("유진 연대책임 최초1+파괴/처치 연계 최대4 PASS");

            context.BattleContext._battleEvent.RaiseTurnEnd(922);
        }

        BattleAction retrial = CreateNamedAction(
            context,
            YujinSkillIds.Retrial,
            939301);
        Skill duelSkill = CharacterVerificationScenarioTools.FindRuntimeSkill(
            owner,
            CharacterVerificationScenarioTools.FindDefinition(
                context.Bundle,
                YujinSkillIds.Inscription));

        if (retrial == null || duelSkill == null)
        {
            failures.Add("유진 재심/각인 RuntimeSkill이 없습니다.");
        }
        else
        {
            context.BattleContext._battleEvent.RaiseTurnEnd(923);
            CharacterVerificationScenarioTools.TrySetPrivateField(
                mechanic,
                "sense",
                0);
            mechanic.AutoUseSense = false;
            retrial.Skill.Execute(retrial);

            BattleAction mine = CharacterVerificationScenarioTools.CreateAction(
                owner,
                CharacterVerificationScenarioTools.GetUsablePart(owner),
                duelSkill,
                target,
                targetPart,
                939302);
            mine.ClashPower = 5;
            mine.LastRollResult = new RollResult
            {
                FinalPower = 5,
                ClashPower = 5,
                IsCritical = false
            };

            BattleAction opponent = CharacterVerificationScenarioTools.CreateAction(
                target,
                targetPart,
                target.RuntimeSkills.FirstOrDefault(),
                owner,
                mine.OwnerPart,
                939303);
            opponent.ClashPower = 10;

            bool first = mechanic.TryRequestExchangeReroll(
                new ExchangeRerollContext(mine, opponent, 0, 0));
            bool sameExchange = mechanic.TryRequestExchangeReroll(
                new ExchangeRerollContext(mine, opponent, 0, 1));
            bool nextExchange = mechanic.TryRequestExchangeReroll(
                new ExchangeRerollContext(mine, opponent, 1, 0));
            int senseAfter = mechanic.Sense;

            context.BattleContext._battleEvent.RaiseTurnEnd(924);
            bool afterTurn = mechanic.TryRequestExchangeReroll(
                new ExchangeRerollContext(mine, opponent, 2, 0));

            if (!first || sameExchange || !nextExchange ||
                senseAfter != 0 || afterTurn)
            {
                failures.Add(
                    $"유진 재심 재굴림 불일치: First={first}, Same={sameExchange}, " +
                    $"Next={nextExchange}, Sense={senseAfter}, AfterTurn={afterTurn}");
            }
            else
            {
                observations.Add("유진 재심 교환당 무료 1회·감 미소모·TurnEnd 해제 PASS");
            }
        }
    }

    private static void RaiseYujinWinningExchange(
        CharacterVerificationContext context,
        string skillId,
        ActionType opponentType,
        long actionId)
    {
        Character owner = context.Character;
        Character target = context.OpponentCharacter;
        BodyPart ownerPart =
            CharacterVerificationScenarioTools.GetUsablePart(owner);
        BodyPart targetPart =
            CharacterVerificationScenarioTools.GetUsablePart(target);
        Skill skill = CharacterVerificationScenarioTools.FindRuntimeSkill(
            owner,
            CharacterVerificationScenarioTools.FindDefinition(
                context.Bundle,
                skillId));
        Skill opponentSkill = target.RuntimeSkills.FirstOrDefault(
            item => item?.ActionType == opponentType) ??
            target.RuntimeSkills.FirstOrDefault();

        if (skill == null || opponentSkill == null)
            return;

        BattleAction mine = CharacterVerificationScenarioTools.CreateAction(
            owner,
            ownerPart,
            skill,
            target,
            targetPart,
            actionId);
        BattleAction theirs = CharacterVerificationScenarioTools.CreateAction(
            target,
            targetPart,
            opponentSkill,
            owner,
            ownerPart,
            actionId + 100000);
        mine.CurrentRollType = CombatRollType.Attack;
        mine.LastRollResult = new RollResult
        {
            FinalPower = 10,
            ClashPower = 10,
            IsCritical = false
        };
        mine.HasRolled = true;

        DamageContext damage = new DamageContext(
            DamageRequest.SkillPart(mine, 1, false))
        {
            AppliedDamage = 1,
            AppliedPartDamage = 1,
            WasApplied = true
        };

        context.BattleContext._battleEvent.RaiseExchangeResolved(
            new ClashExchangeResult
            {
                FirstAction = mine,
                SecondAction = theirs,
                WinnerAction = mine,
                LoserAction = theirs,
                DamageContext = damage,
                IsDuelExchange = opponentType == ActionType.Duel
            });
    }

    private static BattleAction CreateNamedAction(
        CharacterVerificationContext context,
        string skillId,
        long actionId)
    {
        SkillDefinition definition =
            CharacterVerificationScenarioTools.FindDefinition(
                context.Bundle,
                skillId);
        Skill skill = CharacterVerificationScenarioTools.FindRuntimeSkill(
            context.Character,
            definition);

        if (skill == null)
            return null;

        return CharacterVerificationScenarioTools.CreateAction(
            context.Character,
            CharacterVerificationScenarioTools.GetUsablePart(context.Character),
            skill,
            context.OpponentCharacter,
            CharacterVerificationScenarioTools.GetUsablePart(context.OpponentCharacter),
            actionId);
    }

    private static int CountWeakenedParts(
        Character character)
    {
        return character?.BodyParts?.Count(
            item => item?.IsWeakened == true) ?? 0;
    }

    private static void VerifyElite(
        CharacterVerificationContext context,
        EliteEnemyMechanic mechanic,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        Character owner = context.Character;
        Character target = context.OpponentCharacter;
        BodyPart ownerPart = CharacterVerificationScenarioTools.GetUsablePart(owner);
        BodyPart targetPart = CharacterVerificationScenarioTools.GetUsablePart(target);
        Skill skill = owner.RuntimeSkills.FirstOrDefault();

        if (owner.RuntimeStatus != null)
            owner.RuntimeStatus.currentPrestige = 0;
        CharacterVerificationScenarioTools.ResetCombatState(target);

        BattleAction winner = CharacterVerificationScenarioTools.CreateAction(
            owner, ownerPart, skill, target, targetPart, 940001);
        BattleAction loser = CharacterVerificationScenarioTools.CreateAction(
            target, targetPart, target.RuntimeSkills.FirstOrDefault(), owner, ownerPart, 940002);
        context.BattleContext._battleEvent.RaiseClashWin(winner, loser);

        int prestigeAfterWin = owner.RuntimeStatus?.currentPrestige ?? 0;
        int bleeding = targetPart == null
            ? target.GetStatus<Bleeding>()?.Stack ?? 0
            : target.GetPartStatus<Bleeding>(targetPart)?.Stack ?? 0;

        if (prestigeAfterWin != 10 || bleeding != 1)
        {
            failures.Add(
                $"Elite 합 승리 효과 불일치: Prestige={prestigeAfterWin}, Bleeding={bleeding}");
        }

        context.BattleContext._battleEvent.RaiseBodyPartWeakened(owner, ownerPart);
        int afterWeaken = owner.RuntimeStatus?.currentPrestige ?? 0;
        context.BattleContext._battleEvent.RaiseBodyPartDestroyed(owner, ownerPart);
        int afterBreak = owner.RuntimeStatus?.currentPrestige ?? 0;

        if (afterWeaken != prestigeAfterWin + 15 ||
            afterBreak != afterWeaken + 25)
        {
            failures.Add(
                $"Elite 자가 부위 이벤트 위세 불일치: " +
                $"Win={prestigeAfterWin}, Weaken={afterWeaken}, Break={afterBreak}");
        }
        else
        {
            observations.Add("Elite 합 승리 +10/출혈1, 자가 약화 +15, 파괴 +25 PASS");
        }
    }

    private static void VerifyPosture(
        CharacterVerificationContext context,
        EnemyPostureMechanic mechanic,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        EnemyPosture[] expected =
        {
            EnemyPosture.Crouching,
            EnemyPosture.Offensive,
            EnemyPosture.Normal
        };

        for (int i = 0; i < expected.Length; i++)
        {
            CharacterVerificationScenarioTools.TrySetPrivateField(
                mechanic,
                "turnsRemaining",
                1);
            context.BattleContext._battleEvent.RaiseTurnStart(i + 1);

            if (mechanic.Current != expected[i])
            {
                failures.Add(
                    $"EnemyPosture 회전 실패[{i}]: {mechanic.Current}, Expected={expected[i]}");
            }

            int drift = mechanic.ExpectedPlayerMomentumDrift;
            bool driftValid = mechanic.Current switch
            {
                EnemyPosture.Crouching => drift > 0,
                EnemyPosture.Offensive => drift < 0,
                _ => drift == 0
            };

            if (!driftValid || mechanic.CurrentAttackSlotLimit < 0)
                failures.Add($"EnemyPosture 수치 계약 실패: {mechanic.Current}");
        }

        observations.Add("EnemyPosture Normal→Crouching→Offensive→Normal PASS");
    }

    private static void VerifyBloodScent(
        CharacterVerificationContext context,
        NormalEnemyBloodScentMechanic mechanic,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        Character owner = context.Character;
        Character target = context.OpponentCharacter;
        BodyPart ownerPart = CharacterVerificationScenarioTools.GetUsablePart(owner);
        BodyPart targetPart = CharacterVerificationScenarioTools.GetUsablePart(target);
        CharacterVerificationScenarioTools.ResetCombatState(target);

        BattleAction winner = CharacterVerificationScenarioTools.CreateAction(
            owner,
            ownerPart,
            owner.RuntimeSkills.FirstOrDefault(),
            target,
            targetPart,
            950001);
        BattleAction loser = CharacterVerificationScenarioTools.CreateAction(
            target,
            targetPart,
            target.RuntimeSkills.FirstOrDefault(),
            owner,
            ownerPart,
            950002);

        context.BattleContext._battleEvent.RaiseClashWin(winner, loser);

        int bleeding = targetPart == null
            ? target.GetStatus<Bleeding>()?.Stack ?? 0
            : target.GetPartStatus<Bleeding>(targetPart)?.Stack ?? 0;

        if (bleeding != 1)
            failures.Add($"피 냄새 합 승리 출혈 불일치: {bleeding}");
        else
            observations.Add("NormalEnemy 피 냄새 합 승리 출혈 1 PASS");
    }
}
