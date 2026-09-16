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
    [HideInInspector, Tooltip("Legacy 0915 field. C-34 이후 제안은 가중 랜덤을 사용하지 않습니다.")]
    public float OfferWeight = 1f;
    [TextArea(2, 6)] public string Description;
    public List<EmotionAugmentEffectDefinition> Effects = new();
}

[CreateAssetMenu(menuName = "Battle/Progression/Emotion Augment Catalog", fileName = "EmotionAugmentCatalog")]
public sealed class EmotionAugmentCatalog : ScriptableObject
{
    public List<EmotionAugmentDefinition> Entries = new();

    public void GetCandidates(
        EmotionType emotion,
        int tier,
        List<EmotionAugmentDefinition> destination)
    {
        if (destination == null)
            return;

        destination.Clear();
        if (Entries == null)
            return;

        int normalizedTier = Mathf.Clamp(tier, 1, 3);

        for (int i = 0; i < Entries.Count; i++)
        {
            EmotionAugmentDefinition entry = Entries[i];
            if (entry != null &&
                entry.Emotion == emotion &&
                Mathf.Clamp(entry.Tier, 1, 3) == normalizedTier)
            {
                destination.Add(entry);
            }
        }
    }

    /// <summary>
    /// C-34: 같은 감정/티어는 Catalog 순서의 정확히 3장을 항상 동일하게 제안한다.
    /// 3장이 아니면 조용히 근사하지 않고 authoring 오류로 반환한다.
    /// </summary>
    public bool TryBuildCanonicalOffer(
        EmotionType emotion,
        int tier,
        List<EmotionAugmentDefinition> destination,
        out string reason)
    {
        if (destination == null)
        {
            reason = "destination is null";
            return false;
        }

        GetCandidates(emotion, tier, destination);

        if (destination.Count != 3)
        {
            reason =
                $"{emotion} Tier {Mathf.Clamp(tier, 1, 3)}는 정확히 3장이어야 합니다. Actual={destination.Count}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public int GetCandidateCount(
        EmotionType emotion,
        int tier)
    {
        if (Entries == null)
            return 0;

        int normalizedTier = Mathf.Clamp(tier, 1, 3);
        int count = 0;

        for (int i = 0; i < Entries.Count; i++)
        {
            EmotionAugmentDefinition entry = Entries[i];
            if (entry != null &&
                entry.Emotion == emotion &&
                Mathf.Clamp(entry.Tier, 1, 3) == normalizedTier)
            {
                count++;
            }
        }

        return count;
    }
}

public sealed class EmotionAugmentOffer
{
    public int FervorLevel;
    public EmotionType Emotion;
    public readonly List<EmotionAugmentDefinition> Choices = new();
}

/// <summary>
/// 열광 레벨업과 감정 증강 제안을 연결한다.
///
/// - 열광 0->1 : Tier 1 증강 3택
/// - 열광 1->2 : Tier 2 증강 3택
/// - 열광 2->3 : Tier 3 증강 3택
///
/// 한 번의 턴 종료 처리에서 레벨업이 여러 번 발생해도 제안을 덮어쓰지 않고
/// 순서대로 큐에 보관한다. PendingOffer가 존재하는 동안 다음 턴 진행은 BattleManager가 차단한다.
/// </summary>
public sealed class EmotionAugmentManager
{
    private readonly BattleContext context;
    private readonly FervorManager fervor;
    private readonly List<EmotionAugmentDefinition> candidates = new();
    private readonly List<EmotionAugmentDefinition> acquired = new();
    private readonly Queue<EmotionAugmentOffer> queuedOffers = new();

    public EmotionAugmentOffer PendingOffer { get; private set; }
    public IReadOnlyList<EmotionAugmentDefinition> Acquired => acquired;
    public int PendingOfferCount =>
        (PendingOffer != null ? 1 : 0) + queuedOffers.Count;

    public event Action<EmotionAugmentOffer> OfferCreated;
    public event Action<EmotionAugmentOffer> OfferPresentationRequested;
    public event Action<EmotionAugmentDefinition> AugmentAcquired;

    public EmotionAugmentManager(
        BattleContext context,
        FervorManager fervor)
    {
        this.context = context;
        this.fervor = fervor;

        if (fervor != null)
            fervor.LevelUp += OnFervorLevelUp;
    }

    public void ResetForBattle()
    {
        PendingOffer = null;
        queuedOffers.Clear();
        acquired.Clear();
    }

    public void RequestPendingOfferPresentation()
    {
        if (PendingOffer != null)
            OfferPresentationRequested?.Invoke(PendingOffer);
    }

    public bool Choose(int choiceIndex)
    {
        if (PendingOffer == null ||
            choiceIndex < 0 ||
            choiceIndex >= PendingOffer.Choices.Count)
        {
            return false;
        }

        EmotionAugmentDefinition selected =
            PendingOffer.Choices[choiceIndex];

        if (selected == null)
            return false;

        PendingOffer = null;
        acquired.Add(selected);
        Apply(selected);
        AugmentAcquired?.Invoke(selected);

        PromoteQueuedOffer();
        return true;
    }

    private void OnFervorLevelUp(
        FervorLevelUpContext levelUp)
    {
        if (!context.SelectedEmotion.HasValue)
        {
            Debug.LogWarning(
                "[EmotionAugment] 열광 레벨업이 발생했지만 선택 감정이 없습니다.");
            return;
        }

        if (context.EmotionAugmentCatalog == null)
        {
            Debug.LogWarning(
                "[EmotionAugment] 열광 레벨업이 발생했지만 EmotionAugmentCatalog가 없습니다.");
            return;
        }

        int tier = Mathf.Clamp(levelUp.NewLevel, 1, 3);
        EmotionType emotion = context.SelectedEmotion.Value;

        if (!context.EmotionAugmentCatalog.TryBuildCanonicalOffer(
                emotion,
                tier,
                candidates,
                out string reason))
        {
            Debug.LogWarning(
                $"[EmotionAugment] C-34 canonical offer 생성 실패: {reason}");
            return;
        }

        EmotionAugmentOffer offer =
            new EmotionAugmentOffer
            {
                FervorLevel = tier,
                Emotion = emotion
            };

        // Catalog order is the canonical deterministic order.
        for (int i = 0; i < candidates.Count; i++)
            offer.Choices.Add(candidates[i]);

        QueueOffer(offer);
    }

    private void QueueOffer(
        EmotionAugmentOffer offer)
    {
        if (offer == null)
            return;

        if (PendingOffer == null)
        {
            PendingOffer = offer;
            OfferCreated?.Invoke(offer);
            return;
        }

        queuedOffers.Enqueue(offer);
    }

    private void PromoteQueuedOffer()
    {
        if (PendingOffer != null ||
            queuedOffers.Count == 0)
        {
            return;
        }

        PendingOffer = queuedOffers.Dequeue();
        OfferCreated?.Invoke(PendingOffer);
    }

    private void Apply(
        EmotionAugmentDefinition augment)
    {
        Character owner = context?.Player;
        if (owner == null || augment?.Effects == null)
            return;

        EmotionAugmentRuntimeContext runtime =
            new EmotionAugmentRuntimeContext
            {
                BattleContext = context,
                Owner = owner,
                Augment = augment
            };

        foreach (EmotionAugmentEffectDefinition effect
                 in augment.Effects)
        {
            if (effect == null)
                continue;

            effect.Apply(runtime);

            CombatMechanic mechanic =
                effect.CreateMechanic(runtime);

            if (mechanic != null)
                owner.AddRuntimeMechanic(mechanic);
        }
    }


}
