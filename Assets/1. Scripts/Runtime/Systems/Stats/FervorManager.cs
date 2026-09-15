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
    }

    public void ResolveTurnEnd(int turn)
    {
        Character player = context?.Player;
        if (player == null || momentum == null)
            return;

        MomentumState finalState = momentum.GetFinalTurnState(player);
        int finalMomentum = momentum.GetPerspectiveValue(player);
        int gain = settings.GetGain(finalState);

        if (player.Mechanics != null)
        {
            foreach (CombatMechanic mechanic in player.Mechanics)
            {
                if (mechanic is IFervorTurnEndGainModifier modifier)
                    gain = Mathf.Max(0, modifier.ModifyTurnEndFervorGain(finalState, gain));
            }
        }

        int before = Exaltation;

        if (gain > 0)
        {
            Exaltation += gain;
            ExaltationChanged?.Invoke(before, Exaltation);
        }

        Debug.Log(
            $"[Fervor][TurnEnd] Turn={turn}, FinalMomentum={finalMomentum}, " +
            $"State={finalState}, Gain={gain:+#;-#;0}, Exaltation={before}->{Exaltation}, " +
            $"Level={FervorLevel}");

        if (gain <= 0)
            return;

        while (FervorLevel < settings.MaximumLevel)
        {
            int cost = settings.GetCostForNextLevel(FervorLevel);
            if (cost <= 0 || Exaltation < cost)
                break;

            int previousLevel = FervorLevel;
            Exaltation -= cost;
            FervorLevel++;

            // 확정 보상: 빛 최대치 +1 후 전량 회복.
            player.IncreaseEnergyMaximum(1, fillToMaximum: true);

            LevelUp?.Invoke(new FervorLevelUpContext(
                turn,
                previousLevel,
                FervorLevel,
                Exaltation));

            Debug.Log(
                $"[Fervor][LevelUp] Turn={turn}, Level={previousLevel}->{FervorLevel}, " +
                $"Exaltation={Exaltation}, Energy={player.CurrentEnergy}/{player.MaxEnergy}");
        }
    }
}