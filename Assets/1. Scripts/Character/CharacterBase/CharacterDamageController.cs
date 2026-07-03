using UnityEngine;

public class CharacterDamageController
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

    //------------------------------------------------
    // 기존 외부 호출 유지
    //------------------------------------------------

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

    //------------------------------------------------
    // 피해 타입 라우터
    //------------------------------------------------

    private void ApplyDamage(DamageRequest request)
    {
        if (!CanReceiveDamage(request))
            return;

        switch (request.Type)
        {
            case DamageType.SkillPart:
                ApplySkillPartDamage(request);
                break;

            case DamageType.StatusPart:
                ApplyStatusPartDamage(request);
                break;

            case DamageType.Direct:
                ApplyDirectDamage(request);
                break;

            case DamageType.True:
                ApplyTrueDamage(request);
                break;
        }
    }

    //------------------------------------------------
    // 공통 검증
    //------------------------------------------------

    private bool CanReceiveDamage(DamageRequest request)
    {
        if (owner == null)
            return false;

        if (owner.IsDead)
            return false;

        if (request.Damage <= 0)
            return false;

        return true;
    }

    //------------------------------------------------
    // 1. 일반 스킬 부위 피해
    //------------------------------------------------
    // 규칙:
    // - targetPart == null      -> 직접 피해
    // - targetPart Broken      -> 직접 피해
    // - targetPart Weakened    -> 직접 피해
    //      - CanBreakPart true면 약화 부위 파괴 가능
    // - targetPart Normal      -> 부위 HP 감소
    //      - HP 0 이하가 되면 Weakened
    //      - Normal 상태에서 바로 Broken은 불가능
    //------------------------------------------------

    private void ApplySkillPartDamage(DamageRequest request)
    {
        BodyPart targetPart =
            request.TargetPart;

        if (targetPart == null)
        {
            ApplyDirectDamage(
                DamageRequest.Direct(request.Damage));

            return;
        }

        if (targetPart.IsBroken)
        {
            ApplyDirectDamage(
                DamageRequest.Direct(request.Damage));

            return;
        }

        if (targetPart.IsWeakened)
        {
            ApplyDirectDamage(
                DamageRequest.Direct(request.Damage));

            if (request.CanBreakPart)
            {
                bodyPartController.TryBreakWeakenedPart(
                    targetPart);
            }

            owner.CheckDead();
            return;
        }

        ApplyNormalPartDamage(
            targetPart,
            request.Damage);

        owner.CheckDead();
    }

    //------------------------------------------------
    // 2. 상태이상 부위 피해
    //------------------------------------------------
    // 규칙:
    // - 상태이상 피해는 Normal 부위를 Weakened로 만들 수 있음
    // - 상태이상 피해는 Broken을 만들 수 없음
    // - 이미 Weakened면 추가 파괴 없음
    // - Broken 부위에는 적용하지 않음
    //------------------------------------------------

    private void ApplyStatusPartDamage(DamageRequest request)
    {
        BodyPart targetPart =
            request.TargetPart;

        if (targetPart == null)
            return;

        if (targetPart.IsBroken)
            return;

        if (targetPart.IsWeakened)
        {
            Debug.Log(
                $"{owner.Data.CharacterName} {targetPart.Type} 부위는 이미 약화 상태입니다. " +
                $"{request.SourceEffect?.Name} 피해로 파괴되지 않습니다.");

            return;
        }

        int actualDamage =
            Mathf.Min(
                request.Damage,
                Mathf.CeilToInt(targetPart.PartHP));

        if (actualDamage <= 0)
            return;

        float beforePartHP =
            targetPart.PartHP;

        targetPart.PartHP =
            Mathf.Max(
                targetPart.PartHP - actualDamage,
                0f);

        owner.ReduceCurrentHP(
            actualDamage);

        Debug.Log(
            $"{owner.Data.CharacterName}의 {targetPart.Type} 부위에 " +
            $"{request.SourceEffect?.Name} 피해 {actualDamage} " +
            $"HP : {beforePartHP:0} -> {targetPart.PartHP:0}");

        if (targetPart.PartHP <= 0f)
        {
            bodyPartController.WeakenPart(
                targetPart);
        }

        owner.CheckDead();
    }

    //------------------------------------------------
    // 3. 직접 피해
    //------------------------------------------------
    // 규칙:
    // - 부위 상태와 무관하게 캐릭터 현재 HP 감소
    // - Broken 부위 타격, Weakened 부위 타격의 결과로도 사용됨
    //------------------------------------------------

    private void ApplyDirectDamage(DamageRequest request)
    {
        owner.ReduceCurrentHP(
            request.Damage);

        Debug.Log(
            $"{owner.Data.CharacterName}이 직접 피해 {request.Damage}를 받음");

        owner.CheckDead();
    }

    //------------------------------------------------
    // 4. 고정 피해
    //------------------------------------------------
    // 규칙:
    // - 캐릭터 현재 HP 직접 감소
    // - 상태이상 고정 피해, 특수 피해 등에 사용
    // - Direct와 분리해둔 이유:
    //   나중에 방어도, 저항, 피해 감소를 구분하기 위함
    //------------------------------------------------

    private void ApplyTrueDamage(DamageRequest request)
    {
        owner.ReduceCurrentHP(
            request.Damage);

        Debug.Log(
            $"{owner.Data.CharacterName}이 고정 피해 {request.Damage}를 받음");

        owner.CheckDead();
    }

    //------------------------------------------------
    // 일반 부위 HP 감소
    //------------------------------------------------

    private void ApplyNormalPartDamage(
        BodyPart targetPart,
        int damage)
    {
        if (targetPart == null)
            return;

        if (damage <= 0)
            return;

        owner.ReduceCurrentHP(
            damage);

        float beforePartHP =
            targetPart.PartHP;

        targetPart.PartHP =
            Mathf.Max(
                targetPart.PartHP - damage,
                0f);

        Debug.Log(
            $"{owner.Data.CharacterName}의 {targetPart.Type} 부위에 {damage} 피해 " +
            $"HP : {beforePartHP:0} -> {targetPart.PartHP:0}");

        if (targetPart.PartHP <= 0f)
        {
            bodyPartController.WeakenPart(
                targetPart);
        }
    }
}