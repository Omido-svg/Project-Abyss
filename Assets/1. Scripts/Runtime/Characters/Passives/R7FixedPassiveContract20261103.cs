using System;
using System.Collections.Generic;

/// <summary>
/// R7-9 / 1029 §§13.1-13.3, 17.1, 18.2, 19.4.
/// Immutable code-defined passives: no SkillEffectDefinition adapter,
/// no equip/unequip state and no mutable ScriptableObject combat state.
/// </summary>
public static class R7FixedPassiveContract20261103
{
    public readonly struct Entry
    {
        public readonly string CharacterId;
        public readonly string PassiveName;
        public readonly Type OwnerType;
        public readonly Type MechanicType;
        public Entry(string id,string name,Type owner,Type mechanic)
        {
            CharacterId=id;
            PassiveName=name;
            OwnerType=owner;
            MechanicType=mechanic;
        }
    }
    private static readonly Entry[] Registry =
    {
        new Entry("olaf","절름발이도 말을 탄다",
            typeof(Olaf),typeof(OlafMadnessMechanic)),
        new Entry("yujin","군자불어괴력난신",
            typeof(Yujin),typeof(YujinMechanic)),
        new Entry("hifumi","몸을 버려야 떠오를 여울도 있다",
            typeof(Hifumi),typeof(HifumiMechanic))
    };
    public static IReadOnlyList<Entry> FixedPassives=>Registry;
    public const int YujinTurnSenseGain=1;

    // "적중" requires an actual applied ATTACK-damage context. A won
    // defensive roll or a cancelled/tie/empty exchange is not a hit.
    public static bool IsOlafAttackHit(
        ClashExchangeResult result,BattleAction owned,DamageContext damage)
    {
        if(result==null||owned?.Owner==null||damage==null||
           result.WasCancelled||result.IsContentError||result.IsTie||
           result.WinnerAction!=owned||
           owned.CurrentRollType!=CombatRollType.Attack||
           (owned.ActionType!=ActionType.Duel&&
            owned.ActionType!=ActionType.NormalAttack))
            return false;
        return damage.WasApplied && damage.Attacker==owned.Owner &&
               damage.Target!=null && damage.Target!=owned.Owner;
    }

    // 1029 uses "승리시" for BLUE normal, not one-sided damage success.
    public static bool IsOlafBlueNormalWin(
        ClashExchangeResult result,BattleAction owned)
    {
        return result!=null&&owned!=null&&
            !result.WasCancelled&&!result.IsContentError&&!result.IsTie&&
            !result.IsOneSided&&result.WinnerAction==owned&&
            owned.ActionType==ActionType.NormalAttack&&
            owned.Skill?.IsBlue==true;
    }

    public static bool IsOlafDuelLoss(
        ClashExchangeResult result,BattleAction owned,BattleAction opponent)
    {
        return result!=null&&owned!=null&&opponent!=null&&
            !result.WasCancelled&&!result.IsContentError&&!result.IsTie&&
            !result.IsOneSided&&result.IsDuelExchange&&
            result.LoserAction==owned&&
            owned.ActionType==ActionType.Duel&&
            opponent.ActionType==ActionType.Duel;
    }

    // Only a newly broken OWN or opposing body part can grant +1 Madness.
    public static bool IsOlafRelevantPartBreak(
        BodyPartBreakEventContext result,Character owner,
        BattleContext battle)
    {
        if(owner==null||result?.Target==null||result.Part==null||
           result.StateAfter!=BodyPartState.Broken||
           result.StateBefore==BodyPartState.Broken)
            return false;
        return result.Target==owner||
            battle?.Enemies?.Contains(result.Target)==true;
    }
}