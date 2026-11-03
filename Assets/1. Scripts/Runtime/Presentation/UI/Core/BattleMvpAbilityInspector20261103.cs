using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectAbyss.Abilities;

/// <summary>
/// Read-only MVP model projection. It consumes the current planning selection,
/// authored native/legacy entries and read-only R4 target allocation snapshot.
/// No gameplay state changes, target selection or RNG consumption.
/// </summary>
internal sealed class BattleMvpAbilityProjection20261103
{
    internal readonly struct Snapshot
    {
        public readonly string Heading;
        public readonly string Details;
        public readonly string Allocation;
        public readonly string Runtime;
        public Snapshot(string heading,string details,string allocation,string runtime)
        {
            Heading=heading;
            Details=details;
            Allocation=allocation;
            Runtime=runtime;
        }
    }

    private static string TriggerName(SkillEffectTiming value)
    {
        switch(value)
        {
            case SkillEffectTiming.OnAttackResolutionStarting:
                return "공격 해결 시작";
            case SkillEffectTiming.OnExchangeWin:
                return "교환 승리";
            case SkillEffectTiming.OnRollWin:
                return "구형 굴림 승리";
            case SkillEffectTiming.OnExchangeLose:
                return "교환 패배";
            case SkillEffectTiming.OnRollLose:
                return "구형 굴림 패배";
            case SkillEffectTiming.OnClashEnd:
                return "합 종료";
            case SkillEffectTiming.OnPreparationActivated:
                return "도사림 발동";
            case SkillEffectTiming.OnTurnStart:
                return "턴 시작";
            case SkillEffectTiming.OnTurnEnd:
                return "턴 종료";
            case SkillEffectTiming.AfterDamage:
                return "피해 정산 후";
            case SkillEffectTiming.OnDuelMatched:
                return "구형 결투 효과 게이트";
            default:
                return value.ToString();
        }
    }

