using System;
using UnityEngine;

public enum BattleUiScreenMode
{
    DefaultBattle,
    SkillSelection,
    CharacterDetails
}

[DisallowMultipleComponent]
public sealed class BattleScreenModeController : MonoBehaviour
{
    [SerializeField] private GameObject defaultBattleLayer;
    [SerializeField] private GameObject skillSelectionLayer;
    [SerializeField] private GameObject characterDetailLayer;
    [SerializeField] private GameObject[] keepVisibleInAllModes;

    public BattleUiScreenMode CurrentMode { get; private set; } =
        BattleUiScreenMode.DefaultBattle;

    public bool IsResolutionOverlayActive { get; private set; }

    public event Action<BattleUiScreenMode> ModeChanged;
    public event Action<bool> ResolutionOverlayChanged;

    public void Configure(
        GameObject defaultLayer,
        GameObject skillLayer,
        GameObject detailLayer,
        GameObject[] persistentObjects = null)
    {
        defaultBattleLayer = defaultLayer;
        skillSelectionLayer = skillLayer;
        characterDetailLayer = detailLayer;
        keepVisibleInAllModes = persistentObjects;
        ShowDefaultMode();
    }

    private void Awake()
    {
        ApplyMode(CurrentMode);
    }

    public void ShowDefaultMode()
    {
        ApplyMode(BattleUiScreenMode.DefaultBattle);
    }

    public void ShowSkillMode()
    {
        ApplyMode(BattleUiScreenMode.SkillSelection);
    }

    public void ShowCharacterDetailMode()
    {
        ApplyMode(BattleUiScreenMode.CharacterDetails);
    }

    /// <summary>
    /// Resolution은 별도 화면 모드가 아니라 현재 base mode 위에 올라가는 overlay 상태다.
    /// resolving 중에 상세/스킬 모드 요청이 바뀌어도 기록만 유지하고,
    /// overlay가 끝날 때 최신 CurrentMode를 다시 적용한다.
    /// </summary>
    public void SetResolutionOverlayActive(bool active)
    {
        if (IsResolutionOverlayActive == active)
            return;

        IsResolutionOverlayActive = active;
        ApplyMode(CurrentMode);
        ResolutionOverlayChanged?.Invoke(active);
    }

    private void ApplyMode(BattleUiScreenMode mode)
    {
        bool changed =
            CurrentMode != mode;

        CurrentMode = mode;

        bool allowBaseLayers =
            !IsResolutionOverlayActive;

        SetActive(
            defaultBattleLayer,
            allowBaseLayers &&
            mode == BattleUiScreenMode.DefaultBattle);

        SetActive(
            skillSelectionLayer,
            allowBaseLayers &&
            mode == BattleUiScreenMode.SkillSelection);

        SetActive(
            characterDetailLayer,
            allowBaseLayers &&
            mode == BattleUiScreenMode.CharacterDetails);

        if (keepVisibleInAllModes != null)
        {
            foreach (GameObject target in keepVisibleInAllModes)
            {
                SetActive(
                    target,
                    allowBaseLayers);
            }
        }

        if (changed)
            ModeChanged?.Invoke(CurrentMode);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
