using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SkillShaderEffectDefinition",
    menuName = "Battle/Visual/Shader Effect Definition")]
public sealed class SkillShaderEffectDefinition : ScriptableObject
{
    [Header("Shader Property Contract")]
    public List<SkillShaderFloatProperty> FloatProperties = new();
    public List<SkillShaderColorProperty> ColorProperties = new();
    public List<SkillShaderVectorProperty> VectorProperties = new();

    [Header("Validation")]
    public bool IgnoreMissingProperties;
}
