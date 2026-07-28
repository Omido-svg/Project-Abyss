using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillTimelineShaderController : MonoBehaviour
{
    private readonly Dictionary<int, List<SkillShaderTimelineContribution>>
        contributionByTrack = new();
    private readonly Dictionary<RendererSlotKey, OriginalRendererState>
        originalBySlot = new();
    private readonly Dictionary<RendererSlotKey, MaterialPropertyBlock>
        workingBlocks = new();

    private SkillCutsceneRuntimeContext context;

    public void Configure(SkillCutsceneRuntimeContext runtimeContext)
    {
        context = runtimeContext;
    }

    public void UpdateTrack(
        int trackId,
        IReadOnlyList<SkillShaderTimelineContribution> contributions)
    {
        List<SkillShaderTimelineContribution> copy = new();

        if (contributions != null)
        {
            for (int i = 0; i < contributions.Count; i++)
                copy.Add(contributions[i]);
        }

        contributionByTrack[trackId] = copy;
        RebuildAll();
    }

    public void RemoveTrack(int trackId)
    {
        contributionByTrack.Remove(trackId);
        RebuildAll();
    }

    public void RestoreAll()
    {
        RestoreOriginalBlocks();
        contributionByTrack.Clear();
        originalBySlot.Clear();
        workingBlocks.Clear();
    }

    private void RebuildAll()
    {
        RestoreOriginalBlocks();

        Dictionary<RendererSlotKey, List<ResolvedContribution>> bySlot = new();

        foreach (List<SkillShaderTimelineContribution> trackValues
                 in contributionByTrack.Values)
        {
            foreach (SkillShaderTimelineContribution contribution in trackValues)
                ResolveContribution(contribution, bySlot);
        }

        foreach (KeyValuePair<RendererSlotKey, List<ResolvedContribution>> pair
                 in bySlot)
        {
            ApplyContributions(pair.Key, pair.Value);
        }
    }

    private void ResolveContribution(
        SkillShaderTimelineContribution contribution,
        Dictionary<RendererSlotKey, List<ResolvedContribution>> bySlot)
    {
        SkillShaderTimelineClip clip = contribution.Clip;

        if (clip?.Definition == null || context == null)
            return;

        float curveTime = Mathf.Clamp01(
            contribution.NormalizedTime * Mathf.Max(0.01f, clip.PlaybackSpeed));
        float curveWeight = clip.StrengthCurve == null
            ? curveTime
            : clip.StrengthCurve.Evaluate(curveTime);
        float strength = Mathf.Clamp01(
            contribution.TimelineWeight * curveWeight);

        if (strength <= 0.0001f)
            return;

        Renderer[] renderers = ResolveRenderers(clip);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;

            if (materials == null || materials.Length == 0)
                continue;

            int first = clip.MaterialSlot >= 0
                ? clip.MaterialSlot
                : 0;
            int last = clip.MaterialSlot >= 0
                ? clip.MaterialSlot
                : materials.Length - 1;

            for (int slot = first; slot <= last; slot++)
            {
                if (slot < 0 || slot >= materials.Length || materials[slot] == null)
                    continue;

                RendererSlotKey key = new(renderer, slot);
                CaptureOriginal(key);

                if (!bySlot.TryGetValue(key, out List<ResolvedContribution> list))
                {
                    list = new List<ResolvedContribution>();
                    bySlot.Add(key, list);
                }

                list.Add(new ResolvedContribution(
                    clip.Definition,
                    strength));
            }
        }
    }

    private Renderer[] ResolveRenderers(SkillShaderTimelineClip clip)
    {
        Character character = clip.TargetCharacter == SkillShaderTargetCharacter.Attacker
            ? context.Attacker
            : context.Target;

        if (character == null)
            return Array.Empty<Renderer>();

        Transform root;

        switch (clip.RendererBinding)
        {
            case SkillShaderRendererBinding.AnchorChildren:
                root = context.ResolveVisualFxBinding(
                    clip.TargetCharacter == SkillShaderTargetCharacter.Attacker
                        ? SkillVisualFxBinding.AttackerAnchor
                        : SkillVisualFxBinding.TargetAnchor,
                    clip.AnchorKey);
                break;

            default:
                root = character.transform;
                break;
        }

        if (root == null)
            return Array.Empty<Renderer>();

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(
            clip.IncludeInactive);

        if (clip.RendererBinding != SkillShaderRendererBinding.RendererName ||
            string.IsNullOrWhiteSpace(clip.RendererName))
        {
            return renderers;
        }

        List<Renderer> filtered = new();

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null &&
                string.Equals(
                    renderer.name,
                    clip.RendererName,
                    StringComparison.OrdinalIgnoreCase))
            {
                filtered.Add(renderer);
            }
        }

        return filtered.ToArray();
    }

    private void ApplyContributions(
        RendererSlotKey key,
        List<ResolvedContribution> contributions)
    {
        Renderer renderer = key.Renderer;

        if (renderer == null)
            return;

        Material[] materials = renderer.sharedMaterials;

        if (key.MaterialSlot < 0 ||
            key.MaterialSlot >= materials.Length ||
            materials[key.MaterialSlot] == null)
        {
            return;
        }

        Material material = materials[key.MaterialSlot];
        MaterialPropertyBlock block = GetWorkingBlock(key);
        renderer.GetPropertyBlock(block, key.MaterialSlot);

        Dictionary<int, float> floats = new();
        Dictionary<int, Color> colors = new();
        Dictionary<int, Vector4> vectors = new();

        foreach (ResolvedContribution contribution in contributions)
        {
            SkillShaderEffectDefinition definition = contribution.Definition;
            float strength = contribution.Strength;

            foreach (SkillShaderFloatProperty property in definition.FloatProperties)
            {
                if (!TryGetPropertyId(material, property.PropertyName,
                        definition.IgnoreMissingProperties, out int propertyId))
                    continue;

                if (!floats.TryGetValue(propertyId, out float current))
                    current = material.GetFloat(propertyId);

                floats[propertyId] = BlendFloat(
                    current,
                    property.TargetValue,
                    strength,
                    property.BlendMode);
            }

            foreach (SkillShaderColorProperty property in definition.ColorProperties)
            {
                if (!TryGetPropertyId(material, property.PropertyName,
                        definition.IgnoreMissingProperties, out int propertyId))
                    continue;

                if (!colors.TryGetValue(propertyId, out Color current))
                    current = material.GetColor(propertyId);

                colors[propertyId] = BlendColor(
                    current,
                    property.TargetValue,
                    strength,
                    property.BlendMode);
            }

            foreach (SkillShaderVectorProperty property in definition.VectorProperties)
            {
                if (!TryGetPropertyId(material, property.PropertyName,
                        definition.IgnoreMissingProperties, out int propertyId))
                    continue;

                if (!vectors.TryGetValue(propertyId, out Vector4 current))
                    current = material.GetVector(propertyId);

                vectors[propertyId] = BlendVector(
                    current,
                    property.TargetValue,
                    strength,
                    property.BlendMode);
            }
        }

        foreach (KeyValuePair<int, float> value in floats)
            block.SetFloat(value.Key, value.Value);

        foreach (KeyValuePair<int, Color> value in colors)
            block.SetColor(value.Key, value.Value);

        foreach (KeyValuePair<int, Vector4> value in vectors)
            block.SetVector(value.Key, value.Value);

        renderer.SetPropertyBlock(block, key.MaterialSlot);
    }

    private void CaptureOriginal(RendererSlotKey key)
    {
        if (originalBySlot.ContainsKey(key) || key.Renderer == null)
            return;

        MaterialPropertyBlock original = new();
        key.Renderer.GetPropertyBlock(original, key.MaterialSlot);
        originalBySlot.Add(key, new OriginalRendererState(key.Renderer, key.MaterialSlot, original));
    }

    private void RestoreOriginalBlocks()
    {
        foreach (OriginalRendererState state in originalBySlot.Values)
        {
            if (state.Renderer != null)
                state.Renderer.SetPropertyBlock(state.OriginalBlock, state.MaterialSlot);
        }
    }

    private MaterialPropertyBlock GetWorkingBlock(RendererSlotKey key)
    {
        if (!workingBlocks.TryGetValue(key, out MaterialPropertyBlock block))
        {
            block = new MaterialPropertyBlock();
            workingBlocks.Add(key, block);
        }

        block.Clear();
        return block;
    }

    private static bool TryGetPropertyId(
        Material material,
        string propertyName,
        bool ignoreMissing,
        out int propertyId)
    {
        propertyId = 0;

        if (material == null || string.IsNullOrWhiteSpace(propertyName))
            return false;

        propertyId = Shader.PropertyToID(propertyName);

        if (material.HasProperty(propertyId))
            return true;

        if (!ignoreMissing)
        {
            Debug.LogWarning(
                $"[SkillTimelineShaderController] Shader Property 없음 / " +
                $"Material={material.name}, Property={propertyName}");
        }

        return false;
    }

    private static float BlendFloat(
        float current,
        float target,
        float weight,
        SkillShaderPropertyBlendMode mode)
    {
        return mode switch
        {
            SkillShaderPropertyBlendMode.Add => current + target * weight,
            SkillShaderPropertyBlendMode.Multiply => current * Mathf.Lerp(1f, target, weight),
            SkillShaderPropertyBlendMode.Maximum => Mathf.Max(current, Mathf.Lerp(current, target, weight)),
            SkillShaderPropertyBlendMode.Minimum => Mathf.Min(current, Mathf.Lerp(current, target, weight)),
            _ => Mathf.Lerp(current, target, weight)
        };
    }

    private static Color BlendColor(
        Color current,
        Color target,
        float weight,
        SkillShaderPropertyBlendMode mode)
    {
        return mode switch
        {
            SkillShaderPropertyBlendMode.Add => current + target * weight,
            SkillShaderPropertyBlendMode.Multiply => current * Color.Lerp(Color.white, target, weight),
            SkillShaderPropertyBlendMode.Maximum => MaxColor(current, Color.Lerp(current, target, weight)),
            SkillShaderPropertyBlendMode.Minimum => MinColor(current, Color.Lerp(current, target, weight)),
            _ => Color.Lerp(current, target, weight)
        };
    }

    private static Vector4 BlendVector(
        Vector4 current,
        Vector4 target,
        float weight,
        SkillShaderPropertyBlendMode mode)
    {
        return mode switch
        {
            SkillShaderPropertyBlendMode.Add => current + target * weight,
            SkillShaderPropertyBlendMode.Multiply => Vector4.Scale(current, Vector4.Lerp(Vector4.one, target, weight)),
            SkillShaderPropertyBlendMode.Maximum => MaxVector(current, Vector4.Lerp(current, target, weight)),
            SkillShaderPropertyBlendMode.Minimum => MinVector(current, Vector4.Lerp(current, target, weight)),
            _ => Vector4.Lerp(current, target, weight)
        };
    }

    private static Color MaxColor(Color a, Color b) =>
        new(Mathf.Max(a.r, b.r), Mathf.Max(a.g, b.g), Mathf.Max(a.b, b.b), Mathf.Max(a.a, b.a));

    private static Color MinColor(Color a, Color b) =>
        new(Mathf.Min(a.r, b.r), Mathf.Min(a.g, b.g), Mathf.Min(a.b, b.b), Mathf.Min(a.a, b.a));

    private static Vector4 MaxVector(Vector4 a, Vector4 b) =>
        new(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z), Mathf.Max(a.w, b.w));

    private static Vector4 MinVector(Vector4 a, Vector4 b) =>
        new(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z), Mathf.Min(a.w, b.w));

    private readonly struct ResolvedContribution
    {
        public ResolvedContribution(
            SkillShaderEffectDefinition definition,
            float strength)
        {
            Definition = definition;
            Strength = strength;
        }

        public SkillShaderEffectDefinition Definition { get; }
        public float Strength { get; }
    }

    private readonly struct RendererSlotKey : IEquatable<RendererSlotKey>
    {
        public RendererSlotKey(Renderer renderer, int materialSlot)
        {
            Renderer = renderer;
            MaterialSlot = materialSlot;
            rendererId = renderer != null ? renderer.GetInstanceID() : 0;
        }

        public Renderer Renderer { get; }
        public int MaterialSlot { get; }
        private readonly int rendererId;

        public bool Equals(RendererSlotKey other) =>
            rendererId == other.rendererId && MaterialSlot == other.MaterialSlot;

        public override bool Equals(object obj) =>
            obj is RendererSlotKey other && Equals(other);

        public override int GetHashCode() =>
            unchecked((rendererId * 397) ^ MaterialSlot);
    }

    private readonly struct OriginalRendererState
    {
        public OriginalRendererState(
            Renderer renderer,
            int materialSlot,
            MaterialPropertyBlock originalBlock)
        {
            Renderer = renderer;
            MaterialSlot = materialSlot;
            OriginalBlock = originalBlock;
        }

        public Renderer Renderer { get; }
        public int MaterialSlot { get; }
        public MaterialPropertyBlock OriginalBlock { get; }
    }
}
