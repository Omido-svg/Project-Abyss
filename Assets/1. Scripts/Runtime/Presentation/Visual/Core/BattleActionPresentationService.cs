using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ActionResolver가 Presentation concrete type을 직접 알지 않도록 하는 최소 계약.
/// </summary>
public interface IBattleActionPresentation
{
    IEnumerator PlayStandalone(BattleAction action);
    IEnumerator PlayClash(ClashResultContext result);
}

/// <summary>
/// R6-Ic optional presentation for ALREADY committed combat exchanges.
/// Existing production presenters still support their original PlayClash.
/// </summary>
public interface IBattleActionStepPresentation
{
    IEnumerator PlayCommittedExchange(
        ClashResultContext root, ClashExchangeResult exchange);
}

public sealed class BattleActionPresentationService :
    IBattleActionPresentation, IBattleActionStepPresentation
{
    private readonly BattleAnimationDirector director;
    private readonly BattleVisualRequestBuilder requestBuilder;

    public BattleActionPresentationService(
        BattleAnimationDirector director)
    {
        this.director = director;
        requestBuilder = new BattleVisualRequestBuilder();
    }

    public IEnumerator PlayStandalone(BattleAction action)
    {
        if (BattleSimulationRuntime.IsBatchSimulation ||
            action == null ||
            director == null)
        {
            yield break;
        }

        List<int> hitDamages =
            CreateHitDamagesFromAction(action);

        BattleVisualRequest request =
            requestBuilder.Build(
                action,
                clashSteps: null,
                hitDamages: hitDamages,
                opponentAction: null,
                targetPartHpBefore: null,
                targetPartHpAfter: null,
                damageContext: null);

        if (request == null)
            yield break;

        yield return director.Play(request);
    }

    public IEnumerator PlayClash(ClashResultContext result)
    {
        if (result == null ||
            BattleSimulationRuntime.IsBatchSimulation ||
            director == null)
        {
            yield break;
        }

        BattleVisualRequest request =
            requestBuilder.BuildClashSequence(result);

        if (request == null)
            yield break;

        yield return director.Play(request);
    }

    /// <summary>
    /// Build an isolated one-exchange visual request. Never re-run any
    /// roll, damage, status or BattleEvent. Original ExchangeIndex survives.
    /// Each request uses the existing Timeline pipeline and is awaited
    /// before the next reaction or original exchange.
    /// </summary>
    public IEnumerator PlayCommittedExchange(
        ClashResultContext root, ClashExchangeResult exchange)
    {
        if (root == null || exchange == null ||
            BattleSimulationRuntime.IsBatchSimulation || director == null)
            yield break;
        if (root.Exchanges == null || !root.Exchanges.Contains(exchange))
            throw new System.InvalidOperationException(
                "Cannot present an exchange absent from the committed domain result.");

        var slice = new ClashResultContext
        {
            FirstAction = root.FirstAction,
            SecondAction = root.SecondAction,
            IsClash = root.IsClash,
            MomentumAtStart = exchange.MomentumBefore,
            MomentumAfterResolution = exchange.MomentumAfter
        };
        slice.Exchanges.Add(exchange);
        var request = requestBuilder.BuildClashSequence(slice);
        if (request == null)
            yield break;
        yield return director.Play(request);
    }

    private static List<int> CreateHitDamagesFromAction(
        BattleAction action)
    {
        List<int> result = new();

        int resolvedDamage =
            action?.PrimaryDamageContext
                ?.GetDisplayDamage() ?? 0;

        if (resolvedDamage > 0)
        {
            result.Add(resolvedDamage);
            return result;
        }

        if (action?.HasDamageLog == true &&
            action.LoggedDamage > 0)
        {
            result.Add(action.LoggedDamage);
        }

        return result;
    }
}