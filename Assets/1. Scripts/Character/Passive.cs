using UnityEngine;

public abstract class Passive
{
    protected Character owner;
    protected BattleEvent battleEvent;

    public string PassiveName { get; protected set; }

    public bool IsRegistered { get; private set; }

    //--------------------------------
    // 초기화
    //--------------------------------

    public virtual void Initialize(
        Character owner,
        BattleEvent battleEvent)
    {
        this.owner = owner;
        this.battleEvent = battleEvent;
    }

    //--------------------------------
    // 등록
    //--------------------------------

    public void Register()
    {
        if (IsRegistered)
            return;

        if (owner == null)
        {
            Debug.LogWarning($"{GetType().Name} Register 실패 : owner가 null입니다.");
            return;
        }

        if (battleEvent == null)
        {
            Debug.LogWarning($"{GetType().Name} Register 실패 : battleEvent가 null입니다.");
            return;
        }

        OnRegister();

        IsRegistered = true;
    }

    //--------------------------------
    // 해제
    //--------------------------------

    public void Unregister()
    {
        if (!IsRegistered)
            return;

        OnUnregister();

        IsRegistered = false;
    }

    //--------------------------------
    // 자식 클래스에서 구현
    //--------------------------------

    protected abstract void OnRegister();

    protected abstract void OnUnregister();
}