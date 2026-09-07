using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunItemTier
{
    Tier1 = 1,
    Tier2 = 2,
    Tier3 = 3,
    Tier4 = 4,
    Tier5 = 5,
    Class = 100
}

public enum RunItemOfferSource
{
    EliteRandom = 0,
    Shop = 1,
    BossClassChoice = 2
}

[CreateAssetMenu(menuName = "Run/Items/Run Item Definition", fileName = "NewRunItem")]
public sealed class RunItemDefinition : ScriptableObject
{
    public string ItemId;
    public string DisplayName;
    public RunItemTier Tier = RunItemTier.Tier1;
    [Tooltip("직업 아이템이면 해당 Character 식별자/이름을 기록합니다. 공용은 비웁니다.")]
    public string CharacterKey;
    [TextArea(2, 6)] public string Description;
    [Tooltip("실제 전투 빌드에 주입할 기존 CharacterItem. 효과 값이 미정이면 비워둘 수 있습니다.")]
    public CharacterItem CombatItem;
}

[CreateAssetMenu(menuName = "Run/Items/Run Item Catalog", fileName = "RunItemCatalog")]
public sealed class RunItemCatalog : ScriptableObject
{
    public List<RunItemDefinition> Items = new();
}

[Serializable]
public sealed class ItemEconomySettings
{
    [Range(0f, 1f)] public float Tier1Weight = 0.40f;
    [Range(0f, 1f)] public float Tier2Weight = 0.25f;
    [Range(0f, 1f)] public float Tier3Weight = 0.15f;
    [Range(0f, 1f)] public float Tier4Weight = 0.08f;
    [Range(0f, 1f)] public float Tier5Weight = 0.04f;
    [Range(0f, 1f)] public float ClassWeight = 0.08f;

    public int Tier1Price = 400;
    public int Tier2Price = 550;
    public int Tier3Price = 700;
    public int Tier4Price = 850;
    public int Tier5Price = 1000;
    public int ClassPrice = 900;
    [Range(0f, 1f)] public float SellRate = 0.33f;
    [Range(5, 6)] public int ShopSlotMinimum = 5;
    [Range(5, 6)] public int ShopSlotMaximum = 6;
    [Min(1f)] public float RerollCostMultiplier = 1.5f;

    public int RollShopSlotCount()
    {
        int min = Mathf.Clamp(ShopSlotMinimum, 5, 6);
        int max = Mathf.Clamp(ShopSlotMaximum, min, 6);
        return UnityEngine.Random.Range(min, max + 1);
    }

    public int GetPrice(RunItemTier tier) => tier switch
    {
        RunItemTier.Tier1 => Tier1Price,
        RunItemTier.Tier2 => Tier2Price,
        RunItemTier.Tier3 => Tier3Price,
        RunItemTier.Tier4 => Tier4Price,
        RunItemTier.Tier5 => Tier5Price,
        RunItemTier.Class => ClassPrice,
        _ => 0
    };

    public int GetSellPrice(RunItemDefinition item) =>
        item == null ? 0 : Mathf.FloorToInt(GetPrice(item.Tier) * SellRate);
}

public sealed class RunInventory
{
    private readonly List<RunItemDefinition> owned = new();
    private readonly HashSet<string> acquiredHistory = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<RunItemDefinition> Owned => owned;

    public bool Acquire(RunItemDefinition item)
    {
        if (item == null) return false;
        owned.Add(item);
        acquiredHistory.Add(GetId(item));
        return true;
    }

    public bool Sell(RunItemDefinition item)
    {
        if (item == null) return false;
        return owned.Remove(item); // 판 직업 아이템도 획득 이력은 유지한다.
    }

    public bool HasEverAcquired(RunItemDefinition item) =>
        item != null && acquiredHistory.Contains(GetId(item));

    private static string GetId(RunItemDefinition item) =>
        string.IsNullOrWhiteSpace(item.ItemId) ? item.name : item.ItemId;
}

public sealed class RunItemOfferService
{
    private readonly RunItemCatalog catalog;
    private readonly ItemEconomySettings economy;

    public RunItemOfferService(RunItemCatalog catalog, ItemEconomySettings economy = null)
    {
        this.catalog = catalog;
        this.economy = economy ?? new ItemEconomySettings();
    }

