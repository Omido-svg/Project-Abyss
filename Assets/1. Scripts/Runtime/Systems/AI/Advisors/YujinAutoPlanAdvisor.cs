using UnityEngine;

public sealed class YujinAutoPlanAdvisor : ICharacterAutoPlanAdvisor
{
    public bool Supports(Character character) => character is Yujin;

    public void PreparePlan(BattleContext context, Character character, PlayerAutoPlanMode mode)
    {
        Yujin yujin = character as Yujin;
        YujinMechanic mechanic = yujin?.YujinMechanic;
        if (mechanic == null)
            return;

        // 환형은 이제 전용 UI/직접 호출이 아니라 도사림 스킬로 예약한다.
        // AI의 실제 무기 전환은 아래 ScoreCandidate에서 환형 스킬을 선택하도록 유도한다.
        mechanic.AutoUseSense =
            mechanic.Sense > 0 &&
            (mode == PlayerAutoPlanMode.WinRate || mechanic.Sense >= 2);
    }

    public float ScoreCandidate(in AutoPlanCandidateContext context)
    {
        Yujin yujin = context.Owner as Yujin;
        YujinMechanic mechanic = yujin?.YujinMechanic;
        if (mechanic == null)
            return 0f;

        string id = context.SkillId;
        int mark = mechanic.GetMark(context.TargetPart);
        float score = 0f;

        if (context.TargetPart != null)
        {
            if (mechanic.CurrentWeapon == YujinWeaponType.Nakil &&
                context.TargetPart.IsWeakened)
                score += 1500f;

            if (mark >= 44)
                score += mechanic.CurrentWeapon == YujinWeaponType.Nakil ? 2200f : 650f;
            else
                score += mark * (context.Mode == PlayerAutoPlanMode.Damage ? 12f : 5f);
        }

        if (YujinMechanic.TryGetHwanhyeongWeapon(
                id,
                out YujinWeaponType skillWeapon))
        {
            YujinWeaponType desiredWeapon =
                ResolveDesiredWeapon(
                    context,
                    mechanic);

            score +=
                desiredWeapon == skillWeapon &&
                mechanic.CanSwitchWeapon(skillWeapon)
                    ? 1600f
                    : -800f;
        }
        else if (id == YujinSkillIds.Breakfast)
            score += mechanic.Sense <= 1 ? 520f : -120f;
        else if (id == YujinSkillIds.Inscription)
            score += mechanic.AutoUseSense && mechanic.Sense > 0 ? 700f : 160f;
        else if (id == YujinSkillIds.Pursuit)
            score += mechanic.Sense * 230f;
        else if (id == YujinSkillIds.Capture)
        {
            // 포착은 이번 턴 앞면 확률을 올려 모든 후속 코인 판정을 안정화한다.
            score += context.Mode == PlayerAutoPlanMode.WinRate
                ? 1800f
                : 650f;

            score +=
                (1f - mechanic.CurrentFrontChance) * 700f;
        }
        else if (id == YujinSkillIds.Sentencing)
        {
            // 양형은 뒷면 수치를 보강하므로 피해 우선 및 낙일의 낮은
            // 기본 앞면 확률을 보완할 때 가치가 높다.
            score += context.Mode == PlayerAutoPlanMode.Damage
                ? 1450f
                : 850f;

            if (mechanic.CurrentWeapon == YujinWeaponType.Nakil)
                score += 300f;
        }
        else if (id == YujinSkillIds.Brand)
            score += context.TargetPart != null && mark < 44 ? 900f : 180f;
        else if (id == YujinSkillIds.JointLiability)
            score += context.Mode == PlayerAutoPlanMode.WinRate ? 900f : 500f;
        else if (id == YujinSkillIds.Retrial)
            score += context.Mode == PlayerAutoPlanMode.WinRate ? 1050f : 420f;

        if (mechanic.CurrentWeapon == YujinWeaponType.Jeokseol &&
            context.Target?.BodyParts != null)
            score += 240f;

        return score;
    }

    private static YujinWeaponType ResolveDesiredWeapon(
        in AutoPlanCandidateContext context,
        YujinMechanic mechanic)
    {
        if (context.Mode == PlayerAutoPlanMode.WinRate)
            return YujinWeaponType.Baeku;

        if (context.TargetPart != null &&
            (context.TargetPart.IsWeakened || mechanic.GetMark(context.TargetPart) >= 28))
            return YujinWeaponType.Nakil;

        if (context.Target?.BodyParts != null && context.Target.BodyParts.Count > 2)
            return YujinWeaponType.Jeokseol;

        return YujinWeaponType.Nakil;
    }

    public string GetSummary(Character character)
    {
        YujinMechanic mechanic = (character as Yujin)?.YujinMechanic;
        if (mechanic == null)
            return string.Empty;

        string weapon =
            mechanic.HasPendingWeapon
                ? $"{mechanic.CurrentWeapon}→{mechanic.PendingWeapon}"
                : mechanic.CurrentWeapon.ToString();

        return
            $"{weapon} / 감 자동 {(mechanic.AutoUseSense ? "ON" : "OFF")} / 감 {mechanic.Sense}";
    }
}
