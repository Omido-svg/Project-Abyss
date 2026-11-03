using System;
using UnityEngine;

/// <summary>
/// Run Flow Test와 실제 Battle Test Scene 사이에서 공유할 실제 콘텐츠 참조.
/// Editor installer가 Phase E 카탈로그와 Battle Test Scene의 플레이어 prefab을 연결한다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Progression/Run Flow Live Content Registry",
    fileName = "RunFlowLiveContentRegistry")]
public sealed class RunFlowLiveContentRegistry : ScriptableObject
{
    public const string ResourcesLoadPath =
        "ProjectAbyss/RunFlowLiveContentRegistry";

    [Header("Phase E Content")]
    public RunItemCatalog RunItemCatalog;
    public EmotionAugmentCatalog EmotionAugmentCatalog;

    [Header("Playable Character Prefabs")]
    public Character OlafPrefab;
    public Character YujinPrefab;
    public Character HifumiPrefab;

    public static RunFlowLiveContentRegistry Load()
    {
        return Resources.Load<RunFlowLiveContentRegistry>(
            ResourcesLoadPath);
    }

    public Character ResolvePlayerPrefab(string characterKey)
    {
        if (string.IsNullOrWhiteSpace(characterKey))
            return null;

        string normalized = characterKey.Trim();

        if (string.Equals(normalized, "Olaf", StringComparison.OrdinalIgnoreCase))
            return OlafPrefab;

        if (string.Equals(normalized, "Yujin", StringComparison.OrdinalIgnoreCase))
            return YujinPrefab;

        if (string.Equals(normalized, "Hifumi", StringComparison.OrdinalIgnoreCase))
            return HifumiPrefab;

        return null;
    }

    public int CountValidRunItems()
    {
        if (RunItemCatalog?.Items == null)
            return 0;

        int count = 0;
        for (int i = 0; i < RunItemCatalog.Items.Count; i++)
        {
            if (RunItemCatalog.Items[i] != null)
                count++;
        }

        return count;
    }

    // R7-11 distinguishes a serialized TEMP combat link from an
    // actual canonically approved native combat implementation.
    public int CountNativeReadyRunItems()
    {
        if(RunItemCatalog?.Items==null)return 0;
        int count=0;
        foreach(var item in RunItemCatalog.Items)
            if(item!=null&&
               R7NativeRunItemCatalog20261103.TryResolve(item,out var graph)&&
               graph.State==R7NativeRunItemState20261103.NativeReady)
                count++;
        return count;
    }

    public int CountNativePendingRunItems()
    {
        if(RunItemCatalog?.Items==null)return 0;
        int count=0;
        foreach(var item in RunItemCatalog.Items)
            if(item!=null&&
               R7NativeRunItemCatalog20261103.TryResolve(item,out var graph)&&
               graph.State==
                   R7NativeRunItemState20261103.CanonicalEffectPending)
                count++;
        return count;
    }

    public int CountCombatLinkedRunItems()
    {
        if (RunItemCatalog?.Items == null)
            return 0;

        int count = 0;
        for (int i = 0; i < RunItemCatalog.Items.Count; i++)
        {
            RunItemDefinition item = RunItemCatalog.Items[i];
            if (item != null && item.CombatItem != null)
                count++;
        }

        return count;
    }
}
