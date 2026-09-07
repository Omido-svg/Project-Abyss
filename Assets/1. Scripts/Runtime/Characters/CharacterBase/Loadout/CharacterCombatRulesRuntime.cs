using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 최신 전투 규칙에서 Character의 슬롯, 장착 스킬, 보스 페이즈를 통합한다.
/// ScriptableObject 원본을 런타임에 직접 수정하지 않는다.
/// </summary>
public sealed class CharacterCombatRulesRuntime
{
    private readonly Character owner;
    private readonly BossPhaseController bossPhaseController;
    private readonly CharacterSkillLoadoutRuntime loadoutRuntime;

    // 과거 CharacterSlotConfig는 슬롯마다 속도 범위를 Override할 수 있었지만,
    // 현재 규칙에서는 같은 BodyPart의 모든 ActionSlot이 하나의 속도를 공유한다.
    // 충돌 데이터가 있을 때 같은 경고를 매 턴 반복하지 않도록 기록한다.
    private readonly HashSet<string> warnedSharedSpeedOverrideConflicts = new();

    public BossPhaseData CurrentBossPhase =>
        bossPhaseController?.CurrentPhase;

    public bool HasStructuredRules =>
        loadoutRuntime?.HasSource == true ||
        (owner?.Data?.ActionSlots != null && owner.Data.ActionSlots.Count > 0) ||
        (owner?.Data?.BossPhases != null && owner.Data.BossPhases.Count > 0);

    public CharacterSkillLoadoutRuntime Loadout =>
        loadoutRuntime;

    public event Action<BossPhaseData, BossPhaseData> BossPhaseChanged
    {
        add
        {
            if (bossPhaseController != null)
                bossPhaseController.PhaseChanged += value;
        }
        remove
        {
            if (bossPhaseController != null)
                bossPhaseController.PhaseChanged -= value;
        }
    }

    public event Action LoadoutChanged
    {
        add
        {
            if (loadoutRuntime != null)
                loadoutRuntime.LoadoutChanged += value;
        }
        remove
        {
            if (loadoutRuntime != null)
                loadoutRuntime.LoadoutChanged -= value;
        }
    }

    public CharacterCombatRulesRuntime(
        Character owner)
    {
        this.owner = owner;

        loadoutRuntime =
            new CharacterSkillLoadoutRuntime(
                owner,
                owner?.CombatLoadoutSource);

        if (owner?.Data?.BossPhases != null &&
            owner.Data.BossPhases.Count > 0)
        {
            bossPhaseController =
                new BossPhaseController(
                    owner,
                    owner.Data.BossPhases);
        }
    }

    public bool EvaluateBossPhase(
        BattleContext context,
        int currentTurn)
    {
        if (bossPhaseController == null)
            return false;

        return bossPhaseController.Evaluate(
            context,
            currentTurn);
    }

    public List<Skill> CreateRuntimeSkills()
    {
        List<Skill> result = new();

        CharacterCombatLoadout loadout =
            owner?.CombatLoadoutSource;

        if (loadout != null)
        {
            foreach (SkillDefinition definition
                     in loadout.EnumerateAllDefinitions())
            {
                AddRuntimeSkill(
                    result,
                    definition);
            }
        }

        // FixedSkill / Energy fallback은 슬롯 설정 자체가 런타임 공급원이 된다.
        // 별도의 SkillPool 등록을 빼먹어도 configured slot이 조용히 비는 일을 막는다.
        AddConfiguredSlotSkills(
            result,
            owner?.Data?.ActionSlots);

        if (owner?.Data?.BossPhases == null)
            return result;

        foreach (BossPhaseData phase in owner.Data.BossPhases)
        {
            if (phase == null)
                continue;

            AddConfiguredSlotSkills(
                result,
                phase.SlotConfigs);

            foreach (SkillDefinition definition
                     in phase.EnumerateSkillPool())
            {
                AddRuntimeSkill(
                    result,
                    definition);
            }
        }

        return result;
    }

    private void AddConfiguredSlotSkills(
        List<Skill> destination,
        IReadOnlyList<CharacterSlotConfig> configs)
    {
        if (destination == null ||
            configs == null)
        {
            return;
        }

        for (int i = 0; i < configs.Count; i++)
        {
            CharacterSlotConfig config = configs[i];
            if (config == null || !config.Enabled)
                continue;

            AddRuntimeSkill(
                destination,
                config.FixedSkill);

            AddRuntimeSkill(
                destination,
                config.InsufficientEnergyFallbackSkill);
        }
    }

