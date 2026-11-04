using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Player-facing presentation for the three playable characters.
/// Technical asset notes remain in SkillDefinition.Description for authoring
/// and migration audits; they must not leak into runtime tooltips.
/// No skill effects, costs, rolls, IDs or ScriptableObjects are mutated.
/// </summary>
public static class BattleSkillDescriptionFormatter
{
    private static readonly Dictionary<string, string> Overview =
        new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // ── 빌야르 (legacy code name: Olaf) ──
        ["olaf.duel.absorb_blood"] = "대상 부위의 출혈이 5 이상이면 출혈 5를 제거하고 빛 2를 회복합니다.",
        ["olaf.duel.all_in"] = "1굴림: 출혈 2 부여.\n2굴림: 쇠약 1을 1턴 부여.\n3굴림이 합에 참여하면 상대 방어도를 모두 제거합니다.",
        ["olaf.duel.blood_charge"] = "합 승리 시 출혈 1을 부여하고 다음 턴 골절 1을 1턴 예약합니다.\n합 패배 시 광기 2를 얻습니다.",
        ["olaf.duel.blood_oath"] = "출혈 5~9 구간에서는 위력 +4, 재생 4(3턴), 출혈 3 및 부위 파괴 효과가 적용됩니다.\n출혈 10 이상에서는 위력 +8, 출혈 추가 부여 및 빛 회복으로 강화됩니다.",
        ["olaf.duel.blood_price"] = "사용 시 자신에게 출혈 4를 부여합니다.\n2굴림이 합에 참여하면 자신의 출혈 구간에 따라 다음 턴 힘 1~3을 3턴 얻습니다.",
        ["olaf.duel.fortify"] = "상대 출혈량에 따라 다음 턴 힘을 얻습니다.\n출혈 0~4: 힘 1 / 5~9: 힘 2 / 10 이상: 힘 3 (각 1턴).",
        ["olaf.duel.head_on"] = "부위를 파괴할 수 있는 정면 결투입니다.\n교환 승리 시 실제로 가한 피해의 10%를 자신도 받습니다.",
        ["olaf.duel.hold_on"] = "교환 승리 시 다음 턴 대상에게 균열 2를 2턴 부여합니다.",
        ["olaf.duel.honorable_fight"] = "서로 한 번씩 굴리는 결투를 반복해 한쪽 부위를 약화시키는 기술입니다.",
        ["olaf.duel.payback_all"] = "사용할 때 기세가 -30 이하라면 이 스킬의 모든 굴림 위력이 4 증가합니다.",
        ["olaf.duel.protect_self"] = "다음 턴 자신에게 보호 1을 1턴 부여합니다.",
        ["olaf.duel.rend"] = "상대 결투와 맞붙은 모든 교환에서 승패와 관계없이 출혈 1을 부여합니다.\n교환 패배 시 광기 1을 얻습니다. 일방 공격에는 적용되지 않습니다.",
        ["olaf.duel.single_combat"] = "같은 적 슬롯을 노린 다른 아군 슬롯의 일방 공격을 포기합니다.\n이 결투에서 이기면 대상에게 출혈 1을 부여합니다.",
        ["olaf.duel.single_cut"] = "출혈을 이용한 단발 결투입니다. 출혈 폭발의 세부 피해 계수는 아직 정해지지 않았습니다.",
        ["olaf.duel.standard"] = "교환 승리 시 출혈 1을 추가하고, 패배 시 광기 1을 얻습니다.\n대상 출혈이 10 이상이면 현재 출혈×10 피해로 폭발시키며, 한 번 사용할 때 최대 한 번 발동합니다.",
        ["olaf.duel.tenacious"] = "1·2굴림에서 출혈을 각각 1씩 부여합니다.\n3굴림에서는 자신에게 재생 2를 3턴 부여합니다.",
        ["olaf.duel.whatever_comes"] = "사용할 때마다 이 전투에서의 빛 비용이 1씩 감소합니다(최소 1).\n사용 시 광기 1을 얻으며 부위를 파괴할 수 있습니다.",
        ["olaf.normal.aim_weakness"] = "1굴림: 쇠약 1을 3턴 부여합니다.\n2굴림: 대상 출혈이 5 이상이면 균열 1을 1턴 부여합니다.\n마지막 굴림은 대상의 상태이상에 영향을 받습니다.",
        ["olaf.normal.bite"] = "공격 교환 승리 시 균열·골절·쇠약 중 한 종류를 골라 다음 턴 1을 1턴 부여합니다.",
        ["olaf.normal.block_way"] = "사용 시 방어도를 10 얻습니다.",
        ["olaf.normal.choke"] = "공격 교환 승리 시 다음 턴 대상에게 고통을 3턴 부여합니다.",
        ["olaf.normal.enduring_slash"] = "공격·수비·공격 순서로 굴립니다.\n공격 굴림 승리 시 대상 부위에 출혈을 부여합니다.",
        ["olaf.normal.exploit_gap"] = "1·2굴림이 각각 승리하면 다음 턴 균열 1을 1턴씩 부여합니다.\n마지막 굴림은 흐트러짐에 추가로 영향을 줍니다.",
        ["olaf.normal.overhead_smash"] = "최저 위력이 높은 두 번의 공격으로 상대를 압박합니다.\n상대 결투 효과를 제한하면서 피해를 가합니다.",
        ["olaf.normal.slice_thin"] = "2굴림 승리 시 대상 출혈을 2 추가하고 자신에게도 출혈 1을 부여합니다.",
        ["olaf.normal.wild_hack"] = "빛을 소비하지 않고 세 번 공격해 출혈을 쌓습니다.",
        ["olaf.preparation.blood_oath"] = "자신에게 출혈 3을 부여하는 대신 재생 4를 3턴 얻습니다.",
        ["olaf.preparation.bloto"] = "체력이 가장 낮은 자신의 부위를 약화시키고 광기 1을 얻습니다.\n적에게 공포를 부여해 위력을 3턴 동안 1 낮춥니다.",
        ["olaf.preparation.crouch"] = "현재 방어도를 12로 설정합니다. 이미 가진 방어도와 합산되지 않습니다.",
        ["olaf.preparation.glare"] = "이번 턴 자신의 모든 합 판정값이 1 증가합니다.",
        ["olaf.preparation.stoke_prestige"] = "이번 턴 교환에서 이길 때마다 상대 출혈 구간을 이용해 위세를 모으는 도사림입니다.",
        ["olaf.prestige.backs_to_wall"] = "이번 턴 전신·부위 체력이 1 미만으로 떨어지지 않으며 부위가 파괴되지 않습니다.\n결투 교환에서 피해를 받을 때마다 광기 1을 얻습니다.",
        ["olaf.prestige.blooming_wound"] = "조준 부위에 출혈을 3~5 부여합니다.\n광기 5당 출혈 부여량이 1 증가하며 즉시 폭발하지 않습니다.",
        ["olaf.prestige.bursting_madness"] = "광기 2를 얻고, 현재 광기×5와 대상 출혈량을 합친 만큼 피해를 줍니다.\n출혈을 소비하지 않으며 부위 파괴는 불가능합니다.",

