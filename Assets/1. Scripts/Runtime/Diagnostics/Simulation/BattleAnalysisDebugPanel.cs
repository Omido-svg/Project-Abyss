using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleAnalysisDebugPanel : MonoBehaviour
{
    [SerializeField] private Button winRateButton;
    [SerializeField] private Button damageButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private TMP_Text statusText;

    private BattleBatchSimulationRunner runner;

    public void Configure(
        Button winRate,
        Button damage,
        Button stop,
        TMP_Text status)
    {
        winRateButton = winRate;
        damageButton = damage;
        stopButton = stop;
        statusText = status;
    }

    private void Awake()
    {
        BindButtons();
    }

    private void Start()
    {
        ResolveRunner();
        runner?.RegisterPanel(this);
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    private void BindButtons()
    {
        UnbindButtons();

        winRateButton?.onClick.AddListener(
            RunWinRateAnalysis);

        damageButton?.onClick.AddListener(
            RunDamageAnalysis);

        stopButton?.onClick.AddListener(
            StopAnalysis);
    }

    private void UnbindButtons()
    {
        winRateButton?.onClick.RemoveListener(
            RunWinRateAnalysis);

        damageButton?.onClick.RemoveListener(
            RunDamageAnalysis);

        stopButton?.onClick.RemoveListener(
            StopAnalysis);
    }

    private void ResolveRunner()
    {
        runner =
            BattleBatchSimulationRunner.GetOrCreate();
    }

    public void RunWinRateAnalysis()
    {
        ResolveRunner();
        runner?.RunWinRateAnalysis();
    }

    public void RunDamageAnalysis()
    {
        ResolveRunner();
        runner?.RunDamageAnalysis();
    }

    public void StopAnalysis()
    {
        ResolveRunner();
        runner?.StopAnalysis();
    }

    public void SetStatus(string value)
    {
        if (statusText != null)
            statusText.text = value ?? string.Empty;
    }

    public void SetRunning(bool running)
    {
        if (winRateButton != null)
            winRateButton.interactable = !running;

        if (damageButton != null)
            damageButton.interactable = !running;

        if (stopButton != null)
            stopButton.interactable = running;
    }
}
