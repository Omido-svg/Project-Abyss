#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
/// <summary>
/// Reproducible, no-scene-save test for the MVP UI presentation / batching patch.
/// Numbers validate configuration and dirty-write suppression, not GPU-instancing
/// claims or automatic reductions in draw calls.
/// </summary>
public static class BattleMvpUiGate20261103
{
    private static void Execute(bool play)
    {
        if(EditorApplication.isPlaying!=play)
            throw new InvalidOperationException(play?
                "MVP UI Live gate requires PlayMode.":
                "MVP UI Static gate requires idle EditMode.");
        int pass=0,fail=0;
        var cases=new List<string>();
        void Check(string id,bool ok,string evidence)
        {
            cases.Add((ok?"PASS":"FAIL")+" | "+id+" | "+evidence);
            if(ok)pass++;
            else {fail++;Debug.LogError("[MVP-UI] FAIL "+id+" "+evidence);}
        }
        var mode=FindFirstObject<BattleScreenModeController>();
        var ui=FindFirstObject<BattleUIManager>();
        var bar=FindFirstObject<BattleTopStatusBarUI>();
        var gauge=FindFirstObject<GameplayV5ProgressionHudUI>();
        var arrows=FindFirstObject<BattleWorldSlotArrowOverlayUI>();
        var rail=FindFirstObject<BattleActionOrderRailUI>();
        var status=FindFirstObject<BattleStatusListUI>();
        var characterDetail=FindFirstObject<BattleCharacterDetailPanelUI>();

        Check("U01 Existing MVP Presentation controller",mode!=null,mode?.name??"missing");
        Check("U02 Existing MVP selection Model->Presenter->View",ui?.Selection!=null,
            ui?.Selection?.Mode.ToString()??"missing");
        Check("U03 Existing status, resource and progression HUDs",
            bar!=null&&gauge!=null,"top="+(bar!=null)+" progression="+(gauge!=null));
        Check("U04 Existing real weighted target and action rail UI",
            arrows!=null&&rail!=null,
            "weightArrows="+(arrows!=null)+" actionRail="+(rail!=null));
        Check("U05 Existing target/status detail UI",
            status!=null || characterDetail!=null,
            "status="+(status!=null)+" characterDetail="+(characterDetail!=null));
        var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,FindObjectsSortMode.None);
        var tmps=UnityEngine.Object.FindObjectsByType<Component>(
            FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(c=>c!=null && c.GetType().Name.Contains("TextMeshPro")).ToArray();
        Check("U06 Existing TMP/Canvas sources",
            canvases.Length>=4 && tmps.Length>=90,
            "canvases="+canvases.Length+" TMP="+tmps.Length);

        var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Check("U07 Battle Test Scene preserved",
            active.path=="Assets/5. Scenes/Battle Test Scene.unity",
            "scene="+active.path+" dirty="+active.isDirty);

        var sceneMode=mode?.transform;
        if(play)
        {
            var optimizer=FindFirstObject<BattleMvpCanvasOptimizer20261103>();
            var insight=FindFirstObject<BattleMvpAbilityInspector20261103>();
            var dynamicLayer=sceneMode?.Find(
                "PersistentTopLayer/TopStatusBar/MvpDynamicValueCanvas");
            var panel=sceneMode?.Find("MvpAbilityInsight");
            var toggle=sceneMode?.Find("MvpAbilityToggle");
            Check("U08 Runtime feature group Canvas partition",
                optimizer!=null && optimizer.CreatedCanvasCount>=8,
                "partitions="+optimizer?.CreatedCanvasCount);
            Check("U09 Five changing top-bar TMPs isolated",
                dynamicLayer!=null && dynamicLayer.childCount==5 &&
                optimizer?.RelocatedTextCount==5,
                "moved="+optimizer?.RelocatedTextCount);
            Check("U10 Ability 5-factor view and F3 toggle",
                insight!=null && panel!=null && toggle!=null &&
                panel.GetComponentsInChildren<Component>(true)
                    .Count(c=>c!=null && c.GetType().Name.Contains("TextMeshPro"))>=5,
                "panel="+(panel!=null)+" toggle="+(toggle!=null));
            Check("U11 Dirty TMP equality guard active",
                bar!=null && bar.SkippedTextWrites>0,
                "topbar skipped="+(bar?.SkippedTextWrites??0)+
                " changed="+(bar?.ChangedTextWrites??0));
            Check("U12 Runtime UI Presenter dirty updates / collapsed idle",
                insight!=null && (insight.SkippedTextWrites>0 ||
                    (panel!=null && !panel.gameObject.activeSelf)),
                "presenter skipped="+(insight?.SkippedTextWrites??0)+
                " updated="+(insight?.RenderedUpdates??0)+
                " collapsed="+(panel!=null&&!panel.gameObject.activeSelf));
            Check("U13 Dynamic content Canvas has no extra raycaster",
                dynamicLayer!=null && dynamicLayer.GetComponent<Canvas>()!=null &&
                dynamicLayer.GetComponent("GraphicRaycaster")==null,
                "non-interactive glyphs do not need raycast");
            Check("U14 Interactive child layers retain raycasting",
                sceneMode?.Find("DefaultBattleLayer")?.GetComponent("GraphicRaycaster")!=null &&
                sceneMode?.Find("SkillSelectionLayer")?.GetComponent("GraphicRaycaster")!=null &&
                sceneMode?.Find("CharacterDetailLayer")?.GetComponent("GraphicRaycaster")!=null,
                "nested interactive canvases have dedicated raycasters");
        }
        else
        {
            string root=Directory.GetParent(Application.dataPath).FullName;
            string uiFolder=Path.Combine(root,
                "Assets/1. Scripts/Runtime/Presentation/UI/Core");
            var expected=new[]{
                "BattleMvpCanvasOptimizer20261103.cs",
                "BattleMvpAbilityInspector20261103.cs"};
            Check("U08 UI scripts and metas remain asset-safe",
                expected.All(n=>File.Exists(Path.Combine(uiFolder,n)) &&
                    File.Exists(Path.Combine(uiFolder,n+".meta"))),
                "new scripts="+expected.Length);
            var freeze=Path.Combine(root,
                "ProjectAbyss_GFinal_20261103/GFINAL_CORE_FREEZE_CHECK.md");
            Check("U09 G-FINAL protected combat core remains frozen",
                File.Exists(freeze) && File.ReadAllText(freeze).Contains(
                    "CORE_STRUCTURE_BASELINE_INTACT"),
                "read-only evidence; re-run actual freeze gate after UI patch");
        }
        string dir=Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "ProjectAbyss_UI_MVP_Audit_20261103");
        Directory.CreateDirectory(dir);
        var b=new StringBuilder().AppendLine("# UI MVP "+(play?"Play":"Edit")+" Gate")
            .AppendLine("PASS="+pass+" FAIL="+fail)
            .AppendLine("This audit does not prove reduced GPU draw calls.")
            .AppendLine("No scene save, no SO or gameplay modifications.");
        foreach(var line in cases)b.AppendLine(line);
        if(play)
            b.AppendLine("Unity Editor stats (single frame only): DrawCalls="+
                UnityStats.drawCalls+" Batches="+UnityStats.batches+
                " SetPass="+UnityStats.setPassCalls+
                " InstancedBatches="+UnityStats.instancedBatches);
        File.WriteAllText(Path.Combine(dir,
            "MVP_UI_"+(play?"PLAY":"EDIT")+"_GATE.md"),b.ToString(),
            new UTF8Encoding(false));
        Debug.Log("[MVP-UI] RESULT PASS="+pass+" FAIL="+fail+
            " ("+(play?"PLAY":"EDIT")+")");
        if(fail>0)throw new InvalidOperationException("MVP-UI gate failed: "+fail);
    }

    private static T FindFirstObject<T>() where T:UnityEngine.Object =>
        UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

    public static void RunEdit()=>Execute(false);

    public static void RunPlay()=>Execute(true);
}
#endif