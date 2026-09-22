#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0922 Phase 8 — Hifumi mechanics/content migration.
/// Confirmed structure is migrated; canonical (미정) numbers remain explicit PENDING sentinels.
/// </summary>
public static class Canonical0922Phase8HifumiMigration
{
    private const string LoadoutPath =
        "Assets/2. Data/Characters/Design2026/Hifumi/Hifumi_Loadout.asset";

    private const string Root =
        "Assets/2. Data/Progression/Canonical0922/Phase8/Hifumi";

    [MenuItem("Game System Verification/0922 Canonical/Phase 8 - Apply Hifumi Migration")]
    public static void ApplyFromMenu()
    {
        EnsureFolder(Root);

        CharacterCombatLoadout loadout =
            AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(LoadoutPath);

        if (loadout == null)
        {
            Debug.LogError("[0922 Phase8] Hifumi loadout missing: " + LoadoutPath);
            return;
        }

        Dictionary<string, SkillDefinition> skills =
            loadout.EnumerateAllDefinitions()
                .Where(x => x != null)
                .GroupBy(x => x.SkillId)
                .ToDictionary(x => x.Key, x => x.First());

        int touched = 0;

        SkillDefinition a = Find(skills, HifumiSkillIds.Yukcham);
        if (a != null)
        {
            ConfigureFixed(a, 4, 1, 12,
                PhysicalDamageType.Cut,
                PhysicalDamageType.Cut,
                PhysicalDamageType.Pierce,
                PhysicalDamageType.Blunt);
            Mark(a, "A 육참: 4굴림·빛1·기본12, (반격). 반격 성공 보상은 현재 뼈 구간 기준 +(구간+1)*20.");
            touched++;
        }

        SkillDefinition b = Find(skills, HifumiSkillIds.Goldan);
        if (b != null)
        {
            ConfigureFixed(b, 2, 1, 20,
                PhysicalDamageType.Cut,
                PhysicalDamageType.Cut);
            Mark(b, "B 골단: 2굴림·빛1·기본20, 실제 뼈 구간당 자기 기본위력 -4. 만개 반격 x2/회복350/즉시 약화/조건부 파괴/기세+25, 반격 발동 시 뼈 전량 소모.");
            touched++;
        }

        SkillDefinition c = Find(skills, HifumiSkillIds.RecklessBet);
        if (c != null)
        {
            ConfigureFixed(c, 5, 2, 13,
                PhysicalDamageType.Pierce,
                PhysicalDamageType.Pierce,
                PhysicalDamageType.Pierce,
                PhysicalDamageType.Pierce,
                PhysicalDamageType.Pierce);
            Mark(c, "C 무모한 베팅: 5굴림·빛2·기본13. 사용시 보유(실제+가불) 70 미만이면 뼈+100+다음턴 받는 HP피해 +3/교환, 70 이상이면 뼈40 비용.");
            touched++;
        }

        SkillDefinition e = ResolveOrCreateDuelAsset(
            skills,
            HifumiSkillIds.RaiseStake,
            "판돈을 올리다",
            "E_RaiseStake.asset");
        if (e != null)
        {
            ConfigureRollCount(e, 2);
            e.OverrideEnergyCost = true;
            e.EnergyCost = 0;
            Mark(e,
                "E 판돈을 올리다: 시작 빛0. 교환 승리마다 전투 동안 위력+1, 패배마다 빛 비용+1. 비용4 도달 이후 사용 불가. " +
                "PENDING_CANONICAL: 최초 기본위력은 미정이며 비용 증가로 기본위력을 재계산하지 않음.");
            touched++;
        }

        SkillDefinition g = ResolveOrCreateDuelAsset(
            skills,
            HifumiSkillIds.DoubleOrNothing,
            "곱절을 노리다",
            "G_DoubleOrNothing.asset");
        if (g != null)
        {
            ConfigureRollCount(g, 3);
            MarkPending(g,
                "G 곱절을 노리다: 3굴림. 각 굴림 전 고정 K 베팅, 승리 +3K/패배 -K/보유<K면 그 굴림 베팅 없음. K·빛·기본위력은 (미정).");
            touched++;
        }

        SkillDefinition h = ResolveOrCreateDuelAsset(
            skills,
            HifumiSkillIds.ShakeFate,
            "운명을 흔들다",
            "H_ShakeFate.asset");
        if (h != null)
        {
            ConfigureRollCount(h, 2);
            MarkPending(h,
                "H 운명을 흔들다: 2굴림. Moku/Blank 정상, Shigoro 자동승리, Arashi 자동승리+상대 다음 굴림1 무효, 1·2·3은 판정값0 정상비교이며 공통 대실패 부가효과 제외. 빛/기본위력 (미정).");
            touched++;
        }

        SkillDefinition i = ResolveOrCreateDuelAsset(
            skills,
            HifumiSkillIds.EngraveBody,
            "몸에 새기다",
            "I_EngraveBody.asset");
        if (i != null)
        {
            ConfigureFixed(i, 1, 0, 15, PhysicalDamageType.Cut);
            MarkPending(i,
                "I 몸에 새기다: 런 영구 1→4굴림, 빛 0/0/1/1, 승리축/패배축 첫 임계 도달 branch lock. Stage1 기본15·(반격), 전 단계 절단. Stage2~4 수치/임계치는 (미정)이며 RunProgressionState에 진행을 저장.");
            touched++;
        }

        SkillDefinition j = EnsureDuelAsset(
            HifumiSkillIds.RollCompendium,
            "굴림 도감",
            "J_RollCompendium.asset");
        ConfigurePendingBase(j, 3, 2, SkillColor.Blue);
        MarkPending(j,
            "J 굴림 도감: 3굴림·BLUE·빛2·(반격). 매칭 중 1·2·3 하나당 그 합의 모든 반격 위력 +1(중첩). 기본위력/기타 보너스는 (미정).");
        touched++;

        SkillDefinition k = EnsureDuelAsset(
            HifumiSkillIds.FlipTable,
            "판을 엎다",
            "K_FlipTable.asset");
        ConfigureBlockedPending(k, 3, SkillColor.Red);
        MarkPending(k,
            "K 판을 엎다: RED 3굴림. 사용시 우세 기세 전량→0 후 X 참조, 굴림별 위력/뼈/골절 및 뼈 비용 미지불시 자기 침체. N/T·비용·기본위력 등 (미정)이라 runtime 사용 잠금.");
        touched++;

        SkillDefinition l = EnsureDuelAsset(
            HifumiSkillIds.Provoke,
            "도발형",
            "L_Provoke.asset");
        ConfigureBlockedPending(l, 2, SkillColor.Blue);
        MarkPending(l,
            "L 도발형: BLUE 2굴림. 사용시 자기 부위 도발, 승리시 다음턴 견고 + 임시 키워드 핏값. 지속/N/빛/기본위력/핏값 정의가 (미정)이라 runtime 사용 잠금.");
        touched++;

        SkillDefinition m = EnsureDuelAsset(
            HifumiSkillIds.CleanseAll,
            "전체 정화형",
            "M_CleanseAll.asset");
        ConfigureBlockedPending(m, 1, SkillColor.Unset);
        MarkPending(m,
            "M 전체 정화형: 뼈 비용 지불 후 캐릭터 상태 전부 제거(부위 상태 제외). 굴림수/빛/기본위력/뼈 비용 (미정)이라 runtime 사용 잠금.");
        touched++;

        // Normal canonical structural notes.
        SkillDefinition small = Find(skills, HifumiSkillIds.SmallChange);
        if (small != null)
        {
            MarkPending(small, "평타① 푼돈 걸기: 2굴림·빛0·(반격), 반격은 일반공격식 기본4+1+친치로. 기본위력은 (미정).");
            touched++;
        }

        SkillDefinition bold = Find(skills, HifumiSkillIds.BoldJudgment);
        if (bold != null)
        {
            Mark(bold, "평타② 과감한 판단: 3굴림·빛1·기본14. 사용시 뼈40(부족시 뼈+20), 다음턴 속도-1. 합 동안 HP/흐트러짐 피해를 한 번도 받지 않으면 뼈+50.");
            touched++;
        }

        SkillDefinition mutual = Find(skills, HifumiSkillIds.MutualDestruction);
        if (mutual != null)
        {
            MarkPending(mutual, "평타③ 동귀어진: 자기 고정피해 N(뼈 적립)+상대 동일 N 확정피해 구조만 확정. N/굴림/빛/기본위력/타입은 (미정).");
            touched++;
        }

        SkillDefinition allIn = Find(skills, HifumiSkillIds.AllIn);
        if (allIn != null)
        {
            Mark(allIn, "위세2 올인: 실제 뼈 전량 wager, 친치로 3회. 대실패 하나라도 Bone0 우선 / Arashi·Shigoro=500 / Moku=wager+100 / Blank=0.");
            touched++;
        }

        SkillDefinition trick = Find(skills, HifumiSkillIds.Trick);
        if (trick != null)
        {
            Mark(trick, "위세3 속임수: 그 턴 모든 히후미 친치로를 Arashi 또는 1·2·3으로 고정. 1·2·3은 일반 공통 대실패 규칙을 따른다(H 카드만 예외).");
            touched++;
        }

        // Build the exact 0922 explicit Duel pool; D/F stay as legacy assets only.
        Dictionary<string, SkillDefinition> finalMap =
            loadout.EnumerateAllDefinitions()
                .Where(x => x != null)
                .Concat(new[] { e, g, h, i, j, k, l, m })
                .Where(x => x != null)
                .GroupBy(x => x.SkillId)
                .ToDictionary(x => x.Key, x => x.First());

        List<SkillDefinition> canonical = new();
        foreach (string id in HifumiSkillIds.CanonicalDuel)
        {
            if (finalMap.TryGetValue(id, out SkillDefinition definition) && definition != null)
                canonical.Add(definition);
        }

        loadout.DuelSkillPool ??= new List<SkillDefinition>();
        loadout.DuelSkillPool.Clear();
        loadout.DuelSkillPool.AddRange(canonical);

        loadout.DuelSkills ??= new List<SkillDefinition>();
        loadout.DuelSkills.RemoveAll(x =>
            x == null || HifumiSkillIds.IsLegacyRemovedDuel(x.SkillId));
        foreach (SkillDefinition definition in canonical)
        {
            if (loadout.DuelSkills.Count >= CharacterCombatLoadout.DuelLimit)
                break;
            if (!loadout.DuelSkills.Contains(definition))
                loadout.DuelSkills.Add(definition);
        }

        if (loadout.Catalog != null)
        {
            List<SkillDefinition> catalog =
                loadout.Catalog.Skills
                    .Where(x => x != null && !HifumiSkillIds.IsLegacyRemovedDuel(x.SkillId))
                    .Concat(new[] { e, g, h, i, j, k, l, m })
                    .Where(x => x != null)
                    .Distinct()
                    .ToList();
            loadout.Catalog.ReplaceAll(catalog);
            EditorUtility.SetDirty(loadout.Catalog);
        }

        EditorUtility.SetDirty(loadout);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[0922 Phase8] Migration applied. HifumiTouched={touched}, " +
            $"CanonicalDuelPool={canonical.Count}. (미정)은 PENDING_CANONICAL로 보존됩니다. " +
            "이제 Phase 8 Verify를 실행하세요.");
    }

    private static SkillDefinition Find(
        IReadOnlyDictionary<string, SkillDefinition> map,
        string id)
    {
        if (map != null && map.TryGetValue(id, out SkillDefinition result))
            return result;
        return null;
    }

    private static SkillDefinition ResolveOrCreateDuelAsset(
        IReadOnlyDictionary<string, SkillDefinition> map,
        string id,
        string displayName,
        string filename)
    {
        SkillDefinition skill = Find(map, id);

        // Phase 8 R3: E/G/H/I may exist in the project but have fallen out of
        // both the explicit pool and catalog, in which case EnumerateAllDefinitions()
        // cannot discover them. Recover the original asset first so PresentationAsset,
        // UpgradeProfile and other serialized references are preserved.
        if (skill == null)
            skill = FindProjectSkill(id, displayName);

        if (skill == null)
            return EnsureDuelAsset(id, displayName, filename);

        SetSkillId(skill, id);
        skill.SkillName = displayName;
        skill.ActionType = ActionType.Duel;
        skill.ResolverType = SkillResolverType.Chinchiro;
        skill.CanBreakPart = false;
        skill.BreakMode = PartBreakMode.None;
        skill.GainPrestige = true;
        skill.EffectEntries ??= new List<SkillEffectEntry>();
        skill.Effects ??= new List<SkillEffectDefinition>();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static SkillDefinition FindProjectSkill(
        string id,
        string displayName)
    {
        string[] guids = AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/2. Data" });

        // Stable ID is authoritative. Prefer it over a display-name recovery.
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (skill != null && string.Equals(skill.SkillId, id, StringComparison.Ordinal))
                return skill;
        }

        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (skill != null &&
                skill.ActionType == ActionType.Duel &&
                string.Equals(skill.SkillName, displayName, StringComparison.Ordinal))
            {
                return skill;
            }
        }

        return null;
    }

    private static SkillDefinition EnsureDuelAsset(
        string id,
        string displayName,
        string filename)
    {
        string path = Root + "/" + filename;
        SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<SkillDefinition>();
            AssetDatabase.CreateAsset(skill, path);
        }

        SetSkillId(skill, id);
        skill.SkillName = displayName;
        skill.ActionType = ActionType.Duel;
        skill.ResolverType = SkillResolverType.Chinchiro;
        skill.CanBreakPart = false;
        skill.BreakMode = PartBreakMode.None;
        skill.GainPrestige = true;
        skill.EffectEntries ??= new List<SkillEffectEntry>();
        skill.Effects ??= new List<SkillEffectDefinition>();
        skill.EffectEntries.Clear();
        skill.Effects.Clear();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static void ConfigureFixed(
        SkillDefinition skill,
        int rollCount,
        int energy,
        int basePower,
        params PhysicalDamageType[] physical)
    {
        if (skill == null) return;
        skill.ActionType = ActionType.Duel;
        skill.ResolverType = SkillResolverType.Chinchiro;
        skill.OverrideEnergyCost = true;
        skill.EnergyCost = Mathf.Max(0, energy);
        skill.BasePowerRule = SkillBasePowerRule.FixedBase;
        skill.BasePower = Mathf.Max(0, basePower);
        ConfigureRolls(skill, rollCount, physical);
        EditorUtility.SetDirty(skill);
    }

    private static void ConfigurePendingBase(
        SkillDefinition skill,
        int rollCount,
        int energy,
        SkillColor color)
    {
        if (skill == null) return;
        skill.ActionType = ActionType.Duel;
        skill.ResolverType = SkillResolverType.Chinchiro;
        skill.Color = color;
        skill.OverrideEnergyCost = true;
        skill.EnergyCost = Mathf.Max(0, energy);
        skill.BasePowerRule = SkillBasePowerRule.FixedBase;
        skill.BasePower = 0; // PENDING sentinel, not a canonical gameplay value.
        ConfigureRolls(skill, rollCount, null);
        EditorUtility.SetDirty(skill);
    }

    private static void ConfigureBlockedPending(
        SkillDefinition skill,
        int placeholderRollCount,
        SkillColor color)
    {
        if (skill == null) return;
        skill.ActionType = ActionType.Duel;
        skill.ResolverType = SkillResolverType.Chinchiro;
        skill.Color = color;
        skill.OverrideEnergyCost = true;
        skill.EnergyCost = 0; // runtime is blocked until canonical cost exists.
        skill.BasePowerRule = SkillBasePowerRule.FixedBase;
        skill.BasePower = 0;
        ConfigureRolls(skill, Mathf.Max(1, placeholderRollCount), null);
        EditorUtility.SetDirty(skill);
    }

    private static void ConfigureRollCount(SkillDefinition skill, int count)
    {
        ConfigureRolls(skill, count, null);
    }

    private static void ConfigureRolls(
        SkillDefinition skill,
        int count,
        IReadOnlyList<PhysicalDamageType> physical)
    {
        if (skill == null) return;
        count = Mathf.Clamp(count, 1, 8);
        skill.Rolls ??= new List<SkillRollData>();
        while (skill.Rolls.Count < count)
            skill.Rolls.Add(new SkillRollData());
        while (skill.Rolls.Count > count)
            skill.Rolls.RemoveAt(skill.Rolls.Count - 1);

        for (int index = 0; index < skill.Rolls.Count; index++)
        {
            skill.Rolls[index] ??= new SkillRollData();
            SkillRollData roll = skill.Rolls[index];
            roll.Index = index;
            if (physical != null && index < physical.Count)
            {
                roll.OverridePhysicalType = true;
                roll.PhysicalType = physical[index];
            }
        }
        skill.ExchangeRollCount = count;
        EditorUtility.SetDirty(skill);
    }

    private static void Mark(SkillDefinition skill, string text) =>
        SetDescription(skill, "[CANONICAL_0922_PHASE8] ", text);

    private static void MarkPending(SkillDefinition skill, string text) =>
        SetDescription(skill, "[PENDING_CANONICAL][0922_PHASE8] ", text);

    private static void SetDescription(
        SkillDefinition skill,
        string prefix,
        string text)
    {
        if (skill == null) return;
        string body = string.Join("\n", (skill.Description ?? string.Empty)
            .Split(new[] { '\n' }, StringSplitOptions.None)
            .Where(x =>
                !x.StartsWith("[CANONICAL_0922_PHASE8]", StringComparison.Ordinal) &&
                !x.StartsWith("[PENDING_CANONICAL][0922_PHASE8]", StringComparison.Ordinal)));
        skill.Description = prefix + text +
            (string.IsNullOrWhiteSpace(body) ? string.Empty : "\n" + body.Trim());
        EditorUtility.SetDirty(skill);
    }

    private static void SetSkillId(SkillDefinition skill, string id)
    {
        SerializedObject so = new SerializedObject(skill);
        SerializedProperty property = so.FindProperty("skillId");
        if (property != null)
        {
            property.stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(skill);
    }

    private static void EnsureFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }
}
#endif
