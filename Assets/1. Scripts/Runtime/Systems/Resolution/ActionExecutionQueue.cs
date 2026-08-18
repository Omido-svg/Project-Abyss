using System.Collections.Generic;

/// <summary>
/// 한 턴의 최종 실행 순서를 보존한다.
/// 실행 순서는 Prestige -> Preparation -> Combat(Clash/OneSide)이다.
/// </summary>
public sealed class ActionExecutionQueue
{
    public Queue<ActionSlot> PrestigeQueue { get; } = new();

    public Queue<ActionSlot> PreparationQueue { get; } = new();


    public Queue<ClashPair> ClashQueue { get; set; } = new();

    public bool IsEmpty =>
        PrestigeQueue.Count == 0 &&
        PreparationQueue.Count == 0 &&
        ClashQueue.Count == 0;
}