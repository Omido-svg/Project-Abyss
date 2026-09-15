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
            // P0 D-07 확정 불변식:
            // 약화 상태는 회복으로 풀리지 않는다. 이후 타격은 전체 HP를 계속 깎고,
            // 파괴 권한이 있는 타격만 회복되어 올라간 부위 HP도 함께 깎아 0에서 파괴한다.
            owner.ReduceCurrentHP(request.Damage);

            if (request.CanBreakPart)
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
            // 0915 C-13: 약화 부위의 HP는 1에서 유지되지만
            // 같은 타격의 post-mitigation 요청 피해는 Whole HP에 계속 전량 반영한다.
            owner.ReduceCurrentHP(request.Damage);
            owner.CheckDead();
            return;
        }

        int beforePartHP = Mathf.Max(
            0,
            Mathf.CeilToInt(targetPart.PartHP));

        int actualDamage = targetPart.ApplyDamage(
            request.Damage);

        // 0915 C-12: Part clamp와 Whole HP는 서로 다른 장부다.
        // Part가 1에서 멈추더라도 Whole HP에는 요청 피해 전량을 적용한다.
        owner.ReduceCurrentHP(request.Damage);

        Debug.Log(
            $"{OwnerName()}의 {targetPart.Type} 부위에 " +
            $"{request.SourceEffect?.Name} 피해 {actualDamage} " +
            $"HP : {beforePartHP} -> {targetPart.PartHP:0}");

        if (targetPart.PartHP <= 1f)
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

        // 0915 C-12: 부위 장부는 1에서 clamp될 수 있지만 Whole HP 장부는
        // post-mitigation 요청 피해를 전량 받는다. 둘을 같은 actualDamage로 묶지 않는다.
        int actualDamage = targetPart.ApplyDamage(damage);
        owner.ReduceCurrentHP(damage);

        Debug.Log(
            $"{OwnerName()}의 {targetPart.Type} 부위에 {actualDamage} 피해 " +
            $"HP : {beforePartHP} -> {targetPart.PartHP:0}");

        if (targetPart.PartHP <= 1f)
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