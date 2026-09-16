using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunFlowTestPhase
{
    CharacterSelect = 0,
    EmotionSelect = 1,
    NodeMap = 2,
    NodeDetail = 3,
    RunComplete = 4
}

public enum RunFlowTestNodeType
{
    NormalBattle = 0,
    EliteBattle = 1,
    TurnLimitBattle = 2,
    GoldNode = 3,
    Encounter = 4,
    MysteryLegacyRouter = 5,
    GoldShop = 6,
    HealthShop = 7,
    Maintenance = 8,
    BossBattle = 9
}

public enum RunFlowTestPartState
{
    Normal = 0,
    Weakened = 1,
    Broken = 2
}

[Serializable]
public sealed class RunFlowTestPart
{
    public PartType Type;
    public string Name;
    public RunFlowTestPartState State;

    // Live Battle에서 왕복한 정확한 부위 HP 스냅샷.
    // 첫 전투 전에는 HasExactHp=false로 두어 기존 상태 기반 fixture와 호환한다.
    public float CurrentHp;
    public float MaximumHp;
    public bool HasExactHp;

    public RunFlowTestPart(PartType type, string name)
    {
        Type = type;
        Name = name;
        State = RunFlowTestPartState.Normal;
        CurrentHp = 0f;
        MaximumHp = 0f;
        HasExactHp = false;
    }

    public void SetSnapshot(
        RunFlowTestPartState state,
        float currentHp,
        float maximumHp)
    {
        State = state;
        MaximumHp = Mathf.Max(1f, maximumHp);
        CurrentHp = Mathf.Clamp(currentHp, 0f, MaximumHp);
        HasExactHp = true;
    }

    public void ClearExactHp()
    {
        CurrentHp = 0f;
        MaximumHp = 0f;
        HasExactHp = false;
    }
}

/// <summary>
/// RunFlowTestScene 전용 전투 외부 HP/부위 proxy.
/// 실제 Character를 대체하려는 생산 코드가 아니라, 아직 Run Scene이 없는 동안
/// 정비/체력상점/스테이지 전회복 흐름을 검증하기 위한 테스트 상태다.
/// </summary>
public sealed class RunFlowTestAvatarState
{
    public const int CanonicalBaseMaximumHp = 600;

    private readonly RunProgressionState progression;

    public int CurrentHp { get; private set; }
    public int MaximumHp => CanonicalBaseMaximumHp + (progression?.MaximumHpBonus ?? 0);
    public IReadOnlyList<RunFlowTestPart> Parts => parts;

    private readonly List<RunFlowTestPart> parts = new()
    {
        new RunFlowTestPart(PartType.HEAD, "머리"),
        new RunFlowTestPart(PartType.LEFT_HAND, "왼팔"),
        new RunFlowTestPart(PartType.RIGHT_HAND, "오른팔"),
        new RunFlowTestPart(PartType.LEGS, "다리")
    };

    public RunFlowTestAvatarState(RunProgressionState progression)
    {
        this.progression = progression;
        Reset();
    }

    public void Reset()
    {
        CurrentHp = MaximumHp;
        for (int i = 0; i < parts.Count; i++)
        {
            parts[i].State = RunFlowTestPartState.Normal;
            parts[i].ClearExactHp();
        }
    }

    public void FullHeal()
    {
        CurrentHp = MaximumHp;

        // 스테이지 전회복은 HP만 회복한다. 약화/파괴 상태는 별도 규칙이다.
        // 파괴 부위는 회복 대상이 아니므로 0을 유지한다.
        for (int i = 0; i < parts.Count; i++)
        {
            RunFlowTestPart part = parts[i];
            if (!part.HasExactHp)
                continue;

            if (part.State == RunFlowTestPartState.Broken)
                part.CurrentHp = 0f;
            else
                part.CurrentHp = Mathf.Max(1f, part.MaximumHp);
        }
    }

    public void SetCurrentHpFromBattle(int currentHp)
    {
        CurrentHp = Mathf.Clamp(currentHp, 1, MaximumHp);
    }

