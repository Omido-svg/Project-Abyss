using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Read-only, player-facing battle passive/augment descriptions.
/// Keep gameplay contracts in their original mechanics and SO assets.
/// Only builds text when the detail panel's Passives tab is opened.
/// </summary>
public static class BattlePassiveDescriptionFormatter
{
    public sealed class Entry
    {
        public readonly string Title;
        public readonly string ListLabel;
        public readonly string Description;

        public Entry(string title, string category, string summary, string description)
        {
            Title = title;
            ListLabel = $"<color=#AFCBF1>{category}</color> · {title}" +
                        (string.IsNullOrWhiteSpace(summary) ? "" : "\n" + summary);
            Description = description;
        }
    }

    public static List<Entry> Build(Character character)
    {
        var result = new List<Entry>();
        if (character == null)
            return result;

        var mechanics = character.Mechanics;
        var shown = new HashSet<CombatMechanic>();

        // The fixed passive has a *designed name* independent of the internal
        // CombatMechanic implementation class and its helper components.
        foreach (CharacterFixedPassiveContract.Entry fixedPassive
                 in CharacterFixedPassiveContract.FixedPassives)
        {
            if (!fixedPassive.OwnerType.IsInstanceOfType(character))
                continue;

            CombatMechanic source = mechanics?.FirstOrDefault(
                m => m != null && fixedPassive.MechanicType.IsInstanceOfType(m));
            if (source == null)
                continue;

            result.Add(BuildFixed(character, source, fixedPassive.PassiveName));
            shown.Add(source);
        }

        // Character augments can grant static stats without creating a runtime
        // mechanic. They still deserve a player-facing row.
        if (character.EquippedAugments != null)
        {
            foreach (CharacterAugment augment in character.EquippedAugments)
            {
                if (augment == null || augment is YujinHwanhyeongFlowPassive)
                    continue; // disabled legacy asset; grants no effect
                string name = SafeName(augment.AugmentName, "증강 효과");
                string explanation = SafeDescription(augment.Description,
                    "장착한 증강에 의해 전투 능력이 변경됩니다.");
                result.Add(new Entry(name, "장착 증강", "장착 시 적용", Body(
                    explanation, "장착 효과")));
            }
        }

        if (mechanics != null)
        {
            foreach (CombatMechanic mechanic in mechanics)
            {
                if (mechanic == null || shown.Contains(mechanic) ||
                    IsImplementationOnly(mechanic))
                    continue;

                Entry additional = BuildAdditional(mechanic, character);
                if (additional != null)
                    result.Add(additional);
            }
        }
        return result;
    }

