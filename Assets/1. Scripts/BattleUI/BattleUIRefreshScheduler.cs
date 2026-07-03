using System.Collections.Generic;
using UnityEngine;

public class BattleUIRefreshScheduler : MonoBehaviour
{
    [SerializeField] private BattleUIManager uiManager;

    private readonly HashSet<Character> dirtyCharacters =
        new HashSet<Character>();

    private readonly HashSet<BodyPart> dirtyBodyParts =
        new HashSet<BodyPart>();

    private bool refreshAll;

    private void Awake()
    {
        if (uiManager == null)
            uiManager = FindFirstObjectByType<BattleUIManager>();
    }

    public void MarkAllDirty()
    {
        refreshAll = true;
    }

    public void MarkCharacterDirty(Character character)
    {
        if (character == null)
            return;

        dirtyCharacters.Add(character);
    }

    public void MarkBodyPartDirty(
        Character character,
        BodyPart part)
    {
        if (character != null)
            dirtyCharacters.Add(character);

        if (part != null)
            dirtyBodyParts.Add(part);
    }

    private void LateUpdate()
    {
        Flush();
    }

    public void Flush()
    {
        if (uiManager == null)
            return;

        if (!refreshAll &&
            dirtyCharacters.Count == 0 &&
            dirtyBodyParts.Count == 0)
        {
            return;
        }

        if (refreshAll)
        {
            uiManager.RefreshAllUI();
        }
        else
        {
            foreach (Character character in dirtyCharacters)
            {
                uiManager.RefreshCharacterUI(character);
            }

            foreach (BodyPart part in dirtyBodyParts)
            {
                uiManager.RefreshBodyPartUI(part);
            }
        }

        refreshAll = false;
        dirtyCharacters.Clear();
        dirtyBodyParts.Clear();
    }
}