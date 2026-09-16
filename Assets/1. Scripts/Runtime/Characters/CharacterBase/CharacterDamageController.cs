using UnityEngine;

public sealed class CharacterDamageController
{
    private readonly Character owner;
    private readonly CharacterBodyPartController bodyPartController;

    public CharacterDamageController(
        Character owner,
        CharacterBodyPartController bodyPartController)
    {
        this.owner = owner;
        this.bodyPartController = bodyPartController;
    }

    public void TakeDamage(
        BodyPart targetPart,
        int damage,
        bool canBreakPart)
    {
        ApplyDamage(
            DamageRequest.SkillPart(
                targetPart,
                damage,
                canBreakPart));
    }

    public void TakeStatusPartDamage(
        BodyPart targetPart,
        int damage,
        StatusEffect sourceEffect)
    {
        ApplyDamage(
            DamageRequest.StatusPart(
                targetPart,
                damage,
                sourceEffect));
    }

    public void TakeTrueDamage(
        int damage,
        StatusEffect sourceEffect)
    {
        ApplyDamage(
            DamageRequest.True(
                damage,
                sourceEffect));
    }

    public void TakeDirectDamage(
        int damage,
        Character source = null,
        BattleAction sourceAction = null)
    {
        ApplyDamage(
            DamageRequest.Direct(
                source,
                owner,
                damage,
                sourceAction));
    }

    private void ApplyDamage(DamageRequest request)
    {
        if (!CanReceiveDamage(request))
            return;

        switch (request.Type)
        {
            case DamageType.SkillPart:
            case DamageType.Counter:
            case DamageType.Prestige:
            case DamageType.BleedExplosion:
                ApplySkillPartDamage(request);
                break;

            case DamageType.StatusPart:
                ApplyStatusPartDamage(request);
                break;

            case DamageType.True:
                ApplyTrueDamage(request);
                break;

            case DamageType.Direct:
            case DamageType.SelfCost:
            case DamageType.Execution:
            default:
                ApplyDirectDamage(request);
                break;
        }
    }

    private bool CanReceiveDamage(DamageRequest request)
    {
        if (owner == null ||
            owner.RuntimeStatus == null ||
            owner.IsDead ||
            request.Damage <= 0)
        {
            return false;
        }

        BodyPart part = request.TargetPart;

        if (part != null &&
            part.Owner != null &&
            part.Owner != owner)
        {
            return false;
        }

        return true;
    }

    private void ApplySkillPartDamage(DamageRequest request)
    {
        BodyPart targetPart = request.TargetPart;

        if (targetPart == null || targetPart.IsBroken)
        {
            ApplyDirectDamage(request);
            return;
        }

        if (targetPart.IsWeakened)
        {
            // P0 D-07 확정 불변식. 단 C-35 왕귀로 죽음의 저항이 해제된 대상은
            // 일반 타격도 0까지 내려가며 파괴 권한/선행 약화 게이트를 우회한다.
            owner.ReduceCurrentHP(request.Damage);

            bool weakenedDeathResistanceDisabled =
                owner.BattleContext?.Services?.EmotionRulebreakerService?
                    .IsDeathResistanceDisabled(owner) == true;

            if (weakenedDeathResistanceDisabled)
            {
                targetPart.ApplyDamageWithoutDeathResistance(request.Damage);
                if (targetPart.PartHP <= 0f)
                {
                    bodyPartController?.BreakPartIgnoringDeathResistance(
                        targetPart,
                        owner.ActiveDamageContext?.Attacker,
                        owner.ActiveDamageContext?.Action);
                }
            }
            else if (request.CanBreakPart)
            {
                targetPart.ApplyBreakAuthorityDamage(request.Damage);
                if (targetPart.PartHP <= 0f)
                {
                    bodyPartController?.TryBreakWeakenedPart(
                        targetPart,
                        owner.ActiveDamageContext?.Attacker,
                        owner.ActiveDamageContext?.Action);
                }
            }

            owner.CheckDead();
            return;
        }

        ApplyNormalPartDamage(
            targetPart,
            request.Damage);

        owner.CheckDead();
    }