    private static Entry BuildFixed(Character character, CombatMechanic mechanic,
        string canonicalName)
    {
        if (character is Olaf && mechanic is OlafMadnessMechanic olaf)
        {
            string text = Body(
                "<b><color=#E6C68A>발동 조건</color></b>\n" +
                "• 공격 굴림에 승리하고 실제 피해를 입히면 대상 부위에 출혈 1을 부여합니다.\n" +
                "• 푸른 일반공격의 합에서 승리하면 광기 1을 얻습니다.\n" +
                "• 결투 교환에서 패배하거나, 자신 또는 적의 부위가 새로 파괴되면 광기 1을 얻습니다.\n\n" +
                "<b><color=#E6C68A>광기 단계 효과</color></b>\n" +
                "• 광기 0~3: 추가 위력·피해 보정 없음\n" +
                "• 광기 4~7: 굴림 위력 +1, 받는 피해 +1\n" +
                "• 광기 8: 굴림 위력 +2, 받는 피해 +2, 공격 적중 시 출혈 2 부여\n\n" +
                "<b><color=#AFCBF1>현재 상태</color></b>\n" +
                $"광기 {olaf.CurrentMadness}/{OlafMadnessMechanic.MaxMadnessValue}" +
                $" · { (olaf.IsBlooming ? "만개" : olaf.MadnessStage == 0 ? "기본" : "광기 "+olaf.MadnessStage+"단계") }",
                "고유 패시브");
            return new Entry(canonicalName, "고유 패시브",
                "출혈 적중 · 광기 축적 · 위력 강화", text);
        }

        if (character is Yujin && mechanic is YujinMechanic yujin)
        {
            var weapon = yujin.CurrentWeaponProfile;
            string weaponName = WeaponName(yujin.CurrentWeapon);
            string text = Body(
                "<b><color=#E6C68A>발동 조건</color></b>\n" +
                $"• 매 턴 시작 시 살수의 감 {CharacterFixedPassiveContract.YujinTurnSenseGain}을 얻습니다.\n" +
                "• 적의 부위를 파괴하거나 직접 적을 처치하면 살수의 감 1을 얻습니다.\n" +
                "• 결투 공격의 합에서 이기면 대상에게 현재 무기의 결투 표식을 부여합니다.\n" +
                "• 붉은 일반공격의 합에서 이기면 현재 무기의 일반공격 표식을 부여합니다.\n" +
                $"• 대상 또는 부위에 표식이 {YujinMechanic.MarkIgnitionThreshold} 쌓이면 무기별 발화 효과가 발생합니다.\n\n" +
                "<b><color=#E6C68A>현재 무기</color></b>\n" +
                $"{weaponName} · 일반공격 승리 시 표식 {weapon.NormalMarkAmount}" +
                $" / 결투 승리 시 표식 {weapon.DuelMarkAmount}\n\n" +
                "<b><color=#AFCBF1>현재 상태</color></b>\n" +
                $"살수의 감 {yujin.Sense}",
                "고유 패시브");
            return new Entry(canonicalName, "고유 패시브",
                "표식 누적·발화 · 살수의 감 획득", text);
        }

        if (character is Hifumi && mechanic is HifumiMechanic hifumi)
        {
            string text = Body(
                "<b><color=#E6C68A>발동 조건</color></b>\n" +
                "• 실제로 체력 피해를 받으면 피해량과 이번 턴의 핏값만큼 뼈를 얻습니다.\n" +
                "• 반격 특성이 있는 기술로 합에서 지면 해당 교환 수만큼 친치로 반격을 시도합니다.\n" +
                $"• 친치로가 1·2·3 대실패이면 체력 {HifumiMechanic.HifumiSelfDamage}을 잃고 뼈 {HifumiNativeRules.CatastropheDirectBone}을 얻으며 반격 1회를 추가합니다.\n" +
                "• 턴 종료 기세가 열세·짓눌림일 때는 고조를 최소 3 획득합니다.\n\n" +
                "<b><color=#E6C68A>뼈와 만개</color></b>\n" +
                $"• 뼈는 최대 {HifumiMechanic.MaxBone}까지 쌓이며, {HifumiMechanic.BonePerTier}마다 한 구간으로 계산합니다.\n" +
                "• 가불은 비용 지불과 만개 판정에는 포함되지만 뼈 구간에는 포함되지 않습니다.\n" +
                $"• 뼈와 가불의 합이 {HifumiMechanic.BloomThreshold} 이상이면 만개합니다.\n" +
                "• 만개 중 골단의 반격 횟수는 두 배가 됩니다.\n\n" +
                "<b><color=#AFCBF1>현재 상태</color></b>\n" +
                $"뼈 {hifumi.Bone}/{HifumiMechanic.MaxBone} · 가불 {hifumi.AdvanceBone}" +
                (hifumi.IsBloom ? " · 만개" : ""),
                "고유 패시브");
            return new Entry(canonicalName, "고유 패시브",
                "피격으로 뼈 획득 · 친치로 반격 · 만개", text);
        }

        return new Entry(SafeName(canonicalName, "고유 패시브"), "고유 패시브",
            "캐릭터 고유 전투 특성",
            Body("고유 패시브의 발동 조건은 현재 캐릭터의 전투 규칙을 따릅니다.",
                "고유 패시브"));
    }

