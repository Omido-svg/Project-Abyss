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

    [Header("Momentum")]
    [SerializeField] private bool overrideMomentum = false;
    [Range(-100f, 100f)]
    [SerializeField] private float momentum = 0f;

    [Header("Player Prestige")]
    [SerializeField] private bool overridePlayerPrestige = false;
    [SerializeField] private int playerCurrentPrestige = 0;
    [SerializeField] private int playerMaxPrestige = 100;

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

        InitializePlayerPartList();
        InitializeEnemyPartList();
    }

    private void InitializePlayerPartList()
    {
        Character player = battleManager.BattleContext.Player;

        if (player == null || player.BodyParts == null)
            return;

        while (playerParts.Count < player.BodyParts.Count)
        {
            BodyPart part = player.BodyParts[playerParts.Count];

            playerParts.Add(new PartTuningValue
            {
                partIndex = playerParts.Count,
                currentHP = (int)part.PartHP,
                maxHP = (int)part.MaxPartHP,
                isUsable = part.IsUsable,
                isWeakened = part.IsWeakened,
                isBroken = part.IsBroken
            });
        }
    }

    private void InitializeEnemyPartList()
    {
        List<Character> enemyList = battleManager.BattleContext.Enemies;

        if (enemyList == null)
            return;

        while (enemies.Count < enemyList.Count)
        {
            Character enemy = enemyList[enemies.Count];

            EnemyTuningValue enemyValue = new EnemyTuningValue
            {
                enemyIndex = enemies.Count,
                parts = new List<PartTuningValue>()
            };

            if (enemy != null && enemy.BodyParts != null)
            {
                for (int i = 0; i < enemy.BodyParts.Count; i++)
                {
                    BodyPart part = enemy.BodyParts[i];

                    enemyValue.parts.Add(new PartTuningValue
                    {
                        partIndex = i,
                        currentHP =(int) part.PartHP,
                        maxHP = (int)part.MaxPartHP,
                        isUsable = part.IsUsable,
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

        if (player.CurrentStatus != null)
            player.CurrentStatus.maxPrestige = playerMaxPrestige;

        if (player.RuntimeStatus != null)
            player.RuntimeStatus.currentPrestige = playerCurrentPrestige;
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

    public bool isUsable = true;
    public bool isWeakened = false;
    public bool isBroken = false;
}

[Serializable]
public class EnemyTuningValue
{
    public int enemyIndex;
    public List<PartTuningValue> parts = new();
}