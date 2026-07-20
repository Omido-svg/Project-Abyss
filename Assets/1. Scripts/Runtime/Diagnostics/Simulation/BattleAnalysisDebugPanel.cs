using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleAnalysisDebugPanel : MonoBehaviour
{
    [SerializeField] private Button winRateButton;
    [SerializeField] private Button damageButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Button openFolderButton;
    [SerializeField] private TMP_Text statusText;

    private BattleBatchSimulationRunner runner;

    public void Configure(
        Button winRate,
        Button damage,
        Button stop,
        TMP_Text status)
    {
        Configure(
            winRate,
            damage,
            stop,
            null,
            status);
    }

    public void Configure(
        Button winRate,
        Button damage,
        Button stop,
        Button openFolder,
        TMP_Text status)
    {
        winRateButton = winRate;
        damageButton = damage;
        stopButton = stop;
        openFolderButton = openFolder;
        statusText = status;
    }

    private void Awake()
    {
        BindButtons();
    }

    private void OnEnable()
    {
        BindButtons();
        ResolveAndRegisterRunner();
    }

    private void Start()
    {
        ResolveAndRegisterRunner();
    }

    private void OnDisable()
    {
        UnbindButtons();
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

        openFolderButton?.onClick.AddListener(
            OpenOutputFolder);
    }

    private void UnbindButtons()
    {
        winRateButton?.onClick.RemoveListener(
            RunWinRateAnalysis);

        damageButton?.onClick.RemoveListener(
            RunDamageAnalysis);

        stopButton?.onClick.RemoveListener(
            StopAnalysis);

        openFolderButton?.onClick.RemoveListener(
            OpenOutputFolder);
    }

    private BattleBatchSimulationRunner ResolveRunner()
    {
        runner =
            BattleBatchSimulationRunner.GetOrCreate();

        return runner;
    }

    private void ResolveAndRegisterRunner()
    {
        BattleBatchSimulationRunner resolved =
            ResolveRunner();

        resolved?.RegisterPanel(this);
    }

    public void RunWinRateAnalysis()
    {
        BattleBatchSimulationRunner resolved =
            ResolveRunner();

        if (resolved == null)
        {
            SetStatus("배치 분석 Runner를 생성하지 못했습니다.");
            return;
        }

        resolved.RegisterPanel(this);
        resolved.RunWinRateAnalysis();
    }

    public void RunDamageAnalysis()
    {
        BattleBatchSimulationRunner resolved =
            ResolveRunner();

        if (resolved == null)
        {
            SetStatus("배치 분석 Runner를 생성하지 못했습니다.");
            return;
        }

        resolved.RegisterPanel(this);
        resolved.RunDamageAnalysis();
    }

    public void StopAnalysis()
    {
        BattleBatchSimulationRunner resolved =
            ResolveRunner();

        resolved?.StopAnalysis();
    }

    public void OpenOutputFolder()
    {
        BattleBatchSimulationRunner resolved =
            ResolveRunner();

        resolved?.OpenOutputFolder();
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