    private static bool IsImplementationOnly(CombatMechanic mechanic)
    {
        return mechanic is StaggerGaugeMechanic ||
               mechanic is OlafRulebreakerMechanic ||
               mechanic is OlafImmortalFuryMechanic ||
               mechanic is Yujin0917Mechanic ||
               mechanic is YujinHwanhyeongFlowPassiveMechanic;
    }

    private static Entry BuildAdditional(CombatMechanic mechanic, Character character)
    {
        if (mechanic is OlafBloodyAxeMechanic)
            return new Entry("피 묻은 도끼", "아이템 효과",
                "공격 적중 시 출혈 추가",
                Body("실제 공격 피해가 발생하면 대상에게 출혈을 추가로 부여합니다.\n" +
                     "한 행동에서 여러 번 발동할 수 있는지는 해당 아이템 설정에 따릅니다.",
                     "장착 아이템"));

        if (mechanic is NormalEnemyBloodScentMechanic)
            return new Entry("피 냄새", "고유 특성", "합 승리 시 출혈 1",
                Body("합에서 승리하면 상대 부위에 출혈 1을 부여합니다.",
                    "적 고유 특성"));

        if (mechanic is EliteEnemyMechanic)
            return new Entry("엘리트 본능", "고유 특성",
                "합 승리·부위 약화·파괴로 위세 획득",
                Body("합에서 승리하면 위세 10과 출혈 1을 얻습니다.\n" +
                     "자신의 부위가 약화되면 위세 15, 파괴되면 위세 25를 얻습니다.",
                     "적 고유 특성"));

        // R7 emotion graphs retain the exact public name and original
        // human-written description in the source EmotionAugmentCatalog.
        object graph = ReadField<object>(mechanic, "definition", null);
        if (graph is NativeEmotionGraph native)
        {
            EmotionAugmentDefinition source = null;
            var defs = character?.BattleContext?.EmotionAugmentCatalog?.Entries;
            if (defs != null)
                source = defs.FirstOrDefault(x => x != null &&
                    x.AugmentId == native.StableId);
            string name = SafeName(source?.DisplayName ?? native.SourceDisplayName,
                "감정 증강");
            string explanation = SafeDescription(source?.Description,
                "습득한 감정 증강의 효과가 전투에 적용됩니다.");
            if (native.State == NativeEmotionGraphState.CanonicalValuesPending)
                explanation += "\n\n※ 미정인 수치·효과는 아직 적용되지 않을 수 있습니다.";
            return new Entry(name, "감정 증강", "습득 시 적용",
                Body(explanation, "감정 증강"));
        }

        string type = mechanic.GetType().Name;
        if (type == "ProvisionalEmotionModifierMechanic" ||
            type == "NativeEmotionPrototypeMechanic")
        {
            string label = SafeName(ReadField(mechanic, "label", ""), "감정 증강");
            return new Entry(label, "감정 증강", "전투 능력 보정",
                Body(DescribePrototype(mechanic, false), "감정 증강"));
        }
        if (type == "ProvisionalBalanceEmotionEffectMechanic")
        {
            string label = SafeName(ReadField(mechanic, "label", ""), "감정 증강");
            return new Entry(label, "감정 증강", "전투 능력 보정",
                Body(DescribePrototype(mechanic, true), "감정 증강"));
        }
        if (type == "ProvisionalRunItemEffectMechanic")
        {
            return new Entry("장착 아이템 보정", "아이템 효과",
                "굴림·피해·기세·위세 보정",
                Body(DescribePrototype(mechanic, true), "아이템 효과"));
        }

        // Other mechanics must not appear under their C# type names.
        // A descriptive Korean MechanicName can be used as-is.
        string nameText = SafeName(mechanic.MechanicName, "전투 특성");
        if (HasTechnicalName(nameText))
            nameText = "추가 전투 특성";
        return new Entry(nameText, "추가 효과", "전투 중 자동으로 적용",
            Body("전투 중 적용되는 추가 효과입니다.\n" +
                 "구체적인 발동 조건과 수치가 아직 설명 데이터로 제공되지 않습니다.",
                 "추가 효과"));
    }