    private void AddRuntimeSkill(
        List<Skill> destination,
        SkillDefinition definition)
    {
        if (destination == null ||
            definition == null ||
            ContainsDefinition(
                destination,
                definition))
        {
            return;
        }

        Skill skill =
            owner?.CreateRuntimeSkillForLoadout(
                definition) ??
            definition.CreateRuntimeSkill();

        if (skill != null)
            destination.Add(skill);
    }

    private static bool ContainsDefinition(
        IReadOnlyList<Skill> skills,
        SkillDefinition definition)
    {
        if (skills == null || definition == null)
            return false;

        foreach (Skill skill in skills)
        {
            if (skill?.Definition == definition)
                return true;
        }

        return false;
    }

    public int GetActiveSlotCount()
    {
        int count = 0;

        foreach (CharacterSlotConfig config in GetActiveSlotConfigs())
        {
            if (config != null && config.Enabled)
                count++;
        }

        return count;
    }

    public int GetActiveCombatSlotCount()
    {
        int count = 0;
        foreach (CharacterSlotConfig config in GetActiveSlotConfigs())
        {
            if (config == null || !config.Enabled)
                continue;
            if (config.Allows(ActionType.NormalAttack) ||
                config.Allows(ActionType.Duel) ||
                config.Allows(ActionType.Prestige))
            {
                count++;
            }
        }
        return count;
    }

    public IReadOnlyList<CharacterSlotConfig>
        GetActiveSlotConfigs()
    {
        if (CurrentBossPhase != null &&
            CurrentBossPhase.SlotConfigs != null &&
            CurrentBossPhase.SlotConfigs.Count > 0)
        {
            return CurrentBossPhase.SlotConfigs;
        }

        if (owner?.Data?.ActionSlots != null &&
            owner.Data.ActionSlots.Count > 0)
        {
            return owner.Data.ActionSlots;
        }

        return System.Array.Empty<CharacterSlotConfig>();
    }

    /// <summary>
    /// 같은 BodyPart에 연결된 모든 슬롯이 공유할 속도 범위 Override를 반환한다.
    ///
    /// CharacterSlotConfig의 직렬화 필드는 호환성을 위해 유지하지만,
    /// OverrideSpeedRange는 더 이상 "개별 슬롯 속도"가 아니다.
    /// 같은 부위의 여러 슬롯 중 Override가 하나라도 있으면 그 범위를 부위 전체에 적용한다.
    /// 서로 다른 Override 범위가 동시에 존재하면 첫 번째 활성 설정을 사용하고 1회 경고한다.
    /// </summary>
    public bool TryGetSharedSpeedRange(
        BodyPart linkedPart,
        out int minSpeed,
        out int maxSpeed)
    {
        minSpeed = 0;
        maxSpeed = 0;

        IReadOnlyList<CharacterSlotConfig> configs =
            GetActiveSlotConfigs();

        if (configs == null)
            return false;

        CharacterSlotConfig selected = null;

        foreach (CharacterSlotConfig config in configs)
        {
            if (config == null ||
                !config.Enabled ||
                !config.OverrideSpeedRange ||
                ResolveLinkedPart(config) != linkedPart)
            {
                continue;
            }

            int candidateMin =
                Mathf.Max(
                    0,
                    config.MinSpeed);

            int candidateMax =
                Mathf.Max(
                    candidateMin,
                    config.MaxSpeed);

            if (selected == null)
            {
                selected = config;
                minSpeed = candidateMin;
                maxSpeed = candidateMax;
                continue;
            }

            if (candidateMin == minSpeed &&
                candidateMax == maxSpeed)
            {
                continue;
            }

            string partKey =
                linkedPart != null
                    ? linkedPart.Type.ToString()
                    : "CHARACTER";

            if (warnedSharedSpeedOverrideConflicts.Add(partKey))
            {
                Debug.LogWarning(
                    $"[Shared Part Speed] {owner?.Data?.CharacterName ?? owner?.name ?? "UNKNOWN"} / " +
                    $"{partKey}에 서로 다른 슬롯별 속도 Override가 설정되어 있습니다. " +
                    $"같은 부위 슬롯은 하나의 속도를 공유하므로 첫 범위 " +
                    $"{minSpeed}~{maxSpeed}를 부위 전체에 사용합니다.");
            }
        }

        return selected != null;
    }