    public List<RunItemDefinition> CreateOffer(
        RunItemOfferSource source,
        RunInventory inventory,
        string characterKey,
        int count)
    {
        List<RunItemDefinition> result = new();
        if (catalog?.Items == null || count <= 0) return result;

        List<RunItemDefinition> pool = new();
        foreach (RunItemDefinition item in catalog.Items)
        {
            if (item == null) continue;

            if (item.Tier == RunItemTier.Class)
            {
                if (!string.Equals(item.CharacterKey, characterKey, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (inventory?.HasEverAcquired(item) == true)
                    continue;
            }

            if (source == RunItemOfferSource.BossClassChoice &&
                item.Tier != RunItemTier.Class)
            {
                continue;
            }

            pool.Add(item);
        }

        while (result.Count < count && pool.Count > 0)
        {
            int index = source == RunItemOfferSource.BossClassChoice
                ? UnityEngine.Random.Range(0, pool.Count)
                : PickWeighted(pool);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }

    public float GetRerollCost(float baseCost, int rerollCount) =>
        Mathf.Max(0f, baseCost) *
        Mathf.Pow(economy.RerollCostMultiplier, Mathf.Max(0, rerollCount));

    // Tier weight는 "아이템 하나"의 가중치가 아니라 "티어 버킷"의 확률이다.
    // 따라서 같은 티어의 아이템 수가 많아져도 그 티어 자체의 등장 확률은 커지지 않는다.
    private static readonly RunItemTier[] WeightedTierOrder =
    {
        RunItemTier.Tier1,
        RunItemTier.Tier2,
        RunItemTier.Tier3,
        RunItemTier.Tier4,
        RunItemTier.Tier5,
        RunItemTier.Class
    };

    private int PickWeighted(List<RunItemDefinition> pool)
    {
        if (pool == null || pool.Count == 0)
            return -1;

        // 1) 현재 pool에 실제 아이템이 남아 있는 티어만 확률 계산에 포함한다.
        //    비어 있는 티어의 확률 질량은 남은 티어끼리 자동으로 재정규화된다.
        float totalTierWeight = 0f;
        RunItemTier lastAvailableTier = default;
        bool hasWeightedTier = false;

        for (int i = 0; i < WeightedTierOrder.Length; i++)
        {
            RunItemTier tier = WeightedTierOrder[i];
            if (!ContainsTier(pool, tier))
                continue;

            float weight = Mathf.Max(0f, GetTierWeight(tier));
            if (weight <= 0f)
                continue;

            totalTierWeight += weight;
            lastAvailableTier = tier;
            hasWeightedTier = true;
        }

        // 모든 사용 가능한 티어의 weight가 0이면 기존 안전 fallback처럼 전체 pool 균등 선택.
        if (!hasWeightedTier || totalTierWeight <= 0f)
            return UnityEngine.Random.Range(0, pool.Count);

        // 2) 아이템 개수와 무관하게 티어를 먼저 추첨한다.
        float roll = UnityEngine.Random.Range(0f, totalTierWeight);
        RunItemTier selectedTier = lastAvailableTier;

        for (int i = 0; i < WeightedTierOrder.Length; i++)
        {
            RunItemTier tier = WeightedTierOrder[i];
            if (!ContainsTier(pool, tier))
                continue;

            float weight = Mathf.Max(0f, GetTierWeight(tier));
            if (weight <= 0f)
                continue;

            roll -= weight;
            if (roll <= 0f)
            {
                selectedTier = tier;
                break;
            }
        }

        // 3) 선택된 티어 내부에서는 eligible item 중 하나를 균등 선택한다.
        int tierItemCount = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null &&
                pool[i].Tier == selectedTier)
            {
                tierItemCount++;
            }
        }

        if (tierItemCount <= 0)
            return UnityEngine.Random.Range(0, pool.Count);

        int tierItemIndex =
            UnityEngine.Random.Range(0, tierItemCount);

        for (int i = 0; i < pool.Count; i++)
        {
            RunItemDefinition item = pool[i];
            if (item == null ||
                item.Tier != selectedTier)
            {
                continue;
            }

            if (tierItemIndex == 0)
                return i;

            tierItemIndex--;
        }

        // 위의 count와 탐색이 같은 pool을 사용하므로 정상적으로는 도달하지 않는다.
        return pool.Count - 1;
    }

    private static bool ContainsTier(
        IReadOnlyList<RunItemDefinition> pool,
        RunItemTier tier)
    {
        if (pool == null)
            return false;

        for (int i = 0; i < pool.Count; i++)
        {
            RunItemDefinition item = pool[i];
            if (item != null &&
                item.Tier == tier)
            {
                return true;
            }
        }

        return false;
    }

    private float GetTierWeight(RunItemTier tier) => tier switch
    {
        RunItemTier.Tier1 => economy.Tier1Weight,
        RunItemTier.Tier2 => economy.Tier2Weight,
        RunItemTier.Tier3 => economy.Tier3Weight,
        RunItemTier.Tier4 => economy.Tier4Weight,
        RunItemTier.Tier5 => economy.Tier5Weight,
        RunItemTier.Class => economy.ClassWeight,
        _ => 0f
    };
}