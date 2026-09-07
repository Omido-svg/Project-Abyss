using System;
using System.Collections.Generic;
using System.Text;

public enum BattleKeywordTone
{
    Neutral = 0,
    Beneficial = 1,
    Harmful = 2,
    Unique = 3
}

public static class BattleKeywordGlossary
{
    private static readonly Dictionary<string, string> descriptions =
        new(System.StringComparer.Ordinal)
        {
            ["일반공격"] =
                "합이 지정되지 않았으면 일방 공격으로 처리됩니다. 합 상대가 있으면 교환 굴림에 참여합니다.",
            ["결투"] =
                "상대 행동 슬롯과 합을 구성하는 공격 행동입니다. 결투 대 결투에서는 합 전체 승자가 아니라 개별 교환 승패를 사용합니다.",
            ["도사림"] =
                "계획 단계에서 누르는 즉시 적용되는 준비 행동입니다. 합과 행동 슬롯을 사용하지 않으며 생산한 빛도 같은 턴 계획에 사용할 수 있습니다.",
            ["위세"] =
                "개인 위세 조건이나 자원을 사용하는 특수 행동입니다. 스킬 정의에 따라 선턴 또는 COMBAT 단계에서 처리됩니다.",
            ["빛"] =
                "행동 계획 시 예약되고 실행 시 소비되는 개인 자원입니다. 각 스킬이 고유 비용을 가집니다.",
            ["합"] =
                "서로 연결된 두 행동 슬롯이 교환 단위로 굴림을 비교하는 판정입니다.",
            ["수비"] =
                "Gameplay v5에서 구 수비 굴림은 흐트러짐 공격 굴림으로 대체되었습니다. HP 피해는 주지 않고 흐트러짐 피해를 주며 자신의 흐트러짐을 회복합니다.",
            ["흐트러짐"] =
                "HP와 별개의 게이지입니다. 공격/흐트러짐 굴림의 최종 굴림값에 흐트러짐 내성을 적용해 감소하며, 0이 되면 취약 창이 열려 HP 내성이 전 타입 ×2로 덮어써집니다.",
            ["부위 파괴"] =
                "파괴된 부위에 연결된 행동 슬롯을 사용할 수 없게 만듭니다. 일반 부위 피해만으로는 HP가 1 아래로 내려가지 않습니다.",
            ["공격 가중치"] =
                "메인 타깃을 포함해 한 공격 굴림이 피해를 줄 수 있는 서로 다른 캐릭터 수입니다. 추가 타깃은 메인 타깃과 같은 진영에서 무작위로 정해집니다.",
            ["혈상"] =
                "올라프 전용 고유 키워드입니다. 턴 종료 피해는 현재 스택 × 피해값이며 처리 후 스택이 1 감소합니다.",
            ["절단"] =
                "물리 공격 속성입니다. 대상의 절단 내성 배율을 적용합니다.",
            ["둔격"] =
                "물리 공격 속성입니다. 대상의 둔격 내성 배율을 적용합니다.",
            ["관통"] =
                "물리 공격 속성입니다. 대상의 관통 내성 배율을 적용합니다.",
            ["무력화"] =
                "구 명칭입니다. Gameplay v5에서는 흐트러짐 게이지/취약 창 시스템을 사용합니다.",
            ["뼈"] =
                "히후미 전용 고유 자원입니다. 실제 받은 피해를 1:1로 응축하며 짓눌림에서는 획득량이 2배가 됩니다. 최대 500입니다.",
            ["환형"] =
                "유진의 무기 전환 준비 행동입니다. 선택한 무기는 현재 턴이 아니라 다음 턴 시작에 적용됩니다.",
            ["힘"] =
                "공격 굴림의 판정 범위 전체를 스택만큼 올립니다. 1턴 유지됩니다.",
            ["쇠약"] =
                "공격 굴림의 판정 범위 전체를 스택만큼 내립니다. 1턴 유지됩니다.",
            ["견고"] =
                "흐트러짐 굴림의 판정 범위 전체를 스택만큼 올립니다. 1턴 유지됩니다.",
            ["무장해제"] =
                "흐트러짐 굴림의 판정 범위 전체를 스택만큼 내립니다. 1턴 유지됩니다.",
            ["골절"] =
                "굴림 최댓값만 스택만큼 낮춥니다. 범위 이동 효과와 별도로 누산되며 1턴 유지됩니다.",
            ["보호"] =
                "받는 피해를 스택만큼 감소시킵니다. 1턴 유지됩니다.",
            ["균열"] =
                "받는 피해를 스택만큼 증가시킵니다. 1턴 유지됩니다.",
            ["열기"] =
                "지속형 공용 상태입니다. 매 턴 시작에 스택만큼 위세 게이지를 얻습니다.",
            ["재생"] =
                "최신 TODO2 규칙에 따라 1턴 상태입니다. 턴 종료에 스택만큼 회복합니다.",
            ["고통"] =
                "최신 TODO2 규칙에 따라 1턴 상태입니다. 활성 중 회복량을 절반으로 만듭니다.",
            ["화상"] =
                "지속 피해 상태입니다. 스택에 비례한 피해를 주며 설정된 지속시간 동안 유지됩니다.",
            ["Burn"] =
                "화상과 같은 상태입니다. 지속 피해를 주고 스택/지속시간이 갱신됩니다.",
            ["기절"] =
                "행동을 제한하는 해로운 상태입니다.",
            ["Stun"] =
                "기절과 같은 상태입니다. 행동을 제한합니다.",
            ["기세"] =
                "매 턴 0에서 시작하는 -100~100 줄다리기 바입니다. 일반 성공 교환은 20, 결투 대 결투의 개별 교환 승리는 총 40을 밀며 턴 종료 구간이 고조 충전량을 정합니다.",
            ["고조"] =
                "턴 종료 기세 구간으로 누적되는 열광 성장 게이지입니다. 짓누름 10, 우세 5, 중립 2, 열세/짓눌림 0을 얻습니다.",
            ["열광"] =
                "고조를 4/8/10 소비해 0~3 레벨로 성장합니다. 레벨업마다 빛 최대치 +1과 전량 회복을 받고 해당 티어의 감정 증강 선택을 엽니다.",
            ["가드"] =
                "대상 피해 보정이 끝난 뒤 최종 피해를 대신 받아 감소하는 소모형 방어막입니다.",
            ["Block"] =
                "가드와 같은 의미입니다. 피해를 대신 받아 감소하는 소모형 방어막입니다."
        };

