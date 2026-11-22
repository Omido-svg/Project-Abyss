using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CharacterVerificationForcedScenarioRunner
{
    private static readonly HashSet<Type> SupportedEffects =
        new HashSet<Type>
        {
            typeof(AddBodyPartStatusEffect),
            typeof(ApplyStatusIfConditionEffect),
            typeof(ApplyChinchiroSelfDamageEffect),
            typeof(ApplyRolledPartDamageEffect),
            typeof(ConditionalDamageEffect),
            typeof(ForceBreakOwnPartEffect),
            typeof(GainCustomResourceEffect),
            typeof(GainPrestigeEffect),
            typeof(ModifyResourceEffect),
            typeof(PreparationFlatSetupEffect),
            typeof(OlafBreakOwnPartEffect),
            typeof(OlafWeakenOwnPartEffect),
            typeof(OlafNormalBleedEffect),
            typeof(OlafPrestigeEffect),
            // 11-20 검증 도구 개편 — 일반 효과 파이프라인(Apply)으로 도는 캐릭터 rider들.
            typeof(TempBalanceSkillProxyEffectDefinition),
            typeof(OlafMadnessEffectDefinition),
            typeof(YujinRiderEffectDefinition),
            typeof(YujinFinisherEffectDefinition)
        };

    public static bool IsEffectSupported(
        SkillEffectDefinition definition)
    {
        if (definition == null)
            return false;

        Type type = definition.GetType();
        return SupportedEffects.Any(
            supported => supported.IsAssignableFrom(type));
    }

    public static CharacterVerificationCaseResult RunSkill(
        CharacterVerificationContext context,
        SkillDefinition definition)
    {
        // 11-21: 규칙상 잠금이 (미정) 수치 때문인 카드 — 「막혀 있어야 정상」.
        if (ExpectedBlockedByPendingNumbers.TryGetValue(
                definition?.SkillId ?? string.Empty, out string pendingReason))
        {
            Character blockedOwner = context?.Character;
            Skill blockedSkill = blockedOwner == null ? null :
                CharacterVerificationScenarioTools.FindRuntimeSkill(blockedOwner, definition);
            if (blockedSkill != null)
                CharacterVerificationScenarioTools.FillResources(blockedOwner, definition, blockedSkill);
            bool usable = blockedSkill?.CanUseByResource(blockedOwner) == true &&
                          !PendingNumbersBlockRuntime(blockedOwner, definition);
            return usable
                ? context.Fail("(미정) 수치 카드는 막혀 있어야 함", "사용 가능", pendingReason)
                : context.Pass("(미정) 수치 카드는 막혀 있어야 함", "막힘 — 정상", pendingReason);
        }

        // 다수 적 전용 효과 — 단일 적 Fixture에서는 원리상 확인 불가(실패가 아니라 건너뜀).
        if (definition?.SkillId == "yujin.duel.spread_rumor" &&
            (context?.BattleContext?.Enemies?.Count(e => e != null && !e.IsDead) ?? 0) < 2)
        {
            return context.Skip(
                "소문을 내다 — 방금 맞은 적을 뺀 다른 적에게 표식을 준다. 적 2명 이상 Fixture가 필요(시스템 검증 · 플레이로 확인)");
        }

        var restore = PrepareUsageConditions(context?.Character, definition);
        try
        {
            return RunSkillCore(context, definition);
        }
        finally
        {
            restore?.Invoke();
        }
    }

    private static readonly Dictionary<string, string> ExpectedBlockedByPendingNumbers =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hifumi.duel.l.provoke"] = "규칙서 수치 (미정) — HifumiSkillIds.HasPendingCanonicalRuntimeNumbers",
            ["hifumi.duel.m.cleanse_all"] = "규칙서 수치 (미정) — HifumiSkillIds.HasPendingCanonicalRuntimeNumbers",
            ["hifumi.duel.k.flip_table"] = "규칙서 수치 (미정) — HifumiSkillIds.HasPendingCanonicalRuntimeNumbers",
        };

    // 덫 두 장은 사용은 되지만 (미정) 수치 때문에 효과가 등록되지 않는다 — 그쪽을 막힘으로 본다.
    private static bool PendingNumbersBlockRuntime(Character owner, SkillDefinition d)
    {
        if (d?.SkillId != "yujin.duel.trap" && d?.SkillId != "yujin.duel.deadline")
            return false;
        YujinMarkRuleSettings rules = owner?.GetMechanic<YujinMechanic>()?.MarkRules;
        bool registers = rules != null && rules.TrapLifetimeTurns > 0 &&
            (d.SkillId == "yujin.duel.deadline" || rules.ExplosionDamagePerTrap > 0);
        return !registers;
    }

    /// <summary>
    /// 11-21: 사용 조건 잠금(규칙상 정상 잠금)을 검증기가 직접 만든다. 끝나면 되돌린다.
    /// 닥치는 대로 · 끝장을 보다 = 빛 4(최대치 3 → 열광 레벨업으로 4) / 최후의 저항 = 지난 턴 짓눌림 /
    /// 손에 익히다 = 이번 턴 환형 교체 / 퇴로 차단 = 지난 턴 짓누름.
    /// </summary>
    private static Action PrepareUsageConditions(Character owner, SkillDefinition d)
    {
        if (owner == null || d == null)
            return null;
        Action restore = null;

        if (d.SkillId == "olaf.duel.whatever_comes" || d.SkillId == "yujin.duel.p.finish_it")
        {
            int need = Mathf.Max(0, d.EnergyCost - owner.MaxEnergy);
            if (need > 0)
            {
                owner.AdjustEnergyMaximum(need, true);
                restore += () => owner.AdjustEnergyMaximum(-need, false);
            }
        }

        MomentumState? previous =
            d.SkillId == "olaf.preparation.last_resistance" ? MomentumState.LastStand :
            d.SkillId == "yujin.duel.block_retreat" ? MomentumState.Overwhelm :
            (MomentumState?)null;
        if (previous.HasValue)
        {
            MomentumManager momentum = owner.BattleContext?.ResolveMomentumManager();
            CharacterVerificationScenarioTools.TrySetPrivateField(momentum, "playerPreviousFinalState", previous.Value);
            CharacterVerificationScenarioTools.TrySetPrivateField(momentum, "enemyPreviousFinalState", previous.Value);
            restore += () =>
            {
                CharacterVerificationScenarioTools.TrySetPrivateField(momentum, "playerPreviousFinalState", MomentumState.Balance);
                CharacterVerificationScenarioTools.TrySetPrivateField(momentum, "enemyPreviousFinalState", MomentumState.Balance);
            };
        }

        if (d.SkillId == "yujin.normal.familiar_hand")
        {
            YujinMechanic yujin = owner.GetMechanic<YujinMechanic>();
            CharacterVerificationScenarioTools.TrySetPrivateField(yujin, "weaponChangedThisTurn", true);
            restore += () => CharacterVerificationScenarioTools.TrySetPrivateField(yujin, "weaponChangedThisTurn", false);
        }
        return restore;
    }

    private static CharacterVerificationCaseResult RunSkillCore(
        CharacterVerificationContext context,
        SkillDefinition definition)
    {
        if (context?.Character == null ||
            context.OpponentCharacter == null ||
            definition == null)
        {
            return context?.Fail(
                "Owner·Target·SkillDefinition 존재",
                "검증 Fixture 누락");
        }

        Character owner = context.Character;
        Character target = context.OpponentCharacter;
        Skill skill = CharacterVerificationScenarioTools
            .FindRuntimeSkill(owner, definition);

        if (skill == null)
        {
            return context.Fail(
                "SkillDefinition의 RuntimeSkill 생성",
                "NULL",
                definition.SkillId);
        }

        List<string> failures = new List<string>();
        List<string> observations = new List<string>();

        BodyPart ownerPart =
            CharacterVerificationScenarioTools.GetUsablePart(owner);
        BodyPart targetPart =
            CharacterVerificationScenarioTools.GetUsablePart(target);

        BattleAction action =
            CharacterVerificationScenarioTools.CreateAction(
                owner,
                ownerPart,
                skill,
                target,
                targetPart);

        VerifyResourceContract(
            owner,
            definition,
            skill,
            action,
            failures,
            observations);

        VerifyRollContract(
            owner,
            skill,
            action,
            context.Definition.Seed,
            failures,
            observations);

        if (definition.ActionType == ActionType.NormalAttack ||
            definition.ActionType == ActionType.Duel)
        {
            VerifyPrimaryDamageTarget(
                context,
                action,
                failures,
                observations);
        }
        else if (action.Target != target ||
                 action.TargetPart != targetPart)
        {
            failures.Add("BattleAction의 대상 캐릭터/부위 바인딩이 생성 직후 불일치합니다.");
        }

        CharacterVerificationScenarioTools.FillResources(
            owner,
            definition,
            skill);

        bool requireObservableExecuteMutation =
            PrepareCustomExecuteScenario(
                owner,
                definition,
                observations);

        string beforeExecute =
            CharacterVerificationScenarioTools.CaptureFingerprint(
                owner,
                target,
                definition.CustomResourceKey);

        skill.Execute(action);

        string afterExecute =
            CharacterVerificationScenarioTools.CaptureFingerprint(
                owner,
                target,
                definition.CustomResourceKey);

        bool requiresCustomExecuteChange =
            definition.ActionType == ActionType.Preparation ||
            definition.ActionType == ActionType.Prestige;

        if (requiresCustomExecuteChange &&
            requireObservableExecuteMutation &&
            !definition.EnumerateEffectEntries().Any() &&
            string.Equals(
                beforeExecute,
                afterExecute,
                StringComparison.Ordinal))
        {
            failures.Add(
                "도사림/위세 Execute가 준비된 유효 시나리오에서도 어떤 관찰 가능 상태도 변경하지 않았습니다.");
        }

        int effectCount = 0;
        if (IsEventOwnedNativeRoute(definition))
        {
            // 위세 3장 · 출혈 폭발 결투는 OlafMadnessMechanic이 이벤트로 직접 실행한다.
            // 디스패처는 빈 결과를 돌려주는 것이 정상 — 시스템 검증(Phase D)이 본다.
            observations.Add("Route: 이벤트 소유(빌야르 메커닉) — 디스패처 결과 없음이 정상");
        }
        else if (IsProductionNativeRoute(definition))
        {
            effectCount = SweepNativeRoute(
                context,
                definition,
                skill,
                ownerPart,
                targetPart,
                failures,
                observations);
        }
        else
        {
            foreach (SkillEffectEntry entry
                     in definition.EnumerateEffectEntries())
            {
                effectCount++;
                VerifyEffectEntry(
                    context,
                    definition,
                    action,
                    entry,
                    failures,
                    observations);
            }
        }

        if (failures.Count == 0)
        {
            return context.Pass(
                "자원·굴림·대상·모든 효과 Entry 강제 실행",
                $"PASS / Rolls={skill.ExchangeRollCount}, Effects={effectCount}",
                string.Join("\n", observations));
        }

        return context.Fail(
            "자원·굴림·대상·모든 효과 Entry 강제 실행",
            $"FAIL {failures.Count}",
            string.Join("\n", failures) +
            "\n\nObservations\n" +
            string.Join("\n", observations));
    }

    /// <summary>
    /// Character-specific Preparation/Prestige는 정상 사용 조건이 충족되지 않으면
    /// 합법적으로 no-op일 수 있다. generic coverage가 "무조건 상태가 바뀌어야 한다"는
    /// 잘못된 전제로 false positive를 만들지 않도록 실제 사용 가능한 선행 상태를 만든다.
    ///
    /// 반환값이 false인 경우는 Execute 자체가 확률적으로 정상 no-op일 수 있고,
    /// 별도의 결정론적 전용 Case가 그 동작을 검증하는 경우다.
    /// </summary>
    private static bool PrepareCustomExecuteScenario(
        Character owner,
        SkillDefinition definition,
        ICollection<string> observations)
    {
        if (owner == null ||
            definition == null)
        {
            return true;
        }

        string skillId =
            definition.SkillId;

        // 11-21: 현행 환형(공격/수비)은 무기를 계획 단계에서 고른다 — 옛 무기별 ID만 받던 조건을 넓힘.
        YujinWeaponType targetWeapon = YujinWeaponType.Jeokseol;
        if (owner is Yujin yujin &&
            (YujinMechanic.TryGetHwanhyeongWeapon(skillId, out targetWeapon) ||
             YujinMechanic.IsHwanhyeongSkill(skillId)))
        {
            YujinMechanic mechanic =
                yujin.GetMechanic<YujinMechanic>();

            if (mechanic != null)
            {
                // 환형 · 백우는 유진의 기본 무기가 백우라서,
                // 아무 준비 없이 Execute하면 "현재 무기와 동일" 조건으로 정상 거절된다.
                // 대상 무기와 다른 무기로 Fixture를 세팅해 실제 예약 경로를 검사한다.
                YujinWeaponType sourceWeapon =
                    targetWeapon == YujinWeaponType.Baeku
                        ? YujinWeaponType.Jeokseol
                        : YujinWeaponType.Baeku;

                mechanic.SetWeaponForVerification(
                    sourceWeapon);

                observations?.Add(
                    $"Precondition: 환형 {sourceWeapon}->{targetWeapon} 실행 상태 구성");

                // 11-21: 환형의 무기 교체는 계획 확정(TryCommitPlannedSlot)에서 일어난다 —
                // Execute는 원래 아무것도 안 바꾼다. 계획 확정 경로로 확인하고 되돌린다.
                YujinWeaponType before = mechanic.CurrentWeapon;
                YujinWeaponType next = before == YujinWeaponType.Baeku
                    ? YujinWeaponType.Jeokseol : YujinWeaponType.Baeku;
                Skill hwanSkill = CharacterVerificationScenarioTools.FindRuntimeSkill(owner, definition);
                var slot = new ActionSlot
                {
                    ActionId = 970001, Owner = owner,
                    Part = CharacterVerificationScenarioTools.GetUsablePart(owner),
                    Skill = hwanSkill, PlanningChoiceId = next.ToString()
                };
                bool ok = ((IActionPlanningCommitRule)mechanic).TryCommitPlannedSlot(slot, out string reason);
                bool switched = mechanic.CurrentWeapon == next && mechanic.WeaponChangedThisTurn;
                mechanic.SetWeaponForVerification(sourceWeapon);
                CharacterVerificationScenarioTools.TrySetPrivateField(mechanic, "weaponChangedThisTurn", false);
                CharacterVerificationScenarioTools.TrySetPrivateField(mechanic, "weaponSwitchUsedThisTurn", false);
                ClearPrivateCollection(mechanic, "weaponPlans");
                observations?.Add(ok && switched
                    ? $"Planning PASS: 환형 계획 확정 {before}->{next}"
                    : $"Planning FAIL: 환형 계획 확정 실패 ({reason})");
                return !(ok && switched);
            }

            return true;
        }

        if (owner is Hifumi hifumi)
        {
            HifumiMechanic mechanic =
                hifumi.HifumiMechanic;

            if (mechanic == null)
                return true;

            if (skillId == HifumiSkillIds.FoldHand)
            {
                // 이 판 접지는 뼈 100 이상일 때만 소비/회복한다.
                mechanic.SetBoneForVerification(
                    150);

                observations?.Add(
                    "Precondition: 이 판 접지 Bone=150");
                return true;
            }

            if (skillId == HifumiSkillIds.GamblerMove)
            {
                // 도박수는 현재 뼈를 전소하므로 Bone=0에서는 정상 no-op이다.
                mechanic.SetBoneForVerification(
                    250);

                observations?.Add(
                    "Precondition: 도박수 Bone=250");
                return true;
            }

            if (skillId == HifumiSkillIds.AllIn)
            {
                // 올인은 3회 친치로 결과가 전부 Blank면 상태 변화가 없는 것이 정상이다.
                // 무작위 결과 한 번의 fingerprint로 실패 판정하지 않는다.
                // 최고 결과/대실패 전소는 Hifumi 전용
                // `hifumi.prestige.contracts`가 결정론적으로 검증한다.
                mechanic.SetBoneForVerification(
                    100);

                observations?.Add(
                    "AllIn: random Blank 정상 no-op 허용 / " +
                    "결정론적 계약은 hifumi.prestige.contracts에서 별도 검증");
                return false;
            }
        }

        return true;
    }

    private static void VerifyResourceContract(
        Character owner,
        SkillDefinition definition,
        Skill skill,
        BattleAction action,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        if (owner == null || skill == null)
            return;

        if (owner.CurrentEnergy > 0)
        {
            owner.TryConsumeEnergy(
                owner.CurrentEnergy,
                action,
                skill);
        }

        if (owner.RuntimeStatus != null)
            owner.RuntimeStatus.currentPrestige = 0;

        if (!string.IsNullOrWhiteSpace(
                definition.CustomResourceKey))
        {
            SkillResourceAccess.Set(
                owner,
                definition.CustomResourceKey,
                0);
        }

        bool needsResource =
            skill.EnergyCost > 0 ||
            skill.ActionType == ActionType.Prestige ||
            definition.RequireFullPrestige ||
            definition.PrestigeCost > 0 ||
            definition.CustomResourceCost > 0;

        bool canUseEmpty = skill.CanUseByResource(owner);
        if (needsResource && canUseEmpty)
        {
            failures.Add(
                "자원이 비어 있는데 CanUseByResource가 true입니다.");
        }

        CharacterVerificationScenarioTools.FillResources(
            owner,
            definition,
            skill);

        int energyBefore = owner.CurrentEnergy;
        int prestigeBefore =
            owner.RuntimeStatus?.currentPrestige ?? 0;
        int customBefore =
            SkillResourceAccess.Get(
                owner,
                definition.CustomResourceKey);

        if (!skill.CanUseByResource(owner))
        {
            failures.Add(
                "충분한 자원을 넣은 뒤에도 CanUseByResource가 false입니다.");
            return;
        }

        // 11-21: 쓸 때마다 비용이 줄어드는 카드(닥치는 대로 등)는 소모 직후 EnergyCost가
        // 이미 내려가 있다 — 기대값은 소모 전 비용으로 계산한다.
        int costBefore = skill.EnergyCost;

        if (!skill.TryConsumeResource(owner, action))
        {
            failures.Add(
                "충분한 자원 상태에서 TryConsumeResource가 실패했습니다.");
            return;
        }

        int expectedEnergy =
            Mathf.Max(0, energyBefore - costBefore);

        if (owner.CurrentEnergy != expectedEnergy)
        {
            failures.Add(
                $"에너지 비용 불일치: {energyBefore}->{owner.CurrentEnergy}, " +
                $"Expected={expectedEnergy}");
        }

        if (definition.OverrideResourceRules)
        {
            int expectedPrestige = prestigeBefore;
            if (definition.ConsumeAllPrestige)
                expectedPrestige = 0;
            else if (definition.PrestigeCost > 0)
                expectedPrestige = Mathf.Max(
                    0,
                    prestigeBefore - definition.PrestigeCost);

            if ((owner.RuntimeStatus?.currentPrestige ?? 0) !=
                expectedPrestige)
            {
                failures.Add(
                    "위세 비용 소비 결과가 SkillDefinition과 다릅니다.");
            }

            if (!string.IsNullOrWhiteSpace(
                    definition.CustomResourceKey))
            {
                int expectedCustom = customBefore;
                if (definition.ConsumeAllCustomResource)
                    expectedCustom = 0;
                else if (definition.CustomResourceCost > 0)
                    expectedCustom = Mathf.Max(
                        0,
                        customBefore - definition.CustomResourceCost);

                int actualCustom = SkillResourceAccess.Get(
                    owner,
                    definition.CustomResourceKey);

                if (actualCustom != expectedCustom)
                {
                    failures.Add(
                        $"특수 자원 비용 불일치: {customBefore}->{actualCustom}, " +
                        $"Expected={expectedCustom}");
                }
            }
        }
        else if (skill.ActionType == ActionType.Prestige &&
                 (owner.RuntimeStatus?.currentPrestige ?? 0) != 0)
        {
            failures.Add("기본 위세 스킬이 위세 게이지를 0으로 만들지 않았습니다.");
        }

        observations.Add(
            $"Resource PASS: Energy {energyBefore}->{owner.CurrentEnergy}, " +
            $"Prestige {prestigeBefore}->{(owner.RuntimeStatus?.currentPrestige ?? 0)}");
    }

    private static void VerifyRollContract(
        Character owner,
        Skill skill,
        BattleAction action,
        int seed,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        int count = Mathf.Max(1, skill.ExchangeRollCount);

        for (int index = 0; index < count; index++)
        {
            UnityEngine.Random.InitState(seed + index * 31);
            RollResult first =
                skill.RollPowerResultForExchange(index);

            UnityEngine.Random.InitState(seed + index * 31);
            RollResult second =
                skill.RollPowerResultForExchange(index);

            if (first == null || second == null)
            {
                failures.Add($"Roll[{index}]가 NULL입니다.");
                continue;
            }

            if (first.FinalPower != second.FinalPower ||
                first.RawValue != second.RawValue ||
                first.IsCritical != second.IsCritical)
            {
                failures.Add(
                    $"Roll[{index}]가 같은 Seed에서 결정론적이지 않습니다: " +
                    $"{first.GetShortDisplayText()} vs {second.GetShortDisplayText()}");
            }

            if (first.FinalPower < 0)
                failures.Add($"Roll[{index}] FinalPower가 음수입니다.");

            observations.Add(
                $"Roll[{index}]={first.GetShortDisplayText()}");
        }

        UnityEngine.Random.InitState(seed);
        action.RollPowerForExchange(0);
    }

    private static void VerifyPrimaryDamageTarget(
        CharacterVerificationContext context,
        BattleAction action,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        if (action?.Owner == null ||
            action.Target == null ||
            context.BattleContext?.ResolveDamageManager() == null)
        {
            failures.Add("기본 피해 검증의 Owner/Target/DamageManager가 없습니다.");
            return;
        }

        BodyPart targetPart = action.TargetPart;
        int before = targetPart == null
            ? action.Target.CurrentHP
            : Mathf.CeilToInt(targetPart.PartHP);

        DamageRequest request = DamageRequest.Custom(
            targetPart == null
                ? DamageType.Direct
                : DamageType.SkillPart,
            action.Owner,
            action.Target,
            targetPart,
            1,
            1f,
            action.Skill?.CanBreakPart == true,
            applyMomentum: false,
            applyGuard: false,
            sourceAction: action);

        request.ApplyAttackerModifiers = false;
        request.ApplyTargetModifiers = false;

        DamageContext damage = context.BattleContext
            .ResolveDamageManager()
            .ApplyDamageContext(request);

        int after = targetPart == null
            ? action.Target.CurrentHP
            : Mathf.CeilToInt(targetPart.PartHP);

        if (damage == null ||
            damage.Target != action.Target ||
            damage.TargetPart != targetPart ||
            after >= before)
        {
            failures.Add(
                $"기본 피해 대상/적용 실패: Before={before}, After={after}, " +
                $"TargetMatch={damage?.Target == action.Target}, " +
                $"PartMatch={damage?.TargetPart == targetPart}");
        }
        else
        {
            observations.Add(
                $"DamageTarget PASS: {before}->{after} / " +
                $"{action.Target.Data?.CharacterName}/{targetPart?.Type.ToString() ?? "CHARACTER"}");
        }
    }

    private static void VerifyEffectEntry(
        CharacterVerificationContext verification,
        SkillDefinition definition,
        BattleAction action,
        SkillEffectEntry entry,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        if (entry?.Definition == null)
        {
            failures.Add("NULL SkillEffectEntry가 있습니다.");
            return;
        }

        SkillEffectDefinition effect = entry.Definition;
        if (!IsEffectSupported(effect))
        {
            failures.Add(
                $"지원하지 않는 Effect 계약: {effect.GetType().FullName}");
            return;
        }

        Character owner = action.Owner;
        Character target = action.Target;
        CharacterVerificationScenarioTools.ResetCombatState(owner);
        CharacterVerificationScenarioTools.ResetCombatState(target);
        CharacterVerificationScenarioTools.FillResources(
            owner,
            definition,
            action.Skill);

        action.ClearResolutionTarget();
        action.BeginResolutionSequence();
        action.RollPowerForExchange(0);

        DamageContext fakeDamage = CreateFakeDamage(action);
        KillEventContext kill = KillEventContext.External(
            owner,
            target,
            action);

        int useCount = 1;
        SkillEffectContext effectContext =
            new SkillEffectContext(
                action,
                definition,
                effect.Timing,
                CreateOpponentAction(action),
                fakeDamage,
                kill,
                useCount);

        UnityEngine.Random.InitState(
            verification.Definition.Seed + Mathf.Abs(effect.GetInstanceID()));

        SkillEffectContext selected =
            effectContext.WithTarget(effect.TargetSelector);

        PrepareEffectSpecificState(
            effect,
            entry.Overrides,
            selected,
            action);

        if (!SeedConditions(
                effect,
                selected,
                ref fakeDamage,
                ref kill,
                ref useCount,
                out string conditionFailure))
        {
            failures.Add(
                $"{effect.name}: 조건 강제 구성 실패 - {conditionFailure}");
            return;
        }

        effectContext = new SkillEffectContext(
            action,
            definition,
            effect.Timing,
            CreateOpponentAction(action),
            fakeDamage,
            kill,
            useCount);

        string key = definition.CustomResourceKey;
        if (effect is GainCustomResourceEffect gain)
            key = gain.ResourceKey;
        else if (effect is ModifyResourceEffect modify)
            key = modify.CustomResourceKey;

        string before =
            CharacterVerificationScenarioTools.CaptureFingerprint(
                owner,
                target,
                key);

        UnityEngine.Random.InitState(
            verification.Definition.Seed + Mathf.Abs(effect.GetInstanceID()));

        // 11-21: 옛 Entry.TryApply 대신 실전 디스패처로 실행한다. 네이티브 카드는
        // 네이티브 그래프가, 레거시 카드는 레거시 Entry가 돈다(실전과 같은 경로).
        // 상황 = Entry의 실효 타이밍 + 굴림 위치(RestrictToRoll) + 결투 대 결투.
        SkillEffectResult result = DispatchProductionRoute(
            action,
            entry,
            effectContext.OpponentAction,
            fakeDamage,
            kill,
            observations);

        string after =
            CharacterVerificationScenarioTools.CaptureFingerprint(
                owner,
                target,
                key);

        if (result == null || !result.WasApplied)
        {
            failures.Add(
                $"{effect.name}: State={result?.State.ToString() ?? "NULL"}, " +
                $"Message={result?.Message}");
            return;
        }

        bool delegatedOlafBleed =
            effect is OlafNormalBleedEffect &&
            owner.GetMechanic<OlafMadnessMechanic>() != null;

        if (!delegatedOlafBleed &&
            string.Equals(before, after, StringComparison.Ordinal))
        {
            failures.Add(
                $"{effect.name}: Applied를 반환했지만 관찰 가능 상태가 변하지 않았습니다.");
            return;
        }

        observations.Add(
            $"Effect PASS: {effect.GetType().Name} / " +
            $"Timing={effect.Timing} / Target={result.Target?.Data?.CharacterName ?? "NULL"}");
    }

    // ------------------------------------------------------------------
    // 11-21 네이티브 카드 스윕 — 실전 디스패처로 타이밍 × 굴림을 훑는다.
    // 네이티브 그래프는 옛 Entry와 타이밍 · 굴림이 다를 수 있어서(엄호 · 버티기 등)
    // Entry 단위가 아니라 카드 단위로 본다. 연산마다 「한 번이라도 적용 + 상태 변화」면 통과.
    // ------------------------------------------------------------------
    private static readonly SkillEffectTiming[] SweepTimings =
    {
        SkillEffectTiming.OnExecute,
        SkillEffectTiming.OnPreparationActivated,
        SkillEffectTiming.OnAttackResolutionStarting,
        SkillEffectTiming.OnDuelMatched,
        SkillEffectTiming.OnExchangeWin,
        SkillEffectTiming.OnExchangeLose,
        SkillEffectTiming.AfterDamage,
        SkillEffectTiming.OnCritical,
        SkillEffectTiming.OnKill,
        SkillEffectTiming.OnPartWeakened,
        SkillEffectTiming.OnPartBroken,
        SkillEffectTiming.OnClashEnd
    };

    private static readonly System.Reflection.MethodInfo IsRegisteredNativeSourceMethod =
        typeof(Skill).Assembly.GetType("SkillEffectDispatcher")?.GetMethod(
            "IsRegisteredNativeSource",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);

    private static bool IsEventOwnedNativeRoute(SkillDefinition d) =>
        ProjectAbyss.Abilities.OlafNativeAbilityRuntimeBinding.IsNativeExplosionSource(d) ||
        ProjectAbyss.Abilities.OlafNativeAbilityRuntimeBinding.IsNativePrestigeSource(d);

    private static bool IsProductionNativeRoute(SkillDefinition d)
    {
        if (d == null)
            return false;
        if (ProjectAbyss.Abilities.OlafNativeAbilityRuntimeBinding.IsApprovedSource(d) ||
            ProjectAbyss.Abilities.YujinNativeAbilityRuntimeBinding.IsKnownSource(d) ||
            ProjectAbyss.Abilities.PreparationStatusAbilityBinding.IsAuthoredSource(d))
            return true;
        return IsRegisteredNativeSourceMethod?.Invoke(null, new object[] { d }) is bool b && b;
    }

    private sealed class OpTrace
    {
        public bool Applied;
        public bool Changed;
        public readonly SortedSet<string> States = new SortedSet<string>();
        public readonly SortedSet<string> Messages = new SortedSet<string>();
        public string AppliedAt;
        public string Fingerprint;
    }

    private static int SweepNativeRoute(
        CharacterVerificationContext verification,
        SkillDefinition definition,
        Skill skill,
        BodyPart ownerPart,
        BodyPart targetPart,
        ICollection<string> failures,
        ICollection<string> observations)
    {
        Character owner = verification.Character;
        Character target = verification.OpponentCharacter;
        var ops = new SortedDictionary<int, OpTrace>();
        int rolls = Mathf.Max(1, skill.ExchangeRollCount);
        long actionId = 960000;

        foreach (SkillEffectTiming timing in SweepTimings)
        {
            for (int roll = 1; roll <= rolls; roll++)
            for (int variant = 0; variant < 3; variant++)
            {
                CharacterVerificationScenarioTools.ResetCombatState(owner);
                CharacterVerificationScenarioTools.ResetCombatState(target);
                CharacterVerificationScenarioTools.FillResources(owner, definition, skill);
                PrepareNativePrerequisites(definition, owner, target, ownerPart, targetPart);

                BattleAction action = CharacterVerificationScenarioTools.CreateAction(
                    owner, ownerPart, skill, target, targetPart, actionId++);
                action.BeginResolutionSequence();
                action.RollPowerForExchange(roll - 1);

                BattleAction opponent = CreateOpponentAction(action);
                if (skill.ActionType == ActionType.Duel)
                {
                    action.SetDuelEffectsEligible(true);
                    opponent = CreateOpponentAction(action, ActionType.Duel) ?? opponent;
                    opponent?.SetDuelEffectsEligible(true);
                }

                // 피해 상황 3갈래 — 0 전부(처치 · 약화 · 파괴 · 흐트러짐 · 치명) / 1 약화만 / 2 파괴만.
                // 같은 타격에서 조건이 겹치면 앞 연산이 자원(표식 등)을 먼저 가져가므로 갈래를 나눈다.
                DamageContext damage = CreateFakeDamage(action);
                damage.WasWeakened = variant != 2;
                damage.WasBroken = variant != 1;
                damage.OpenedVulnerabilityWindow = variant == 0;
                damage.WasKilled = variant == 0;
                KillEventContext kill = KillEventContext.External(owner, target, action);
                bool won = timing != SkillEffectTiming.OnExchangeLose;

                // 실전 순서 — 교환 이후 타이밍은 같은 액션의 「사용시」가 먼저 돈다
                // (피의 맹세처럼 사용시에 준비한 구간을 승리시에 쓰는 카드).
                if (timing != SkillEffectTiming.OnExecute &&
                    timing != SkillEffectTiming.OnPreparationActivated)
                {
                    ExecuteDefinitionEffectsMethod?.Invoke(
                        skill,
                        new object[]
                        {
                            // 사용시는 행동 시작에 한 번 — 1굴림 위치로 돈다.
                            action, SkillEffectTiming.OnExecute, opponent, damage, kill,
                            null, 0, null, true, false, won, null
                        });
                }

                string before = CharacterVerificationScenarioTools.CaptureFingerprint(
                    owner, target, definition.CustomResourceKey);
                UnityEngine.Random.InitState(verification.Definition.Seed + (int)actionId);

                var results = ExecuteDefinitionEffectsMethod?.Invoke(
                    skill,
                    new object[]
                    {
                        action, timing, opponent, damage, kill,
                        null, roll - 1, null, true, false, won, null
                    }) as IReadOnlyList<SkillEffectResult>;

                string after = CharacterVerificationScenarioTools.CaptureFingerprint(
                    owner, target, definition.CustomResourceKey);
                bool changed = !string.Equals(before, after, StringComparison.Ordinal);

                if (results == null)
                    continue;
                for (int i = 0; i < results.Count; i++)
                {
                    if (!ops.TryGetValue(i, out OpTrace trace))
                        ops[i] = trace = new OpTrace();
                    SkillEffectResult r = results[i];
                    if (r == null)
                        continue;
                    if (r.State != SkillEffectResultState.NotScheduled)
                    {
                        trace.States.Add(r.State + "@" + timing + "/R" + roll + "/V" + variant);
                        if (!string.IsNullOrWhiteSpace(r.Message))
                            trace.Messages.Add(r.Message);
                    }
                    if (r.WasApplied)
                    {
                        trace.Applied = true;
                        trace.AppliedAt ??= timing + "/R" + roll;
                        if (!changed && trace.Fingerprint == null)
                            trace.Fingerprint = after;
                        if (changed)
                        {
                            trace.Changed = true;
                            trace.AppliedAt = timing + "/R" + roll;
                        }
                    }
                }
            }
        }

        CharacterVerificationScenarioTools.ResetCombatState(owner);
        CharacterVerificationScenarioTools.ResetCombatState(target);

        if (ops.Count == 0)
        {
            failures.Add("네이티브 경로가 어떤 타이밍에서도 결과를 돌려주지 않았습니다.");
            return 0;
        }

        foreach (var pair in ops)
        {
            OpTrace t = pair.Value;
            string label = "op#" + pair.Key;
            if (t.Applied && t.Changed)
                observations.Add($"Native PASS: {label} @ {t.AppliedAt}");
            else if (t.Applied && IsJudgmentOnlyOperation(definition, pair.Key))
                observations.Add($"Native PASS: {label} @ {t.AppliedAt} (판정 시점 위력 연산 — 상태 변화 없음이 정상)");
            else if (t.Applied)
            {
                failures.Add($"{label}: Applied({t.AppliedAt})였지만 관찰 가능 상태가 변하지 않았습니다.");
                observations.Add($"Fingerprint {label}: {t.Fingerprint}");
            }
            else
                failures.Add($"{label}: 어떤 상황에서도 적용되지 않음 — " +
                    (t.States.Count == 0 ? "모든 타이밍 NotScheduled" : string.Join(", ", t.States.Take(4))) +
                    (t.Messages.Count == 0 ? "" : " / " + string.Join(" | ", t.Messages)));
        }
        return ops.Count;
    }

    /// <summary>
    /// 카드별 선행 조건 — 네이티브 연산이 요구하는 상태를 만든다(11-21, 단계적으로 채운다).
    /// </summary>
    private static void PrepareNativePrerequisites(
        SkillDefinition definition,
        Character owner,
        Character target,
        BodyPart ownerPart,
        BodyPart targetPart)
    {
        if (definition == null || owner == null)
            return;

        // 캐릭터 밖 상태 초기화(ResetCombatState가 안 지우는 것).
        YujinMechanic yujin = owner.GetMechanic<YujinMechanic>();
        if (yujin != null)
        {
            ClearPrivateCollection(yujin, "marks");
            ClearPrivateCollection(yujin, "pendingMarks");
            CharacterVerificationScenarioTools.TrySetPrivateField(yujin, "sense", 0);
            CharacterVerificationScenarioTools.TrySetPrivateField(yujin, "weaponChangedThisTurn", false);
        }

        OlafRulebreakerMechanic rule = owner.GetMechanic<OlafRulebreakerMechanic>();
        OlafMadnessMechanic madness = owner.GetMechanic<OlafMadnessMechanic>();

        void LowHp()
        {
            if (owner.RuntimeStatus != null)
                owner.RuntimeStatus.currentHP = Mathf.Max(1, owner.MaxCombatHP * 6 / 10);
        }

        switch (definition.SkillId)
        {
            // ── 빌야르 ──
            case "olaf.preparation.calm_madness":       // 체력 70% 미만 · 광기 2 소모
                LowHp();
                madness?.AddMadness(3);
                break;
            case "olaf.preparation.devour_stagger":     // 체력 70% 미만 · 흐트러짐 20 소모
            case "olaf.preparation.blood_oath":         // 체력 70% 미만
                LowHp();
                break;
            case "olaf.preparation.erase_wound":        // 자기 출혈 소모
                rule?.AddBleeding(owner, ownerPart, 3);
                break;
            case "olaf.preparation.last_resistance":    // 지난 턴 짓눌림(하드 게이트)
                CharacterVerificationScenarioTools.TrySetPrivateField(rule, "infiniteAttackSpeedThisTurn", false);
                MomentumManager momentum = owner.BattleContext?.ResolveMomentumManager();
                CharacterVerificationScenarioTools.TrySetPrivateField(
                    momentum, "playerPreviousFinalState", MomentumState.LastStand);
                CharacterVerificationScenarioTools.TrySetPrivateField(
                    momentum, "enemyPreviousFinalState", MomentumState.LastStand);
                break;
            case "olaf.normal.first_strike":            // 턴 번호가 있어야 다음 턴 예약
                CharacterVerificationScenarioTools.TrySetPrivateField(rule, "currentTurn", 1);
                break;
            case "olaf.normal.aim_weakness":            // 2굴림 = 대상 출혈 5 이상
                rule?.AddBleeding(target, targetPart, 10);
                break;
            case "olaf.duel.blood_oath":                // 자기 출혈 전부 소모 → 구간(5~9 / 10+)
                rule?.AddBleeding(owner, ownerPart, 10);
                break;
            case "olaf.duel.fortify":                   // 대상 출혈 2씩 소모 · 3굴림 = 힘 · 신속 보유
                rule?.AddBleeding(target, targetPart, 4);
                owner.ApplyStatus(StatusEffectFactory.CreateCanonical0922(StatusEffectId.Strength, 1, 1),
                    owner, null, null, 0, SkillEffectTiming.OnExecute, false);
                owner.ApplyStatus(StatusEffectFactory.CreateCanonical0922(StatusEffectId.Swift, 1, 2),
                    owner, null, null, 0, SkillEffectTiming.OnExecute, false);
                if (owner.RuntimeStatus != null)
                    owner.RuntimeStatus.currentPrestige = 0;
                break;

            // ── 천명 ──
            case "yujin.normal.ace_in_the_hole":        // 감 소모
            case "yujin.duel.p.finish_it":
            case "yujin.duel.o.advance_timing":
            case "yujin.preparation.hold_breath":
                yujin?.GrantSense(10);
                break;
            case "yujin.normal.familiar_hand":          // 환형 바꾼 턴
                CharacterVerificationScenarioTools.TrySetPrivateField(yujin, "weaponChangedThisTurn", true);
                break;
            case "yujin.normal.ledger_cleanup":         // 대상 표식 소모 · 이전 · 함정
            case "yujin.duel.block_retreat":
            case "yujin.duel.cut_throat":
            case "yujin.duel.trap":
            case "yujin.duel.deadline":
                yujin?.GrantMark(target, targetPart, 25);
                break;
            case "yujin.normal.confirm_kill":           // 대상 지정 보유
                target?.ApplyStatus(new YujinDesignationStatus(1, 2),
                    owner, null, null, 0, SkillEffectTiming.OnExecute, false);
                break;
            case "yujin.preparation.wait_chance":       // 열광 레벨 1 이상
                FervorManager fervor = owner.BattleContext?.Services?.FervorManager;
                CharacterVerificationScenarioTools.TrySetPrivateField(
                    fervor, "<FervorLevel>k__BackingField", 1);
                break;
        }
    }

    private static void ClearPrivateCollection(object owner, string field)
    {
        var info = owner?.GetType().GetField(field,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        (info?.GetValue(owner) as System.Collections.IDictionary)?.Clear();
        (info?.GetValue(owner) as System.Collections.IList)?.Clear();
    }

    /// <summary>
    /// 판정 시점에 위력만 바꾸는 연산 — 자기 상태를 바꾸지 않는 것이 정상.
    /// 실제 효과는 굴림 판정(OlafRulebreakerMechanic) · ClashManager 연쇄에서 본다.
    /// </summary>
    private static bool IsJudgmentOnlyOperation(SkillDefinition d, int op) =>
        (d.SkillId == "olaf.normal.exploit_gap" && op == 0) ||
        (d.SkillId == "olaf.normal.aim_weakness" && op == 2) ||
        (d.SkillId == "olaf.duel.no_retreat" && op == 0);

    private static readonly System.Reflection.MethodInfo ExecuteDefinitionEffectsMethod =
        typeof(Skill).GetMethod(
            "ExecuteDefinitionEffects",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);

    private static SkillEffectResult DispatchProductionRoute(
        BattleAction action,
        SkillEffectEntry entry,
        BattleAction opponentAction,
        DamageContext damage,
        KillEventContext kill,
        ICollection<string> observations)
    {
        Skill skill = action?.Skill;
        if (skill == null || ExecuteDefinitionEffectsMethod == null)
            return null;

        SkillEffectTiming timing = entry.EffectiveTiming;
        int rollIndex = entry.RestrictToRoll
            ? Mathf.Max(1, entry.RollNumber) - 1
            : 0;
        bool won = timing != SkillEffectTiming.OnExchangeLose &&
                   timing != SkillEffectTiming.OnClashLose &&
                   timing != SkillEffectTiming.OnRollLose;

        if (skill.ActionType == ActionType.Duel)
        {
            action.SetDuelEffectsEligible(true);
            opponentAction = CreateOpponentAction(action, ActionType.Duel) ??
                             opponentAction;
            opponentAction?.SetDuelEffectsEligible(true);
        }

        var results = ExecuteDefinitionEffectsMethod.Invoke(
            skill,
            new object[]
            {
                action, timing, opponentAction, damage, kill,
                null, rollIndex, null, true, false, won, null
            }) as IReadOnlyList<SkillEffectResult>;

        if (results == null || results.Count == 0)
            return null;

        observations.Add(
            $"Route: {entry.Definition.name} / Timing={timing} / Roll={rollIndex + 1} / " +
            string.Join(",", results.Select(r => r?.State.ToString() ?? "NULL")));

        return results.FirstOrDefault(r => r != null && r.WasApplied) ??
               results.FirstOrDefault(r => r != null);
    }

    private static void PrepareEffectSpecificState(
        SkillEffectDefinition effect,
        SkillEffectOverrides overrides,
        SkillEffectContext context,
        BattleAction action)
    {
        Character owner = context?.Owner;
        Character target = context?.Target;

        if (effect is ApplyChinchiroSelfDamageEffect)
        {
            action.LastRollResult = new RollResult
            {
                ResolverType = SkillResolverType.Chinchiro,
                ChinchiroCombination = ChinchiroCombination.Hifumi,
                ChinchiroSelfDamage = 3,
                FinalPower = Mathf.Max(1, action.RolledPower)
            };
            action.HasRolled = true;
        }

        if (effect is GainPrestigeEffect gainPrestige)
        {
            Character receiver = gainPrestige.GiveToSelectedTarget
                ? target
                : owner;
            if (receiver?.RuntimeStatus != null)
                receiver.RuntimeStatus.currentPrestige = 0;
        }

        if (effect is GainCustomResourceEffect gainCustom)
        {
            Character receiver = gainCustom.GiveToSelectedTarget
                ? target
                : owner;
            string key = overrides?.ResolveResourceKey(gainCustom.ResourceKey) ??
                         gainCustom.ResourceKey;
            SkillResourceAccess.Set(receiver, key, 0);
        }

        if (effect is ModifyResourceEffect modify)
        {
            Character receiver = target ?? owner;
            int amount = overrides?.ResolveAmount(modify.Amount) ?? modify.Amount;

            if (modify.ResourceType == SkillResourceType.Prestige &&
                receiver?.RuntimeStatus != null)
            {
                receiver.RuntimeStatus.currentPrestige =
                    amount >= 0 ? 0 : Mathf.Max(10, -amount);
            }
            else if (receiver != null)
            {
                string key = overrides?.ResolveResourceKey(
                    modify.CustomResourceKey) ?? modify.CustomResourceKey;
                SkillResourceAccess.Set(
                    receiver,
                    key,
                    amount >= 0 ? 0 : Mathf.Max(10, -amount));
            }
        }

        if (effect is OlafPrestigeEffect && owner is Olaf olaf)
        {
            olaf.MadnessMechanic?.SetMadnessForDebug(5);

            if (target != null)
            {
                if (context.TargetPart != null)
                {
                    target.AddPartStatus(
                        context.TargetPart,
                        new Bleeding(3),
                        owner);
                }
                else
                {
                    target.AddStatus(
                        new Bleeding(3),
                        owner);
                }
            }
        }
    }

    private static DamageContext CreateFakeDamage(
        BattleAction action)
    {
        DamageRequest request = DamageRequest.Custom(
            action?.TargetPart == null
                ? DamageType.Direct
                : DamageType.SkillPart,
            action?.Owner,
            action?.Target,
            action?.TargetPart,
            999,
            1f,
            false,
            false,
            false,
            action);

        DamageContext context = new DamageContext(request)
        {
            WasCritical = true,
            AppliedDamage = 999,
            AppliedPartDamage = 999,
            FinalDamage = 999,
            WasApplied = true,
            WasKilled = true
        };

        return context;
    }

    private static BattleAction CreateOpponentAction(
        BattleAction action,
        ActionType? requiredType = null)
    {
        Skill opponentSkill = action?.Target?.RuntimeSkills?
            .FirstOrDefault(item => item != null &&
                (requiredType == null || item.ActionType == requiredType));
        if (opponentSkill == null && requiredType != null)
            return null;

        return CharacterVerificationScenarioTools.CreateAction(
            action?.Target,
            action?.TargetPart,
            opponentSkill,
            action?.Owner,
            action?.OwnerPart,
            actionId: 900002);
    }

    private static bool SeedConditions(
        SkillEffectDefinition effect,
        SkillEffectContext context,
        ref DamageContext damage,
        ref KillEventContext kill,
        ref int useCount,
        out string failure)
    {
        failure = null;

        if (effect?.Conditions == null)
            return true;

        foreach (SkillEffectCondition condition in effect.Conditions)
        {
            if (condition == null)
                continue;

            bool desiredRaw = !condition.Invert;

            switch (condition.Type)
            {
                case SkillEffectConditionType.Always:
                    if (!desiredRaw)
                    {
                        failure = "Invert된 Always 조건은 도달 불가능합니다.";
                        return false;
                    }
                    break;

                case SkillEffectConditionType.ClashWon:
                    if ((effect.Timing == SkillEffectTiming.OnClashWin) != desiredRaw)
                    {
                        failure = "ClashWon 조건과 Effect Timing 조합이 도달 불가능합니다.";
                        return false;
                    }
                    break;

                case SkillEffectConditionType.ClashLost:
                    if ((effect.Timing == SkillEffectTiming.OnClashLose) != desiredRaw)
                    {
                        failure = "ClashLost 조건과 Effect Timing 조합이 도달 불가능합니다.";
                        return false;
                    }
                    break;

                case SkillEffectConditionType.WasCritical:
                    damage.WasCritical = desiredRaw;
                    break;

                case SkillEffectConditionType.TargetHasStatus:
                    if (desiredRaw)
                    {
                        StatusEffect status = StatusEffectFactory.Create(
                            condition.StatusEffectId,
                            1,
                            3);

                        if (context.TargetPart != null &&
                            condition.CheckPartStatus)
                        {
                            context.Target.AddPartStatus(
                                context.TargetPart,
                                status,
                                context.Owner);
                        }
                        else
                        {
                            context.Target.AddStatus(
                                status,
                                context.Owner);
                        }
                    }
                    break;

                case SkillEffectConditionType.OwnerHasBrokenPart:
                    if (desiredRaw)
                    {
                        BodyPart part = condition.AnyOwnerPart
                            ? CharacterVerificationScenarioTools.GetUsablePart(context.Owner)
                            : context.Owner?.GetBodyPart(condition.OwnerPartType);
                        part?.SetDebugState(0f, 1f, false, true);
                    }
                    break;

                case SkillEffectConditionType.CustomResourceAtLeast:
                    SkillResourceAccess.Set(
                        context.Owner,
                        condition.ResourceKey,
                        desiredRaw
                            ? Mathf.Max(1, condition.Threshold)
                            : Mathf.Max(0, condition.Threshold - 1));
                    break;

                case SkillEffectConditionType.PrestigeAtLeast:
                    if (context.Owner?.RuntimeStatus != null)
                    {
                        context.Owner.RuntimeStatus.currentPrestige =
                            desiredRaw
                                ? Mathf.Max(1, condition.Threshold)
                                : Mathf.Max(0, condition.Threshold - 1);
                    }
                    break;

                case SkillEffectConditionType.FirstUseThisTurn:
                    useCount = desiredRaw ? 1 : 2;
                    break;

                case SkillEffectConditionType.DamageDealtAtLeast:
                    damage.AppliedDamage = desiredRaw
                        ? Mathf.Max(1, condition.Threshold)
                        : Mathf.Max(0, condition.Threshold - 1);
                    damage.AppliedPartDamage = damage.AppliedDamage;
                    damage.FinalDamage = damage.AppliedDamage;
                    break;

                case SkillEffectConditionType.TargetPartIsWeakened:
                    context.TargetPart?.SetDebugState(
                        1f,
                        Mathf.Max(2f, context.TargetPart.PartHP),
                        desiredRaw,
                        false);
                    break;

                case SkillEffectConditionType.TargetPartIsBroken:
                    context.TargetPart?.SetDebugState(
                        desiredRaw ? 0f : 10f,
                        10f,
                        false,
                        desiredRaw);
                    break;

                case SkillEffectConditionType.TargetHpRatioAtMost:
                    if (context.Target?.RuntimeStatus != null)
                    {
                        context.Target.RuntimeStatus.currentHP = desiredRaw
                            ? Mathf.Max(1, Mathf.FloorToInt(
                                context.Target.MaxCombatHP * condition.Ratio))
                            : context.Target.MaxCombatHP;
                    }
                    break;

                case SkillEffectConditionType.OwnerHpRatioAtMost:
                    if (context.Owner?.RuntimeStatus != null)
                    {
                        context.Owner.RuntimeStatus.currentHP = desiredRaw
                            ? Mathf.Max(1, Mathf.FloorToInt(
                                context.Owner.MaxCombatHP * condition.Ratio))
                            : context.Owner.MaxCombatHP;
                    }
                    break;

                case SkillEffectConditionType.KilledTarget:
                    kill = desiredRaw
                        ? KillEventContext.External(
                            context.Owner,
                            context.Target,
                            context.Action)
                        : null;
                    damage.WasKilled = desiredRaw;
                    break;

                default:
                    failure = $"미지원 조건: {condition.Type}";
                    return false;
            }
        }

        return true;
    }
}