#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// R7-11 final scoped EditMode integration of ACTUAL catalog shop/boss
/// transactions with native-pending UI disclosure. Does not simulate
/// completed item gameplay nor rewrite original run-item authoring.
/// </summary>
public static class R7Stage11LiveEconomyClosure20261103
{
    public static void Run()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)
            throw new InvalidOperationException("Requires idle EditMode");

        int passed=0;
        void Check(string name,bool good)
        {
            if(!good)
                throw new InvalidOperationException("[R7-11 Economy] FAIL "+name);
            passed++;
        }

        var source=AssetDatabase.LoadAssetAtPath<RunItemCatalog>(
            "Assets/2. Data/Progression/RunItems/RunItemCatalog.asset");
        Check("01 original 60 source catalog loaded",source?.Items?.Count==60);
        var settings=new ItemEconomySettings();
        var offer=new RunItemOfferService(source,settings);
        var transactions=new RunEconomyService(new RunEconomySettings());
        var state=new RunProgressionState();
        state.Reset(4000,1);
        var classItem=source.Items.First(x=>x.Tier==RunItemTier.Class&&
            x.CharacterKey=="Olaf");
        var common=source.Items.First(x=>x.Tier==RunItemTier.Tier1);
        Check("02 run starts with exact test gold 4000",state.Gold==4000);
        Check("03 approved source lookup is still pending",
            R7NativeRunItemCatalog20261103.TryResolve(classItem,out var cg)&&
            cg.State==R7NativeRunItemState20261103.CanonicalEffectPending&&
            R7NativeRunItemCatalog20261103.
                ResolveExecutableCombatItem(classItem)==null);
        Check("04 ordinary purchase uses real economy path",
            transactions.TryBuyItem(state,classItem,settings));
        Check("05 class price 900 paid once",
            state.Gold==3100&&state.Inventory.Owned.Count==1&&
            state.Inventory.HasEverAcquired(classItem));
        Check("06 real shop sale returns precisely 297",
            transactions.TrySellItem(state,classItem,settings));
        Check("07 sale restores gold, not acquisition history",
            state.Gold==3397&&state.Inventory.Owned.Count==0&&
            state.Inventory.HasEverAcquired(classItem));
        Check("08 cannot rebuy sold unique class item",
            !transactions.TryBuyItem(state,classItem,settings));
        Check("09 failed class repurchase refunds price exactly",
            state.Gold==3397&&state.Inventory.Owned.Count==0);
        Check("10 sold class never appears in actual boss three-choice",
            !offer.CreateOffer(RunItemOfferSource.BossClassChoice,
                state.Inventory,"Olaf",3).Contains(classItem));
        var boss=offer.CreateOffer(
            RunItemOfferSource.BossClassChoice,state.Inventory,"Olaf",3);
        Check("11 boss three eligible distinct owned-history-filtered cards",
            boss.Count==3&&boss.All(x=>x.Tier==RunItemTier.Class&&
                x.CharacterKey=="Olaf"&&x!=classItem)&&
            boss.Select(x=>x.ItemId).Distinct().Count()==3);
        var shop=offer.CreateOffer(RunItemOfferSource.Shop,
            state.Inventory,"Olaf",6);
        Check("12 actual shop contains six source-catalog SOs",
            shop.Count==6&&shop.All(x=>source.Items.Contains(x)));
        Check("13 shop never offers wrong character class or sold class",
            shop.All(x=>x.Tier!=RunItemTier.Class||
                x.CharacterKey=="Olaf")&&!shop.Contains(classItem));
        Check("14 real shop has no duplicate slots",
            shop.Select(x=>x.ItemId).Distinct().Count()==6);
        Check("15 original 60 shop items still have no approved effect",
            shop.All(x=>R7NativeRunItemCatalog20261103.
                ResolveExecutableCombatItem(x)==null));
        Check("16 multiple common items can be held",
            transactions.TryBuyItem(state,common,settings)&&
            transactions.TryBuyItem(state,common,settings)&&
            state.Inventory.Owned.Count==2);
        int paid=0;
        int oldGold=state.Gold;
        Check("17 first reroll pays 100G",
            transactions.TryPayGoldShopReroll(state,100,0,
                out paid,settings)&&paid==100&&state.Gold==oldGold-100);
        oldGold=state.Gold;
        Check("18 second reroll pays 150G",
            transactions.TryPayGoldShopReroll(state,100,1,
                out paid,settings)&&paid==150&&state.Gold==oldGold-150);
        oldGold=state.Gold;
        Check("19 third reroll pays 225G",
            transactions.TryPayGoldShopReroll(state,100,2,
                out paid,settings)&&paid==225&&state.Gold==oldGold-225);
        oldGold=state.Gold;
        Check("20 nonexistent sale does not grant gold",
            !transactions.TrySellItem(state,classItem,settings)&&
            state.Gold==oldGold);
        state.Reset(5000,1);
        Check("21 reset clears purchased and sold class history",
            state.Inventory.Owned.Count==0&&
            !state.Inventory.HasEverAcquired(classItem));
        Check("22 class item available again only in NEW run",
            transactions.TryBuyItem(state,classItem,settings)&&
            state.Gold==4100);
        Check("23 no run-inventory hard cap on common source entries",
            Enumerable.Range(0,18).All(_=>
                state.Inventory.Acquire(common))&&
            state.Inventory.Owned.Count==19);
        Check("24 exact stage 1/2 boss preference excludes other characters",
            offer.CreateOffer(RunItemOfferSource.BossClassChoice,
                state.Inventory,"Hifumi",3)
            .All(x=>x.CharacterKey=="Hifumi"&&
                x.Tier==RunItemTier.Class));

        // The live UI must disclose that all 60 carried source cards are
        // currently deliberately non-executable, never TEMP_BALANCE.
        var label=typeof(RunFlowTestHarness).GetMethod(
            "NativeItemPendingLabel",BindingFlags.NonPublic|BindingFlags.Static);
        Check("25 per-card pending label helper installed",label!=null);
        string status=(string)label.Invoke(null,new object[]{classItem});
        Check("26 native pending item explicitly described with no combat bonus",
            status.Contains("효과 미정")&&status.Contains("전투 보너스 없음"));
        var transient=ScriptableObject.CreateInstance<RunItemDefinition>();
        try
        {
            transient.ItemId="unregistered.runflow.mock";
            transient.name="Transient RunFlow Mock";
            string result=(string)label.Invoke(null,new object[]{transient});
            Check("27 transient test item never misrepresented as native",
                string.IsNullOrEmpty(result));
        }
        finally{UnityEngine.Object.DestroyImmediate(transient);}

        var originalCatalog=RunFlowLiveContentRegistry.Load();
        Check("28 live registry 60 identity-bound pending, zero executable",
            originalCatalog!=null&&
            originalCatalog.CountNativePendingRunItems()==60&&
            originalCatalog.CountNativeReadyRunItems()==0);
        Check("29 source item assets not marked dirty",
            source.Items.All(x=>x!=null&&!EditorUtility.IsDirty(x)));
        Check("30 active Battle Test Scene is still unsaved",
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==
                "Battle Test Scene"&&
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty);

        Debug.Log("[R7-11 Economy] Real 60-source catalog shop/boss/inventory " +
            "transaction and per-card pending UI PASS="+passed+
            " FAIL=0; item effect values all (미정); NO_R9 NO_PLAYMODE");
    }
}
#endif