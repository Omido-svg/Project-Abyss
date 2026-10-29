using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity-facing frame scheduler for combat HUD refreshes.
/// Invalidations are kept in BattleUIRefreshBatch, separate from the View.
/// </summary>
public sealed class BattleUIRefreshScheduler : MonoBehaviour
{
    [SerializeField] private BattleUIManager uiManager;

    private readonly BattleUIRefreshBatch pending = new();
    private bool isFlushing;

    private void Awake()
    {
        if (uiManager == null)
            uiManager = FindFirstObjectByType<BattleUIManager>();
    }

    public void MarkAllDirty() => pending.MarkAll();

    public void MarkCharacterDirty(Character character) => pending.MarkCharacter(character);

    public void MarkBodyPartDirty(Character character, BodyPart part) =>
        pending.MarkPart(character, part);

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
        if (isFlushing || uiManager == null || !pending.HasPending)
            return;

        // Drain before invoking UI callbacks. The old implementation cleared
        // dirty flags *after* callbacks, dropping updates raised while flushing.
        BattleUIRefreshSnapshot snapshot = pending.Drain();
        isFlushing = true;

        try
        {
            if (snapshot.RefreshAll)
            {
                uiManager.RefreshAllUI();
                return;
            }

            var refreshedCharacters = new HashSet<Character>();
            foreach (Character character in snapshot.Characters)
            {
                if (character == null)
                    continue;

                uiManager.RefreshCharacterUI(character);
                refreshedCharacters.Add(character);
            }

            foreach (KeyValuePair<BodyPart, Character> pair in snapshot.Parts)
            {
                if (pair.Key == null ||
                    (pair.Value != null && refreshedCharacters.Contains(pair.Value)))
                {
                    continue;
                }

                uiManager.RefreshTargetUI(pair.Value, pair.Key);
            }
        }
        finally
        {
            isFlushing = false;
        }
    }
}
