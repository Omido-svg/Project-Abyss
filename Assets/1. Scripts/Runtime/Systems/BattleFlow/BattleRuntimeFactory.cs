using System;
using UnityEngine;
using ProjectAbyss.Abilities;

public sealed class BattleRuntimeComposition
{
    public BattleRuntimeServices Services { get; internal set; }

    public BattleLogger BattleLogger => Services?.BattleLogger;
    public ActionManager ActionManager => Services?.ActionManager;
    public MomentumManager MomentumManager => Services?.MomentumManager;
    public FervorManager FervorManager => Services?.FervorManager;
    public PrestigeChargeService PrestigeChargeService => Services?.PrestigeChargeService;
    public BattleRewardService RewardService => Services?.RewardService;
    public EmotionAugmentManager EmotionAugmentManager => Services?.EmotionAugmentManager;
    public EmotionRulebreakerService EmotionRulebreakerService => Services?.EmotionRulebreakerService;
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

        services.RewardService?.Dispose();
        // Battle-local one-shot queue and binding data cannot leak across runs.
        services.ReactionScheduler?.ClearForBattleEnd();
        services.ReactionActionRegistry?.ClearForBattleEnd();
        services.SpecialistReactionRegistry?.ClearForBattleEnd();
        services.R7NativeResourceMigration?.Dispose();
        services.R7NativeBleedingMigration?.Dispose();
        services.R7LegacyStatusMigration?.Dispose();
        services.R7PreparationStatusMigration?.Dispose();

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
        services.PrestigeChargeService =
            new PrestigeChargeService(context);
        services.RewardService =
            new BattleRewardService(context, services.PrestigeChargeService);
        services.EmotionRulebreakerService =
            new EmotionRulebreakerService(context);
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

        // R6-Ia checkpoint infrastructure is active, but NO legacy mechanic
        // is auto-registered. Hifumi's existing OnClashResolved handler remains
        // the only executor until that content is explicitly migrated.
        services.ReactionScheduler = new BattleReactionScheduler();
        services.ReactionActionRegistry = new R6ReactionActionRegistry();
        services.SpecialistReactionRegistry = new R6SpecialistReactionRegistry();
        // R7 first production cohort: 15 uniquely identified, single-resource
        // effect cards. Each executes native R5 commands; all others legacy.
        // Per-battle Enabled toggle allows safe rollback without asset edits.
        services.R7NativeResourceMigration =
            new R7ProductionNativeResourceMigration();
        // R7 production blood wound native cohort: nine unique stable
        // single-effect skills, status resolved only by R5/BattleEffectResolver.
        // Per-battle Enabled=false instantly restores the legacy path.
        services.R7NativeBleedingMigration =
            new R7ProductionNativeBleedingMigration(context);
        // Seven verified non-Preparation legacy/mixed status cards:
        // only Bleeding status entry uses native R5 command; mixed damage
        // entries retain exactly their original dispatch order.
        // Per-battle Enabled=false reverts this cohort to legacy.
        services.R7LegacyStatusMigration =
            new R7ProductionLegacyStatusMigration(context);
        // R7 first native Preparation cohort; two status entries run via
        // typed R5 commands with exact PlanningUndoJournal merge/new undo.
        // Third setup entry remains on the original specialist.
        services.R7PreparationStatusMigration =
            new R7ProductionPreparationStatusMigration(context);
        services.ActionResolver.EnableR6ReactionExecution(
            services.ReactionScheduler,services.ReactionActionRegistry);
        services.ActionResolver.EnableR6SpecialistReactions(
            services.SpecialistReactionRegistry);

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
