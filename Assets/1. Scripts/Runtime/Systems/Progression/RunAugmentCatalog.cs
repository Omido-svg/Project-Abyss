using System;
using System.Collections.Generic;

// 런 증강(전투 후 보상 · 스탯형). 전투 한정 「감정 증강」(EmotionAugmentSystem)과는 별개 층.
// 풀 = 전투 콘텐츠 명세 「증강 목록」 시트 28종(11-24 사용자: 시트 풀 그대로).
// 효과 값은 시트 값을 임시값으로 쓴다. 규칙: 위력은 건드리지 않는다 · 곱연산 금지(퍼센트는 합산).

public enum RunAugmentEffect
{
    MaxHpPercent = 0,      // 각 부위 체력 +N%
    ArmorPerTurn = 1,      // 매 턴 시작 방어도 +N
    PrestigeGainPercent = 2,
    GoldGainPercent = 3,
    ShopDiscountPercent = 4,
    RerollDiscountPercent = 5,
    SpeedPlus = 6,         // 속도 최소 · 최대 +N
    HealPercent = 7,       // 일회성: 모든 부위 N% 회복
    GoldInstant = 8,       // 일회성: 골드 N 획득
    RandomPick = 9         // 일회성: [minTier, maxTier] 상시 증강을 count개 무작위로
}

public sealed class RunAugmentDefinition
{
    public string Id;
    public string DisplayName;
    public int Tier;                 // 1~3
    public bool IsOneShot;           // 즉시 사용 · 보관 불가
    public RunAugmentEffect Effect;
    public int Value;                // 퍼센트 / 방어도 / 속도 / 골드량
    public int RandomCount;          // RandomPick 전용
    public int RandomMinTier;        // RandomPick 전용
    public int RandomMaxTier;        // RandomPick 전용
    public string Description;
}

public static class RunAugmentCatalog
{
    // 티어 확률(11-24 사용자: 스테이지와 무관, 티어마다 확률만 다르게). 값은 임시.
    public const int Tier1Weight = 60;
    public const int Tier2Weight = 30;
    public const int Tier3Weight = 10;

    // 증강 상점 판매가(임시, 티어별).
    public const int ShopSlotCount = 6;
    public const int ShopRerollBaseCost = 50;   // 임시(아이템 상점 하네스 기본값과 같게)
    public const float ShopRerollMultiplier = 1.5f;
    public static int GetShopPrice(int tier) => tier switch { 1 => 150, 2 => 250, _ => 350 };

    private static readonly List<RunAugmentDefinition> all = Build();
    public static IReadOnlyList<RunAugmentDefinition> All => all;

    public static RunAugmentDefinition Find(string id)
    {
        foreach (var a in all) if (a.Id == id) return a;
        return null;
    }

