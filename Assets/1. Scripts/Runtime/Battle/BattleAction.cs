using System.Collections.Generic;
using UnityEngine;

public enum ActionType
{
    NormalAttack,
    Duel,
    Preparation,
    Prestige
}

public class BattleAction
{
    public ActionSlot Slot;

    private Character resolutionTarget;
    private BodyPart resolutionTargetPart;
    private bool hasResolutionTargetOverride;

    public long ActionId => Slot == null ? 0 : Slot.ActionId;
    public int ActionIndex => Slot == null ? 0 : Slot.ActionIndex;
    public Character Owner => Slot == null ? null : Slot.Owner;

    /// <summary>
    /// 행동을 계획할 때 선택했던 원래 대상입니다.
    /// 합에 끌려 들어간 행동은 실제 교환 상대와 다를 수 있습니다.
    /// </summary>
    public Character DeclaredTarget =>
        Slot == null
            ? null
            : Slot.TargetCharacter;

    public BodyPart DeclaredTargetPart =>
        Slot == null
            ? null
            : Slot.TargetPart;

    /// <summary>
    /// 현재 해석에서 실제 효과와 피해를 받을 대상입니다.
    /// 일반·일방 행동에서는 계획 대상이고,
    /// 합에서는 대상 Character를 상대 BattleAction의 Owner로 고정합니다.
    /// 단, 처음부터 그 상대를 공격하도록 계획한 행동의 TargetPart는 그대로 보존합니다.
    /// TargetSlot은 합 상대를, TargetPart는 실제 피해 부위를 나타내는 독립 계약입니다.
    /// </summary>
    public Character Target =>
        hasResolutionTargetOverride
            ? resolutionTarget
            : DeclaredTarget;

    public BodyPart OwnerPart => Slot == null ? null : Slot.Part;

    public BodyPart TargetPart =>
        hasResolutionTargetOverride
            ? resolutionTargetPart
            : DeclaredTargetPart;

    public bool HasResolutionTargetOverride =>
        hasResolutionTargetOverride;

    public bool WasRedirectedByClash =>
        hasResolutionTargetOverride &&
        (resolutionTarget != DeclaredTarget ||
         resolutionTargetPart != DeclaredTargetPart);

    public Skill Skill => Slot == null ? null : Slot.Skill;
    public int Speed => Slot == null ? 0 : Slot.Speed;
    public ActionPhase Phase => Slot == null ? ActionPhase.COMBAT : Slot.Phase;

    public ActionType ActionType =>
        Skill == null
            ? ActionType.NormalAttack
            : Skill.ActionType;

    public int RolledPower;
    public int ClashPower;
    public int SpeedModifier;
    public int MomentumModifier;
    public int PreparationModifier;
    public int JudgmentModifier;
    public int CurrentRollIndex;
    public CombatRollType CurrentRollType = CombatRollType.Attack;

    public bool Critical =>
        LastRollResult != null &&
        LastRollResult.IsCritical;

    public int finalPower;

    public int FinalPower
    {
        get => finalPower;
        set
        {
            finalPower = value;
            RolledPower = value;
            ClashPower =
                value +
                JudgmentModifier +
                SpeedModifier +
                MomentumModifier +
                PreparationModifier;
        }
    }

    public bool HasRolled;
    public RollResult LastRollResult;

    public List<RollResult> RollHistory { get; } = new();
    public List<DamageContext> DamageContexts { get; } = new();

    private readonly List<AttackWeightTarget>
        attackWeightTargets =
            new List<AttackWeightTarget>();

    private readonly List<AttackWeightHitResult>
        attackWeightHitResults =
            new List<AttackWeightHitResult>();

    private readonly Dictionary<int, RollResult> cachedRollResults = new();

    public IReadOnlyList<AttackWeightTarget>
        AttackWeightTargets =>
            attackWeightTargets;

    public IReadOnlyList<AttackWeightHitResult>
        AttackWeightHitResults =>
            attackWeightHitResults;

    public bool HasResolvedAttackWeightTargets
    {
        get;
        private set;
    }

    public int RequestedAttackWeight =>
        Mathf.Max(
            1,
            Skill?.AttackWeight ?? 1);

    public int ResolvedAttackWeight =>
        attackWeightTargets.Count;

