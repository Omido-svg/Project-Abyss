using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Enemy/Boss Phase Data",
    fileName = "BossPhaseData")]
public sealed class BossPhaseData : ScriptableObject
{
    public string PhaseId = "PHASE_01";
    public string DisplayName = "Phase 1";

    [Header("Transition condition")]
    [Range(0f, 1f)]
    public float EnterAtOrBelowHpRate = 1f;

    public int MinimumTurn = 1;

    public bool RequireMomentumAtOrBelow;
    [Range(-100, 100)]
    public int MomentumAtOrBelow;

    [Header("Runtime replacement")]
    public List<CharacterSlotConfig> SlotConfigs = new();

    public List<SkillDefinition> NormalSkillPool = new();
    public List<SkillDefinition> DuelSkillPool = new();
    public List<SkillDefinition> PreparationSkillPool = new();
    public List<SkillDefinition> PrestigeSkillPool = new();

    [Header("AI weights")]
    public float NormalWeight = 1f;
    public float DuelWeight = 1f;
    public float PreparationWeight = 1f;
    public float PrestigeWeight = 1f;

    public BossPhaseQueuedActionPolicy QueuedActionPolicy =
        BossPhaseQueuedActionPolicy.KeepAlreadyPlanned;

    public bool IsSatisfied(
        Character owner,
        BattleContext context,
        int currentTurn)
    {
        if (owner == null)
            return false;

        if (currentTurn < Mathf.Max(1, MinimumTurn))
            return false;

        int maxHp = Mathf.Max(1, owner.MaxCombatHP);
        float hpRate =
            Mathf.Clamp01((float)owner.CurrentHP / maxHp);

        if (hpRate > EnterAtOrBelowHpRate)
            return false;

        if (RequireMomentumAtOrBelow)
        {
            MomentumManager manager =
                context?.battleManager?.MomentumManager;

            if (manager == null)
                return false;

            int relative =
                manager.GetPerspectiveValue(owner);

            if (relative > MomentumAtOrBelow)
                return false;
        }

        return true;
    }

    public IEnumerable<SkillDefinition> EnumerateSkillPool()
    {
        foreach (SkillDefinition value in Enumerate(NormalSkillPool))
            yield return value;

        foreach (SkillDefinition value in Enumerate(DuelSkillPool))
            yield return value;

        foreach (SkillDefinition value in Enumerate(PreparationSkillPool))
            yield return value;

        foreach (SkillDefinition value in Enumerate(PrestigeSkillPool))
            yield return value;
    }

    private static IEnumerable<SkillDefinition> Enumerate(
        List<SkillDefinition> list)
    {
        if (list == null)
            yield break;

        foreach (SkillDefinition value in list)
        {
            if (value != null)
                yield return value;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        MinimumTurn = Mathf.Max(1, MinimumTurn);

        SlotConfigs ??= new List<CharacterSlotConfig>();
        NormalSkillPool ??= new List<SkillDefinition>();
        DuelSkillPool ??= new List<SkillDefinition>();
        PreparationSkillPool ??= new List<SkillDefinition>();
        PrestigeSkillPool ??= new List<SkillDefinition>();

        for (int i = 0; i < SlotConfigs.Count; i++)
            SlotConfigs[i]?.Sanitize(i);
    }
#endif
}