        // ── 명성 (legacy code name: Yujin) ──
        ["yujin.duel.contract_target"] = "결투 교환 승리로 대상의 표식을 누적합니다. 현재 무기에 따라 표식 부여량이 달라집니다.",
        ["yujin.duel.brutal_finish"] = "결투 교환 승리 후 살수의 감을 소비해 추가 공격을 노립니다. 추가 코인은 앞면이면 공격하고 뒷면이면 종료합니다.",
        ["yujin.normal.background_check"] = "현재 무기에 따라 코인을 굴리고, 공격 교환 승리로 대상의 표식을 쌓습니다.",
        ["yujin.normal.catch_breath"] = "살수의 감을 보충하는 일반 공격입니다. 추가 획득량의 별도 명세는 아직 없습니다.",
        ["yujin.normal.confirm_kill"] = "지정된 적을 마무리하는 공격입니다. 지정과 연계되는 추가 기세 수치는 아직 확정되지 않았습니다.",
        ["yujin.preparation.read"] = "이번 턴 코인이 앞면으로 나올 확률을 높이는 도사림입니다(포착 계열).",
        ["yujin.preparation.advance_pay"] = "이번 턴 코인의 뒷면 판정값을 높이는 도사림입니다(양형 계열).",
        ["yujin.prestige.prep_work"] = "표식을 먼저 부여해 이후 공격을 준비합니다. 현재 무기의 표식 규칙이 적용됩니다.",
        ["yujin.prestige.certain_execution"] = "확정 앞면 효과를 활용해 공격을 이어갑니다. 처치·부위 파괴·표식 발화에 따라 연장될 수 있습니다.",
        ["yujin.prestige.flexible_response"] = "이번 턴 코인의 뒷면을 다시 굴리는 효과입니다. 같은 코인의 재굴림은 한 번만 적용됩니다.",
        
["yujin.duel.all_targets"] = "사용 시 모든 적에게 지정 2를 3턴 부여합니다.",
        ["yujin.duel.designate_target"] = "사용 시 목표 부위에 지정 2를 3턴 부여합니다.",
        ["yujin.duel.inscription"] = "결투 교환 승리 시 대상에게 표식을 부여합니다.\n백우 8 / 적설 12 / 낙일 16.\n살수의 감을 사용하도록 선택하면 이길 때까지 또는 감이 소진될 때까지 재굴림합니다.",
        ["yujin.duel.o.advance_timing"] = "살수의 감 4를 소비해 대상 부위 표식을 발화 기준인 44까지 채우고 즉시 발화시킵니다.",
        ["yujin.duel.p.finish_it"] = "전투 중 사용할 때마다 빛 비용이 4→3→2→1로 감소합니다.\n사용 시 살수의 감을 모두 소비하고, 1개당 위력이 2 증가합니다.",
        ["yujin.duel.probe_weakness"] = "교환 승리 시 다음 턴 상대에게 무기에 따른 약화 효과를 부여합니다.\n백우: 쇠약 / 적설: 골절 / 낙일: 균열 (각 1, 1턴).",
        ["yujin.duel.pursuit"] = "결투 교환 승리 후 살수의 감 1을 소비해 추가 코인을 굴립니다.\n앞면이면 추가 공격, 뒷면이면 종료합니다.\n교환당 최대 추가 코인: 백우 1 / 적설 2 / 낙일 5.",
        ["yujin.normal.breakfast"] = "공격 교환 승리마다 살수의 감 1을 얻습니다. 이 스킬 자체에는 기본 표식 부여가 없습니다.",
        ["yujin.normal.inspection"] = "현재 무기에 따라 코인을 굴립니다: 백우 3회 / 적설 2회 / 낙일 1회.\n공격 승리 시 대상에게 표식을 4 / 6 / 8 부여합니다.",
        ["yujin.normal.wanted"] = "공격 교환 승리 시 다음 턴 모든 적에게 무기별 약화를 부여합니다.\n백우: 쇠약 / 적설: 골절 / 낙일: 균열 (각 1, 1턴).",
        ["yujin.preparation.capture"] = "이번 턴 자신의 모든 코인이 앞면으로 나올 확률이 10%p 증가합니다.",
        ["yujin.preparation.hwanhyeong.attack"] = "다음 턴 사용할 다른 무기를 선택합니다.\n교체하는 턴에는 기존 무기의 위력 보너스를 즉시 얻습니다.",
        ["yujin.preparation.hwanhyeong.defense"] = "다음 턴 사용할 다른 무기를 선택합니다.\n교체하는 턴에는 방어도 30을 즉시 얻습니다.",
        ["yujin.preparation.hwanhyeong.baeku"] = "다음 턴부터 무기를 백우로 교체합니다. 이번 턴에는 기존 무기를 사용합니다.",
        ["yujin.preparation.hwanhyeong.jeokseol"] = "다음 턴부터 무기를 적설로 교체합니다. 이번 턴에는 기존 무기를 사용합니다.",
        ["yujin.preparation.hwanhyeong.nakil"] = "다음 턴부터 무기를 낙일로 교체합니다. 이번 턴에는 기존 무기를 사용합니다.",
        ["yujin.preparation.sentencing"] = "이번 턴 자신의 코인이 뒷면일 때 판정값이 2 증가합니다.",
        ["yujin.preparation.sharpen"] = "표식 10을 소비해 다음 턴 신속 1을 1턴 얻습니다.",
        ["yujin.prestige.brand"] = "현재 무기의 크리값만큼 조준 부위에 표식을 부여합니다.\n이번 턴 공격 교환 승리마다 표식 3을 추가합니다. 적설은 두 부위에 적용합니다.",
        ["yujin.prestige.joint_liability"] = "다음 코인 한 개를 확정 앞면으로 만듭니다.\n이번 턴 처치·부위 파괴·표식 발화가 발생하면 추가로 확정 앞면을 얻습니다(최대 4회).",
        ["yujin.prestige.retrial"] = "이번 턴 각 코인의 뒷면을 한 번 자동 재굴림합니다.\n한 교환에서 같은 코인에는 한 번만 적용되며 살수의 감을 소비하지 않습니다.",

