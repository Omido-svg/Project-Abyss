using System.Collections.Generic;
using UnityEngine;

public class OlafMadnessMechanic : CombatMechanic
{
    private int madness;

    public int Madness => madness;
    public int MaxMadness => 5;

    public override string MechanicName => "광전사의 광기";

    private const int DuelLoseMadnessGain = 2;
    private const int PartBreakMadnessGain = 1;

    private const int NormalBleedAmount = 1;
    private const int MaxMadnessNormalBleedAmount = 2;
    private const int DuelWinBleedAmount = 2;
    private const int ImmortalFuryBleedBonus = 2;

    private const int DuelPushBonus = 10;

    private const int BleedExplosionDamagePerStack = 5;
    private const int PrestigeExtraDamagePerMadness = 5;
    
    private int suppressPartBreakMadnessDepth;

    //------------------------------------------------
    // 등록 / 해제
    //------------------------------------------------

    public override void OnRegister()
    {
        if (battleEvent == null)
            return;

        battleEvent.OnClashWin += OnClashWin;
        battleEvent.OnClashLose += OnClashLose;
        battleEvent.OnBodyPartDestroyed += OnBodyPartDestroyed;
        battleEvent.OnKill += OnKill;
    }

    public override void OnUnregister()
    {
        if (battleEvent == null)
            return;

        battleEvent.OnClashWin -= OnClashWin;
        battleEvent.OnClashLose -= OnClashLose;
        battleEvent.OnBodyPartDestroyed -= OnBodyPartDestroyed;
        battleEvent.OnKill -= OnKill;
    }

    //------------------------------------------------
    // 결투 승리
    //------------------------------------------------

    private void OnClashWin(
        BattleAction winnerAction,
        BattleAction loserAction)
    {
        if (winnerAction == null)
            return;

        if (winnerAction.Owner != owner)
            return;

        if (winnerAction.Skill == null)
            return;

        if (winnerAction.Skill.ActionType != ActionType.Duel)
            return;

        ApplyDuelWinEffect(winnerAction);
    }

    private void ApplyDuelWinEffect(
        BattleAction action)
    {
        if (action == null)
            return;

        if (action.Target == null)
            return;

        int bleedAmount =
            GetDuelWinBleedAmount();

        action.Target.AddStatus(
            new Bleeding(bleedAmount),
            owner);

        Debug.Log(
            $"{owner.Data.CharacterName} 결투 승리 효과 : 출혈 {bleedAmount} 부여");

        TryExplodeBleedingByDuel(action);
    }

    //------------------------------------------------
    // 결투 패배
    //------------------------------------------------

    private void OnClashLose(
        BattleAction loserAction,
        BattleAction winnerAction)
    {
        if (loserAction == null)
            return;

        if (loserAction.Owner != owner)
            return;

        if (loserAction.Skill == null)
            return;

        if (loserAction.Skill.ActionType != ActionType.Duel)
            return;

        AddMadness(DuelLoseMadnessGain);

        Debug.Log(
            $"{owner.Data.CharacterName} 결투 패배 : 광기 {DuelLoseMadnessGain} 증가");
    }

    //------------------------------------------------
    // 부위 파괴
    //------------------------------------------------

    private void OnBodyPartDestroyed(
        Character target,
        BodyPart part)
    {
        if (target == null || part == null)
            return;

        //--------------------------------
        // 위세 처리 중 발생한 부위 파괴는
        // 광기 증가로 치지 않는다.
        //--------------------------------

        if (suppressPartBreakMadnessDepth > 0)
        {
            Debug.Log(
                $"{owner.Data.CharacterName} 위세 처리 중 부위 파괴 : 광기 증가 무시");

            return;
        }

        //--------------------------------
        // 일반적인 부위 파괴는 광기 증가
        //--------------------------------

        AddMadness(
            PartBreakMadnessGain);
    }

    //------------------------------------------------
    // 처치
    //------------------------------------------------

    private void OnKill(
        Character killer,
        Character victim)
    {
        if (killer != owner)
            return;

        if (!IsMaxMadness())
            return;

        RecoverTwoBrokenOrWeakenedParts();
        ResetMadness();

        Debug.Log(
            $"{owner.Data.CharacterName} 광기 5스택 처치 보상 : 부위 2개 회복");
    }

