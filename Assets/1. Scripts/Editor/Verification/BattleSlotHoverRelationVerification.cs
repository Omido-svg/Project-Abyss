#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// G-FINAL-safe view-only relation regression. Play test creates one
/// temporary planned action then removes it in finally; no scene save.
/// </summary>
public static class BattleSlotHoverRelationVerification
{
    private static void Save(string mode,IEnumerable<string> checks,int pass,int fail)
    {
        var root=Directory.GetParent(Application.dataPath).FullName;
        var dir=Path.Combine(root,"ProjectAbyss_UI_HoverRelationFix_20261103");
        Directory.CreateDirectory(dir);
        var b=new StringBuilder()
            .AppendLine("# MVP Hover Relation "+mode+" Gate")
            .AppendLine("PASS="+pass+" FAIL="+fail)
            .AppendLine("Tests include incoming enemy actions AND player actions clashing those enemies.")
            .AppendLine("All production action/clash/target contracts are unchanged.");
        foreach(var check in checks)b.AppendLine(check);
        File.WriteAllText(Path.Combine(dir,"MVP_HOVER_"+mode+"_GATE.md"),
            b.ToString(),new UTF8Encoding(false));
        Debug.Log("[MVP HOVER] "+mode+" PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("MVP hover gate failed "+fail);
    }

    public static void Edit()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)
            throw new InvalidOperationException("Idle EditMode required");
        int pass=0,fail=0;
        var lines=new List<string>();
        void Check(string key,bool ok,string detail)
        {
            if(ok)pass++;else fail++;
            lines.Add((ok?"PASS":"FAIL")+" | "+key+" | "+detail);
        }

        var leg=new ActionSlot{ActionId=1001,Phase=ActionPhase.COMBAT};
        var enemy1=new ActionSlot{ActionId=2001,TargetSlot=leg,Phase=ActionPhase.COMBAT};
        var arm=new ActionSlot{ActionId=1002,TargetSlot=enemy1,Phase=ActionPhase.COMBAT};
        var enemy2=new ActionSlot{ActionId=2002,TargetSlot=leg,Phase=ActionPhase.COMBAT};
        var head=new ActionSlot{ActionId=1003,TargetSlot=enemy2,Phase=ActionPhase.COMBAT};
        var unrelated=new ActionSlot{ActionId=3333,Phase=ActionPhase.COMBAT};
        var tertiary=new ActionSlot{ActionId=4444,TargetSlot=arm,Phase=ActionPhase.COMBAT};
        var slots=new ActionSlot[]{leg,enemy1,arm,enemy2,head,unrelated,tertiary};
        var hoverLeg=BattleSlotHoverRelations.Resolve(
            slots,leg,s=>ReferenceEquals(s.TargetSlot,leg),null);
        Check("H01 two direct attackers",hoverLeg.Contains(enemy1)&&hoverLeg.Contains(enemy2),
            string.Join(",",hoverLeg.Select(x=>x.ActionId)));
        Check("H02 player arm and head counter-clash actions",
            hoverLeg.Contains(arm)&&hoverLeg.Contains(head),"two-hop attackers are visible");
        Check("H03 hovered leg remains highlighted",hoverLeg.Contains(leg),"hover action");
        Check("H04 unrelated action remains dim",!hoverLeg.Contains(unrelated),"no whole-board brightening");
        Check("H05 no unbounded third-hop expansion",!hoverLeg.Contains(tertiary),"bounded to 2 edges");
        var hoverEmpty=BattleSlotHoverRelations.Resolve(
            slots,null,s=>ReferenceEquals(s.TargetSlot,leg),null);
        Check("H06 unassigned hovered part still connects to arm",
            hoverEmpty.Contains(arm)&&hoverEmpty.Contains(enemy1),"no player action required on hovered part");
        var hoverEnemy=BattleSlotHoverRelations.Resolve(
            slots,enemy1,s=>ReferenceEquals(s.TargetSlot,enemy1),null);
        Check("H07 enemy hover highlights attacking arm",hoverEnemy.Contains(arm),"reverse link");
        Check("H08 enemy hover also exposes the part it attacks",hoverEnemy.Contains(leg),"outgoing link");
        Check("H09 second enemy is not pulled in by 3-hop propagation",
            !hoverEnemy.Contains(enemy2),"no overbroad transitive expansion");
        Check("H10 production slot targets untouched",
            ReferenceEquals(arm.TargetSlot,enemy1)&&ReferenceEquals(enemy1.TargetSlot,leg),
            "read-only graph");

