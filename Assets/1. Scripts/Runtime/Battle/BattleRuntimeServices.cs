using System.Collections;
using UnityEngine;

/// <summary>
/// BattleContext가 상위 BattleManager로 역참조하지 않고도
/// 실제 런타임 서비스에 접근할 수 있게 하는 명시적 dependency bundle.
/// </summary>
public sealed class BattleRuntimeServices
{
    public BattleLogger BattleLogger { get; set; }
    public ActionManager ActionManager { get; set; }
    public MomentumManager MomentumManager { get; set; }
    public FervorManager FervorManager { get; set; }
    public EmotionAugmentManager EmotionAugmentManager { get; set; }
    public SpeedManager SpeedManager { get; set; }
    public DamageManager DamageManager { get; set; }
    public ClashManager ClashManager { get; set; }
    public ActionResolver ActionResolver { get; set; }
    public ClashBuilder ClashBuilder { get; set; }
    public AIManager AIManager { get; set; }
    public TurnManager TurnManager { get; set; }
    public IBattleCoroutineHost CoroutineHost { get; set; }

    public BattleRuntimeServices Clone()
    {
        return new BattleRuntimeServices
        {
            BattleLogger = BattleLogger,
            ActionManager = ActionManager,
            MomentumManager = MomentumManager,
            FervorManager = FervorManager,
            EmotionAugmentManager = EmotionAugmentManager,
            SpeedManager = SpeedManager,
            DamageManager = DamageManager,
            ClashManager = ClashManager,
            ActionResolver = ActionResolver,
            ClashBuilder = ClashBuilder,
            AIManager = AIManager,
            TurnManager = TurnManager,
            CoroutineHost = CoroutineHost
        };
    }
}

/// <summary>
/// TurnManager가 BattleManager/MonoBehaviour concrete type에 의존하지 않고
/// coroutine lifecycle만 사용할 수 있게 하는 최소 계약.
/// </summary>
public interface IBattleCoroutineHost
{
    bool IsAvailable { get; }
    Coroutine Start(IEnumerator routine);
    void Stop(Coroutine coroutine);
}

/// <summary>
/// Unity MonoBehaviour를 IBattleCoroutineHost로 감싸는 Composition Root adapter.
/// </summary>
public sealed class UnityBattleCoroutineHost : IBattleCoroutineHost
{
    private readonly MonoBehaviour host;

    public UnityBattleCoroutineHost(MonoBehaviour host)
    {
        this.host = host;
    }

    public bool IsAvailable =>
        host != null &&
        host.isActiveAndEnabled;

    public Coroutine Start(IEnumerator routine)
    {
        if (!IsAvailable || routine == null)
            return null;

        return host.StartCoroutine(routine);
    }

    public void Stop(Coroutine coroutine)
    {
        if (host == null || coroutine == null)
            return;

        host.StopCoroutine(coroutine);
    }
}