using System;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    public enum SingleEffectKind { GainBlock=1, GainPrestige=2, GainClashPower=3, ApplyCanonicalStatus=4 }
    public enum NativeSingleEffectTarget { Owner=1, CurrentTarget=2 }

    /// <summary>
    /// R7-3 immutable single-effect authoring. Original SkillEffectEntry remains
    /// only UI diagnostic metadata; no TryApply, override or legacy Apply calls.
    /// No character-specialist keyword or mixed/damage operation is allowed.
    /// </summary>
    [CreateAssetMenu(menuName="Project Abyss/R7/Native Single Effect",
        fileName="R7NativeSingleEffect")]
    public sealed class NativeSingleEffectDefinition : ScriptableObject
    {
        public int SchemaVersion=1;
        public string StableId;
        public string SourceAssetGuid;
        public string SourceAssetName;
        public AbilityEventKind Trigger;
        public SingleEffectKind Kind;
        public NativeSingleEffectTarget Target=NativeSingleEffectTarget.Owner;
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
                if(Kind==SingleEffectKind.ApplyCanonicalStatus)
                    return (Target==NativeSingleEffectTarget.Owner||
                            Target==NativeSingleEffectTarget.CurrentTarget)&&
                        (Duration>=1||Duration==StatusEffect.InfiniteDuration)&&
                        Status!=StatusEffectId.Fear&&
                        Status!=StatusEffectId.Designation&&
                        StatusEffectFactory.IsCanonicalGenericAuthorable(Status);
                return !NextTurn&&Target==NativeSingleEffectTarget.Owner&&Duration==1&&
                    (Kind==SingleEffectKind.GainBlock||
                     Kind==SingleEffectKind.GainPrestige||
                     Kind==SingleEffectKind.GainClashPower);
            }
        }
    }
}