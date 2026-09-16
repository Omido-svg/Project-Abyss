using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase C — Progression / Run gate.
/// C-34/C-35/C-36/C-37/C-39를 기존 Game System Verification 흐름에서 검증한다.
/// </summary>
public sealed class PhaseCGameSystemVerificationModule : IGameSystemVerificationModule
{
    public string ModuleId => "phase_c.progression_run";
    public int Order => 400;

    public IEnumerable<GameSystemVerificationCase> BuildCases()
    {
        yield return Static(
            "phasec.c34.fixed_emotion_offer",
            "C-34",
            "감정 증강 고정 3장",
            GameSystemVerificationCategory.Emotion,
            "같은 감정/티어는 Catalog 순서의 정확히 3장을 항상 동일하게 제안",
            VerifyFixedEmotionOffer);

        yield return Runtime(
            "phasec.c35.rulebreaker_runtime",
            "C-35",
            "감정 룰브레이커 공통 런타임",
            GameSystemVerificationCategory.Emotion,
            "슬롯 보존/추가·봉쇄, 부위 재생, 내성/죽음의 저항 덮어쓰기, 흐트러짐 제거, 부활 훅이 battle-scoped service로 동작",
            VerifyRulebreakerRuntime);

        yield return Static(
            "phasec.c35.rulebreaker_flow_hooks",
            "C-35",
            "기세 이월·해결 반복 공통 훅",
            GameSystemVerificationCategory.Emotion,
            "양/음 기세 이월과 player resolution repeat 요청을 데이터 효과가 공통 service로 예약/소비 가능",
            VerifyRulebreakerFlowHooks);

        yield return Static(
            "phasec.c36.second_upgrade_unset",
            "C-36",
            "2회차 강화 비용 Unset",
            GameSystemVerificationCategory.Upgrade,
            "1회 비용은 50/100/125/150, 2회 비용은 명시 CostOverride 없이는 quote 불가",
            VerifySecondUpgradeUnset);

        yield return Static(
            "phasec.c37.upgrade_payload",
            "C-37",
            "강화 payload 확장",
            GameSystemVerificationCategory.Upgrade,
            "Power + 비용 + UpgradeKey 효과 수치를 2단계 누적 데이터로 표현",
            VerifyUpgradePayload);

        yield return Static(
            "phasec.c39.run_economy_contract",
            "C-39",
            "런 경제 수식/상점 계약",
            GameSystemVerificationCategory.Economy,
            "n=75+25*(stage-1), 1.5n/2.5n, reroll x1.5, HP→Gold 미정=Unset",
            VerifyRunEconomyContract);

        yield return Runtime(
            "phasec.c39.run_maintenance_stage",
            "C-39",
            "정비·스테이지 회복 런타임",
            GameSystemVerificationCategory.Economy,
            "정비 비용이 실제 Gold를 소비하고 부위/HP를 복구하며 Stage clear가 HP full + stage advance",
            VerifyRunMaintenanceAndStage);
    }

    private static GameSystemVerificationCase Runtime(
        string id,
        string req,
        string name,
        GameSystemVerificationCategory category,
        string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> probe) =>
        new(
            id,
            req,
            name,
            category,
            GameSystemVerificationExecutionMode.IsolatedRuntime,
            expected,
            probe,
            true);

