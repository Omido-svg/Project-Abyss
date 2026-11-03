using System;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    // Gameplay operations authored directly into native SOs. The enum is NOT
    // the deprecated Olaf0922Phase7EffectOperation serialized type.
    public enum R7OlafNativeOperation20261103
    {
        RandomTargetDebuffNextTurn=1, TargetStatusNextTurn=2,
        OwnerStatusNextTurn=3, OwnerStatusImmediate=4,
        TargetBleeding=5, OwnerBleeding=6, OwnerBlock=7,
        ClearTargetBlock=8, BloodPriceStrengthByOwnerBleeding=9,
        ConsumeTargetBleedingForEnergy=10, PrepareBloodOathDuel=11,
        ResolveBloodOathDuelWin=12, AddMadness=13,
        StrengthByTargetBleedingBandNextTurn=14,
        TargetStatusNextTurnIfBleedingAtLeast=15,
        FortifyConsumeBleedingForStrength=16,
        FortifyConsumeBleedingForSwift=17,
        FortifyPrestigeIfBuffsPresent=18,
        PowerPerTargetNegativeStatusOnThirdRoll=19,
        TargetBleedingRandomOneOrTwo=20,
        GainEnergy=21,
        CalmMadnessHealAndPrestige=22,
        DevourStaggerHeal=23,
        OwnerStaggerDamage=24,
        PowerPerWeakenedOwnerPartEachRoll=25,
        MadnessGainWithRagnarokLoss=26,
        ConsumeOwnerBleedingForTurnPower=27,
        ProtectLowestHpOwnerPartForTurn=28,
        ScheduleNextTurnMaxSlotSpeed=29,
        ActivateAdjustedMaxSpeedIfLastStand=30,
        ChainExtraOneSidedAttackOnTransition=31
    }

    [Serializable]
    public sealed class R7OlafNativeEntry20261103
    {
        public R7OlafNativeOperation20261103 Operation;
        public SkillEffectTiming Timing;
        public bool RestrictToRoll;
        [Min(1)] public int RollNumber=1;
        public StatusEffectId StatusId;
        [Min(0)] public int Amount=1;
        public int Duration=1;
        [Min(0)] public int MinimumBleeding;
    }

    [CreateAssetMenu(menuName="Project Abyss/R7/Native Olaf Ability",
        fileName="R7NativeOlafAbility")]
    public sealed class R7NativeOlafAbilityDefinition20261103 : ScriptableObject
    {
        public int SchemaVersion=1;
        public string StableId;
        public string SourceAssetGuid;
        public string SourceAssetName;
        public R7OlafNativeEntry20261103[] Operations=Array.Empty<R7OlafNativeEntry20261103>();

        public bool IsValid
        {
            get
            {
                if(SchemaVersion!=1||string.IsNullOrWhiteSpace(StableId)||
                   string.IsNullOrWhiteSpace(SourceAssetName)||
                   string.IsNullOrWhiteSpace(SourceAssetGuid)||
                   SourceAssetGuid.Length!=32||Operations==null||Operations.Length==0)
                    return false;
                foreach(var e in Operations)
                {
                    if(e==null||!Enum.IsDefined(typeof(R7OlafNativeOperation20261103),e.Operation)||
                        e.RollNumber<1||e.Amount<0||e.MinimumBleeding<0||
                        e.Duration==0) return false;
                    if(e.Timing!=SkillEffectTiming.OnExecute&&
                       e.Timing!=SkillEffectTiming.OnExchangeWin&&
                       e.Timing!=SkillEffectTiming.OnExchangeLose&&
                       e.Timing!=SkillEffectTiming.OnDuelMatched&&
                       e.Timing!=SkillEffectTiming.OnPreparationActivated)
                        return false;
                }
                return true;
            }
        }
    }
}