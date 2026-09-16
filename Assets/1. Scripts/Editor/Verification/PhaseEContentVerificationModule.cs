#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class PhaseEContentVerificationModule : IGameSystemVerificationModule
{
    public string ModuleId => "phase_e.content_data";
    public int Order => 900;

    public IEnumerable<GameSystemVerificationCase> BuildCases()
    {
        yield return Static("phasee.c31.normal_enemy", "C-31", "일반 몹 기준 데이터", GameSystemVerificationCategory.Data,
            "Stage1~3의 3/4마리 조우, HP135/101, Stagger100, 1슬롯×3굴림, HP 내성 타입 수 구성", VerifyC31);
        yield return Static("phasee.c32.emotions", "C-32", "감정 증강 63장", GameSystemVerificationCategory.Emotion,
            "7감정×3티어×3장=63. 미정값은 Unset, P2 완료는 Runtime 연결까지", VerifyC32);
        yield return Static("phasee.c33.items", "C-33", "Run Item 카탈로그", GameSystemVerificationCategory.Economy,
            "공용30 + 직업10×3, 티어/가격/확률/판매/5~6매대/리롤1.5", VerifyC33);
        yield return Static("phasee.c38.upgrades", "C-38", "만렙 XLSX 강화 역산", GameSystemVerificationCategory.Upgrade,
            "기본/강화1/강화2로 분해하고 강화2 결과가 XLSX 만렙값과 동일", VerifyC38);
        yield return Static("phasee.c40.boss", "C-40", "1스테이지 기준 보스", GameSystemVerificationCategory.Data,
            "A=3굴림 위력13, B=2굴림 위력14, A/A/B, Energy2, B 평타 fallback, break=false, random target", VerifyC40);
        yield return Static("phasee.c44.roll_texture", "C-44", "위치별 굴림 텍스처 Validator", GameSystemVerificationCategory.Contract,
            "위치별 평균의 평균 = 기본위력+4.5 위반을 audit에서 검출", VerifyC44);
        yield return Static("phasee.o02.olaf_pool", "O-02", "올라프 전체 스킬 Core Data", GameSystemVerificationCategory.Data,
            "최신 0916 풀 + 기본/강화1/강화2 데이터 및 Runtime 연결", VerifyO02);
        yield return Static("phasee.y06.yujin_pool", "Y-06", "유진 전체 스킬 Core Data", GameSystemVerificationCategory.Data,
            "최신 0916 평타9/결투14/도사림/위세3 + Runtime 연결", VerifyY06);
        yield return Static("phasee.h08.poker_face", "H-08", "히후미 포커페이스 데이터", GameSystemVerificationCategory.Data,
            "강한 도사림/빛1, 받는 피해 교환당 -4 · 피격당 뼈+4", VerifyH08);
    }

    private static GameSystemVerificationCase Static(string id, string req, string name,
        GameSystemVerificationCategory category, string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> probe) =>
        new(id, req, name, category, GameSystemVerificationExecutionMode.StaticContract, expected, probe, true);

    private static PhaseEContentManifest Manifest() =>
        AssetDatabase.LoadAssetAtPath<PhaseEContentManifest>(PhaseEContentMigration.ManifestPath);

    private static GameSystemVerificationProbeResult VerifyC31(GameSystemVerificationContext _)
    {
        PhaseEEnemyContentCatalog c = AssetDatabase.LoadAssetAtPath<PhaseEEnemyContentCatalog>(PhaseEContentMigration.EnemyCatalogPath);
        if (c?.NormalEncounters == null) return GameSystemVerificationProbeResult.Fail("PhaseEEnemyContentCatalog missing");
        bool ok = c.NormalEncounters.Count == 6;
        for (int stage=1; stage<=3; stage++)
        for (int count=3; count<=4; count++)
        {
            var p = c.NormalEncounters.FirstOrDefault(x=>x!=null && x.Stage==stage && x.EnemyCount==count);
            ok &= p != null && p.HpPerEnemy == (count==3?135:101) && p.StaggerMax==100 && p.ActionSlotsPerEnemy==1 && p.RollsPerAction==3;
            if (p != null)
            {
                int w=stage<=2?1:0, n=stage==1?2:(stage==2?1:2), r=stage==1?0:1;
                ok &= p.WeakTypeCount==w && p.NeutralTypeCount==n && p.ResistantTypeCount==r;
            }
        }
        return ok ? GameSystemVerificationProbeResult.Pass("6 encounter profiles canonical") : GameSystemVerificationProbeResult.Fail("normal encounter profile mismatch");
    }

    private static GameSystemVerificationProbeResult VerifyC32(GameSystemVerificationContext _)
    {
        var c=AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(PhaseEContentMigration.EmotionCatalogPath);
        if (c?.Entries == null) return GameSystemVerificationProbeResult.Fail("EmotionAugmentCatalog missing");
        int total=c.Entries.Count(x=>x!=null), connected=c.Entries.Count(x=>x!=null && x.RuntimeReadiness==EmotionAugmentRuntimeReadiness.RuntimeConnected);
        int dataOnly=c.Entries.Count(x=>x!=null && x.RuntimeReadiness==EmotionAugmentRuntimeReadiness.DataOnlyPendingRuntime);
        int designPending=c.Entries.Count(x=>x!=null && x.RuntimeReadiness==EmotionAugmentRuntimeReadiness.DesignValuePending);
        bool shape=total==63;
        foreach(EmotionType e in Enum.GetValues(typeof(EmotionType)))
            for(int tier=1;tier<=3;tier++) shape &= c.Entries.Count(x=>x!=null && x.Emotion==e && x.Tier==tier)==3;
        if(!shape) return GameSystemVerificationProbeResult.Fail($"total={total}, tier-shape invalid");
        return dataOnly==0 && designPending==0
            ? GameSystemVerificationProbeResult.Pass($"63/63 runtime connected")
            : GameSystemVerificationProbeResult.Pending($"63 definitions; RuntimeConnected={connected}, DataOnly={dataOnly}, DesignPending={designPending}", "정본에 없는 값/공통 runtime hook을 임의로 만들지 않아 P2는 아직 닫히지 않습니다.");
    }

    private static GameSystemVerificationProbeResult VerifyC33(GameSystemVerificationContext _)
    {
        var c=AssetDatabase.LoadAssetAtPath<RunItemCatalog>(PhaseEContentMigration.RunItemCatalogPath);
        if(c?.Items==null) return GameSystemVerificationProbeResult.Fail("RunItemCatalog missing");
        var econ=new ItemEconomySettings();
        bool economy=Mathf.Approximately(econ.Tier1Weight,.40f)&&Mathf.Approximately(econ.Tier2Weight,.25f)&&Mathf.Approximately(econ.Tier3Weight,.15f)&&Mathf.Approximately(econ.Tier4Weight,.08f)&&Mathf.Approximately(econ.Tier5Weight,.04f)&&Mathf.Approximately(econ.ClassWeight,.08f)
            && econ.Tier1Price==400&&econ.Tier2Price==550&&econ.Tier3Price==700&&econ.Tier4Price==850&&econ.Tier5Price==1000&&econ.ClassPrice==900&&Mathf.Approximately(econ.SellRate,.33f)&&econ.ShopSlotMinimum==5&&econ.ShopSlotMaximum==6&&Mathf.Approximately(econ.RerollCostMultiplier,1.5f);
        if(!economy) return GameSystemVerificationProbeResult.Fail("Item economy constants mismatch");
        int common=c.Items.Count(x=>x!=null && x.Tier!=RunItemTier.Class), cls=c.Items.Count(x=>x!=null && x.Tier==RunItemTier.Class);
        bool complete=common==30 && cls==30;
        return complete ? GameSystemVerificationProbeResult.Pass("60 item definitions + canonical economy") : GameSystemVerificationProbeResult.Pending($"economy PASS; actual item definitions common={common}/30 class={cls}/30", "0916 source에는 60개 실제 아이템 명칭/효과 목록이 없습니다.");
    }

    private static GameSystemVerificationProbeResult VerifyC38(GameSystemVerificationContext _)
    {
        var m=Manifest();
        return m?.UpgradeDecompositionComplete==true ? GameSystemVerificationProbeResult.Pass("upgrade decomposition marked complete") : GameSystemVerificationProbeResult.Pending("Base/Upgrade1 decomposition unset", "0916 XLSX는 강화2 완료 만렙값만 제공하며 역산 규칙이 미정입니다.");
    }

    private static GameSystemVerificationProbeResult VerifyC40(GameSystemVerificationContext _)
    {
        var c=AssetDatabase.LoadAssetAtPath<PhaseEEnemyContentCatalog>(PhaseEContentMigration.EnemyCatalogPath);
        if(c==null) return GameSystemVerificationProbeResult.Fail("enemy catalog missing");
        bool ok=c.BossPartCount==5&&c.BossPartHp==150&&c.BossWholeHp==1500&&c.BossStaggerMax==400&&c.BossStartingEnergy==2&&c.NormalPostureSequence=="A,A,B"&&c.RandomTarget&&c.FallbackBToNormalWhenEnergyInsufficient
            && c.SkillA!=null&&c.SkillA.RollCount==3&&c.SkillA.BasePower==13&&c.SkillA.Duel&&!c.SkillA.CanBreakPart
            && c.SkillB!=null&&c.SkillB.RollCount==2&&c.SkillB.BasePower==14&&c.SkillB.Duel&&!c.SkillB.CanBreakPart;
        return ok ? GameSystemVerificationProbeResult.Pass("Stage1 boss canonical catalog migrated") : GameSystemVerificationProbeResult.Fail("Stage1 boss canonical fields mismatch");
    }

    private static GameSystemVerificationProbeResult VerifyC44(GameSystemVerificationContext _)
    {
        var m=Manifest();
        if(m==null) return GameSystemVerificationProbeResult.Fail("manifest missing");
        PhaseEContentMigration.AuditRollTextures(m); EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
        return GameSystemVerificationProbeResult.Pass($"Scanned={m.RollTextureScanned}, Passed={m.RollTexturePassed}, Pending={m.RollTexturePending}, Violations={m.RollTextureViolationCount}",
            m.RollTextureViolationCount>0 ? string.Join("\n", m.RollTextureIssues.Take(20)) : null);
    }

    private static GameSystemVerificationProbeResult VerifyO02(GameSystemVerificationContext _)
    {
        var m=Manifest();
        return m?.OlafSkillPoolComplete==true ? GameSystemVerificationProbeResult.Pass("Olaf skill pool marked complete") : GameSystemVerificationProbeResult.Pending("Olaf pool data waits for C-38", "만렙→기본/강화1/강화2 역산 규칙 확정 후 안전 이관합니다.");
    }

    private static GameSystemVerificationProbeResult VerifyY06(GameSystemVerificationContext _)
    {
        var m=Manifest();
        return m?.YujinSkillPoolComplete==true ? GameSystemVerificationProbeResult.Pass("Yujin skill pool marked complete") : GameSystemVerificationProbeResult.Pending("Yujin latest pool waits for C-38", "0916 최신 풀은 평타9/결투A~N 14장이며 구 A~L/평타2 계약을 사용하지 않습니다.");
    }

    private static GameSystemVerificationProbeResult VerifyH08(GameSystemVerificationContext _)
    {
        const string path="Assets/2. Data/Characters/Design2026/Hifumi/Skills/포커페이스.asset";
        var s=AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        bool ok=s!=null&&s.ActionType==ActionType.Preparation&&s.PreparationTier==PreparationTier.Strong&&s.OverrideEnergyCost&&s.EnergyCost==1&&s.Description!=null&&s.Description.Contains("-4")&&s.Description.Contains("+4");
        return ok ? GameSystemVerificationProbeResult.Pass("PokerFace Strong/1, -4 damage and +4 bone data synced") : GameSystemVerificationProbeResult.Fail(s==null?"PokerFace asset missing":$"Action={s.ActionType}, Tier={s.PreparationTier}, Cost={s.EnergyCost}, Desc={s.Description}");
    }
}
#endif