    private static GameSystemVerificationCase Static(
        string id,
        string req,
        string name,
        GameSystemVerificationCategory category,
        string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> probe) =>
        new(
            id,
            req,
            name,
            category,
            GameSystemVerificationExecutionMode.StaticContract,
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

    private static BodyPart FirstPart(Character character)
    {
        if (character?.BodyParts == null)
            return null;

        foreach (BodyPart part in character.BodyParts)
        {
            if (part != null)
                return part;
        }

        return null;
    }

    private static GameSystemVerificationProbeResult VerifyFixedEmotionOffer(
        GameSystemVerificationContext _)
    {
        EmotionAugmentCatalog catalog =
            ScriptableObject.CreateInstance<EmotionAugmentCatalog>();
        List<EmotionAugmentDefinition> created = new();

        try
        {
            for (int i = 0; i < 3; i++)
            {
                EmotionAugmentDefinition entry =
                    ScriptableObject.CreateInstance<EmotionAugmentDefinition>();
                entry.name = $"VERIFY_AWE_T1_{i}";
                entry.AugmentId = entry.name;
                entry.Emotion = EmotionType.Awe;
                entry.Tier = 1;
                entry.OfferWeight = i + 1;
                created.Add(entry);
                catalog.Entries.Add(entry);
            }

            List<EmotionAugmentDefinition> first = new();
            List<EmotionAugmentDefinition> second = new();

            bool firstOk =
                catalog.TryBuildCanonicalOffer(
                    EmotionType.Awe,
                    1,
                    first,
                    out string firstReason);

            bool secondOk =
                catalog.TryBuildCanonicalOffer(
                    EmotionType.Awe,
                    1,
                    second,
                    out string secondReason);

            bool same =
                firstOk &&
                secondOk &&
                first.Count == 3 &&
                second.Count == 3;

            for (int i = 0; same && i < 3; i++)
                same = ReferenceEquals(first[i], second[i]) &&
                       ReferenceEquals(first[i], created[i]);

            return same
                ? GameSystemVerificationProbeResult.Pass(
                    "Fixed order=0,1,2 / count=3 / OfferWeight ignored")
                : GameSystemVerificationProbeResult.Fail(
                    $"First={firstOk}:{firstReason}, Second={secondOk}:{secondReason}, Counts={first.Count}/{second.Count}");
        }
        finally
        {
            foreach (EmotionAugmentDefinition entry in created)
                DestroyVerificationObject(entry);
            DestroyVerificationObject(catalog);
        }
    }

    private static GameSystemVerificationProbeResult VerifyRulebreakerRuntime(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out GameSystemVerificationProbeResult fail))
            return fail;

        EmotionRulebreakerService service =
            f.Context?.Services?.EmotionRulebreakerService;

        BodyPart part = FirstPart(f.Player);
        if (service == null || part == null)
            return GameSystemVerificationProbeResult.Fail("RulebreakerService/PlayerPart missing");

        int baseSlots =
            f.Player.GetMaxActionSlotsForPart(part);

        service.AddPartSlots(
            f.Player,
            part.Type,
            1);

        int addedSlots =
            f.Player.GetMaxActionSlotsForPart(part);

        service.SetPartSlotsSuppressed(
            f.Player,
            part.Type,
            true);

        int suppressedSlots =
            f.Player.GetMaxActionSlotsForPart(part);

        service.SetPartSlotsSuppressed(
            f.Player,
            part.Type,
            false);

        service.GrantBrokenPartSlotPreservation(
            f.Player,
            1);

        bool preserved =
            service.TryPreserveSlotsOnBreak(
                f.Player,
                part);

        part.SetDebugState(
            0,
            Mathf.Max(1f, part.MaxPartHP),
            false,
            true);

        int preservedBrokenSlots =
            f.Player.GetMaxActionSlotsForPart(part);

        bool regenerated =
            service.RegenerateOneBrokenPartAsWeakened(
                f.Player,
                part.Type);

        service.SetHpResistanceOverride(
            f.Player,
            1f);

        bool resistance =
            service.TryGetHpResistanceOverride(
                f.Player,
                out float resistanceValue) &&
            Mathf.Approximately(resistanceValue, 1f);

        service.SetStaggerGaugeSuppressed(
            f.Player,
            true);

        bool staggerSuppressed =
            service.IsStaggerGaugeSuppressed(f.Player);

