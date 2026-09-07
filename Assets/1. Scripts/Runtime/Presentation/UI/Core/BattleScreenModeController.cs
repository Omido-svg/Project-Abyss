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

    public event Action<BattleUiScreenMode> ModeChanged;

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

    private void ApplyMode(BattleUiScreenMode mode)
    {
        bool changed =
            CurrentMode != mode;

        CurrentMode = mode;

        SetActive(
            defaultBattleLayer,
            mode == BattleUiScreenMode.DefaultBattle);

        SetActive(
            skillSelectionLayer,
            mode == BattleUiScreenMode.SkillSelection);

        SetActive(
            characterDetailLayer,
            mode == BattleUiScreenMode.CharacterDetails);

        if (keepVisibleInAllModes != null)
        {
            foreach (GameObject target in keepVisibleInAllModes)
            {
                if (target != null && !target.activeSelf)
                    target.SetActive(true);
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
