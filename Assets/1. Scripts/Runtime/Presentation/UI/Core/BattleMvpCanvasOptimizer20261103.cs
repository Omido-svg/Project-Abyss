using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MVP view-only uGUI partitioning. Runs after scene load and never changes
/// gameplay, authoring assets or saved scene hierarchy.
/// A Canvas rebuilds its own graphic batch; this does NOT enable GPU instancing
/// and extra canvases can increase batches / draw calls.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(900)]
public sealed class BattleMvpCanvasOptimizer20261103 : MonoBehaviour
{
    private readonly List<Canvas> createdCanvases=new List<Canvas>();
    private readonly List<GraphicRaycaster> createdRaycasters=new List<GraphicRaycaster>();
    private bool installed;
    private int relocatedTextCount;

    public int CreatedCanvasCount=>createdCanvases.Count;
    public int RelocatedTextCount=>relocatedTextCount;

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
        AttachToScene();
    }

    
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToScene()
    {
        // Scene-authored controller exists only in battle UI. Do not create
        // a replacement HUD for other scenes.
        var controller=FindFirstObjectByType<BattleScreenModeController>(
            FindObjectsInactive.Include);
        if(controller==null)return;
        var host=controller.gameObject;
        if(host.GetComponent<BattleMvpCanvasOptimizer20261103>()==null)
            host.AddComponent<BattleMvpCanvasOptimizer20261103>();
        if(host.GetComponent<BattleMvpAbilityInspector20261103>()==null)
            host.AddComponent<BattleMvpAbilityInspector20261103>();
    }

    private void Start()
    {
        Install();
    }

    public void Install()
    {
        if(installed)return;
        var root=transform.Find("PersistentTopLayer");
        var defaultBattle=transform.Find("DefaultBattleLayer");
        if(root==null||defaultBattle==null)
        {
            Debug.LogWarning("[MVP Canvas] Required authored battle UI layers unavailable.",this);
            return;
        }

        installed=true;
        // Static outer shell and low-change screens; no game data ownership.
        Isolate(root,false);
        Isolate(transform.Find("SkillSelectionLayer"),true);
        Isolate(transform.Find("CharacterDetailLayer"),true);
        Isolate(transform.Find("EmotionAugmentChoiceOverlay"),true);
        Isolate(transform.Find("ClashRollPresentationLayer"),false);
        Isolate(transform.Find("BattleActionAnnouncePanel"),false);

        // More frequent planning / gameplay values are isolated from the
        // static shell. These boundaries are feature-sized, not per-glyph.
        Isolate(defaultBattle,true);
        Isolate(root.Find("GameplayV5ProgressionHUD"),false);
        Isolate(root.Find("TopStatusBar/MomentumBar"),false);

        // The top-bar text is changed continuously by existing presenters.
        // Place all non-interactive TMP glyphs under ONE nested dynamic Canvas.
        // Keep the static background in the parent Canvas.
        var bar=root.Find("TopStatusBar") as RectTransform;
        if(bar!=null)
        {
            const string name="MvpDynamicValueCanvas";
            var existing=bar.Find(name);
            RectTransform container=existing as RectTransform;
            if(container==null)
            {
                var go=new GameObject(name,typeof(RectTransform),typeof(Canvas));
                go.transform.SetParent(bar,false);
                container=go.GetComponent<RectTransform>();
                container.anchorMin=Vector2.zero;
                container.anchorMax=Vector2.one;
                container.offsetMin=Vector2.zero;
                container.offsetMax=Vector2.zero;
                container.localScale=Vector3.one;
                var cv=go.GetComponent<Canvas>();
                cv.overrideSorting=false;
                cv.pixelPerfect=false;
                createdCanvases.Add(cv);
            }
            var texts=new List<TMP_Text>();
            foreach(Transform child in bar)
            {
                if(child==container)continue;
                var tmp=child.GetComponent<TMP_Text>();
                if(tmp!=null && !tmp.raycastTarget)
                    texts.Add(tmp);
            }
            foreach(var label in texts)
            {
                label.rectTransform.SetParent(container,true);
                relocatedTextCount++;
            }
            container.SetAsLastSibling();
        }

        Debug.Log("[MVP Canvas] Installed feature-group Canvas partitions: "+
            createdCanvases.Count+", relocated static-vs-dynamic TMP: "+
            relocatedTextCount+"; existing canvases preserved.",this);
    }

    private void Isolate(Transform node,bool requiresInput)
    {
        if(node==null)return;
        Canvas canvas=node.GetComponent<Canvas>();
        if(canvas==null)
        {
            canvas=node.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting=false;
            canvas.pixelPerfect=false;
            createdCanvases.Add(canvas);
        }
        if(requiresInput && node.GetComponent<GraphicRaycaster>()==null)
        {
            var raycaster=node.gameObject.AddComponent<GraphicRaycaster>();
            createdRaycasters.Add(raycaster);
        }
    }

    private void OnDestroy()
    {
        // The grouping exists only in the active runtime scene.
        // Scene destruction disposes children and components; do not
        // reshuffle authored objects during teardown.
        createdCanvases.Clear();
        createdRaycasters.Clear();
    }
}