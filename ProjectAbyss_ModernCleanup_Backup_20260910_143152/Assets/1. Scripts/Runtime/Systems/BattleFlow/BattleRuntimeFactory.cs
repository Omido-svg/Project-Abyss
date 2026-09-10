using System;
using UnityEngine;

public sealed class BattleRuntimeComposition
{
    public BattleRuntimeServices Services { get; internal set; }

    public BattleLogger BattleLogger => Services?.BattleLogger;
    public ActionManager ActionManager => Services?.ActionManager;
    public MomentumManager MomentumManager => Services?.MomentumManager;
    public FervorManager FervorManager => Services?.FervorManager;
    public EmotionAugmentManager EmotionAugmentManager => Services?.EmotionAugmentManager;
    public SpeedManager SpeedManager => Services?.SpeedManager;
    public DamageManager DamageManager => Services?.DamageManager;
    public ClashManager ClashManager => Services?.ClashManager;
    public ActionResolver ActionResolver => Services?.ActionResolver;
    public ClashBuilder ClashBuilder => Services?.ClashBuilder;
    public AIManager AIManager => Services?.AIManager;
    public TurnManager TurnManager => Services?.TurnManager;
}

/// <summary>
/// BattleManager의 lifecycle orchestration과 concrete service graph 생성을 분리한
/// Project Abyss 전투 Composition Root.
/// </summary>
public static class BattleRuntimeFactory
{
    public static BattleRuntimeComposition Create(
        BattleContext context,
        MonoBehaviour coroutineOwner,
        BattleAnimationDirector animationDirector,
        BattleLifecycleGuard lifecycleGuard,
        Action<Exception> fatalErrorHandler)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        BattleRuntimeServices services =
            context.Services ??
            new BattleRuntimeServices();

        context.Services = services;

        services.CoroutineHost =
            new UnityBattleCoroutineHost(coroutineOwner);
        services.BattleLogger =
            new BattleLogger();
        services.ActionManager =
            new ActionManager();
        services.MomentumManager =
            new MomentumManager(context);
        services.FervorManager =
            new FervorManager(context, services.MomentumManager);
        services.EmotionAugmentManager =
            new EmotionAugmentManager(context, services.FervorManager);
        services.SpeedManager =
            new SpeedManager(context);
        services.DamageManager =
            new DamageManager(
                context,
                services.MomentumManager);
        services.ClashManager =
            new ClashManager(
                context,
                services.DamageManager,
                services.MomentumManager);

        IBattleActionReporter reporter =
            new BattleActionLogReporter(
                services.BattleLogger);

        IBattleActionPresentation presentation =
            new BattleActionPresentationService(
                animationDirector);

        services.ActionResolver =
            new ActionResolver(
                context,
                services.ClashManager,
                reporter,
                presentation);

        services.ClashBuilder =
            new ClashBuilder();

        services.AIManager =
            new AIManager(
                context,
                services.ActionManager);

        services.TurnManager =
            new TurnManager(
                context,
                services.ActionManager,
                services.AIManager,
                services.SpeedManager,
                services.ActionResolver,
                services.MomentumManager,
                services.ClashBuilder,
                lifecycleGuard,
                services.CoroutineHost,
                services.BattleLogger,
                fatalErrorHandler);

        return new BattleRuntimeComposition
        {
            Services = services
        };
    }
}