        // ── 히후미 ──
        ["hifumi.prestige.gambler_move"] = "위세를 사용하는 히후미 전용 기술입니다. 고유 효과의 구체적인 규칙은 아직 기재되지 않았습니다.",
        ["hifumi.preparation.engrave_bone"] = "뼈와 연관된 도사림 기술입니다. 획득·소비 수치 및 실제 효과는 아직 기재되지 않았습니다.",
        ["hifumi.preparation.give_flesh"] = "방어·피격을 주제로 하는 도사림 기술입니다. 실제 적용되는 수치와 효과는 아직 기재되지 않았습니다.",
        ["hifumi.preparation.fold_hand"] = "판을 접는 도사림 기술입니다. 실제 비용과 고유 효과는 아직 기재되지 않았습니다.",
        
["hifumi.duel.e.raise_stake"] = "교환 승리마다 이 전투에서의 위력이 1 증가합니다.\n패배할 때마다 빛 비용이 1 증가하며, 비용이 4가 되면 더 이상 사용할 수 없습니다.",
        ["hifumi.duel.g.double_or_nothing"] = "각 굴림 전에 뼈를 걸고 친치로로 승부합니다.\n이기면 베팅한 양의 3배를 얻고, 지면 베팅한 양을 잃습니다.",
        ["hifumi.duel.goldan"] = "뼈 구간이 높을수록 기본 위력이 4씩 감소합니다.\n반격이 성공하면 보유 뼈를 모두 소비해 추가 반격·회복·약화 효과를 발동합니다. 상황에 따라 부위 파괴와 기세 증가도 가능합니다.",
        ["hifumi.duel.h.shake_fate"] = "친치로 결과에 따라 승패가 달라집니다.\n시고로·아라시는 자동 승리하며 아라시는 상대의 다음 굴림도 무효화합니다.\n1·2·3은 판정값 0으로 정상 비교합니다.",
        ["hifumi.duel.i.engrave_body"] = "런 진행에 따라 최대 4굴림까지 성장하는 결투입니다.\n승리·패배 중 먼저 조건을 충족한 성장 경로가 고정됩니다.",
        ["hifumi.duel.j.roll_compendium"] = "합에서 친치로 1·2·3이 나올 때마다 해당 합의 반격 위력이 1씩 증가합니다.",
        ["hifumi.duel.k.flip_table"] = "현재 유리한 기세를 소비해 강력한 공격을 시도합니다.\n각 굴림의 추가 효과와 뼈 비용은 확정되지 않았습니다.",
        ["hifumi.duel.l.provoke"] = "자신의 부위로 적의 공격을 유도하는 결투입니다.\n승리 시 다음 턴 견고와 핏값을 얻도록 설계되어 있습니다.",
        ["hifumi.duel.m.cleanse_all"] = "뼈를 소비해 캐릭터에 적용된 상태이상을 제거하는 기술입니다.\n부위에 적용된 상태는 제거하지 않습니다.",
        ["hifumi.duel.reckless_bet"] = "보유한 뼈와 가불이 70 미만이면 뼈 100을 얻지만, 다음 턴 교환마다 받는 체력 피해가 3 증가합니다.\n70 이상이면 뼈 40을 소비합니다.",
        ["hifumi.duel.yukcham"] = "반격이 성공하면 뼈를 (현재 뼈 구간+1)×20 얻습니다.\n친치로 1·2·3 대실패가 나오면 턴당 한 번 체력 60을 잃고 같은 양을 뼈로 응축합니다.",
        ["hifumi.normal.bold_judgment"] = "사용 시 뼈 40을 지불합니다(부족하면 뼈 20 획득). 다음 턴 속도가 1 감소합니다.\n합에서 체력·흐트러짐 피해를 모두 피하면 뼈 50을 얻습니다.",
        ["hifumi.normal.small_change"] = "빛 없이 친치로로 공격하고 반격을 노리는 기술입니다.\n1·2·3 대실패가 나오면 턴당 한 번 자신에게 피해가 발생합니다.",
        ["hifumi.preparation.poker_face"] = "매 교환 받는 피해가 4 감소하고, 피격할 때마다 뼈 4를 얻습니다.",
        ["hifumi.prestige.all_in"] = "실제 뼈를 모두 걸고 친치로를 3회 굴립니다.\n대실패가 하나라도 나오면 뼈 0, 아라시·시고로는 500, 모쿠는 베팅한 뼈+100을 얻습니다.",
        ["hifumi.prestige.trick"] = "이번 턴 히후미의 친치로 결과를 아라시 또는 1·2·3으로 고정합니다.\n1·2·3은 일반 대실패 규칙을 따릅니다."
    };

    private static readonly Dictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["hifumi.duel.l.provoke"] = "도발",
        ["hifumi.duel.m.cleanse_all"] = "상태 정화",
        ["olaf.duel.mad_bite"] = "미쳐 물어뜯기",
        ["olaf.duel.no_retreat"] = "물러서지 않기",
        ["olaf.duel.blood_charge"] = "피의 돌진",
        ["yujin.preparation.hwanhyeong.attack"] = "환형 · 공격",
        ["yujin.preparation.hwanhyeong.defense"] = "환형 · 수비"
    };

    public static string GetDisplayName(Skill skill)
    {
        if (skill == null) return "스킬";
        string id = skill.Definition?.SkillId;
        if (!string.IsNullOrEmpty(id) &&
            DisplayNames.TryGetValue(id, out string label))
            return label;

        string name = skill.SkillName;
        if (string.IsNullOrWhiteSpace(name))
            name = skill.Definition?.SkillName;

        if (string.IsNullOrWhiteSpace(name))
            return "이름 없는 스킬";

        name = name.Trim();
        foreach (string suffix in new[]{
            "(일반공격)", "(결투)", "(도사림)", "(위세)"})
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
                return name.Substring(0, name.Length - suffix.Length).TrimEnd();
        }
        return name;
    }

    public static bool HasUnconfirmedDetails(Skill skill)
    {
        string raw = skill?.Definition?.Description ?? string.Empty;
        return raw.Contains("PENDING_CANONICAL") ||
               raw.Contains("(미정)") ||
               raw.Contains("미정이라");
    }

    public static string GetOverview(Skill skill)
    {
        if (skill == null) return "스킬을 선택하세요.";
        SkillDefinition definition = skill.Definition;
        string id = definition?.SkillId ?? string.Empty;
        string raw = definition?.Description ?? string.Empty;

        if (Overview.TryGetValue(id, out string defined))
        {
            // An older provisional card shares the same SkillId with a
            // confirmed runtime definition. Its pending note wins on scope:
            // never show an unconfirmed explosion coefficient as fixed.
            if (id == "olaf.duel.standard" &&
                HasUnconfirmedDetails(skill))
                return "교환 승리 시 출혈을 부여하고 패배 시 광기를 얻습니다.\n출혈 폭발의 피해 계수는 아직 확정되지 않았습니다.";
            return defined;
        }

        if (!LooksLikeTechnicalNote(raw))
            return raw.Trim();

        List<string> effects = ExtractAppliedEffects(definition);
        if (effects.Count > 0)
            return "현재 적용되는 효과:\n• " +
                   string.Join("\n• ", effects.Take(4));

        return "전투 계획에 따라 사용하는 " +
               BattleSkillUiText.GetActionTypeName(skill.ActionType) +
               " 스킬입니다.\n추가 효과의 상세 설명은 아직 정리되지 않았습니다.";
    }

    private static bool LooksLikeTechnicalNote(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        foreach (string flag in new[]{
            "[CANONICAL_", "[TEMP_", "[PENDING_", "[0917_",
            "[0922_", "Phase D", "proxy", "metadata synced",
            "runtime hook", "TEMP rider"})
            if (raw.IndexOf(flag, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        return false;
    }

    private static List<string> ExtractAppliedEffects(SkillDefinition definition)
    {
        var list = new List<string>();
        if (definition == null) return list;
        foreach (SkillEffectEntry entry in definition.EnumerateEffectEntries())
        {
            if (entry?.Definition == null) continue;
            string label = DescribeEffect(entry);
            if (string.IsNullOrWhiteSpace(label) || list.Contains(label))
                continue;
            list.Add(label);
        }
        return list;
    }

    private static string DescribeEffect(SkillEffectEntry entry)
    {
        SkillEffectDefinition effect = entry.Definition;
        if (effect is TempBalanceSkillProxyEffectDefinition proxy)
        {
            int amount = entry.Overrides?.ResolveAmount(proxy.BaseAmount) ??
                         proxy.BaseAmount;
            switch (proxy.Operation)
            {
                case ProvisionalBalanceSkillProxyOperation.GainBlock:
                    return "자신에게 방어도 " + amount + " 부여";
                case ProvisionalBalanceSkillProxyOperation.GainEnergy:
                    return "빛 " + amount + " 회복";
                case ProvisionalBalanceSkillProxyOperation.GainPrestige:
                    return "위세 " + amount + " 획득";
                case ProvisionalBalanceSkillProxyOperation.OlafMadness:
                    return "광기 " + amount + " 획득";
                case ProvisionalBalanceSkillProxyOperation.TargetBleeding:
                    return "대상 부위에 출혈 " + amount + " 부여";
                case ProvisionalBalanceSkillProxyOperation.YujinSense:
                    return "살수의 감 " + amount + " 획득";
                case ProvisionalBalanceSkillProxyOperation.YujinMark:
                    return "대상에게 표식 " + amount + " 부여";
                case ProvisionalBalanceSkillProxyOperation.YujinMarkTrap:
                    return "대상 표식에 함정 효과 적용";
                case ProvisionalBalanceSkillProxyOperation.YujinMarkDeadline:
                    return "표식에 발동 기한 설정";
                case ProvisionalBalanceSkillProxyOperation.TurnClashPowerBonus:
                    return "이번 턴 합 위력 " + amount + " 증가";
            }
        }
        if (effect is Yujin0916SkillEffectDefinition yujin)
        {
            switch (yujin.Operation)
            {
                case Yujin0916EffectOperation.LedgerCleanup:
                    return "대상 표식 20을 소비해 빛 1 회복";
                case Yujin0916EffectOperation.StepBackBlock:
                    return "방어도 10 획득";
                case Yujin0916EffectOperation.AimCriticalRider:
                    return "치명타 시 현재 무기에 따른 추가 효과";
                case Yujin0916EffectOperation.ConfirmKillMomentum:
                    return null; // Old operation is disabled in runtime.
                case Yujin0916EffectOperation.FamiliarHand:
                    return "이번 턴 무기를 교체했다면 살수의 감 1 획득";
                case Yujin0916EffectOperation.AceInTheHole:
                    return "살수의 감을 소비해 다음 코인을 확정 앞면으로 변경";
                case Yujin0916EffectOperation.Wanted:
                    return "모든 적에게 무기별 약화 효과 예약";
                case Yujin0916EffectOperation.DesignateCurrent:
                    return "대상에게 지정 2를 3턴 부여";
                case Yujin0916EffectOperation.IntoShadowsBlock:
                    return "방어도 15 획득";
                case Yujin0916EffectOperation.GainSense:
                    return "살수의 감 " + Math.Max(1,yujin.Amount) + " 획득";
                case Yujin0916EffectOperation.CutThroatMark:
                    return "대상 부위에 표식 20 부여";
                case Yujin0916EffectOperation.SpreadRumorMark:
                    return "모든 적에게 표식 8 부여";
                case Yujin0916EffectOperation.ProbeWeakness:
                    return "대상에게 무기별 약화 효과 예약";
                case Yujin0916EffectOperation.Trap:
                    return "대상 표식에 함정 20 설정";
                case Yujin0916EffectOperation.Deadline:
                    return "대상 표식에 발동 기한 설정";
                case Yujin0916EffectOperation.DesignateAll:
                    return "모든 적에게 지정 2를 3턴 부여";
                case Yujin0916EffectOperation.Period:
                    return "표식을 이용해 추가 효과 발동";
                case Yujin0916EffectOperation.HoldBreath:
                    return "표식 10을 소비해 방어도 20 획득";
                case Yujin0916EffectOperation.Sharpen:
                    return "표식 10을 소비해 다음 턴 신속 1 획득";
            }
        }
        if (effect is Olaf0922Phase7SkillEffectDefinition olaf)
        {
            string statusName = BattleKeywordGlossary.GetStatusEffectDisplayName(
                olaf.StatusId);
            switch (olaf.Operation)
            {
                case Olaf0922Phase7EffectOperation.RandomTargetDebuffNextTurn:
                    return "교환 승리 시 다음 턴 무작위 약화 효과 예약";
                case Olaf0922Phase7EffectOperation.TargetStatusNextTurn:
                    return "다음 턴 대상에게 " + statusName +
                           " " + olaf.Amount + " 부여";
                case Olaf0922Phase7EffectOperation.OwnerStatusNextTurn:
                    return "다음 턴 자신에게 " + statusName +
                           " " + olaf.Amount + " 부여";
                case Olaf0922Phase7EffectOperation.OwnerStatusImmediate:
                    return "자신에게 " + statusName + " " + olaf.Amount + " 부여";
                case Olaf0922Phase7EffectOperation.TargetBleeding:
                    return "대상에게 출혈 " + olaf.Amount + " 부여";
                case Olaf0922Phase7EffectOperation.OwnerBleeding:
                    return "자신에게 출혈 " + olaf.Amount + " 부여";
                case Olaf0922Phase7EffectOperation.OwnerBlock:
                    return "방어도 " + olaf.Amount + " 획득";
                case Olaf0922Phase7EffectOperation.ClearTargetBlock:
                    return "대상의 방어도 제거";
                case Olaf0922Phase7EffectOperation.BloodPriceStrengthByOwnerBleeding:
                    return "자신의 출혈에 따라 힘 획득";
                case Olaf0922Phase7EffectOperation.ConsumeTargetBleedingForEnergy:
                    return "대상 출혈을 소비해 빛 회복";
                case Olaf0922Phase7EffectOperation.PrepareBloodOathDuel:
                case Olaf0922Phase7EffectOperation.ResolveBloodOathDuelWin:
                    return "출혈 구간에 따른 결투 추가 효과";
                case Olaf0922Phase7EffectOperation.AddMadness:
                    return "광기 " + olaf.Amount + " 획득";
                case Olaf0922Phase7EffectOperation.StrengthByTargetBleedingBandNextTurn:
                    return "대상 출혈 구간에 따라 다음 턴 힘 획득";
                case Olaf0922Phase7EffectOperation.TargetStatusNextTurnIfBleedingAtLeast:
                    return "출혈 조건을 만족하면 다음 턴 " + statusName + " 부여";
            }
        }
        if (effect is AddBodyPartStatusEffect add)
            return BattleKeywordGlossary.GetStatusEffectDisplayName(
                add.StatusEffectId) + " 부여";
        if (effect is GainPrestigeEffect)
            return "위세 획득";
        if (effect is GainCustomResourceEffect gain)
            return "전용 자원 " + (gain.ResourceKey??"") + " 획득";
        return null;
    }

    public static bool IsTechnicalLeak(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return Regex.IsMatch(text,
            @"\[PENDING_CANONICAL\]|\[CANONICAL_\w+\]|\[TEMP_BALANCE|Phase\s*[A-Z]\s*(?:rider|proxy)|SkillEffectDefinition|(?:\bLegacy|\bTODO\b)|metadata synced",
            RegexOptions.IgnoreCase);
    }
}