using System.Collections.Generic;
using ProjectAbyss.Abilities;
public sealed class HifumiCounterExecutionContext : CounterExecutionContext
{
    public int BoneAtStart { get; }
    public int AdvanceAtStart { get; }
    public int BoneTier { get; }
    public bool Bloom { get; }
    public HifumiCounterExecutionContext(long id,int rolls,EffectCause cause,int bone,int advance)
        :base(id,rolls,cause,new Dictionary<string,long>{{"hifumi.bone",bone},{"hifumi.advance",advance},
            {"hifumi.bone_tier",System.Math.Min(5,bone/100)},{"hifumi.bloom",(long)bone+advance>=500?1:0}})
    {BoneAtStart=bone;AdvanceAtStart=advance;BoneTier=System.Math.Min(5,bone/100);Bloom=(long)bone+advance>=500;}
}