    private static string TargetName(SkillEffectTargetSelector value)
    {
        switch(value)
        {
            case SkillEffectTargetSelector.CurrentTarget:
                return "현재 선택 대상";
            case SkillEffectTargetSelector.DamageTarget:
                return "실제 피해 대상";
            case SkillEffectTargetSelector.Owner:
            case SkillEffectTargetSelector.OwnerCharacter:
                return "사용자";
            case SkillEffectTargetSelector.Opponent:
                return "상대";
            case SkillEffectTargetSelector.KillVictim:
                return "처치 대상";
            case SkillEffectTargetSelector.RandomAliveEnemy:
                return "생존 적 1명";
            case SkillEffectTargetSelector.RandomAliveEnemyPart:
                return "생존 적 부위";
            case SkillEffectTargetSelector.RandomUsableOwnerPart:
                return "사용 가능한 자신의 부위";
            default:
                return value.ToString();
        }
    }

    
public Snapshot Build(BattleManager manager,BattleUIManager ui,string lastEvent)
    {
        var selection=ui?.Selection;
        Skill skill=selection?.Skill;
        if(skill==null)
            return new Snapshot(
                "<b>능력 정보</b>  ·  선택 대기",
                "스킬을 선택하면 발동·조건·대상·동작·수명 정보를 표시합니다.\n"+
                "위세·상태·전용 자원은 기존 캐릭터 HUD에서 갱신됩니다.",
                "가중치/조준은 전투 모델의 확정 결과만 표시합니다.",
                lastEvent??"전투 이벤트 대기");

        SkillDefinition definition=skill.Definition;
        string display=string.IsNullOrWhiteSpace(skill.SkillName)
            ? (definition?.SkillName??"스킬") : skill.SkillName;
        var entries=definition?.EnumerateEffectEntries()
            .Where(e=>e?.Definition!=null).ToArray()
            ?? Array.Empty<SkillEffectEntry>();

        string trigger=entries.Length==0?"전용 실행 경로 / 미등록":
            string.Join(", ",entries.Select(e=>TriggerName(e.EffectiveTiming))
                .Distinct().Take(3));
        string condition=entries.Length==0?"별도 캐릭터 규칙 참고":
            entries.Sum(e=>e.Conditions?.Count??0)+"개 조건";
        string targets=entries.Length==0?"현재 타깃 선택 규칙":
            string.Join(", ",entries.Select(e=>TargetName(e.Definition.TargetSelector))
                .Distinct().Take(3));
        string operation=entries.Length==0?"전용 메커닉 또는 미정 효과":
            string.Join(", ",entries.Select(e=>e.Definition.name)
                .Where(n=>!string.IsNullOrEmpty(n)).Take(2));
        string lifetime=entries.Any(e=>e.RestrictToRoll)
            ? "일부 효과 굴림 제한": "각 효과의 런타임 수명 계약";
        string body=
            "<color=#A8C9FF>발동</color>  "+trigger+"\n"+
            "<color=#A8C9FF>조건</color>  "+condition+"\n"+
            "<color=#A8C9FF>대상</color>  "+targets+"\n"+
            "<color=#A8C9FF>동작</color>  "+operation+"\n"+
            "<color=#A8C9FF>수명</color>  "+lifetime;

        var model=skill.AttackWeightTargetingModel;
        int weight=Mathf.Max(1,skill.AttackWeight);
        string mode=model==AttackWeightTargetingModel.SlotWeighted
            ? "슬롯 예산":"기존 캐릭터 대상";
        string allocation="공격 가중치 "+weight+"  ·  "+mode;
        var plans=manager?.ActionManager?.Slots;
        ActionSlot slot=null;
        if(plans!=null && selection?.Owner!=null)
        {
            slot=plans.FirstOrDefault(p=>p!=null &&
                ReferenceEquals(p.Owner,selection.Owner) &&
                ReferenceEquals(p.Part,selection.OwnerPart) &&
                p.ActionIndex==selection.ActionIndex &&
                ReferenceEquals(p.Skill,skill));
        }
        if(model==AttackWeightTargetingModel.SlotWeighted)
        {
            var battle=manager?.BattleContext;
            if(slot!=null && slot.TargetCharacter!=null && battle!=null)
            {
                IReadOnlyList<Character> hostileSide=null;
                if(ReferenceEquals(slot.Owner,battle.Player))
                    hostileSide=battle.Enemies;
                else if(ReferenceEquals(slot.TargetCharacter,battle.Player))
                    hostileSide=new Character[]{battle.Player};
                if(hostileSide!=null)
                {
                    // Same pure projection as the R4 live bridge; never call a
                    // model mutation or pretend a failed selection is valid.
                    var preview=R4LiveSlotQuery.Capture(
                        slot,hostileSide,plans,0,weight);
                    if(preview?.Selection?.Success==true &&
                       preview.HasUnambiguousMain)
                    {
                        var allocationModel=preview.Selection.Allocation;
                        allocation+="\n선정 슬롯 "+
                          (allocationModel?.SelectedSlots.Count??0)+
                          " · 실제 피격 단위 "+
                          (allocationModel?.DistinctHitTargets.Count??0)+
                          "  (메인만 합)";
                    }
                    else allocation+="\n계획 배분 미확정 — 실제 타깃 변동 가능";
                }
            }
            else allocation+="\n타깃 확정 전 — 배분 미리보기 없음";
        }
        else allocation+="\n레거시 모드 · 슬롯 예산으로 해석하지 않음";

        var target=slot?.TargetCharacter ?? selection?.Target;
        if(target!=null)
            allocation+="\n대상 "+target+
                (slot?.TargetPart!=null?" / "+slot.TargetPart.PartId:
                    selection?.TargetPart!=null?" / "+selection.TargetPart.PartId:"");

        return new Snapshot(
            "<b>"+display+"</b>  ·  "+skill.ActionType,
            body,
            allocation,
            lastEvent??"전투 이벤트 대기");
    }
}

