#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0922 Phase 7 — Olaf / Yujin 확정 스킬 콘텐츠 migration.
/// 문서의 (미정)은 기존 임시값을 canonical로 승격하지 않고 PENDING으로 둔다.
/// 반복 실행해도 동일한 결과가 되도록 asset/entry를 upsert한다.
/// </summary>
public static class Canonical0922Phase7SkillContentMigration
{
    private const string OlafLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Olaf/Olaf_TODO_Loadout.asset";
    private const string YujinLoadoutPath =
        "Assets/2. Data/Characters/Design2026/Yujin/Yujin_TODO_Loadout.asset";
    private const string Root =
        "Assets/2. Data/Progression/Canonical0922/Phase7";
    private const string OlafEffectsRoot = Root + "/Olaf/Effects";
    private const string YujinEffectsRoot = Root + "/Yujin/Effects";

    [MenuItem("Game System Verification/0922 Canonical/Phase 7 - Apply Olaf Yujin Skill Migration")]
    public static void ApplyFromMenu()
    {
        EnsureFolder(OlafEffectsRoot);
        EnsureFolder(YujinEffectsRoot);

        CharacterCombatLoadout olaf =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(OlafLoadoutPath);
        CharacterCombatLoadout yujin =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(YujinLoadoutPath);

        if (olaf == null || yujin == null)
        {
            Debug.LogError(
                $"[0922 Phase7] Loadout missing. Olaf={olaf != null}, Yujin={yujin != null}");
            return;
        }

        int olafChanged = ApplyOlaf(olaf);
        int yujinChanged = ApplyYujin(yujin);

        EditorUtility.SetDirty(olaf);
        EditorUtility.SetDirty(yujin);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[0922 Phase7] Migration applied. " +
            $"OlafTouched={olafChanged}, YujinTouched={yujinChanged}. " +
            "(미정)은 PENDING_CANONICAL로 보존됩니다. " +
            "이제 Phase 7 Verify를 실행하세요.");
    }

    private static int ApplyOlaf(CharacterCombatLoadout loadout)
    {
        int changed = 0;

        SkillDefinition bite = Find(loadout, OlafSkillIds.Bite, "물어뜯기");
        if (bite != null)
        {
            ConfigureRolls(bite,
                new[] { 9, 10, 10 },
                new[] { 20, 21, 21 });
            bite.OverrideEnergyCost = true;
            bite.EnergyCost = 0;
            SetEffects(bite,
                Entry(O("Olaf_Bite_RandomDebuff", Olaf0922Phase7EffectOperation.RandomTargetDebuffNextTurn), SkillEffectTiming.OnExchangeWin));
            Mark(bite, "0922 확정: 승리 순간 균열/골절/쇠약 중 1종 결정 → 다음 턴 1·1. 범위 9~20/10~21/10~21.");
            changed++;
        }

        SkillDefinition exploit = Find(loadout, OlafSkillIds.ExploitGap, "틈을 파고들다");
        if (exploit != null)
        {
            SetEffects(exploit,
                Entry(O("Olaf_Exploit_Rupture11", Olaf0922Phase7EffectOperation.TargetStatusNextTurn, StatusEffectId.Rupture, 1, 1), SkillEffectTiming.OnExchangeWin, 1),
                Entry(O("Olaf_Exploit_Rupture12", Olaf0922Phase7EffectOperation.TargetStatusNextTurn, StatusEffectId.Rupture, 1, 1), SkillEffectTiming.OnExchangeWin, 2));
            Mark(exploit, "0922 확정: 1·2번째 승리 각각 다음 턴 균열 1·1 독립 Entry. 3번째 흐트러짐 rider는 기존 런타임 유지.");
            changed++;
        }

        SkillDefinition aimWeak = Find(loadout, OlafSkillIds.AimWeakness, "약점을 노리다");
        if (aimWeak != null)
        {
            Olaf0922Phase7SkillEffectDefinition conditional =
                O("Olaf_AimWeak_RuptureIfBleed5", Olaf0922Phase7EffectOperation.TargetStatusNextTurnIfBleedingAtLeast, StatusEffectId.Rupture, 1, 1);
            conditional.MinimumBleeding = 5;
            EditorUtility.SetDirty(conditional);

            SetEffects(aimWeak,
                Entry(O("Olaf_AimWeak_Weakness13", Olaf0922Phase7EffectOperation.TargetStatusNextTurn, StatusEffectId.Weakness, 1, 3), SkillEffectTiming.OnExchangeWin, 1),
                Entry(conditional, SkillEffectTiming.OnExchangeWin, 2));
            Mark(aimWeak, "0922 확정 N/T: 1번째 쇠약1·3, 2번째는 출혈5+일 때 균열1·1. 3번째 상태개수 위력 rider는 기존 런타임 유지.");
            changed++;
        }

        SkillDefinition choke = Find(loadout, OlafSkillIds.Choke, "숨통을 조이다");
        if (choke != null)
        {
            SetEffects(choke,
                Entry(O("Olaf_Choke_Pain3", Olaf0922Phase7EffectOperation.TargetStatusNextTurn, StatusEffectId.Pain, 1, 3), SkillEffectTiming.OnExchangeWin));
            Mark(choke, "0922 확정: 승리시 다음 턴 고통 3턴.");
            changed++;
        }

        SkillDefinition blockWay = Find(loadout, OlafSkillIds.BlockWay, "막아서기");
        if (blockWay != null)
        {
            SetEffects(blockWay,
                Entry(O("Olaf_BlockWay_Block10", Olaf0922Phase7EffectOperation.OwnerBlock, amount: 10), SkillEffectTiming.OnExecute));
            Mark(blockWay, "0922 확정: 사용시 방어도 +10.");
            changed++;
        }

        SkillDefinition slice = Find(loadout, OlafSkillIds.SliceThin, "저며내기");
        if (slice != null)
        {
            SetEffects(slice,
                Entry(O("Olaf_Slice_TargetBleed2", Olaf0922Phase7EffectOperation.TargetBleeding, amount: 2), SkillEffectTiming.OnExchangeWin, 2),
                Entry(O("Olaf_Slice_SelfBleed1", Olaf0922Phase7EffectOperation.OwnerBleeding, amount: 1), SkillEffectTiming.OnExchangeWin, 2));
            Mark(slice, "0922 확정: 2번째 승리 출혈 +2 추가 + 자기 출혈1.");
            changed++;
        }

        SkillDefinition tenacious = Find(loadout, OlafSkillIds.Tenacious, "끈질기게");
        if (tenacious != null)
        {
            SetEffects(tenacious,
                Entry(O("Olaf_Tenacious_Bleed1_R1", Olaf0922Phase7EffectOperation.TargetBleeding, amount: 1), SkillEffectTiming.OnExchangeWin, 1),
                Entry(O("Olaf_Tenacious_Bleed1_R2", Olaf0922Phase7EffectOperation.TargetBleeding, amount: 1), SkillEffectTiming.OnExchangeWin, 2),
                Entry(O("Olaf_Tenacious_Regen23", Olaf0922Phase7EffectOperation.OwnerStatusImmediate, StatusEffectId.Regeneration, 2, 3), SkillEffectTiming.OnExchangeWin, 3));
            Mark(tenacious, "0922 확정: 1·2번째 출혈+1 / 3번째 재생2·3.");
            changed++;
        }

        SkillDefinition charge = Find(loadout, OlafSkillIds.BloodCharge, "피의돌진");
        if (charge != null)
        {
            SetEffects(charge,
                Entry(O("Olaf_BloodCharge_Bleed1", Olaf0922Phase7EffectOperation.TargetBleeding, amount: 1), SkillEffectTiming.OnExchangeWin),
                Entry(O("Olaf_BloodCharge_Fracture11", Olaf0922Phase7EffectOperation.TargetStatusNextTurn, StatusEffectId.Fracture, 1, 1), SkillEffectTiming.OnExchangeWin),
                Entry(O("Olaf_BloodCharge_LoseMadness2", Olaf0922Phase7EffectOperation.AddMadness, amount: 2), SkillEffectTiming.OnExchangeLose));
            Mark(charge, "0922 확정: 승리 출혈+1+골절1·1 예약 / 패배 광기+2.");
            changed++;
        }

        SkillDefinition hold = Find(loadout, OlafSkillIds.HoldOn, "물고 늘어지기");
        if (hold != null)
        {
            SetEffects(hold,
                Entry(O("Olaf_HoldOn_Rupture22", Olaf0922Phase7EffectOperation.TargetStatusNextTurn, StatusEffectId.Rupture, 2, 2), SkillEffectTiming.OnExchangeWin));
            Mark(hold, "0922 확정: 승리시 다음 턴 균열2·2.");
            changed++;
        }

        SkillDefinition headOn = Find(loadout, OlafSkillIds.HeadOn, "정면승부");
        if (headOn != null)
        {
            SetEffects(headOn);
            headOn.CanBreakPart = true;
            headOn.BreakMode = PartBreakMode.WeakenedOnly;
            Rule(headOn).Enabled = true;
            Mark(headOn, "0922 확정: 파괴 권한. 결투 교환 승리시 실제 준 피해의 10% SelfCost. Runtime=OlafRulebreakerMechanic.");
            changed++;
        }

        SkillDefinition fortify = Find(loadout, OlafSkillIds.Fortify, "굳히기");
        if (fortify != null)
        {
            SetEffects(fortify,
                Entry(O("Olaf_Fortify_StrengthBand", Olaf0922Phase7EffectOperation.StrengthByTargetBleedingBandNextTurn, duration: 1), SkillEffectTiming.OnDuelMatched, 1));
            Mark(fortify, "0922 확정: 상대 출혈 0~4/5~9/10+ → 다음 턴 자기 힘1/2/3·1.");
            changed++;
        }

        SkillDefinition protect = Find(loadout, OlafSkillIds.ProtectSelf, "몸을 사리다");
        if (protect != null)
        {
            SetEffects(protect,
                Entry(O("Olaf_ProtectSelf_Protection11", Olaf0922Phase7EffectOperation.OwnerStatusNextTurn, StatusEffectId.Protection, 1, 1), SkillEffectTiming.OnDuelMatched, 1));
            Mark(protect, "0922 확정: 다음 턴 자기 보호1·1.");
            changed++;
        }

        SkillDefinition allIn = Find(loadout, OlafSkillIds.AllIn, "전부 걸다");
        if (allIn != null)
        {
            SetEffects(allIn,
                Entry(O("Olaf_AllIn_Bleed2", Olaf0922Phase7EffectOperation.TargetBleeding, amount: 2), SkillEffectTiming.OnExchangeWin, 1),
                Entry(O("Olaf_AllIn_Weakness11", Olaf0922Phase7EffectOperation.TargetStatusNextTurn, StatusEffectId.Weakness, 1, 1), SkillEffectTiming.OnExchangeWin, 2),
                Entry(O("Olaf_AllIn_ClearBlock", Olaf0922Phase7EffectOperation.ClearTargetBlock), SkillEffectTiming.OnDuelMatched, 3));
            Mark(allIn, "0922 확정: R1 출혈+2 / R2 쇠약1·1 / R3 매칭시 상대 방어도 전부 제거.");
            changed++;
        }

        SkillDefinition bloodPrice = Find(loadout, OlafSkillIds.BloodPrice, "피의 대가");
        if (bloodPrice != null)
        {
            SetEffects(bloodPrice,
                Entry(O("Olaf_BloodPrice_SelfBleed4", Olaf0922Phase7EffectOperation.OwnerBleeding, amount: 4), SkillEffectTiming.OnExecute),
                Entry(O("Olaf_BloodPrice_SelfStrength", Olaf0922Phase7EffectOperation.BloodPriceStrengthByOwnerBleeding), SkillEffectTiming.OnDuelMatched, 2));
            Mark(bloodPrice, "0922 변경: 상대 균열이 아니라 자기 출혈+4 → R2 매칭시 자기 출혈 구간으로 다음 턴 자기 힘1/2/3·3.");
            changed++;
        }

        SkillDefinition absorb = Find(loadout, OlafSkillIds.AbsorbBlood, "피를 흡수하다");
        if (absorb != null)
        {
            SetEffects(absorb,
                Entry(O("Olaf_AbsorbBlood_5ToLight2", Olaf0922Phase7EffectOperation.ConsumeTargetBleedingForEnergy, amount: 5), SkillEffectTiming.OnExecute));
            Mark(absorb, "0922 확정: 대상 출혈5 이상이면 5 제거 → 빛+2.");
            changed++;
        }

        SkillDefinition whatever = Find(loadout, OlafSkillIds.WhateverComes, "닥치는 대로");
        if (whatever != null)
        {
            SetEffects(whatever,
                Entry(O("Olaf_WhateverComes_Madness1", Olaf0922Phase7EffectOperation.AddMadness, amount: 1), SkillEffectTiming.OnExecute));
            SkillRulebreakerSettings rule = Rule(whatever);
            rule.Enabled = true;
            rule.EnergyCostReductionPerCommittedUse = 1;
            rule.MinimumEnergyCost = 1;
            whatever.CanBreakPart = true;
            whatever.BreakMode = PartBreakMode.WeakenedOnly;
            Mark(whatever, "0922 확정: 전투 내 사용마다 비용-1(최소1), 사용시 광기+1, 파괴 권한. 초기 비용 값은 데이터에 둔다.");
            changed++;
        }

        SkillDefinition honorable = Find(loadout, OlafSkillIds.HonorableFight, "명예로운 전투");
        if (honorable != null)
        {
            SetEffects(honorable);
            SkillRulebreakerSettings rule = Rule(honorable);
            rule.Enabled = true;
            rule.MirrorThisSkillToOpponentOnDuelMatch = true;
            honorable.CanBreakPart = true;
            honorable.BreakMode = PartBreakMode.WeakenedOnly;
            Mark(honorable,
                "0922 확정: 양쪽 1굴림으로 강제 후 한쪽 부위 약화까지 반복. " +
                "PENDING_CANONICAL: 부위 없는 일반몹 종료조건 미정 — 1교환 fallback.");
            changed++;
        }

        SkillDefinition oathDuel = Find(loadout, OlafSkillIds.BloodOathDuel, "피의 맹세");
        if (oathDuel != null)
        {
            SetEffects(oathDuel,
                Entry(O("Olaf_BloodOath_Setup", Olaf0922Phase7EffectOperation.PrepareBloodOathDuel), SkillEffectTiming.OnExecute),
                Entry(O("Olaf_BloodOath_Win1", Olaf0922Phase7EffectOperation.ResolveBloodOathDuelWin), SkillEffectTiming.OnExchangeWin, 1),
                Entry(O("Olaf_BloodOath_Win2", Olaf0922Phase7EffectOperation.ResolveBloodOathDuelWin), SkillEffectTiming.OnExchangeWin, 2));
            SkillRulebreakerSettings rule = Rule(oathDuel);
            rule.Enabled = true;
            rule.ConsumeAllOwnerBleedingOnExecute = false;
            rule.BreakAuthorityBleedingThreshold = 0;
            rule.IgnoreWeakenPrerequisiteBleedingThreshold = 0;
            oathDuel.CanBreakPart = false;
            oathDuel.BreakMode = PartBreakMode.None;
            Mark(oathDuel, "0922 확정 3구간: 0~4 없음 / 5~9 +4·일반파괴·재생4·3·R1출혈3 / 10+ +8·직접파괴·R1/R2출혈3·R2빛1.");
            changed++;
        }

        SkillDefinition payback = Find(loadout, OlafSkillIds.PaybackAll, "전부 되갚다");
        if (payback != null)
        {
            SkillRulebreakerSettings rule = Rule(payback);
            rule.Enabled = true;
            rule.ApplyCurrentDisadvantageOrWorsePowerBonus = true;
            rule.CurrentDisadvantageOrWorsePowerBonus = 4;
            rule.ApplyCurrentLastStandPowerBonus = false;
            rule.CurrentLastStandPowerBonus = 0;
            Mark(payback, "0922 확정: 사용시 B<=-30이면 이 스킬 모든 굴림 위력+4.");
            changed++;
        }

        SkillDefinition singleCombat = Find(loadout, OlafSkillIds.SingleCombat, "일기토");
        if (singleCombat != null)
        {
            SkillRulebreakerSettings rule = Rule(singleCombat);
            rule.Enabled = true;
            rule.SuppressFriendlyOneSidedHitsOnSameTargetSlot = true;
            singleCombat.CanBreakPart = true;
            singleCombat.BreakMode = PartBreakMode.WeakenedOnly;
            Mark(singleCombat, "0922 확정: 같은 상대 슬롯을 노린 다른 내 슬롯 일방타격 포기 + 승리시 출혈1(기존 효과 유지).");
            changed++;
        }

        // 폭발 계수와 단칼 위력은 미정. stale 값이 canonical로 오인되지 않게 0/표식만 유지한다.
        foreach ((string id, string name) in new[]
                 {
                     (OlafSkillIds.Standard, "표준"),
                     (OlafSkillIds.SingleCut, "단칼")
                 })
        {
            SkillDefinition skill = Find(loadout, id, name);
            if (skill == null) continue;
            SkillRulebreakerSettings rule = Rule(skill);
            rule.Enabled = true;
            rule.ExplodeBleedingOncePerAction = true;
            rule.BleedingExplosionMultiplier = 0;
            skill.CanBreakPart = true;
            skill.BreakMode = PartBreakMode.WeakenedOnly;
            Mark(skill, "PENDING_CANONICAL: 출혈 폭발 계수 미정. 값 0은 비활성 sentinel이며 기획 확정 전 하드코딩 금지.");
            changed++;
        }

        SkillDefinition oathPrep = Find(loadout, OlafSkillIds.BloodOathPreparation, "피의 서약");
        if (oathPrep != null)
        {
            SetEffects(oathPrep,
                Entry(O("Olaf_BloodOathPrep_Bleed3", Olaf0922Phase7EffectOperation.OwnerBleeding, amount: 3), SkillEffectTiming.OnExecute),
                Entry(O("Olaf_BloodOathPrep_Regen43", Olaf0922Phase7EffectOperation.OwnerStatusImmediate, StatusEffectId.Regeneration, 4, 3), SkillEffectTiming.OnExecute));
            Mark(oathPrep, "0922 확정: 자기 출혈+3 + 재생4·3.");
            changed++;
        }

        loadout.CharacterPreparationPool ??= new List<SkillDefinition>();
        loadout.DuelSkillPool ??= new List<SkillDefinition>();
        loadout.DuelSkills ??= new List<SkillDefinition>();

        SkillDefinition stoke = Find(loadout, OlafSkillIds.StokePrestige, "위세를 지피다", "olaf.duel.stoke_prestige");
        if (stoke != null)
        {
            SetSkillId(stoke, OlafSkillIds.StokePrestige);
            stoke.ActionType = ActionType.Preparation;
            stoke.Color = SkillColor.Unset;
            stoke.OverrideEnergyCost = true;
            stoke.EnergyCost = 1;
            stoke.PreparationTier = PreparationTier.Strong;
            SetEffects(stoke);
            RemoveByReference(loadout.DuelSkills, stoke);
            RemoveByReference(loadout.DuelSkillPool, stoke);
            UpsertBySkillId(loadout.CharacterPreparationPool, stoke);
            Mark(stoke,
                "0922 변경: 결투→강한 도사림. 그 턴 이긴 교환마다 상대 출혈 구간으로 위세 충전. " +
                "PENDING_CANONICAL: 구간별 충전량 미정이므로 runtime 효과 비활성.");
            changed++;
        }

        return changed;
    }

    private static int ApplyYujin(CharacterCombatLoadout loadout)
    {
        int changed = 0;

        changed += ConfigureYujinEffect(loadout, YujinSkillIds.DesignateTarget, "목표 지정",
            Y("Yujin_C_Designation23", Yujin0916EffectOperation.DesignateCurrent),
            SkillEffectTiming.OnExecute, energy: 1,
            description: "0922 확정: 사용시 상대 부위 지정2·3.");

        changed += ConfigureYujinEffect(loadout, YujinSkillIds.AllTargets, "모두를 노리다",
            Y("Yujin_M_Designation23_All", Yujin0916EffectOperation.DesignateAll),
            SkillEffectTiming.OnExecute, energy: 2,
            description: "0922 확정: 사용시 모든 적 지정2·3.");

        changed += ConfigureYujinEffect(loadout, YujinSkillIds.Wanted, "현상수배",
            Y("Yujin_Wanted_WeaponDebuff11", Yujin0916EffectOperation.Wanted),
            SkillEffectTiming.OnExchangeWin, energy: 1,
            description: "0922 확정: 승리시 다음 턴 모든 적 — 백우 쇠약1·1 / 적설 골절1·1 / 낙일 균열1·1.");

        changed += ConfigureYujinEffect(loadout, YujinSkillIds.ProbeWeakness, "약점을 캐내다",
            Y("Yujin_H_WeaponDebuff11", Yujin0916EffectOperation.ProbeWeakness),
            SkillEffectTiming.OnExchangeWin, energy: 1,
            description: "0922 확정: 승리시 다음 턴 상대 — 백우 쇠약1·1 / 적설 골절1·1 / 낙일 균열1·1.");

        changed += ConfigureYujinEffect(loadout, YujinSkillIds.Sharpen, "칼을 갈다",
            Y("Yujin_Sharpen_Swift11", Yujin0916EffectOperation.Sharpen),
            SkillEffectTiming.OnExecute, energy: 0,
            description: "0922 확정: 표식10 소비 → 다음 턴 신속1·1.");

        // O/P는 0917 asset을 유지하되 0922 Phase6 atomic mark path와 동적 비용을 재확인한다.
        SkillDefinition o = Find(loadout, YujinSkillIds.AdvanceTiming, "때를 앞당기다");
        if (o != null)
        {
            Mark(o, "0922 확정: 감4 소비 → 표식을 44까지 채워 즉시 발화. K/L rider 포함, overflow 없음.");
            changed++;
        }

        SkillDefinition p = Find(loadout, YujinSkillIds.FinishIt, "끝장을 보다");
        if (p != null)
        {
            SkillRulebreakerSettings rule = Rule(p);
            rule.Enabled = true;
            rule.EnergyCostReductionPerCommittedUse = 1;
            rule.MinimumEnergyCost = 1;
            Mark(p, "0922 확정: 실제 비용4→3→2→1, 사용시 감 전량→1당 위력+2, 기본위력은 빛2 기준 고정.");
            changed++;
        }

        return changed;
    }

    private static int ConfigureYujinEffect(
        CharacterCombatLoadout loadout,
        string id,
        string name,
        Yujin0916SkillEffectDefinition effect,
        SkillEffectTiming timing,
        int energy,
        string description)
    {
        SkillDefinition skill = Find(loadout, id, name);
        if (skill == null)
            return 0;

        skill.OverrideEnergyCost = true;
        skill.EnergyCost = Mathf.Max(0, energy);
        SetEffects(skill, Entry(effect, timing));
        Mark(skill, description);
        return 1;
    }

    private static Olaf0922Phase7SkillEffectDefinition O(
        string file,
        Olaf0922Phase7EffectOperation op,
        StatusEffectId status = StatusEffectId.Rupture,
        int amount = 1,
        int duration = 1)
    {
        Olaf0922Phase7SkillEffectDefinition asset =
            EnsureAsset<Olaf0922Phase7SkillEffectDefinition>(
                $"{OlafEffectsRoot}/{file}.asset");
        asset.Operation = op;
        asset.StatusId = status;
        asset.Amount = Mathf.Max(0, amount);
        asset.Duration = duration < 0 ? StatusEffect.InfiniteDuration : Mathf.Max(1, duration);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static Yujin0916SkillEffectDefinition Y(
        string file,
        Yujin0916EffectOperation op)
    {
        Yujin0916SkillEffectDefinition asset =
            EnsureAsset<Yujin0916SkillEffectDefinition>(
                $"{YujinEffectsRoot}/{file}.asset");
        asset.Operation = op;
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static SkillEffectEntry Entry(
        SkillEffectDefinition effect,
        SkillEffectTiming timing,
        int rollNumber = 0)
    {
        return new SkillEffectEntry
        {
            Definition = effect,
            OverrideTiming = true,
            Timing = timing,
            RestrictToRoll = rollNumber > 0,
            RollNumber = Mathf.Max(1, rollNumber),
            Conditions = new List<SkillEffectCondition>(),
            Overrides = new SkillEffectOverrides()
        };
    }

    private static void SetEffects(
        SkillDefinition skill,
        params SkillEffectEntry[] entries)
    {
        if (skill == null)
            return;

        skill.EffectEntries ??= new List<SkillEffectEntry>();
        skill.EffectEntries.Clear();
        if (entries != null)
        {
            foreach (SkillEffectEntry entry in entries)
                if (entry?.Definition != null)
                    skill.EffectEntries.Add(entry);
        }
        skill.Effects ??= new List<SkillEffectDefinition>();
        skill.Effects.Clear();

        if (skill.Rolls != null)
        {
            foreach (SkillRollData roll in skill.Rolls)
            {
                if (roll == null)
                    continue;

                roll.EffectEntries ??= new List<SkillEffectEntry>();
                roll.OnWinEffectEntries ??= new List<SkillEffectEntry>();
                roll.OnLoseEffectEntries ??= new List<SkillEffectEntry>();
                roll.OnWinEffects ??= new List<SkillEffectDefinition>();
                roll.OnLoseEffects ??= new List<SkillEffectDefinition>();

                roll.EffectEntries.Clear();
                roll.OnWinEffectEntries.Clear();
                roll.OnLoseEffectEntries.Clear();
                roll.OnWinEffects.Clear();
                roll.OnLoseEffects.Clear();
            }
        }

        EditorUtility.SetDirty(skill);
    }

    private static void ConfigureRolls(
        SkillDefinition skill,
        IReadOnlyList<int> min,
        IReadOnlyList<int> max)
    {
        if (skill == null || min == null || max == null || min.Count != max.Count)
            return;

        skill.Rolls ??= new List<SkillRollData>();
        while (skill.Rolls.Count < min.Count)
            skill.Rolls.Add(new SkillRollData());
        while (skill.Rolls.Count > min.Count)
            skill.Rolls.RemoveAt(skill.Rolls.Count - 1);

        for (int i = 0; i < min.Count; i++)
        {
            skill.Rolls[i] ??= new SkillRollData();
            skill.Rolls[i].Index = i;
            skill.Rolls[i].MinPower = Mathf.Max(1, min[i]);
            skill.Rolls[i].MaxPower = Mathf.Max(skill.Rolls[i].MinPower, max[i]);
        }
        skill.ExchangeRollCount = Mathf.Clamp(min.Count, 1, 8);
        EditorUtility.SetDirty(skill);
    }

    private static SkillRulebreakerSettings Rule(SkillDefinition skill)
    {
        skill.Rulebreaker ??= new SkillRulebreakerSettings();
        EditorUtility.SetDirty(skill);
        return skill.Rulebreaker;
    }

    private static SkillDefinition Find(
        CharacterCombatLoadout loadout,
        string id,
        string name,
        string legacyId = null)
    {
        if (loadout == null)
            return null;

        SkillDefinition[] all = loadout.EnumerateAllDefinitions()
            .Where(x => x != null)
            .Distinct()
            .ToArray();

        SkillDefinition found = all.FirstOrDefault(x =>
            string.Equals(x.SkillId, id, StringComparison.Ordinal));
        if (found != null) return found;

        if (!string.IsNullOrWhiteSpace(legacyId))
        {
            found = all.FirstOrDefault(x =>
                string.Equals(x.SkillId, legacyId, StringComparison.Ordinal));
            if (found != null) return found;
        }

        return all.FirstOrDefault(x =>
            string.Equals(x.SkillName, name, StringComparison.Ordinal));
    }

    private static void Mark(SkillDefinition skill, string text)
    {
        if (skill == null)
            return;

        string old = skill.Description ?? string.Empty;
        string[] lines = old.Split(new[] { '\n' }, StringSplitOptions.None);
        string body = string.Join("\n", lines.Where(x =>
            !x.StartsWith("[CANONICAL_0922_PHASE7]", StringComparison.Ordinal) &&
            !x.StartsWith("[PENDING_CANONICAL][0922_PHASE7]", StringComparison.Ordinal)));

        string prefix = text != null && text.Contains("PENDING_CANONICAL")
            ? "[PENDING_CANONICAL][0922_PHASE7] "
            : "[CANONICAL_0922_PHASE7] ";

        skill.Description = prefix + text +
            (string.IsNullOrWhiteSpace(body) ? string.Empty : "\n" + body.Trim());
        EditorUtility.SetDirty(skill);
    }

    private static void RemoveByReference(
        List<SkillDefinition> list,
        SkillDefinition skill)
    {
        if (list == null || skill == null)
            return;

        list.RemoveAll(x => x == skill ||
            (x != null && string.Equals(x.SkillId, skill.SkillId, StringComparison.Ordinal)) ||
            (x != null && string.Equals(x.SkillName, skill.SkillName, StringComparison.Ordinal)));
    }

    private static void UpsertBySkillId(
        List<SkillDefinition> list,
        SkillDefinition skill)
    {
        if (list == null || skill == null)
            return;

        int index = list.FindIndex(x => x != null &&
            (ReferenceEquals(x, skill) ||
             string.Equals(x.SkillId, skill.SkillId, StringComparison.Ordinal) ||
             string.Equals(x.SkillName, skill.SkillName, StringComparison.Ordinal)));

        if (index >= 0)
            list[index] = skill;
        else
            list.Add(skill);
    }

    private static void SetSkillId(SkillDefinition skill, string id)
    {
        if (skill == null || string.IsNullOrWhiteSpace(id))
            return;

        SerializedObject so = new SerializedObject(skill);
        SerializedProperty property = so.FindProperty("skillId");
        if (property != null)
        {
            property.stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(skill);
    }

    private static T EnsureAsset<T>(string path)
        where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
