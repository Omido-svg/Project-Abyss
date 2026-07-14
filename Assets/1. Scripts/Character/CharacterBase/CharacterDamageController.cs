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
            // 약화 부위 타격은 캐릭터 직접 피해다.
            // 파괴 가능 공격이면 사망 판정보다 먼저 부위 파괴를 완료해
            // 불사의 분노 등의 즉시 반응이 개입할 수 있게 한다.
            ApplyDirectDamage(
                request,
                checkDead: false);

            if (request.CanBreakPart)
            {
                bodyPartController?.TryBreakWeakenedPart(
                    targetPart,
                    owner.ActiveDamageContext?.Attacker,
                    owner.ActiveDamageContext?.Action);
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
            Debug.Log(
                $"{OwnerName()} {targetPart.Type} 부위는 이미 약화 상태입니다. " +
                $"{request.SourceEffect?.Name} 피해로 파괴되지 않습니다.");
            return;
        }

        int beforePartHP = Mathf.Max(
            0,
            Mathf.CeilToInt(targetPart.PartHP));

        int actualDamage = targetPart.ApplyDamage(
            request.Damage);

        if (actualDamage <= 0)
            return;

        owner.ReduceCurrentHP(actualDamage);

        Debug.Log(
            $"{OwnerName()}의 {targetPart.Type} 부위에 " +
            $"{request.SourceEffect?.Name} 피해 {actualDamage} " +
            $"HP : {beforePartHP} -> {targetPart.PartHP:0}");

        if (targetPart.PartHP <= 0f)
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

        // Normal 부위 피해는 실제 남은 부위 HP까지만 전체 HP에서 차감한다.
        // 초과분은 약화 부위 직접 피해 규칙을 우회해 넘기지 않는다.
        int actualDamage = targetPart.ApplyDamage(damage);

        if (actualDamage <= 0)
            return;

        owner.ReduceCurrentHP(actualDamage);

        Debug.Log(
            $"{OwnerName()}의 {targetPart.Type} 부위에 {actualDamage} 피해 " +
            $"HP : {beforePartHP} -> {targetPart.PartHP:0}");

        if (targetPart.PartHP <= 0f)
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
