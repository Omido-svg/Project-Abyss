using System.Collections.Generic;

/// <summary>
/// BattleUIManager에서 Planning 선택 상태의 저장 책임을 분리한다.
/// 기존 BattleUIManager public facade는 그대로 유지한다.
/// </summary>
public sealed class BattlePlanningSelectionState
{
    public BattleInputMode InputMode { get; set; } =
        BattleInputMode.SelectOwner;

    public Character SelectedOwner { get; set; }
    public BodyPart SelectedOwnerPart { get; set; }

    public int SelectedActionIndex { get; set; }
    public int SelectedMaxActionSlots { get; set; } = 1;

    public Character SelectedTarget { get; set; }
    public BodyPart SelectedTargetPart { get; set; }

    public Dictionary<BodyPart, int>
        ActionIndexCursorByPart { get; } = new();

    public void Reset()
    {
        InputMode = BattleInputMode.SelectOwner;
        SelectedOwner = null;
        SelectedOwnerPart = null;
        SelectedActionIndex = 0;
        SelectedMaxActionSlots = 1;
        SelectedTarget = null;
        SelectedTargetPart = null;
    }
}
