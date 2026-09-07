using System;
using System.Collections.Generic;
using UnityEngine;

public enum EmotionType
{
    Awe = 0,        // 경외
    Faith = 1,      // 신의
    Admiration = 2, // 동경
    Impression = 3,// 감복
    Compassion = 4,// 연민
    Longing = 5,    // 그리움
    Detachment = 6  // 초연
}

public abstract class EmotionAugmentEffectDefinition : ScriptableObject
{
    public abstract void Apply(EmotionAugmentRuntimeContext context);
    public virtual CombatMechanic CreateMechanic(EmotionAugmentRuntimeContext context) => null;
}

public sealed class EmotionAugmentRuntimeContext
{
    public BattleContext BattleContext;
    public Character Owner;
    public EmotionAugmentDefinition Augment;
}

[CreateAssetMenu(menuName = "Battle/Progression/Emotion Augment Definition", fileName = "NewEmotionAugment")]
public sealed class EmotionAugmentDefinition : ScriptableObject
{
    public string AugmentId;
    public string DisplayName;
    public EmotionType Emotion;
    [Range(1, 3)] public int Tier = 1;
    [Min(0f)] public float OfferWeight = 1f;
    [TextArea(2, 6)] public string Description;
    public List<EmotionAugmentEffectDefinition> Effects = new();
}

[CreateAssetMenu(menuName = "Battle/Progression/Emotion Augment Catalog", fileName = "EmotionAugmentCatalog")]
public sealed class EmotionAugmentCatalog : ScriptableObject
{
    public List<EmotionAugmentDefinition> Entries = new();

    public void GetCandidates(EmotionType emotion, int tier, List<EmotionAugmentDefinition> destination)
    {
        if (destination == null) return;
        destination.Clear();
        if (Entries == null) return;
        for (int i = 0; i < Entries.Count; i++)
        {
            EmotionAugmentDefinition entry = Entries[i];
            if (entry != null && entry.Emotion == emotion && entry.Tier == tier)
                destination.Add(entry);
        }
    }
}

public sealed class EmotionAugmentOffer
{
    public int FervorLevel;
    public EmotionType Emotion;
    public readonly List<EmotionAugmentDefinition> Choices = new();
}

/// <summary>
/// 열광 레벨업과 감정 증강 제안을 연결한다. 효과 값/확률은 전부 Catalog 데이터이며
/// 전투가 끝나거나 ResetForBattle 호출 시 획득 목록과 pending offer가 사라진다.
/// </summary>
public sealed class EmotionAugmentManager
{
    private readonly BattleContext context;
    private readonly FervorManager fervor;
    private readonly List<EmotionAugmentDefinition> candidates = new();
    private readonly List<EmotionAugmentDefinition> acquired = new();

    public EmotionAugmentOffer PendingOffer { get; private set; }
    public IReadOnlyList<EmotionAugmentDefinition> Acquired => acquired;
    public event Action<EmotionAugmentOffer> OfferCreated;
    public event Action<EmotionAugmentDefinition> AugmentAcquired;

    public EmotionAugmentManager(BattleContext context, FervorManager fervor)
    {
        this.context = context;
        this.fervor = fervor;
        if (fervor != null) fervor.LevelUp += OnFervorLevelUp;
    }

    public void ResetForBattle()
    {
        PendingOffer = null;
        acquired.Clear();
    }

    public bool Choose(int choiceIndex)
    {
        if (PendingOffer == null || choiceIndex < 0 || choiceIndex >= PendingOffer.Choices.Count)
            return false;

        EmotionAugmentDefinition selected = PendingOffer.Choices[choiceIndex];
        PendingOffer = null;
        if (selected == null) return false;

        acquired.Add(selected);
        Apply(selected);
        AugmentAcquired?.Invoke(selected);
        return true;
    }

    private void OnFervorLevelUp(FervorLevelUpContext levelUp)
    {
        if (!context.SelectedEmotion.HasValue || context.EmotionAugmentCatalog == null)
            return;

        context.EmotionAugmentCatalog.GetCandidates(context.SelectedEmotion.Value, levelUp.NewLevel, candidates);
        if (candidates.Count == 0) return;

        EmotionAugmentOffer offer = new EmotionAugmentOffer
        {
            FervorLevel = levelUp.NewLevel,
            Emotion = context.SelectedEmotion.Value
        };

        List<EmotionAugmentDefinition> pool = new(candidates);
        while (offer.Choices.Count < 3 && pool.Count > 0)
        {
            int index = PickWeightedIndex(pool);
            offer.Choices.Add(pool[index]);
            pool.RemoveAt(index);
        }

        PendingOffer = offer;
        OfferCreated?.Invoke(offer);
    }

    private void Apply(EmotionAugmentDefinition augment)
    {
        Character owner = context?.Player;
        if (owner == null || augment?.Effects == null) return;

        EmotionAugmentRuntimeContext runtime = new EmotionAugmentRuntimeContext
        {
            BattleContext = context,
            Owner = owner,
            Augment = augment
        };

        foreach (EmotionAugmentEffectDefinition effect in augment.Effects)
        {
            if (effect == null) continue;
            effect.Apply(runtime);
            CombatMechanic mechanic = effect.CreateMechanic(runtime);
            if (mechanic != null)
                owner.AddRuntimeMechanic(mechanic);
        }
    }

    private static int PickWeightedIndex(List<EmotionAugmentDefinition> pool)
    {
        float total = 0f;
        for (int i = 0; i < pool.Count; i++) total += Mathf.Max(0f, pool[i]?.OfferWeight ?? 0f);
        if (total <= 0f) return UnityEngine.Random.Range(0, pool.Count);

        float roll = UnityEngine.Random.Range(0f, total);
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= Mathf.Max(0f, pool[i]?.OfferWeight ?? 0f);
            if (roll <= 0f) return i;
        }
        return pool.Count - 1;
    }
}