    private void ApplyStatusPartDamage(DamageRequest request)
    {
        BodyPart targetPart = request.TargetPart;

        if (targetPart == null)
        {
            ApplyDirectDamage(request);
            return;
        }

        if (targetPart.IsBroken)
            return;

        if (targetPart.IsWeakened)
        {
            // 0915 C-13 기본은 1 유지. C-35 왕귀로 죽음의 저항이 해제되면
            // 상태 피해도 0까지 내려가 부위를 파괴할 수 있다.
            owner.ReduceCurrentHP(request.Damage);

            bool weakenedStatusDeathResistanceDisabled =
                owner.BattleContext?.Services?.EmotionRulebreakerService?
                    .IsDeathResistanceDisabled(owner) == true;

            if (weakenedStatusDeathResistanceDisabled)
            {
                targetPart.ApplyDamageWithoutDeathResistance(request.Damage);
                if (targetPart.PartHP <= 0f)
                {
                    bodyPartController?.BreakPartIgnoringDeathResistance(
                        targetPart,
                        owner.ActiveDamageContext?.Attacker,
                        owner.ActiveDamageContext?.Action);
                }
            }

            owner.CheckDead();
            return;
        }

        int beforePartHP = Mathf.Max(
            0,
            Mathf.CeilToInt(targetPart.PartHP));

        bool deathResistanceDisabled =
            owner.BattleContext?.Services?.EmotionRulebreakerService?
                .IsDeathResistanceDisabled(owner) == true;

        int actualDamage = deathResistanceDisabled
            ? targetPart.ApplyDamageWithoutDeathResistance(request.Damage)
            : targetPart.ApplyDamage(request.Damage);

        // 0915 C-12: Part clamp와 Whole HP는 서로 다른 장부다.
        // Part가 1에서 멈추더라도 Whole HP에는 요청 피해 전량을 적용한다.
        owner.ReduceCurrentHP(request.Damage);

        Debug.Log(
            $"{OwnerName()}의 {targetPart.Type} 부위에 " +
            $"{request.SourceEffect?.Name} 피해 {actualDamage} " +
            $"HP : {beforePartHP} -> {targetPart.PartHP:0}");

        if (deathResistanceDisabled && targetPart.PartHP <= 0f)
        {
            bodyPartController?.BreakPartIgnoringDeathResistance(
                targetPart,
                owner.ActiveDamageContext?.Attacker,
                owner.ActiveDamageContext?.Action);
        }
        else if (targetPart.PartHP <= 1f)
        {
            bodyPartController?.WeakenPart(
                targetPart,
                owner.ActiveDamageContext?.Attacker,
                owner.ActiveDamageContext?.Action);
        }

        owner.CheckDead();
    }

    private void ApplyDirectDamage(
        DamageRequest request,
        bool checkDead = true)
    {
        int beforeHp = owner.CurrentHP;

        owner.ReduceCurrentHP(request.Damage);

        int applied = Mathf.Max(
            0,
            beforeHp - owner.CurrentHP);

        Debug.Log(
            $"{OwnerName()}이 직접 피해 {applied}를 받음 " +
            $"HP : {beforeHp} -> {owner.CurrentHP}");

        if (checkDead)
            owner.CheckDead();
    }

    private void ApplyTrueDamage(DamageRequest request)
    {
        int beforeHp = owner.CurrentHP;
        owner.ReduceCurrentHP(request.Damage);

        int applied = Mathf.Max(
            0,
            beforeHp - owner.CurrentHP);

        Debug.Log(
            $"{OwnerName()}이 고정 피해 {applied}를 받음 " +
            $"HP : {beforeHp} -> {owner.CurrentHP}");

        owner.CheckDead();
    }

    private void ApplyNormalPartDamage(
        BodyPart targetPart,
        int damage)
    {
        if (targetPart == null || damage <= 0)
            return;

        int beforePartHP = Mathf.Max(
            0,
            Mathf.CeilToInt(targetPart.PartHP));

        bool deathResistanceDisabled =
            owner.BattleContext?.Services?.EmotionRulebreakerService?
                .IsDeathResistanceDisabled(owner) == true;

        // C-12 기본은 1 clamp. C-35 왕귀가 활성화된 대상만 0까지 허용한다.
        int actualDamage = deathResistanceDisabled
            ? targetPart.ApplyDamageWithoutDeathResistance(damage)
            : targetPart.ApplyDamage(damage);
        owner.ReduceCurrentHP(damage);

        Debug.Log(
            $"{OwnerName()}의 {targetPart.Type} 부위에 {actualDamage} 피해 " +
            $"HP : {beforePartHP} -> {targetPart.PartHP:0}");

        if (deathResistanceDisabled && targetPart.PartHP <= 0f)
        {
            bodyPartController?.BreakPartIgnoringDeathResistance(
                targetPart,
                owner.ActiveDamageContext?.Attacker,
                owner.ActiveDamageContext?.Action);
        }
        else if (targetPart.PartHP <= 1f)
        {
            bodyPartController?.WeakenPart(
                targetPart,
                owner.ActiveDamageContext?.Attacker,
                owner.ActiveDamageContext?.Action);
        }
    }

    private string OwnerName()
    {
        return owner?.Data?.CharacterName ??
               owner?.name ??
               "NULL";
    }
}
