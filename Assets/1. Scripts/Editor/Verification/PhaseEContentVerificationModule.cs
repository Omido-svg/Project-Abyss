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
        yield return Static("phasee.presentation.canonical", "PRESENTATION", "0916 canonical Timeline presentation closure", GameSystemVerificationCategory.Contract,
            "Olaf/Yujin 모든 canonical SkillDefinition이 complete SkillVisualDefinition을 가져 Hit Event 기반 HP presentation을 재생", VerifyCanonicalPresentation);
        yield return Static("phasee.run.exact_part_hp", "RUN-HP", "Run ↔ Battle exact part HP snapshot", GameSystemVerificationCategory.LiveState,
            "머리/왼팔/오른팔/다리의 state/currentHP/maxHP가 Run 상태에 정확히 저장되고 재사용 가능한지 검사", VerifyExactPartHp);
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
        if(dataOnly==0 && designPending==0 && connected==63)
        {
            var m=Manifest();
            string suffix=m?.TempBalanceActive==true
                ? $" / {m.ActiveBalanceProfile} proxies={m.TempEmotionProxyCount}"
                : string.Empty;
            return GameSystemVerificationProbeResult.Pass($"63/63 runtime connected{suffix}");
        }
        return GameSystemVerificationProbeResult.Pending(
            $"63 definitions; RuntimeConnected={connected}, DataOnly={dataOnly}, DesignPending={designPending}",
            "Runtime 연결이 남아 있습니다.");
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
        int executable=c.Items.Count(x=>x!=null && x.CombatItem!=null);
        bool complete=common==30 && cls==30 && executable==60;
        var manifest=Manifest();
        return complete
            ? GameSystemVerificationProbeResult.Pass(
                $"60 item definitions + canonical economy / executable={executable}/60 / profile={manifest?.ActiveBalanceProfile}")
            : GameSystemVerificationProbeResult.Pending(
                $"economy PASS; actual item definitions common={common}/30 class={cls}/30 executable={executable}/60",
                "실제 아이템 정의/runtime 연결이 남아 있습니다.");
    }

    private static GameSystemVerificationProbeResult VerifyC38(GameSystemVerificationContext _)
    {
        PhaseEContentManifest manifest = Manifest();
        if (manifest?.UpgradeDecompositionComplete != true)
        {
            return GameSystemVerificationProbeResult.Pending(
                "Base/Upgrade1/Upgrade2 decomposition unset",
                "Phase E 강화 분해 migration을 먼저 적용하세요.");
        }

        CharacterCombatLoadout olaf = AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(
            "Assets/2. Data/Characters/Design2026/Olaf/Olaf_TODO_Loadout.asset");
        CharacterCombatLoadout yujin = AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(
            "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_Loadout.asset");

        if (olaf == null || yujin == null)
            return GameSystemVerificationProbeResult.Fail("Olaf/Yujin loadout missing");

        List<SkillDefinition> all = olaf.EnumerateAllDefinitions()
            .Concat(yujin.EnumerateAllDefinitions())
            .Where(x => x != null)
            .Distinct()
            .ToList();

        int profiles = all.Count(x => x.UpgradeProfile != null);
        List<string> secondCostViolations = all
            .Where(x => x.UpgradeProfile?.Upgrade2 != null &&
                        x.UpgradeProfile.Upgrade2.CostOverride != 0)
            .Select(x => $"{x.SkillName}:{x.UpgradeProfile.Upgrade2.CostOverride}")
            .ToList();

        if (profiles != all.Count)
        {
            return GameSystemVerificationProbeResult.Fail(
                $"UpgradeProfile={profiles}/{all.Count}",
                "모든 canonical 스킬에 강화 profile이 필요합니다.");
        }

        if (secondCostViolations.Count > 0)
        {
            return GameSystemVerificationProbeResult.Fail(
                $"Upgrade2 CostOverride violations={secondCostViolations.Count}",
                "0916 정본에서 2회차 가격은 (미정)이므로 0(Unset)이어야 합니다.\n" +
                string.Join("\n", secondCostViolations.Take(40)));
        }

        return GameSystemVerificationProbeResult.Pass(
            $"UpgradeProfile={profiles}/{all.Count}; Upgrade2 CostOverride=Unset(0) 전부 PASS",
            "문서 수치는 강화 2회 완료 만렙 목표로 유지하되, 2회차 가격은 정본 (미정)이므로 canonical 숫자를 만들지 않습니다.");
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
        string details=m.RollTextureIssues!=null && m.RollTextureIssues.Count>0
            ? string.Join("\n", m.RollTextureIssues.Take(40))
            : null;
        if(m.RollTextureViolationCount>0)
            return GameSystemVerificationProbeResult.Fail(
                $"Scanned={m.RollTextureScanned}, Passed={m.RollTexturePassed}, Pending={m.RollTexturePending}, Violations={m.RollTextureViolationCount}", details);
        if(m.RollTexturePending>0)
            return GameSystemVerificationProbeResult.Pending(
                $"Scanned={m.RollTextureScanned}, Passed={m.RollTexturePassed}, Pending={m.RollTexturePending}, Violations=0", details);
        return GameSystemVerificationProbeResult.Pass(
            $"Scanned={m.RollTextureScanned}, Passed={m.RollTexturePassed}, Pending=0, Violations=0", details);
    }

    private static GameSystemVerificationProbeResult VerifyO02(GameSystemVerificationContext _)
    {
        PhaseEContentManifest manifest = Manifest();
        const string path = "Assets/2. Data/Characters/Design2026/Olaf/Olaf_TODO_Loadout.asset";
        CharacterCombatLoadout loadout = AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(path);
        if (loadout == null)
            return GameSystemVerificationProbeResult.Fail("Olaf loadout missing");

        int normal = loadout.NormalSkillPool?.Count ?? 0;
        int duel = loadout.DuelSkillPool?.Count ?? 0;
        int preparation = (loadout.CommonPreparationPool?.Count ?? 0) +
                          (loadout.CharacterPreparationPool?.Count ?? 0);
        int prestige = loadout.PrestigeSkillPool?.Count ?? 0;

        string[] canonical = OlafSkillIds.CanonicalNormal
            .Concat(OlafSkillIds.CanonicalDuel)
            .Concat(OlafSkillIds.CanonicalPreparation)
            .Concat(OlafSkillIds.CanonicalPrestige)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        HashSet<string> actual = SkillIdSet(loadout);
        List<string> missing = canonical.Where(id => !actual.Contains(id)).ToList();
        List<string> extra = actual.Where(id => !canonical.Contains(id, StringComparer.Ordinal)).ToList();
        bool exactIds = missing.Count == 0 && extra.Count == 0 && actual.Count == canonical.Length;
        bool profiles = loadout.EnumerateAllDefinitions().All(x => x != null && x.UpgradeProfile != null);
        bool secondCostsUnset = SecondUpgradeCostsUnset(loadout);

        bool ok = manifest?.OlafSkillPoolComplete == true &&
                  normal == 16 && duel == 21 && preparation == 9 && prestige == 3 &&
                  exactIds && profiles && secondCostsUnset;

        string details =
            $"N={normal}/16 D={duel}/21 P={preparation}/9 R={prestige}/3 IDs={actual.Count}/{canonical.Length} " +
            $"profiles={profiles} upgrade2Unset={secondCostsUnset}";

        if (ok)
            return GameSystemVerificationProbeResult.Pass($"Olaf 0916 canonical closure PASS / {details}");

        if (missing.Count > 0)
            details += "\nMissing IDs:\n" + string.Join("\n", missing);
        if (extra.Count > 0)
            details += "\nExtra IDs:\n" + string.Join("\n", extra);

        return GameSystemVerificationProbeResult.Fail("Olaf 0916 canonical closure mismatch", details);
    }

    private static GameSystemVerificationProbeResult VerifyY06(GameSystemVerificationContext _)
    {
        PhaseEContentManifest manifest = Manifest();
        const string path = "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_Loadout.asset";
        CharacterCombatLoadout loadout = AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(path);
        if (loadout == null)
            return GameSystemVerificationProbeResult.Fail("Yujin loadout missing");

        int normal = loadout.NormalSkillPool?.Count ?? 0;
        int duel = loadout.DuelSkillPool?.Count ?? 0;
        int preparation = (loadout.CommonPreparationPool?.Count ?? 0) +
                          (loadout.CharacterPreparationPool?.Count ?? 0);
        int prestige = loadout.PrestigeSkillPool?.Count ?? 0;

        string[] canonical = YujinSkillIds.CanonicalNormal
            .Concat(YujinSkillIds.CanonicalDuel)
            .Concat(YujinSkillIds.CanonicalPreparation)
            .Concat(YujinSkillIds.CanonicalPrestige)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        HashSet<string> actual = SkillIdSet(loadout);
        List<string> missing = canonical.Where(id => !actual.Contains(id)).ToList();
        List<string> extra = actual.Where(id => !canonical.Contains(id, StringComparer.Ordinal)).ToList();
        bool exactIds = missing.Count == 0 && extra.Count == 0 && actual.Count == canonical.Length;
        bool profiles = loadout.EnumerateAllDefinitions().All(x => x != null && x.UpgradeProfile != null);
        bool secondCostsUnset = SecondUpgradeCostsUnset(loadout);

        var requiredEffects = new (string id, Yujin0916EffectOperation operation)[]
        {
            (YujinSkillIds.LedgerCleanup, Yujin0916EffectOperation.LedgerCleanup),
            (YujinSkillIds.Aim, Yujin0916EffectOperation.AimCriticalRider),
            (YujinSkillIds.ConfirmKill, Yujin0916EffectOperation.ConfirmKillMomentum),
            (YujinSkillIds.FamiliarHand, Yujin0916EffectOperation.FamiliarHand),
            (YujinSkillIds.AceInTheHole, Yujin0916EffectOperation.AceInTheHole),
            (YujinSkillIds.Wanted, Yujin0916EffectOperation.Wanted),
            (YujinSkillIds.Trap, Yujin0916EffectOperation.Trap),
            (YujinSkillIds.Deadline, Yujin0916EffectOperation.Deadline),
            (YujinSkillIds.AllTargets, Yujin0916EffectOperation.DesignateAll),
            (YujinSkillIds.Period, Yujin0916EffectOperation.Period),
            (YujinSkillIds.HoldBreath, Yujin0916EffectOperation.HoldBreath),
            (YujinSkillIds.Sharpen, Yujin0916EffectOperation.Sharpen)
        };

        List<string> effectMissing = requiredEffects
            .Where(pair => !HasCanonicalEffect(loadout, pair.id, pair.operation))
            .Select(pair => $"{pair.id} -> {pair.operation}")
            .ToList();

        bool effects = effectMissing.Count == 0;
        bool ok = manifest?.YujinSkillPoolComplete == true &&
                  normal == 9 && duel == 14 && preparation == 9 && prestige == 3 &&
                  exactIds && profiles && secondCostsUnset && effects;

        string details =
            $"N={normal}/9 D={duel}/14 P={preparation}/9 R={prestige}/3 IDs={actual.Count}/{canonical.Length} " +
            $"profiles={profiles} upgrade2Unset={secondCostsUnset} explicitRiders={requiredEffects.Length - effectMissing.Count}/{requiredEffects.Length}";

        if (ok)
            return GameSystemVerificationProbeResult.Pass($"Yujin 0916 canonical closure PASS / {details}");

        if (missing.Count > 0)
            details += "\nMissing IDs:\n" + string.Join("\n", missing);
        if (extra.Count > 0)
            details += "\nExtra IDs:\n" + string.Join("\n", extra);
        if (effectMissing.Count > 0)
            details += "\nMissing explicit runtime riders:\n" + string.Join("\n", effectMissing);

        return GameSystemVerificationProbeResult.Fail("Yujin 0916 canonical closure mismatch", details);
    }

    private static GameSystemVerificationProbeResult VerifyCanonicalPresentation(GameSystemVerificationContext _)
    {
        CharacterCombatLoadout olaf = AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(
            "Assets/2. Data/Characters/Design2026/Olaf/Olaf_TODO_Loadout.asset");
        CharacterCombatLoadout yujin = AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(
            "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_Loadout.asset");

        if (olaf == null || yujin == null)
            return GameSystemVerificationProbeResult.Fail("Olaf/Yujin loadout missing");

        List<string> missing = new List<string>();
        int total = 0;
        int complete = 0;

        foreach (var pair in new[]
        {
            new { Owner = "Olaf", Loadout = olaf },
            new { Owner = "Yujin", Loadout = yujin }
        })
        {
            foreach (SkillDefinition skill in pair.Loadout.EnumerateAllDefinitions()
                         .Where(x => x != null)
                         .Distinct())
            {
                total++;
                if (skill.PresentationAsset is SkillVisualDefinition visual &&
                    visual.HasCompleteTimelineSet)
                {
                    complete++;
                    continue;
                }

                string presentation = skill.PresentationAsset == null
                    ? "NULL"
                    : skill.PresentationAsset.GetType().Name;
                missing.Add(
                    $"{pair.Owner}/{skill.SkillName} ({skill.SkillId}) -> {presentation}");
            }
        }

        if (missing.Count == 0)
        {
            return GameSystemVerificationProbeResult.Pass(
                $"canonical presentation complete={complete}/{total}",
                "Gameplay HP는 Resolve에서 선계산되고, 표시 HP는 Timeline Hit Event에서 commit되는 계약을 유지합니다.");
        }

        return GameSystemVerificationProbeResult.Fail(
            $"canonical presentation incomplete={missing.Count}, complete={complete}/{total}",
            string.Join("\n", missing.Take(80)));
    }

    private static GameSystemVerificationProbeResult VerifyExactPartHp(GameSystemVerificationContext _)
    {
        RunProgressionState progression = new RunProgressionState();
        progression.Reset(0, 1);
        RunFlowTestAvatarState avatar = new RunFlowTestAvatarState(progression);

        avatar.SetCurrentHpFromBattle(437);
        avatar.SetPartSnapshot(PartType.HEAD, RunFlowTestPartState.Normal, 88f, 100f);
        avatar.SetPartSnapshot(PartType.LEFT_HAND, RunFlowTestPartState.Weakened, 37f, 100f);
        avatar.SetPartSnapshot(PartType.RIGHT_HAND, RunFlowTestPartState.Normal, 64f, 100f);
        avatar.SetPartSnapshot(PartType.LEGS, RunFlowTestPartState.Broken, 0f, 100f);

        bool shape = avatar.Parts != null && avatar.Parts.Count == 4;
        bool uniqueTypes = shape &&
                           avatar.Parts.Select(x => x.Type).Distinct().Count() == 4 &&
                           avatar.GetPart(PartType.HEAD) != null &&
                           avatar.GetPart(PartType.LEFT_HAND) != null &&
                           avatar.GetPart(PartType.RIGHT_HAND) != null &&
                           avatar.GetPart(PartType.LEGS) != null;
        bool exactFlags = shape && avatar.Parts.All(x => x != null && x.HasExactHp);
        RunFlowTestPart head = avatar.GetPart(PartType.HEAD);
        RunFlowTestPart left = avatar.GetPart(PartType.LEFT_HAND);
        RunFlowTestPart right = avatar.GetPart(PartType.RIGHT_HAND);
        RunFlowTestPart legs = avatar.GetPart(PartType.LEGS);
        bool values = shape && uniqueTypes &&
                      avatar.CurrentHp == 437 &&
                      Mathf.Approximately(head.CurrentHp, 88f) &&
                      Mathf.Approximately(left.CurrentHp, 37f) &&
                      Mathf.Approximately(right.CurrentHp, 64f) &&
                      Mathf.Approximately(legs.CurrentHp, 0f) &&
                      left.State == RunFlowTestPartState.Weakened &&
                      legs.State == RunFlowTestPartState.Broken &&
                      avatar.Parts.All(x => Mathf.Approximately(x.MaximumHp, 100f));

        RunFlowLiveTransferAudit audit = new RunFlowLiveTransferAudit
        {
            SameRunProgression = true,
            SameSkillUpgrades = true,
            ExpectedCombatItems = 0,
            EquippedCombatItems = 0,
            ExpectedTempBalanceMechanics = 0,
            TempBalanceMechanics = 0,
            ExpectedPartSnapshots = 4,
            AppliedPartSnapshots = 4
        };

        bool contract = audit.Passed;
        if (shape && uniqueTypes && exactFlags && values && contract)
        {
            return GameSystemVerificationProbeResult.Pass(
                "Exact part HP snapshot model 4/4 + transfer audit contract PASS",
                "실제 Scene 왕복은 RunFlow Live Transfer Audit의 ExactPartHP=4/4 로그로 추가 실행 검증하세요.");
        }

        return GameSystemVerificationProbeResult.Fail(
            $"shape={shape}, uniqueTypes={uniqueTypes}, exactFlags={exactFlags}, values={values}, audit={contract}",
            "Run-scoped part state/currentHP/maxHP 저장 계약이 깨졌습니다.");
    }

    private static HashSet<string> SkillIdSet(CharacterCombatLoadout loadout)
    {
        return new HashSet<string>(
            loadout?.EnumerateAllDefinitions()
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SkillId))
                .Select(x => x.SkillId) ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);
    }

    private static bool SecondUpgradeCostsUnset(CharacterCombatLoadout loadout)
    {
        return loadout != null && loadout.EnumerateAllDefinitions()
            .Where(x => x?.UpgradeProfile?.Upgrade2 != null)
            .All(x => x.UpgradeProfile.Upgrade2.CostOverride == 0);
    }

    private static bool HasCanonicalEffect(
        CharacterCombatLoadout loadout,
        string skillId,
        Yujin0916EffectOperation operation)
    {
        SkillDefinition skill = loadout?.EnumerateAllDefinitions()
            .FirstOrDefault(x => x != null &&
                                 string.Equals(x.SkillId, skillId, StringComparison.Ordinal));
        if (skill?.EffectEntries == null)
            return false;

        return skill.EffectEntries.Any(entry =>
            entry?.Definition is Yujin0916SkillEffectDefinition effect &&
            effect.Operation == operation);
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