    private static string DescribePrototype(CombatMechanic mechanic, bool flat)
    {
        var lines = new List<string>();
        int roll = ReadField(mechanic, "rollBonus", 0);
        if (roll != 0)
            lines.Add($"굴림 위력 {(roll > 0 ? "+" : "")}{roll}");

        if (flat)
        {
            int outgoing = ReadField(mechanic, "damageDealtFlatBonus", 0);
            int reduction = ReadField(mechanic, "damageTakenFlatReduction", 0);
            if (outgoing != 0)
                lines.Add($"가하는 피해 {(outgoing > 0 ? "+" : "")}{outgoing}");
            if (reduction > 0)
                lines.Add($"받는 피해 {reduction} 감소");
        }
        else
        {
            float outgoing = ReadField(mechanic, "damageDealtMultiplier", 1f);
            float incoming = ReadField(mechanic, "damageTakenMultiplier", 1f);
            if (Mathf.Abs(outgoing-1f) > .001f)
                lines.Add($"가하는 피해 ×{outgoing:0.##}");
            if (Mathf.Abs(incoming-1f) > .001f)
                lines.Add($"받는 피해 ×{incoming:0.##}");
        }

        int momentum = ReadField(mechanic, "positiveMomentumBonus", 0);
        if (momentum > 0)
            lines.Add($"유리한 기세 변화 +{momentum}");

        if (flat)
        {
            int prestige = ReadField(mechanic, "prestigeGainFlatBonus", 0);
            if (prestige != 0)
                lines.Add($"위세 획득 {(prestige > 0 ? "+" : "")}{prestige}");
        }
        else
        {
            float prestige = ReadField(mechanic, "prestigeGainMultiplier", 1f);
            if (Mathf.Abs(prestige-1f) > .001f)
                lines.Add($"위세 획득 ×{prestige:0.##}");
        }

        return lines.Count > 0
            ? string.Join("\n", lines.Select(line => "• " + line))
            : "적용 중인 수치 보정이 없습니다.";
    }

    private static T ReadField<T>(CombatMechanic mechanic, string field, T fallback)
    {
        if (mechanic == null)
            return fallback;

        FieldInfo info = mechanic.GetType().GetField(field,
            BindingFlags.NonPublic | BindingFlags.Instance);
        object value = info?.GetValue(mechanic);
        return value is T typed ? typed : fallback;
    }

    private static string WeaponName(YujinWeaponType weapon) => weapon switch
    {
        YujinWeaponType.Baeku => "백우",
        YujinWeaponType.Jeokseol => "적설",
        YujinWeaponType.Nakil => "낙일",
        _ => "현재 무기"
    };

    private static string SafeName(string name, string fallback)
    {
        return string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
    }

    private static bool HasTechnicalName(string name)
    {
        return name.Contains("Mechanic") ||
               name.Contains("TEMP_") ||
               name.Contains("R7 Native") ||
               name.Contains("0917") ||
               name.Contains("0922") ||
               name.Contains(" / ") ||
               name.Contains("Prototype") ||
               name.EndsWith(" v5", StringComparison.Ordinal);
    }

    private static string SafeDescription(string raw, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;
        // Never print migration metadata, internal types or temporary notes.
        if (BattleSkillDescriptionFormatter.IsTechnicalLeak(raw) ||
            raw.Contains("[R7-") ||
            raw.Contains("[0917_") ||
            raw.Contains("[0922_"))
            return fallback;
        return raw.Trim();
    }

    private static string Body(string explanation, string category) =>
        $"<color=#AFCBF1>{category}</color>\n\n{explanation}";
}