    public void Damage(int amount)
    {
        CurrentHp = Mathf.Clamp(CurrentHp - Mathf.Max(0, amount), 1, MaximumHp);
    }

    public void Heal(int amount)
    {
        CurrentHp = Mathf.Clamp(CurrentHp + Mathf.Max(0, amount), 1, MaximumHp);
    }

    public void SetPartState(int index, RunFlowTestPartState state)
    {
        if (index < 0 || index >= parts.Count)
            return;
        parts[index].State = state;
    }

    public void SetPartSnapshot(
        int index,
        RunFlowTestPartState state,
        float currentHp,
        float maximumHp)
    {
        if (index < 0 || index >= parts.Count)
            return;

        parts[index].SetSnapshot(state, currentHp, maximumHp);
    }

    public void SetPartSnapshot(
        PartType type,
        RunFlowTestPartState state,
        float currentHp,
        float maximumHp)
    {
        RunFlowTestPart part = GetPart(type);
        part?.SetSnapshot(state, currentHp, maximumHp);
    }

    public RunFlowTestPart GetPart(PartType type)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] != null && parts[i].Type == type)
                return parts[i];
        }
        return null;
    }

    public bool TryMaintenanceRestore(
        RunProgressionState runState,
        RunEconomySettings settings,
        int partIndex,
        out string message)
    {
        message = string.Empty;
        if (runState == null || settings == null)
        {
            message = "Run state/settings가 없습니다.";
            return false;
        }

        if (partIndex < 0 || partIndex >= parts.Count)
        {
            message = "부위를 선택하세요.";
            return false;
        }

        RunFlowTestPart part = parts[partIndex];
        if (part.State == RunFlowTestPartState.Normal)
        {
            message = "정상 부위는 정비 상태 회복 대상이 아닙니다.";
            return false;
        }

        int cost = Mathf.Max(0, settings.MaintenanceCost);
        if (!runState.TrySpendGold(cost))
        {
            message = $"골드 부족: 필요 {cost}G";
            return false;
        }

        part.State = RunFlowTestPartState.Normal;
        // 정비는 구조 상태 자체를 복구하므로 이전 전투의 exact-part HP 스냅샷을
        // 그대로 재사용하지 않는다. 다음 Battle 진입은 정상 부위 기본 HP 규칙을 사용한다.
        part.ClearExactHp();
        int heal = Mathf.Max(1, Mathf.FloorToInt(MaximumHp * settings.MaintenanceHealMaxHpRatio));
        Heal(heal);
        message = $"정비 완료: {part.Name} 상태 회복 + HP {heal} 회복 / -{cost}G";
        return true;
    }

    public bool TryBuyMaximumHp(
        RunProgressionState runState,
        RunEconomyService economy,
        int maximumHpAmount,
        out string message)
    {
        message = string.Empty;
        if (runState == null || economy == null || maximumHpAmount <= 0)
        {
            message = "잘못된 요청입니다.";
            return false;
        }

        int hpCost = economy.GetCurrentHpCostForMaximumHp(maximumHpAmount);
        if (hpCost <= 0 || CurrentHp - hpCost < 1)
        {
            message = $"현재 HP가 부족합니다. MaxHP +{maximumHpAmount} 비용 = 현재 HP {hpCost}";
            return false;
        }

        CurrentHp -= hpCost;
        runState.AddMaximumHpBonus(maximumHpAmount);
        message = $"MaxHP +{maximumHpAmount}, 현재 HP -{hpCost}";
        return true;
    }

    public bool TryExchangeCurrentHpForGoldTestOnly(
        RunProgressionState runState,
        RunEconomyService economy,
        int hp,
        out string message)
    {
        message = string.Empty;
        if (runState == null || economy == null || hp <= 0)
        {
            message = "잘못된 요청입니다.";
            return false;
        }

        if (!economy.TryGetCurrentHpToGoldQuote(hp, out int gold))
        {
            message = "현재HP→골드 환율은 0916에서 (미정)입니다. TEST RATE를 먼저 켜세요.";
            return false;
        }

        if (CurrentHp - hp < 1)
        {
            message = "거래 후 HP 1 이상을 남겨야 합니다.";
            return false;
        }

        CurrentHp -= hp;
        runState.AddGold(gold);
        message = $"TEST ONLY: 현재 HP -{hp} → +{gold}G";
        return true;
    }
}

