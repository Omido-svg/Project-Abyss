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

        sandboxRoot.hideFlags =
            HideFlags.HideAndDontSave;

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

        BattleContext.VerificationMomentumManager =
            new MomentumManager(
                BattleContext);

        BattleContext.VerificationDamageManager =
            new DamageManager(
                BattleContext,
                BattleContext.VerificationMomentumManager);

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
        clone.hideFlags = HideFlags.HideAndDontSave;
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
            BattleContext.VerificationDamageManager = null;
            BattleContext.VerificationMomentumManager = null;
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
