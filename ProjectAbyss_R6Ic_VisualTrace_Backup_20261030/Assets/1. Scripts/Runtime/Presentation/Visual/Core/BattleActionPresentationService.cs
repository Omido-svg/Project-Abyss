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

public sealed class BattleActionPresentationService :
    IBattleActionPresentation
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