public sealed class RunFlowTestSession : IDisposable
{
    public RunFlowTestPhase Phase { get; private set; } = RunFlowTestPhase.CharacterSelect;
    public RunFlowTestNodeType? ActiveNode { get; private set; }

    public string SelectedCharacterKey { get; private set; } = string.Empty;
    public EmotionType? SelectedEmotion { get; private set; }

    public RunProgressionState Progression { get; } = new();
    public RunEconomySettings EconomySettings { get; } = new();
    public ItemEconomySettings ItemEconomy { get; } = new();
    public RunEconomyService Economy { get; private set; }
    public RunFlowTestAvatarState Avatar { get; private set; }

    public float RewardVariance { get; set; } = 1f;
    public int TestRerollBaseCost { get; set; } = 50;
    public int GoldShopRerollCount { get; private set; }
    public int MockSkillCount { get; private set; }
    public int MockEliteBonusRewardCount { get; private set; }
    public bool RunCompleted { get; private set; }

    public IReadOnlyList<RunItemDefinition> CurrentShopOffer => currentShopOffer;
    public IReadOnlyList<RunItemDefinition> CurrentBossOffer => currentBossOffer;
    public IReadOnlyList<string> History => history;
    public IReadOnlyList<SkillDefinition> MockUpgradeSkills => mockUpgradeSkills;

    private readonly List<RunItemDefinition> transientItems = new();
    private readonly List<RunItemDefinition> currentShopOffer = new();
    private readonly List<RunItemDefinition> currentBossOffer = new();
    private readonly List<string> history = new();
    private readonly List<SkillDefinition> mockUpgradeSkills = new();

    private RunItemCatalog transientCatalog;
    private RunItemOfferService itemOfferService;

    public RunFlowTestSession()
    {
        EconomySettings.Normalize();
        Economy = new RunEconomyService(EconomySettings);
        Avatar = new RunFlowTestAvatarState(Progression);
        BuildTransientItemCatalog();
        BuildTransientUpgradeSkills();
        ResetAll();
    }

    public void Dispose()
    {
        for (int i = 0; i < transientItems.Count; i++)
        {
            if (transientItems[i] != null)
                UnityEngine.Object.Destroy(transientItems[i]);
        }

        if (transientCatalog != null)
            UnityEngine.Object.Destroy(transientCatalog);

        for (int i = 0; i < mockUpgradeSkills.Count; i++)
        {
            if (mockUpgradeSkills[i] != null)
                UnityEngine.Object.Destroy(mockUpgradeSkills[i]);
        }
    }

    public void ResetAll()
    {
        Progression.Reset(0, 1);
        Avatar.Reset();
        SelectedCharacterKey = string.Empty;
        SelectedEmotion = null;
        RewardVariance = 1f;
        TestRerollBaseCost = 50;
        GoldShopRerollCount = 0;
        MockSkillCount = 0;
        MockEliteBonusRewardCount = 0;
        RunCompleted = false;
        ActiveNode = null;
        currentShopOffer.Clear();
        currentBossOffer.Clear();
        history.Clear();
        Phase = RunFlowTestPhase.CharacterSelect;
        EconomySettings.HasCurrentHpToGoldRate = false;
        EconomySettings.CurrentHpToGoldRate = 0f;
        EconomySettings.Normalize();
        RefreshTransientClassKeys();
        Log("새 테스트 런 시작: Stage 1 / Gold 0 / HP Full");
    }

    public void SelectCharacter(string characterKey)
    {
        if (string.IsNullOrWhiteSpace(characterKey))
            return;

        SelectedCharacterKey = characterKey.Trim();
        RefreshTransientClassKeys();
        Phase = RunFlowTestPhase.EmotionSelect;
        Log($"캐릭터 선택: {SelectedCharacterKey}");
    }

    public void SelectEmotion(EmotionType emotion)
    {
        SelectedEmotion = emotion;
        Phase = RunFlowTestPhase.NodeMap;
        Log($"감정 선택: {GetEmotionDisplayName(emotion)}");
    }

