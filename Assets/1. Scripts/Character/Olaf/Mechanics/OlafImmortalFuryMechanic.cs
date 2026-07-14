using UnityEngine;

public class OlafImmortalFuryMechanic :
    CombatMechanic,
    IBodyPartBreakImmediateReaction
{
    public override string MechanicName =>
        "불사의 분노";

    private const int Duration = 3;
    private const int SelfDamagePerTurn = 5;

    private bool isActive;
    private int turnsLeft;

    public bool IsActive => isActive;
    public int TurnsLeft => turnsLeft;

    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnTurnEnd += OnTurnEnd,
            () => battleEvent.OnTurnEnd -= OnTurnEnd,
            "OnTurnEnd");
    }

    public override void OnUnregister()
    {
        isActive = false;
        turnsLeft = 0;
    }

    // 마지막에서 두 번째 부위가 파괴된 직후,
    // CharacterLifeController의 사망 판정보다 먼저 호출된다.
    public void OnBodyPartBrokenBeforeDeath(
        BodyPartBreakEventContext context)
    {
        if (context?.Target != owner ||
            context.Part == null ||
            owner == null ||
            owner.IsDead)
        {
            return;
        }

        TryEnterImmortalFury();
    }

    private void TryEnterImmortalFury()
    {
        if (isActive || owner == null)
            return;

        if (CountAliveParts() != 1)
            return;

        isActive = true;
        turnsLeft = Duration;

        owner.GetMechanic<OlafMadnessMechanic>()
            ?.SetMadnessToMax();

        Debug.Log(
            $"{owner.Data.CharacterName} 불사의 분노 발동! " +
            $"{turnsLeft}턴 동안 사망하지 않음");
    }

    public override void ModifyActionSlotPolicy(
        ActionSlotPolicyContext context)
    {
        if (context == null ||
            context.Owner != owner ||
            !isActive ||
            context.Part == null ||
            context.Part.IsBroken)
        {
            return;
        }

        context.MaxSlots =
            Mathf.Max(
                context.MaxSlots,
                2);
    }

    public override bool CanOwnerDie()
    {
        return !isActive;
    }

    private void OnTurnEnd(int turn)
    {
        if (!isActive ||
            owner == null ||
            owner.IsDead)
        {
            return;
        }

        owner.GetMechanic<OlafMadnessMechanic>()
            ?.SetMadnessToMax();

        // 자해도 DamageRequest → DamageContext → 이벤트 경로를 탄다.
        OlafCombatPipeline.ApplyDamage(
            owner,
            DamageRequest.SelfCost(
                owner,
                SelfDamagePerTurn));

        turnsLeft =
            Mathf.Max(0, turnsLeft - 1);

        Debug.Log(
            $"{owner.Data.CharacterName} 불사의 분노 지속 중 : " +
            $"남은 턴 {turnsLeft}");

        if (turnsLeft > 0)
            return;

        Debug.Log(
            $"{owner.Data.CharacterName} 불사의 분노 종료 : 강제 사망");

        // 먼저 비활성화해야 CanOwnerDie()가 true가 된다.
        isActive = false;

        owner.BattleContext?.EffectResolver?.ForceKill(
            EffectRequest.ForceKill(
                owner,
                owner));
    }

    private int CountAliveParts()
    {
        if (owner?.BodyParts == null)
            return 0;

        int count = 0;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part != null &&
                !part.IsBroken)
            {
                count++;
            }
        }

        return count;
    }
}
