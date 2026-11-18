using System;
using UnityEngine;

/// <summary>
/// 규칙서 §19 히후미 카드 중 수치가 (미정)인 칸.
/// 기본값은 sim_py `extra_skills.txt`의 임시값을 따른다. 규칙서가 값을 확정하면 여기를 맞춘다.
/// </summary>
[Serializable]
public sealed class HifumiCardRuleSettings
{
    [Header("평타② 과감한 판단 (§19.6)")]
    [Tooltip("사용시 지불을 시도하는 뼈(가불 우선). 임시값 — sim HF-B02 40")]
    [Min(0)] public int BoldJudgmentBoneCost = 40;

    [Tooltip("지불 실패 시 다음 턴 자신에게 거는 속박 N (1턴)")]
    [Min(0)] public int BoldJudgmentBindOnFail = 1;

    [Tooltip("합 종료시 완전 차단이면 얻는 가불. 임시값 — sim HF-B02 40")]
    [Min(0)] public int BoldJudgmentCompleteBlockAdvance = 40;

    [Header("평타③ 동귀어진 (§19.6)")]
    [Tooltip("자신(이 카드를 쓴 슬롯 부위)과 조준한 상대 부위에 같은 값으로 주는 고정 피해 N. 임시값 — 작업/히후미.md 측정 추천 10 (11-18 사용자)")]
    [Min(0)] public int MutualDestructionFixedDamage = 10;
}
