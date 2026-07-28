using System;
using UnityEngine;

public enum SkillVisualFxBinding
{
    CombatFrame = 0,
    AttackerRoot = 1,
    AttackerVisualRoot = 2,
    TargetRoot = 3,
    TargetVisualRoot = 4,
    AttackerAnchor = 5,
    TargetAnchor = 6,
    AttackerBodyPart = 7,
    TargetBodyPart = 8,
    Camera = 9,
    World = 10
}

public enum SkillTimelineVfxFollowMode
{
    SpawnWorldFixed = 0,
    FollowBinding = 1
}

public enum SkillTimelineVfxPlaybackMode
{
    OneShot = 0,
    ClipControlled = 1,
    LoopDuringClip = 2
}

public enum SkillShaderTargetCharacter
{
    Attacker = 0,
    Target = 1
}

public enum SkillShaderRendererBinding
{
    AllCharacterRenderers = 0,
    RendererName = 1,
    AnchorChildren = 2
}

public enum SkillShaderPropertyBlendMode
{
    Override = 0,
    Add = 1,
    Multiply = 2,
    Maximum = 3,
    Minimum = 4
}

[Serializable]
public sealed class SkillShaderFloatProperty
{
    public string PropertyName = "_EffectAmount";
    public float TargetValue = 1f;
    public SkillShaderPropertyBlendMode BlendMode =
        SkillShaderPropertyBlendMode.Override;
}

[Serializable]
public sealed class SkillShaderColorProperty
{
    public string PropertyName = "_EffectColor";
    public Color TargetValue = Color.white;
    public SkillShaderPropertyBlendMode BlendMode =
        SkillShaderPropertyBlendMode.Override;
}

[Serializable]
public sealed class SkillShaderVectorProperty
{
    public string PropertyName = "_EffectVector";
    public Vector4 TargetValue = Vector4.zero;
    public SkillShaderPropertyBlendMode BlendMode =
        SkillShaderPropertyBlendMode.Override;
}
