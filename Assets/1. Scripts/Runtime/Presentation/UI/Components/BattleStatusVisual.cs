using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure, read-only world/status-tab presentation derived from the existing
/// BattleStatusPresentationProjector. Never writes StatusEffect or BattleContext.
/// Symbols are TMP-ready MVP glyph icons; no missing sprite assets are invented.
/// </summary>
public static class BattleStatusVisual
{
    public readonly struct Badge
    {
        public readonly string Key;
        public readonly string Glyph;
        public readonly string Name;
        public readonly string Count;
        public readonly string Duration;
        public readonly string Part;
        public readonly string Description;
        public readonly Color Color;
        public readonly bool Unique;
        public readonly BattleStatusDisposition Disposition;

        public Badge(string key,string glyph,string name,string count,
            string duration,string part,string description,
            Color color,bool unique,BattleStatusDisposition disposition)
        {
            Key=key??string.Empty;
            Glyph=glyph??"상";
            Name=name??"상태";
            Count=count??string.Empty;
            Duration=duration??string.Empty;
            Part=part??string.Empty;
            Description=description??string.Empty;
            Color=color;
            Unique=unique;
            Disposition=disposition;
        }

        public string Signature => Key+"|"+Name+"|"+Count+"|"+Duration+"|"+Part+
            "|"+Unique+"|"+Disposition;
        public string CompactLabel =>
            Glyph+(string.IsNullOrEmpty(Count)?"":" "+Count)+
            (string.IsNullOrEmpty(Duration)?"":"\n"+Duration);
    }

    public static List<Badge> Build(Character character)
    {
        var result=new List<Badge>();
        if(character==null)return result;
        var chips=BattleStatusPresentationProjector.Build(character);
        foreach(var model in chips)
        {
            if(model?.Representative==null||!model.CurrentActive)continue;
            result.Add(FromChip(model));
        }
        // Gameplay-critical first, then grouped unique, other debuffs and buffs.
        result.Sort((a,b)=>{
            int pa=Priority(a);
            int pb=Priority(b);
            if(pa!=pb)return pa.CompareTo(pb);
            return string.Compare(a.Key,b.Key,StringComparison.Ordinal);
        });
        return result;
    }

    public static Badge FromEffect(StatusEffect effect,Character target)
    {
        if(effect==null)return default;
        var projection=BattleStatusPresentationProjector.BuildFromEffects(
            target,new[]{effect});
        if(projection.Count==0)return default;
        return FromChip(projection[0]);
    }

    private static int Priority(Badge b)
    {
        if(b.Unique)return 0;
        return b.Disposition==BattleStatusDisposition.Harmful?1:
            b.Disposition==BattleStatusDisposition.Beneficial?2:3;
    }

    private static Badge FromChip(BattleStatusChipModel model)
    {
        var effect=model.Representative;
        string name=BattleStatusUiText.GetDisplayName(effect);
        bool unique=effect is IUniqueKeywordStatus;
        var disposition=BattleStatusUiText.GetDisposition(effect);
        string count=model.PresentationKind switch
        {
            BattleStatusPresentationKind.NumericTimed =>
                model.CurrentTotalValue.ToString(),
            BattleStatusPresentationKind.Bespoke when model.BespokeCurrentValue>0 =>
                model.BespokeCurrentValue.ToString(),
            _ when model.EntryCount>1 => "×"+model.EntryCount,
            _ => string.Empty
        };
        string duration=model.HasInfiniteDuration?"∞":
            model.MaxRemainingDuration>0 ? model.MaxRemainingDuration+"T":
            string.Empty;
        string part=effect.OwnerPart!=null?
            BattleBodyPartUiText.GetDisplayName(effect.OwnerPart):
            string.Empty;
        string glyph=IconGlyph(effect,name);
        Color color=unique?new Color(0.64f,0.35f,0.88f):
            disposition==BattleStatusDisposition.Harmful?
            new Color(0.80f,0.29f,0.32f):
            disposition==BattleStatusDisposition.Beneficial?
            new Color(0.21f,0.62f,0.52f):
            new Color(0.42f,0.52f,0.67f);
        // Description remains detailed on the existing Status tab.
        return new Badge(model.StableKey,glyph,name,count,duration,part,
            BattleStatusUiText.BuildDescription(effect,model.ViewedCharacter),
            color,unique,disposition);
    }

    public static string IconGlyph(StatusEffect effect,string displayName=null)
    {
        if(effect is Bleeding)return "혈";
        if(effect is Burn)return "화";
        if(effect is OlafFearStatus)return "공";
        if(effect is YujinDesignationStatus)return "지";
        if(effect is StrengthStatus)return "힘";
        if(effect is ProtectionStatus)return "보";
        if(effect is SturdyStatus)return "강";
        if(effect is RegenerationStatus)return "재";
        if(effect is SwiftStatus)return "속";
        if(effect is WeaknessStatus)return "약";
        if(effect is DisarmStatus)return "무";
        if(effect is FractureStatus)return "파";
        if(effect is RuptureStatus)return "열";
        if(effect is Stun)return "기";
        if(effect is SealedPartStatus)return "봉";
        if(effect is BrokenPartStatus)return "파";
        if(effect is PartDisabledStatus)return "약";
        if(effect is IUniqueKeywordStatus)
        {
            var s=displayName??BattleStatusUiText.GetDisplayName(effect);
            return string.IsNullOrEmpty(s)?"고":s.Substring(0,1);
        }
        var name=displayName??BattleStatusUiText.GetDisplayName(effect);
        return string.IsNullOrWhiteSpace(name)?"상":name.Substring(0,1);
    }
}