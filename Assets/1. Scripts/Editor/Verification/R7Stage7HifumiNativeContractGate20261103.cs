#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// R7-7 targeted EditMode-only Hifumi canonical contract check.
/// No PlayMode, scene save, R9, scene reload or combat simulation.
/// </summary>
public static class R7Stage7HifumiNativeContractGate20261103
{
    public static void Run()
    {
        if(EditorApplication.isPlaying)
            throw new InvalidOperationException("R7-7 runs in EditMode only.");
        int pass=0;
        void Check(bool ok,string label)
        {
            if(!ok)throw new InvalidOperationException("R7-7 FAIL "+label);
            pass++;
        }

        var all=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(p=>new{Path=p,Source=AssetDatabase.LoadAssetAtPath<SkillDefinition>(p)})
            .Where(x=>x.Source!=null&&
                x.Source.SkillId.StartsWith("hifumi.",StringComparison.Ordinal))
            .ToArray();
        Check(all.Length==20,"20 Hifumi SkillDefinition assets");
        Check(all.All(x=>x.Source.EffectEntries?.Count==0),
            "20 Hifumi skill effects are event-owned, not legacy EffectEntries");
        Check(all.All(x=>!EditorUtility.IsDirty(x.Source)&&
            !string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(x.Path))),
            "original source asset GUIDs are retained");

        Check(R7HifumiNativeRules20261103.BoneFromReceivedHit(20,8)==28,
            "BloodPrice 8 augments actual 20 HP received into 28 bone");
        Check(R7HifumiNativeRules20261103.BoneFromReceivedHit(0,8)==0,
            "0 HP damage does not trigger BloodPrice hit accrual");
        Check(R7HifumiNativeRules20261103.BoneFromReceivedHit(20,-2)==20,
            "negative BloodPrice does not reduce actual hit accrual");
        Check(R7HifumiNativeRules20261103.CatastropheDirectBone==60,
            "catastrophe directly grants 60 Bone");
        Check(R7HifumiNativeRules20261103.SpendableBone(250,300)==550,
            "Advance adds to spendable, not capped at 500");
        Check(R7HifumiNativeRules20261103.GamblingPowerFromBoneBurned(500)==30,
            "prestige gambler consumes total 500 to gain 30 turn power");
        Check(R7HifumiNativeRules20261103.GamblingPowerFromBoneBurned(199)==6,
            "prestige gambler burns in full 100-Bone steps");
        Check(R7HifumiNativeRules20261103.GoldanBreakMode(false)==
            PartBreakMode.WeakenedOnly,"normal Goldan requires an already weakened part");
        Check(R7HifumiNativeRules20261103.GoldanBreakMode(true)==
            PartBreakMode.IgnoreWeakenedPrerequisite,"bloom Goldan grants execution");

        var mechanic=new HifumiMechanic();
        mechanic.SetBoneForVerification(250);
        mechanic.SetAdvanceBoneForVerification(300);
        Check(mechanic.BoneBand==2 && mechanic.SpendableBone==550 &&
              mechanic.IsBloom,"real BoneBand is separate from Advance/Bloom");
        Check(mechanic.ConsumeBone(310)&&mechanic.Bone==240&&
              mechanic.AdvanceBone==0,"payment first uses Advance 300 then real 10");
        mechanic.SetBoneForVerification(310);
        mechanic.SetAdvanceBoneForVerification(220);
        mechanic.ExecuteSkillForVerification(HifumiSkillIds.GamblerMove);
        Check(mechanic.Bone==0 && mechanic.AdvanceBone==0,
            "Gambler prestige actually burns both payment resources");

