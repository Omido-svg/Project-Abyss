using System;
using ProjectAbyss.Abilities;

/// <summary>Run growth counters persist across battles; zero thresholds stay undecided.</summary>
public sealed class YujinGrowthProgress
{
    private readonly AbilityStateStore state;
    private readonly AbilityScopeKey owner;
    public YujinGrowthProgress(AbilityStateStore state,string ownerId="player/yujin"){this.state=state??throw new ArgumentNullException(nameof(state));owner=AbilityScopeKey.ForRun(ownerId);}
    public long Progress(YujinGrowthAxis axis)=>state.Get(owner,"yujin.growth/"+axis+"/progress");
    public int Stage(YujinGrowthAxis axis)=>(int)Math.Max(1,state.Get(owner,"yujin.growth/"+axis+"/stage"));
    public void Record(YujinGrowthAxis axis,int amount)
    {if(amount>0)state.Set(owner,"yujin.growth/"+axis+"/progress",checked(Progress(axis)+amount));}
    public bool TryAdvance(YujinGrowthAxis axis,int required)
    {
        if(required<=0||Stage(axis)>=4||Progress(axis)<required)return false;
        state.Set(owner,"yujin.growth/"+axis+"/stage",Stage(axis)+1);
        state.Set(owner,"yujin.growth/"+axis+"/progress",0);return true;
    }
}