    public DamageContext PrimaryDamageContext
    {
        get
        {
            foreach (DamageContext context
                     in DamageContexts)
            {
                if (context != null &&
                    context.Target == Target &&
                    context.TargetPart == TargetPart)
                {
                    return context;
                }
            }

            return LastDamageContext;
        }
    }

    public bool HasDamageLog;
    public int LoggedDamage;
    public int LoggedBeforeHP;
    public int LoggedAfterHP;

    public DamageContext LastDamageContext;
    public DamageResult LastDamageResult;
    public DamageEventResult LastDamageEventResult;

    public int TotalResolvedDamage
    {
        get
        {
            int total = 0;

            foreach (DamageContext context in DamageContexts)
            {
                total += context?.GetDisplayDamage() ?? 0;
            }

            return total;
        }
    }

    /// <summary>
    /// ActionSlot의 계획 타깃을 변경하지 않고,
    /// 현재 해석에서만 사용할 타깃을 고정합니다.
    /// UI 화살표와 다음 턴 계획에는 영향을 주지 않습니다.
    /// </summary>
    public void BindResolutionTarget(
        Character target,
        BodyPart targetPart)
    {
        if (targetPart != null &&
            targetPart.Owner != null &&
            targetPart.Owner != target)
        {
            Debug.LogError(
                "[BattleAction] 해석 타깃 부위의 Owner가 대상 캐릭터와 다릅니다. " +
                $"ActionId={ActionId}, " +
                $"Owner={GetCharacterName(Owner)}, " +
                $"Target={GetCharacterName(target)}, " +
                $"PartOwner={GetCharacterName(targetPart.Owner)}, " +
                $"Part={targetPart.Type}");

            targetPart = null;
        }

        resolutionTarget = target;
        resolutionTargetPart = targetPart;
        hasResolutionTargetOverride = true;
    }

    public void BindClashOpponent(
        BattleAction opponent)
    {
        if (opponent == null)
            return;

        BodyPart targetPart =
            ResolveClashTargetPart(opponent);

        BindResolutionTarget(
            opponent.Owner,
            targetPart);
    }

    private BodyPart ResolveClashTargetPart(
        BattleAction opponent)
    {
        if (opponent?.Owner == null)
            return null;

        // 정확한 적 ActionSlot(TargetSlot)을 지정해 합을 만들었더라도
        // 실제 피해 부위(TargetPart)는 별도 선택값이다.
        // Stage 1 Boss처럼 상대 ActionSlot.Part가 null인 글로벌 슬롯에서
        // HEAD 같은 선언 피해 부위를 null로 덮어쓰지 않는다.
        if (DeclaredTarget == opponent.Owner)
        {
            BodyPart declaredPart =
                DeclaredTargetPart;

            if (declaredPart == null ||
                declaredPart.Owner == null ||
                declaredPart.Owner == opponent.Owner)
            {
                return declaredPart;
            }
        }

        // 원래 다른 대상을 보던 행동이 합에 끌려온 경우에는
        // 기존 규칙대로 상대 행동의 원천 부위를 사용한다.
        return opponent.OwnerPart;
    }

    public void ClearResolutionTarget()
    {
        resolutionTarget = null;
        resolutionTargetPart = null;
        hasResolutionTargetOverride = false;
    }

    private static string GetCharacterName(
        Character character)
    {
        return character?.Data?.CharacterName ??
               character?.name ??
               "NULL";
    }

    public void BeginResolutionSequence()
    {
        ResetCurrentRollState();
        RollHistory.Clear();
        DamageContexts.Clear();
        cachedRollResults.Clear();

        attackWeightTargets.Clear();
        attackWeightHitResults.Clear();
        HasResolvedAttackWeightTargets = false;

        HasDamageLog = false;
        LoggedDamage = 0;
        LoggedBeforeHP = 0;
        LoggedAfterHP = 0;
        LastDamageContext = null;
        LastDamageResult = null;
        LastDamageEventResult = null;
    }

    public int GetEffectiveExchangeRollCount()
    {
        int baseCount =
            Mathf.Max(
                1,
                Skill?.ExchangeRollCount ?? 1);

        return Owner == null
            ? baseCount
            : Owner.ModifyExchangeRollCount(
                this,
                baseCount);
    }

    public int RollPower()
    {
        return RollPowerForExchange(
            RollHistory.Count);
    }

