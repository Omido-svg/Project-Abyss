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
            typeof(OlafPrestigeEffect)
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

        if (owner is Yujin yujin &&
            YujinMechanic.TryGetHwanhyeongWeapon(
                skillId,
                out YujinWeaponType targetWeapon))
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

        if (!skill.TryConsumeResource(owner, action))
        {
            failures.Add(
                "충분한 자원 상태에서 TryConsumeResource가 실패했습니다.");
            return;
        }

        int expectedEnergy =
            Mathf.Max(0, energyBefore - skill.EnergyCost);

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

        SkillEffectResult result = entry.TryApply(
            effectContext,
            effect.Timing);

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
        BattleAction action)
    {
        Skill opponentSkill = action?.Target?.RuntimeSkills?
            .FirstOrDefault(item => item != null);

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