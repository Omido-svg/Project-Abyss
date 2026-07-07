using UnityEngine;

public abstract class SkillEffectDefinition : ScriptableObject
{
    public abstract void Apply(SkillEffectContext context);
}