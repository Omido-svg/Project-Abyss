using System;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    // R7-6 native authoring is intentionally independent of the historical
    // Yujin0916/0917 SkillEffectDefinition enum and its Apply() method.
    public enum R7YujinNativeOperation20261103
    {
        ConsumeTargetMarkForEnergy=1, AddOwnerBlock=2, AimWeaponCriticalRider=3,
        DesignationMomentumRider=4, SenseAfterSwitch=5, SpendSenseForForcedFront=6,
        WantedWeaponDeferredRider=7, ApplyDesignation=8, GainSense=9,
        CutThroatMark=10, SpreadRumorMark=11, WeaponDebuff=12,
        RegisterExplosionTrap=13, RegisterResonanceTrap=14, DesignateAllEnemies=15,
        PeriodSenseRider=16, ConsumeMarkForBlock=17, ConsumeMarkForSwift=18,
        ForceMarkIgnition=19, SpendSenseBundlesForPower=20
    }

    public enum R7YujinCondition20261103
    {
        None=0, ThisHitWeakened=1, ThisHitBroke=2,
        ThisHitKilled=3, ThisHitWeakenedOrBroke=4, Critical=5
    }

    [Serializable]
    public sealed class R7YujinNativeEntry20261103
    {
        public R7YujinNativeOperation20261103 Operation;
        public SkillEffectTiming Timing;
        public R7YujinCondition20261103 Condition;
        public bool RestrictToRoll;
        [Min(1)] public int RollNumber=1;
        [Min(0)] public int Amount=1;
        [Min(0)] public int AlternativeAmount;
        [Min(0)] public int Duration=1;
        // True means the source numerical parameters are historical PhaseE
        // content and NOT final 1029 canon (whose value is '미정').
        public bool ProvisionalBalance=true;
    }

    [CreateAssetMenu(menuName="Project Abyss/R7/Native Yujin Ability",
        fileName="R7NativeYujinAbility")]
    public sealed class R7NativeYujinAbilityDefinition20261103:ScriptableObject
    {
        public int SchemaVersion=1;
        public string StableId;
        public string SourceAssetName;
        public string SourceAssetGuid;
        public R7YujinNativeEntry20261103[] Operations=Array.Empty<R7YujinNativeEntry20261103>();

        public bool IsValid
        {
            get
            {
                if(SchemaVersion!=1||string.IsNullOrWhiteSpace(StableId)||
                   string.IsNullOrWhiteSpace(SourceAssetName)||
                   string.IsNullOrWhiteSpace(SourceAssetGuid)||
                   SourceAssetGuid.Length!=32||Operations==null||Operations.Length==0)
                    return false;
                foreach(var op in Operations)
                {
                    if(op==null||!Enum.IsDefined(typeof(R7YujinNativeOperation20261103),op.Operation)||
                       !Enum.IsDefined(typeof(R7YujinCondition20261103),op.Condition)||
                       op.RollNumber<1||op.Amount<0||op.AlternativeAmount<0||op.Duration<0)return false;
                    if(op.Timing!=SkillEffectTiming.OnExecute&&
                       op.Timing!=SkillEffectTiming.OnExchangeWin&&
                       op.Timing!=SkillEffectTiming.OnExchangeLose&&
                       op.Timing!=SkillEffectTiming.OnKill&&
                       op.Timing!=SkillEffectTiming.OnPartBroken&&
                       op.Timing!=SkillEffectTiming.OnPreparationActivated)
                        return false;
                }
                return true;
            }
        }
    }
}