/// <summary>
/// MVP View. The root shell is static; live TMP values sit on one nested
/// Canvas. Text meshes change only when the presenter's snapshot changes.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(910)]
public sealed class BattleMvpAbilityInspector20261103:MonoBehaviour
{
    [SerializeField] private bool visibleOnStart=false;

    private BattleManager battleManager;
    private BattleUIManager uiManager;
    private BattleEvent boundEvent;
    private BattleMvpAbilityProjection20261103 presenter;
    private RectTransform panel;
    private CanvasGroup canvasGroup;
    private TMP_Text heading,details,allocation,runtime;
    
    private RectTransform toggleRoot;
    private TMP_Text toggleLabel;
private Button toggleButton;
    private GameObject dynamicLayer;
    private bool built,expanded;
    private float nextPoll;
    private bool dirty=true;
    private string lastEvent="전투 이벤트 대기";
    private string oldHeading,oldDetails,oldAllocation,oldRuntime;
    private int renderedUpdates;
    private int skippedTextWrites;

    public int RenderedUpdates=>renderedUpdates;
    public int SkippedTextWrites=>skippedTextWrites;

    private void Awake()
    {
        presenter=new BattleMvpAbilityProjection20261103();
        expanded=visibleOnStart;
    }
    private void Start()
    {
        BuildView();
    }
    private void OnDisable()
    {
        Unbind();
    }
    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.F3))
            SetExpanded(!expanded);
        if(!built)return;

        // Show no extra overlay on top of existing modal surfaces.
        bool modalActive=transform.Find("SkillSelectionLayer")?.gameObject.activeInHierarchy==true ||
                         transform.Find("CharacterDetailLayer")?.gameObject.activeInHierarchy==true ||
                         transform.Find("EmotionAugmentChoiceOverlay")?.gameObject.activeInHierarchy==true;
        bool shouldShow=expanded && !modalActive;
        if(toggleRoot!=null && toggleRoot.gameObject.activeSelf==modalActive)
            toggleRoot.gameObject.SetActive(!modalActive);
        
