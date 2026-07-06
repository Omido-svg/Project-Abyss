using System;
using System.Collections.Generic;
using UnityEngine;

public class BattleDebugTuner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleUIManager battleUIManager;

    [Header("Apply Option")]
    [SerializeField] private bool liveApply = true;
    [SerializeField] private bool refreshUIAfterApply = true;
    
    [Header("View Refresh")]
    [SerializeField] private bool refreshCharacterViewAfterApply = true;

    [Header("Momentum")]
    [SerializeField] private bool overrideMomentum = false;
    [Range(-100f, 100f)]
    [SerializeField] private float momentum = 0f;

    [Header("Player Prestige")]
    [SerializeField] private bool overridePlayerPrestige = false;
    [SerializeField] private int playerCurrentPrestige = 0;
    [SerializeField] private int playerMaxPrestige = 100;

    [Header("Enemy Prestige")]
    [SerializeField] private bool overrideEnemyPrestige = false;

    [Header("Player Parts")]
    [SerializeField] private bool overridePlayerParts = false;
    [SerializeField] private List<PartTuningValue> playerParts = new();

    [Header("Enemy Parts")]
    [SerializeField] private bool overrideEnemyParts = false;
    [SerializeField] private List<EnemyTuningValue> enemies = new();

    private void Awake()
    {
        FindReferences();
    }

    private void Start()
    {
        FindReferences();
        InitializeDefaultLists();
        Apply();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (!liveApply)
            return;

        Apply();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        FindReferences();
        Apply();
    }

    private void FindReferences()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (battleUIManager == null)
            battleUIManager = FindFirstObjectByType<BattleUIManager>();
    }

    [ContextMenu("Initialize Default Lists")]
    public void InitializeDefaultLists()
    {
        if (!IsReady())
            return;

        playerParts.Clear();
        enemies.Clear();

        InitializePlayerPartList();
        InitializeEnemyList();
    }

    private void InitializePlayerPartList()
    {
        Character player = battleManager.BattleContext.Player;

        if (player == null || player.BodyParts == null)
            return;

        playerParts.Clear();

        for (int i = 0; i < player.BodyParts.Count; i++)
        {
            BodyPart part = player.BodyParts[i];

            if (part == null)
                continue;

            playerParts.Add(new PartTuningValue
            {
                partIndex = i,
                currentHP = Mathf.RoundToInt(part.PartHP),
                maxHP = Mathf.RoundToInt(part.MaxPartHP),
                isWeakened = part.IsWeakened,
                isBroken = part.IsBroken
            });
        }
    }

    private void InitializeEnemyList()
    {
        List<Character> enemyList = battleManager.BattleContext.Enemies;

        if (enemyList == null)
            return;

        enemies.Clear();

        for (int enemyIndex = 0; enemyIndex < enemyList.Count; enemyIndex++)
        {
            Character enemy = enemyList[enemyIndex];

            EnemyTuningValue enemyValue = new EnemyTuningValue
            {
                enemyIndex = enemyIndex,
                currentPrestige = GetCurrentPrestige(enemy),
                maxPrestige = GetMaxPrestige(enemy),
                parts = new List<PartTuningValue>()
            };

            if (enemy != null && enemy.BodyParts != null)
            {
                for (int partIndex = 0; partIndex < enemy.BodyParts.Count; partIndex++)
                {
                    BodyPart part = enemy.BodyParts[partIndex];

                    if (part == null)
                        continue;

                    enemyValue.parts.Add(new PartTuningValue
                    {
                        partIndex = partIndex,
                        currentHP = Mathf.RoundToInt(part.PartHP),
                        maxHP = Mathf.RoundToInt(part.MaxPartHP),
                        isWeakened = part.IsWeakened,
                        isBroken = part.IsBroken
                    });
                }
            }

            enemies.Add(enemyValue);
        }
    }

    [ContextMenu("Apply Now")]
    public void Apply()
    {
        if (!IsReady())
            return;

        ApplyMomentum();
        ApplyPlayerPrestige();
        ApplyEnemyPrestige();
        ApplyPlayerParts();
        ApplyEnemyParts();

        if (refreshUIAfterApply && battleUIManager != null)
            battleUIManager.RefreshAllBodyPartButtons();
    }

    private void ApplyMomentum()
    {
        if (!overrideMomentum)
            return;

        if (battleManager.MomentumManager == null)
            return;

        battleManager.MomentumManager.SetMomentumForDebug(momentum);
    }

    private void ApplyPlayerPrestige()
    {
        if (!overridePlayerPrestige)
            return;

        Character player = battleManager.BattleContext.Player;

        if (player == null)
            return;

        int maxPrestige =
            Mathf.Max(1, playerMaxPrestige);

        int currentPrestige =
            Mathf.Clamp(
                playerCurrentPrestige,
                0,
                maxPrestige);

        if (player.CurrentStatus != null)
            player.CurrentStatus.maxPrestige = maxPrestige;

        if (player.RuntimeStatus != null)
            player.RuntimeStatus.currentPrestige = currentPrestige;
    }

    private void ApplyEnemyPrestige()
    {
        if (!overrideEnemyPrestige)
            return;

        List<Character> enemyList =
            battleManager.BattleContext.Enemies;

        if (enemyList == null)
            return;

        foreach (EnemyTuningValue enemyValue in enemies)
        {
            if (enemyValue == null)
                continue;

            if (enemyValue.enemyIndex < 0 ||
                enemyValue.enemyIndex >= enemyList.Count)
                continue;

            Character enemy =
                enemyList[enemyValue.enemyIndex];

            if (enemy == null)
                continue;

            int maxPrestige =
                Mathf.Max(1, enemyValue.maxPrestige);

            int currentPrestige =
                Mathf.Clamp(
                    enemyValue.currentPrestige,
                    0,
                    maxPrestige);

            if (enemy.CurrentStatus != null)
                enemy.CurrentStatus.maxPrestige = maxPrestige;

            if (enemy.RuntimeStatus != null)
                enemy.RuntimeStatus.currentPrestige = currentPrestige;
        }
    }

    private void ApplyPlayerParts()
    {
        if (!overridePlayerParts)
            return;

        Character player = battleManager.BattleContext.Player;

        if (player == null)
            return;

        ApplyPartList(player, playerParts);
    }

    private void ApplyEnemyParts()
    {
        if (!overrideEnemyParts)
            return;

        List<Character> enemyList = battleManager.BattleContext.Enemies;

        if (enemyList == null)
            return;

        foreach (EnemyTuningValue enemyValue in enemies)
        {
            if (enemyValue == null)
                continue;

            if (enemyValue.enemyIndex < 0 ||
                enemyValue.enemyIndex >= enemyList.Count)
                continue;

            Character enemy = enemyList[enemyValue.enemyIndex];

            if (enemy == null)
                continue;

            ApplyPartList(enemy, enemyValue.parts);
        }
    }

    private void ApplyPartList(
        Character character,
        List<PartTuningValue> values)
    {
        if (character == null)
            return;

        if (character.BodyParts == null)
            return;

        if (values == null)
            return;

        foreach (PartTuningValue value in values)
        {
            if (value == null)
                continue;

            if (value.partIndex < 0 ||
                value.partIndex >= character.BodyParts.Count)
                continue;

            BodyPart part = character.BodyParts[value.partIndex];

            if (part == null)
                continue;

            part.SetDebugState(
                value.currentHP,
                value.maxHP,
                value.isWeakened,
                value.isBroken);
        }

        character.ForceRecalculateHP();

        RefreshCharacterView(character);
    }
    
    private void RefreshCharacterView(Character character)
    {
        if (!refreshCharacterViewAfterApply)
            return;

        if (character == null)
            return;

        CharacterView characterView =
            character.GetComponent<CharacterView>();

        if (characterView == null)
            characterView = character.GetComponentInChildren<CharacterView>();

        if (characterView == null)
            return;

        characterView.RefreshVisualState();
    }

    private int GetCurrentPrestige(Character character)
    {
        if (character == null)
            return 0;

        if (character.RuntimeStatus == null)
            return 0;

        return character.RuntimeStatus.currentPrestige;
    }

    private int GetMaxPrestige(Character character)
    {
        if (character == null)
            return 100;

        if (character.CurrentStatus == null)
            return 100;

        return character.CurrentStatus.maxPrestige;
    }

    private bool IsReady()
    {
        if (battleManager == null)
            return false;

        if (battleManager.BattleContext == null)
            return false;

        return true;
    }
}

[Serializable]
public class PartTuningValue
{
    public int partIndex;

    public int currentHP = 50;
    public int maxHP = 50;

    public bool isWeakened = false;
    public bool isBroken = false;
}

[Serializable]
public class EnemyTuningValue
{
    public int enemyIndex;

    [Header("Prestige")]
    public int currentPrestige = 0;
    public int maxPrestige = 100;

    [Header("Parts")]
    public List<PartTuningValue> parts = new();
}