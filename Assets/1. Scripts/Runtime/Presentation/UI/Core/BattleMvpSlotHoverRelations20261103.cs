using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Read-only MVP hover relation projection. A hovered part/action highlights:
/// 1) actions directly aimed at that slot,
/// 2) its own assigned action and outgoing target,
/// 3) actions targeting the incoming attacker (e.g. the player's arm that
///    clashes the attacker targeting the player's leg),
/// 4) authoritative preview clash partners.
/// This is deliberately a bounded one-hop expansion from the direct seeds.
/// No gameplay target, action, clash decision, or assignment is changed.
/// </summary>
public static class BattleMvpSlotHoverRelations20261103
{
    private static int cachedFrame=-1;
    private static BattleUIManager cachedManager;
    private static BattleWorldActionSlotCellUI cachedHovered;
    private static IReadOnlyList<ActionSlot> cachedSlots;
    private static int cachedSlotCount=-1;
    private static readonly HashSet<ActionSlot> cachedFocus=
        new HashSet<ActionSlot>();

    public static bool IsRelated(BattleWorldActionSlotCellUI hovered,
        ActionSlot candidate,BattleUIManager manager)
    {
        if(hovered==null||candidate==null||manager==null)
            return false;
        UpdateCache(hovered,manager);
        return cachedFocus.Contains(candidate);
    }

    /// <summary>
    /// Also marks a still-empty planning cell if a focused enemy attacks
    /// that part. This is needed when an enemy slot is hovered and its
    /// outgoing attack points to a player slot without an assigned skill.
    /// </summary>
    public static bool IsRelatedCell(
        BattleWorldActionSlotCellUI hovered,
        BattleWorldActionSlotCellUI candidate,
        BattleUIManager manager)
    {
        if(hovered==null||candidate==null||manager==null)
            return false;
        if(ReferenceEquals(hovered,candidate))return true;
        UpdateCache(hovered,manager);
        var represented=candidate.GetRepresentedActionSlot();
        if(represented!=null)
        {
            foreach(var source in cachedFocus)
                if(Same(represented,source))
                    return true;
        }
        foreach(var source in cachedFocus)
            if(TargetsHovered(source,candidate))
                return true;
        return false;
    }

    
public static int FocusCount(BattleWorldActionSlotCellUI hovered,
        BattleUIManager manager)
    {
        if(hovered==null||manager==null)return 0;
        UpdateCache(hovered,manager);
        return cachedFocus.Count;
    }

    private static void UpdateCache(BattleWorldActionSlotCellUI hovered,
        BattleUIManager manager)
    {
        var battle=manager.BattleManager;
        var slots=battle?.ActionManager?.Slots;
        int count=slots?.Count??0;
        if(cachedFrame==Time.frameCount &&
           ReferenceEquals(manager,cachedManager) &&
           ReferenceEquals(hovered,cachedHovered) &&
           ReferenceEquals(slots,cachedSlots) &&
           cachedSlotCount==count)
            return;

        cachedFrame=Time.frameCount;
        cachedManager=manager;
        cachedHovered=hovered;
        cachedSlots=slots;
        cachedSlotCount=count;
        cachedFocus.Clear();
        if(slots==null||hovered==null)return;

        IReadOnlyList<ClashPair> pairs=battle?.ClashBuilder
            ?.BuildClashPreview(slots);
        var selected=Resolve(slots,hovered.GetRepresentedActionSlot(),
            source=>TargetsHovered(source,hovered),pairs);
        foreach(var action in selected)
            cachedFocus.Add(action);
    }

    public static HashSet<ActionSlot> Resolve(
        IReadOnlyList<ActionSlot> slots,
        ActionSlot hoveredAction,
        Func<ActionSlot,bool> directlyTargetsHovered,
        IReadOnlyList<ClashPair> previewPairs)
    {
        var focus=new HashSet<ActionSlot>();
        if(slots==null)return focus;
        var seeds=new HashSet<ActionSlot>();
        foreach(var slot in slots)
        {
            if(slot==null||slot.Phase!=ActionPhase.COMBAT||slot.SkipResolution)
                continue;
            if(Same(slot,hoveredAction) ||
                (directlyTargetsHovered!=null && directlyTargetsHovered(slot)))
                seeds.Add(slot);
        }
        if(seeds.Count==0)return focus;

        foreach(var seed in seeds)focus.Add(seed);
        // One extra step: outgoing and reverse sources of every directly
        // affected action. This catches P-arm -> E-attack -> P-leg.
        foreach(var seed in seeds)
        {
            foreach(var candidate in slots)
            {
                if(candidate==null || candidate.Phase!=ActionPhase.COMBAT ||
                    candidate.SkipResolution)
                    continue;
                if(TargetsAction(seed,candidate) ||
                   TargetsAction(candidate,seed))
                    focus.Add(candidate);
            }
        }
        // ClashBuilder is authoritative if the two intents are a real clash,
        // even when the recorded target was part-only rather than TargetSlot.
        if(previewPairs!=null)
        {
            foreach(var pair in previewPairs)
            {
                if(pair==null||!pair.IsClash||
                    pair.First==null||pair.Second==null)
                    continue;
                if(seeds.Contains(pair.First)||seeds.Contains(pair.Second))
                {
                    focus.Add(pair.First);
                    focus.Add(pair.Second);
                }
            }
        }
        return focus;
    }

    private static bool TargetsHovered(ActionSlot source,
        BattleWorldActionSlotCellUI cell)
    {
        if(source==null||cell==null)return false;
        if(source.TargetSlot!=null)
        {
            if(cell.IsTargetCell)
                return Same(source.TargetSlot,cell.TargetSlot);
            var represented=cell.GetRepresentedActionSlot();
            return represented!=null&&Same(source.TargetSlot,represented);
        }
        return source.TargetCharacter!=null &&
            source.TargetCharacter==cell.Owner &&
            SamePart(source.TargetPart,cell.Part);
    }

    private static bool TargetsAction(ActionSlot source,ActionSlot target)
    {
        if(source==null||target==null||source==target)return false;
        if(source.TargetSlot!=null)
            return Same(source.TargetSlot,target);
        return source.TargetCharacter!=null && target.Owner!=null &&
               source.TargetCharacter==target.Owner &&
               SamePart(source.TargetPart,target.Part);
    }

    private static bool Same(ActionSlot a,ActionSlot b)
    {
        return a!=null && b!=null &&
            (ReferenceEquals(a,b) ||
             (a.ActionId>0 && b.ActionId>0 &&
              a.ActionId==b.ActionId && a.Owner==b.Owner));
    }

    private static bool SamePart(BodyPart a,BodyPart b)
    {
        if(ReferenceEquals(a,b))return true;
        return a!=null&&b!=null&&a.Owner==b.Owner&&
            string.Equals(a.PartId,b.PartId,StringComparison.Ordinal);
    }
}