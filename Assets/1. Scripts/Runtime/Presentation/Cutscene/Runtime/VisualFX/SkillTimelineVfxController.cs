using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

[DisallowMultipleComponent]
public sealed class SkillTimelineVfxController : MonoBehaviour
{
    private readonly Dictionary<int, ActiveVfxState> activeByClip = new();
    private SkillCutsceneRuntimeContext context;
    private BattleVfxManager manager;

    public void Configure(SkillCutsceneRuntimeContext runtimeContext)
    {
        context = runtimeContext;
        ResolveManager();
    }

    public void Begin(
        int trackId,
        int clipKey,
        SkillVfxTimelineClip clip,
        float clipDuration)
    {
        if (clip == null || clip.Definition == null ||
            activeByClip.ContainsKey(clipKey) ||
            !PassesBattleFilter(clip))
        {
            return;
        }

        if (!Application.isPlaying && !clip.PreviewInEditMode)
            return;

        ResolveManager();

        if (manager == null || context == null)
            return;

        Transform binding = context.ResolveVisualFxBinding(
            clip.Binding,
            clip.AnchorKey);

        Pose basis = ResolveBasis(clip.Binding, binding);
        Pose startPose = EvaluatePose(clip, basis, 0f);
        Transform parent =
            clip.FollowMode == SkillTimelineVfxFollowMode.FollowBinding
                ? binding
                : null;

        BattleVfxContext vfxContext =
            BattleVfxContext.FromRequest(
                context.Request,
                clip.UseHitIndexFilter ? clip.HitIndex : -1,
                ResolveDamage(clip));

        float lifetime = ResolveLifetime(clip, clipDuration);
        bool autoRelease =
            clip.PlaybackMode == SkillTimelineVfxPlaybackMode.OneShot &&
            !clip.ReleaseAtClipEnd;

        BattleVfxInstance instance = manager.PlayTimelineVfx(
            clip.Definition,
            vfxContext,
            parent,
            startPose.position,
            startPose.rotation,
            clip.StartScale,
            clip.PlaybackSpeed,
            lifetime,
            autoRelease);

        if (instance == null)
            return;

        activeByClip.Add(
            clipKey,
            new ActiveVfxState(
                trackId,
                clipKey,
                clip,
                instance,
                binding,
                basis,
                clipDuration));
    }

    public void UpdateClip(
        int trackId,
        int clipKey,
        SkillVfxTimelineClip clip,
        float normalizedTime,
        float timelineWeight)
    {
        if (!activeByClip.TryGetValue(clipKey, out ActiveVfxState state) ||
            state.Instance == null ||
            state.Instance.IsReleased)
        {
            return;
        }

        Pose basis = state.Basis;

        if (clip.FollowMode == SkillTimelineVfxFollowMode.FollowBinding)
        {
            Transform latestBinding = context.ResolveVisualFxBinding(
                clip.Binding,
                clip.AnchorKey);
            state.Binding = latestBinding;
            basis = ResolveBasis(clip.Binding, latestBinding);
        }

        Pose pose = EvaluatePose(clip, basis, normalizedTime);
        Transform transform = state.Instance.transform;
        transform.SetPositionAndRotation(pose.position, pose.rotation);
        transform.localScale = EvaluateScale(clip, normalizedTime);

        if (!Application.isPlaying)
        {
            ScrubPreview(
                state.Instance.gameObject,
                normalizedTime * state.ClipDuration,
                clip.PlaybackSpeed);
        }
    }

    public void End(
        int trackId,
        int clipKey,
        SkillVfxTimelineClip clip)
    {
        if (!activeByClip.TryGetValue(clipKey, out ActiveVfxState state))
            return;

        activeByClip.Remove(clipKey);

        bool mustRelease =
            !Application.isPlaying ||
            clip == null ||
            clip.ReleaseAtClipEnd ||
            clip.PlaybackMode != SkillTimelineVfxPlaybackMode.OneShot;

        if (mustRelease)
            Release(state.Instance);
    }

    public void StopTrack(int trackId)
    {
        List<int> keys = new();

        foreach (KeyValuePair<int, ActiveVfxState> pair in activeByClip)
        {
            if (pair.Value.TrackId == trackId)
                keys.Add(pair.Key);
        }

        foreach (int key in keys)
        {
            ActiveVfxState state = activeByClip[key];
            activeByClip.Remove(key);
            Release(state.Instance);
        }
    }

    public void RestoreAll()
    {
        foreach (ActiveVfxState state in activeByClip.Values)
            Release(state.Instance);

        activeByClip.Clear();
    }

    private bool PassesBattleFilter(SkillVfxTimelineClip clip)
    {
        BattleVisualRequest request = context?.Request;

        if (clip.RequireKill && !(request?.WasKilled ?? false))
            return false;

        if (clip.RequirePartBreak && !(request?.BrokePart ?? false))
            return false;

        if (clip.RequireCritical && !(request?.WasCritical ?? false))
            return false;

        if (clip.RequirePositiveDamage && ResolveDamage(clip) <= 0)
            return false;

        if (clip.RequirePositiveResolvedDamage && ResolveTotalDamage(request) <= 0)
            return false;

        if (clip.RequiredAttackerData != null &&
            request?.Attacker?.Data != clip.RequiredAttackerData)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(clip.RequiredSkillNameContains))
        {
            string skillName = request?.SourceAction?.Skill?.SkillName;

            if (string.IsNullOrEmpty(skillName) ||
                !skillName.Contains(clip.RequiredSkillNameContains))
            {
                return false;
            }
        }

