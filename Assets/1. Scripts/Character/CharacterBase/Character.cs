using System.Collections.Generic;
using UnityEngine;

public abstract class Character : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterData data;
    public CharacterData Data => data;

    public BattleContext BattleContext => battleContext;

    public BattleEvent BattleEvent
    {
        get
        {
            if (battleContext == null)
                return null;

            return battleContext._battleEvent;
        }
    }

    private CharacterDamageController damageController;
    private CharacterBodyPartController bodyPartController;
    private CharacterStatusController statusController;
    private CharacterBuildController buildController;
    private CharacterResourceController resourceController;
    private CharacterLifeController lifeController;

    private DamageContext activeDamageContext;

    public DamageContext ActiveDamageContext =>
        activeDamageContext;

    public bool IsDamageResolutionInProgress =>
        activeDamageContext != null;

    public int CurrentHP
    {
        get
        {
            if (RuntimeStatus == null)
                return 0;

            return RuntimeStatus.currentHP;
        }
    }

    public IReadOnlyList<StatusEffect> StatusEffects
    {
        get
        {
            if (statusController == null)
                return System.Array.Empty<StatusEffect>();

            return statusController.CharacterStatuses;
        }
    }

    private CharacterMechanicController mechanicController;

    public IReadOnlyList<CombatMechanic> Mechanics
    {
        get
        {
            if (mechanicController == null)
                return System.Array.Empty<CombatMechanic>();

            return mechanicController.Mechanics;
        }
    }

    //--------------------------------
    // Body Parts
    //--------------------------------

    public abstract IReadOnlyList<BodyPart> BodyParts { get; }

    public ICombatTargetModel TargetModel { get; private set; }

    public bool UsesBodyParts =>
        TargetModel?.UsesBodyParts ??
        (BodyParts != null && BodyParts.Count > 0);

    public bool IsSingleHpTarget =>
        TargetModel != null &&
        !TargetModel.UsesBodyParts;

    public int MaxCombatHP
    {
        get
        {
            if (TargetModel != null)
                return TargetModel.GetMaxHp(this);

            return Mathf.Max(1, CurrentHP);
        }
    }

    //--------------------------------
    // Status
    //--------------------------------

    public CurrentStatus CurrentStatus { get; protected set; }

    public RuntimeStatus RuntimeStatus { get; protected set; }

    protected BattleContext battleContext;

    public CombatResourceBank Resources
    {
        get
        {
            if (resourceController == null)
                return null;

            return resourceController.Resources;
        }
    }

    public bool IsDead
    {
        get
        {
            if (lifeController == null)
                return false;

            return lifeController.IsDead;
        }
    }

    public void TransferPartStatusesToCharacter(BodyPart part)
    {
        if (statusController == null)
            return;

        statusController.TransferPartStatusesToCharacter(
            part);
    }

    //--------------------------------

    protected BattleEvent battleEvent;

    //------------------------------------------------

    // 아이템과 증강들
    [SerializeField] private List<CharacterItem> equippedItems = new();
    [SerializeField] private List<CharacterAugment> equippedAugments = new();

    public IReadOnlyList<CharacterItem> EquippedItems
    {
        get
        {
            if (buildController == null)
                return equippedItems;

            return buildController.EquippedItems;
        }
    }

    public IReadOnlyList<CharacterAugment> EquippedAugments
    {
        get
        {
            if (buildController == null)
                return equippedAugments;

            return buildController.EquippedAugments;
        }
    }


    public virtual void Initialize(BattleContext context)
    {
        if (context == null)
        {
            Debug.LogWarning($"{GetType().Name} Initialize 실패 : BattleContext가 null입니다.");
            return;
        }

        battleContext = context;
        battleEvent = context._battleEvent;

        resourceController =
            new CharacterResourceController(this);

        resourceController.Reset();

        mechanicController =
            new CharacterMechanicController(this);

        mechanicController.Clear();

        lifeController =
            new CharacterLifeController(
                this,
                mechanicController);

        lifeController.Reset();

        buildController =
            new CharacterBuildController(
                this,
                equippedItems,
                equippedAugments);

        //--------------------------------
        // 1. 기본 부위 / 기본 스킬 생성
        //--------------------------------

        BuildBodyParts();

        //--------------------------------
        // 2. 아이템이 스킬 목록 수정
        //--------------------------------

        buildController.ApplyItemSkillModifiers(
            BodyParts);

        //--------------------------------
        // 3. 스킬 초기화
        //--------------------------------

        foreach (BodyPart part in BodyParts)
        {
            if (part == null)
                continue;

            part.Initialize(this);

            foreach (Skill skill in part.AvailableSkills)
            {
                if (skill == null)
                    continue;

                skill.Initialize(this, battleEvent);
            }
        }

        foreach (Skill skill in GetCharacterSkills())
        {
            if (skill == null)
                continue;

            skill.Initialize(this, battleEvent);
        }

        //--------------------------------
        // 4. 기본 스탯 재계산
        //--------------------------------

        RecalculateStatus();

        //--------------------------------
        // 5. 아이템/증강 스탯 적용
        //--------------------------------

        buildController.ApplyStatusModifiers(
            CurrentStatus);

        //--------------------------------
        // 6. 부위 스탯 적용
        //--------------------------------

        buildController.ApplyBodyPartModifiers(
            BodyParts);

        //--------------------------------
        // 7. 타겟 모델 / 런타임 스탯 생성
        //--------------------------------

        TargetModel =
            CreateCombatTargetModel();

        RuntimeStatus =
            new RuntimeStatus(CurrentStatus);

        RuntimeStatus.currentHP =
            CalculateInitialHP();

        bodyPartController =
            new CharacterBodyPartController(this);

        statusController =
            new CharacterStatusController(this);

        damageController =
            new CharacterDamageController(
                this,
                bodyPartController);

        //--------------------------------
        // 8. 기본 메커닉 생성
        //--------------------------------

        BuildMechanics();

        //--------------------------------
        // 9. 아이템/증강 메커닉 생성
        //--------------------------------

        foreach (CombatMechanic mechanic in buildController.CreateMechanics())
        {
            AddMechanic(mechanic);
        }

        //--------------------------------
        // 10. 메커닉 초기화/등록
        //--------------------------------

        mechanicController.InitializeAndRegisterAll(
            battleContext);
    }
    public virtual void UnregisterMechanics()
    {
        mechanicController?.UnregisterAll();
    }

    protected abstract void BuildBodyParts();

    protected virtual IEnumerable<Skill> GetCharacterSkills()
    {
        return System.Array.Empty<Skill>();
    }

    protected virtual ICombatTargetModel CreateCombatTargetModel()
    {
        if (BodyParts != null &&
            BodyParts.Count > 0)
        {
            return new BodyPartTargetModel();
        }

        return new SingleHpTargetModel(1);
    }

    protected virtual void BuildMechanics()
    {
    }

    protected void AddMechanic(CombatMechanic mechanic)
    {
        if (mechanicController == null)
            return;

        mechanicController.AddMechanic(mechanic);
    }

    //------------------------------------------------

    public virtual int GetMaxActionSlotsForPart(BodyPart part)
    {
        if (part == null)
        {
            return IsSingleHpTarget
                ? 1
                : 0;
        }

        if (part.IsBroken)
            return 0;

        ActionSlotPolicyContext context =
            new ActionSlotPolicyContext
            {
                Owner = this,
                Part = part,
                MaxSlots = 1
            };

        mechanicController?.ModifyActionSlotPolicy(context);

        return Mathf.Max(
            0,
            context.MaxSlots);
    }

    public T GetStatus<T>() where T : StatusEffect
    {
        if (statusController == null)
            return null;

        return statusController.GetStatus<T>();
    }

    public BodyPart GetRandomUsablePart()
    {
        List<BodyPart> candidates = new();

        foreach (BodyPart part in BodyParts)
        {
            if (part == null)
                continue;

            if (part.IsBroken)
                continue;

            candidates.Add(part);
        }

        if (candidates.Count == 0)
            return null;

        return candidates[
            UnityEngine.Random.Range(0, candidates.Count)];
    }

    private int CalculateInitialHP()
    {
        if (TargetModel != null)
        {
            return Mathf.Max(
                0,
                TargetModel.CalculateInitialHp(this));
        }

        int hp = 0;

        if (BodyParts == null)
            return hp;

        foreach (BodyPart part in BodyParts)
        {
            if (part == null ||
                part.IsBroken)
            {
                continue;
            }

            hp += Mathf.Max(
                0,
                Mathf.RoundToInt(part.PartHP));
        }

        return hp;
    }

    //------------------------------------------------

    public virtual void RecalculateStatus()
    {
        CurrentStatus = new CurrentStatus(Data);

        // TODO
        // 장비
        // 패시브
        // 영구버프
    }

    //------------------------------------------------

    public virtual void TurnStart()
    {
        if (IsDead)
            return;

        statusController?.OnTurnStart();
    }

    public virtual void TurnEnd()
    {
        if (IsDead)
            return;

        statusController?.OnTurnEnd();

        ClearBlock();

        CheckDead();
    }

    internal void BeginDamageResolution(
        DamageContext context)
    {
        activeDamageContext = context;
    }

    internal void EndDamageResolution(
        DamageContext context)
    {
        if (activeDamageContext == context)
            activeDamageContext = null;
    }

    //------------------------------------------------
    // Damage
    //------------------------------------------------

    public void TakeDamage(
        BodyPart targetPart,
        int damage,
        bool canBreakPart)
    {
        if (damageController == null)
            return;

        damageController.TakeDamage(
            targetPart,
            damage,
            canBreakPart);
    }

    //------------------------------------------------

    public void ReduceCurrentHP(int damage)
    {
        if (RuntimeStatus == null)
            return;

        RuntimeStatus.currentHP =
            Mathf.Max(
                RuntimeStatus.currentHP - damage,
                0);
    }

    public void RestoreCurrentHP(int amount)
    {
        if (RuntimeStatus == null || amount <= 0)
            return;

        RuntimeStatus.currentHP =
            Mathf.Min(
                RuntimeStatus.currentHP + amount,
                MaxCombatHP);
    }

    public void RecoverPart(BodyPart part)
    {
        if (bodyPartController == null)
            return;

        bodyPartController.RecoverPart(part);
    }

    //------------------------------------------------

    public void ForceRecalculateHP()
    {
        if (RuntimeStatus == null)
            return;

        RuntimeStatus.currentHP =
            CalculateInitialHP();
    }

    public bool TryBreakWeakenedPart(BodyPart part)
    {
        return TryBreakWeakenedPart(
            part,
            activeDamageContext?.Attacker,
            activeDamageContext?.Action);
    }

    public bool TryBreakWeakenedPart(
        BodyPart part,
        Character source,
        BattleAction sourceAction = null)
    {
        if (bodyPartController == null)
            return false;

        return bodyPartController.TryBreakWeakenedPart(
            part,
            source,
            sourceAction);
    }

    //------------------------------------------------

    public virtual void TakeDirectDamage(
        int damage,
        Character source = null,
        BattleAction sourceAction = null)
    {
        if (damageController == null)
            return;

        damageController.TakeDirectDamage(
            damage,
            source,
            sourceAction);
    }

    //------------------------------------------------

    public virtual void TakeTrueDamage(
        int damage,
        StatusEffect sourceEffect)
    {
        if (damageController == null)
            return;

        damageController.TakeTrueDamage(
            damage,
            sourceEffect);
    }

    //------------------------------------------------

    public virtual void CheckDead()
    {
        CheckDead(
            activeDamageContext?.Attacker,
            activeDamageContext?.Action);
    }

    public virtual void CheckDead(
        Character killer,
        BattleAction sourceAction = null)
    {
        if (lifeController == null)
            return;

        lifeController.CheckDead(
            killer,
            sourceAction);
    }

    //------------------------------------------------

    protected virtual bool CanDie()
    {
        if (lifeController == null)
            return false;

        return lifeController.CanDie();
    }

    //------------------------------------------------

    public virtual void Die()
    {
        Die(
            null,
            null,
            activeDamageContext);
    }

    public virtual void Die(
        Character killer,
        BattleAction sourceAction = null,
        DamageContext damageContext = null)
    {
        if (lifeController == null)
            return;

        lifeController.Die(
            killer,
            sourceAction,
            damageContext);
    }

    //------------------------------------------------
    // Disabled / Weakened
    //------------------------------------------------

    protected virtual void OnBodyPartDisabled(BodyPart part)
    {
        StatusEffect effect =
            CreateDisabledDebuff(part);

        if (effect == null)
            return;

        AddPartStatus(
            part,
            effect,
            this);
    }

    public void ApplyDisabledStatusForPart(BodyPart part)
    {
        if (part == null || part.IsBroken)
            return;

        OnBodyPartDisabled(part);
    }

    protected virtual StatusEffect CreateDisabledDebuff(
        BodyPart part)
    {
        return null;
    }
    //------------------------------------------------
    // 고유 파괴 루트용
    // 올라프 출혈 3스택, 유진 처형, 김삿갓 뼈 스택 등에서 사용
    //------------------------------------------------

    public void ForceBreakPart(BodyPart part)
    {
        ForceBreakPart(
            part,
            this,
            null);
    }

    public void ForceBreakPart(
        BodyPart part,
        Character source,
        BattleAction sourceAction = null)
    {
        if (bodyPartController == null)
            return;

        bodyPartController.ForceBreakPart(
            part,
            source,
            sourceAction);
    }

    //------------------------------------------------

    public virtual void OnBodyPartBroken(
        BodyPart part,
        StatusEffect disabledDebuff)
    {
        StatusEffect brokenStatus =
            CreateBrokenPartStatus(part);

        if (brokenStatus == null)
            return;

        AddStatus(brokenStatus, this);
    }

    protected virtual StatusEffect CreateBrokenPartStatus(
        BodyPart part)
    {
        if (part == null)
            return null;

        return part.Type switch
        {
            PartType.HEAD => new BrokenHead(),
            PartType.LEFT_HAND => new BrokenArm(PartType.LEFT_HAND),
            PartType.RIGHT_HAND => new BrokenArm(PartType.RIGHT_HAND),
            PartType.LEGS => new BrokenLegs(),
            _ => null
        };
    }

    //------------------------------------------------
    // Character Status
    //------------------------------------------------

    public void AddStatus(StatusEffect effect, Character source)
    {
        if (statusController == null)
            return;

        statusController.AddStatus(effect, source);
    }

    public void RemoveStatus(StatusEffect effect)
    {
        RemoveStatus(
            effect,
            StatusEffectRemoveReason.Manual);
    }

    public void RemoveStatus(
        StatusEffect effect,
        StatusEffectRemoveReason reason)
    {
        if (statusController == null)
            return;

        statusController.RemoveStatus(
            effect,
            reason);
    }

    public void RemoveAllPartStatuses(
        BodyPart part,
        StatusEffectRemoveReason reason)
    {
        statusController?.RemoveAllPartStatuses(
            part,
            reason);
    }

    public void RemoveBrokenStatusForPart(BodyPart part)
    {
        statusController?.RemoveBrokenStatusForPart(part);
    }

    //------------------------------------------------

    public void AddPrestige(int amount)
    {
        if (resourceController == null)
            return;

        resourceController.AddPrestige(amount);
    }

    //------------------------------------------------

    public int ModifyRoll(
        BattleAction action,
        int roll)
    {
        int value = roll;

        foreach (StatusEffect effect in StatusEffects)
        {
            if (effect == null)
                continue;

            value = effect.ModifyRoll(action, value);
        }

        if (action != null &&
            action.OwnerPart != null)
        {
            foreach (StatusEffect effect in action.OwnerPart.StatusEffects)
            {
                if (effect == null)
                    continue;

                value = effect.ModifyRoll(action, value);
            }
        }

        if (mechanicController != null)
        {
            value =
                mechanicController.ModifyRoll(
                    action,
                    value);
        }

        return value;
    }

    public bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        if (skill == null)
            return false;

        if (part == null)
        {
            if (!IsSingleHpTarget)
                return false;
        }
        else if (part.IsBroken)
        {
            return false;
        }

        foreach (StatusEffect effect in StatusEffects)
        {
            if (effect == null)
                continue;

            if (!effect.CanUseSkill(part, skill))
                return false;
        }

        if (part != null)
        {
            foreach (StatusEffect effect in part.StatusEffects)
            {
                if (effect == null)
                    continue;

                if (!effect.CanUseSkill(part, skill))
                    return false;
            }
        }

        if (mechanicController != null &&
            !mechanicController.CanUseSkill(part, skill))
        {
            return false;
        }

        if (!skill.CanUseByResource(this))
            return false;

        return true;
    }

    public void NotifyBodyPartBreakBeforeDeath(
        BodyPartBreakEventContext context)
    {
        mechanicController?
            .NotifyBodyPartBreakBeforeDeath(
                context);
    }

    public T GetMechanic<T>() where T : CombatMechanic
    {
        if (mechanicController == null)
            return null;

        return mechanicController.GetMechanic<T>();
    }

    public virtual bool CanAIUse(
        Character owner,
        BodyPart part,
        BattleContext context)
    {
        return true;
    }

    public void AddItem(CharacterItem item)
    {
        if (buildController == null)
        {
            if (item == null)
                return;

            if (equippedItems.Contains(item))
                return;

            equippedItems.Add(item);
            return;
        }

        buildController.AddItem(item);
    }

    public void AddAugment(CharacterAugment augment)
    {
        if (buildController == null)
        {
            if (augment == null)
                return;

            if (equippedAugments.Contains(augment))
                return;

            equippedAugments.Add(augment);
            return;
        }

        buildController.AddAugment(augment);
    }

    public void AddBlock(int amount)
    {
        if (resourceController == null)
            return;

        resourceController.AddBlock(amount);
    }

    public void ClearBlock()
    {
        if (resourceController == null)
            return;

        resourceController.ClearBlock();
    }

    public void AddPartStatus(
        BodyPart part,
        StatusEffect effect,
        Character source)
    {
        if (statusController == null)
            return;

        statusController.AddPartStatus(part, effect, source);
    }

    public void RemovePartStatus(
        BodyPart part,
        StatusEffect effect)
    {
        RemovePartStatus(
            part,
            effect,
            StatusEffectRemoveReason.Manual);
    }

    public void RemovePartStatus(
        BodyPart part,
        StatusEffect effect,
        StatusEffectRemoveReason reason)
    {
        if (statusController == null)
            return;

        statusController.RemovePartStatus(
            part,
            effect,
            reason);
    }

    public T GetPartStatus<T>(BodyPart part) where T : StatusEffect
    {
        if (statusController == null)
            return null;

        return statusController.GetPartStatus<T>(
            part);
    }

    public bool HasStatus<T>() where T : StatusEffect
    {
        if (statusController == null)
            return false;

        return statusController.HasStatus<T>();
    }

    public bool HasPartStatus<T>(BodyPart part) where T : StatusEffect
    {
        if (statusController == null)
            return false;

        return statusController.HasPartStatus<T>(
            part);
    }

    public void TakeStatusPartDamage(
        BodyPart targetPart,
        int damage,
        StatusEffect sourceEffect)
    {
        if (damageController == null)
            return;

        damageController.TakeStatusPartDamage(
            targetPart,
            damage,
            sourceEffect);
    }
}
