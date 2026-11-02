using System;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    public enum R7SingleEffectKind { GainBlock=1, GainPrestige=2, GainClashPower=3, ApplyCanonicalStatus=4 }
    public enum R7SingleTarget { Owner=1, CurrentTarget=2 }

    /// <summary>
    /// R7-3 immutable single-effect authoring. Original SkillEffectEntry remains
    /// only UI diagnostic metadata; no TryApply, override or legacy Apply calls.
    /// No character-specialist keyword or mixed/damage operation is allowed.
    /// </summary>
    [CreateAssetMenu(menuName="Project Abyss/R7/Native Single Effect",
        fileName="R7NativeSingleEffect")]
    public sealed class R7NativeSingleEffectDefinition20261102 : ScriptableObject
    {
        public int SchemaVersion=1;
        public string StableId;
        public string SourceAssetGuid;
        public string SourceAssetName;
        public AbilityEventKind Trigger;
        public R7SingleEffectKind Kind;
        public R7SingleTarget Target=R7SingleTarget.Owner;
        public StatusEffectId Status;
        [Min(1)] public int Amount=1;
        public int Duration=1;
        public bool NextTurn;
        public bool RestrictToRoll;
        [Min(1)] public int RollNumber=1;

        public bool IsValid
        {
            get
            {
                if(SchemaVersion!=1||string.IsNullOrWhiteSpace(StableId)||
                    string.IsNullOrWhiteSpace(SourceAssetGuid)||
                    string.IsNullOrWhiteSpace(SourceAssetName)||
                    Amount<=0||RollNumber<1||
                    (Trigger!=AbilityEventKind.RollUsing&&
                     Trigger!=AbilityEventKind.ExchangeWon))return false;
                if(Kind==R7SingleEffectKind.ApplyCanonicalStatus)
                    return (Target==R7SingleTarget.Owner||
                            Target==R7SingleTarget.CurrentTarget)&&
                        (Duration>=1||Duration==StatusEffect.InfiniteDuration)&&
                        Status!=StatusEffectId.Fear&&
                        Status!=StatusEffectId.Designation&&
                        StatusEffectFactory.IsCanonicalGenericAuthorable(Status);
                return !NextTurn&&Target==R7SingleTarget.Owner&&Duration==1&&
                    (Kind==R7SingleEffectKind.GainBlock||
                     Kind==R7SingleEffectKind.GainPrestige||
                     Kind==R7SingleEffectKind.GainClashPower);
            }
        }
    }
}