if(panel.gameObject.activeSelf!=shouldShow)
            panel.gameObject.SetActive(shouldShow);
        if(!shouldShow)return;

        if(Time.unscaledTime<nextPoll && !dirty)return;
        nextPoll=Time.unscaledTime+0.15f;
        if(battleManager==null)
            battleManager=FindFirstObjectByType<BattleManager>();
        if(uiManager==null)
            uiManager=FindFirstObjectByType<BattleUIManager>();
        Bind();

        // Poll planning selection at 6.6 Hz: there is no universal selection
        // changed event. Domain events also mark dirty immediately.
        var snapshot=presenter.Build(battleManager,uiManager,lastEvent);
        Put(heading,snapshot.Heading,ref oldHeading);
        Put(details,snapshot.Details,ref oldDetails);
        Put(allocation,snapshot.Allocation,ref oldAllocation);
        Put(runtime,snapshot.Runtime,ref oldRuntime);
        dirty=false;
    }

    private void Put(TMP_Text label,string value,ref string last)
    {
        if(value==last)
        {
            skippedTextWrites++;
            return;
        }
        last=value;
        if(label!=null)
        {
            label.text=value??string.Empty;
            renderedUpdates++;
        }
    }

    private void Bind()
    {
        var next=battleManager?.BattleContext?._battleEvent;
        if(next!=null && next.IsDisposed)next=null;
        if(ReferenceEquals(next,boundEvent))return;
        Unbind();
        boundEvent=next;
        if(boundEvent==null)return;
        boundEvent.OnActionStart+=OnActionStart;
        boundEvent.OnActionEnd+=OnActionEnd;
        boundEvent.OnTurnStart+=OnTurnStart;
        boundEvent.OnTurnEnd+=OnTurnEnd;
        boundEvent.OnBattleEnded+=OnBattleEnded;
        dirty=true;
    }
    private void Unbind()
    {
        if(boundEvent==null)return;
        boundEvent.OnActionStart-=OnActionStart;
        boundEvent.OnActionEnd-=OnActionEnd;
        boundEvent.OnTurnStart-=OnTurnStart;
        boundEvent.OnTurnEnd-=OnTurnEnd;
        boundEvent.OnBattleEnded-=OnBattleEnded;
        boundEvent=null;
    }
    private void OnActionStart(BattleAction action)
    {
        lastEvent="행동 시작  ·  "+(action?.Skill?.SkillName??"스킬")+"  #"+
            (action?.ActionId.ToString()??"-");
        dirty=true;
    }
    private void OnActionEnd(BattleAction action)
    {
        lastEvent="행동 종료  ·  "+(action?.Skill?.SkillName??"스킬");
        dirty=true;
    }
    private void OnTurnStart(int turn)
    {
        lastEvent="턴 "+turn+" 시작  ·  계획 단계";
        dirty=true;
    }
    private void OnTurnEnd(int turn)
    {
        lastEvent="턴 "+turn+" 종료";
        dirty=true;
    }
    private void OnBattleEnded()
    {
        lastEvent="전투 종료";
        dirty=true;
        Unbind();
    }

    private void SetExpanded(bool value)
    {
        expanded=value;
        dirty=true;
        if(built && panel!=null)
            panel.gameObject.SetActive(value);
    }

    private void BuildView()
    {
        if(built)return;
        var parent=GetComponent<RectTransform>();
        if(parent==null)return;
        var existing=transform.Find("MvpAbilityInsight");
        if(existing!=null)
        {
            panel=existing as RectTransform;
            built=panel!=null;
            return;
        }
        GameObject root=new GameObject(
            "MvpAbilityInsight",typeof(RectTransform),typeof(Image),
            typeof(CanvasGroup));
        root.transform.SetParent(parent,false);
        panel=root.GetComponent<RectTransform>();
        panel.anchorMin=panel.anchorMax=new Vector2(1f,1f);
        panel.pivot=new Vector2(1f,1f);
        panel.anchoredPosition=new Vector2(-18f,-158f);
        panel.sizeDelta=new Vector2(520f,340f);

        var bg=root.GetComponent<Image>();
        bg.color=new Color(0.035f,0.050f,0.080f,0.94f);
        bg.raycastTarget=false;
        canvasGroup=root.GetComponent<CanvasGroup>();
        canvasGroup.interactable=true;
        canvasGroup.blocksRaycasts=true;

        var existingToggle=transform.Find("MvpAbilityToggle");
        if(existingToggle==null)
        {
            var toggleGo=new GameObject("MvpAbilityToggle",
                typeof(RectTransform),typeof(Image),typeof(Button));
            toggleGo.transform.SetParent(parent,false);
            toggleRoot=toggleGo.GetComponent<RectTransform>();
            toggleRoot.anchorMin=toggleRoot.anchorMax=new Vector2(1f,1f);
            toggleRoot.pivot=new Vector2(1f,1f);
            toggleRoot.anchoredPosition=new Vector2(-18f,-57f);
            toggleRoot.sizeDelta=new Vector2(150f,28f);
            var toggleBg=toggleGo.GetComponent<Image>();
            toggleBg.color=new Color(0.11f,0.18f,0.27f,0.92f);
            toggleButton=toggleGo.GetComponent<Button>();
            toggleButton.targetGraphic=toggleBg;
            toggleButton.onClick.AddListener(()=>SetExpanded(!expanded));
        }
        else toggleRoot=existingToggle as RectTransform;
        
        
var template=transform.Find("PersistentTopLayer/TopStatusBar/TurnText")
            ?.GetComponent<TMP_Text>();
        
        if(toggleRoot!=null)
        {
            toggleLabel=CreateText(toggleRoot,"ToggleLabel","능력 정보  [F3]",
                Vector2.zero,Vector2.one,
                new Vector2(5f,0f),new Vector2(-5f,0f),
                13,template,FontStyles.Bold);
            toggleLabel.alignment=TextAlignmentOptions.Center;
        }
if(template==null)
            template=GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();

        CreateText(panel,"StaticTitle","능력 계약  ·  발동 / 조건 / 대상 / 동작 / 수명",
            new Vector2(0f,1f),new Vector2(1f,1f),
            new Vector2(16f,-40f),new Vector2(-18f,-8f),
            18,template,FontStyles.Bold);

        var dynamic=new GameObject(
            "MvpAbilityDynamicCanvas",typeof(RectTransform),typeof(Canvas));
        dynamic.transform.SetParent(panel,false);
        var r=dynamic.GetComponent<RectTransform>();
        r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;
        r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;
        var cv=dynamic.GetComponent<Canvas>();
        cv.overrideSorting=false;
        dynamicLayer=dynamic;

        heading=CreateText(r,"SelectedSkill",string.Empty,
            new Vector2(0,1),new Vector2(1,1),
            new Vector2(16,-83),new Vector2(-16,-44),
            21,template,FontStyles.Normal);
        details=CreateText(r,"FiveFactors",string.Empty,
            new Vector2(0,1),new Vector2(1,1),
            new Vector2(16,-225),new Vector2(-16,-86),
            17,template,FontStyles.Normal);
        allocation=CreateText(r,"WeightAndTarget",string.Empty,
            new Vector2(0,1),new Vector2(1,1),
            new Vector2(16,-301),new Vector2(-16,-230),
            16,template,FontStyles.Normal);
        runtime=CreateText(r,"RuntimeEvent",string.Empty,
            new Vector2(0,0),new Vector2(1,0),
            new Vector2(16,6),new Vector2(-16,34),
            15,template,FontStyles.Normal);
        var separator=new GameObject("Separator",typeof(RectTransform),typeof(Image));
        separator.transform.SetParent(panel,false);
        var sr=separator.GetComponent<RectTransform>();
        sr.anchorMin=new Vector2(0,0);sr.anchorMax=new Vector2(1,0);
        sr.pivot=new Vector2(0.5f,0.5f);
        sr.offsetMin=new Vector2(16f,40f);sr.offsetMax=new Vector2(-16f,41f);
        separator.GetComponent<Image>().color=new Color(0.25f,0.33f,0.45f,0.6f);
        separator.GetComponent<Image>().raycastTarget=false;

        built=true;
        panel.gameObject.SetActive(expanded);
        Debug.Log("[MVP Ability Inspector] Read-only 5-factor/slot-budget/"+
            "event TMP presentation created. Toggle F3 or click button.",this);
    }

    private static TMP_Text CreateText(RectTransform host,string name,
        string value,Vector2 min,Vector2 max,Vector2 offsetMin,
        Vector2 offsetMax,float fontSize,TMP_Text template,FontStyles styles)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));
        go.transform.SetParent(host,false);
        var rect=go.GetComponent<RectTransform>();
        rect.anchorMin=min;rect.anchorMax=max;
        rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;
        var text=go.GetComponent<TextMeshProUGUI>();
        if(template!=null)
        {
            text.font=template.font;
            text.fontSharedMaterial=template.fontSharedMaterial;
        }
        else if(TMP_Settings.defaultFontAsset!=null)
            text.font=TMP_Settings.defaultFontAsset;
        text.fontSize=fontSize;
        text.fontStyle=styles;
        text.color=new Color(0.90f,0.94f,1f);
        text.richText=true;
        text.alignment=TextAlignmentOptions.TopLeft;
        text.enableWordWrapping=false;
        text.overflowMode=TextOverflowModes.Ellipsis;
        text.raycastTarget=false;
        text.text=value??string.Empty;
        return text;
    }
}