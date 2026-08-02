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

        if (owner?.Data?.BossPhases == null)
            return result;

        foreach (BossPhaseData phase in owner.Data.BossPhases)
        {
            if (phase == null)
                continue;

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

        return BuildLegacySlotFallback();
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
    /// 유진은 네 부위가 각각 하나의 행동 원천이며, 현재 장착한
    /// 일반/결투/도사림/위세를 어느 정상 부위에서든 선택할 수 있다.
    /// 위세 1회 제한은 슬롯 카테고리가 아니라 PrestigeUsePolicy가 담당한다.
    /// 다른 캐릭터는 기존 CharacterSlotConfig 제한을 그대로 사용한다.
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

        if (owner is Yujin)
        {
            return slot.Part == null ||
                   !slot.Part.IsBroken;
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

    private IReadOnlyList<CharacterSlotConfig>
        BuildLegacySlotFallback()
    {
        List<CharacterSlotConfig> fallback = new();

        if (owner?.BodyParts == null ||
            owner.BodyParts.Count == 0)
        {
            // Single HP 캐릭터도 독립 슬롯 하나는 갖는다.
            fallback.Add(
                new CharacterSlotConfig
                {
                    SlotId = "LEGACY_CHARACTER_SLOT_01",
                    DisplayName = "행동 슬롯 1",
                    Enabled = true,
                    HasLinkedPart = false,
                    OverrideSpeedRange = false,
                    AllowedActionTypes =
                        new List<ActionType>
                        {
                            ActionType.NormalAttack,
                            ActionType.Duel,
                            ActionType.Preparation,
                            ActionType.Prestige
                        }
                });

            return fallback;
        }

        int index = 0;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            fallback.Add(
                new CharacterSlotConfig
                {
                    SlotId =
                        $"LEGACY_{part.Type}_{index:00}",
                    DisplayName =
                        part.Type.ToString(),
                    Enabled = true,
                    HasLinkedPart = true,
                    LinkedPartType = part.Type,
                    OverrideSpeedRange = false,
                    AllowedActionTypes =
                        new List<ActionType>
                        {
                            ActionType.NormalAttack,
                            ActionType.Duel,
                            ActionType.Preparation,
                            ActionType.Prestige
                        }
                });

            index++;
        }

        return fallback;
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