    public void SetStage(int stage)
    {
        Progression.SetStage(Mathf.Clamp(stage, 1, 3));
        Log($"DEBUG Stage 변경: {Progression.Stage}");
    }

    public void EnterNode(RunFlowTestNodeType node)
    {
        ActiveNode = node;
        Phase = RunFlowTestPhase.NodeDetail;
        Log($"노드 진입: {GetNodeDisplayName(node)}");

        if (node == RunFlowTestNodeType.GoldShop)
            RefreshGoldShopOffer();
        else if (node == RunFlowTestNodeType.BossBattle)
            currentBossOffer.Clear();
    }

    public void BackToNodeMap()
    {
        ActiveNode = null;
        Phase = RunCompleted ? RunFlowTestPhase.RunComplete : RunFlowTestPhase.NodeMap;
    }

    public int GrantBattleReward(RunFlowTestNodeType node)
    {
        RunNodeRewardKind rewardKind = node switch
        {
            RunFlowTestNodeType.NormalBattle => RunNodeRewardKind.NormalBattle,
            RunFlowTestNodeType.EliteBattle => RunNodeRewardKind.EliteBattle,
            RunFlowTestNodeType.TurnLimitBattle => RunNodeRewardKind.TurnLimitBattle,
            _ => RunNodeRewardKind.NormalBattle
        };

        int amount = Economy.GrantNodeGold(Progression, rewardKind, RewardVariance);
        Log($"{GetNodeDisplayName(node)} 승리 보상: +{amount}G");
        return amount;
    }

    public int ResolveGoldNode()
    {
        int amount = Economy.GrantNodeGold(Progression, RunNodeRewardKind.GoldNode, RewardVariance);
        Log($"골드 노드 해결: +{amount}G");
        BackToNodeMap();
        return amount;
    }

    public RunItemDefinition GrantEliteMockItem()
    {
        List<RunItemDefinition> offer = itemOfferService.CreateOffer(
            RunItemOfferSource.EliteRandom,
            Progression.Inventory,
            SelectedCharacterKey,
            1);

        if (offer.Count <= 0 || offer[0] == null)
        {
            Log("엘리트 Mock Item 지급 실패: eligible transient item 없음");
            return null;
        }

        RunItemDefinition item = offer[0];
        if (!Progression.Inventory.Acquire(item))
            return null;

        MockEliteBonusRewardCount++;
        Log($"엘리트 추가 보상(Mock Item): {item.DisplayName}");
        return item;
    }

    public void ResolveEncounterMockSkill()
    {
        MockSkillCount++;
        Log("조우 노드: Mock Skill 1개 획득. 실제 조우 콘텐츠/풀은 아직 미정·Phase E 이후 연결 대상.");
        BackToNodeMap();
    }

    public void ResolveEncounterNoReward()
    {
        Log("조우 노드: 테스트용 무보상 종료. 현재 0916 MD에는 조우 세부 보상표가 없음.");
        BackToNodeMap();
    }

    public void RouteMysteryTo(RunFlowTestNodeType target)
    {
        if (target != RunFlowTestNodeType.GoldNode && target != RunFlowTestNodeType.Encounter)
            return;

        Log($"Legacy 물음표 Router → {GetNodeDisplayName(target)}");
        EnterNode(target);
    }

    public void RefreshGoldShopOffer()
    {
        currentShopOffer.Clear();
        int count = ItemEconomy.RollShopSlotCount();
        List<RunItemDefinition> offer = itemOfferService.CreateOffer(
            RunItemOfferSource.Shop,
            Progression.Inventory,
            SelectedCharacterKey,
            count);

        currentShopOffer.AddRange(offer);
        Log($"골드 상점 매대 생성: {currentShopOffer.Count}칸 (0916: 5~6칸)");
    }