    //------------------------------------------------
    // 출혈 폭발
    //------------------------------------------------

    private void TryExplodeBleedingByDuel(
        BattleAction action)
    {
        if (action == null)
            return;

        if (action.Target == null)
            return;

        if (action.TargetPart == null)
            return;

        Bleeding bleeding =
            action.Target.GetStatus<Bleeding>();

        if (bleeding == null)
            return;

        if (!bleeding.CanExplode)
            return;

        int explosionDamage =
            bleeding.Stack * BleedExplosionDamagePerStack;

        action.Target.TakeTrueDamage(
            explosionDamage,
            bleeding);

        action.Target.ForceBreakPart(
            action.TargetPart);

        action.Target.RemoveStatus(
            bleeding);

        Debug.Log(
            $"{owner.Data.CharacterName} 출혈 폭발 : " +
            $"{action.Target.Data.CharacterName} {action.TargetPart.Type} 파괴");
    }

    //------------------------------------------------
    // 회복
    //------------------------------------------------

    private void RecoverTwoBrokenOrWeakenedParts()
    {
        if (owner == null)
            return;

        List<BodyPart> candidates = new();

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            if (part.IsBroken || part.IsWeakened)
                candidates.Add(part);
        }

        candidates.Sort(
            (a, b) => a.PartHP.CompareTo(b.PartHP));

        int recoverCount =
            Mathf.Min(2, candidates.Count);

        for (int i = 0; i < recoverCount; i++)
        {
            BodyPart part = candidates[i];

            part.Recover();

            if (battleEvent != null)
            {
                battleEvent.RaiseBodyPartRecovered(
                    owner,
                    part);
            }
        }

        owner.ForceRecalculateHP();
    }

    //------------------------------------------------
    // 외부에서 사용하는 값
    //------------------------------------------------

    public int GetNormalAttackBleedAmount()
    {
        int amount =
            IsMaxMadness()
                ? MaxMadnessNormalBleedAmount
                : NormalBleedAmount;

        if (IsImmortalFuryActive())
            amount += ImmortalFuryBleedBonus;

        return amount;
    }

    public int GetDuelWinBleedAmount()
    {
        int amount = DuelWinBleedAmount;

        if (IsImmortalFuryActive())
            amount += ImmortalFuryBleedBonus;

        return amount;
    }

    public int GetDuelPushBonus()
    {
        return DuelPushBonus + madness * 2;
    }

    public int ConsumeMadnessForPrestigeDamage()
    {
        int damage =
            madness * PrestigeExtraDamagePerMadness;

        ResetMadness();

        return damage;
    }

    //------------------------------------------------
    // 광기
    //------------------------------------------------

    public void AddMadness(int amount)
    {
        if (amount <= 0)
            return;

        madness += amount;

        if (madness > MaxMadness)
            madness = MaxMadness;

        Debug.Log(
            $"{owner.Data.CharacterName} 광기 증가 : {madness}/{MaxMadness}");
    }

    public void ResetMadness()
    {
        madness = 0;
        Debug.Log(
            $"{owner.Data.CharacterName} 광기 초기화 : {madness}/{MaxMadness}");
    }

    public bool IsMaxMadness()
    {
        return madness >= MaxMadness;
    }
    
    public void SetMadnessToMax()
    {
        madness = MaxMadness;

        Debug.Log(
            $"{owner.Data.CharacterName} 광기 최대치 : {madness}/{MaxMadness}");
    }
    
    private bool IsImmortalFuryActive()
    {
        OlafImmortalFuryMechanic fury =
            owner.GetMechanic<OlafImmortalFuryMechanic>();

        return fury != null && fury.IsActive;
    }
    
    public void BeginSuppressPartBreakMadness()
    {
        suppressPartBreakMadnessDepth++;
    }

    public void EndSuppressPartBreakMadness()
    {
        suppressPartBreakMadnessDepth =
            Mathf.Max(0, suppressPartBreakMadnessDepth - 1);
    }
}