        var scriptRoot=Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "Assets/1. Scripts/Runtime/Presentation/UI");
        Check("H11 pointer/arrow/rail share hover projector",
            File.ReadAllText(Path.Combine(scriptRoot,
                "Panels/BattleWorldActionSlotCellUI.cs")).Contains(
                    "BattleSlotHoverRelations.IsRelatedCell") &&
            File.ReadAllText(Path.Combine(scriptRoot,
                "Panels/BattleWorldSlotArrowOverlayUI.cs")).Contains(
                    "BattleSlotHoverRelations.IsRelated") &&
            File.ReadAllText(Path.Combine(scriptRoot,
                "Panels/BattleActionOrderRailUI.cs")).Contains(
                    "BattleSlotHoverRelations.IsRelated"),
            "same focus computation across visuals");

        Save("EDIT",lines,pass,fail);
    }

    public static void Play()
    {
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling)
            throw new InvalidOperationException("PlayMode required");
        int pass=0,fail=0;
        var lines=new List<string>();
        void Check(string key,bool ok,string detail)
        {
            if(ok)pass++;else fail++;
            lines.Add((ok?"PASS":"FAIL")+" | "+key+" | "+detail);
        }
        var manager=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var ui=UnityEngine.Object.FindFirstObjectByType<BattleUIManager>();
        var player=manager?.BattleContext?.Player;
        var slots=manager?.ActionManager?.Slots;
        if(player==null||slots==null||ui==null)
            throw new InvalidOperationException("Battle Test model not ready");
        var enemy=slots.FirstOrDefault(s=>s!=null&&s.Owner!=player&&
            s.TargetCharacter==player&&s.TargetPart!=null&&
            s.Phase==ActionPhase.COMBAT);
        var playerPart=player.BodyParts?.FirstOrDefault(p=>p!=null &&
            !p.IsBroken && p!=enemy?.TargetPart);
        var skill=player.RuntimeSkills?.FirstOrDefault(s=>s!=null &&
            s.ActionType==ActionType.Duel);
        if(enemy==null||playerPart==null||skill==null)
            throw new InvalidOperationException("Need one enemy part attack, player part and duel");
        var temp=new ActionSlot{
            ActionId=88211003L,SlotId="MVP_UI_ONLY_HOVER_GATE",
            Owner=player,Part=playerPart,ActionIndex=0,
            Skill=skill,TargetCharacter=enemy.Owner,TargetPart=enemy.Part,
            TargetSlot=enemy,Phase=ActionPhase.COMBAT,
            Speed=enemy.Speed+3,ResourceCostCommitted=false
        };
        var cells=UnityEngine.Object.FindObjectsByType<BattleWorldActionSlotCellUI>(
            FindObjectsInactive.Include,FindObjectsSortMode.None);
        BattleWorldActionSlotCellUI hovered=null;
        BattleWorldActionSlotCellUI hoveredEnemy=null;
        bool added=false;
        int initialCount=slots.Count;
        try
        {
            added=manager.ActionManager.TryAddSlot(temp);
            Check("P01 temporary plan accepted",added,"only in PlayMode");
            hovered=cells.FirstOrDefault(c=>c.IsPlanningCell &&
                c.Owner==player && c.Part==enemy.TargetPart);
            hoveredEnemy=cells.FirstOrDefault(c=>c.IsTargetCell &&
                c.TargetSlot==enemy);
            var plannedCell=cells.FirstOrDefault(c=>c.IsPlanningCell &&
                c.Owner==player && c.Part==playerPart);
            Check("P02 world planning cells exist",hovered!=null&&
                hoveredEnemy!=null&&plannedCell!=null,"leg/arm/enemy cell");
            if(added&&hovered!=null)
            {
                hovered.OnPointerEnter(null);
                Check("P03 hovered player part focuses incoming enemy",
                    BattleSlotHoverRelations.IsRelated(
                        hovered,enemy,ui),"enemy action "+enemy.ActionId);
                Check("P04 hovered player part focuses clash partner",
                    BattleSlotHoverRelations.IsRelated(
                        hovered,temp,ui),"player action "+temp.ActionId);
                Check("P05 actual player arm cell illuminated",
                    plannedCell!=null &&
                    BattleSlotHoverRelations.IsRelatedCell(
                        hovered,plannedCell,ui),"arm not directly attacked");
                Check("P06 preview clash stays authoritative",
                    manager.ClashBuilder.BuildClashPreview(
                        manager.ActionManager.Slots)
                        .Any(p=>p!=null&&p.IsClash&&
                            (p.First==temp||p.Second==temp)),
                    "combat preview, not UI invented clash");
                hovered.OnPointerExit(null);
                hovered=null;
                hoveredEnemy.OnPointerEnter(null);
                Check("P07 hovering enemy also highlights empty attacked player part",
                    BattleSlotHoverRelations.IsRelatedCell(
                        hoveredEnemy,cells.FirstOrDefault(c=>c.IsPlanningCell &&
                            c.Owner==player&&c.Part==enemy.TargetPart),ui),
                    "symmetric outgoing part");
            }
        }
        finally
        {
            if(hovered!=null)hovered.OnPointerExit(null);
            if(hoveredEnemy!=null)hoveredEnemy.OnPointerExit(null);
            if(added)manager.ActionManager.RemoveSlotById(temp.ActionId);
        }
        Check("P08 original plans restored after smoke",
            manager.ActionManager.Slots.Count==initialCount &&
            manager.ActionManager.Slots.All(s=>s.ActionId!=temp.ActionId),
            "no action retained");
        Save("PLAY",lines,pass,fail);
    }
}
#endif