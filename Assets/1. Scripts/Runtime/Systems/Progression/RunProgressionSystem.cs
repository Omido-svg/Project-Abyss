using System;
using UnityEngine;

public enum RunNodeRewardKind
{
    NormalBattle = 0,
    EliteBattle = 1,
    TurnLimitBattle = 2,
    GoldNode = 3
}

public enum RunShopKind
{
    Gold = 0,
    Health = 1
}

[Serializable]
public sealed class RunEconomySettings
{
    [Min(0)] public int Stage1BaseGold = 75;
    [Min(0)] public int StageGoldStep = 25;

    [Header("Maintenance")]
    [Tooltip("0916 §16.3: 75 gold = part-state recovery + 25% max-HP healing.")]
    [Min(0)] public int MaintenanceCost = 75;
    [Range(0f, 1f)] public float MaintenanceHealMaxHpRatio = 0.25f;

    [Header("Node reward")]
    [Min(0f)] public float EliteAndTurnLimitMultiplier = 1.5f;
    [Min(0f)] public float GoldNodeMultiplier = 2.5f;
    [Range(0f, 1f)] public float RewardVarianceMinimum = 0.9f;
    [Range(1f, 2f)] public float RewardVarianceMaximum = 1.1f;

    [Header("Health shop")]
    [Tooltip("0916 (미정). false일 때 현재 HP→Gold 환율은 Unset이며 거래할 수 없습니다.")]
    public bool HasCurrentHpToGoldRate;
    [Min(0f)] public float CurrentHpToGoldRate;
    [Tooltip("0916 확정: 최대 HP +1의 가격은 현재 HP 3.")]
    [Min(1)] public int CurrentHpCostPerMaximumHp = 3;

    public void Normalize()
    {
        Stage1BaseGold = Mathf.Max(0, Stage1BaseGold);
        StageGoldStep = Mathf.Max(0, StageGoldStep);
        MaintenanceCost = Mathf.Max(0, MaintenanceCost);
        MaintenanceHealMaxHpRatio = Mathf.Clamp01(MaintenanceHealMaxHpRatio);
        EliteAndTurnLimitMultiplier = Mathf.Max(0f, EliteAndTurnLimitMultiplier);
        GoldNodeMultiplier = Mathf.Max(0f, GoldNodeMultiplier);
        RewardVarianceMinimum = Mathf.Clamp(RewardVarianceMinimum, 0f, 1f);
        RewardVarianceMaximum = Mathf.Max(1f, RewardVarianceMaximum);
        if (RewardVarianceMaximum < RewardVarianceMinimum)
            RewardVarianceMaximum = RewardVarianceMinimum;

        if (!HasCurrentHpToGoldRate)
            CurrentHpToGoldRate = 0f;
        else
            CurrentHpToGoldRate = Mathf.Max(0f, CurrentHpToGoldRate);

        CurrentHpCostPerMaximumHp = Mathf.Max(1, CurrentHpCostPerMaximumHp);
    }
}


public enum HifumiEngraveBranch
{
    None = 0,
    Victory = 1,
    Defeat = 2
}

/// <summary>
/// 0922 §19.7 몸에 새기다의 런 영구 진행 상태.
/// 단계 상승 임계치는 아직 (미정)이므로 여기서는 진행 축/branch lock/현재 단계를
/// 저장하기만 하고, 임계치를 임의로 발명하지 않는다.
/// </summary>
[Serializable]
public sealed class HifumiEngraveRunProgression
{
    public int Stage { get; private set; } = 1;
    public HifumiEngraveBranch Branch { get; private set; } = HifumiEngraveBranch.None;
    public int VictoryProgress { get; private set; }
    public int DefeatProgress { get; private set; }

    public void Reset()
    {
        Stage = 1;
        Branch = HifumiEngraveBranch.None;
        VictoryProgress = 0;
        DefeatProgress = 0;
    }

