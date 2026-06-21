using System.Collections.Generic;

public class ActionManager
{
    BattleContext _battleContext;

    public ActionManager(BattleContext _battleContext)
    {
        this._battleContext = _battleContext;
    }
    
    private readonly List<BattleAction> actionQueue = new();

    public IReadOnlyList<BattleAction> Actions => actionQueue;

    /// 이번 턴 행동 초기화
    public void Clear()
    {
        actionQueue.Clear();
    }

    /// 행동 추가
    public void AddAction(BattleAction action)
    {
        actionQueue.Add(action);
    }

    /// 속도순 정렬
    public void SortBySpeed()
    {
        actionQueue.Sort((a, b) => b.Speed.CompareTo(a.Speed));
    }

    /// 실행할 행동 반환
    public Queue<BattleAction> BuildQueue()
    {
        return new Queue<BattleAction>(actionQueue);
    }
    
    public void CreateActions()
    {
        List<Character> characters = _battleContext.AllCharacters;
        
        actionQueue.Clear();

        foreach (Character character in characters)
        {
            if (character.IsDead)
                continue;

            foreach (BodyPart part in character.BodyParts)
            {
                // 행동하지 않는 부위
                if (part.SelectedSkill == null)
                    continue;

                BattleAction action = new BattleAction()
                {
                    Owner = character,
                    Target = part.SelectedTarget,

                    OwnerPart = part,
                    TargetPart = part.SelectedTargetPart,

                    Skill = part.SelectedSkill,

                    Speed = part.CurrentSpeed
                };

                actionQueue.Add(action);
            }
        }

        SortBySpeed();
    }
}