    private static List<RunAugmentDefinition> Build()
    {
        var list = new List<RunAugmentDefinition>();
        void P(int tier, string key, string name, RunAugmentEffect e, int v, string desc) =>
            list.Add(new RunAugmentDefinition { Id = $"aug.t{tier}.{key}", DisplayName = name, Tier = tier, Effect = e, Value = v, Description = desc });
        void O(int tier, string key, string name, RunAugmentEffect e, int v, string desc,
            int cnt = 0, int minT = 0, int maxT = 0) =>
            list.Add(new RunAugmentDefinition { Id = $"aug.t{tier}.{key}", DisplayName = name, Tier = tier, IsOneShot = true, Effect = e, Value = v, RandomCount = cnt, RandomMinTier = minT, RandomMaxTier = maxT, Description = desc });

        // 1티어 (속도는 2티어부터 — 합 판정값에 직접 붙는 강한 자원)
        P(1, "maxhp", "최대 체력 증가", RunAugmentEffect.MaxHpPercent, 5, "각 부위의 체력이 5% 증가");
        P(1, "armor", "방어도 증가", RunAugmentEffect.ArmorPerTurn, 3, "매 턴 시작 시 방어도 +3");
        P(1, "prestige", "위세 게이지 획득량 증가", RunAugmentEffect.PrestigeGainPercent, 10, "위세 게이지 획득량 +10%");
        P(1, "gold", "골드 획득량 증가", RunAugmentEffect.GoldGainPercent, 10, "골드 획득량 +10%");
        P(1, "shop", "상점 구매가 할인", RunAugmentEffect.ShopDiscountPercent, 5, "상점 구매가 5% 할인");
        P(1, "reroll", "아이템 리롤 가격 할인", RunAugmentEffect.RerollDiscountPercent, 10, "리롤 가격 10% 할인");
        O(1, "heal", "현재 체력 회복", RunAugmentEffect.HealPercent, 10, "모든 부위 10% 회복");
        O(1, "goldnow", "골드 획득", RunAugmentEffect.GoldInstant, 300, "골드 300 획득");
        O(1, "random", "무작위 증강", RunAugmentEffect.RandomPick, 0, "1~2티어 증강을 무작위로 1개", 1, 1, 2);

        // 2티어
        P(2, "speed", "속도 증가", RunAugmentEffect.SpeedPlus, 1, "속도 최소치와 최대치 +1");
        P(2, "maxhp", "최대 체력 증가", RunAugmentEffect.MaxHpPercent, 10, "각 부위의 체력이 10% 증가");
        P(2, "armor", "방어도 증가", RunAugmentEffect.ArmorPerTurn, 5, "매 턴 시작 시 방어도 +5");
        P(2, "prestige", "위세 게이지 획득량 증가", RunAugmentEffect.PrestigeGainPercent, 20, "위세 게이지 획득량 +20%");
        P(2, "gold", "골드 획득량 증가", RunAugmentEffect.GoldGainPercent, 15, "골드 획득량 +15%");
        P(2, "shop", "상점 구매가 할인", RunAugmentEffect.ShopDiscountPercent, 10, "상점 구매가 10% 할인");
        P(2, "reroll", "아이템 리롤 가격 할인", RunAugmentEffect.RerollDiscountPercent, 20, "리롤 가격 20% 할인");
        O(2, "heal", "현재 체력 회복", RunAugmentEffect.HealPercent, 20, "모든 부위 20% 회복");
        O(2, "goldnow", "골드 획득", RunAugmentEffect.GoldInstant, 450, "골드 450 획득");
        O(2, "random", "무작위 증강", RunAugmentEffect.RandomPick, 0, "2~3티어 증강을 무작위로 1개", 1, 2, 3);

        // 3티어
        P(3, "speed", "속도 증가", RunAugmentEffect.SpeedPlus, 2, "속도 최소치와 최대치 +2");
        P(3, "maxhp", "최대 체력 증가", RunAugmentEffect.MaxHpPercent, 15, "각 부위의 체력이 15% 증가");
        P(3, "armor", "방어도 증가", RunAugmentEffect.ArmorPerTurn, 7, "매 턴 시작 시 방어도 +7");
        P(3, "prestige", "위세 게이지 획득량 증가", RunAugmentEffect.PrestigeGainPercent, 30, "위세 게이지 획득량 +30%");
        P(3, "gold", "골드 획득량 증가", RunAugmentEffect.GoldGainPercent, 20, "골드 획득량 +20%");
        P(3, "shop", "상점 구매가 할인", RunAugmentEffect.ShopDiscountPercent, 15, "상점 구매가 15% 할인");
        P(3, "reroll", "아이템 리롤 가격 할인", RunAugmentEffect.RerollDiscountPercent, 30, "리롤 가격 30% 할인");
        O(3, "random", "무작위 증강", RunAugmentEffect.RandomPick, 0, "2~3티어 증강을 무작위로 2개", 2, 2, 3);
        O(3, "goldnow", "골드 획득", RunAugmentEffect.GoldInstant, 600, "골드 600 획득");
        return list;
    }
}

/// <summary>런이 들고 있는 상시 증강. 같은 증강을 또 얻으면 겹쳐 쌓인다(퍼센트 · 수치 합산).</summary>
public sealed class RunAugmentInventory
{
    private readonly List<RunAugmentDefinition> owned = new();
    public IReadOnlyList<RunAugmentDefinition> Owned => owned;

    public void Clear() => owned.Clear();

    public bool Add(RunAugmentDefinition aug)
    {
        if (aug == null || aug.IsOneShot) return false;
        owned.Add(aug);
        return true;
    }

    public int Sum(RunAugmentEffect effect)
    {
        int total = 0;
        foreach (var a in owned) if (a.Effect == effect) total += a.Value;
        return total;
    }

    // 퍼센트는 합산(곱연산 금지). 할인은 100%를 넘지 않게 상한.
    public float GoldGainMultiplier => 1f + Sum(RunAugmentEffect.GoldGainPercent) / 100f;
    public float ShopPriceMultiplier => 1f - Math.Min(90, Sum(RunAugmentEffect.ShopDiscountPercent)) / 100f;
    public float RerollCostMultiplier => 1f - Math.Min(90, Sum(RunAugmentEffect.RerollDiscountPercent)) / 100f;
}

public sealed class RunAugmentOfferService
{
    private readonly Random rng;
    public RunAugmentOfferService(int seed) { rng = new Random(seed); }

    private int RollTier()
    {
        int total = RunAugmentCatalog.Tier1Weight + RunAugmentCatalog.Tier2Weight + RunAugmentCatalog.Tier3Weight;
        int r = rng.Next(total);
        if (r < RunAugmentCatalog.Tier1Weight) return 1;
        if (r < RunAugmentCatalog.Tier1Weight + RunAugmentCatalog.Tier2Weight) return 2;
        return 3;
    }

    private RunAugmentDefinition PickInTier(int tier, ICollection<RunAugmentDefinition> exclude, bool persistentOnly = false)
    {
        var pool = new List<RunAugmentDefinition>();
        foreach (var a in RunAugmentCatalog.All)
            if (a.Tier == tier && (exclude == null || !exclude.Contains(a)) && (!persistentOnly || !a.IsOneShot))
                pool.Add(a);
        return pool.Count == 0 ? null : pool[rng.Next(pool.Count)];
    }