    public void RecordExchangeOutcome(bool won)
    {
        if (Branch == HifumiEngraveBranch.Victory)
        {
            if (won) VictoryProgress++;
            return;
        }

        if (Branch == HifumiEngraveBranch.Defeat)
        {
            if (!won) DefeatProgress++;
            return;
        }

        if (won) VictoryProgress++;
        else DefeatProgress++;
    }

    /// <summary>
    /// 향후 canonical threshold data가 들어온 뒤 호출하는 승격 API.
    /// threshold <= 0은 미정 sentinel이며 절대 자동 승격하지 않는다.
    /// 첫 threshold 도달 축에서 branch가 lock된다.
    /// </summary>
    public bool TryAdvance(int victoryThreshold, int defeatThreshold)
    {
        if (Stage >= 4)
            return false;

        bool victoryReady = victoryThreshold > 0 && VictoryProgress >= victoryThreshold;
        bool defeatReady = defeatThreshold > 0 && DefeatProgress >= defeatThreshold;

        if (!victoryReady && !defeatReady)
            return false;

        if (Branch == HifumiEngraveBranch.None)
        {
            // 동시 도달은 기획상 발생 순서를 런타임이 추적해야 하지만 현재 threshold 자체가
            // 미정이다. 호출자가 먼저 도달한 축만 ready로 넘기는 계약으로 둔다.
            if (victoryReady == defeatReady)
                return false;

            Branch = victoryReady
                ? HifumiEngraveBranch.Victory
                : HifumiEngraveBranch.Defeat;
        }

        bool lockedAxisReady = Branch == HifumiEngraveBranch.Victory
            ? victoryReady
            : defeatReady;

        if (!lockedAxisReady)
            return false;

        Stage = Mathf.Clamp(Stage + 1, 1, 4);
        VictoryProgress = 0;
        DefeatProgress = 0;
        return true;
    }

    public void SetForVerification(
        int stage,
        HifumiEngraveBranch branch,
        int victoryProgress = 0,
        int defeatProgress = 0)
    {
        Stage = Mathf.Clamp(stage, 1, 4);
        Branch = branch;
        VictoryProgress = Mathf.Max(0, victoryProgress);
        DefeatProgress = Mathf.Max(0, defeatProgress);
    }
}

/// <summary>
/// Run-scoped mutable progression. BattleContext receives the same SkillUpgradeState instance,
/// so purchased upgrades affect runtime skills without copying max-level values into base assets.
/// </summary>
public sealed class RunProgressionState
{
    public int Stage { get; private set; } = 1;
    public int Gold { get; private set; }

    /// <summary>
    /// Whole-HP-only run bonus. Part Max HP is intentionally untouched.
    /// This is also the correct storage axis for effects such as §14 초연 "처치 성장".
    /// </summary>
    public int MaximumHpBonus { get; private set; }

    public SkillUpgradeState SkillUpgrades { get; } = new();
    public RunInventory Inventory { get; } = new();
    public HifumiEngraveRunProgression HifumiEngrave { get; } = new();

    public void Reset(int startingGold = 0, int startingStage = 1)
    {
        Stage = Mathf.Max(1, startingStage);
        Gold = Mathf.Max(0, startingGold);
        MaximumHpBonus = 0;
        SkillUpgrades.Clear();
        Inventory.Clear();
        HifumiEngrave.Reset();
    }

    public void SetStage(int stage) =>
        Stage = Mathf.Max(1, stage);

    public void AdvanceStage() =>
        Stage = Mathf.Max(1, Stage + 1);

    public void AddGold(int amount)
    {
        if (amount <= 0)
            return;
        Gold += amount;
    }

    public bool TrySpendGold(int amount)
    {
        int cost = Mathf.Max(0, amount);
        if (Gold < cost)
            return false;
        Gold -= cost;
        return true;
    }

    public void AddMaximumHpBonus(int amount)
    {
        if (amount <= 0)
            return;
        MaximumHpBonus += amount;
    }
}

/// <summary>
/// C-39 run economy contract. UI/node graph calls this service; it does not depend on a scene.
/// Actual item catalog/content remains Phase E (C-33).
/// </summary>
public sealed class RunEconomyService
{
    private readonly RunEconomySettings settings;

