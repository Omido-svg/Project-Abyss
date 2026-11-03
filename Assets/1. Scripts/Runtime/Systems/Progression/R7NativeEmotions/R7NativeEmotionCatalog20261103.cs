using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// R7-10 production runtime route for all 63 approved 1029 emotion cards.
/// Source GUID is an editor-migration invariant and remains serialized on
/// native graphs. Runtime identities also compare stable ID/name/tier/emotion.
/// No reference to legacy EmotionAugmentEffectDefinition is loaded or called.
/// </summary>
public static class R7NativeEmotionCatalog20261103
{
    public const string ResourcePath="ProjectAbyss/NativeEmotions";
    private static Dictionary<string,R7NativeEmotionGraph20261103> registry;

    public static void ResetCache() => registry=null;

    private static Dictionary<string,R7NativeEmotionGraph20261103> Build()
    {
        var result=new Dictionary<string,R7NativeEmotionGraph20261103>(
            StringComparer.Ordinal);
        foreach(var asset in Resources.LoadAll<R7NativeEmotionGraph20261103>(
            ResourcePath))
        {
            if(asset==null || !asset.IsValid)
                throw new InvalidOperationException(
                    "R7-10 native emotion graph null/invalid");
            if(!result.TryAdd(asset.StableId,asset))
                throw new InvalidOperationException(
                    "R7-10 duplicate source stable ID: "+asset.StableId);
        }
        if(result.Count!=63)
            throw new InvalidOperationException(
                "R7-10 requires precisely 63 native emotion graphs, found "+
                result.Count);
        return result;
    }

    public static IReadOnlyDictionary<string,R7NativeEmotionGraph20261103>
        Registry => registry ??= Build();

    public static bool TryResolve(EmotionAugmentDefinition source,
        out R7NativeEmotionGraph20261103 graph)
    {
        graph=null;
        if(source==null||string.IsNullOrWhiteSpace(source.AugmentId))
            return false;
        if(!Registry.TryGetValue(source.AugmentId,out graph))
            return false;
        return graph.Matches(source);
    }
}
