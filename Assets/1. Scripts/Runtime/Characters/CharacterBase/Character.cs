using System.Collections.Generic;
using UnityEngine;

public abstract class Character : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterData data;
    public CharacterData Data => data;

    [System.NonSerialized]
    private CharacterCombatLoadout authoringCombatLoadoutOverride;

    public CharacterCombatLoadout CombatLoadoutSource =>
        authoringCombatLoadoutOverride != null
            ? authoringCombatLoadoutOverride
            : data?.CombatLoadout;

    /// <summary>
    /// 구형 호출부 호환. CharacterData의 CombatLoadout을 사용한다.
    /// </summary>
    public void ConfigureAuthoringCore(
        CharacterData characterData,
        IReadOnlyList<CharacterItem> items = null,
        IReadOnlyList<CharacterAugment> augments = null)
    {
        ConfigureAuthoringCore(
            characterData,
            characterData?.CombatLoadout,
            items,
            augments);
    }

    /// <summary>
    /// Bundle이 Initialize 이전에 CharacterData, 최신 CombatLoadout,
    /// Item, Passive/Augment를 한 번에 적용하는 진입점이다.
    /// </summary>
    public void ConfigureAuthoringCore(
        CharacterData characterData,
        CharacterCombatLoadout combatLoadout,
        IReadOnlyList<CharacterItem> items = null,
        IReadOnlyList<CharacterAugment> augments = null)
    {
        if (characterData != null)
            data = characterData;

        authoringCombatLoadoutOverride = combatLoadout;

        if (items != null)
        {
            equippedItems ??= new List<CharacterItem>();
            equippedItems.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                CharacterItem item = items[i];

                if (item != null && !equippedItems.Contains(item))
                    equippedItems.Add(item);
            }
        }

        if (augments != null)
        {
            equippedAugments ??= new List<CharacterAugment>();
            equippedAugments.Clear();

            for (int i = 0; i < augments.Count; i++)
            {
                CharacterAugment augment = augments[i];

                if (augment != null && !equippedAugments.Contains(augment))
                    equippedAugments.Add(augment);
            }
        }
    }

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
    private CharacterMechanicController mechanicController;

    private readonly CharacterCombatState combatState = new();
    private readonly CharacterEventBinder eventBinder = new();
    private readonly List<Skill> characterSkills = new();
    private CharacterCombatRulesRuntime combatRulesRuntime;

    private CharacterTargetModel targetModel;

    public CharacterCombatState CombatState => combatState;

    public bool IsInitialized =>
        combatState.IsInitialized;

    public DamageContext ActiveDamageContext =>
        combatState.ActiveDamageContext;

    public bool IsDamageResolutionInProgress =>
        combatState.IsDamageResolutionInProgress;

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

    public IReadOnlyList<CombatMechanic> Mechanics
    {
        get
        {
            if (mechanicController == null)
                return System.Array.Empty<CombatMechanic>();

            return mechanicController.Mechanics;
        }
    }

    /// <summary>
    /// UI, 도감, 전투 상세 화면에서 현재 런타임에 초기화된 전체 스킬을
    /// 안전하게 읽기 위한 읽기 전용 노출입니다.
    /// </summary>
    public IReadOnlyList<Skill> RuntimeSkills => characterSkills;

    //--------------------------------
    // Body Parts
    //--------------------------------

    public abstract IReadOnlyList<BodyPart> BodyParts { get; }

    public BodyPart GetBodyPart(
        PartType type)
    {
        if (BodyParts == null)
            return null;

        foreach (BodyPart part in BodyParts)
        {
            if (part != null &&
                part.Type == type)
            {
                return part;
            }
        }

        return null;
    }

    // 기존 외부 코드 호환용 ICombatTargetModel 노출.
    public ICombatTargetModel TargetModel =>
        targetModel?.Model;

    public CharacterTargetModel Targeting =>
        targetModel;

    public bool UsesBodyParts =>
        targetModel?.UsesBodyParts ??
        (BodyParts != null && BodyParts.Count > 0);

    public bool IsSingleHpTarget =>
        targetModel != null &&
        !targetModel.UsesBodyParts;

    public int MaxCombatHP
    {
        get
        {
            if (targetModel != null)
                return targetModel.GetMaxHp(this);

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

    public int CurrentEnergy =>
        resourceController?.CurrentEnergy ?? 0;

    public int MaxEnergy =>
        resourceController?.MaxEnergy ??
        CurrentStatus?.maxEnergy ??
        Data?.maxEnergy ?? 0;

    public int TurnClashPowerBonus =>
        combatState?.TurnClashPowerBonus ?? 0;

    public CharacterCombatRulesRuntime CombatRulesRuntime => combatRulesRuntime;

    public virtual bool SupportsLastStand =>
        Data != null &&
        (Data.CombatantTier == CombatantTier.EliteEnemy ||
         Data.CombatantTier == CombatantTier.Boss);

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
            Debug.LogWarning(
                $"{GetType().Name} Initialize 실패 : BattleContext가 null입니다.");
            return;
        }

        // 같은 GameObject를 전투 재시작에서 다시 사용할 수 있으므로
        // 이전 Skill/Mechanic/Status 구독을 먼저 완전히 해제한다.
        ShutdownRuntime(clearBattleReferences: true);
        combatState.BeginInitialization();

        battleContext = context;
        battleEvent = context._battleEvent;

        try
        {
            resourceController ??=
                new CharacterResourceController(this);
            resourceController.Reset();

            mechanicController ??=
                new CharacterMechanicController(this);
            mechanicController.Reset();

            buildController =
                new CharacterBuildController(
                    this,
                    equippedItems,
                    equippedAugments);

            //--------------------------------
            // 1. 부위와 스킬 런타임 객체 생성
            //--------------------------------
            BuildBodyParts();

            buildController.ApplyItemSkillModifiers(
                BodyParts);

            if (BodyParts != null)
            {
                foreach (BodyPart part in BodyParts)
                {
                    part?.Initialize(this);
                }
            }

            //--------------------------------
            // 2. 스탯과 타겟 모델 구성
            //--------------------------------
            RecalculateStatus();

            CurrentStatus ??=
                new CurrentStatus(data);

            buildController.ApplyStatusModifiers(
                CurrentStatus);

            resourceController.ConfigureEnergy(
                CurrentStatus?.maxEnergy ?? data?.maxEnergy ?? 0,
                fillToMaximum: true);

            buildController.ApplyBodyPartModifiers(
                BodyParts);

            targetModel =
                new CharacterTargetModel(
                    CreateCombatTargetModel());

            RuntimeStatus =
                new RuntimeStatus(CurrentStatus);

            RuntimeStatus.currentHP =
                CalculateInitialHP();

            //--------------------------------
            // 3. 런타임 컨트롤러 구성
            //--------------------------------
            bodyPartController =
                new CharacterBodyPartController(this);

            statusController =
                new CharacterStatusController(this);

            damageController =
                new CharacterDamageController(
                    this,
                    bodyPartController);

            lifeController =
                new CharacterLifeController(
                    this,
                    mechanicController,
                    combatState);

            lifeController.Reset();

            //--------------------------------
            // 4. 캐릭터별 슬롯 / 장착 스킬 / 보스 페이즈 런타임
            //--------------------------------
            combatRulesRuntime =
                new CharacterCombatRulesRuntime(this);

            //--------------------------------
            // 5. 모든 런타임 Skill 이벤트 구독
            //--------------------------------
            characterSkills.Clear();

            IEnumerable<Skill> extraSkills =
                GetCharacterSkills();

            if (extraSkills != null)
            {
                foreach (Skill skill in extraSkills)
                {
                    AddRuntimeSkillIfUnique(
                        characterSkills,
                        skill);
                }
            }

            foreach (Skill skill in combatRulesRuntime.CreateRuntimeSkills())
            {
                AddRuntimeSkillIfUnique(
                    characterSkills,
                    skill);
            }

            eventBinder.BindSkills(
                this,
                battleEvent,
                BodyParts,
                characterSkills);

            //--------------------------------
            // 5. 메커닉 생성 및 구독
            //--------------------------------
            BuildMechanics();

            if (Data?.EnableStaggerGauge == true)
            {
                AddMechanic(
                    new StaggerGaugeMechanic(
                        Data.GetEffectiveMaxStaggerGauge(battleContext?.Rules)));
            }

            foreach (CombatMechanic mechanic
                     in buildController.CreateMechanics())
            {
                AddMechanic(mechanic);
            }

            mechanicController.InitializeAndRegisterAll(
                battleContext);

            RuntimeStatus.Clamp(
                CurrentStatus,
                MaxCombatHP);

            combatState.CompleteInitialization();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            ShutdownRuntime(clearBattleReferences: true);
            combatState.FailInitialization();
        }
    }

    private void ShutdownRuntime(
        bool clearBattleReferences)
    {
        eventBinder.UnbindAll();
        mechanicController?.Reset();

        statusController?.ClearAll(
            StatusEffectRemoveReason.Cleared,
            raiseEvents: false);

        // StatusController가 아직 없던 초기화 실패도 안전하게 정리한다.
        if (statusController == null &&
            BodyParts != null)
        {
            foreach (BodyPart part in BodyParts)
            {
                part?.ClearStatusEffects();
            }
        }

        characterSkills.Clear();
        combatRulesRuntime = null;
        combatState.ClearDamageResolution();

        damageController = null;
        bodyPartController = null;
        statusController = null;
        buildController = null;
        lifeController = null;
        targetModel = null;
        CurrentStatus = null;
        RuntimeStatus = null;

        if (!clearBattleReferences)
            return;

        battleEvent = null;
        battleContext = null;
    }

    public virtual void DisposeRuntime()
    {
        ShutdownRuntime(clearBattleReferences: true);
        combatState.Dispose();
    }

    protected virtual void OnDestroy()
    {
        DisposeRuntime();
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

    /// <summary>
    /// Modern CharacterCombatLoadout의 SkillDefinition을 캐릭터 고유 RuntimeSkill로
    /// 변환하는 확장점이다. 기본은 Definition 팩토리를 사용하고, Olaf처럼
    /// 고유 Duel/Preparation Adapter가 필요한 캐릭터가 오버라이드한다.
    /// </summary>
    public virtual Skill CreateRuntimeSkillForLoadout(
        SkillDefinition definition)
    {
        return definition?.CreateRuntimeSkill();
    }

    protected virtual ICombatTargetModel CreateCombatTargetModel()
    {
        return CharacterTargetModel.CreateDefault(
            this,
            data);
    }

    /// <summary>
    /// 여러 런타임 스킬 공급원이 같은 SkillDefinition을 제공하더라도
    /// RuntimeSkill을 한 번만 등록한다.
    /// 참조 동일성뿐 아니라 SkillId도 비교해 중복 이벤트 구독을 차단한다.
    /// </summary>
    private static void AddRuntimeSkillIfUnique(
        ICollection<Skill> destination,
        Skill candidate)
    {
        if (destination == null || candidate == null)
            return;

        SkillDefinition candidateDefinition =
            candidate.Definition;

        foreach (Skill existing in destination)
        {
            if (ReferenceEquals(existing, candidate))
                return;

            SkillDefinition existingDefinition =
                existing?.Definition;

            if (candidateDefinition == null ||
                existingDefinition == null)
            {
                continue;
            }

            if (ReferenceEquals(
                    existingDefinition,
                    candidateDefinition))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(
                    candidateDefinition.SkillId) &&
                string.Equals(
                    existingDefinition.SkillId,
                    candidateDefinition.SkillId,
                    System.StringComparison.Ordinal))
            {
                return;
            }
        }

        destination.Add(candidate);
    }

    protected virtual void BuildMechanics()
    {
    }

    public bool IsValidTargetPart(
        BodyPart part,
        bool allowBrokenPart = false)
    {
        return targetModel != null &&
               targetModel.IsValidTargetPart(
                   this,
                   part,
                   allowBrokenPart);
    }

    public IReadOnlyList<TargetPoint> GetTargetPoints(
        bool includeBrokenParts = false)
    {
        return targetModel?.GetTargetPoints(
                   this,
                   includeBrokenParts) ??
               System.Array.Empty<TargetPoint>();
    }

    protected void AddMechanic(CombatMechanic mechanic)
    {
        if (mechanicController == null)
            return;

        mechanicController.AddMechanic(mechanic);
    }

    public void AddRuntimeMechanic(CombatMechanic mechanic)
    {
        if (mechanicController == null || mechanic == null)
            return;
        mechanicController.AddMechanic(mechanic);
        if (IsInitialized)
            mechanicController.RegisterMechanic(mechanic, battleContext);
    }

    //------------------------------------------------

    public virtual int GetMaxActionSlots()
    {
        int configured =
            combatRulesRuntime?.GetActiveSlotCount() ?? 0;

        return configured > 0
            ? configured
            : GetMaxCombatActionSlots();
    }

    public virtual int GetMaxCombatActionSlots()
    {
        int configured = combatRulesRuntime?.GetActiveCombatSlotCount() ?? 0;
        return configured > 0 ? configured : 3;
    }

    public virtual int GetMaxActionSlotsForPart(BodyPart part)
    {
        if (part == null)
        {
            int globalConfigured = combatRulesRuntime?.GetSlotCountForPart(null) ?? 0;
            if (globalConfigured > 0)
                return globalConfigured;
            return IsSingleHpTarget ? 1 : 0;
        }
        if (part.IsBroken)
            return 0;

        int configured = combatRulesRuntime?.GetSlotCountForPart(part) ?? 0;
        if (combatRulesRuntime?.HasStructuredRules == true)
            return Mathf.Max(0, configured);

        if (configured > 0)
            return configured;

        ActionSlotPolicyContext context = new ActionSlotPolicyContext
        {
            Owner = this,
            Part = part,
            MaxSlots = 1
        };
        mechanicController?.ModifyActionSlotPolicy(context);
        return Mathf.Max(0, context.MaxSlots);
    }

    public void ConfigureActionSlot(ActionSlot slot)
    {
        if (slot == null || slot.Owner != this)
            return;

        CharacterSlotConfig config =
            combatRulesRuntime?.GetSlotConfig(slot.Part, slot.ActionIndex);

        slot.SlotConfig = config;
        slot.SlotId = config?.SlotId ??
                      $"RUNTIME_{slot.Part?.Type.ToString() ?? "CHARACTER"}_{slot.ActionIndex:00}";

        // Speed는 ActionSlot 개별 속성이 아니라 BodyPart의 턴 속도를 복사한 값이다.
        // 같은 부위의 ActionIndex 0/1/2...는 반드시 하나의 SpeedManager 굴림을 공유한다.
        // CharacterSlotConfig의 속도 Override도 SpeedManager에서 "부위 단위"로만 해석한다.
        // 여기서 개별 슬롯을 다시 굴리면 같은 머리의 두 슬롯 속도가 달라지는 문제가 생긴다.
    }

    public bool CanUseActionSlot(ActionSlot slot)
    {
        if (slot == null || slot.Owner != this)
            return false;

        ConfigureActionSlot(slot);

        if (slot.SlotConfig?.HasLinkedPart == true &&
            slot.Part?.IsBroken == true)
        {
            return false;
        }

        if (slot.SlotConfig != null &&
            !slot.SlotConfig.Enabled)
        {
            return false;
        }

        // 부위형 캐릭터는 과거 CharacterSlotConfig의 AllowedActionTypes보다
        // 현재 게임의 부위별 스킬 카테고리 계약을 최우선한다.
        if (slot.Skill != null &&
            slot.Part != null &&
            !BodyPartSkillAccessPolicy.Allows(
                slot.Part,
                slot.Skill.ActionType))
        {
            return false;
        }

        return true;
    }

    public IReadOnlyList<Skill> GetSelectableSkills(
        BodyPart part,
        int actionIndex = 0)
    {
        IReadOnlyList<Skill> selectable;

        if (combatRulesRuntime?.HasStructuredRules == true)
        {
            ActionSlot probe = new ActionSlot
            {
                Owner = this,
                Part = part,
                ActionIndex = Mathf.Max(0, actionIndex)
            };

            ConfigureActionSlot(probe);
            selectable =
                combatRulesRuntime.GetAvailableSkillsForSlot(
                    probe,
                    characterSkills);
        }
        else if (part != null)
        {
            selectable = part.AvailableSkills;
        }
        else
        {
            selectable = characterSkills;
        }

        return BodyPartSkillAccessPolicy.Filter(
            part,
            selectable);
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

        if (BodyParts == null)
            return null;

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
        if (targetModel != null)
        {
            return Mathf.Max(
                0,
                targetModel.CalculateInitialHp(this));
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
        if (!IsInitialized || IsDead)
            return;

        // 도사림의 그 턴 한정 합 보정은 다음 턴 시작 전에 초기화한다.
        combatState?.ClearTurnModifiers();

        combatRulesRuntime?.EvaluateBossPhase(
            battleContext,
            battleContext?.Services?.TurnManager?.CurrentTurn ?? 1);

        TurnStartEnergyPolicy energyPolicy =
            Data?.TurnStartEnergyPolicy ?? TurnStartEnergyPolicy.GlobalGain;

        switch (energyPolicy)
        {
            case TurnStartEnergyPolicy.RefillToMaximum:
                resourceController?.RestoreEnergyToFull();
                break;

            case TurnStartEnergyPolicy.GainFlat:
                resourceController?.AddEnergy(
                    Data?.TurnStartEnergyAmount ?? 0,
                    CombatResourceChangeReason.TurnRefill);
                break;

            case TurnStartEnergyPolicy.None:
                break;

            default:
                resourceController?.AddEnergy(
                    battleContext?.Rules?.Energy?.TurnStartGain ?? 1,
                    CombatResourceChangeReason.TurnRefill);
                break;
        }

        statusController?.OnTurnStart();
    }

    public virtual void TurnEnd()
    {
        if (!IsInitialized || IsDead)
            return;

        statusController?.OnTurnEnd();

        ClearBlock();

        CheckDead();
    }

    internal void BeginDamageResolution(
        DamageContext context)
    {
        combatState.BeginDamageResolution(context);
    }

    internal void EndDamageResolution(
        DamageContext context)
    {
        combatState.EndDamageResolution(context);
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

        int modified = amount;
        foreach (StatusEffect effect in StatusEffects)
        {
            if (effect != null)
                modified = effect.ModifyHealing(modified);
        }

        modified = Mathf.Max(0, modified);
        RuntimeStatus.currentHP =
            Mathf.Min(
                RuntimeStatus.currentHP + modified,
                MaxCombatHP);
    }

    public bool SetMaxCombatHpForDebug(
        int maxHp,
        bool preserveCurrentHpRatio = false)
    {
        if (targetModel == null ||
            targetModel.UsesBodyParts)
        {
            return false;
        }

        int oldMax =
            Mathf.Max(
                1,
                MaxCombatHP);

        int oldCurrent =
            RuntimeStatus?.currentHP ?? 0;

        if (!targetModel.TrySetMaxHpForDebug(
                this,
                maxHp))
        {
            return false;
        }

        if (RuntimeStatus == null)
            return true;

        int newMax =
            Mathf.Max(
                1,
                MaxCombatHP);

        if (preserveCurrentHpRatio)
        {
            float ratio =
                Mathf.Clamp01(
                    (float)oldCurrent /
                    oldMax);

            RuntimeStatus.currentHP =
                Mathf.Clamp(
                    Mathf.RoundToInt(
                        newMax * ratio),
                    0,
                    newMax);
        }
        else
        {
            RuntimeStatus.currentHP =
                Mathf.Clamp(
                    oldCurrent,
                    0,
                    newMax);
        }

        return true;
    }

    public bool SetBodyPartStateForDebug(
        BodyPart part,
        float currentHp,
        float maxHp,
        BodyPartState state,
        bool clearNonStructuralStatuses = true)
    {
        if (part == null ||
            BodyParts == null ||
            (part.Owner != null &&
             part.Owner != this))
        {
            return false;
        }

        bool ownsPart = false;

        foreach (BodyPart ownedPart
                 in BodyParts)
        {
            if (!ReferenceEquals(
                    ownedPart,
                    part))
            {
                continue;
            }

            ownsPart = true;
            break;
        }

        if (!ownsPart)
            return false;

        List<StatusEffect> partEffects =
            new(
                part.StatusEffects);

        foreach (StatusEffect effect
                 in partEffects)
        {
            if (effect == null)
                continue;

            if (!clearNonStructuralStatuses &&
                effect is not PartDisabledStatus)
            {
                continue;
            }

            RemovePartStatus(
                part,
                effect,
                StatusEffectRemoveReason.Cleared);
        }

        RemoveBrokenStatusForPart(
            part);

        part.SetDebugState(
            currentHp,
            maxHp,
            state == BodyPartState.Weakened,
            state == BodyPartState.Broken);

        switch (state)
        {
            case BodyPartState.Weakened:
                ApplyDisabledStatusForPart(
                    part);
                break;

            case BodyPartState.Broken:
                if (!clearNonStructuralStatuses)
                {
                    TransferPartStatusesToCharacter(
                        part);
                }

                OnBodyPartBroken(
                    part,
                    null);
                break;
        }

        return true;
    }

    public void RecoverPart(BodyPart part)
    {
        if (bodyPartController == null)
            return;

        bodyPartController.RecoverPart(part);
    }

    public void RestoreTemporaryWeakenedPart(
        BodyPart part,
        float hpBeforeTemporaryWeaken)
    {
        bodyPartController?.RestoreTemporaryWeakenedPart(
            part,
            hpBeforeTemporaryWeaken);
    }

    //------------------------------------------------

    public void ForceRecalculateHP()
    {
        if (RuntimeStatus == null)
            return;

        RuntimeStatus.currentHP =
            Mathf.Clamp(
                CalculateInitialHP(),
                0,
                MaxCombatHP);
    }

    public void WeakenPart(
        BodyPart part,
        Character source = null,
        BattleAction sourceAction = null)
    {
        bodyPartController?.WeakenPart(
            part,
            source ?? this,
            sourceAction);
    }

    public void AddTurnClashPowerBonus(int amount)
    {
        if (amount == 0)
            return;

        combatState?.AddTurnClashPowerBonus(amount);

        Debug.Log(
            $"{Data?.CharacterName ?? name} 이번 턴 합 위력 " +
            $"{amount:+#;-#;0} / 누적 {TurnClashPowerBonus:+#;-#;0}");
    }

    public bool TryBreakWeakenedPart(BodyPart part)
    {
        return TryBreakWeakenedPart(
            part,
            ActiveDamageContext?.Attacker,
            ActiveDamageContext?.Action);
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
            ActiveDamageContext?.Attacker,
            ActiveDamageContext?.Action);
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
            ActiveDamageContext);
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

        statusController?.AddStatus(
            effect,
            this,
            part);
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
    // 올라프 혈상 3스택, 유진 처형, 김삿갓 뼈 스택 등에서 사용
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
        statusController?.RemoveStatusesFromSourcePart(
            part,
            StatusEffectRemoveReason.PartBroken);
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
        statusController?.AddStatus(effect, source);
    }

    public void AddStatus(
        StatusEffect effect,
        Character source,
        BodyPart sourcePart)
    {
        statusController?.AddStatus(effect, source, sourcePart);
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
        statusController?.RemoveStatusesFromSourcePart(
            part,
            StatusEffectRemoveReason.PartRecovered);
    }

    public void RemoveDisabledStatusForPart(BodyPart part)
    {
        statusController?.RemoveDisabledStatusForPart(part);
    }

    //------------------------------------------------

    public void AddPrestige(int amount)
    {
        if (resourceController == null)
            return;

        resourceController.AddPrestige(amount);
    }

    public int GetCustomResource(string key)
    {
        return resourceController?.GetResource(key) ?? 0;
    }

    public void SetCustomResource(
        string key,
        int value,
        int max = int.MaxValue)
    {
        resourceController?.SetResource(
            key,
            value,
            max);
    }

    public void AddCustomResource(
        string key,
        int amount,
        int max = int.MaxValue)
    {
        resourceController?.AddResource(
            key,
            amount,
            max);
    }

    public bool TryConsumeCustomResource(
        string key,
        int amount)
    {
        return resourceController != null &&
               resourceController.TryConsumeResource(
                   key,
                   amount);
    }

    public bool CanAffordEnergy(int amount)
    {
        return resourceController != null &&
               resourceController.CanAffordEnergy(amount);
    }

    public bool TryConsumeEnergy(
        int amount,
        BattleAction sourceAction = null,
        Skill sourceSkill = null)
    {
        return resourceController != null &&
               resourceController.TryConsumeEnergy(
                   amount,
                   sourceAction,
                   sourceSkill);
    }

    public void AddEnergy(
        int amount,
        CombatResourceChangeReason reason =
            CombatResourceChangeReason.SkillEffect,
        BattleAction sourceAction = null,
        Skill sourceSkill = null)
    {
        resourceController?.AddEnergy(
            amount,
            reason,
            sourceAction,
            sourceSkill);
    }

    public void IncreaseEnergyMaximum(int amount, bool fillToMaximum = true)
    {
        resourceController?.IncreaseEnergyMaximum(amount, fillToMaximum);
    }

    public CharacterRuntimeSnapshot CaptureRuntimeSnapshot()
    {
        return CharacterRuntimeSnapshot.Capture(this);
    }

    //------------------------------------------------

    public int ModifyRoll(
        BattleAction action,
        int roll)
    {
        int value = roll;
        int commonShift = 0;
        int commonMaxReduction = 0;

        ApplyRollStatusList(
            StatusEffects,
            action,
            ref value,
            ref commonShift,
            ref commonMaxReduction);

        if (action?.OwnerPart != null)
        {
            ApplyRollStatusList(
                action.OwnerPart.StatusEffects,
                action,
                ref value,
                ref commonShift,
                ref commonMaxReduction);
        }

        if (mechanicController != null)
        {
            value =
                mechanicController.ModifyRoll(
                    action,
                    value);
        }

        // 공용 상태이상은 "범위 전체 이동"과 "최댓값 절단"을 별도 누산한다.
        // 따라서 힘 +1 & 골절 1은 상쇄되지 않고 바닥 +1 / 천장 유지가 된다.
        int nonCommonDelta = value - roll;
        value += commonShift;

        if (commonMaxReduction > 0 && action?.Skill != null)
        {
            int shiftedMaximum = Mathf.Max(
                1,
                action.Skill.MaxPower +
                nonCommonDelta +
                commonShift -
                commonMaxReduction);

            value = Mathf.Min(value, shiftedMaximum);
        }

        return value;
    }

    private static void ApplyRollStatusList(
        IReadOnlyList<StatusEffect> statuses,
        BattleAction action,
        ref int value,
        ref int commonShift,
        ref int commonMaxReduction)
    {
        if (statuses == null)
            return;

        foreach (StatusEffect effect in statuses)
        {
            if (effect == null)
                continue;

            if (effect is ICommonRollShiftStatus shiftStatus)
            {
                commonShift += shiftStatus.GetRollShift(action);
                continue;
            }

            if (effect is ICommonRollMaxReductionStatus maxStatus)
            {
                commonMaxReduction += Mathf.Max(
                    0,
                    maxStatus.GetMaxReduction(action));
                continue;
            }

            value = effect.ModifyRoll(action, value);
        }
    }

    public int ModifyExchangeRollCount(
        BattleAction action,
        int rollCount)
    {
        int value = Mathf.Max(1, rollCount);

        foreach (StatusEffect effect in StatusEffects)
        {
            if (effect != null)
            {
                value = Mathf.Max(
                    1,
                    effect.ModifyExchangeRollCount(
                        action,
                        value));
            }
        }

        if (action?.OwnerPart != null)
        {
            foreach (StatusEffect effect
                     in action.OwnerPart.StatusEffects)
            {
                if (effect != null)
                {
                    value = Mathf.Max(
                        1,
                        effect.ModifyExchangeRollCount(
                            action,
                            value));
                }
            }
        }

        return mechanicController == null
            ? value
            : mechanicController.ModifyExchangeRollCount(
                action,
                value);
    }

    public bool TryRequestExchangeReroll(
        ExchangeRerollContext context)
    {
        return mechanicController != null &&
               mechanicController.TryRequestExchangeReroll(
                   context);
    }

    public bool CanBreakPart(
        BodyPart part,
        BattleAction sourceAction)
    {
        return mechanicController == null ||
               mechanicController.CanBreakOwnerPart(
                   part,
                   sourceAction);
    }

    public bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        if (skill == null)
            return false;

        if (part != null &&
            !BodyPartSkillAccessPolicy.Allows(
                part,
                skill.ActionType))
        {
            return false;
        }

        if (part == null)
        {
            bool hasGlobalStructuredSlots =
                combatRulesRuntime?.GetSlotCountForPart(null) > 0;
            if (!IsSingleHpTarget && !hasGlobalStructuredSlots)
                return false;
        }
        else if ((part.Owner != null && part.Owner != this) ||
                 part.IsBroken)
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