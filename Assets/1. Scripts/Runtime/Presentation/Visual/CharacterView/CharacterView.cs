using System.Collections.Generic;
using UnityEngine;

public class CharacterView : MonoBehaviour, ISerializationCallbackReceiver
{
    [Header("Model")]
    [SerializeField] private Character character;

    [Header("State Animator")]
    [Tooltip("Idle / Hit / Dead / 상태 표현만 담당합니다. 공격 모션은 Timeline Animation Track이 직접 재생합니다.")]
    [SerializeField] private Animator animator;

    [Header("Body Part Anchors")]
    [SerializeField] private List<BodyPartAnchor> bodyPartAnchors = new();

    private readonly Dictionary<PartType, Transform> bodyPartAnchorCache = new();
    private bool bodyPartAnchorCacheDirty = true;

    [Header("Camera Points")]
    [SerializeField] private Transform lookAtPoint;
    [SerializeField] private Transform attackCameraPoint;
    [SerializeField] private Transform hitCameraPoint;

    [Header("Hit State")]
    [SerializeField] private string hitStateName = "Hit";
    [SerializeField] private float hitCrossFadeDuration = 0.03f;
    [Tooltip("Timeline Hit Event가 발생한 같은 프레임에 Hit State의 첫 포즈를 즉시 평가합니다.")]
    [SerializeField] private bool evaluateHitImmediately = true;
    [SerializeField] private int baseLayerIndex = 0;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private bool missingAnimatorWarningLogged;
    private bool missingHitPlaybackWarningLogged;
    private bool hasAnimatorSpeedOverride;
    private float animatorSpeedBeforeAction = 1f;

