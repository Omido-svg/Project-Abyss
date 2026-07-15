using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterView : MonoBehaviour, ISerializationCallbackReceiver
{
    [Header("Model")]
    [SerializeField] private Character character;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationEventRelay eventRelay;

    [Header("Body Part Anchors")]
    [SerializeField] private List<BodyPartAnchor> bodyPartAnchors = new();

    private readonly Dictionary<PartType, Transform> bodyPartAnchorCache = new();
    private bool bodyPartAnchorCacheDirty = true;

    [Header("Camera Points")]
    [SerializeField] private Transform lookAtPoint;
    [SerializeField] private Transform attackCameraPoint;
    [SerializeField] private Transform hitCameraPoint;
    
    [SerializeField] private string hitStateName = "Hit";
    [SerializeField] private float hitCrossFadeDuration = 0.03f;
    [SerializeField] private int baseLayerIndex = 0;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    public void PlayHit()
    {
        if (animator == null)
            return;

        animator.SetTrigger(HitHash);
    }

    public void PlayHitRestart()
    {
        if (logDebug)
        {
            Debug.Log(
                $"[CharacterView] 실제 Hit 재생 / " +
                $"ViewObject={name}, " +
                $"Root={transform.root.name}");
        }
        
        if (animator == null)
        {
            Debug.LogWarning($"{name} Animator 없음 - Hit 재생 불가");
            return;
        }

        int hitStateHash =
            Animator.StringToHash(hitStateName);

        animator.ResetTrigger(HitHash);

        if (animator.HasState(baseLayerIndex, hitStateHash))
        {
            animator.CrossFadeInFixedTime(
                hitStateHash,
                hitCrossFadeDuration,
                baseLayerIndex,
                0f);

            if (logDebug)
                Debug.Log($"{name} Hit 상태 직접 재생 : {hitStateName}");
            return;
        }

        Debug.LogWarning(
            $"{name} Animator에 Hit State를 찾을 수 없음 : {hitStateName} / " +
            $"Trigger 방식으로 대체 실행");

        animator.SetTrigger(HitHash);
    }

    private Action hitFrameCallback;
    private Action effectFrameCallback;

    private bool animationEnded;

    private static readonly int VisualStateHash =
        Animator.StringToHash("VisualState");

    private static readonly int HitHash =
        Animator.StringToHash("Hit");

    private static readonly int DeadHash =
        Animator.StringToHash("Dead");

    private static readonly int NormalAttackHash =
        Animator.StringToHash("NormalAttack");

    private static readonly int DuelHash =
        Animator.StringToHash("Duel");

    private static readonly int PreparationHash =
        Animator.StringToHash("Preparation");

    private static readonly int PrestigeHash =
        Animator.StringToHash("Prestige");

    public Character Character => character;

    public Transform LookAtPoint => lookAtPoint != null ? lookAtPoint : transform;
    public Transform AttackCameraPoint => attackCameraPoint != null ? attackCameraPoint : transform;
    public Transform HitCameraPoint => hitCameraPoint != null ? hitCameraPoint : transform;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        eventRelay = GetComponentInChildren<AnimationEventRelay>();
        InvalidateBodyPartAnchorCache();
    }

    private void Awake()
    {
        RebuildBodyPartAnchorCache();
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        CancelActionPlayback();
        UnsubscribeEvents();
    }

    private void OnValidate()
    {
        InvalidateBodyPartAnchorCache();
    }

    private void OnTransformChildrenChanged()
    {
        InvalidateBodyPartAnchorCache();
    }

    void ISerializationCallbackReceiver.OnBeforeSerialize()
    {
    }

    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        bodyPartAnchorCacheDirty = true;
    }

    public void Bind(Character targetCharacter)
    {
        character = targetCharacter;
        RefreshVisualState();
    }

    private void SubscribeEvents()
    {
        if (eventRelay == null)
            return;

        eventRelay.HitFrame += OnHitFrame;
        eventRelay.EffectFrame += OnEffectFrame;
        eventRelay.AnimationEnd += OnAnimationEnd;
    }

    private void UnsubscribeEvents()
    {
        if (eventRelay == null)
            return;

        eventRelay.HitFrame -= OnHitFrame;
        eventRelay.EffectFrame -= OnEffectFrame;
        eventRelay.AnimationEnd -= OnAnimationEnd;
    }

    public IEnumerator PlayAction(
        ActionType actionType,
        Action onHitFrame = null,
        Action onEffectFrame = null,
        float timeout = 5f)
    {
        if (animator == null)
            yield break;

        animationEnded = false;

        hitFrameCallback = onHitFrame;
        effectFrameCallback = onEffectFrame;

        try
        {
            int triggerHash =
                GetTriggerHash(actionType);

            ResetActionTriggers();

            animator.SetTrigger(triggerHash);

            float elapsed = 0f;

            while (!animationEnded && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            hitFrameCallback = null;
            effectFrameCallback = null;
            animationEnded = false;
        }

        RefreshVisualState();
    }

    public void CancelActionPlayback()
    {
        ClearActionPlaybackCallbacks();

        if (animator != null)
            RefreshVisualState();
    }

    public void AbortActionPlayback()
    {
        ClearActionPlaybackCallbacks();

        if (animator == null)
            return;

        bool isDead =
            character != null &&
            character.IsDead;

        ResetActionTriggers();

        if (animator.isActiveAndEnabled)
            animator.Rebind();

        RefreshVisualState();

        if (isDead)
            PlayDead();

        if (animator.isActiveAndEnabled)
            animator.Update(0f);
    }

    private void ClearActionPlaybackCallbacks()
    {
        hitFrameCallback = null;
        effectFrameCallback = null;
        animationEnded = true;
    }

    private void ResetActionTriggers()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(HitHash);
        animator.ResetTrigger(DeadHash);
        animator.ResetTrigger(NormalAttackHash);
        animator.ResetTrigger(DuelHash);
        animator.ResetTrigger(PreparationHash);
        animator.ResetTrigger(PrestigeHash);
    }

    public void PlayDead()
    {
        if (animator == null)
            return;

        animator.SetTrigger(DeadHash);
    }

    public void SetVisualStateForTest(CharacterVisualState state)
    {
        if (animator == null)
            return;

        animator.SetFloat(
            VisualStateHash,
            (float)state);
    }

    public void RefreshVisualState()
    {
        if (animator == null)
        {
            Debug.LogWarning($"{name} CharacterView Animator 없음");
            return;
        }

        CharacterVisualState state =
            CalculateVisualState();

        animator.SetFloat(
            VisualStateHash,
            (float)state);

        if (logDebug)
        {
            Debug.Log(
                $"{name} VisualState 갱신 : {state} / float = {(float)state}");
        }
    }

    private CharacterVisualState CalculateVisualState()
    {
        if (character == null)
            return CharacterVisualState.Normal;

        if (character.BodyParts == null)
            return CharacterVisualState.Normal;

        bool hasWeakenedPart = false;

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            if (part.IsBroken)
                return CharacterVisualState.Broken;

            if (part.IsWeakened)
                hasWeakenedPart = true;
        }

        if (hasWeakenedPart)
            return CharacterVisualState.Weakened;

        return CharacterVisualState.Normal;
    }

    public Transform GetBodyPartAnchor(BodyPart part)
    {
        if (part == null)
            return transform;

        return GetBodyPartAnchor(part.Type);
    }

    public Transform GetBodyPartAnchor(PartType partType)
    {
        EnsureBodyPartAnchorCache();

        if (!bodyPartAnchorCache.TryGetValue(
                partType,
                out Transform anchor))
        {
            return transform;
        }

        if (anchor != null)
            return anchor;

        // A cached Unity object can become invalid when the hierarchy changes at runtime.
        // Rebuild once so a later valid duplicate entry can take over.
        RebuildBodyPartAnchorCache();

        if (bodyPartAnchorCache.TryGetValue(
                partType,
                out anchor) &&
            anchor != null)
        {
            return anchor;
        }

        return transform;
    }

    public void InvalidateBodyPartAnchorCache()
    {
        bodyPartAnchorCacheDirty = true;
    }

    public void RebuildBodyPartAnchorCache()
    {
        bodyPartAnchorCache.Clear();

        if (bodyPartAnchors != null)
        {
            foreach (BodyPartAnchor anchor in bodyPartAnchors)
            {
                if (anchor == null || anchor.Anchor == null)
                    continue;

                // Preserve the previous list lookup semantics: the first valid
                // anchor for a part wins, while null entries are ignored.
                if (bodyPartAnchorCache.ContainsKey(anchor.PartType))
                    continue;

                bodyPartAnchorCache.Add(
                    anchor.PartType,
                    anchor.Anchor);
            }
        }

        bodyPartAnchorCacheDirty = false;
    }

    private void EnsureBodyPartAnchorCache()
    {
        if (bodyPartAnchorCacheDirty)
            RebuildBodyPartAnchorCache();
    }

    private int GetTriggerHash(ActionType actionType)
    {
        return actionType switch
        {
            ActionType.NormalAttack => NormalAttackHash,
            ActionType.Duel => DuelHash,
            ActionType.Preparation => PreparationHash,
            ActionType.Prestige => PrestigeHash,
            _ => NormalAttackHash
        };
    }

    private void OnHitFrame()
    {
        hitFrameCallback?.Invoke();
    }

    private void OnEffectFrame()
    {
        effectFrameCallback?.Invoke();
    }

    private void OnAnimationEnd()
    {
        animationEnded = true;
    }
    
    public Vector3 GetDamageNumberPosition(BodyPart part)
    {
        Transform anchor = GetBodyPartAnchor(part);

        if (anchor == null)
            return transform.position + Vector3.up * 1.5f;

        return anchor.position;
    }

    public Vector3 GetDamageNumberPosition(PartType partType)
    {
        Transform anchor = GetBodyPartAnchor(partType);

        if (anchor == null)
            return transform.position + Vector3.up * 1.5f;

        return anchor.position;
    }
}