    /// <summary>턴제한 전투 보상 — 3택 1. 티어는 슬롯마다 티어 확률로 굴리고 티어 안에서는 균일. 중복 제외.</summary>
    public List<RunAugmentDefinition> CreateTurnLimitOffer(int count = 3)
    {
        var result = new List<RunAugmentDefinition>();
        int guard = 0;
        while (result.Count < count && guard++ < 100)
        {
            var pick = PickInTier(RollTier(), result);
            if (pick != null) result.Add(pick);
        }
        return result;
    }

    /// <summary>「무작위 증강」 일회성 — 지정 티어 범위의 상시 증강을 count개 얻는다(범위 안 티어 균일, 그 안에서 균일).</summary>
    public List<RunAugmentDefinition> ResolveRandomPick(RunAugmentDefinition oneShot)
    {
        var result = new List<RunAugmentDefinition>();
        if (oneShot == null || oneShot.Effect != RunAugmentEffect.RandomPick) return result;
        for (int i = 0; i < oneShot.RandomCount; i++)
        {
            int tier = rng.Next(oneShot.RandomMinTier, oneShot.RandomMaxTier + 1);
            var pick = PickInTier(tier, null, persistentOnly: true);
            if (pick != null) result.Add(pick);
        }
        return result;
    }
}

/// <summary>
/// 증강 상점(11-24 사용자) — 아이템 상점과 별도 페이지 · 6칸 · 상시 증강만(일회성 제외) · 티어 확률 없이 균일.
/// 구매하면 그 자리가 새 증강으로 교체되고, 리롤은 6칸 전부를 다시 굴린다. 리롤 횟수 · 비용은 아이템 상점과 독립.
/// 한 화면 안에서는 같은 증강이 겹치지 않는다(11-24 임시 — 사용자 확인 대기).
/// </summary>
public sealed class RunAugmentShop
{
    private readonly Random rng;
    private readonly List<RunAugmentDefinition> pool = new();
    private readonly RunAugmentDefinition[] slots = new RunAugmentDefinition[RunAugmentCatalog.ShopSlotCount];

    public IReadOnlyList<RunAugmentDefinition> Slots => slots;
    public int RerollCount { get; private set; }

    public RunAugmentShop(int seed)
    {
        rng = new Random(seed);
        foreach (var a in RunAugmentCatalog.All) if (!a.IsOneShot) pool.Add(a);
        Open();
    }

    /// <summary>상점 노드에 들어갈 때마다 호출 — 리롤 횟수 초기화 + 6칸 새로.</summary>
    public void Open()
    {
        RerollCount = 0;
        FillAll();
    }

    private void FillAll()
    {
        for (int i = 0; i < slots.Length; i++) slots[i] = null;
        for (int i = 0; i < slots.Length; i++) slots[i] = DrawUnshown();
    }

    private RunAugmentDefinition DrawUnshown()
    {
        var candidates = new List<RunAugmentDefinition>();
        foreach (var a in pool) if (Array.IndexOf(slots, a) < 0) candidates.Add(a);
        if (candidates.Count == 0) candidates = pool;
        return candidates[rng.Next(candidates.Count)];
    }

    public int GetPrice(RunAugmentDefinition aug, RunProgressionState state) =>
        aug == null ? 0 : (int)Math.Floor(RunAugmentCatalog.GetShopPrice(aug.Tier) * (state?.Augments.ShopPriceMultiplier ?? 1f));

    public int GetNextRerollCost() =>
        (int)Math.Floor(RunAugmentCatalog.ShopRerollBaseCost * Math.Pow(RunAugmentCatalog.ShopRerollMultiplier, RerollCount));

    public bool TryBuy(int index, RunProgressionState state, out string message)
    {
        message = string.Empty;
        if (state == null || index < 0 || index >= slots.Length || slots[index] == null)
        { message = "잘못된 증강 슬롯입니다."; return false; }
        var aug = slots[index];
        int price = GetPrice(aug, state);
        if (!state.TrySpendGold(price)) { message = $"골드 부족: 필요 {price}G"; return false; }
        state.Augments.Add(aug);
        slots[index] = null;
        slots[index] = DrawUnshown();      // 산 자리는 새 증강으로 교체
        message = $"증강 구매: {aug.DisplayName}({aug.Description}) -{price}G";
        return true;
    }

    public bool TryReroll(RunProgressionState state, out string message)
    {
        message = string.Empty;
        int cost = GetNextRerollCost();
        if (state == null || !state.TrySpendGold(cost)) { message = $"리롤 골드 부족: 필요 {cost}G"; return false; }
        RerollCount++;
        FillAll();                         // 6칸 전부 다시
        message = $"증강 리롤: -{cost}G / 다음 {GetNextRerollCost()}G";
        return true;
    }
}
