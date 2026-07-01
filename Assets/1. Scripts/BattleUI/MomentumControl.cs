using UnityEngine;
using UnityEngine.UI;

public class MomentumScrollbarUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private Slider momentumSlider;

    [Header("Display")]
    [SerializeField] private bool playerAdvantageIsRight = true;

    [Header("Range")]
    [SerializeField] private float minMomentum = -100f;
    [SerializeField] private float maxMomentum = 100f;

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (momentumSlider == null)
            momentumSlider = GetComponent<Slider>();

        if (momentumSlider != null)
        {
            momentumSlider.minValue = 0f;
            momentumSlider.maxValue = 1f;
        }
    }

    private void Update()
    {
        if (battleManager == null)
            return;

        if (battleManager.MomentumManager == null)
            return;

        if (momentumSlider == null)
            return;

        float momentum =
            battleManager.MomentumManager.CurrentMomentum;

        float normalized =
            Mathf.InverseLerp(
                minMomentum,
                maxMomentum,
                momentum);

        if (!playerAdvantageIsRight)
            normalized = 1f - normalized;

        momentumSlider.value = normalized;
    }
}