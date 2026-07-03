using UnityEngine;

public class BattleUIEventBinder : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleUIManager uiManager;
    [SerializeField] private BattleUIRefreshScheduler refreshScheduler;

    private BattleEvent battleEvent;

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (uiManager == null)
            uiManager = FindFirstObjectByType<BattleUIManager>();

        if (refreshScheduler == null)
            refreshScheduler = FindFirstObjectByType<BattleUIRefreshScheduler>();
    }

    private bool isBound;

    private void Start()
    {
        TryBind();
    }

    private void Update()
    {
        if (isBound)
            return;

        TryBind();
    }

    private void TryBind()
    {
        if (isBound)
            return;

        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (battleManager == null)
            return;

        if (battleManager.BattleContext == null)
            return;

        battleEvent = battleManager.BattleContext._battleEvent;

        if (battleEvent == null)
            return;

        SubscribeBattleEvents();

        isBound = true;

        Debug.Log("[BattleUIEventBinder] BattleEvent subscribed");
    }

    public void Unbind()
    {
        if (battleEvent == null)
            return;

        UnsubscribeBattleEvents();

        battleEvent = null;
        isBound = false;
    }

    private void SubscribeBattleEvents()
    {
        battleEvent.OnTurnStart -= HandleTurnStart;
        battleEvent.OnTurnStart += HandleTurnStart;

        battleEvent.OnTurnEnd -= HandleTurnEnd;
        battleEvent.OnTurnEnd += HandleTurnEnd;

        battleEvent.OnActionStart -= HandleActionChanged;
        battleEvent.OnActionStart += HandleActionChanged;

        battleEvent.OnActionEnd -= HandleActionChanged;
        battleEvent.OnActionEnd += HandleActionChanged;

        battleEvent.OnDamageTaken -= HandleDamageTaken;
        battleEvent.OnDamageTaken += HandleDamageTaken;

        battleEvent.OnDamageDealt -= HandleDamageDealt;
        battleEvent.OnDamageDealt += HandleDamageDealt;

        battleEvent.OnStatusApplied -= HandleStatusChanged;
        battleEvent.OnStatusApplied += HandleStatusChanged;

        battleEvent.OnStatusRemoved -= HandleStatusChanged;
        battleEvent.OnStatusRemoved += HandleStatusChanged;

        battleEvent.OnBodyPartWeakened -= HandleBodyPartChanged;
        battleEvent.OnBodyPartWeakened += HandleBodyPartChanged;

        battleEvent.OnBodyPartDestroyed -= HandleBodyPartChanged;
        battleEvent.OnBodyPartDestroyed += HandleBodyPartChanged;

        battleEvent.OnBodyPartRecovered -= HandleBodyPartChanged;
        battleEvent.OnBodyPartRecovered += HandleBodyPartChanged;

        battleEvent.OnBodyPartStatusApplied -= HandleBodyPartStatusChanged;
        battleEvent.OnBodyPartStatusApplied += HandleBodyPartStatusChanged;

        battleEvent.OnBodyPartStatusRemoved -= HandleBodyPartStatusChanged;
        battleEvent.OnBodyPartStatusRemoved += HandleBodyPartStatusChanged;

        battleEvent.OnCharacterDeath -= HandleCharacterChanged;
        battleEvent.OnCharacterDeath += HandleCharacterChanged;

        battleEvent.OnClashResolved -= HandleClashResolved;
        battleEvent.OnClashResolved += HandleClashResolved;

        battleEvent.OnDamageResolved -= HandleDamageResolved;
        battleEvent.OnDamageResolved += HandleDamageResolved;
    }

    private void UnsubscribeBattleEvents()
    {
        battleEvent.OnTurnStart -= HandleTurnStart;
        battleEvent.OnTurnEnd -= HandleTurnEnd;

        battleEvent.OnActionStart -= HandleActionChanged;
        battleEvent.OnActionEnd -= HandleActionChanged;

        battleEvent.OnDamageTaken -= HandleDamageTaken;
        battleEvent.OnDamageDealt -= HandleDamageDealt;

        battleEvent.OnStatusApplied -= HandleStatusChanged;
        battleEvent.OnStatusRemoved -= HandleStatusChanged;

        battleEvent.OnBodyPartWeakened -= HandleBodyPartChanged;
        battleEvent.OnBodyPartDestroyed -= HandleBodyPartChanged;
        battleEvent.OnBodyPartRecovered -= HandleBodyPartChanged;

        battleEvent.OnBodyPartStatusApplied -= HandleBodyPartStatusChanged;
        battleEvent.OnBodyPartStatusRemoved -= HandleBodyPartStatusChanged;

        battleEvent.OnCharacterDeath -= HandleCharacterChanged;

        battleEvent.OnClashResolved -= HandleClashResolved;
        battleEvent.OnDamageResolved -= HandleDamageResolved;
    }

    private void HandleTurnStart(int turn)
    {
        uiManager?.ResetTurnInputState();
        refreshScheduler?.MarkAllDirty();
    }

    private void HandleTurnEnd(int turn)
    {
        refreshScheduler?.MarkAllDirty();
    }

    private void HandleActionChanged(BattleAction action)
    {
        refreshScheduler?.MarkAllDirty();
    }

    private void HandleDamageTaken(
        Character character,
        int damage)
    {
        refreshScheduler?.MarkCharacterDirty(character);
    }

    private void HandleDamageDealt(
        Character character,
        int damage)
    {
        refreshScheduler?.MarkCharacterDirty(character);
    }

    private void HandleStatusChanged(
        Character character,
        StatusEffect effect)
    {
        refreshScheduler?.MarkCharacterDirty(character);
    }

    private void HandleBodyPartChanged(
        Character character,
        BodyPart part)
    {
        refreshScheduler?.MarkBodyPartDirty(
            character,
            part);
    }

    private void HandleBodyPartStatusChanged(
        Character character,
        BodyPart part,
        StatusEffect effect)
    {
        refreshScheduler?.MarkBodyPartDirty(
            character,
            part);
    }

    private void HandleCharacterChanged(Character character)
    {
        refreshScheduler?.MarkCharacterDirty(character);
        refreshScheduler?.MarkAllDirty();
    }

    private void HandleClashResolved(ClashResultContext context)
    {
        refreshScheduler?.MarkAllDirty();
    }

    private void HandleDamageResolved(DamageContext context)
    {
        refreshScheduler?.MarkAllDirty();
    }
}