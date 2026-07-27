using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleCharacterWorldClickInspector : MonoBehaviour
{
    [SerializeField] private Character character;
    [SerializeField] private BattleUIManager battleUiManager;
    [SerializeField] private BattleCharacterDetailPanelUI detailPanel;

    public Character BoundCharacter => character;

    public void Configure(
        Character target,
        BattleUIManager manager,
        BattleCharacterDetailPanelUI panel)
    {
        Character localCharacter =
            GetComponent<Character>();

        character =
            localCharacter != null
                ? localCharacter
                : target;

        battleUiManager =
            manager;

        detailPanel =
            panel;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        Character localCharacter =
            GetComponent<Character>();

        if (localCharacter != null)
            character = localCharacter;

        if (battleUiManager == null)
        {
            battleUiManager =
                FindFirstObjectByType<BattleUIManager>(
                    FindObjectsInactive.Include);
        }

        if (detailPanel == null)
        {
            detailPanel =
                FindFirstObjectByType<BattleCharacterDetailPanelUI>(
                    FindObjectsInactive.Include);
        }
    }

    public bool OpenOwnDetails(
        string source = "Inspector")
    {
        ResolveReferences();

        return OpenDetailsForCharacter(
            character,
            detailPanel,
            source);
    }

    public static bool OpenDetailsForCharacter(
        Character target,
        BattleCharacterDetailPanelUI fallbackPanel = null,
        string source = "Unknown")
    {
        if (BattlePresentationInteractionLock.IsLocked)
        {
            Debug.Log(
                $"[CharacterDetailRoute] 연출 중 상세 패널 입력 차단 / Source={source}");
            return false;
        }

        if (target == null)
        {
            Debug.LogError(
                $"[CharacterDetailRoute] Target=NULL, Source={source}");

            return false;
        }

        BattleCharacterDetailPanelUI panel =
            fallbackPanel != null
                ? fallbackPanel
                : Object.FindFirstObjectByType<
                    BattleCharacterDetailPanelUI>(
                        FindObjectsInactive.Include);

        if (panel == null)
        {
            Debug.LogError(
                "[CharacterDetailRoute] DetailPanel=NULL, " +
                $"Source={source}, " +
                $"Target={GetCharacterLabel(target)}#" +
                $"{target.GetInstanceID()}, " +
                $"Path={BattleCharacterPointerRouter.GetHierarchyPath(target.transform)}",
                target);

            return false;
        }

        BattleCharacterPointerRouter.SelectPersistentCharacter(
            target);

        Debug.Log(
            "[CharacterDetailRoute][SHOW_REQUEST] " +
            $"Source={source}, " +
            $"Target={GetCharacterLabel(target)}#" +
            $"{target.GetInstanceID()}, " +
            $"TargetType={target.GetType().Name}, " +
            $"TargetPath={BattleCharacterPointerRouter.GetHierarchyPath(target.transform)}, " +
            $"Panel={BattleCharacterPointerRouter.GetHierarchyPath(panel.transform)}",
            target);

        panel.Show(
            target);

        if (panel.CurrentCharacter != target)
        {
            Debug.LogError(
                "[CharacterDetailRoute][SHOW_MISMATCH] " +
                $"Requested={GetCharacterLabel(target)}#" +
                $"{target.GetInstanceID()}, " +
                $"Actual={GetCharacterLabel(panel.CurrentCharacter)}#" +
                $"{(panel.CurrentCharacter != null ? panel.CurrentCharacter.GetInstanceID() : 0)}",
                panel);

            return false;
        }

        return true;
    }

    private static string GetCharacterLabel(
        Character target)
    {
        if (target == null)
            return "NULL";

        return target.Data?.CharacterName ??
               target.name;
    }
}