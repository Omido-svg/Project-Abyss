using UnityEngine;

public class OlafImmortalFuryMechanic : CombatMechanic
{
    public override string MechanicName => "불사의 분노";

    private bool isActive;
    private int turnsLeft;

    public bool IsActive => isActive;
    public int TurnsLeft => turnsLeft;

    private const int Duration = 3;
    private const int SelfDamagePerTurn = 5;

    //------------------------------------------------
    // 등록 / 해제
    //------------------------------------------------

    public override void OnRegister()
    {
        if (battleEvent == null)
            return;

        battleEvent.OnBodyPartDestroyed += OnBodyPartDestroyed;
        battleEvent.OnTurnEnd += OnTurnEnd;
    }

    public override void OnUnregister()
    {
        if (battleEvent == null)
            return;

        battleEvent.OnBodyPartDestroyed -= OnBodyPartDestroyed;
        battleEvent.OnTurnEnd -= OnTurnEnd;
    }

    //------------------------------------------------
    // 부위 파괴 감지
    //------------------------------------------------

    private void OnBodyPartDestroyed(
        Character target,
        BodyPart part)
    {
        if (target == null || part == null)
            return;

        if (target != owner)
            return;

        TryEnterImmortalFury();
    }

    //------------------------------------------------
    // 불사의 분노 진입
    //------------------------------------------------

    private void TryEnterImmortalFury()
    {
        if (isActive)
            return;

        if (owner == null)
            return;

        int alivePartCount =
            CountAliveParts();

        if (alivePartCount != 1)
            return;

        isActive = true;
        turnsLeft = Duration;

        OlafMadnessMechanic madness =
            owner.GetMechanic<OlafMadnessMechanic>();

        if (madness != null)
        {
            madness.SetMadnessToMax();
        }

        Debug.Log(
            $"{owner.Data.CharacterName} 불사의 분노 발동! " +
            $"{turnsLeft}턴 동안 사망하지 않음");
    }

    //------------------------------------------------
    // 불사의 분노 중 행동 슬롯 정책
    //------------------------------------------------

    public override void ModifyActionSlotPolicy(
        ActionSlotPolicyContext context)
    {
        if (context == null)
            return;

        if (context.Owner != owner)
            return;

        if (!isActive)
            return;

        if (context.Part == null)
            return;

        if (context.Part.IsBroken)
            return;

        // 불사의 분노 중에는 남은 1개 부위로 2회 행동 가능
        context.MaxSlots =
            Mathf.Max(
                context.MaxSlots,
                2);
    }

    //------------------------------------------------
    // 불사의 분노 중 사망 방지
    //------------------------------------------------

    public override bool CanOwnerDie()
    {
        if (!isActive)
            return true;

        // 불사의 분노 중에는 HP가 0이어도 죽지 않음
        return false;
    }

    //------------------------------------------------
    // 턴 종료 처리
    //------------------------------------------------

    private void OnTurnEnd(int turn)
    {
        if (!isActive)
            return;

        if (owner == null)
            return;

        if (owner.IsDead)
            return;

        OlafMadnessMechanic madness =
            owner.GetMechanic<OlafMadnessMechanic>();

        if (madness != null)
        {
            madness.SetMadnessToMax();
        }

        owner.TakeTrueDamage(
            SelfDamagePerTurn,
            null);

        turnsLeft--;

        Debug.Log(
            $"{owner.Data.CharacterName} 불사의 분노 지속 중 : " +
            $"남은 턴 {turnsLeft}");

        if (turnsLeft <= 0)
        {
            Debug.Log(
                $"{owner.Data.CharacterName} 불사의 분노 종료 : 강제 사망");

            isActive = false;

            owner.Die();
        }
    }

    //------------------------------------------------

    private int CountAliveParts()
    {
        if (owner == null)
            return 0;

        int count = 0;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            if (!part.IsBroken)
                count++;
        }

        return count;
    }
}