        service.ConfigureOverwhelmAscension(
            f.Player,
            requiredConsecutiveTurns: 2,
            enemyHpResistanceOverride: 2f);
        service.ObserveTurnFinalState(f.Player, MomentumState.Overwhelm);
        bool ascensionEarly =
            service.IsDeathResistanceDisabled(f.Enemy);
        service.ObserveTurnFinalState(f.Player, MomentumState.Overwhelm);

        bool ascensionActive =
            service.IsDeathResistanceDisabled(f.Enemy) &&
            service.TryGetHpResistanceOverride(
                f.Enemy,
                out float enemyResistance) &&
            Mathf.Approximately(enemyResistance, 2f);

        // Fixture enemy가 일반몹(single HP)일 수도 있으므로 실제 bypass damage path는
        // 확실히 부위를 가진 player clone에서 별도로 검증한다.
        service.SetDeathResistanceDisabled(f.Player, true);
        part.SetDebugState(
            2,
            Mathf.Max(2f, part.MaxPartHP),
            false,
            false);
        f.Player.TakeDamage(
            part,
            2,
            canBreakPart: false);
        bool deathResistanceDamage = part.IsBroken;

        service.ConfigureRevive(
            f.Player,
            1,
            0.30f,
            true);

        f.Player.RuntimeStatus.currentHP = 0;
        f.Player.CheckDead();

        bool revived =
            !f.Player.IsDead &&
            f.Player.CurrentHP > 0;

        bool ok =
            addedSlots == baseSlots + 1 &&
            suppressedSlots == 0 &&
            preserved &&
            preservedBrokenSlots > 0 &&
            regenerated &&
            part.IsWeakened &&
            resistance &&
            staggerSuppressed &&
            !ascensionEarly &&
            ascensionActive &&
            deathResistanceDamage &&
            revived;