    public bool TryBuyShopItem(int index, out string message)
    {
        message = string.Empty;
        if (index < 0 || index >= currentShopOffer.Count)
        {
            message = "잘못된 상점 슬롯입니다.";
            return false;
        }

        RunItemDefinition item = currentShopOffer[index];
        if (!Economy.TryBuyItem(Progression, item, ItemEconomy))
        {
            message = $"구매 실패: {item?.DisplayName ?? "<null>"} / 골드 또는 상태 확인";
            return false;
        }

        currentShopOffer.RemoveAt(index);
        message = $"구매 완료: {item.DisplayName} ({ItemEconomy.GetPrice(item.Tier)}G)";
        Log(message);
        return true;
    }

    public bool TrySellOwnedItem(int index, out string message)
    {
        message = string.Empty;
        IReadOnlyList<RunItemDefinition> owned = Progression.Inventory.Owned;
        if (index < 0 || index >= owned.Count)
        {
            message = "잘못된 보유 아이템 인덱스입니다.";
            return false;
        }

        RunItemDefinition item = owned[index];
        int sellPrice = ItemEconomy.GetSellPrice(item);
        if (!Economy.TrySellItem(Progression, item, ItemEconomy))
        {
            message = "판매 실패";
            return false;
        }

        message = $"판매 완료: {item.DisplayName} +{sellPrice}G (획득 이력은 유지)";
        Log(message);
        return true;
    }

    public bool TryRerollGoldShop(out string message)
    {
        message = string.Empty;
        if (!Economy.TryPayGoldShopReroll(
                Progression,
                Mathf.Max(0, TestRerollBaseCost),
                GoldShopRerollCount,
                out int paid,
                ItemEconomy))
        {
            message = "리롤 결제 실패: 골드 부족";
            return false;
        }

        GoldShopRerollCount++;
        RefreshGoldShopOffer();
        message = $"리롤 완료: -{paid}G / 다음 누적 x{Mathf.Pow(ItemEconomy.RerollCostMultiplier, GoldShopRerollCount):0.##}";
        Log(message);
        return true;
    }

