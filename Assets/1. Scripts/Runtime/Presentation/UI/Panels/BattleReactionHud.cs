using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One Presenter/event subscription for battle status invalidation and actual
/// reactive roll notifications. The status ViewModels are built by the existing
/// pure BattleStatusPresentationProjector. A domain event never triggers damage.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(930)]
public sealed class BattleReactionHud : MonoBehaviour
{
    private BattleManager manager;
    private BattleEvent boundEvent;
    private float nextBindAttempt;
    private RectTransform root;
    private TMP_FontAsset font;
    private readonly List<ToastView> rows=new List<ToastView>();
    private readonly List<ToastItem> notices=new List<ToastItem>();
    private bool contentDirty;
    private int callbackCount;
    private int textWriteCount;

    private sealed class ToastItem
    {
        public long Id;
        public string Text;
        public Color Accent;
        public float Expires;
    }
    private sealed class ToastView
    {
        public GameObject GameObject;
        public Image Background;
        public TMP_Text Text;
        public string LastText;
    }

    public int CallbackCount=>callbackCount;
    public int TextWriteCount=>textWriteCount;
    public int VisibleNoticeCount=>notices.Count;
    public bool Bound=>boundEvent!=null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SubscribeOnSceneLoad()
    {
        // The run-flow loads Battle scenes after initial application startup.
        // Never rely on AfterSceneLoad firing for every later scene transition.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(
        UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Install();
    }

    
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var host=FindFirstObjectByType<BattleScreenModeController>(
            FindObjectsInactive.Include);
        if(host!=null && host.GetComponent<BattleReactionHud>()==null)
            host.gameObject.AddComponent<BattleReactionHud>();
    }

    private void Awake()
    {
        BuildView();
    }

    private void OnEnable()
    {
        nextBindAttempt=0f;
    }

    private void Update()
    {
        if(boundEvent==null||boundEvent.IsDisposed)
        {
            if(Time.unscaledTime>=nextBindAttempt)
            {
                nextBindAttempt=Time.unscaledTime+0.20f;
                BindCurrent();
            }
        }
        if(notices.Count>0)
        {
            int before=notices.Count;
            notices.RemoveAll(x=>Time.unscaledTime>=x.Expires);
            if(before!=notices.Count)contentDirty=true;
        }
        if(contentDirty)Render();
    }

    private void BindCurrent()
    {
        if(manager==null)manager=FindFirstObjectByType<BattleManager>();
        var current=manager?.BattleContext?._battleEvent;
        if(current!=null && current.IsDisposed)current=null;
        if(ReferenceEquals(current,boundEvent))return;
        Unbind();
        boundEvent=current;
        if(boundEvent==null)return;
        boundEvent.OnStatusApplyResolved+=StatusApplied;
        boundEvent.OnStatusTicked+=StatusTicked;
        boundEvent.OnStatusRemovedDetailed+=StatusRemoved;
        boundEvent.OnTurnStart+=TurnStarted;
        boundEvent.OnTurnEnd+=TurnEnded;
        boundEvent.OnActionStart+=CounterActionStarted;
        boundEvent.OnActionEnd+=CounterActionEnded;
        
boundEvent.OnReactiveRollStarted+=ReactiveStarted;
        boundEvent.OnReactiveRollResolved+=ReactiveResolved;
        boundEvent.OnBattleEnded+=BattleEnded;
    }

    private void Unbind()
    {
        if(boundEvent==null)return;
        boundEvent.OnStatusApplyResolved-=StatusApplied;
        boundEvent.OnStatusTicked-=StatusTicked;
        boundEvent.OnStatusRemovedDetailed-=StatusRemoved;
        boundEvent.OnTurnStart-=TurnStarted;
        boundEvent.OnTurnEnd-=TurnEnded;
        boundEvent.OnActionStart-=CounterActionStarted;
        boundEvent.OnActionEnd-=CounterActionEnded;
        
boundEvent.OnReactiveRollStarted-=ReactiveStarted;
        boundEvent.OnReactiveRollResolved-=ReactiveResolved;
        boundEvent.OnBattleEnded-=BattleEnded;
        boundEvent=null;
    }

    private void OnDisable()
    {
        Unbind();
        notices.Clear();
        contentDirty=true;
        Render();
    }

    private void OnDestroy()=>Unbind();

    private void Dirty(Character character)
    {
        if(character==null)return;
        var strips=FindObjectsByType<BattleWorldStatusStrip>(
            FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(var strip in strips)
            if(strip!=null && strip.Owner==character)strip.MarkDirty();
    }
    private void DirtyAll()
    {
        foreach(var strip in FindObjectsByType<BattleWorldStatusStrip>(
            FindObjectsInactive.Include,FindObjectsSortMode.None))
            strip?.MarkDirty();
    }

    private void StatusApplied(StatusEffectApplyResult result)
    {
        callbackCount++;
        Dirty(result?.TargetCharacter);
    }
    private void StatusTicked(StatusEffectTickContext context)
    {
        callbackCount++;
        Dirty(context?.TargetCharacter);
    }
    private void StatusRemoved(Character character,BodyPart part,
        StatusEffect status,StatusEffectRemoveReason reason)
    {
        callbackCount++;
        Dirty(character);
    }
    private void TurnStarted(int turn)
    {
        callbackCount++;
        DirtyAll();
    }
    private void TurnEnded(int turn)
    {
        callbackCount++;
        DirtyAll();
    }
    private void BattleEnded()
    {
        callbackCount++;
        DirtyAll();
        notices.Clear();
        contentDirty=true;
        Unbind();
    }

    private static string Name(Character character)=>
        character==null?"대상":(character.Data?.CharacterName??character.name);

    private static string Kind(BattleReactiveRollSourceKind kind)=>
        kind switch
        {
            BattleReactiveRollSourceKind.Counter=>"반격",
            BattleReactiveRollSourceKind.FollowUp=>"후속 굴림",
            BattleReactiveRollSourceKind.BonusRoll=>"추가 굴림",
            _=>"반응 굴림"
        };

    // R7-8 enemy counter is a real queued Action, not a
    // BattleReactiveRollEvent. Only label the explicitly tagged reaction,
    // never guess based on arbitrary ActionId or skill name.
    private void CounterActionStarted(BattleAction action)
    {
        if(!EnemyCounterReaction.IsCounterReaction(action))
            return;
        callbackCount++;
        AddOrUpdate(action.ActionId,
            "<b>반격 행동</b>  "+Name(action.Owner)+" → "+Name(action.Target),
            new Color(0.78f,0.43f,0.91f),2.3f);
    }

    private void CounterActionEnded(BattleAction action)
    {
        if(!EnemyCounterReaction.IsCounterReaction(action))
            return;
        callbackCount++;
        AddOrUpdate(action.ActionId,
            "<b>반격 행동 종료</b>  "+Name(action.Owner),
            new Color(0.43f,0.67f,0.92f),1.3f);
    }

    
private void ReactiveStarted(BattleReactiveRollEvent ev)
    {
        if(ev==null)return;
        callbackCount++;
        string kind=Kind(ev.SourceKind);
        string title="반응 발생 · "+kind;
        string summary=Name(ev.Owner)+" → "+Name(ev.Target)+
            "  ("+ev.DisplaySequenceNumber+"/"+Math.Max(1,ev.SequenceCount)+")";
        AddOrUpdate(ev.EventId,"<b>"+title+"</b>  "+summary,
            new Color(0.62f,0.39f,0.89f),2.3f);
    }
    private void ReactiveResolved(BattleReactiveRollEvent ev)
    {
        if(ev==null)return;
        callbackCount++;
        string title="반응 정산 · "+Kind(ev.SourceKind);
        string summary=Name(ev.Owner)+" → "+Name(ev.Target);
        AddOrUpdate(ev.EventId,"<b>"+title+"</b>  "+summary,
            new Color(0.36f,0.67f,0.93f),1.35f);
    }

    private void AddOrUpdate(long id,string value,Color tint,float seconds)
    {
        var item=notices.Find(x=>x.Id==id);
        if(item==null)
        {
            item=new ToastItem{Id=id};
            notices.Add(item);
        }
        item.Text=value;
        item.Accent=tint;
        item.Expires=Time.unscaledTime+seconds;
        if(notices.Count>3)notices.RemoveAt(0);
        contentDirty=true;
    }

    private void Render()
    {
        contentDirty=false;
        if(root==null)return;
        if(root.gameObject.activeSelf!=(notices.Count>0))
            root.gameObject.SetActive(notices.Count>0);
        for(int i=0;i<rows.Count;i++)
        {
            bool visible=i<notices.Count;
            if(rows[i].GameObject.activeSelf!=visible)
                rows[i].GameObject.SetActive(visible);
            if(!visible)continue;
            var item=notices[notices.Count-1-i];
            var view=rows[i];
            view.Background.color=new Color(item.Accent.r*0.33f,
                item.Accent.g*0.33f,item.Accent.b*0.33f,0.94f);
            if(view.LastText!=item.Text)
            {
                view.Text.text=item.Text;
                view.LastText=item.Text;
                textWriteCount++;
            }
        }
    }

    private void BuildView()
    {
        if(root!=null)return;
        var canvasHost=GetComponent<RectTransform>();
        if(canvasHost==null)return;
        var template=GetComponentInChildren<TMP_Text>(true);
        font=template!=null?template.font:TMP_Settings.defaultFontAsset;
        var go=new GameObject("MVP_ReactionEventCanvas",typeof(RectTransform),
            typeof(Canvas));
        go.transform.SetParent(canvasHost,false);
        root=go.GetComponent<RectTransform>();
        root.anchorMin=root.anchorMax=new Vector2(0.5f,1f);
        root.pivot=new Vector2(0.5f,1f);
        root.anchoredPosition=new Vector2(0f,-118f);
        root.sizeDelta=new Vector2(620f,174f);
        go.GetComponent<Canvas>().overrideSorting=false;
        for(int i=0;i<3;i++)
        {
            var item=new GameObject("Reaction_"+i,typeof(RectTransform),
                typeof(Image));
            item.transform.SetParent(root,false);
            var r=item.GetComponent<RectTransform>();
            r.anchorMin=r.anchorMax=new Vector2(0.5f,1f);
            r.pivot=new Vector2(0.5f,1f);
            r.anchoredPosition=new Vector2(0f,-i*56f);
            r.sizeDelta=new Vector2(610f,50f);
            var bg=item.GetComponent<Image>();
            bg.raycastTarget=false;
            var labelGo=new GameObject("Label",typeof(RectTransform),
                typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(item.transform,false);
            var tr=labelGo.GetComponent<RectTransform>();
            tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;
            tr.offsetMin=new Vector2(13f,0f);tr.offsetMax=new Vector2(-13f,0f);
            var text=labelGo.GetComponent<TextMeshProUGUI>();
            if(font!=null)text.font=font;
            text.fontSize=20f;
            text.color=Color.white;
            text.alignment=TextAlignmentOptions.Center;
            text.textWrappingMode=TextWrappingModes.NoWrap;
            text.overflowMode=TextOverflowModes.Truncate;
            text.richText=true;
            text.raycastTarget=false;
            text.text=string.Empty;
            rows.Add(new ToastView{GameObject=item,Background=bg,Text=text});
            item.SetActive(false);
        }
        root.gameObject.SetActive(false);
    }
}