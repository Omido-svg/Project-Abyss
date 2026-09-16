using System.Collections.Generic;
using UnityEngine;

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

    [Header("Phase E Content State")]
    [Tooltip("0916 XLSX의 상태 열을 그대로 보존합니다.")]
    public string DesignStatus;
    public EmotionAugmentRuntimeReadiness RuntimeReadiness =
        EmotionAugmentRuntimeReadiness.DataOnlyPendingRuntime;
    [TextArea(1, 4)] public string RuntimeNote;

    public List<EmotionAugmentEffectDefinition> Effects = new();
}
