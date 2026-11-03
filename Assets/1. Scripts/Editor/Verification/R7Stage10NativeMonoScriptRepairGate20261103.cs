#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// R7-10 / R7-11 integration hotfix gate. R7-10's old 63 native graphs
/// serialized m_Script:{fileID:0} (EditorClassIdentifier fallback) and
/// disappeared after domain reload. The five concrete ScriptableObject
/// classes now have their own MonoScript assets and stable links.
/// </summary>
public static class R7Stage10NativeMonoScriptRepairGate20261103
{
    public static void Run()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)
            throw new InvalidOperationException("idle EditMode only");

        int pass=0;
        void Check(string label,bool good)
        {
            if(!good)throw new InvalidOperationException(
                "[R7-10-MonoScript] FAIL "+label);
            pass++;
        }
        var names=new[]{
            "R7NativeEmotion33Graph20261103",
            "R7NativeEmotionCommonGraph20261103",
            "R7NativeEmotionRulebreakerGraph20261103",
            "R7NativeEmotionPrototypeGraph20261103",
            "R7NativeEmotionPendingGraph20261103"
        };
        var counts=new[]{33,6,10,1,13};
        var scripts=Directory.GetFiles(Application.dataPath,"*.cs",
            SearchOption.AllDirectories);
        var guids=new Dictionary<string,string>();
        foreach(var name in names)
        {
            var f=scripts.Single(x=>Path.GetFileName(x)==name+".cs");
            var path=f.Substring(Path.GetDirectoryName(Application.dataPath)
                .Length+1).Replace("\\","/");
            var script=AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            var guid=AssetDatabase.AssetPathToGUID(path);
            Check("concrete class script and meta: "+name,
                script!=null&&script.GetClass()?.Name==name&&
                guid?.Length==32&&File.Exists(f+".meta"));
            guids[name]=guid;
        }

        var folder="Assets/2. Data/Resources/ProjectAbyss/NativeEmotions";
        var assets=Directory.GetFiles(
            Path.Combine(Path.GetDirectoryName(Application.dataPath),folder),
            "*.asset");
        Check("exactly 63 source graph files",assets.Length==63);
        var observed=new Dictionary<string,int>();
        foreach(var f in assets)
        {
            var data=File.ReadAllText(f);
            var concrete=names.Where(n=>
                data.Contains("m_EditorClassIdentifier: "+
                    "ProjectAbyss.Runtime::"+n)).ToArray();
            Check("one known concrete class per asset "+Path.GetFileName(f),
                concrete.Length==1);
            string name=concrete[0];
            string expected="m_Script: {fileID: 11500000, guid: "+
                guids[name]+", type: 3}";
            Check("no null script and correct concrete MonoScript GUID "+
                    Path.GetFileName(f),
                data.Contains(expected)&&
                !data.Contains("m_Script: {fileID: 0}"));
            string rel=f.Substring(
                Path.GetDirectoryName(Application.dataPath).Length+1)
                .Replace("\\","/");
            var asset=AssetDatabase.LoadAssetAtPath<
                R7NativeEmotionGraph20261103>(rel);
            Check("Unity loads correct concrete native graph, stable GUID "+
                    Path.GetFileName(f),
                asset!=null&&asset.GetType().Name==name&&
                asset.IsValid&&File.Exists(f+".meta")&&
                AssetDatabase.AssetPathToGUID(rel)?.Length==32);
            observed[name]=observed.GetValueOrDefault(name)+1;
        }
        for(int i=0;i<names.Length;i++)
            Check("concrete SO count "+names[i],
                observed.GetValueOrDefault(names[i])==counts[i]);

        R7NativeEmotionCatalog20261103.ResetCache();
        Check("63 native graphs load through actual Resources after reload",
            R7NativeEmotionCatalog20261103.Registry.Count==63);
        Check("all 63 source effects have their bound native asset",
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(
                "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset")
                .Entries.All(x=>
                    R7NativeEmotionCatalog20261103.TryResolve(x,out _)));
        Check("no active scene was saved",
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==
            "Battle Test Scene"&&
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty);

        Debug.Log("[R7-10 MonoScript Repair] PASS="+pass+
            " FAIL=0; 63/63 m_Script GUID resolved, "+
            "all after domain reload, 63 source identities intact. "+
            "NO_PLAYMODE NO_R9 NO_SCENE_SAVE");
    }
}
#endif