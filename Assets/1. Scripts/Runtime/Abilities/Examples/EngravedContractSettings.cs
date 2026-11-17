using System;
using UnityEngine;
using ProjectAbyss.Abilities;

namespace ProjectAbyss.Examples
{
    // These are educational example values, not canonical Project Abyss balance.
    [CreateAssetMenu(menuName="Project Abyss/Examples/Engraved Contract Settings")]
    public sealed class EngravedContractSettings : ScriptableObject,IAbilityRuleSettingsDefinition
    {
        [Min(1)] public int MarkWithoutStartMark=4;
        [Min(1)] public int MarkWithStartMark=8;
        public IAbilityRuleSettings CaptureSettings()=>new EngravedContractSpec(
            MarkWithoutStartMark,MarkWithStartMark);
    }

    public sealed class EngravedContractSpec : IAbilityRuleSettings
    {
        public readonly int Unmarked,Marked;
        public EngravedContractSpec(int unmarked,int marked)
        {
            if(unmarked<=0||marked<=0)
                throw new ArgumentException("All educational skill amounts must be explicitly positive.");
            Unmarked=unmarked;Marked=marked;
        }
    }
}
