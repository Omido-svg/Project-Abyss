using System;
using UnityEngine;

/// <summary>
/// Production BattleRuntimeFactory를 그대로 사용해 Player/Enemy clone을 초기화하는
/// Game System Verification용 격리 Host.
/// 검증 전용으로 Manager graph를 재조립하지 않으므로 실제 게임의 Composition Root 변경을 자동 추적한다.
/// </summary>
public sealed class GameSystemVerificationFixture : IDisposable
{
    private GameObject root;
    private GameObject playerClone;
    private GameObject enemyClone;
    private GameSystemVerificationCoroutineHost coroutineOwner;
    private BattleLifecycleGuard lifecycleGuard;
    private UnityEngine.Random.State randomStateBefore;
    private bool hasRandomState;
    private bool disposed;

    public Character Player { get; private set; }
    public Character Enemy { get; private set; }
    public BattleContext Context { get; private set; }
    public BattleRuntimeComposition Runtime { get; private set; }
    public Exception FatalError { get; private set; }

    public ActionManager ActionManager => Runtime?.ActionManager;
    public SpeedManager SpeedManager => Runtime?.SpeedManager;
    public DamageManager DamageManager => Runtime?.DamageManager;
    public MomentumManager MomentumManager => Runtime?.MomentumManager;
    public FervorManager FervorManager => Runtime?.FervorManager;
    public ClashManager ClashManager => Runtime?.ClashManager;
    public ClashBuilder ClashBuilder => Runtime?.ClashBuilder;
    public TurnManager TurnManager => Runtime?.TurnManager;

    public static GameSystemVerificationFixture Create(
        Character playerSource,
        Character enemySource,
        BattleRuleSettings sourceRules = null)
    {
        if (playerSource == null)
            throw new InvalidOperationException("Player source가 없습니다.");
        if (enemySource == null)
            throw new InvalidOperationException("Enemy source가 없습니다.");

        GameSystemVerificationFixture fixture = new();
        try
        {
            fixture.Build(playerSource, enemySource, sourceRules);
            return fixture;
        }
        catch
        {
            fixture.Dispose();
            throw;
        }
    }

    private void Build(
        Character playerSource,
        Character enemySource,
        BattleRuleSettings sourceRules)
    {
        randomStateBefore = UnityEngine.Random.state;
        hasRandomState = true;

        root = new GameObject("[Game System Verification Sandbox]");
        root.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInBuild;
        root.SetActive(false);

        coroutineOwner = root.AddComponent<GameSystemVerificationCoroutineHost>();

        playerClone = CreateClone(playerSource.gameObject, "_SystemVerificationPlayer");
        enemyClone = CreateClone(enemySource.gameObject, "_SystemVerificationEnemy");

        Player = ResolveCharacter(playerClone);
        Enemy = ResolveCharacter(enemyClone);

        if (Player == null || Enemy == null)
        {
            throw new InvalidOperationException(
                "검증 Clone에서 Character Component를 찾지 못했습니다.");
        }

        DisableNonEssentialBehaviours(playerClone, Player);
        DisableNonEssentialBehaviours(enemyClone, Enemy);

        Context = new BattleContext
        {
            Player = Player,
            Rules = CloneRules(sourceRules),
            SuppressPresentation = true
        };
        Context.Enemies.Add(Enemy);
        Context.EffectResolver = new BattleEffectResolver(Context);

        lifecycleGuard = new BattleLifecycleGuard();

        root.SetActive(true);

        Runtime = BattleRuntimeFactory.Create(
            Context,
            coroutineOwner,
            null,
            lifecycleGuard,
            exception => FatalError = exception);

        Player.Initialize(Context);
        Enemy.Initialize(Context);

        if (!Player.IsInitialized || !Enemy.IsInitialized)
        {
            throw new InvalidOperationException(
                "System Verification Character.Initialize 실패");
        }

        if (!lifecycleGuard.MarkReady())
        {
            throw new InvalidOperationException(
                "System Verification BattleLifecycleGuard Ready 전환 실패");
        }

        Runtime.SpeedManager?.RollAllSpeed();
    }

    public CharacterRuntimeSnapshot CapturePlayer() =>
        CharacterRuntimeSnapshot.Capture(Player);

    public CharacterRuntimeSnapshot CaptureEnemy() =>
        CharacterRuntimeSnapshot.Capture(Enemy);

    public bool HasProductionServiceGraph()
    {
        BattleRuntimeServices services = Context?.Services;
        return services != null &&
               services.ActionManager != null &&
               services.MomentumManager != null &&
               services.FervorManager != null &&
               services.EmotionRulebreakerService != null &&
               services.SpeedManager != null &&
               services.DamageManager != null &&
               services.ClashManager != null &&
               services.ActionResolver != null &&
               services.ClashBuilder != null &&
               services.AIManager != null &&
               services.TurnManager != null &&
               services.CoroutineHost != null;
    }

    private GameObject CreateClone(GameObject source, string suffix)
    {
        GameObject clone = UnityEngine.Object.Instantiate(
            source,
            root.transform,
            false);
        clone.name = source.name + suffix;
        clone.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInBuild;
        return clone;
    }

    private static Character ResolveCharacter(GameObject source)
    {
        return source == null
            ? null
            : source.GetComponent<Character>() ??
              source.GetComponentInChildren<Character>(true);
    }

    private static void DisableNonEssentialBehaviours(
        GameObject source,
        Character character)
    {
        if (source == null)
            return;

        MonoBehaviour[] behaviours =
            source.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null ||
                behaviour == character ||
                behaviour is CharacterRandomDebugOverride)
            {
                continue;
            }

            behaviour.enabled = false;
        }
    }

    private static BattleRuleSettings CloneRules(BattleRuleSettings source)
    {
        if (source == null)
        {
            BattleRuleSettings defaults = new();
            defaults.Normalize();
            return defaults;
        }

        try
        {
            BattleRuleSettings clone =
                JsonUtility.FromJson<BattleRuleSettings>(
                    JsonUtility.ToJson(source));
            clone ??= new BattleRuleSettings();
            clone.Normalize();
            return clone;
        }
        catch
        {
            BattleRuleSettings fallback = new();
            fallback.Normalize();
            return fallback;
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        try
        {
            Runtime?.TurnManager?.EndBattle();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        try
        {
            Player?.DisposeRuntime();
            Enemy?.DisposeRuntime();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        try
        {
            Context?._battleEvent?.Dispose();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        lifecycleGuard?.Dispose();

        Player = null;
        Enemy = null;
        Runtime = null;

        if (Context != null)
        {
            Context.Player = null;
            Context.Enemies?.Clear();
            Context.Services = null;
            Context.EffectResolver = null;
        }
        Context = null;

        if (root != null)
        {
            root.SetActive(false);
#if UNITY_EDITOR
            UnityEngine.Object.DestroyImmediate(root);
#else
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(root);
            else
                UnityEngine.Object.DestroyImmediate(root);
#endif
        }

        root = null;
        playerClone = null;
        enemyClone = null;
        coroutineOwner = null;

        if (hasRandomState)
        {
            UnityEngine.Random.state = randomStateBefore;
            hasRandomState = false;
        }
    }
}
