#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// R7-11 EditMode verification of 60 original item sources, native identity
/// manifests, economy & no-TEMP combat injection. Missing canonical values
/// and the common-tier 6/6/6/6/6 mismatch remain explicitly PENDING.
/// </summary>
public static class RunItemMigrationVerification
{
    public static void Run()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)
            throw new InvalidOperationException("R7-11 requires idle EditMode");
        int passed=0;
        void Check(string label,bool condition)
        {
            if(!condition)throw new InvalidOperationException("[R7-11] FAIL "+label);
            passed++;
        }

        var original=AssetDatabase.LoadAssetAtPath<RunItemCatalog>(
            "Assets/2. Data/Progression/RunItems/RunItemCatalog.asset");
        Check("source catalog exists",original!=null);
        Check("60 run item definitions",original.Items?.Count==60);
        Check("original catalog unchanged",!EditorUtility.IsDirty(original));
        Check("no null item definitions",original.Items.All(x=>x!=null));
        Check("unique 60 stable IDs",
            original.Items.Select(x=>x.ItemId).Distinct().Count()==60);
        Check("unique 60 original item GUIDs",
            original.Items.Select(x=>AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(x))).Distinct().Count()==60);
        Check("60 legacy links are still TEMP, but no longer executed",
            original.Items.All(x=>x.CombatItem is ProvisionalBalanceRunItem));
        Check("60 unique original TEMP combat item GUIDs",
            original.Items.Select(x=>AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(x.CombatItem))).Distinct().Count()==60);
        Check("30 common and 30 class",
            original.Items.Count(x=>x.Tier!=RunItemTier.Class)==30&&
            original.Items.Count(x=>x.Tier==RunItemTier.Class)==30);
        Check("10 class each for Olaf, Yujin, Hifumi",
            new[]{"Olaf","Yujin","Hifumi"}.All(key=>
                original.Items.Count(x=>x.Tier==RunItemTier.Class&&
                    x.CharacterKey==key)==10));
        Check("original temporary common tiers are 6/6/6/6/6 NOT canonical",
            Enumerable.Range(1,5).All(i=>
                original.Items.Count(x=>x.Tier==(RunItemTier)i)==6));

        NativeRunItemCatalog.ResetCache();
        var native=NativeRunItemCatalog.Registry;
        Check("60 source-matched native graph records",native.Count==60);
        Check("60 item effects are explicitly pending canonical design",
            native.Values.All(x=>
                x.State==NativeRunItemState.CanonicalEffectPending));
        Check("30 common tier mappings explicitly pending",
            native.Values.Count(x=>x.CanonicalTierMappingPending)==30);
        Check("zero unapproved executable native effects",
            NativeRunItemCatalog.CountApprovedCombatItems(
                original.Items)==0);

        foreach(var source in original.Items)
        {
            Check("stable-ID source match "+source.ItemId,
                NativeRunItemCatalog.TryResolve(source,out _));
            var graph=native[source.ItemId];
            var a=AssetDatabase.GetAssetPath(source);
            var b=AssetDatabase.GetAssetPath(source.CombatItem);
            var c=AssetDatabase.GetAssetPath(graph);
            Check("original GUID and old TEMP proxy GUID retained "+
                source.ItemId,graph.IsValid&&
                graph.SourceGuid==AssetDatabase.AssetPathToGUID(a)&&
                graph.FormerCombatProxyGuid==AssetDatabase.AssetPathToGUID(b)&&
                graph.SourceTier==source.Tier&&
                graph.SourceDisplayName==source.DisplayName&&
                graph.CharacterKey==(source.CharacterKey??string.Empty));
            Check("all source and graph .meta exist and source SOs clean "+
                source.ItemId,
                File.Exists(a+".meta")&&File.Exists(b+".meta")&&
                File.Exists(c+".meta")&&
                !EditorUtility.IsDirty(source)&&
                !EditorUtility.IsDirty(source.CombatItem)&&
                !EditorUtility.IsDirty(graph));
            Check("no false combat effect from former TEMP source "+
                source.ItemId,
                graph.ResolveApprovedCombatItem()==null&&
                NativeRunItemCatalog.
                    ResolveExecutableCombatItem(source)==null&&
                !string.IsNullOrWhiteSpace(graph.PendingReason));
        }

        var live=RunFlowLiveContentRegistry.Load();
        Check("live content registry available",live!=null);
        Check("live registry still references original 60",
            live.CountValidRunItems()==60);
        Check("old serialized combat links retained for compatibility",
            live.CountCombatLinkedRunItems()==60);
        Check("native runnable item count is zero",
            live.CountNativeReadyRunItems()==0);
        Check("native pending count is explicitly 60",
            live.CountNativePendingRunItems()==60);

        var economy=new ItemEconomySettings();
        var tiers=new[]{RunItemTier.Tier1,RunItemTier.Tier2,
            RunItemTier.Tier3,RunItemTier.Tier4,RunItemTier.Tier5,
            RunItemTier.Class};
        var prices=new[]{400,550,700,850,1000,900};
        var sales=new[]{132,182,231,280,330,297};
        for(int i=0;i<6;i++)
        {
            var sample=original.Items.First(x=>x.Tier==tiers[i]);
            Check("canonical purchase price "+tiers[i],
                economy.GetPrice(tiers[i])==prices[i]);
            Check("canonical sell price "+tiers[i],
                economy.GetSellPrice(sample)==sales[i]);
        }
        Check("weighted offer tier masses 40/25/15/8/4/8",
            Mathf.Approximately(economy.Tier1Weight,.40f)&&
            Mathf.Approximately(economy.Tier2Weight,.25f)&&
            Mathf.Approximately(economy.Tier3Weight,.15f)&&
            Mathf.Approximately(economy.Tier4Weight,.08f)&&
            Mathf.Approximately(economy.Tier5Weight,.04f)&&
            Mathf.Approximately(economy.ClassWeight,.08f));
        Check("item tier weights sum to one",
            Mathf.Approximately(economy.Tier1Weight+
                economy.Tier2Weight+economy.Tier3Weight+
                economy.Tier4Weight+economy.Tier5Weight+
                economy.ClassWeight,1f));
        Check("5-to-6 shelf count",
            economy.ShopSlotMinimum==5&&economy.ShopSlotMaximum==6&&
            Enumerable.Range(0,30).All(_=>
            {
                int count=economy.RollShopSlotCount();
                return count==5||count==6;
            }));
        var offer=new RunItemOfferService(original,economy);
        Check("shop reroll costs scale 1.5 each time",
            Mathf.Approximately(offer.GetRerollCost(100,0),100)&&
            Mathf.Approximately(offer.GetRerollCost(100,1),150)&&
            Mathf.Approximately(offer.GetRerollCost(100,2),225));

        var inventory=new RunInventory();
        var cls=original.Items.First(x=>x.Tier==RunItemTier.Class&&
            x.CharacterKey=="Olaf");
        Check("first class acquisition succeeds",inventory.Acquire(cls));
        Check("class item sale succeeds",inventory.Sell(cls));
        Check("class acquired-history survives sale",
            inventory.HasEverAcquired(cls)&&inventory.Owned.Count==0);
        Check("sold class cannot be acquired again",
            !inventory.Acquire(cls));
        var boss=offer.CreateOffer(
            RunItemOfferSource.BossClassChoice,inventory,"Olaf",3);
        Check("boss has exactly three distinct unsold class choices",
            boss.Count==3&&boss.All(x=>x.Tier==RunItemTier.Class&&
                x.CharacterKey=="Olaf"&&x!=cls)&&
            boss.Select(x=>x.ItemId).Distinct().Count()==3);
        var another=offer.CreateOffer(
            RunItemOfferSource.BossClassChoice,inventory,"Hifumi",3);
        Check("other-character class items never in current boss choice",
            another.Count==3&&another.All(x=>x.CharacterKey=="Hifumi"));
        var elite=offer.CreateOffer(
            RunItemOfferSource.EliteRandom,inventory,"Olaf",1);
        Check("elite drops one eligible item, never sold class",
            elite.Count==1&&elite[0]!=cls);
        var shop=offer.CreateOffer(
            RunItemOfferSource.Shop,inventory,"Olaf",6);
        Check("shop presents distinct eligible six-slot offer",
            shop.Count==6&&shop.All(x=>x!=cls)&&
            shop.Select(x=>x.ItemId).Distinct().Count()==6);
        var common=original.Items.First(x=>x.Tier!=RunItemTier.Class);
        Check("inventory has no general 10-item limit",
            Enumerable.Range(0,15).All(_=>inventory.Acquire(common))&&
            inventory.Owned.Count==15);
        inventory.Clear();
        Check("new run clears owned and ever-acquired history",
            inventory.Owned.Count==0&&!inventory.HasEverAcquired(cls));

        var fake=ScriptableObject.CreateInstance<RunItemDefinition>();
        try
        {
            fake.ItemId=cls.ItemId;
            fake.DisplayName=cls.DisplayName;
            fake.CharacterKey=cls.CharacterKey;
            fake.Tier=cls.Tier;
            fake.name="FORGED";
            Check("forged source name fails native lookup",
                !NativeRunItemCatalog.TryResolve(fake,out _));
            bool threw=false;
            try{NativeRunItemCatalog.
                ResolveExecutableCombatItem(fake);}
            catch(InvalidOperationException){threw=true;}
            Check("known source ID spoof cannot execute TEMP proxy",threw);
            fake.ItemId="unpublished.transient.item";
            Check("unregistered test fixture cannot inject combat effect",
                NativeRunItemCatalog.
                    ResolveExecutableCombatItem(fake)==null);
        }
        finally{UnityEngine.Object.DestroyImmediate(fake);}

        var scripts=Directory.GetFiles(Application.dataPath,"*.cs",
            SearchOption.AllDirectories);
        string Script(string name)=>File.ReadAllText(
            scripts.Single(x=>Path.GetFileName(x)==name));
        string bridge=Script("RunFlowLiveBattleBridge.cs");
        string econ=Script("RunItemSystem.cs");
        string overlay=Script("RunFlowLiveIntegrationOverlay.cs");
        Check("bridge does not inject raw old CombatItem from RunInventory",
            bridge.Contains("NativeRunItemCatalog."+
                "ResolveExecutableCombatItem(")&&
            !bridge.Contains("owned[i]?.CombatItem"));
        Check("bridge filters preexisting TEMP from active build",
            bridge.Contains("original is not ProvisionalBalanceRunItem"));
        Check("bridge retains original source prefab rollback path",
            bridge.Contains("RestoreSourcePrefab()")&&
            bridge.Contains("originalItems,"));
        Check("UI explicitly reports 60 pending and zero ready",
            overlay.Contains("CountNativeReadyRunItems()")&&
            overlay.Contains("CountNativePendingRunItems()"));
        Check("sales use exact canonical integer rounding",
            econ.Contains("Mathf.RoundToInt(GetPrice(item.Tier) * SellRate)"));
        Check("class acquired-history prevents re-acquisition",
            econ.Contains("item.Tier==RunItemTier.Class&&HasEverAcquired(item)"));
        Check("active Battle Test Scene left Dirty and unsaved",
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==
                "Battle Test Scene"&&
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty);
        Debug.Log("[R7-11] Native Run Item 60 contract gate PASS="+
            passed+" FAIL=0; 60 identity-bound pending graphs, "+
            "0 approved combat effects; 30 common source tier mapping "+
            "6/6/6/6/6 vs canonical 10/8/6/4/2 PENDING. "+
            "NO_R9 NO_PLAYMODE NO_SCENE_SAVE");
    }
}
#endif