        mechanic.SetBoneForVerification(300);
        mechanic.SetAdvanceBoneForVerification(150);
        mechanic.ResolveAllInForVerification(ChinchiroCombination.Moku,
            ChinchiroCombination.Blank,ChinchiroCombination.Blank);
        Check(mechanic.Bone==500&&mechanic.AdvanceBone==0,
            "All-In Moku repays wagered actual+Advance plus 100, with cap 500");
        mechanic.SetBoneForVerification(250);
        mechanic.SetAdvanceBoneForVerification(200);
        mechanic.ResolveAllInForVerification(ChinchiroCombination.Arashi,
            ChinchiroCombination.Shigoro,ChinchiroCombination.Moku);
        Check(mechanic.Bone==500&&mechanic.AdvanceBone==0,
            "All-In jackpot returns 500 permanent Bone");
        mechanic.SetBoneForVerification(250);
        mechanic.SetAdvanceBoneForVerification(200);
        mechanic.ResolveAllInForVerification(ChinchiroCombination.Arashi,
            ChinchiroCombination.Hifumi,ChinchiroCombination.Moku);
        Check(mechanic.Bone==0&&mechanic.AdvanceBone==0,
            "All-In catastrophe overrides any jackpot");
        mechanic.GrantBloodPriceForTurn(4);
        mechanic.GrantBloodPriceForTurn(2);
        Check(mechanic.BloodPriceThisTurn==6&&mechanic.Bone==0,
            "BloodPrice keyword does not directly grant Bone");
        mechanic.QueueBloodPriceNextTurn(3);
        Check(mechanic.BloodPriceThisTurn==6,
            "next-turn BloodPrice does not alter current-turn value");
        Check(HifumiSkillIds.IsCounterSkill(HifumiSkillIds.Yukcham)&&
            HifumiSkillIds.IsCounterSkill(HifumiSkillIds.Goldan)&&
            HifumiSkillIds.IsCounterSkill(HifumiSkillIds.RollCompendium)&&
            HifumiSkillIds.IsCounterSkill(HifumiSkillIds.SmallChange)&&
            !HifumiSkillIds.IsCounterSkill(HifumiSkillIds.RecklessBet),
            "Hifumi canonical counter tag authoring");

        // A read-only structural audit of the exact implemented boundary;
        // dynamic damage event ordering is intentionally not claimed verified.
        var mech=Directory.GetFiles(Application.dataPath,"HifumiMechanic.cs",
            SearchOption.AllDirectories).Single();
        var code=File.ReadAllText(mech);
        Check(code.Contains("catastropheSelfDamageDepth>0 && context.DamageType==DamageType.SelfCost") &&
              code.Contains("AddBone(R7HifumiNativeRules20261103.CatastropheDirectBone)") &&
              code.Contains("finally") &&
              code.Contains("catastropheSelfDamageDepth--;"),
            "catastrophe self-injury is separate from direct +60 Bone");
        Check(code.Contains("bool canGoldanBreak = isGoldan;") &&
              code.Contains("GoldanBreakMode(bloomSnapshot)") &&
              !code.Contains("bloomBreakAttempted"),
            "every Goldan counter hit has appropriate break permission");
        Check(code.Contains("int wageredBone = SpendableBone;") &&
              code.Contains("ConsumeAllBonePaymentResources();"),
            "prestige gamble uses combined payment pool, not real Bone alone");

        string root=Directory.GetParent(Application.dataPath).FullName;
        string folder=Path.Combine(root,"ProjectAbyss_R7_7_20261103");
        Directory.CreateDirectory(folder);
        string Q(object v)=>string.Concat((char)34,
            (v?.ToString()??"").Replace("\"","\"\""),(char)34);
        var rows=all.OrderBy(x=>x.Source.SkillId)
            .Select(x=>new[]{x.Source.SkillId,x.Source.name,
                AssetDatabase.AssetPathToGUID(x.Path),x.Path,
                (x.Source.EffectEntries?.Count??-1).ToString(),
                "HifumiMechanic / BattleEvent / R6DeferredReaction"})
            .Prepend(new[]{"SkillId","SkillName","OriginalGUID",
                "OriginalAssetPath","EffectEntryCount","RuntimeOwner"});
        File.WriteAllLines(Path.Combine(folder,
            "R7_7_20_HIFUMI_EVENT_OWNED_SKILLS.csv"),
            rows.Select(row=>string.Join(",",row.Select(Q))),
            new UTF8Encoding(false));
        Debug.Log("[R7-7] EditMode native Hifumi PASS="+pass+
            " FAIL=0 HIFUMI=20 / NO_R9 NO_PLAYMODE");
    }
}
#endif