    private static readonly HashSet<string> beneficialKeywords =
        new(StringComparer.Ordinal)
        {
            "힘",
            "견고",
            "보호",
            "열기",
            "재생"
        };

    private static readonly HashSet<string> harmfulKeywords =
        new(StringComparer.Ordinal)
        {
            "쇠약",
            "무장해제",
            "골절",
            "균열",
            "고통",
            "화상",
            "Burn",
            "기절",
            "Stun"
        };

    private static readonly HashSet<string> uniqueKeywords =
        new(StringComparer.Ordinal)
        {
            "혈상",
            "뼈",
            "환형"
        };

    private static readonly HashSet<string> neutralSystemKeywords =
        new(StringComparer.Ordinal)
        {
            "일반공격",
            "결투",
            "도사림",
            "위세",
            "빛",
            "합",
            "수비",
            "흐트러짐",
            "부위 파괴",
            "공격 가중치",
            "절단",
            "둔격",
            "관통",
            "무력화",
            "기세",
            "고조",
            "열광",
            "가드",
            "Block"
        };

    public static string GetDescription(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return "설명이 없습니다.";

        return descriptions.TryGetValue(keyword, out string description)
            ? description
            : "이 키워드는 캐릭터 또는 스킬 데이터에서 정의되는 고유 효과입니다.";
    }

    public static BattleKeywordTone GetTone(
        string keyword,
        bool unknownIsUnique = true)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return BattleKeywordTone.Neutral;

