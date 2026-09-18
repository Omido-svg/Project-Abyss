using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// AutoPlan 진단 전용 포인터 probe.
/// UnityEngine.UI.Button.interactable == false여도 EventSystem의 PointerDown/PointerClick을 받아
/// BattleAutoPlanButtonPanel이 현재 비활성화된 정확한 이유를 Console에 남긴다.
/// 진단 완료 후 제거 가능한 임시 코드다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleAutoPlanDiagnosticClickProbe :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerClickHandler
{
    private BattleAutoPlanButtonPanel owner;
    private PlayerAutoPlanMode mode;

    public void Configure(
        BattleAutoPlanButtonPanel panel,
        PlayerAutoPlanMode planMode)
    {
        owner = panel;
        mode = planMode;
    }

    public void OnPointerDown(
        PointerEventData eventData)
    {
        owner?.LogAutoPlanDiagnosticPointer(
            mode,
            "POINTER_DOWN",
            eventData);
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        owner?.LogAutoPlanDiagnosticPointer(
            mode,
            "POINTER_CLICK",
            eventData);
    }
}
