using UnityEngine;

/// <summary>
/// 히후미 자동계획 가중치.
/// 테스트 전투/Character Verification의 자동 전투가 히후미의 뼈·반격 루프를
/// 실제로 사용하도록 하는 최소 캐릭터 전용 Advisor다.
/// </summary>
public sealed class HifumiAutoPlanAdvisor : ICharacterAutoPlanAdvisor
{
    public bool Supports(Character character) => character is Hifumi;

    public void PreparePlan(
        BattleContext context,
        Character character,
        PlayerAutoPlanMode mode)
    {
        // 히후미는 유진처럼 계획 단계에 별도 즉시 토글해야 하는 캐릭터 상태가 없다.
        // 도사림/위세 선택은 Candidate Score로 처리한다.
    }

    public float ScoreCandidate(in AutoPlanCandidateContext context)
    {
        Hifumi hifumi = context.Owner as Hifumi;
        HifumiMechanic mechanic = hifumi?.HifumiMechanic;

        if (hifumi == null || mechanic == null)
            return 0f;

        string id = context.SkillId;
        int bone = mechanic.Bone;
        int band = mechanic.BoneBand;
        float hpRatio =
            hifumi.MaxCombatHP > 0
                ? (float)hifumi.CurrentHP / hifumi.MaxCombatHP
                : 1f;

        float score = 0f;

        // -------------------------
        // 일반공격
        // -------------------------
        if (id == HifumiSkillIds.SmallChange)
        {
            score += bone < 40 ? 450f : 120f;
        }
        else if (id == HifumiSkillIds.BoldJudgment)
        {
            score += bone >= 40 ? 520f : 130f;
            if (context.OpposingSlot?.Skill != null &&
                (context.OpposingSlot.Skill.ActionType == ActionType.NormalAttack ||
                 context.OpposingSlot.Skill.ActionType == ActionType.Duel))
            {
                score += 260f;
            }
        }

        // -------------------------
        // 결투
        // -------------------------
        else if (id == HifumiSkillIds.Yukcham)
        {
            // 상시 연료/반격 엔진. 자동전투의 기본 결투 우선순위.
            score += 850f;
            score += Mathf.Max(0, 4 - band) * 90f;
        }
        else if (id == HifumiSkillIds.Goldan)
        {
            // 만개 방출. 500이 아닐 때는 과도한 도배를 막는다.
            if (mechanic.IsBloom)
            {
                score += 2800f;

                if (context.TargetPart != null &&
                    context.TargetPart.IsWeakened)
                {
                    score += 1800f;
                }
            }
            else
            {
                score -= 700f;
                score += band * 80f;
            }
        }
        else if (id == HifumiSkillIds.RecklessBet)
        {
            score += bone < 70 ? 520f : 80f;
            if (hpRatio < 0.35f)
                score -= 300f;
        }

        // -------------------------
        // 도사림
        // 자동계획은 테스트/회귀용이므로 매 턴 도사림 도배를 피하게 강한 조건부 점수를 사용한다.
        // -------------------------
        else if (id == HifumiSkillIds.PokerFace)
        {
            if (hpRatio <= 0.45f)
                score += 2100f;
            else if (bone < 150)
                score += 700f;
            else
                score -= 1000f;
        }
        else if (id == HifumiSkillIds.EngraveBone)
        {
            score += bone < 300 ? 1150f : -850f;
        }
        else if (id == HifumiSkillIds.GiveFlesh)
        {
            score += bone <= 300 ? 900f : -1200f;
            if (hpRatio <= 0.3f)
                score -= 500f;
        }
        else if (id == HifumiSkillIds.FoldHand)
        {
            if (bone >= 100 && hpRatio <= 0.32f)
                score += 2600f;
            else
                score -= 1600f;
        }

        // -------------------------
        // 위세
        // -------------------------
        else if (id == HifumiSkillIds.GamblerMove)
        {
            // 만개 직전/만개에서는 반격 자원을 지우므로 피한다.
            if (bone >= 200 && !mechanic.IsBloom)
                score += context.Mode == PlayerAutoPlanMode.Damage ? 1200f : 350f;
            else
                score -= 1700f;
        }
        else if (id == HifumiSkillIds.AllIn)
        {
            if (bone >= 100 && bone < HifumiMechanic.BloomThreshold)
                score += context.Mode == PlayerAutoPlanMode.Damage ? 650f : -50f;
            else
                score -= 1300f;
        }
        else if (id == HifumiSkillIds.Trick)
        {
            score += context.Mode == PlayerAutoPlanMode.WinRate ? 1450f : 900f;
        }

        return score;
    }

    public string GetSummary(Character character)
    {
        HifumiMechanic mechanic =
            (character as Hifumi)?.HifumiMechanic;

        return mechanic == null
            ? string.Empty
            : $"뼈 {mechanic.Bone}/{HifumiMechanic.MaxBone}" +
              (mechanic.IsBloom ? " / 만개" : string.Empty) +
              $" / 구간 {mechanic.BoneBand}";
    }
}
