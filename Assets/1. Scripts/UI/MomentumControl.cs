using UnityEngine;
using UnityEngine.UI;

public class MomentumScrollbarUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private Slider scrollbar;

    [Header("Option")]
    [SerializeField] private bool usePlayerPerspective = true;

    //------------------------------------------------

    private void Update()
    {
        if (battleManager.MomentumManager == null || scrollbar == null)
            return;

        float momentum = battleManager.MomentumManager.CurrentMomentum;

        // 플레이어 기준 / 적 기준 반전
        if (!usePlayerPerspective)
            momentum *= -1f;

        scrollbar.value = Normalize(momentum);
    }

    //------------------------------------------------

    private float Normalize(float momentum)
    {
        // -100 ~ 100 → 0 ~ 1
        return Mathf.Clamp01((momentum + 100f) / 200f);
    }
}

