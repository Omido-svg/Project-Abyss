using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Non-scene smoke checks for the UI refactor. Run from the Unity editor menu.
/// This does not replace Play Mode regression tests.
/// </summary>
public static class BattleUiMvpRefactorVerification20261029
{
    [MenuItem("Game System Verification/20261029 UI MVP Refactor - Smoke Test")]
    public static void VerifyFromMenu()
    {
        int pass = 0;
        int fail = 0;

        void Check(string label, bool condition)
        {
            if (condition)
            {
                pass++;
                Debug.Log($"[UI MVP Refactor] PASS: {label}");
            }
            else
            {
                fail++;
                Debug.LogError($"[UI MVP Refactor] FAIL: {label}");
            }
        }

        BattleUIRefreshBatch batch = new();
        Check("initial refresh batch empty", !batch.HasPending);

        batch.MarkAll();
        BattleUIRefreshSnapshot first = batch.Drain();
        Check("full refresh snapshot", first.RefreshAll);
        Check("drained changes are cleared", !batch.HasPending);

        // Simulates a new invalidation created by a view callback while the
        // previously drained snapshot is being rendered.
        batch.MarkAll();
        Check("new invalidation remains pending", batch.HasPending);
        BattleUIRefreshSnapshot second = batch.Drain();
        Check("new invalidation survives until next drain", second.RefreshAll);
        Check("batch returns to empty state", !batch.HasPending);

        batch.MarkCharacter(null);
        batch.MarkPart(null, null);
        Check("null invalidations are ignored", !batch.HasPending);

        ActionSlotButtonPresenter slotPresenter = new();
        slotPresenter.Bind(null, null, null, -5, null);
        ActionSlotButtonPresentation action = slotPresenter.CreatePresentation(false);
        Check("empty slot rendered from presenter", action.Label.Contains("빈 슬롯"));
        Check("negative action index clamped", action.Label.Contains("행동 #1"));
        Check("slot without owner is disabled", !action.CanSelect);
        Check("slot without action is empty", !action.HasAction);

        BattleConfiguredSlotBoardPresentation board =
            new BattleConfiguredSlotBoardPresenter().Project(null);
        Check("uninitialized battle projects empty board", board.Items.Count == 0);
        Check("empty board has a signature", !string.IsNullOrEmpty(board.Signature));

        Debug.Log($"[UI MVP Refactor] RESULT PASS={pass} FAIL={fail}");
        if (fail != 0)
            throw new InvalidOperationException(
                $"UI MVP Refactor smoke verification failed: {fail} checks");
    }
}