    public RunEconomySettings Settings => settings;

    public RunEconomyService(RunEconomySettings settings = null)
    {
        this.settings = settings ?? new RunEconomySettings();
        this.settings.Normalize();
    }

    public int GetStageBaseGold(int stage) =>
        Mathf.Max(0, settings.Stage1BaseGold +
            settings.StageGoldStep * (Mathf.Max(1, stage) - 1));

    public int CalculateNodeGold(
        int stage,
        RunNodeRewardKind node,
        float variance = 1f)
    {
        float n = GetStageBaseGold(stage);
        float multiplier = node switch
        {
            RunNodeRewardKind.NormalBattle => 1f,
            RunNodeRewardKind.EliteBattle => settings.EliteAndTurnLimitMultiplier,
            RunNodeRewardKind.TurnLimitBattle => settings.EliteAndTurnLimitMultiplier,
            RunNodeRewardKind.GoldNode => settings.GoldNodeMultiplier,
            _ => 1f
        };

        bool usesVariance =
            node == RunNodeRewardKind.EliteBattle ||
            node == RunNodeRewardKind.TurnLimitBattle ||
            node == RunNodeRewardKind.GoldNode;

        float resolvedVariance = usesVariance
            ? Mathf.Clamp(
                variance,
                settings.RewardVarianceMinimum,
                settings.RewardVarianceMaximum)
            : 1f;

        return Mathf.Max(0, Mathf.FloorToInt(n * multiplier * resolvedVariance));
    }

    public int RollNodeGold(int stage, RunNodeRewardKind node)
    {
        float variance =
            node == RunNodeRewardKind.NormalBattle
                ? 1f
                : UnityEngine.Random.Range(
                    settings.RewardVarianceMinimum,
                    settings.RewardVarianceMaximum);

        return CalculateNodeGold(stage, node, variance);
    }

    public int GrantNodeGold(
        RunProgressionState state,
        RunNodeRewardKind node,
        float? deterministicVariance = null)
    {
        if (state == null)
            return 0;

        int amount = deterministicVariance.HasValue
            ? CalculateNodeGold(state.Stage, node, deterministicVariance.Value)
            : RollNodeGold(state.Stage, node);

        state.AddGold(amount);
        return amount;
    }

    /// <summary>
    /// 0916 정비 75골드 패키지: 선택한 약화/파괴 부위의 상태 회복 + Whole HP 25% 회복.
    /// 회복 가능한 부위가 없는 경우 part는 null이어도 되며 HP 회복만 수행한다.
    /// </summary>
    public bool TryMaintenanceRestore(
        RunProgressionState state,
        Character character,
        BodyPart part = null)
    {
        if (state == null || character == null)
            return false;

        if (part != null &&
            !part.IsBroken &&
            !part.IsWeakened)
        {
            return false;
        }

        int cost = settings.MaintenanceCost;
        if (!state.TrySpendGold(cost))
            return false;

        if (part != null &&
            !character.RecoverPartAtMaintenance(part))
        {
            state.AddGold(cost);
            return false;
        }

        int heal = Mathf.Max(
            1,
            Mathf.FloorToInt(
                character.MaxCombatHP *
                settings.MaintenanceHealMaxHpRatio));

        character.RestoreCurrentHP(heal);
        return true;
    }

    public bool TryMaintenanceUpgradeSkill(
        RunProgressionState state,
        SkillDefinition definition,
        int maximumLevel = SkillUpgradeService.DefaultMaximumLevel) =>
        SkillUpgradeService.TryPurchaseUpgrade(
            state,
            definition,
            maximumLevel);

    // ---------------- Gold shop ----------------

    public bool TryBuyItem(
        RunProgressionState state,
        RunItemDefinition item,
        ItemEconomySettings itemEconomy = null)
    {
        if (state == null || item == null)
            return false;

        ItemEconomySettings economy = itemEconomy ?? new ItemEconomySettings();
        int price = Mathf.Max(0, economy.GetPrice(item.Tier));

        if (!state.TrySpendGold(price))
            return false;

        if (state.Inventory.Acquire(item))
            return true;

        state.AddGold(price);
        return false;
    }