    public CharacterSlotConfig GetSlotConfig(
        BodyPart linkedPart,
        int actionIndex)
    {
        IReadOnlyList<CharacterSlotConfig> configs =
            GetActiveSlotConfigs();

        if (configs == null)
            return null;

        int matchedIndex = 0;

        foreach (CharacterSlotConfig config in configs)
        {
            if (config == null ||
                !config.Enabled)
            {
                continue;
            }

            BodyPart configPart =
                ResolveLinkedPart(config);

            if (configPart != linkedPart)
                continue;

            if (matchedIndex ==
                Mathf.Max(0, actionIndex))
            {
                return config;
            }

            matchedIndex++;
        }

        return null;
    }

    public int GetSlotCountForPart(
        BodyPart linkedPart)
    {
        IReadOnlyList<CharacterSlotConfig> configs =
            GetActiveSlotConfigs();

        if (configs == null)
            return 0;

        int count = 0;

        foreach (CharacterSlotConfig config in configs)
        {
            if (config == null ||
                !config.Enabled)
            {
                continue;
            }

            if (ResolveLinkedPart(config) == linkedPart)
                count++;
        }

        return count;
    }

    /// <summary>
    /// 구조적으로 선택 가능한 스킬만 반환한다.
    /// 빛, 상태이상, 위세 같은 현재 자원 조건은 UI/AI의 CanUseSkill에서 별도 검사한다.
    /// </summary>
    public IReadOnlyList<Skill>
        GetAvailableSkillsForSlot(
            ActionSlot slot,
            IReadOnlyList<Skill> runtimeSkills)
    {
        List<Skill> result = new();

        if (slot == null ||
            runtimeSkills == null)
        {
            return result;
        }

        CharacterSlotConfig config = slot.SlotConfig;
        if (config?.FixedSkill != null)
        {
            AddRuntimeDefinitionMatch(result, runtimeSkills, config.FixedSkill);
            AddRuntimeDefinitionMatch(result, runtimeSkills, config.InsufficientEnergyFallbackSkill);
            return result;
        }

        foreach (Skill skill in runtimeSkills)
        {
            if (skill == null ||
                !IsAllowedForSlot(slot, skill) ||
                !IsAllowedByActiveSkillSet(skill))
            {
                continue;
            }

            AddUnique(result, skill);
        }

        return result;
    }

    /// <summary>
    /// 부위형 캐릭터의 스킬 카테고리는 전 캐릭터 공통 부위 계약을 따른다.
    /// - 머리: 일반 / 결투 / 도사림 / 위세
    /// - 양 팔: 일반 / 결투
    /// - 다리: 도사림
    ///
    /// 이 규칙은 유진을 포함해 동일하게 적용한다. 과거 Asset의
    /// AllowedActionTypes가 다른 규칙을 가지고 있어도 런타임 계약이 우선한다.
    /// part == null인 단일 HP 전투원은 CharacterSlotConfig 규칙을 사용한다.
    /// </summary>
    private bool IsAllowedForSlot(
        ActionSlot slot,
        Skill skill)
    {
        if (slot == null ||
            skill == null)
        {
            return false;
        }

        if (slot.SlotConfig != null &&
            !slot.SlotConfig.Enabled)
        {
            return false;
        }

        if (slot.Part != null)
        {
            return BodyPartSkillAccessPolicy.Allows(
                slot.Part,
                skill.ActionType);
        }

        return slot.AllowsSkill(skill);
    }

    public bool TryEquipSkill(
        SkillDefinition definition,
        out string reason)
    {
        if (CurrentBossPhase != null)
        {
            reason =
                "보스 페이즈 스킬 풀은 페이즈 데이터로 교체되므로 " +
                "전투 중 장착을 변경할 수 없습니다.";
            return false;
        }

        if (loadoutRuntime == null)
        {
            reason =
                "런타임 스킬 장착 데이터가 초기화되지 않았습니다.";
            return false;
        }

        return loadoutRuntime.TryEquip(
            definition,
            out reason);
    }

    public bool TryUnequipSkill(
        SkillDefinition definition)
    {
        if (CurrentBossPhase != null)
            return false;

        return loadoutRuntime != null &&
               loadoutRuntime.TryUnequip(
                   definition);
    }

    public bool TryReplaceSkill(
        SkillDefinition equipped,
        SkillDefinition replacement,
        out string reason)
    {
        if (CurrentBossPhase != null)
        {
            reason =
                "보스 페이즈 중에는 런타임 장착 스킬을 교체할 수 없습니다.";
            return false;
        }

        if (loadoutRuntime == null)
        {
            reason =
                "런타임 스킬 장착 데이터가 초기화되지 않았습니다.";
            return false;
        }

        return loadoutRuntime.TryReplace(
            equipped,
            replacement,
            out reason);
    }

    public CharacterSkillLoadoutState CaptureLoadoutState()
    {
        return loadoutRuntime?.CaptureState();
    }

