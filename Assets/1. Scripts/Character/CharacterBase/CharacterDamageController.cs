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

    public void TakeDamage(
        BodyPart targetPart,
        int damage,
        bool canBreakPart)
    {
        if (owner == null)
            return;

        if (owner.IsDead)
            return;

        if (damage <= 0)
            return;

        if (targetPart == null)
        {
            TakeDirectDamage(damage);
            return;
        }

        if (targetPart.IsBroken)
        {
            TakeDirectDamage(damage);
            return;
        }

        if (targetPart.IsWeakened)
        {
            TakeDirectDamage(damage);

            if (canBreakPart)
            {
                bodyPartController.TryBreakWeakenedPart(
                    targetPart);
            }

            owner.CheckDead();
            return;
        }

        ApplyNormalPartDamage(
            targetPart,
            damage);

        owner.CheckDead();
    }

    public void TakeStatusPartDamage(
        BodyPart targetPart,
        int damage,
        StatusEffect sourceEffect)
    {
        if (owner == null)
            return;

        if (owner.IsDead)
            return;

        if (targetPart == null)
            return;

        if (damage <= 0)
            return;

        if (targetPart.IsBroken)
            return;

        if (targetPart.IsWeakened)
        {
            Debug.Log(
                $"{owner.Data.CharacterName} {targetPart.Type} 부위는 이미 약화 상태입니다. " +
                $"{sourceEffect?.Name} 피해로 파괴되지 않습니다.");

            return;
        }

        int actualDamage =
            Mathf.Min(
                damage,
                Mathf.CeilToInt(targetPart.PartHP));

        if (actualDamage <= 0)
            return;

        float beforePartHP =
            targetPart.PartHP;

        targetPart.PartHP =
            Mathf.Max(
                targetPart.PartHP - actualDamage,
                0f);

        owner.ReduceCurrentHP(actualDamage);

        Debug.Log(
            $"{owner.Data.CharacterName}의 {targetPart.Type} 부위에 " +
            $"{sourceEffect?.Name} 피해 {actualDamage} " +
            $"HP : {beforePartHP:0} -> {targetPart.PartHP:0}");

        if (targetPart.PartHP <= 0f)
        {
            bodyPartController.WeakenPart(
                targetPart);
        }

        owner.CheckDead();
    }

    public void TakeTrueDamage(
        int damage,
        StatusEffect sourceEffect)
    {
        if (owner == null)
            return;

        if (owner.IsDead)
            return;

        if (damage <= 0)
            return;

        owner.ReduceCurrentHP(damage);

        Debug.Log(
            $"{owner.Data.CharacterName}이 고정 피해 {damage}를 받음");

        owner.CheckDead();
    }

    private void TakeDirectDamage(int damage)
    {
        if (damage <= 0)
            return;

        owner.ReduceCurrentHP(damage);

        Debug.Log(
            $"{owner.Data.CharacterName}이 직접 피해 {damage}를 받음");

        owner.CheckDead();
    }

    private void ApplyNormalPartDamage(
        BodyPart targetPart,
        int damage)
    {
        if (targetPart == null)
            return;

        if (damage <= 0)
            return;

        owner.ReduceCurrentHP(damage);

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