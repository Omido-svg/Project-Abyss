using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reuses the existing Status tab's full source/part/duration/description flow.
/// Adds an icon glyph, not a second status inventory or popup.
/// </summary>
public static class BattleMvpDetailStatusBadge20261103
{
    public static void Attach(Button row,StatusEffect effect,Character character)
    {
        if(row==null||effect==null)return;
        if(row.transform.Find("MVP_StatusIcon")!=null)return;

        var badge=BattleMvpStatusVisual20261103.FromEffect(effect,character);
        if(string.IsNullOrEmpty(badge.Name))return;

        var icon=new GameObject("MVP_StatusIcon",typeof(RectTransform),
            typeof(Image));
        icon.transform.SetParent(row.transform,false);
        var rect=icon.GetComponent<RectTransform>();
        rect.anchorMin=rect.anchorMax=new Vector2(0f,0.5f);
        rect.pivot=new Vector2(0f,0.5f);
        rect.anchoredPosition=new Vector2(7f,0f);
        rect.sizeDelta=new Vector2(35f,35f);
        var image=icon.GetComponent<Image>();
        Color c=badge.Color;
        image.color=new Color(c.r*0.58f,c.g*0.58f,c.b*0.58f,0.98f);
        image.raycastTarget=false;

        var glyphGo=new GameObject("Glyph",typeof(RectTransform),
            typeof(TextMeshProUGUI));
        glyphGo.transform.SetParent(icon.transform,false);
        var glyphRect=glyphGo.GetComponent<RectTransform>();
        glyphRect.anchorMin=Vector2.zero;glyphRect.anchorMax=Vector2.one;
        glyphRect.offsetMin=glyphRect.offsetMax=Vector2.zero;

        var glyph=glyphGo.GetComponent<TextMeshProUGUI>();
        var existing=row.GetComponentInChildren<TMP_Text>(true);
        if(existing!=null)
        {
            glyph.font=existing.font;
            var labelRect=existing.rectTransform;
            var offset=labelRect.offsetMin;
            if(offset.x<45f){offset.x=45f;labelRect.offsetMin=offset;}
        }
        else if(TMP_Settings.defaultFontAsset!=null)
            glyph.font=TMP_Settings.defaultFontAsset;
        glyph.fontSize=22f;
        glyph.fontStyle=FontStyles.Bold;
        glyph.alignment=TextAlignmentOptions.Center;
        glyph.color=Color.white;
        glyph.raycastTarget=false;
        glyph.text=badge.Glyph;
    }
}