using System;
using UnityEngine;
using ProjectAbyss.Abilities;

namespace ProjectAbyss.Examples
{
    /// <summary>Creates a disabled sample definition. No canonical assets are replaced or automatically enabled.</summary>
    public static class EngravedContractRules
    {
        public const string StableId="example.yujin.engraved_contract";
        public const string MarkRule="mark_on_win";
        public const string BurstRule="block_on_own_burst";
        public const string ReserveRule="reserve_energy_on_part_transition";
        public const string DeliveryRule="deliver_energy_next_turn";

        public static AbilityRuleSetDefinition Create(SkillDefinition source,string sourceGuid,
            EngravedContractSettings settings,out AbilityScheduleDefinition reservation,
            int blockOnBurst=3,int minimumAppliedHp=6,int energyNextTurn=1)
        {
            if(source==null||settings==null||string.IsNullOrWhiteSpace(sourceGuid))throw new ArgumentException("Source, GUID and settings are required.");
            settings.CaptureSettings();
            if(blockOnBurst<=0||minimumAppliedHp<=0||energyNextTurn<=0)throw new ArgumentException("Positive example rule amounts required.");
            var definition=SkillRules.CreateDraft(source,sourceGuid);
            definition.name="EngravedContractRules";
            var nextTurn=SkillRules.NextTurnReward(definition,DeliveryRule,AbilityEffects.Energy(energyNextTurn));
            reservation=nextTurn.Reservation;
            reservation.name="EngravedContractReservation";

            definition.Rules=new[]{
                // A: 승리 시 실행하되, 표식 분기는 원래 대상의 행동 시작 상태로 고정한다.
                SkillRules.OnExchangeWin(MarkRule,
                    AbilityEffects.Custom(EngravedContractModule.MarkOperation,settings:settings))
                    .AtActionStart(),

                // B: 이 스킬이 직접 일으킨 발화만 보상한다. 내부 이벤트 설정은 헬퍼가 채운다.
                SkillRules.OnOwnDirectMarkBurst(BurstRule,source.SkillId,AbilityEffects.Block(blockOnBurst))
                    .OncePerSkillExecution(),

                // C: 발화·덫 피해를 제외한 원래 승리 타격의 확정 결과로 예약한다.
                SkillRules.AfterWinningHit(ReserveRule,
                    AbilityConditions.All(AbilityConditions.AppliedHpAtLeast(minimumAppliedHp),
                        AbilityConditions.PartWeakenedOrBroken()),
                    nextTurn.Schedule)
                    .OncePerSkillExecution(consumeOnSchedule:true),

                // D: C가 예약할 때 고정한 대상과 스냅샷으로 다음 턴에 별도 실행한다.
                nextTurn.Delivery
            };
            return definition;
        }
    }
}