        return true;
    }

    private static int ResolveTotalDamage(BattleVisualRequest request)
    {
        if (request == null)
            return 0;

        if (request.DamageContext != null)
            return Mathf.Max(0, request.DamageContext.GetDisplayDamage());

        if (request.DamageResult != null)
            return Mathf.Max(0, request.DamageResult.GetDisplayDamage());

        return Mathf.Max(0, request.FallbackDamage);
    }

    private int ResolveDamage(SkillVfxTimelineClip clip)
    {
        BattleVisualRequest request = context?.Request;

        if (request == null)
            return 0;

        int hitIndex = clip != null && clip.UseHitIndexFilter
            ? clip.HitIndex
            : -1;

        return request.GetDamageForHitIndex(hitIndex);
    }

    private void ResolveManager()
    {
        if (manager != null)
            return;

        manager = Object.FindFirstObjectByType<BattleVfxManager>(
            FindObjectsInactive.Include);

        if (manager == null && context != null)
            manager = context.gameObject.AddComponent<BattleVfxManager>();
    }

    private static float ResolveLifetime(
        SkillVfxTimelineClip clip,
        float clipDuration)
    {
        if (clip.UseClipDurationAsLifetime)
            return Mathf.Max(0.01f, clipDuration);

        if (clip.LifetimeOverride > 0f)
            return clip.LifetimeOverride;

        return Mathf.Max(0.01f, clip.Definition.Lifetime);
    }

    private static Pose ResolveBasis(
        SkillVisualFxBinding bindingMode,
        Transform binding)
    {
        if (bindingMode == SkillVisualFxBinding.World || binding == null)
            return new Pose(Vector3.zero, Quaternion.identity);

        return new Pose(binding.position, binding.rotation);
    }

    private static Pose EvaluatePose(
        SkillVfxTimelineClip clip,
        Pose basis,
        float normalizedTime)
    {
        float positionT = clip.AnimateTransform
            ? EvaluateCurve(clip.PositionCurve, normalizedTime)
            : 0f;
        float rotationT = clip.AnimateTransform
            ? EvaluateCurve(clip.RotationCurve, normalizedTime)
            : 0f;

        Vector3 localPosition = Vector3.LerpUnclamped(
            clip.StartPosition,
            clip.EndPosition,
            positionT);
        Quaternion localRotation = Quaternion.SlerpUnclamped(
            Quaternion.Euler(clip.StartEuler),
            Quaternion.Euler(clip.EndEuler),
            rotationT);

        Vector3 worldPosition = basis.position + basis.rotation * localPosition;
        Quaternion worldRotation = basis.rotation * localRotation;
        return new Pose(worldPosition, worldRotation);
    }

    private static Vector3 EvaluateScale(
        SkillVfxTimelineClip clip,
        float normalizedTime)
    {
        if (!clip.AnimateTransform)
            return clip.StartScale;

        float t = EvaluateCurve(clip.ScaleCurve, normalizedTime);
        return Vector3.LerpUnclamped(
            clip.StartScale,
            clip.EndScale,
            t);
    }

    private static float EvaluateCurve(
        AnimationCurve curve,
        float normalizedTime)
    {
        return curve == null
            ? Mathf.Clamp01(normalizedTime)
            : curve.Evaluate(Mathf.Clamp01(normalizedTime));
    }

    private static void ScrubPreview(
        GameObject instance,
        float localTime,
        float playbackSpeed)
    {
        if (instance == null)
            return;

        float safeSpeed = Mathf.Max(0.01f, playbackSpeed);
        float simulatedTime = Mathf.Max(0f, localTime * safeSpeed);
        VisualEffect[] effects =
            instance.GetComponentsInChildren<VisualEffect>(true);

        const float simulationStep = 1f / 60f;
        uint stepCount = simulatedTime <= 0f
            ? 0u
            : (uint)Mathf.Clamp(
                Mathf.CeilToInt(simulatedTime / simulationStep),
                1,
                1800);

        foreach (VisualEffect effect in effects)
        {
            if (effect == null)
                continue;

            effect.playRate = safeSpeed;
            effect.Reinit();

            if (stepCount > 0u)
                effect.Simulate(simulationStep, stepCount);
        }
    }

    private static void Release(BattleVfxInstance instance)
    {
        if (instance == null)
            return;

        if (Application.isPlaying)
        {
            instance.Release();
            return;
        }

        Object.DestroyImmediate(instance.gameObject);
    }

    private sealed class ActiveVfxState
    {
        public ActiveVfxState(
            int trackId,
            int clipKey,
            SkillVfxTimelineClip clip,
            BattleVfxInstance instance,
            Transform binding,
            Pose basis,
            float clipDuration)
        {
            TrackId = trackId;
            ClipKey = clipKey;
            Clip = clip;
            Instance = instance;
            Binding = binding;
            Basis = basis;
            ClipDuration = clipDuration;
        }

        public int TrackId { get; }
        public int ClipKey { get; }
        public SkillVfxTimelineClip Clip { get; }
        public BattleVfxInstance Instance { get; }
        public Transform Binding { get; set; }
        public Pose Basis { get; }
        public float ClipDuration { get; }
    }
}