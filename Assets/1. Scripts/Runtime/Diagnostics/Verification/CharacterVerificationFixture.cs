using System;
using UnityEngine;

public sealed class CharacterVerificationFixture :
    IDisposable
{
    private GameObject sandboxRoot;
    private GameObject characterClone;

    public Character Character { get; private set; }
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

        characterClone =
            UnityEngine.Object.Instantiate(
                profile.Bundle.CharacterPrefab.gameObject,
                sandboxRoot.transform,
                false);

        characterClone.name =
            profile.Bundle.CharacterPrefab.name +
            "_VerificationClone";

        characterClone.hideFlags =
            HideFlags.HideAndDontSave;

        Character =
            characterClone.GetComponent<Character>() ??
            characterClone.GetComponentInChildren<Character>(true);

        if (Character == null)
        {
            throw new InvalidOperationException(
                "검증 Prefab에서 Character Component를 찾지 못했습니다.");
        }

        DisableNonEssentialBehaviours(
            characterClone,
            Character);

        BattleContext =
            new BattleContext
            {
                Player = Character
            };

        Character.Initialize(
            BattleContext);

        if (!Character.IsInitialized)
        {
            throw new InvalidOperationException(
                "격리된 Character.Initialize가 실패했습니다.");
        }
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
        try
        {
            Character?.DisposeRuntime();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        try
        {
            BattleContext?._battleEvent?.Dispose();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        Character = null;
        BattleContext = null;

        if (sandboxRoot == null)
            return;

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

        sandboxRoot = null;
        characterClone = null;
    }
}