        return ok
            ? GameSystemVerificationProbeResult.Pass(
                $"Slots={baseSlots}->{addedSlots}->0, PreservedBroken={preservedBrokenSlots}, Regen={part.State}, " +
                $"Resist={resistanceValue}, Ascension=DeathResOff+HPx2, RevivedHP={f.Player.CurrentHP}")
            : GameSystemVerificationProbeResult.Fail(
                $"Slots={baseSlots}/{addedSlots}/{suppressedSlots}, Preserved={preserved}/{preservedBrokenSlots}, " +
                $"Regen={regenerated}:{part.State}, Resist={resistance}:{resistanceValue}, Stagger={staggerSuppressed}, " +
                $"Ascension={ascensionEarly}/{ascensionActive}/{deathResistanceDamage}, Revived={revived}:{f.Player.CurrentHP}");
    }

    private static GameSystemVerificationProbeResult VerifyRulebreakerFlowHooks(
        GameSystemVerificationContext _)
    {
        BattleContext battleContext = new();
        EmotionRulebreakerService service =
            new EmotionRulebreakerService(battleContext);

        service.ConfigureMomentumCarry(
            carryPositive: true,
            carryNegative: false);

        service.CaptureMomentumForNextTurn(55);
        int positive = service.ConsumeCarriedMomentum();

        service.CaptureMomentumForNextTurn(-55);
        int negativeBlocked = service.ConsumeCarriedMomentum();

        service.ConfigureMomentumCarry(
            carryPositive: false,
            carryNegative: true);

        service.CaptureMomentumForNextTurn(-45);
        int negative = service.ConsumeCarriedMomentum();

        service.RequestPlayerResolutionRepeat(
            payEnergyAgain: true);

        bool repeat =
            service.TryConsumePlayerResolutionRepeat(
                out bool payAgain);

        bool empty =
            !service.TryConsumePlayerResolutionRepeat(
                out bool ignoredPayAgain);

        bool ok =
            positive == 55 &&
            negativeBlocked == 0 &&
            negative == -45 &&
            repeat &&
            payAgain &&
            empty;

        return ok
            ? GameSystemVerificationProbeResult.Pass(
                $"Carry=+{positive}/{negativeBlocked}/{negative}, Repeat={repeat}, PayAgain={payAgain}")
            : GameSystemVerificationProbeResult.Fail(
                $"Carry={positive}/{negativeBlocked}/{negative}, Repeat={repeat}, PayAgain={payAgain}, Empty={empty}");
    }

    private static GameSystemVerificationProbeResult VerifySecondUpgradeUnset(
        GameSystemVerificationContext _)
    {
        SkillDefinition definition =
            ScriptableObject.CreateInstance<SkillDefinition>();
        SkillUpgradeProfile profile =
            ScriptableObject.CreateInstance<SkillUpgradeProfile>();

        try
        {
            definition.name = "VERIFY_DUEL_UPGRADE";
            definition.ActionType = ActionType.Duel;
            definition.UpgradeProfile = profile;

            bool first =
                SkillUpgradeService.TryGetUpgradeCost(
                    definition,
                    1,
                    out int firstCost);

            bool secondUnset =
                !SkillUpgradeService.TryGetUpgradeCost(
                    definition,
                    2,
                    out int secondCost) &&
                secondCost == 0;

            profile.Upgrade2.CostOverride = 222;

            bool secondExplicit =
                SkillUpgradeService.TryGetUpgradeCost(
                    definition,
                    2,
                    out int explicitCost) &&
                explicitCost == 222;

            bool ok =
                first &&
                firstCost == 125 &&
                secondUnset &&
                secondExplicit;

            return ok
                ? GameSystemVerificationProbeResult.Pass(
                    $"L1={firstCost}, L2Unset={secondCost}, L2Explicit={explicitCost}")
                : GameSystemVerificationProbeResult.Fail(
                    $"L1={first}:{firstCost}, L2Unset={secondUnset}:{secondCost}, L2Explicit={secondExplicit}:{explicitCost}");
        }
        finally
        {
            DestroyVerificationObject(profile);
            DestroyVerificationObject(definition);
        }
    }

    private static GameSystemVerificationProbeResult VerifyUpgradePayload(
        GameSystemVerificationContext _)
    {
        SkillDefinition definition =
            ScriptableObject.CreateInstance<SkillDefinition>();
        SkillUpgradeProfile profile =
            ScriptableObject.CreateInstance<SkillUpgradeProfile>();

        try
        {
            definition.name = "VERIFY_PAYLOAD_SKILL";
            definition.ActionType = ActionType.Preparation;
            definition.UpgradeProfile = profile;

            profile.Upgrade1.BasePowerDelta = 1;
            profile.Upgrade1.EnergyCostDelta = 1;
            profile.Upgrade1.AllRollMinPowerDelta = 1;
            profile.Upgrade1.AllRollMaxPowerDelta = 1;
            profile.Upgrade1.PerRollPowerDelta.Add(2);
            profile.Upgrade1.EffectPayloads.Add(
                new SkillUpgradeEffectPayload
                {
                    TargetEffectKey = "core",
                    OverrideAmount = true,
                    Amount = 7
                });

            profile.Upgrade2.BasePowerDelta = 2;
            profile.Upgrade2.EnergyCostDelta = -1;
            profile.Upgrade2.EffectPayloads.Add(
                new SkillUpgradeEffectPayload
                {
                    TargetEffectKey = "core",
                    OverrideDuration = true,
                    Duration = 3
                });

            SkillUpgradeState state = new();
            state.TrySetLevel(definition, 2);

            SkillEffectOverrides resolved =
                SkillUpgradeService.ResolveEffectOverrides(
                    definition,
                    state,
                    "core",
                    new SkillEffectOverrides());

            SkillRollData roll =
                SkillUpgradeService.ResolveRollData(
                    definition,
                    state,
                    new SkillRollData
                    {
                        MinPower = 10,
                        MaxPower = 18
                    },
                    0);

            int basePower =
                SkillUpgradeService.GetCumulativeBasePowerDelta(
                    definition,
                    state);

            int energy =
                SkillUpgradeService.ResolveEnergyCost(
                    definition,
                    state,
                    1);

            bool ok =
                basePower == 3 &&
                energy == 1 &&
                resolved.OverrideAmount &&
                resolved.Amount == 7 &&
                resolved.OverrideDuration &&
                resolved.Duration == 3 &&
                roll.MinPower == 13 &&
                roll.MaxPower == 21;

            return ok
                ? GameSystemVerificationProbeResult.Pass(
                    $"BaseDelta={basePower}, Energy={energy}, Effect={resolved.Amount}/{resolved.Duration}, Roll={roll.MinPower}-{roll.MaxPower}")
                : GameSystemVerificationProbeResult.Fail(
                    $"BaseDelta={basePower}, Energy={energy}, Effect={resolved.OverrideAmount}:{resolved.Amount}/{resolved.OverrideDuration}:{resolved.Duration}, Roll={roll.MinPower}-{roll.MaxPower}");
        }
        finally
        {
            DestroyVerificationObject(profile);
            DestroyVerificationObject(definition);
        }
    }

    private static GameSystemVerificationProbeResult VerifyRunEconomyContract(
        GameSystemVerificationContext _)
    {
        RunEconomySettings settings = new();
        RunEconomyService service =
            new RunEconomyService(settings);

        ItemEconomySettings itemEconomy =
            new ItemEconomySettings();

        RunItemOfferService itemService =
            new RunItemOfferService(
                null,
                itemEconomy);

        int s1 = service.GetStageBaseGold(1);
        int s2 = service.GetStageBaseGold(2);
        int s3 = service.GetStageBaseGold(3);
        int elite =
            service.CalculateNodeGold(
                2,
                RunNodeRewardKind.EliteBattle,
                1f);
        int goldNode =
            service.CalculateNodeGold(
                2,
                RunNodeRewardKind.GoldNode,
                1f);

        float reroll1 =
            itemService.GetRerollCost(
                100f,
                1);
        float reroll2 =
            itemService.GetRerollCost(
                100f,
                2);

        bool hpRateUnset =
            !service.TryGetCurrentHpToGoldQuote(
                10,
                out int hpGold) &&
            hpGold == 0;

        RunProgressionState shopState = new();
        shopState.Reset(startingGold: 1000);
        RunItemDefinition item =
            ScriptableObject.CreateInstance<RunItemDefinition>();

        try
        {
            item.ItemId = "VERIFY_T1_ITEM";
            item.Tier = RunItemTier.Tier1;

            bool bought =
                service.TryBuyItem(
                    shopState,
                    item,
                    itemEconomy);
            int afterBuy = shopState.Gold;

            bool sold =
                service.TrySellItem(
                    shopState,
                    item,
                    itemEconomy);
            int afterSell = shopState.Gold;

            bool rerollPaid =
                service.TryPayGoldShopReroll(
                    shopState,
                    100,
                    1,
                    out int rerollPaidGold,
                    itemEconomy);

            bool ok =
                s1 == 75 &&
                s2 == 100 &&
                s3 == 125 &&
                elite == 150 &&
                goldNode == 250 &&
                Mathf.Approximately(reroll1, 150f) &&
                Mathf.Approximately(reroll2, 225f) &&
                hpRateUnset &&
                service.GetCurrentHpCostForMaximumHp(2) == 6 &&
                bought &&
                afterBuy == 600 &&
                sold &&
                afterSell == 732 &&
                rerollPaid &&
                rerollPaidGold == 150 &&
                shopState.Gold == 582;

            return ok
                ? GameSystemVerificationProbeResult.Pass(
                    $"n={s1}/{s2}/{s3}, Elite={elite}, GoldNode={goldNode}, " +
                    $"Shop=1000->{afterBuy}->{afterSell}->{shopState.Gold}, Reroll={rerollPaidGold}, HpGold=Unset")
                : GameSystemVerificationProbeResult.Fail(
                    $"n={s1}/{s2}/{s3}, Elite={elite}, GoldNode={goldNode}, " +
                    $"RerollFormula={reroll1}/{reroll2}, HpGold={hpGold}, " +
                    $"Shop={bought}/{sold}/{rerollPaid}:{afterBuy}/{afterSell}/{shopState.Gold}/{rerollPaidGold}");
        }
        finally
        {
            DestroyVerificationObject(item);
        }
    }

    private static GameSystemVerificationProbeResult VerifyRunMaintenanceAndStage(
        GameSystemVerificationContext context)
    {
        if (!Fixture(context, out GameSystemVerificationFixture f, out GameSystemVerificationProbeResult fail))
            return fail;

        BodyPart part = FirstPart(f.Player);
        if (part == null)
            return GameSystemVerificationProbeResult.Fail("Player part missing");

        RunProgressionState state = new();
        state.Reset(
            startingGold: 300,
            startingStage: 2);

        // Character.MaxCombatHP가 Whole-HP run bonus를 읽을 수 있게 같은 run state를 주입한다.
        f.Context.RunProgression = state;

        RunEconomyService service =
            new RunEconomyService();

        part.SetDebugState(
            1,
            Mathf.Max(2f, part.MaxPartHP),
            true,
            false);

        f.Player.RuntimeStatus.currentHP =
            Mathf.Max(
                1,
                f.Player.MaxCombatHP / 2);

        int beforeMaintenanceHp = f.Player.CurrentHP;
        bool maintained =
            service.TryMaintenanceRestore(
                state,
                f.Player,
                part);

        int afterMaintenanceGold = state.Gold;
        int afterMaintenanceHp = f.Player.CurrentHP;

        int beforeMax = f.Player.MaxCombatHP;
        int hpBeforeMaxTrade = f.Player.CurrentHP;
        bool maxTrade =
            service.TryExchangeCurrentHpForMaximumHp(
                state,
                f.Player,
                2);
        int hpAfterMaxTrade = f.Player.CurrentHP;
        int afterMax = f.Player.MaxCombatHP;

        service.CompleteStage(
            state,
            f.Player,
            advanceStage: true);

        bool ok =
            maintained &&
            part.State == BodyPartState.Normal &&
            afterMaintenanceGold == 225 &&
            afterMaintenanceHp > beforeMaintenanceHp &&
            maxTrade &&
            hpBeforeMaxTrade - hpAfterMaxTrade == 6 &&
            state.MaximumHpBonus == 2 &&
            afterMax == beforeMax + 2 &&
            f.Player.CurrentHP == f.Player.MaxCombatHP &&
            state.Stage == 3;

        return ok
            ? GameSystemVerificationProbeResult.Pass(
                $"Maintenance Gold=300->{afterMaintenanceGold}, HP={beforeMaintenanceHp}->{afterMaintenanceHp}, " +
                $"HealthShop MaxHP={beforeMax}->{afterMax}(-6 HP), Stage={state.Stage}, Full={f.Player.CurrentHP}")
            : GameSystemVerificationProbeResult.Fail(
                $"Maintenance={maintained}:{part.State}, Gold={afterMaintenanceGold}, " +
                $"HP={beforeMaintenanceHp}->{afterMaintenanceHp}, MaxTrade={maxTrade}:{hpBeforeMaxTrade}->{hpAfterMaxTrade}, " +
                $"Max={beforeMax}->{afterMax}, Bonus={state.MaximumHpBonus}, Full={f.Player.CurrentHP}/{f.Player.MaxCombatHP}, Stage={state.Stage}");
    }

    private static void DestroyVerificationObject(UnityEngine.Object value)
    {
        if (value == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(value);
        else
            UnityEngine.Object.DestroyImmediate(value);
    }
}