    public bool TrySellItem(
        RunProgressionState state,
        RunItemDefinition item,
        ItemEconomySettings itemEconomy = null)
    {
        if (state == null || item == null)
            return false;

        if (!state.Inventory.Sell(item))
            return false;

        ItemEconomySettings economy = itemEconomy ?? new ItemEconomySettings();
        state.AddGold(economy.GetSellPrice(item));
        return true;
    }

    /// <summary>
    /// 리롤의 최초 비용 자체는 별도 run/shop data가 정한다. 여기서는 확정된 x1.5 누적만 결제한다.
    /// rerollCount=0은 baseCost, 1은 x1.5, 2는 x2.25...
    /// </summary>
    public bool TryPayGoldShopReroll(
        RunProgressionState state,
        int baseCost,
        int rerollCount,
        out int paidGold,
        ItemEconomySettings itemEconomy = null)
    {
        paidGold = 0;
        if (state == null || baseCost < 0)
            return false;

        ItemEconomySettings economy = itemEconomy ?? new ItemEconomySettings();
        float multiplier = Mathf.Pow(
            Mathf.Max(1f, economy.RerollCostMultiplier),
            Mathf.Max(0, rerollCount));

        int cost = Mathf.Max(0, Mathf.FloorToInt(baseCost * multiplier));
        if (!state.TrySpendGold(cost))
            return false;

        paidGold = cost;
        return true;
    }

    // ---------------- Health shop ----------------

    public bool TryGetCurrentHpToGoldQuote(
        int hp,
        out int gold)
    {
        gold = 0;
        if (!settings.HasCurrentHpToGoldRate ||
            settings.CurrentHpToGoldRate <= 0f ||
            hp <= 0)
        {
            return false;
        }

        gold = Mathf.Max(
            0,
            Mathf.FloorToInt(hp * settings.CurrentHpToGoldRate));
        return true;
    }

    /// <summary>
    /// (미정) 환율이 명시된 RunEconomySettings에서만 활성화된다.
    /// 상점 거래로 스스로 사망하지 않도록 현재 HP는 최소 1을 남긴다.
    /// </summary>
    public bool TryExchangeCurrentHpForGold(
        RunProgressionState state,
        Character character,
        int hp)
    {
        if (state == null || character?.RuntimeStatus == null || hp <= 0)
            return false;

        if (!TryGetCurrentHpToGoldQuote(hp, out int gold) ||
            character.CurrentHP - hp < 1)
        {
            return false;
        }

        character.RuntimeStatus.currentHP -= hp;
        state.AddGold(gold);
        return true;
    }

    public int GetCurrentHpCostForMaximumHp(int maximumHpAmount) =>
        Mathf.Max(0, maximumHpAmount) *
        settings.CurrentHpCostPerMaximumHp;

    /// <summary>
    /// 0916 확정 health-shop 환율: Max HP +1당 Current HP 3.
    /// 증가분은 Whole Max HP에만 적용하고 부위 Max HP는 건드리지 않는다.
    /// </summary>
    public bool TryExchangeCurrentHpForMaximumHp(
        RunProgressionState state,
        Character character,
        int maximumHpAmount)
    {
        if (state == null || character?.RuntimeStatus == null || maximumHpAmount <= 0)
            return false;

        int hpCost = GetCurrentHpCostForMaximumHp(maximumHpAmount);
        if (hpCost <= 0 || character.CurrentHP - hpCost < 1)
            return false;

        character.RuntimeStatus.currentHP -= hpCost;
        state.AddMaximumHpBonus(maximumHpAmount);
        return true;
    }

    public void CompleteStage(
        RunProgressionState state,
        Character character,
        bool advanceStage = true)
    {
        character?.RestoreAllRunHealthPreservePartStates();
        if (advanceStage)
            state?.AdvanceStage();
    }
}