        if (beneficialKeywords.Contains(keyword))
            return BattleKeywordTone.Beneficial;

        if (harmfulKeywords.Contains(keyword))
            return BattleKeywordTone.Harmful;

        if (uniqueKeywords.Contains(keyword))
            return BattleKeywordTone.Unique;

        if (neutralSystemKeywords.Contains(keyword))
            return BattleKeywordTone.Neutral;

        return unknownIsUnique
            ? BattleKeywordTone.Unique
            : BattleKeywordTone.Neutral;
    }

    public static string GetColorHex(
        BattleKeywordTone tone)
    {
        return tone switch
        {
            // TODO 문서 기준: 이로운 공용 키워드는 노랑, 해로운 공용 키워드는 빨강.
            BattleKeywordTone.Beneficial => "#FFD84A",
            BattleKeywordTone.Harmful => "#FF5B61",
            BattleKeywordTone.Unique => "#D99CFF",
            _ => "#8FD3FF"
        };
    }

    public static string ColorizeKeyword(
        string keyword,
        bool bold = true,
        bool unknownIsUnique = true)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return string.Empty;

        string value =
            bold
                ? $"<b>{keyword}</b>"
                : keyword;

        return
            $"<color={GetColorHex(GetTone(keyword, unknownIsUnique))}>{value}</color>";
    }

    /// <summary>
    /// TMP RichText 태그 내부는 건드리지 않고,
    /// 공용 키워드와 SkillDefinition에 선언된 고유 키워드를 한 번만 색칠한다.
    /// </summary>
    public static string ColorizeText(
        string text,
        IEnumerable<string> extraKeywords = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        List<string> keywords =
            new List<string>(
                descriptions.Keys);

        if (extraKeywords != null)
        {
            foreach (string keyword in extraKeywords)
            {
                if (string.IsNullOrWhiteSpace(keyword) ||
                    keywords.Contains(keyword))
                {
                    continue;
                }

                keywords.Add(keyword);
            }
        }

        keywords.Sort(
            (left, right) =>
                right.Length.CompareTo(left.Length));

        StringBuilder builder =
            new StringBuilder(
                text.Length + 64);

        int index = 0;

        while (index < text.Length)
        {
            if (text[index] == '<')
            {
                int close =
                    text.IndexOf(
                        '>',
                        index);

                if (close >= index)
                {
                    builder.Append(
                        text,
                        index,
                        close - index + 1);

                    index = close + 1;
                    continue;
                }
            }

            string matched = null;

            foreach (string keyword in keywords)
            {
                if (index + keyword.Length > text.Length)
                    continue;

                if (string.Compare(
                        text,
                        index,
                        keyword,
                        0,
                        keyword.Length,
                        StringComparison.Ordinal) == 0)
                {
                    matched = keyword;
                    break;
                }
            }

            if (matched != null)
            {
                builder.Append(
                    ColorizeKeyword(
                        matched,
                        bold: true,
                        unknownIsUnique: true));

                index += matched.Length;
                continue;
            }

            builder.Append(text[index]);
            index++;
        }

        return builder.ToString();
    }

    public static string GetStatusEffectDisplayName(
        StatusEffectId id)
    {
        return id switch
        {
            StatusEffectId.Bleeding => "혈상",
            StatusEffectId.OlafBloodWound => "혈상",
            StatusEffectId.Burn => "화상",
            StatusEffectId.Stun => "기절",
            StatusEffectId.Strength => "힘",
            StatusEffectId.Weakness => "쇠약",
            StatusEffectId.Sturdy => "견고",
            StatusEffectId.Disarm => "무장해제",
            StatusEffectId.Fracture => "골절",
            StatusEffectId.Protection => "보호",
            StatusEffectId.Rupture => "균열",
            StatusEffectId.Heat => "열기",
            StatusEffectId.Regeneration => "재생",
            StatusEffectId.Pain => "고통",
            _ => id.ToString()
        };
    }
}