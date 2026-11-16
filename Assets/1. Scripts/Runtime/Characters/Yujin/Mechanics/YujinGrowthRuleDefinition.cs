using UnityEngine;
using ProjectAbyss.Abilities;

public enum YujinGrowthAxis { Execution, Judgment }
public sealed class YujinGrowthRuleSpec : IAbilityRuleSettings
{
    public readonly YujinGrowthAxis Axis;
    public readonly int Stage, UnmarkedAmount, MarkedAmount, ReapplyAfterBurst;
    public YujinGrowthRuleSpec(YujinGrowthAxis axis,int stage,int unmarked,int marked,int reapply)
    {
        if(stage<0||stage>4||unmarked<=0||marked<=0||reapply<0)throw new System.ArgumentException("Growth values must be explicitly configured");
        Axis=axis;Stage=stage;UnmarkedAmount=unmarked;MarkedAmount=marked;ReapplyAfterBurst=reapply;
    }
}
[CreateAssetMenu(menuName="Project Abyss/Abilities/Yujin Growth Rule",fileName="YujinGrowthRule")]
public sealed class YujinGrowthRuleDefinition : ScriptableObject,IAbilityRuleSettingsDefinition
{
    public YujinGrowthAxis Axis;
    [Range(0,4)] public int Stage;
    [Min(0)] public int UnmarkedAmount,MarkedAmount,ReapplyAfterBurst;
    public IAbilityRuleSettings CaptureSettings()=>new YujinGrowthRuleSpec(Axis,Stage,UnmarkedAmount,MarkedAmount,ReapplyAfterBurst);
}