    public int RollPowerForExchange(
        int exchangeIndex)
    {
        ResetCurrentRollState();
        if (Skill == null) return 0;

        CurrentRollIndex = Mathf.Max(0, exchangeIndex);
        CurrentRollType = Skill.GetRollType(CurrentRollIndex);

        RollResult cached = null;
        bool reuse =
            Skill.ShouldReuseRollData(CurrentRollIndex) &&
            cachedRollResults.TryGetValue(
                CurrentRollIndex,
                out cached);

        LastRollResult = reuse && cached != null
            ? cached.Clone()
            : Skill.RollPowerResultForExchange(CurrentRollIndex);

        if (LastRollResult == null)
        {
            SetPurePower(Skill.BasePower);
            HasRolled = true;
            return RolledPower;
        }

        LastRollResult.RollIndex = CurrentRollIndex;
        LastRollResult.RollType = CurrentRollType;
        LastRollResult.ClearClashModifiers();
        LastRollResult.WasReused = reuse;

        SetPurePower(LastRollResult.FinalPower);

        int judgedPower = Owner != null
            ? Owner.ModifyRoll(this, RolledPower)
            : RolledPower;

        JudgmentModifier =
            (judgedPower - RolledPower) +
            LastRollResult.JudgmentModifier;

        LastRollResult.JudgmentModifier = JudgmentModifier;
        LastRollResult.RecalculateClashPower();

        if (!reuse && Skill.ShouldReuseRollData(CurrentRollIndex))
            cachedRollResults[CurrentRollIndex] = LastRollResult.Clone();

        HasRolled = true;
        RollHistory.Add(LastRollResult.Clone());
        return RolledPower;
    }

    public void InvalidateCachedRoll(
        int exchangeIndex)
    {
        cachedRollResults.Remove(
            Mathf.Max(0, exchangeIndex));
    }

    public void ApplyClashModifiers(
        int speedModifier,
        int momentumModifier,
        int preparationModifier = 0)
    {
        SpeedModifier = speedModifier;
        MomentumModifier = momentumModifier;
        PreparationModifier = preparationModifier;

        ClashPower =
            RolledPower +
            JudgmentModifier +
            SpeedModifier +
            MomentumModifier +
            PreparationModifier;

        LastRollResult?.SetClashModifiers(
            SpeedModifier,
            MomentumModifier,
            PreparationModifier);
    }

    public void ClearClashModifiers()
    {
        ApplyClashModifiers(0, 0);
    }

    public int GetDamagePower() => RolledPower;

    private void SetPurePower(int power)
    {
        RolledPower = power;
        finalPower = power;
        ClashPower = power;
    }

    private void ResetCurrentRollState()
    {
        RolledPower = 0;
        finalPower = 0;
        ClashPower = 0;
        SpeedModifier = 0;
        MomentumModifier = 0;
        PreparationModifier = 0;
        JudgmentModifier = 0;
        CurrentRollType = CombatRollType.Attack;
        HasRolled = false;
        LastRollResult = null;
    }

    public void SetDamageLog(
        int damage,
        int beforeHP,
        int afterHP)
    {
        HasDamageLog = true;
        LoggedDamage += Mathf.Max(0, damage);

        if (DamageContexts.Count <= 1)
            LoggedBeforeHP = Mathf.Max(0, beforeHP);

        LoggedAfterHP = Mathf.Max(0, afterHP);
    }

    public void SetAttackWeightTargets(
        IEnumerable<AttackWeightTarget> targets)
    {
        attackWeightTargets.Clear();

        if (targets != null)
        {
            foreach (AttackWeightTarget target
                     in targets)
            {
                if (target == null ||
                    target.TargetCharacter == null)
                {
                    continue;
                }

                attackWeightTargets.Add(
                    target);
            }
        }

        HasResolvedAttackWeightTargets =
            true;
    }

    public void AddAttackWeightHitResult(
        AttackWeightHitResult result)
    {
        if (result == null ||
            result.Target == null ||
            result.DamageContext == null)
        {
            return;
        }

        attackWeightHitResults.Add(
            result);
    }

    public void SetDamageContext(
        DamageContext context)
    {
        LastDamageContext = context;
        LastDamageResult = context?.Result;
        LastDamageEventResult = context?.EventResult;

        if (context == null)
            return;

        bool isNewContext =
            !DamageContexts.Contains(
                context);

        if (!isNewContext)
            return;

        DamageContexts.Add(
            context);

        SetDamageLog(
            context.GetDisplayDamage(),
            context.GetPrimaryHpBefore(),
            context.GetPrimaryHpAfter());
    }
}