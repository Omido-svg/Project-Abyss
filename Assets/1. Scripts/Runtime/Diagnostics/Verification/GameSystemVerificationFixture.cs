using System;
using UnityEngine;

/// <summary>
/// 현재 인게임 Roster의 Character를 복제해 시스템 규칙만 안전하게 검사하는 샌드박스.
/// 실제 BattleManager의 HP/에너지/속도/ActionManager는 변경하지 않는다.
/// </summary>
public sealed class GameSystemVerificationFixture : IDisposable
{
    private GameObject root;
    private GameObject playerClone;
    private GameObject enemyClone;
    private UnityEngine.Random.State randomStateBefore;
    private bool hasRandomState;

    public Character Player { get; private set; }
    public Character Enemy { get; private set; }
    public BattleContext Context { get; private set; }
    public SpeedManager SpeedManager { get; private set; }

    public static GameSystemVerificationFixture Create(
        Character playerSource,
        Character enemySource)
    {
        if (playerSource == null)
            throw new InvalidOperationException("Player source가 없습니다.");

        if (enemySource == null)
            throw new InvalidOperationException("Enemy source가 없습니다.");

        GameSystemVerificationFixture fixture =
            new GameSystemVerificationFixture();

        try
        {
            fixture.Build(
                playerSource,
                enemySource);

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
        Character enemySource)
    {
        // Sandbox 초기화/Speed Roll이 UnityEngine.Random 전역 상태를 소비해도
        // 실제 전투 RNG 결과가 바뀌지 않도록 전체 검증 구간을 격리한다.
        randomStateBefore =
            UnityEngine.Random.state;

        hasRandomState =
            true;

        root =
            new GameObject(
                "[Game System Verification Sandbox]");

        // Full System Coverage는 Play Mode 전용이고 Dispose에서 즉시 제거한다.
        // DontSaveInEditor 플래그는 Editor persistence 검사와 충돌할 수 있으므로
        // Hierarchy 숨김 + Build 제외만 사용한다.
        root.hideFlags =
            HideFlags.HideInHierarchy |
            HideFlags.DontSaveInBuild;

        root.SetActive(false);

        playerClone =
            CreateClone(
                playerSource.gameObject,
                "_SystemVerificationPlayer");

        enemyClone =
            CreateClone(
                enemySource.gameObject,
                "_SystemVerificationEnemy");

        Player =
            ResolveCharacter(
                playerClone);

        Enemy =
            ResolveCharacter(
                enemyClone);

        if (Player == null || Enemy == null)
        {
            throw new InvalidOperationException(
                "검증 Clone에서 Character Component를 찾지 못했습니다.");
        }

        DisableNonEssentialBehaviours(
            playerClone,
            Player);

        DisableNonEssentialBehaviours(
            enemyClone,
            Enemy);

        Context =
            new BattleContext
            {
                Player = Player,
                SuppressPresentation = true
            };

        Context.Enemies.Add(Enemy);

        MomentumManager verificationMomentum =
            new MomentumManager(Context);

        DamageManager verificationDamage =
            new DamageManager(
                Context,
                verificationMomentum);

        Context.Services =
            new BattleRuntimeServices
            {
                MomentumManager = verificationMomentum,
                DamageManager = verificationDamage
            };

        Context.EffectResolver =
            new BattleEffectResolver(Context);

        Player.Initialize(Context);
        Enemy.Initialize(Context);

        if (!Player.IsInitialized ||
            !Enemy.IsInitialized)
        {
            throw new InvalidOperationException(
                "System Verification Character.Initialize 실패");
        }

        SpeedManager =
            new SpeedManager(Context);

        Context.Services.SpeedManager =
            SpeedManager;

        SpeedManager.RollAllSpeed();
    }

    private GameObject CreateClone(
        GameObject source,
        string suffix)
    {
        GameObject clone =
            UnityEngine.Object.Instantiate(
                source,
                root.transform,
                false);

        clone.name =
            source.name + suffix;

        clone.hideFlags =
            HideFlags.HideInHierarchy |
            HideFlags.DontSaveInBuild;

        return clone;
    }

    private static Character ResolveCharacter(
        GameObject source)
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

    public void Dispose()
    {
        try
        {
            Player?.DisposeRuntime();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        try
        {
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

        Player = null;
        Enemy = null;
        SpeedManager = null;

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

        if (hasRandomState)
        {
            UnityEngine.Random.state =
                randomStateBefore;

            hasRandomState =
                false;
        }
    }
}
