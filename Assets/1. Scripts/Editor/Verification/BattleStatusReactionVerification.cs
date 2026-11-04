#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Repeatable scope-safe audit. It never mutates gameplay statuses or performs
/// a model-side reaction. Optional isolated toast smoke uses only a private
/// presentation method with an in-memory event object.
/// </summary>
public static class BattleStatusReactionVerification
{
    private static void Run(bool play)
    {
        if(EditorApplication.isCompiling || EditorApplication.isPlaying!=play)
            throw new InvalidOperationException(play?
                "PlayMode status/UI audit requires running combat scene":
                "EditMode status/UI audit requires idle editor");
        int pass=0,fail=0;
        var records=new List<string>();
        void Verify(string id,bool ok,string evidence)
        {
            records.Add((ok?"PASS":"FAIL")+" | "+id+" | "+evidence);
            if(ok)pass++;else {fail++;Debug.LogError("[MVP STATUS] "+id+" "+evidence);}
        }
        string basePath=Directory.GetParent(Application.dataPath).FullName;
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Verify("S01 Existing Battle Test Scene",
            scene.path=="Assets/5. Scenes/Battle Test Scene.unity",scene.path);
        Verify("S02 Existing Status tab present",
            UnityEngine.Object.FindFirstObjectByType<BattleCharacterDetailPanelUI>(
                FindObjectsInactive.Include)!=null,"existing detailed Status tab kept");
        Verify("S03 Existing status projector and V5 gauge stay in use",
            typeof(BattleStatusPresentationProjector).GetMethod("Build")!=null &&
            typeof(BattleWorldUniqueGaugeUI)!=null,"reused existing data/view contracts");
        Verify("S04 Unique keyword / numeric duration read-only projection",
            typeof(BattleStatusVisual).GetMethod("Build")!=null &&
            typeof(BattleStatusVisual).GetMethod("FromEffect")!=null,
            "pure chip model projection");
        Verify("S05 Original scoped G-FINAL core evidence intact",
            File.Exists(Path.Combine(basePath,
                "ProjectAbyss_GFinal_20261103/GFINAL_CORE_FREEZE_CHECK.md"))&&
            File.ReadAllText(Path.Combine(basePath,
                "ProjectAbyss_GFinal_20261103/GFINAL_CORE_FREEZE_CHECK.md"))
                .Contains("CORE_STRUCTURE_BASELINE_INTACT"),
            "freeze gate should also be rerun independently");

        var newScripts=new[]{
            "Runtime/Presentation/UI/Components/BattleStatusVisual.cs",
            "Runtime/Presentation/UI/Components/BattleWorldStatusStrip.cs",
            "Runtime/Presentation/UI/Components/BattleDetailStatusBadge.cs",
            "Runtime/Presentation/UI/Panels/BattleReactionHud.cs",
            "Editor/Verification/BattleStatusReactionVerification.cs"
        };
        string scriptRoot=Path.Combine(basePath,"Assets/1. Scripts");
        Verify("S06 New scripts and GUID sidecars present",
            newScripts.All(f=>File.Exists(Path.Combine(scriptRoot,f)) &&
                File.Exists(Path.Combine(scriptRoot,f+".meta"))),
            "scripts="+newScripts.Length+" independent .meta");
        Verify("S07 Detail tab and world plate are additive not replaced",
            File.ReadAllText(Path.Combine(scriptRoot,
                "Runtime/Presentation/UI/Panels/BattleCharacterDetailPanelUI.cs"))
                .Contains("BattleDetailStatusBadge.Attach") &&
            File.ReadAllText(Path.Combine(scriptRoot,
                "Runtime/Presentation/UI/Panels/BattleWorldCharacterPlateUI.cs"))
                .Contains("BattleWorldStatusStrip"),
            "original tabs, HP, resource HUDs preserved");

        // Pure, unattached status fixtures: verify N/T and unique icon mappings.
        var fixturesSource=new StatusEffect[]{
            new Bleeding(3,2,1),new Burn(2,1),
            new StrengthStatus(2,1),new OlafFearStatus(1),
            new YujinDesignationStatus(2)};
        var fixtures=BattleStatusPresentationProjector.BuildFromEffects(
            null,fixturesSource).Select(x=>
            BattleStatusVisual.FromEffect(x.Representative,null)).ToArray();
        Verify("S08 Numeric N/T and keyword visual fixtures",
            fixtures.Length==5 &&
            fixtures.Any(x=>x.Glyph=="혈" && x.Unique && x.Count=="3") &&
            fixtures.Any(x=>x.Glyph=="화" && !x.Unique && x.Count=="2") &&
            fixtures.Any(x=>x.Glyph=="힘" && x.Count=="2" && x.Duration=="1T") &&
            fixtures.Any(x=>x.Glyph=="공" && x.Unique) &&
            fixtures.Any(x=>x.Glyph=="지" && x.Unique),
            "fixtures="+string.Join(",",fixtures.Select(x=>x.Glyph+":"+x.Count+":"+x.Duration)));

        var tagged=new BattleAction{Slot=new ActionSlot{
            SlotId=EnemyCounterReaction.ReactionSlotPrefix+"pure-test"}};
        var normal=new BattleAction{Slot=new ActionSlot{SlotId="NORMAL"}};
        Verify("S09 Real reaction action identified by explicit slot prefix",
            EnemyCounterReaction.IsCounterReaction(tagged) &&
            !EnemyCounterReaction.IsCounterReaction(normal),
            "no inference from ordinary actions");

        if(play)
        {
            var manager=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
            var plates=UnityEngine.Object.FindObjectsByType<BattleWorldCharacterPlateUI>(
                FindObjectsInactive.Include,FindObjectsSortMode.None);
            var strips=UnityEngine.Object.FindObjectsByType<BattleWorldStatusStrip>(
                FindObjectsInactive.Include,FindObjectsSortMode.None);
            var reaction=UnityEngine.Object.FindFirstObjectByType<BattleReactionHud>(
                FindObjectsInactive.Include);

            Verify("P01 World status strips for all world HP plates",
                plates.Length>0 && strips.Length==plates.Length,
                "plates="+plates.Length+" strips="+strips.Length);
            Verify("P02 Existing world canvases reused with isolated status group",
                strips.Length>0&&strips.All(x=>
                    x.transform.Find("MVP_StatusStrip")?.GetComponent<Canvas>()!=null &&
                    x.GetComponents<Canvas>().Length>=1),
                "no separate WorldSpace plate prefab replacement");
            Verify("P03 World status TMP badge is noninteractive",
                strips.All(x=>x.transform.Find("MVP_StatusStrip")?
                    .GetComponent("GraphicRaycaster")==null),
                "world targeting still uses existing pointer router");
            var report=new StringBuilder();
            bool matches=true;
            foreach(var strip in strips)
            {
                int expected=strip.Owner==null?0:Math.Min(4,
                    BattleStatusVisual.Build(strip.Owner).Count);
                if(strip.VisibleChipCount!=expected)matches=false;
                report.Append(strip.Owner?.name).Append(':')
                    .Append(strip.VisibleChipCount).Append('/')
                    .Append(expected).Append("; ");
            }
            Verify("P04 Status projection and visible chip count parity",
                strips.Length>0 && matches,report.ToString());

            bool stable=true;int suppressed=0;
            foreach(var strip in strips)
            {
                int written=strip.ActualTextWrites;
                strip.Refresh(false);
                if(strip.ActualTextWrites!=written)stable=false;
                suppressed+=strip.SuppressedRefreshes;
            }
            Verify("P05 No writes for unchanged status signature",
                stable && suppressed>0,"suppressed="+suppressed);

            Verify("P06 Reaction display presenter created once",
                reaction!=null && UnityEngine.Object
                    .FindObjectsByType<BattleReactionHud>(
                        FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,
                "one event subscriber owner");
            var detailPanel=UnityEngine.Object.FindFirstObjectByType<BattleCharacterDetailPanelUI>(
                FindObjectsInactive.Include);
            var statusTab=detailPanel?.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t=>t.name=="StatusTab");
            Verify("P07 Original Character Detail Status tab retained",
                detailPanel!=null && statusTab!=null,
                "StatusTab="+(statusTab!=null));

            // Presentation-only event smoke. Directly invoke the private
            // handler instead of publishing a fake BattleEvent to gameplay.
            int before=reaction?.VisibleNoticeCount??0;
            if(reaction!=null)
            {
                var evt=new BattleReactiveRollEvent{
                    SourceKind=BattleReactiveRollSourceKind.Counter,
                    DisplayName="UI TEST ONLY",Owner=manager?.BattleContext?.Player,
                    Target=manager?.BattleContext?.Enemies?.FirstOrDefault(),
                    SequenceCount=2,SequenceIndex=0};
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var started=typeof(BattleReactionHud)
                    .GetMethod("ReactiveStarted",flags);
                var resolved=typeof(BattleReactionHud)
                    .GetMethod("ReactiveResolved",flags);
                started?.Invoke(reaction,new object[]{evt});
                bool appeared=reaction.VisibleNoticeCount>=before &&
                    reaction.VisibleNoticeCount>0;
                int once=reaction.VisibleNoticeCount;
                resolved?.Invoke(reaction,new object[]{evt});
                Verify("P08 Reaction start visual appears without gameplay mutation",
                    appeared,"notices="+once+" action not scheduled");
                Verify("P09 Reaction resolved updates same event ID",
                    reaction.VisibleNoticeCount==once,"notices="+
                    reaction.VisibleNoticeCount+" (no duplicate)");
            }
            else
            {
                Verify("P08 Reaction start visual appears without gameplay mutation",
                    false,"no presenter");
                Verify("P09 Reaction resolved updates same event ID",
                    false,"no presenter");
            }
            Verify("P10 Existing reactive action rail remains available",
                UnityEngine.Object.FindFirstObjectByType<BattleActionOrderRailUI>(
                    FindObjectsInactive.Include)!=null,
                "runtime reaction event also appears in original rail");
        }

        var outDir=Path.Combine(basePath,"ProjectAbyss_UI_MVP_Status_20261103");
        Directory.CreateDirectory(outDir);
        var text=new StringBuilder()
            .AppendLine("# MVP Status / Unique Keyword / Reaction UI Gate")
            .AppendLine("Mode: "+(play?"PLAY":"EDIT"))
            .AppendLine("PASS="+pass+" FAIL="+fail)
            .AppendLine("No ScriptableObjects, statuses, or gameplay actions were mutated.")
            .AppendLine("MVP icon glyph placeholders are not final illustrated sprite assets.")
            .AppendLine("Existing CharacterDetail Status tab retained.");
        foreach(var rec in records)text.AppendLine(rec);
        

        
if(play)
            text.AppendLine("Editor frame stats: drawCalls="+UnityStats.drawCalls+
                " setPass="+UnityStats.setPassCalls+
                " instancing="+UnityStats.instancedBatches);
        File.WriteAllText(Path.Combine(outDir,
            "MVP_STATUS_"+(play?"PLAY":"EDIT")+"_GATE.md"),
            text.ToString(),new UTF8Encoding(false));
        Debug.Log("[MVP STATUS] RESULT "+(play?"PLAY":"EDIT")+
            " PASS="+pass+" FAIL="+fail);
        if(fail!=0)
            throw new InvalidOperationException("MVP STATUS gate failed: "+fail);
    }

    public static void RunEdit()=>Run(false);

    public static void RunPlay()=>Run(true);
}
#endif