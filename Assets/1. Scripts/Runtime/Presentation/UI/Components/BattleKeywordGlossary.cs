using System.Collections.Generic;

public static class BattleKeywordGlossary
{
    private static readonly Dictionary<string, string> descriptions =
        new(System.StringComparer.Ordinal)
        {
            ["일반공격"] =
                "합이 지정되지 않았으면 일방 공격으로 처리됩니다. 합 상대가 있으면 교환 굴림에 참여합니다.",
            ["결투"] =
                "상대 행동 슬롯과 합을 구성하는 공격 행동입니다. 결투 대 결투의 최종 승리에서 결투 전용 효과가 발동합니다.",
            ["도사림"] =
                "전투 행동보다 먼저 준비 효과를 적용하는 행동입니다. 현재 구현에서는 FORESIGHT 단계에서 해결됩니다.",
            ["위세"] =
                "개인 위세 조건이나 자원을 사용하는 특수 행동입니다. 스킬 정의에 따라 선턴 또는 COMBAT 단계에서 처리됩니다.",
            ["빛"] =
                "행동 계획 시 예약되고 실행 시 소비되는 개인 자원입니다. 각 스킬이 고유 비용을 가집니다.",
            ["합"] =
                "서로 연결된 두 행동 슬롯이 교환 단위로 굴림을 비교하는 판정입니다.",
            ["수비"] =
                "수비 굴림이 이기면 해당 교환 피해를 0으로 만듭니다. 패배하면 판정값 차이만큼 피해를 받습니다.",
            ["부위 파괴"] =
                "파괴된 부위에 연결된 행동 슬롯을 사용할 수 없게 만듭니다. 일반 부위 피해만으로는 HP가 1 아래로 내려가지 않습니다.",
            ["공격 가중치"] =
                "메인 타깃을 포함해 한 공격 굴림이 피해를 줄 수 있는 서로 다른 캐릭터 수입니다. 추가 타깃은 메인 타깃과 같은 진영에서 무작위로 정해집니다.",
            ["출혈"] =
                "지속시간과 스택을 가진 디버프입니다. 적용 범위에 따라 캐릭터 또는 특정 부위에 존재합니다.",
            ["기세"] =
                "양 진영이 공유하는 전장 게이지입니다. 적중, 수비 성공, 결투 승리 등에 따라 즉시 이동합니다.",
            ["Block"] =
                "직접 피해보다 먼저 소모되어 피해를 흡수하는 방어 자원입니다."
        };

    public static string GetDescription(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return "설명이 없습니다.";

        return descriptions.TryGetValue(keyword, out string description)
            ? description
            : "이 키워드는 캐릭터 또는 스킬 데이터에서 정의되는 고유 효과입니다.";
    }
}