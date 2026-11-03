
using System.Collections.Generic;
using System;
using UnityEngine;

public interface IFervorTurnEndGainModifier
{
    int ModifyTurnEndFervorGain(MomentumState finalState, int currentGain);
}

public readonly struct FervorLevelUpContext
{
    public readonly int Turn;
    public readonly int PreviousLevel;
    public readonly int NewLevel;
    public readonly int RemainingExaltation;

    public FervorLevelUpContext(int turn, int previousLevel, int newLevel, int remainingExaltation)
    {
        Turn = turn;
        PreviousLevel = previousLevel;
        NewLevel = newLevel;
        RemainingExaltation = remainingExaltation;
    }
}

/// <summary>
/// 고조(누적 칸)와 열광(0~3)을 전투 단위로 소유한다.
/// 기세 턴 종료 상태만 소비하며 MomentumManager의 실시간 이동 책임과 분리한다.
/// </summary>
public sealed class FervorManager
{
    private readonly BattleContext context;
    private readonly MomentumManager momentum;
    private readonly FervorRuleSettings settings;

    public int Exaltation { get; private set; }
    

    // The canonical rule assigns a separate mirrored bar to every enemy.
    // Player properties stay intact for existing MVP/EmotionAugment API.
    private sealed class EnemyProgress
    {
        public int Exaltation;
        public int Level;
    }
    private readonly Dictionary<Character, EnemyProgress> enemyProgress =
        new Dictionary<Character, EnemyProgress>();

    public event Action<Character, int, int> EnemyExaltationChanged;
    public event Action<Character, FervorLevelUpContext> EnemyLevelUp;

    public int GetExaltation(Character owner)
    {
        if (owner == null) return 0;
        if (owner == context?.Player) return Exaltation;
        return enemyProgress.TryGetValue(owner, out var state) ? state.Exaltation : 0;
    }

    public int GetFervorLevel(Character owner)
    {
        if (owner == null) return 0;
        if (owner == context?.Player) return FervorLevel;
        return enemyProgress.TryGetValue(owner, out var state) ? state.Level : 0;
    }

    /// <summary>
    /// The actual end-turn award for this unit at the CURRENT momentum
    /// position. A UI can show 0 explicitly for disadvantage/last stand.
    /// No state mutation, energy spending or turn advancement occurs.
    /// </summary>
    public int PreviewTurnEndGain(Character owner)
    {
        if (owner == null || owner.IsDead || momentum == null ||
            GetFervorLevel(owner) >= settings.MaximumLevel)
            return 0;

        MomentumState state = momentum.GetFinalTurnState(owner);
        int gain = settings.GetGain(state);
        if (owner.Mechanics != null)
        {
            foreach (CombatMechanic mechanic in owner.Mechanics)
            {
                if (mechanic is IFervorTurnEndGainModifier modifier)
                    gain = Mathf.Max(0, modifier.ModifyTurnEndFervorGain(state, gain));
            }
        }
        return Mathf.Max(0, gain);
    }
public int FervorLevel { get; private set; }

    public event Action<int, int> ExaltationChanged;
    public event Action<FervorLevelUpContext> LevelUp;

    public FervorManager(BattleContext context, MomentumManager momentum)
    {
        this.context = context;
        this.momentum = momentum;
        settings = context?.Rules?.Fervor ?? new FervorRuleSettings();
        settings.Normalize();
    }

public void ResetForBattle()
    {
        Exaltation = 0;
        FervorLevel = 0;
        enemyProgress.Clear();
    }

public void ResolveTurnEnd(int turn)
    {
        if (context?.Player == null || momentum == null)
            return;

        ResolveOwner(context.Player, turn, true);

        // Each living enemy advances independently from the mirror
        // perspective of the same shared momentum bar.
        if (context.Enemies == null)
            return;

        foreach (Character enemy in context.Enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            ResolveOwner(enemy, turn, false);
        }
    }

    private void ResolveOwner(Character owner, int turn, bool playerSide)
    {
        if (owner == null || owner.IsDead)
            return;

        EnemyProgress enemyState = null;
        if (!playerSide && !enemyProgress.TryGetValue(owner, out enemyState))
        {
            enemyState = new EnemyProgress();
            enemyProgress.Add(owner, enemyState);
        }

        int level = playerSide ? FervorLevel : enemyState.Level;
        int before = playerSide ? Exaltation : enemyState.Exaltation;
        int exaltation = before;
        int gain = PreviewTurnEndGain(owner);
        MomentumState finalState = momentum.GetFinalTurnState(owner);
        int finalMomentum = momentum.GetPerspectiveValue(owner);

        // Level cap is absorbing: additional charge and overflow vanish.
        if (level >= settings.MaximumLevel)
            exaltation = 0;
        else
            exaltation += gain;

        while (level < settings.MaximumLevel)
        {
            int cost = settings.GetCostForNextLevel(level);
            if (cost <= 0 || exaltation < cost)
                break;

            int oldLevel = level;
            exaltation -= cost;
            level++;

            if (level >= settings.MaximumLevel)
                exaltation = 0;

            
            // Preserve the public runtime contract: LevelUp observers must
            // see the new level and remaining charge immediately.
            if (playerSide)
            {
                FervorLevel = level;
                Exaltation = exaltation;
            }
            else
            {
                enemyState.Level = level;
                enemyState.Exaltation = exaltation;
            }
            
owner.IncreaseEnergyMaximum(1, fillToMaximum: true);
            FervorLevelUpContext info = new FervorLevelUpContext(
                turn, oldLevel, level, exaltation);

            if (playerSide)
                LevelUp?.Invoke(info);
            else
                EnemyLevelUp?.Invoke(owner, info);

            Debug.Log("[Fervor][LevelUp] " +
                $"Turn={turn}, Owner={owner.Data?.CharacterName ?? owner.name}, " +
                $"Level={oldLevel}->{level}, Exaltation={exaltation}, " +
                $"Energy={owner.CurrentEnergy}/{owner.MaxEnergy}");
        }

        if (playerSide)
        {
            FervorLevel = level;
            Exaltation = exaltation;
            if (before != exaltation)
                ExaltationChanged?.Invoke(before, exaltation);
        }
        else
        {
            enemyState.Level = level;
            enemyState.Exaltation = exaltation;
            if (before != exaltation)
                EnemyExaltationChanged?.Invoke(owner, before, exaltation);
        }

        Debug.Log("[Fervor][TurnEnd] " +
            $"Turn={turn}, Owner={owner.Data?.CharacterName ?? owner.name}, " +
            $"FinalMomentum={finalMomentum}, State={finalState}, Gain={gain:+#;-#;0}, " +
            $"Exaltation={before}->{exaltation}, Level={level}");
    }
}