    public int GetNextRerollCost()
    {
        float multiplier = Mathf.Pow(ItemEconomy.RerollCostMultiplier, GoldShopRerollCount);
        return Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0, TestRerollBaseCost) * multiplier));
    }

    public void EnableTestOnlyCurrentHpGoldRate(bool enabled)
    {
        EconomySettings.HasCurrentHpToGoldRate = enabled;
        EconomySettings.CurrentHpToGoldRate = enabled ? 2.5f : 0f;
        EconomySettings.Normalize();
        Log(enabled
            ? "TEST ONLY: (미정) 현재HP→Gold 환율을 2.5로 임시 활성화"
            : "현재HP→Gold 테스트 환율 비활성화 (canonical Unset)");
    }

    public void PrepareBossClassOffer()
    {
        currentBossOffer.Clear();
        if (Progression.Stage >= 3)
        {
            Log("Stage 3 보스: 구 맵 기획 기준 최종 보스 테스트. 0916 §16.2의 직업 아이템 3택은 Stage 1·2에만 적용.");
            return;
        }

        List<RunItemDefinition> offer = itemOfferService.CreateOffer(
            RunItemOfferSource.BossClassChoice,
            Progression.Inventory,
            SelectedCharacterKey,
            3);
        currentBossOffer.AddRange(offer);
        Log($"보스 직업 아이템 3택 생성: {currentBossOffer.Count}개");
    }

    public bool ChooseBossClassItemAndCompleteStage(int index, out string message)
    {
        message = string.Empty;
        if (Progression.Stage >= 3)
        {
            message = "Stage 3에서는 이 선택을 사용하지 않습니다.";
            return false;
        }

        if (index < 0 || index >= currentBossOffer.Count)
        {
            message = "보스 직업 아이템을 선택하세요.";
            return false;
        }

        RunItemDefinition item = currentBossOffer[index];
        if (!Progression.Inventory.Acquire(item))
        {
            message = "아이템 획득 실패";
            return false;
        }

        int clearedStage = Progression.Stage;
        Avatar.FullHeal();
        Progression.AdvanceStage();
        currentBossOffer.Clear();
        ActiveNode = null;
        Phase = RunFlowTestPhase.NodeMap;
        message = $"Stage {clearedStage} 클리어: {item.DisplayName} 획득 + HP 전회복 → Stage {Progression.Stage}";
        Log(message);
        return true;
    }

    public void CompleteStageThreeRun()
    {
        Avatar.FullHeal();
        RunCompleted = true;
        ActiveNode = null;
        Phase = RunFlowTestPhase.RunComplete;
        Log("Stage 3 보스 클리어: 테스트 런 완료 (구 맵 기획의 3스테이지 구조를 테스트 shell에만 사용)");
    }


    public bool TryUpgradeMockSkill(int index, out string message)
    {
        message = string.Empty;
        if (index < 0 || index >= mockUpgradeSkills.Count)
        {
            message = "잘못된 Mock Skill입니다.";
            return false;
        }

        SkillDefinition definition = mockUpgradeSkills[index];
        int current = Progression.SkillUpgrades.GetLevel(definition);
        int target = current + 1;

        if (!SkillUpgradeService.TryGetUpgradeCost(definition, target, out int cost))
        {
            message = $"{definition.SkillName}: Lv{target} 비용은 Unset입니다. (0916: 2회차 비용 미정)";
            return false;
        }

        if (!Economy.TryMaintenanceUpgradeSkill(Progression, definition))
        {
            message = $"{definition.SkillName}: 강화 실패 / 필요 {cost}G";
            return false;
        }

        message = $"{definition.SkillName}: Lv{target} 강화 완료 -{cost}G";
        Log(message);
        return true;
    }

    public List<string> RunSelfTest()
    {
        List<string> results = new();
        RunEconomySettings s = new();
        RunEconomyService e = new(s);

        Check(results, e.GetStageBaseGold(1) == 75, "Stage1 n = 75");
        Check(results, e.GetStageBaseGold(2) == 100, "Stage2 n = 100");
        Check(results, e.GetStageBaseGold(3) == 125, "Stage3 n = 125");
        Check(results, e.CalculateNodeGold(1, RunNodeRewardKind.NormalBattle, 1f) == 75, "일반전투 = n");
        Check(results, e.CalculateNodeGold(1, RunNodeRewardKind.EliteBattle, 1f) == 112, "엘리트 = floor(1.5n)");
        Check(results, e.CalculateNodeGold(1, RunNodeRewardKind.TurnLimitBattle, 1f) == 112, "턴제한 = floor(1.5n)");
        Check(results, e.CalculateNodeGold(1, RunNodeRewardKind.GoldNode, 1f) == 187, "골드 노드 = floor(2.5n)");
        Check(results, s.MaintenanceCost == 75, "정비 비용 75G");
        Check(results, Mathf.Approximately(s.MaintenanceHealMaxHpRatio, 0.25f), "정비 최대HP 25% 회복");
        Check(results, !s.HasCurrentHpToGoldRate, "현재HP→Gold 환율 기본 Unset");
        Check(results, s.CurrentHpCostPerMaximumHp == 3, "MaxHP +1 = CurrentHP 3");
        Check(results, ItemEconomy.ShopSlotMinimum == 5 && ItemEconomy.ShopSlotMaximum == 6, "골드 상점 5~6칸");
        Check(results, Mathf.Approximately(ItemEconomy.RerollCostMultiplier, 1.5f), "리롤 누적 x1.5");
        Check(results, ItemEconomy.GetPrice(RunItemTier.Tier1) == 400 && ItemEconomy.GetPrice(RunItemTier.Class) == 900, "아이템 가격표 연결");
        Check(results, Enum.GetValues(typeof(RunFlowTestNodeType)).Length == 10, "테스트 노드 타입 10종 접근 가능");
        return results;
    }

    public static string GetNodeDisplayName(RunFlowTestNodeType node) => node switch
    {
        RunFlowTestNodeType.NormalBattle => "일반 전투",
        RunFlowTestNodeType.EliteBattle => "엘리트 전투",
        RunFlowTestNodeType.TurnLimitBattle => "턴제한 전투",
        RunFlowTestNodeType.GoldNode => "골드 노드",
        RunFlowTestNodeType.Encounter => "조우 노드",
        RunFlowTestNodeType.MysteryLegacyRouter => "물음표 (Legacy Router)",
        RunFlowTestNodeType.GoldShop => "골드 상점",
        RunFlowTestNodeType.HealthShop => "체력 상점",
        RunFlowTestNodeType.Maintenance => "정비",
        RunFlowTestNodeType.BossBattle => "보스 전투",
        _ => node.ToString()
    };

    public static string GetEmotionDisplayName(EmotionType emotion) => emotion switch
    {
        EmotionType.Awe => "경외",
        EmotionType.Faith => "신의",
        EmotionType.Admiration => "동경",
        EmotionType.Impression => "감복",
        EmotionType.Compassion => "연민",
        EmotionType.Longing => "그리움",
        EmotionType.Detachment => "초연",
        _ => emotion.ToString()
    };

    public void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        history.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        while (history.Count > 80)
            history.RemoveAt(0);
    }


    private void BuildTransientUpgradeSkills()
    {
        AddMockSkill("Mock Normal", ActionType.NormalAttack);
        AddMockSkill("Mock Preparation", ActionType.Preparation);
        AddMockSkill("Mock Duel", ActionType.Duel);
        AddMockSkill("Mock Prestige", ActionType.Prestige);
    }

    private void AddMockSkill(string displayName, ActionType actionType)
    {
        SkillDefinition definition = ScriptableObject.CreateInstance<SkillDefinition>();
        definition.name = "RunFlowTest_" + displayName.Replace(" ", string.Empty);
        definition.SkillName = displayName;
        definition.ActionType = actionType;
        definition.Description = "RunFlowTestScene 전용 transient skill. 2회차 비용은 의도적으로 Unset.";
        mockUpgradeSkills.Add(definition);
    }

    private void BuildTransientItemCatalog()
    {
        transientCatalog = ScriptableObject.CreateInstance<RunItemCatalog>();
        transientCatalog.name = "RunFlowTest_TransientCatalog";

        int serial = 0;
        AddTransientTier(RunItemTier.Tier1, 3, ref serial);
        AddTransientTier(RunItemTier.Tier2, 3, ref serial);
        AddTransientTier(RunItemTier.Tier3, 2, ref serial);
        AddTransientTier(RunItemTier.Tier4, 2, ref serial);
        AddTransientTier(RunItemTier.Tier5, 2, ref serial);
        AddTransientTier(RunItemTier.Class, 10, ref serial);

        transientCatalog.Items.AddRange(transientItems);
        itemOfferService = new RunItemOfferService(transientCatalog, ItemEconomy);
    }

    private void AddTransientTier(RunItemTier tier, int count, ref int serial)
    {
        for (int i = 0; i < count; i++)
        {
            serial++;
            RunItemDefinition item = ScriptableObject.CreateInstance<RunItemDefinition>();
            item.name = $"RunFlowTest_Item_{serial:00}";
            item.ItemId = $"runflow_test_{tier}_{serial}";
            item.DisplayName = tier == RunItemTier.Class
                ? $"{SelectedCharacterKey} 직업 Mock {i + 1}"
                : $"{tier} Mock Item {i + 1}";
            item.Tier = tier;
            item.CharacterKey = tier == RunItemTier.Class ? SelectedCharacterKey : string.Empty;
            item.Description = "RunFlowTestScene 전용 transient item. 실제 효과 값은 Phase E 콘텐츠가 아니다.";
            transientItems.Add(item);
        }
    }

    private void RefreshTransientClassKeys()
    {
        for (int i = 0; i < transientItems.Count; i++)
        {
            RunItemDefinition item = transientItems[i];
            if (item == null || item.Tier != RunItemTier.Class)
                continue;

            item.CharacterKey = SelectedCharacterKey;
            item.DisplayName = $"{(string.IsNullOrWhiteSpace(SelectedCharacterKey) ? "Class" : SelectedCharacterKey)} 직업 Mock {i + 1}";
        }
    }

    private static void Check(List<string> destination, bool passed, string label)
    {
        destination.Add($"{(passed ? "PASS" : "FAIL")} | {label}");
    }
}
