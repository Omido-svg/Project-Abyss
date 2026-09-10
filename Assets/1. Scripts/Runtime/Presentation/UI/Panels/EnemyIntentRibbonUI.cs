using UnityEngine;

/// <summary>
/// 폐기된 상단 우측 "적 행동 · 부위 슬롯" HUD의 이전 Scene 참조를 안전하게 정리하기 위한
/// 전환용 컴포넌트입니다. 신규 Scene에서는 사용하지 않습니다.
///
/// 기존 Scene에 이 컴포넌트가 남아 있어도 Play Mode에서는 즉시 비활성화됩니다.
/// Editor 메뉴 Tools/Project Abyss/UI/Remove Enemy Intent Ribbon From Current Scene 을
/// 실행하면 GameObject 자체를 Scene에서 완전히 제거할 수 있습니다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("")]
public sealed class EnemyIntentRibbonUI : MonoBehaviour
{
    private void Awake()
    {
        DisableRetiredView();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            DisableRetiredView();
    }

    private void DisableRetiredView()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }
}
