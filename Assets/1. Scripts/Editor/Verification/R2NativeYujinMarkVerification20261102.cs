#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// R2-K native ledger proof: two Yujins applying independent marks to
/// two real BodyPart objects on a runtime-only Yujin target. No mirrored
/// mutable mark store, asset migration or persisted scene content.
/// </summary>
public static class R2NativeYujinMarkVerification20261102
{
    [MenuItem("Game System Verification/R2 - Live Yujin Mark Part Isolation (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required.");
        var manager=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var ctx=manager?.BattleContext;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/2. Data/TestEncounters/Prefabs/Yujin_Test.prefab");
        if(ctx==null||prefab==null)throw new InvalidOperationException("BattleContext and Yujin prefab required");
        int pass=0,fail=0;
        void Check(string id,bool good,string details)
        {
            if(good){pass++;Debug.Log("[R2-Mark] PASS "+id+" | "+details);}
            else{fail++;Debug.LogError("[R2-Mark] FAIL "+id+" | "+details);}
        }
        GameObject goA=null,goB=null,goTarget=null;
        try
        {
            goA=UnityEngine.Object.Instantiate(prefab);goA.name="R2_YUJIN_OWNER_A_RUNTIME_ONLY";
            goB=UnityEngine.Object.Instantiate(prefab);goB.name="R2_YUJIN_OWNER_B_RUNTIME_ONLY";
            goTarget=UnityEngine.Object.Instantiate(prefab);goTarget.name="R2_YUJIN_TARGET_RUNTIME_ONLY";
            var a=goA.GetComponent<Yujin>();var b=goB.GetComponent<Yujin>();
            var target=goTarget.GetComponent<Yujin>();
            a.Initialize(ctx);b.Initialize(ctx);target.Initialize(ctx);
            long aId=R4LiveSlotQuery.ActorRuntimeId(a);
            long bId=R4LiveSlotQuery.ActorRuntimeId(b);
            long targetId=R4LiveSlotQuery.ActorRuntimeId(target);
            var left=target.BodyParts.FirstOrDefault(p=>p?.Type==PartType.LEFT_HAND);
            var right=target.BodyParts.FirstOrDefault(p=>p?.Type==PartType.RIGHT_HAND);
            Check("M01 three runtime characters exist and carry separate actor IDs",
                a.YujinMechanic!=null&&b.YujinMechanic!=null&&
                aId>0&&bId>0&&targetId>0&&aId!=bId&&bId!=targetId&&aId!=targetId,
                "A="+aId+" B="+bId+" target="+targetId);
            Check("M02 real enemy target has both independent hand Parts",
                left!=null&&right!=null&&left!=right &&
                !string.Equals(left.PartId,right.PartId,StringComparison.Ordinal),
                "left="+left?.PartId+" right="+right?.PartId);
            if(left==null||right==null)throw new InvalidOperationException("No left/right part.");
            var source=new R2YujinMarkSnapshotSource();
            Check("M03 read bridge registers each real owner and carrier once",
                source.TryRegisterYujin(a)&&source.TryRegisterYujin(b)&&
                source.TryRegisterCarrier(target)&&!source.TryRegisterCarrier(target),
                "read-only integration into YujinMechanic");
            var def=new R2KeywordDefinition(R2YujinMarkSnapshotSource.StableKeywordId,
                "yujin","표식",R2KeywordStorageKind.PartStackLedger,R2KeywordCarrier.Part);
            var features=new R2CharacterFeatureSet("yujin",new[]{def});
            var registry=new R2CharacterFeatureRegistry(source,
                id=>id==aId || id==bId ? "yujin" : null);
            Check("M04 feature set shared, actor binding independent",
                registry.TryBind(aId,features)&&registry.TryBind(bId,features) &&
                registry.BoundActorCount==2,
                "no per-SO mutable counters");

            // Direct writes ONLY to the existing native Yujin mechanic, using
            // its public canonical ApplyMarkGain path, below the 44 threshold.
            var gainLeft=a.YujinMechanic.ApplyMarkGain(target,left,43);
            var gainRight=a.YujinMechanic.ApplyMarkGain(target,right,10);
            var gainOther=b.YujinMechanic.ApplyMarkGain(target,left,7);
            int directLeft=a.YujinMechanic.GetMark(target,left);
            int directRight=a.YujinMechanic.GetMark(target,right);
            int otherLeft=b.YujinMechanic.GetMark(target,left);
            Check("M05 actual Yujin mechanic stores 43/10 per part",
                directLeft==43&&directRight==10,
                "live left="+directLeft+" right="+directRight);
            Check("M06 second Yujin same ContentId stores 7 independently",
                otherLeft==7 && directLeft==43,
                "A left="+directLeft+" B left="+otherLeft);
            var qLeft=new R2KeywordReadRequest(aId,targetId,def.KeywordId,left.PartId);
            var qRight=new R2KeywordReadRequest(aId,targetId,def.KeywordId,right.PartId);
            var qOther=new R2KeywordReadRequest(bId,targetId,def.KeywordId,left.PartId);
            Check("M07 R2-K reads actual native stored left 43",
                registry.TryRead(qLeft,out int fromLeft)&&fromLeft==directLeft,
                "R2="+fromLeft+" native="+directLeft);
            Check("M08 R2-K reads actual native stored right 10",
                registry.TryRead(qRight,out int fromRight)&&fromRight==directRight,
                "R2="+fromRight+" native="+directRight);
            Check("M09 second owner reads own mark, not first owner's",
                registry.TryRead(qOther,out int fromOther)&&fromOther==7,
                "R2B="+fromOther+" nativeB="+otherLeft);
            Check("M10 foreign keyword/invalid part and unregistered source rejected",
                !registry.TryRead(new R2KeywordReadRequest(aId,targetId,"olaf.bleed",left.PartId),out _) &&
                !registry.TryRead(new R2KeywordReadRequest(aId,targetId,def.KeywordId,"NO_SUCH_PART"),out _) &&
                !registry.TryRead(new R2KeywordReadRequest(999999,targetId,def.KeywordId,left.PartId),out _),
                "no false zero or cross-owner leakage");
            registry.ClearForBattleEnd();source.ClearForBattleEnd();
            Check("M11 lifecycle clearing does not mutate Yujin's actual marks",
                a.YujinMechanic.GetMark(target,left)==43 &&
                a.YujinMechanic.GetMark(target,right)==10 &&
                !registry.TryRead(qLeft,out _),
                "native backing not copied/cleared");
        }
        finally
        {
            if(goA!=null)UnityEngine.Object.Destroy(goA);
            if(goB!=null)UnityEngine.Object.Destroy(goB);
            if(goTarget!=null)UnityEngine.Object.Destroy(goTarget);
        }
        Debug.Log("[R2-Mark] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R2 native mark isolation failed="+fail);
    }
}
#endif
