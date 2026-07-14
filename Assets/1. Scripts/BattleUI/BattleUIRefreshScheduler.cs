using System.Collections.Generic;
using UnityEngine;

public sealed class BattleUIRefreshScheduler : MonoBehaviour
{
    [SerializeField] private BattleUIManager uiManager;

    private readonly HashSet<Character> dirtyCharacters = new();
    private readonly Dictionary<BodyPart, Character> dirtyBodyParts = new();

    private bool refreshAll;
    private bool isFlushing;

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
        if (character != null)
            dirtyCharacters.Add(character);
    }

    public void MarkBodyPartDirty(Character character, BodyPart part)
    {
        if (part == null)
        {
            MarkCharacterDirty(character);
            return;
        }

        dirtyBodyParts[part] = character ?? part.Owner;
    }

    public void MarkActionDirty(BattleAction action)
    {
        if (action == null)
            return;

        MarkBodyPartDirty(action.Owner, action.OwnerPart);
        MarkBodyPartDirty(action.Target, action.TargetPart);
    }

    private void LateUpdate()
    {
        Flush();
    }

    public void Flush()
    {
        if (isFlushing || uiManager == null)
            return;

        if (!refreshAll &&
            dirtyCharacters.Count == 0 &&
            dirtyBodyParts.Count == 0)
        {
            return;
        }

        isFlushing = true;

        try
        {
            if (refreshAll)
            {
                uiManager.RefreshAllUI();
            }
            else
            {
                foreach (Character character in dirtyCharacters)
                    uiManager.RefreshCharacterUI(character);

                foreach (KeyValuePair<BodyPart, Character> pair
                         in dirtyBodyParts)
                {
                    if (dirtyCharacters.Contains(pair.Value))
                        continue;

                    uiManager.RefreshTargetUI(
                        pair.Value,
                        pair.Key);
                }
            }
        }
        finally
        {
            refreshAll = false;
            dirtyCharacters.Clear();
            dirtyBodyParts.Clear();
            isFlushing = false;
        }
    }
}
