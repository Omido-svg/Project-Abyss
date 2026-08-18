using System;
using UnityEngine;

/// <summary>
/// 캐릭터 한 명과 동일 규격의 표적 한 명을 실제 BattleContext에 초기화하는
/// 결정론적 검증 Fixture다. 실제 Scene BattleManager 없이도 동일 DamageManager와
/// BattleEffectResolver를 사용한다.
/// </summary>
public sealed class CharacterVerificationFixture :
    IDisposable
{
    private GameObject sandboxRoot;
    private GameObject characterClone;
    private GameObject targetClone;

    public Character Character { get; private set; }
    public Character TargetCharacter { get; private set; }
    public BattleContext BattleContext { get; private set; }

    public static CharacterVerificationFixture Create(
        CharacterVerificationProfile profile)
    {
        if (profile?.Bundle?.CharacterPrefab == null)
        {
            throw new InvalidOperationException(
                "검증할 Character Prefab이 없습니다.");
        }

        CharacterVerificationFixture fixture =
            new CharacterVerificationFixture();

        fixture.Build(profile);
        return fixture;
    }

    private void Build(
        CharacterVerificationProfile profile)
    {
        sandboxRoot =
            new GameObject(
                $"[Character Verification] {profile.name}");

        // IsolatedRuntime Fixture는 Play Mode에서만 생성되고 Dispose에서 즉시 파괴된다.
        // HideAndDontSave에는 DontSaveInEditor가 포함되어 있어, Editor가 Play Mode 중
        // 임시 clone 참조를 검사/직렬화할 때 Unity native persistence assertion을
        // 유발할 수 있다. Hierarchy에는 숨기되 DontSaveInEditor는 사용하지 않는다.
        sandboxRoot.hideFlags =
            HideFlags.HideInHierarchy |
            HideFlags.DontSaveInBuild;

        sandboxRoot.SetActive(false);

        characterClone = CreateClone(
            profile.Bundle.CharacterPrefab.gameObject,
            "_VerificationOwner");

        targetClone = CreateClone(
            profile.Bundle.CharacterPrefab.gameObject,
            "_VerificationTarget");

        Character = ResolveCharacter(characterClone);
        TargetCharacter = ResolveCharacter(targetClone);

        if (Character == null || TargetCharacter == null)
        {
            throw new InvalidOperationException(
                "검증 Prefab에서 Owner/Target Character Component를 찾지 못했습니다.");
        }

        DisableNonEssentialBehaviours(
            characterClone,
            Character);

        DisableNonEssentialBehaviours(
            targetClone,
            TargetCharacter);

        BattleContext =
            new BattleContext
            {
                Player = Character,
                SuppressPresentation = true
            };

        BattleContext.Enemies.Add(
            TargetCharacter);

        MomentumManager verificationMomentum =
            new MomentumManager(
                BattleContext);

        DamageManager verificationDamage =
            new DamageManager(
                BattleContext,
                verificationMomentum);

        BattleContext.Services =
            new BattleRuntimeServices
            {
                MomentumManager = verificationMomentum,
                DamageManager = verificationDamage
            };

        BattleContext.EffectResolver =
            new BattleEffectResolver(
                BattleContext);

        Character.Initialize(
            BattleContext);

        TargetCharacter.Initialize(
            BattleContext);

        if (!Character.IsInitialized ||
            !TargetCharacter.IsInitialized)
        {
            throw new InvalidOperationException(
                "격리된 Owner/Target Character.Initialize가 실패했습니다.");
        }
    }

    private GameObject CreateClone(
        GameObject source,
        string suffix)
    {
        GameObject clone =
            UnityEngine.Object.Instantiate(
                source,
                sandboxRoot.transform,
                false);

        clone.name = source.name + suffix;
        clone.hideFlags =
            HideFlags.HideInHierarchy |
            HideFlags.DontSaveInBuild;
        return clone;
    }

    private static Character ResolveCharacter(
        GameObject root)
    {
        return root == null
            ? null
            : root.GetComponent<Character>() ??
              root.GetComponentInChildren<Character>(true);
    }

    private static void DisableNonEssentialBehaviours(
        GameObject root,
        Character character)
    {
        if (root == null)
            return;

        MonoBehaviour[] behaviours =
            root.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0;
             i < behaviours.Length;
             i++)
        {
            MonoBehaviour behaviour =
                behaviours[i];

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
        DisposeCharacter(Character);
        DisposeCharacter(TargetCharacter);

        try
        {
            BattleContext?._battleEvent?.Dispose();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        Character = null;
        TargetCharacter = null;

        if (BattleContext != null)
        {
            BattleContext.Player = null;
            BattleContext.Enemies?.Clear();
            BattleContext.Services = null;
            BattleContext.EffectResolver = null;
        }

        BattleContext = null;

        if (sandboxRoot != null)
        {
            sandboxRoot.SetActive(false);

#if UNITY_EDITOR
            // Full Coverage는 한 Editor frame 안에서도 많은 Fixture를 만들 수 있다.
            // Destroy()를 사용하면 frame 종료까지 clone이 누적되므로 Editor에서는
            // 즉시 제거해 메모리 급증과 파괴 대기 참조를 막는다.
            UnityEngine.Object.DestroyImmediate(
                sandboxRoot);
#else
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(
                    sandboxRoot);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(
                    sandboxRoot);
            }
#endif
        }

        sandboxRoot = null;
        characterClone = null;
        targetClone = null;
    }

    private static void DisposeCharacter(
        Character character)
    {
        try
        {
            character?.DisposeRuntime();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}