    private static readonly int VisualStateHash = Animator.StringToHash("VisualState");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    public Character Character => character;
    public Animator Animator => animator;
    public Transform LookAtPoint => lookAtPoint != null ? lookAtPoint : transform;
    public Transform AttackCameraPoint => attackCameraPoint != null ? attackCameraPoint : transform;
    public Transform HitCameraPoint => hitCameraPoint != null ? hitCameraPoint : transform;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        InvalidateBodyPartAnchorCache();
    }

    private void Awake()
    {
        RebuildBodyPartAnchorCache();
    }

    private void OnDisable()
    {
        CancelActionPlayback();
    }

    private void OnValidate()
    {
        InvalidateBodyPartAnchorCache();
    }

    private void OnTransformChildrenChanged()
    {
        InvalidateBodyPartAnchorCache();
    }

    void ISerializationCallbackReceiver.OnBeforeSerialize() { }

    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        bodyPartAnchorCacheDirty = true;
    }

    public void Bind(Character targetCharacter)
    {
        character = targetCharacter;
        RefreshVisualState();
    }

    public void PlayHit()
    {
        if (animator == null)
            return;

        if (HasAnimatorParameter(HitHash, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(HitHash);
    }

    public void PlayHitRestart()
    {
        if (logDebug)
        {
            Debug.Log(
                $"[CharacterView] Timeline Hit 반응 재생 / ViewObject={name}, Root={transform.root.name}",
                this);
        }

        if (animator == null)
        {
            if (!missingAnimatorWarningLogged)
            {
                missingAnimatorWarningLogged = true;
                Debug.LogWarning($"{name} Animator 없음 - Hit 재생 불가", this);
            }
            return;
        }

        bool hasHitTrigger =
            HasAnimatorParameter(HitHash, AnimatorControllerParameterType.Trigger);

        if (hasHitTrigger)
            animator.ResetTrigger(HitHash);

        if (TryResolveHitStateHash(out int hitStateHash))
        {
            if (evaluateHitImmediately)
            {
                // PlayableDirector의 Event Mixer가 Animator 갱신 뒤에 실행될 수 있으므로
                // CrossFade 예약만 하면 피격 포즈가 다음 프레임에 보일 수 있다.
                // Impact 프레임에는 상태를 즉시 0초에서 평가해 타격과 포즈를 일치시킨다.
                animator.Play(
                    hitStateHash,
                    baseLayerIndex,
                    0f);

                if (animator.isActiveAndEnabled)
                    animator.Update(0f);
            }
            else
            {
                animator.CrossFadeInFixedTime(
                    hitStateHash,
                    hitCrossFadeDuration,
                    baseLayerIndex,
                    0f);
            }

            return;
        }

        if (hasHitTrigger)
        {
            animator.SetTrigger(HitHash);

            if (evaluateHitImmediately &&
                animator.isActiveAndEnabled)
            {
                animator.Update(0f);
            }

            return;
        }

        if (!missingHitPlaybackWarningLogged)
        {
            missingHitPlaybackWarningLogged = true;
            Debug.LogWarning(
                $"{name} Animator에서 Hit 상태/Trigger를 찾을 수 없습니다. " +
                "공격 타이밍은 Timeline이 담당하지만 피격 상태 Animator 구성은 필요합니다.",
                this);
        }
    }

    public void CancelActionPlayback()
    {
        RestoreAnimatorSpeed();
        if (animator != null)
            RefreshVisualState();
    }

    public void AbortActionPlayback()
    {
        RestoreAnimatorSpeed();

        if (animator == null)
            return;

        bool isDead = character != null && character.IsDead;

        if (animator.isActiveAndEnabled)
            animator.Rebind();

        RefreshVisualState();

        if (isDead)
            PlayDead();

        if (animator.isActiveAndEnabled)
            animator.Update(0f);
    }

    public void PlayDead()
    {
        if (animator == null)
            return;

        if (HasAnimatorParameter(DeadHash, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(DeadHash);
    }

    public void SetVisualStateForTest(CharacterVisualState state)
    {
        if (animator == null)
            return;

        animator.SetFloat(VisualStateHash, (float)state);
    }

    public void RefreshVisualState()
    {
        if (animator == null)
        {
            if (!missingAnimatorWarningLogged)
            {
                missingAnimatorWarningLogged = true;
                Debug.LogWarning($"{name} CharacterView Animator 없음", this);
            }
            return;
        }

        CharacterVisualState state = CalculateVisualState();
        animator.SetFloat(VisualStateHash, (float)state);

        if (logDebug)
            Debug.Log($"{name} VisualState 갱신 : {state}", this);
    }

    public Transform GetBodyPartAnchor(BodyPart part)
    {
        return part == null ? transform : GetBodyPartAnchor(part.Type);
    }

    public Transform GetBodyPartAnchor(PartType partType)
    {
        EnsureBodyPartAnchorCache();

        if (bodyPartAnchorCache.TryGetValue(partType, out Transform anchor) &&
            anchor != null)
        {
            return anchor;
        }

        RebuildBodyPartAnchorCache();

        return bodyPartAnchorCache.TryGetValue(partType, out anchor) && anchor != null
            ? anchor
            : transform;
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
                if (anchor == null || anchor.Anchor == null ||
                    bodyPartAnchorCache.ContainsKey(anchor.PartType))
                {
                    continue;
                }

                bodyPartAnchorCache.Add(anchor.PartType, anchor.Anchor);
            }
        }

        bodyPartAnchorCacheDirty = false;
    }

    public Vector3 GetDamageNumberPosition(BodyPart part)
    {
        Transform anchor = GetBodyPartAnchor(part);
        return anchor != null ? anchor.position : transform.position + Vector3.up * 1.5f;
    }

    public Vector3 GetDamageNumberPosition(PartType partType)
    {
        Transform anchor = GetBodyPartAnchor(partType);
        return anchor != null ? anchor.position : transform.position + Vector3.up * 1.5f;
    }

    private void EnsureBodyPartAnchorCache()
    {
        if (bodyPartAnchorCacheDirty)
            RebuildBodyPartAnchorCache();
    }

    private void RestoreAnimatorSpeed()
    {
        if (animator == null || !hasAnimatorSpeedOverride)
            return;

        animator.speed = animatorSpeedBeforeAction;
        animatorSpeedBeforeAction = 1f;
        hasAnimatorSpeedOverride = false;
    }

    private bool HasAnimatorParameter(
        int parameterHash,
        AnimatorControllerParameterType expectedType)
    {
        if (animator == null || animator.parameters == null)
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash && parameter.type == expectedType)
                return true;
        }

        return false;
    }

    private bool TryResolveHitStateHash(out int stateHash)
    {
        stateHash = 0;

        if (animator == null || string.IsNullOrWhiteSpace(hitStateName) ||
            baseLayerIndex < 0 || baseLayerIndex >= animator.layerCount)
        {
            return false;
        }

        int shortNameHash = Animator.StringToHash(hitStateName);
        if (animator.HasState(baseLayerIndex, shortNameHash))
        {
            stateHash = shortNameHash;
            return true;
        }

        string layerName = animator.GetLayerName(baseLayerIndex);
        if (string.IsNullOrWhiteSpace(layerName))
            return false;

        int fullPathHash = Animator.StringToHash($"{layerName}.{hitStateName}");
        if (!animator.HasState(baseLayerIndex, fullPathHash))
            return false;

        stateHash = fullPathHash;
        return true;
    }

    private CharacterVisualState CalculateVisualState()
    {
        if (character?.BodyParts == null)
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

        return hasWeakenedPart
            ? CharacterVisualState.Weakened
            : CharacterVisualState.Normal;
    }
}
