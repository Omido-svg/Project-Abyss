using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World HP plate child. Displays status/buff/debuff/unique-keyword chips,
/// grouped by stable runtime status ID and carrier part. Read-only View;
/// no gameplay or detail-panel state is modified.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleWorldStatusStrip : MonoBehaviour
{
    private const int VisibleSlots=4;
    private Character owner;
    private RectTransform root;
    private readonly List<ChipView> views=new List<ChipView>();
    private TMP_FontAsset font;
    private bool dirty=true;
    private float nextPassiveCheck;
    private string lastSignature=string.Empty;
    private int actualTextWrites;
    private int suppressedRefreshes;
    private int activeChipCount;

    public Character Owner=>owner;
    public int VisibleChipCount=>activeChipCount;
    public int ActualTextWrites=>actualTextWrites;
    public int SuppressedRefreshes=>suppressedRefreshes;
    public string LastSignature=>lastSignature;

    private sealed class ChipView
    {
        public RectTransform Root;
        public Image Background;
        public TMP_Text Glyph;
        public TMP_Text Amount;
        public TMP_Text Duration;
        public TMP_Text Part;
        public string Signature;
    }

    public void Configure(Character character,TMP_Text preferredFont)
    {
        owner=character;
        font=preferredFont!=null?preferredFont.font:TMP_Settings.defaultFontAsset;
        Build();
        dirty=true;
        Refresh(force:true);
    }

    public void MarkDirty()=>dirty=true;

    private void LateUpdate()
    {
        if(owner==null||root==null)return;
        if(!dirty && Time.unscaledTime<nextPassiveCheck)return;
        Refresh(false);
    }

    public void Refresh(bool force=false)
    {
        if(owner==null||root==null)return;
        nextPassiveCheck=Time.unscaledTime+0.65f;
        dirty=false;
        var badges=BattleStatusVisual.Build(owner);
        string signature=string.Join(";",badges.Select(x=>x.Signature));
        if(!force && signature==lastSignature)
        {
            suppressedRefreshes++;
            return;
        }
        lastSignature=signature;
        int size=Math.Min(VisibleSlots,badges.Count);
        bool overflow=badges.Count>VisibleSlots;
        int contentSlots=overflow?VisibleSlots-1:size;
        activeChipCount=size;

        if(root.gameObject.activeSelf!=(size>0))
            root.gameObject.SetActive(size>0);
        if(size==0)return;

        for(int i=0;i<views.Count;i++)
        {
            bool show=i<size;
            if(views[i].Root.gameObject.activeSelf!=show)
                views[i].Root.gameObject.SetActive(show);
            if(!show)continue;
            if(overflow && i==VisibleSlots-1)
                ApplyOverflow(views[i],badges.Count-contentSlots);
            else
                Apply(views[i],badges[i]);
        }
    }

    private void ApplyOverflow(ChipView view,int hidden)
    {
        string signature="OVERFLOW|"+hidden;
        if(view.Signature==signature)return;
        view.Signature=signature;
        view.Background.color=new Color(0.24f,0.29f,0.37f,0.95f);
        Write(view.Glyph,"+");
        Write(view.Amount,hidden.ToString());
        Write(view.Duration,string.Empty);
        Write(view.Part,string.Empty);
    }

    private void Apply(ChipView view,BattleStatusVisual.Badge badge)
    {
        if(view.Signature==badge.Signature)return;
        view.Signature=badge.Signature;
        var tint=badge.Color;
        view.Background.color=new Color(tint.r*0.52f,tint.g*0.52f,
            tint.b*0.52f,0.96f);
        Write(view.Glyph,badge.Glyph);
        Write(view.Amount,badge.Count);
        Write(view.Duration,badge.Duration);
        Write(view.Part,string.IsNullOrEmpty(badge.Part)?"":badge.Part);
    }

    private void Write(TMP_Text text,string value)
    {
        if(text!=null && text.text!=(value??string.Empty))
        {
            text.text=value??string.Empty;
            actualTextWrites++;
        }
    }

    private void Build()
    {
        if(root!=null)return;
        var existing=transform.Find("MVP_StatusStrip");
        if(existing is RectTransform rect)
        {
            root=rect;
            return;
        }

        var go=new GameObject("MVP_StatusStrip",typeof(RectTransform),
            typeof(Canvas));
        go.transform.SetParent(transform,false);
        root=go.GetComponent<RectTransform>();
        root.anchorMin=root.anchorMax=new Vector2(0.5f,0f);
        root.pivot=new Vector2(0.5f,1f);
        root.anchoredPosition=new Vector2(0f,-7f);
        root.sizeDelta=new Vector2(418f,48f);
        var canvas=go.GetComponent<Canvas>();
        canvas.overrideSorting=false;
        canvas.pixelPerfect=false;

        for(int i=0;i<VisibleSlots;i++)
        {
            var background=new GameObject("Status_"+i,typeof(RectTransform),
                typeof(Image));
            background.transform.SetParent(root,false);
            var item=background.GetComponent<RectTransform>();
            item.anchorMin=item.anchorMax=new Vector2(0,0.5f);
            item.pivot=new Vector2(0,0.5f);
            item.sizeDelta=new Vector2(100f,44f);
            item.anchoredPosition=new Vector2(i*105f,0f);
            var image=background.GetComponent<Image>();
            image.color=new Color(0.2f,0.2f,0.3f,0.96f);
            image.raycastTarget=false;
            var view=new ChipView{
                Root=item,
                Background=image,
                Glyph=Text(item,"Icon",new Vector2(0,0.30f),new Vector2(0.40f,1),
                    24f,TextAlignmentOptions.Center),
                Amount=Text(item,"Count",new Vector2(0.36f,0.36f),new Vector2(1,1),
                    24f,TextAlignmentOptions.MidlineRight),
                Duration=Text(item,"Duration",new Vector2(0.1f,0.0f),new Vector2(0.95f,0.42f),
                    16f,TextAlignmentOptions.MidlineRight),
                Part=Text(item,"Part",new Vector2(0.05f,0.0f),new Vector2(0.58f,0.40f),
                    13f,TextAlignmentOptions.MidlineLeft)
            };
            views.Add(view);
            item.gameObject.SetActive(false);
        }
        root.gameObject.SetActive(false);
    }

    private TMP_Text Text(RectTransform host,string name,Vector2 min,
        Vector2 max,float size,TextAlignmentOptions alignment)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));
        go.transform.SetParent(host,false);
        var r=go.GetComponent<RectTransform>();
        r.anchorMin=min;r.anchorMax=max;
        r.offsetMin=new Vector2(2f,0f);r.offsetMax=new Vector2(-2f,0f);
        var t=go.GetComponent<TextMeshProUGUI>();
        if(font!=null)t.font=font;
        t.fontSize=size;
        t.color=Color.white;
        t.alignment=alignment;
        t.overflowMode=TextOverflowModes.Truncate;
        t.textWrappingMode=TextWrappingModes.NoWrap;
        t.raycastTarget=false;
        t.text=string.Empty;
        return t;
    }
}