    public bool TryRestoreLoadoutState(
        CharacterSkillLoadoutState state,
        out string reason)
    {
        if (CurrentBossPhase != null)
        {
            reason =
                "보스 페이즈 중에는 장착 상태를 복원할 수 없습니다.";
            return false;
        }

        if (loadoutRuntime == null)
        {
            reason =
                "런타임 스킬 장착 데이터가 초기화되지 않았습니다.";
            return false;
        }

        return loadoutRuntime.TryRestoreState(
            state,
            out reason);
    }

    public IReadOnlyList<SkillDefinition>
        GetEquippedDefinitions(
            ActionType actionType)
    {
        return loadoutRuntime?.GetEquipped(
                   actionType) ??
               Array.Empty<SkillDefinition>();
    }

    public IEnumerable<SkillDefinition>
        EnumerateSkillCandidates(
            ActionType actionType)
    {
        if (loadoutRuntime == null)
            yield break;

        foreach (SkillDefinition definition
                 in loadoutRuntime
                     .EnumerateCandidates(actionType))
        {
            yield return definition;
        }
    }

    public IEnumerable<SkillDefinition>
        EnumeratePreparationCandidates()
    {
        foreach (SkillDefinition definition
                 in EnumerateSkillCandidates(
                     ActionType.Preparation))
        {
            yield return definition;
        }
    }

    public IEnumerable<SkillDefinition>
        EnumerateCurrentPhaseSkillPool()
    {
        if (CurrentBossPhase == null)
            yield break;

        foreach (SkillDefinition definition
                 in CurrentBossPhase.EnumerateSkillPool())
        {
            if (definition != null)
                yield return definition;
        }
    }

    private bool IsAllowedByActiveSkillSet(
        Skill skill)
    {
        if (skill == null)
            return false;

        SkillDefinition definition =
            skill.Definition;

        if (CurrentBossPhase != null)
        {
            if (definition == null)
                return false;

            foreach (SkillDefinition candidate
                     in CurrentBossPhase
                         .EnumerateSkillPool())
            {
                if (candidate == definition)
                    return true;
            }

            return false;
        }

        if (loadoutRuntime?.HasSource == true)
        {
            return definition != null &&
                   loadoutRuntime.IsEquipped(
                       definition);
        }

        // 아직 CharacterCombatLoadout을 연결하지 않은 구형 캐릭터는
        // 기존 BodyPart/SkillSet 스킬을 그대로 사용할 수 있다.
        return true;
    }

    public BodyPart GetLinkedPart(
        CharacterSlotConfig config)
    {
        return ResolveLinkedPart(config);
    }

    public int GetActionIndex(
        CharacterSlotConfig targetConfig)
    {
        if (targetConfig == null)
            return -1;

        BodyPart targetPart =
            ResolveLinkedPart(targetConfig);

        int actionIndex = 0;

        foreach (CharacterSlotConfig config in GetActiveSlotConfigs())
        {
            if (config == null || !config.Enabled)
                continue;

            if (ReferenceEquals(config, targetConfig))
                return actionIndex;

            if (ResolveLinkedPart(config) == targetPart)
                actionIndex++;
        }

        return -1;
    }

    private BodyPart ResolveLinkedPart(
        CharacterSlotConfig config)
    {
        if (config == null ||
            !config.HasLinkedPart ||
            owner?.BodyParts == null)
        {
            return null;
        }

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part != null &&
                part.Type ==
                config.LinkedPartType)
            {
                return part;
            }
        }

        return null;
    }


    private static void AddRuntimeDefinitionMatch(
        List<Skill> destination,
        IReadOnlyList<Skill> runtimeSkills,
        SkillDefinition definition)
    {
        if (destination == null || runtimeSkills == null || definition == null)
            return;

        for (int i = 0; i < runtimeSkills.Count; i++)
        {
            Skill skill = runtimeSkills[i];
            if (skill?.Definition == definition)
            {
                AddUnique(destination, skill);
                return;
            }
        }
    }
    private static void AddUnique(
        List<Skill> destination,
        Skill skill)
    {
        if (destination == null ||
            skill == null)
        {
            return;
        }

        foreach (Skill existing in destination)
        {
            if (existing == null)
                continue;

            if (ReferenceEquals(existing, skill))
                return;

            if (existing.Definition != null &&
                existing.Definition ==
                skill.Definition)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(
                    existing.SkillName) &&
                string.Equals(
                    existing.SkillName,
                    skill.SkillName,
                    StringComparison.Ordinal))
            {
                return;
            }
        }

        destination.Add(skill);
    }
}