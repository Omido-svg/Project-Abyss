using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Planning 단계에서 즉시 적용된 도사림 효과의 역연산을 ActionSlot 단위로 보존한다.
/// 전투 Resolution에서는 사용하지 않으며, 사용자가 명시적으로 계획을 취소/교체할 때만 실행한다.
/// </summary>
public sealed class PlanningUndoJournal
{
    private readonly List<Action> undoSteps = new();

    public int Count => undoSteps.Count;
    public bool HasEntries => undoSteps.Count > 0;

    public void Record(Action undo)
    {
        if (undo != null)
            undoSteps.Add(undo);
    }

    public bool RollbackAll(out string failureReason)
    {
        failureReason = string.Empty;

        for (int i = undoSteps.Count - 1; i >= 0; i--)
        {
            Action undo = undoSteps[i];
            if (undo == null)
                continue;

            try
            {
                undo();
            }
            catch (Exception exception)
            {
                failureReason =
                    $"Planning undo 실패: {exception.Message}";
                Debug.LogException(exception);
                return false;
            }
        }

        undoSteps.Clear();
        return true;
    }

    public void Clear()
    {
        undoSteps.Clear();
    }
}
