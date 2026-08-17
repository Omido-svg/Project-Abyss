using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum BattleDebugTargetSide
{
    Player,
    Enemy
}

public enum BattleDebugStatusScope
{
    Character,
    BodyPart
}

public enum BattleDebugStatusOperation
{
    ApplyOrMerge,
    RemoveSameType
}

[Serializable]
public sealed class BattleDebugCoreBalance
{
    [Header("HP")]
    public bool overrideHP;
    [Min(0)] public int currentHP;
    [Min(1)] public int maxHP = 1;
    public bool preserveCurrentHpRatioWhenMaxChanges;

    [Header("Block")]
    public bool overrideBlock;
    [Min(0)] public int currentBlock;

    [Header("Prestige")]
    public bool overridePrestige;
    [Min(0)] public int currentPrestige;
    [Min(0)] public int maxPrestige = 100;

    [Header("Speed Range")]
    public bool overrideSpeedRange;
    public int minSpeed = 3;
    public int maxSpeed = 8;

    [Header("Offense")]
    public bool overrideOffense;
    public int flatDamageBonus;
    [Min(0f)] public float damageMultiplier = 1f;

    [Header("Defense")]
    public bool overrideDefense;
    [Min(0)] public int defense;
    [Range(0f, 1f)]
    public float defensePenetrationRate;

    [Header("Prestige Gain")]
    public bool overridePrestigeGain;
    [Min(0f)]
    public float prestigeGainMultiplier = 1f;

    public void Capture(Character character)
    {
        if (character == null)
            return;

        CurrentStatus current =
            character.CurrentStatus;

        RuntimeStatus runtime =
            character.RuntimeStatus;

        currentHP =
            runtime?.currentHP ?? 0;

        maxHP =
            Mathf.Max(
                1,
                character.MaxCombatHP);

        currentBlock =
            runtime?.currentBlock ?? 0;

        currentPrestige =
            runtime?.currentPrestige ?? 0;

        if (current == null)
            return;

        maxPrestige =
            current.maxPrestige;

        minSpeed =
            current.minSpeed;

        maxSpeed =
            current.maxSpeed;

        flatDamageBonus =
            current.flatDamageBonus;

        damageMultiplier =
            current.damageMultiplier;

        defense =
            current.defense;

        defensePenetrationRate =
            current.defensePenetrationRate;

        prestigeGainMultiplier =
            current.prestigeGainMultiplier;
    }

    public void Clamp()
    {
        currentHP =
            Mathf.Max(
                0,
                currentHP);

        maxHP =
            Mathf.Max(
                1,
                maxHP);

        currentBlock =
            Mathf.Max(
                0,
                currentBlock);

        maxPrestige =
            Mathf.Max(
                0,
                maxPrestige);

        currentPrestige =
            Mathf.Clamp(
                currentPrestige,
                0,
                maxPrestige);

        maxSpeed =
            Mathf.Max(
                minSpeed,
                maxSpeed);

        damageMultiplier =
            Mathf.Max(
                0f,
                damageMultiplier);

        defense =
            Mathf.Max(
                0,
                defense);

        defensePenetrationRate =
            Mathf.Clamp01(
                defensePenetrationRate);

        prestigeGainMultiplier =
            Mathf.Max(
                0f,
                prestigeGainMultiplier);
    }
}

[Serializable]
public sealed class BattleDebugPartTuning
{
    public bool enabled = true;

    public PartType partType;
    [Min(0)] public int fallbackPartIndex;

    [Min(0f)] public float currentHP = 1f;
    [Min(1f)] public float maxHP = 1f;

    public BodyPartState state =
        BodyPartState.Normal;

    [Header("Current Turn Speed")]
    public bool overrideRolledSpeed;
    [Min(0)] public int rolledSpeed;

    public void Capture(
        BodyPart part,
        int partIndex,
        SpeedManager speedManager)
    {
        if (part == null)
            return;

        partType =
            part.Type;

        fallbackPartIndex =
            Mathf.Max(
                0,
                partIndex);

        currentHP =
            part.PartHP;

        maxHP =
            part.MaxPartHP;

        state =
            part.State;

        rolledSpeed =
            speedManager == null
                ? 0
                : speedManager.GetSpeed(part);
    }

    public void Clamp()
    {
        fallbackPartIndex =
            Mathf.Max(
                0,
                fallbackPartIndex);

        maxHP =
            Mathf.Max(
                1f,
                maxHP);

        currentHP =
            Mathf.Clamp(
                currentHP,
                0f,
                maxHP);

        rolledSpeed =
            Mathf.Max(
                0,
                rolledSpeed);
    }
}

[Serializable]
public sealed class BattleDebugPartSettings
{
    public bool overrideParts;

    [Tooltip(
        "부위 상태를 바꿀 때 기존 출혈/화상 등 비구조 상태도 제거합니다. " +
        "초기 전투 상황 구성에는 true를 권장합니다.")]
    public bool clearNonStructuralStatusesWhenStateChanges = true;

    [Tooltip(
        "부위 설정 적용 후 총 HP를 남아 있는 부위 HP 합으로 다시 계산합니다. " +
        "Core Balance의 HP Override가 켜져 있으면 마지막에 그 값이 다시 적용됩니다.")]
    public bool syncTotalHPFromParts = true;

    public List<BattleDebugPartTuning> parts =
        new();
}

[Serializable]
public sealed class BattleDebugResourceTuning
{
    public bool enabled = true;
    public string key;
    [Min(0)] public int currentValue;
    [Min(0)] public int maxValue = 100;

    public void Clamp()
    {
        key =
            string.IsNullOrWhiteSpace(key)
                ? string.Empty
                : key.Trim();

        maxValue =
            Mathf.Max(
                0,
                maxValue);

        currentValue =
            Mathf.Clamp(
                currentValue,
                0,
                maxValue);
    }
}

[Serializable]
public sealed class BattleDebugCharacterAdvanced
{
    [Header("Current Turn Speed")]
    [Tooltip(
        "단일 HP 캐릭터의 현재 턴 속도 또는 부위별 속도 설정을 적용합니다.")]
    public bool overrideCurrentRolledSpeed;

    [Min(0)]
    public int singleHpRolledSpeed;

    [Header("Custom Resources")]
    public bool overrideCustomResources;

    public List<BattleDebugResourceTuning>
        customResources =
            new();

    [Header("Olaf")]
    public bool overrideOlafMadness;

    [Range(0, 5)]
    public int olafMadness;

    [Header("Safety")]
    public bool applyToDeadCharacter;

    public void Clamp()
    {
        singleHpRolledSpeed =
            Mathf.Max(
                0,
                singleHpRolledSpeed);

        olafMadness =
            Mathf.Clamp(
                olafMadness,
                0,
                5);

        if (customResources == null)
            customResources = new();

        foreach (BattleDebugResourceTuning resource
                 in customResources)
        {
            resource?.Clamp();
        }
    }
}

[Serializable]
public sealed class BattleDebugCharacterTuning
{
    public bool enabled = true;

    [SerializeField]
    private string characterName =
        "Unassigned";

    [Min(-1)]
    public int enemyIndex = -1;

    public BattleDebugCoreBalance core =
        new();

    public BattleDebugPartSettings partSettings =
        new();

    public BattleDebugCharacterAdvanced additionalSettings =
        new();

    public string CharacterName =>
        string.IsNullOrWhiteSpace(characterName)
            ? "Unassigned"
            : characterName;

    public void SetCharacterName(
        string value)
    {
        characterName =
            string.IsNullOrWhiteSpace(value)
                ? "Unassigned"
                : value;
    }

    public void Capture(
        Character character,
        int index,
        SpeedManager speedManager)
    {
        if (character == null)
            return;

        SetCharacterName(
            character.Data?.CharacterName ??
            character.name);

        enemyIndex =
            index;

        core ??=
            new BattleDebugCoreBalance();

        core.Capture(
            character);

        partSettings ??=
            new BattleDebugPartSettings();

        CaptureParts(
            character,
            speedManager);

        additionalSettings ??=
            new BattleDebugCharacterAdvanced();

        additionalSettings.singleHpRolledSpeed =
            speedManager == null
                ? 0
                : speedManager.GetSpeed(character);

        CaptureResources(
            character);

        OlafMadnessMechanic madness =
            character.GetMechanic<
                OlafMadnessMechanic>();

        additionalSettings.olafMadness =
            madness?.CurrentMadness ?? 0;
    }

    public void Clamp()
    {
        core ??=
            new BattleDebugCoreBalance();

        partSettings ??=
            new BattleDebugPartSettings();

        additionalSettings ??=
            new BattleDebugCharacterAdvanced();

        core.Clamp();

        partSettings.parts ??=
            new List<BattleDebugPartTuning>();

        foreach (BattleDebugPartTuning part
                 in partSettings.parts)
        {
            part?.Clamp();
        }

        additionalSettings.Clamp();
    }

    private void CaptureParts(
        Character character,
        SpeedManager speedManager)
    {
        if (character?.BodyParts == null)
            return;

        Dictionary<PartType, BattleDebugPartTuning>
            existingByType =
                new();

        if (partSettings.parts != null)
        {
            foreach (BattleDebugPartTuning existing
                     in partSettings.parts)
            {
                if (existing == null)
                    continue;

                existingByType[existing.partType] =
                    existing;
            }
        }

        List<BattleDebugPartTuning> captured =
            new();

        for (int i = 0;
             i < character.BodyParts.Count;
             i++)
        {
            BodyPart part =
                character.BodyParts[i];

            if (part == null)
                continue;

            BattleDebugPartTuning tuning =
                existingByType.TryGetValue(
                    part.Type,
                    out BattleDebugPartTuning existing)
                    ? existing
                    : new BattleDebugPartTuning();

            tuning.Capture(
                part,
                i,
                speedManager);

            captured.Add(tuning);
        }

        partSettings.parts =
            captured;
    }

    private void CaptureResources(
        Character character)
    {
        CombatResourceBank bank =
            character?.Resources;

        if (bank == null)
            return;

        Dictionary<string, int> values =
            bank.CaptureValues();

        Dictionary<string, int> maximums =
            bank.CaptureMaximums();

        Dictionary<string, BattleDebugResourceTuning>
            existingByKey =
                new(
                    StringComparer.OrdinalIgnoreCase);

        if (additionalSettings.customResources != null)
        {
            foreach (BattleDebugResourceTuning existing
                     in additionalSettings.customResources)
            {
                if (existing == null ||
                    string.IsNullOrWhiteSpace(existing.key))
                {
                    continue;
                }

                existingByKey[existing.key] =
                    existing;
            }
        }

        List<BattleDebugResourceTuning> captured =
            new();

        foreach (KeyValuePair<string, int> pair
                 in values)
        {
            BattleDebugResourceTuning tuning =
                existingByKey.TryGetValue(
                    pair.Key,
                    out BattleDebugResourceTuning existing)
                    ? existing
                    : new BattleDebugResourceTuning();

            tuning.key =
                pair.Key;

            tuning.currentValue =
                pair.Value;

            tuning.maxValue =
                maximums.TryGetValue(
                    pair.Key,
                    out int maxValue)
                    ? maxValue
                    : int.MaxValue;

            captured.Add(tuning);
        }

        additionalSettings.customResources =
            captured;
    }
}

[Serializable]
public sealed class BattleDebugStatusTestOperation
{
    public bool enabled = true;

    public BattleDebugTargetSide targetSide =
        BattleDebugTargetSide.Enemy;

    [Min(0)]
    public int enemyIndex;

    public BattleDebugStatusScope scope =
        BattleDebugStatusScope.BodyPart;

    public PartType partType =
        PartType.HEAD;

    public BattleDebugStatusOperation operation =
        BattleDebugStatusOperation.ApplyOrMerge;

    public StatusEffectId status =
        StatusEffectId.Bleeding;

    [Min(1)]
    public int stack = 1;

    [Min(1)]
    public int duration = 3;

    public void Clamp()
    {
        enemyIndex =
            Mathf.Max(
                0,
                enemyIndex);

        stack =
            Mathf.Max(
                1,
                stack);

        duration =
            Mathf.Max(
                1,
                duration);
    }
}

[Serializable]
public sealed class PartTuningValue
{
    public int partIndex;
    public int currentHP = 50;
    public int maxHP = 50;
    public bool isWeakened;
    public bool isBroken;
}

[Serializable]
public sealed class EnemyTuningValue
{
    public int enemyIndex;
    public int currentPrestige;
    public int maxPrestige = 100;
    public List<PartTuningValue> parts =
        new();
}

public sealed class BattleDebugTuner : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private BattleManager battleManager;

    [SerializeField]
    private BattleUIManager battleUIManager;

    [Header("Main Balance - Momentum")]
    [SerializeField]
    private bool overrideMomentum;

    [SerializeField, Range(-100f, 100f)]
    private float momentum;

    [Header("Main Balance - Player")]
    [SerializeField]
    private BattleDebugCharacterTuning playerTuning =
        new();

    [Header("Main Balance - Enemies")]
    [SerializeField]
    private List<BattleDebugCharacterTuning>
        enemyTunings =
            new();

    [Header("One Shot Status Test")]
    [SerializeField]
    private List<BattleDebugStatusTestOperation>
        statusOperations =
            new();

    [Header("Apply Settings")]
    [SerializeField]
    private bool applyOnStart;

    [SerializeField]
    private bool applyStatusOperationsOnStart;

    [SerializeField]
    private bool autoCaptureProfilesWhenEmpty = true;

    [SerializeField]
    private bool liveApply;

    [SerializeField, Min(0.05f)]
    private float liveApplyInterval = 0.25f;

    [SerializeField]
    private bool preventApplyDuringResolution = true;

    [SerializeField]
    private bool refreshUIAfterApply = true;

    [SerializeField]
    private bool refreshCharacterViewAfterApply = true;

    [SerializeField]
    private bool logApplySummary = true;

    [Header("Logger")]
    [SerializeField]
    private bool configureLogger = true;

    [SerializeField]
    private BattleLogCategory enabledLogCategories =
        BattleLogCategory.Combat |
        BattleLogCategory.Damage |
        BattleLogCategory.Status |
        BattleLogCategory.Event;

    [SerializeField]
    private BattleLogLevel minimumLogLevel =
        BattleLogLevel.Info;

    [SerializeField]
    private bool includeRealtimeInLog;

    [SerializeField]
    private bool includeFrameInLog = true;

    [SerializeField]
    private bool captureRecentLogs = true;

    [SerializeField, Min(0)]
    private int recentLogCapacity = 250;

    [SerializeField]
    private bool clearRecentLogsOnStart = true;

    //------------------------------------------------
    // Legacy serialized data
    //------------------------------------------------

    [SerializeField, HideInInspector]
    [FormerlySerializedAs("overridePlayerPrestige")]
    private bool legacyOverridePlayerPrestige;

    [SerializeField, HideInInspector]
    [FormerlySerializedAs("playerCurrentPrestige")]
    private int legacyPlayerCurrentPrestige;

    [SerializeField, HideInInspector]
    [FormerlySerializedAs("playerMaxPrestige")]
    private int legacyPlayerMaxPrestige = 100;

    [SerializeField, HideInInspector]
    [FormerlySerializedAs("overrideEnemyPrestige")]
    private bool legacyOverrideEnemyPrestige;

    [SerializeField, HideInInspector]
    [FormerlySerializedAs("overridePlayerParts")]
    private bool legacyOverridePlayerParts;

    [SerializeField, HideInInspector]
    [FormerlySerializedAs("playerParts")]
    private List<PartTuningValue>
        legacyPlayerParts =
            new();

    [SerializeField, HideInInspector]
    [FormerlySerializedAs("overrideEnemyParts")]
    private bool legacyOverrideEnemyParts;

    [SerializeField, HideInInspector]
    [FormerlySerializedAs("enemies")]
    private List<EnemyTuningValue>
        legacyEnemies =
            new();

    [SerializeField, HideInInspector]
    private int serializedVersion;

    private const int CurrentSerializedVersion = 2;

    private float liveApplyTimer;
    private bool liveApplyPendingAfterResolution;

    public bool IsRuntimeReady =>
        IsReady();

    public BattleManager BattleManager =>
        battleManager;

    private void Awake()
    {
        FindReferences();
        MigrateLegacySettingsIfNeeded();
        ApplyLoggerSettings();
    }

    private void OnEnable()
    {
        ApplyLoggerSettings();
    }

    private void Start()
    {
        FindReferences();
        MigrateLegacySettingsIfNeeded();

        if (clearRecentLogsOnStart)
            BattleDebugLog.ClearRecentMessages();

        ApplyLoggerSettings();

        if (autoCaptureProfilesWhenEmpty &&
            ProfilesNeedCapture())
        {
            CaptureCurrentBattleState();
        }

        if (applyOnStart)
            ApplyBalanceNow();

        if (applyStatusOperationsOnStart)
            ApplyStatusOperationsNow();
    }

    private void Update()
    {
        if (!Application.isPlaying ||
            !liveApply)
        {
            liveApplyPendingAfterResolution = false;
            return;
        }

        bool isResolving =
            battleManager?.TurnManager?.IsResolving == true;

        if (isResolving)
        {
            // Live Apply는 턴 해석 중 전투 상태를 건드리지 않는다.
            // 반복 적용과 반복 경고 대신 해석 종료 후 한 번만 적용한다.
            liveApplyPendingAfterResolution = true;
            liveApplyTimer =
                Mathf.Max(
                    0.05f,
                    liveApplyInterval);
            return;
        }

        if (liveApplyPendingAfterResolution)
        {
            liveApplyPendingAfterResolution = false;
            liveApplyTimer =
                Mathf.Max(
                    0.05f,
                    liveApplyInterval);

            ApplyBalanceInternal(
                logSummary: false);
            return;
        }

        liveApplyTimer -=
            Time.unscaledDeltaTime;

        if (liveApplyTimer > 0f)
            return;

        liveApplyTimer =
            Mathf.Max(
                0.05f,
                liveApplyInterval);

        ApplyBalanceInternal(
            logSummary: false);
    }

    private void OnValidate()
    {
        liveApplyInterval =
            Mathf.Max(
                0.05f,
                liveApplyInterval);

        recentLogCapacity =
            Mathf.Max(
                0,
                recentLogCapacity);

        momentum =
            Mathf.Clamp(
                momentum,
                -100f,
                100f);

        MigrateLegacySettingsIfNeeded();
        ClampAllSettings();
        ApplyLoggerSettings();
    }

    [ContextMenu("Capture Current Battle State")]
    public void CaptureCurrentBattleState()
    {
        FindReferences();

        if (!IsReady())
        {
            LogNotReady(
                nameof(CaptureCurrentBattleState));

            return;
        }

        SpeedManager speedManager =
            battleManager.SpeedManager;

        Character player =
            battleManager.BattleContext.Player;

        playerTuning ??=
            new BattleDebugCharacterTuning();

        playerTuning.Capture(
            player,
            -1,
            speedManager);

        List<Character> enemies =
            battleManager.BattleContext.Enemies;

        enemyTunings ??=
            new List<BattleDebugCharacterTuning>();

        Dictionary<int, BattleDebugCharacterTuning>
            existingByIndex =
                new();

        foreach (BattleDebugCharacterTuning existing
                 in enemyTunings)
        {
            if (existing == null)
                continue;

            existingByIndex[existing.enemyIndex] =
                existing;
        }

        List<BattleDebugCharacterTuning> captured =
            new();

        if (enemies != null)
        {
            for (int i = 0;
                 i < enemies.Count;
                 i++)
            {
                Character enemy =
                    enemies[i];

                if (enemy == null)
                    continue;

                BattleDebugCharacterTuning tuning =
                    existingByIndex.TryGetValue(
                        i,
                        out BattleDebugCharacterTuning existing)
                        ? existing
                        : new BattleDebugCharacterTuning();

                tuning.Capture(
                    enemy,
                    i,
                    speedManager);

                captured.Add(tuning);
            }
        }

        enemyTunings =
            captured;

        if (battleManager.MomentumManager != null)
        {
            momentum =
                battleManager
                    .MomentumManager
                    .CurrentMomentum;
        }

        ClampAllSettings();

        BattleDebugLog.Event(
            "[BattleDebugTuner] 현재 전투 상태 캡처 완료",
            BattleLogLevel.Info,
            this);
    }

    [ContextMenu("Apply Balance Now")]
    public void ApplyBalanceNow()
    {
        ApplyBalanceInternal(
            logSummary:
                logApplySummary);
    }

    [ContextMenu("Reroll All Speeds")]
    public void RerollAllSpeeds()
    {
        FindReferences();

        if (!IsReady())
        {
            LogNotReady(
                nameof(RerollAllSpeeds));

            return;
        }

        if (!CanApplyDuringCurrentPhase())
            return;

        battleManager.SpeedManager?
            .RollAllSpeed();

        battleManager.SpeedManager?
            .ApplySpeedToSlots(
                battleManager.ActionManager?.Slots);

        RefreshPresentation();

        BattleDebugLog.Combat(
            "[BattleDebugTuner] 전체 속도 재굴림 완료",
            BattleLogLevel.Info,
            this);
    }

    [ContextMenu("Apply Status Operations")]
    public void ApplyStatusOperationsNow()
    {
        FindReferences();

        if (!IsReady())
        {
            LogNotReady(
                nameof(ApplyStatusOperationsNow));

            return;
        }

        if (!CanApplyDuringCurrentPhase())
            return;

        int successCount = 0;

        if (statusOperations != null)
        {
            foreach (BattleDebugStatusTestOperation operation
                     in statusOperations)
            {
                if (operation == null ||
                    !operation.enabled)
                {
                    continue;
                }

                operation.Clamp();

                if (ApplyStatusOperation(operation))
                    successCount++;
            }
        }

        RefreshPresentation();

        BattleDebugLog.Status(
            $"[BattleDebugTuner] 상태이상 작업 완료 / " +
            $"Success={successCount}",
            BattleLogLevel.Info,
            this);
    }

    [ContextMenu("Remove Configured Status Types")]
    public void RemoveConfiguredStatusTypesNow()
    {
        FindReferences();

        if (!IsReady())
        {
            LogNotReady(
                nameof(RemoveConfiguredStatusTypesNow));

            return;
        }

        if (!CanApplyDuringCurrentPhase())
            return;

        int removedCount = 0;

        if (statusOperations != null)
        {
            foreach (BattleDebugStatusTestOperation operation
                     in statusOperations)
            {
                if (operation == null ||
                    !operation.enabled)
                {
                    continue;
                }

                removedCount +=
                    RemoveStatusType(
                        ResolveStatusTarget(operation),
                        ResolveStatusPart(operation),
                        operation.status,
                        operation.scope);
            }
        }

        RefreshPresentation();

        BattleDebugLog.Status(
            $"[BattleDebugTuner] 설정된 상태이상 제거 완료 / " +
            $"Removed={removedCount}",
            BattleLogLevel.Info,
            this);
    }

    [ContextMenu("Apply Logger Settings")]
    public void ApplyLoggerSettings()
    {
        if (!configureLogger)
            return;

        BattleDebugLog.Configure(
            enabledLogCategories,
            minimumLogLevel,
            includeRealtimeInLog,
            includeFrameInLog,
            captureRecentLogs,
            recentLogCapacity);
    }

    [ContextMenu("Clear Recent Logs")]
    public void ClearRecentLogs()
    {
        BattleDebugLog.ClearRecentMessages();
    }

    [ContextMenu("Print Current Tuning Summary")]
    public void PrintCurrentTuningSummary()
    {
        FindReferences();

        string playerName =
            playerTuning?.CharacterName ??
            "NULL";

        BattleDebugLog.Event(
            "[BattleDebugTuner] Configuration / " +
            $"Ready={IsReady()}, " +
            $"Player={playerName}, " +
            $"EnemyProfiles={enemyTunings?.Count ?? 0}, " +
            $"MomentumOverride={overrideMomentum}, " +
            $"LiveApply={liveApply}, " +
            $"StatusOperations={statusOperations?.Count ?? 0}",
            BattleLogLevel.Info,
            this);
    }

    private void ApplyBalanceInternal(
        bool logSummary)
    {
        FindReferences();

        if (!IsReady())
        {
            if (logSummary)
            {
                LogNotReady(
                    nameof(ApplyBalanceNow));
            }

            return;
        }

        if (!CanApplyDuringCurrentPhase())
            return;

        ClampAllSettings();

        if (overrideMomentum &&
            battleManager.MomentumManager != null)
        {
            battleManager.MomentumManager
                .SetMomentumForDebug(
                    momentum);
        }

        int appliedCharacterCount = 0;

        Character player =
            battleManager.BattleContext.Player;

        if (ApplyCharacterTuning(
                player,
                playerTuning))
        {
            appliedCharacterCount++;
        }

        if (enemyTunings != null)
        {
            foreach (BattleDebugCharacterTuning tuning
                     in enemyTunings)
            {
                Character enemy =
                    ResolveEnemy(tuning);

                if (ApplyCharacterTuning(
                        enemy,
                        tuning))
                {
                    appliedCharacterCount++;
                }
            }
        }

        battleManager.SpeedManager?
            .ApplySpeedToSlots(
                battleManager.ActionManager?.Slots);

        RefreshPresentation();

        if (logSummary)
        {
            BattleDebugLog.Event(
                "[BattleDebugTuner] 밸런스 적용 완료 / " +
                $"Characters={appliedCharacterCount}, " +
                $"Momentum=" +
                $"{(overrideMomentum ? momentum.ToString("0") : "UNCHANGED")}",
                BattleLogLevel.Info,
                this);
        }
    }

    private bool ApplyCharacterTuning(
        Character character,
        BattleDebugCharacterTuning tuning)
    {
        if (character == null ||
            tuning == null ||
            !tuning.enabled ||
            character.CurrentStatus == null ||
            character.RuntimeStatus == null)
        {
            return false;
        }

        BattleDebugCharacterAdvanced advanced =
            tuning.additionalSettings ??
            new BattleDebugCharacterAdvanced();

        if (character.IsDead &&
            !advanced.applyToDeadCharacter)
        {
            return false;
        }

        BattleDebugCoreBalance core =
            tuning.core ??
            new BattleDebugCoreBalance();

        ApplyCurrentStatus(
            character.CurrentStatus,
            core);

        ApplyMaxHp(
            character,
            core);

        ApplyPartSettings(
            character,
            tuning.partSettings);

        if (tuning.partSettings != null &&
            tuning.partSettings.overrideParts &&
            tuning.partSettings.syncTotalHPFromParts)
        {
            character.ForceRecalculateHP();
        }

        ApplyRuntimeStatus(
            character,
            core);

        ApplyCustomResources(
            character,
            advanced);

        ApplyOlafMadness(
            character,
            advanced);

        ApplyCurrentRolledSpeed(
            character,
            tuning);

        character.RuntimeStatus.Clamp(
            character.CurrentStatus,
            character.MaxCombatHP);

        RefreshCharacterView(
            character);

        return true;
    }

    private static void ApplyCurrentStatus(
        CurrentStatus status,
        BattleDebugCoreBalance core)
    {
        if (status == null ||
            core == null)
        {
            return;
        }

        if (core.overridePrestige)
        {
            status.maxPrestige =
                core.maxPrestige;
        }

        if (core.overrideSpeedRange)
        {
            status.minSpeed =
                core.minSpeed;

            status.maxSpeed =
                core.maxSpeed;
        }

        if (core.overrideOffense)
        {
            status.flatDamageBonus =
                core.flatDamageBonus;

            status.damageMultiplier =
                core.damageMultiplier;
        }

        if (core.overrideDefense)
        {
            status.defense =
                core.defense;

            status.defensePenetrationRate =
                core.defensePenetrationRate;
        }

        if (core.overridePrestigeGain)
        {
            status.prestigeGainMultiplier =
                core.prestigeGainMultiplier;
        }

        status.Clamp();
    }

    private static void ApplyMaxHp(
        Character character,
        BattleDebugCoreBalance core)
    {
        if (character == null ||
            core == null ||
            !core.overrideHP ||
            !character.IsSingleHpTarget)
        {
            return;
        }

        character.SetMaxCombatHpForDebug(
            core.maxHP,
            core.preserveCurrentHpRatioWhenMaxChanges);
    }

    private static void ApplyRuntimeStatus(
        Character character,
        BattleDebugCoreBalance core)
    {
        if (character?.RuntimeStatus == null ||
            core == null)
        {
            return;
        }

        if (core.overrideHP)
        {
            character.RuntimeStatus.currentHP =
                Mathf.Clamp(
                    core.currentHP,
                    0,
                    character.MaxCombatHP);
        }

        if (core.overrideBlock)
        {
            character.RuntimeStatus.currentBlock =
                Mathf.Max(
                    0,
                    core.currentBlock);
        }

        if (core.overridePrestige)
        {
            character.RuntimeStatus.currentPrestige =
                Mathf.Clamp(
                    core.currentPrestige,
                    0,
                    character.CurrentStatus?.maxPrestige ?? 0);
        }
    }

    private void ApplyPartSettings(
        Character character,
        BattleDebugPartSettings settings)
    {
        if (character == null ||
            settings == null ||
            !settings.overrideParts ||
            character.BodyParts == null ||
            settings.parts == null)
        {
            return;
        }

        foreach (BattleDebugPartTuning tuning
                 in settings.parts)
        {
            if (tuning == null ||
                !tuning.enabled)
            {
                continue;
            }

            BodyPart part =
                ResolvePart(
                    character,
                    tuning.partType,
                    tuning.fallbackPartIndex);

            if (part == null)
                continue;

            BodyPartState previousState =
                part.State;

            character.SetBodyPartStateForDebug(
                part,
                tuning.currentHP,
                tuning.maxHP,
                tuning.state,
                settings
                    .clearNonStructuralStatusesWhenStateChanges);

            if (tuning.state ==
                    BodyPartState.Broken &&
                previousState !=
                    BodyPartState.Broken)
            {
                battleManager.ActionManager?
                    .RemoveSlots(
                        character,
                        part);
            }
        }
    }

    private static void ApplyCustomResources(
        Character character,
        BattleDebugCharacterAdvanced advanced)
    {
        if (character == null ||
            advanced == null ||
            !advanced.overrideCustomResources ||
            advanced.customResources == null)
        {
            return;
        }

        foreach (BattleDebugResourceTuning resource
                 in advanced.customResources)
        {
            if (resource == null ||
                !resource.enabled ||
                string.IsNullOrWhiteSpace(resource.key))
            {
                continue;
            }

            character.SetCustomResource(
                resource.key,
                resource.currentValue,
                resource.maxValue);
        }
    }

    private static void ApplyOlafMadness(
        Character character,
        BattleDebugCharacterAdvanced advanced)
    {
        if (character == null ||
            advanced == null ||
            !advanced.overrideOlafMadness)
        {
            return;
        }

        character
            .GetMechanic<OlafMadnessMechanic>()
            ?.SetMadnessForDebug(
                advanced.olafMadness);
    }

    private void ApplyCurrentRolledSpeed(
        Character character,
        BattleDebugCharacterTuning tuning)
    {
        if (character == null ||
            tuning == null ||
            battleManager.SpeedManager == null)
        {
            return;
        }

        BattleDebugCharacterAdvanced advanced =
            tuning.additionalSettings;

        if (character.IsSingleHpTarget)
        {
            if (advanced != null &&
                advanced.overrideCurrentRolledSpeed)
            {
                battleManager.SpeedManager
                    .SetSpeedForDebug(
                        character,
                        null,
                        advanced.singleHpRolledSpeed);
            }

            return;
        }

        BattleDebugPartSettings parts =
            tuning.partSettings;

        if (parts?.parts == null)
            return;

        foreach (BattleDebugPartTuning partTuning
                 in parts.parts)
        {
            if (partTuning == null ||
                !partTuning.enabled ||
                !partTuning.overrideRolledSpeed)
            {
                continue;
            }

            BodyPart part =
                ResolvePart(
                    character,
                    partTuning.partType,
                    partTuning.fallbackPartIndex);

            if (part == null)
                continue;

            battleManager.SpeedManager
                .SetSpeedForDebug(
                    character,
                    part,
                    partTuning.rolledSpeed);
        }
    }

    private bool ApplyStatusOperation(
        BattleDebugStatusTestOperation operation)
    {
        Character target =
            ResolveStatusTarget(operation);

        if (target == null)
            return false;

        BodyPart targetPart =
            ResolveStatusPart(operation);

        if (operation.scope ==
                BattleDebugStatusScope.BodyPart &&
            targetPart == null)
        {
            return false;
        }

        if (operation.operation ==
                BattleDebugStatusOperation.RemoveSameType)
        {
            return RemoveStatusType(
                       target,
                       targetPart,
                       operation.status,
                       operation.scope) > 0;
        }

        StatusEffect effect =
            StatusEffectFactory.Create(
                operation.status,
                operation.stack,
                operation.duration);

        if (effect == null)
            return false;

        if (operation.scope ==
            BattleDebugStatusScope.Character)
        {
            target.AddStatus(
                effect,
                target);

            return true;
        }

        target.AddPartStatus(
            targetPart,
            effect,
            target);

        return true;
    }

    private int RemoveStatusType(
        Character target,
        BodyPart targetPart,
        StatusEffectId statusId,
        BattleDebugStatusScope scope)
    {
        if (target == null)
            return 0;

        Type targetType =
            GetStatusType(
                statusId);

        if (targetType == null)
            return 0;

        int removed = 0;

        if (scope ==
            BattleDebugStatusScope.Character)
        {
            List<StatusEffect> snapshot =
                new(
                    target.StatusEffects);

            foreach (StatusEffect effect in snapshot)
            {
                if (effect == null ||
                    effect.GetType() != targetType)
                {
                    continue;
                }

                target.RemoveStatus(
                    effect,
                    StatusEffectRemoveReason.Manual);

                removed++;
            }

            return removed;
        }

        if (targetPart == null)
            return 0;

        List<StatusEffect> partSnapshot =
            new(
                targetPart.StatusEffects);

        foreach (StatusEffect effect in partSnapshot)
        {
            if (effect == null ||
                effect.GetType() != targetType)
            {
                continue;
            }

            target.RemovePartStatus(
                targetPart,
                effect,
                StatusEffectRemoveReason.Manual);

            removed++;
        }

        return removed;
    }

    private Character ResolveStatusTarget(
        BattleDebugStatusTestOperation operation)
    {
        if (operation == null ||
            battleManager?.BattleContext == null)
        {
            return null;
        }

        if (operation.targetSide ==
            BattleDebugTargetSide.Player)
        {
            return battleManager
                .BattleContext
                .Player;
        }

        List<Character> enemies =
            battleManager
                .BattleContext
                .Enemies;

        if (enemies == null ||
            operation.enemyIndex < 0 ||
            operation.enemyIndex >= enemies.Count)
        {
            return null;
        }

        return enemies[
            operation.enemyIndex];
    }

    private BodyPart ResolveStatusPart(
        BattleDebugStatusTestOperation operation)
    {
        if (operation == null ||
            operation.scope ==
                BattleDebugStatusScope.Character)
        {
            return null;
        }

        Character target =
            ResolveStatusTarget(operation);

        return ResolvePart(
            target,
            operation.partType,
            0);
    }

    private Character ResolveEnemy(
        BattleDebugCharacterTuning tuning)
    {
        if (tuning == null ||
            battleManager?.BattleContext?.Enemies == null)
        {
            return null;
        }

        List<Character> enemies =
            battleManager
                .BattleContext
                .Enemies;

        if (tuning.enemyIndex >= 0 &&
            tuning.enemyIndex < enemies.Count)
        {
            Character indexed =
                enemies[tuning.enemyIndex];

            if (indexed != null)
                return indexed;
        }

        foreach (Character enemy in enemies)
        {
            string enemyName =
                enemy?.Data?.CharacterName ??
                enemy?.name;

            if (string.Equals(
                    enemyName,
                    tuning.CharacterName,
                    StringComparison.Ordinal))
            {
                return enemy;
            }
        }

        return null;
    }

    private static BodyPart ResolvePart(
        Character character,
        PartType partType,
        int fallbackIndex)
    {
        if (character?.BodyParts == null)
            return null;

        foreach (BodyPart part
                 in character.BodyParts)
        {
            if (part != null &&
                part.Type == partType)
            {
                return part;
            }
        }

        if (fallbackIndex < 0 ||
            fallbackIndex >=
                character.BodyParts.Count)
        {
            return null;
        }

        return character.BodyParts[
            fallbackIndex];
    }

    private static Type GetStatusType(
        StatusEffectId id)
    {
        return id switch
        {
            StatusEffectId.Bleeding =>
                typeof(Bleeding),
            StatusEffectId.Burn =>
                typeof(Burn),
            StatusEffectId.Stun =>
                typeof(Stun),
            _ =>
                null
        };
    }

    private void RefreshPresentation()
    {
        if (refreshUIAfterApply &&
            battleUIManager != null)
        {
            battleUIManager
                .RefreshAllBodyPartButtons();
        }
    }

    private void RefreshCharacterView(
        Character character)
    {
        if (!refreshCharacterViewAfterApply ||
            character == null)
        {
            return;
        }

        CharacterView view =
            character.GetComponent<
                CharacterView>();

        if (view == null)
        {
            view =
                character.GetComponentInChildren<
                    CharacterView>();
        }

        view?.RefreshVisualState();
    }

    private bool CanApplyDuringCurrentPhase()
    {
        if (!preventApplyDuringResolution)
            return true;

        if (battleManager?.TurnManager == null ||
            !battleManager.TurnManager.IsResolving)
        {
            return true;
        }

        BattleDebugLog.Warning(
            BattleLogCategory.Event,
            "[BattleDebugTuner] 턴 해석 중에는 설정을 적용할 수 없습니다.",
            this);

        return false;
    }

    private void FindReferences()
    {
        if (battleManager == null)
        {
            battleManager =
                FindFirstObjectByType<
                    BattleManager>();
        }

        if (battleUIManager == null)
        {
            battleUIManager =
                FindFirstObjectByType<
                    BattleUIManager>();
        }
    }

    private bool IsReady()
    {
        return
            battleManager != null &&
            battleManager.BattleContext != null &&
            battleManager.BattleContext.Player != null;
    }

    private bool ProfilesNeedCapture()
    {
        if (playerTuning == null ||
            playerTuning.CharacterName ==
                "Unassigned")
        {
            return true;
        }

        List<Character> enemies =
            battleManager?.BattleContext?.Enemies;

        int expectedEnemyCount =
            enemies?.Count ?? 0;

        return
            enemyTunings == null ||
            enemyTunings.Count !=
                expectedEnemyCount;
    }

    private void ClampAllSettings()
    {
        playerTuning ??=
            new BattleDebugCharacterTuning();

        enemyTunings ??=
            new List<BattleDebugCharacterTuning>();

        statusOperations ??=
            new List<BattleDebugStatusTestOperation>();

        playerTuning.Clamp();

        foreach (BattleDebugCharacterTuning tuning
                 in enemyTunings)
        {
            tuning?.Clamp();
        }

        foreach (BattleDebugStatusTestOperation operation
                 in statusOperations)
        {
            operation?.Clamp();
        }
    }

    private void MigrateLegacySettingsIfNeeded()
    {
        if (serializedVersion >=
            CurrentSerializedVersion)
        {
            return;
        }

        playerTuning ??=
            new BattleDebugCharacterTuning();

        if (legacyOverridePlayerPrestige)
        {
            playerTuning.core.overridePrestige =
                true;

            playerTuning.core.currentPrestige =
                legacyPlayerCurrentPrestige;

            playerTuning.core.maxPrestige =
                legacyPlayerMaxPrestige;
        }

        if (legacyOverridePlayerParts &&
            legacyPlayerParts != null)
        {
            playerTuning.partSettings.overrideParts =
                true;

            playerTuning.partSettings.parts =
                ConvertLegacyParts(
                    legacyPlayerParts);
        }

        if ((legacyOverrideEnemyPrestige ||
             legacyOverrideEnemyParts) &&
            legacyEnemies != null &&
            legacyEnemies.Count > 0)
        {
            enemyTunings ??=
                new List<BattleDebugCharacterTuning>();

            if (enemyTunings.Count == 0)
            {
                foreach (EnemyTuningValue legacy
                         in legacyEnemies)
                {
                    if (legacy == null)
                        continue;

                    BattleDebugCharacterTuning tuning =
                        new()
                        {
                            enemyIndex =
                                legacy.enemyIndex
                        };

                    tuning.SetCharacterName(
                        $"Enemy[{legacy.enemyIndex}]");

                    if (legacyOverrideEnemyPrestige)
                    {
                        tuning.core.overridePrestige =
                            true;

                        tuning.core.currentPrestige =
                            legacy.currentPrestige;

                        tuning.core.maxPrestige =
                            legacy.maxPrestige;
                    }

                    if (legacyOverrideEnemyParts)
                    {
                        tuning.partSettings.overrideParts =
                            true;

                        tuning.partSettings.parts =
                            ConvertLegacyParts(
                                legacy.parts);
                    }

                    enemyTunings.Add(tuning);
                }
            }
        }

        serializedVersion =
            CurrentSerializedVersion;

        ClampAllSettings();
    }

    private static List<BattleDebugPartTuning>
        ConvertLegacyParts(
            IReadOnlyList<PartTuningValue> legacyParts)
    {
        List<BattleDebugPartTuning> result =
            new();

        if (legacyParts == null)
            return result;

        foreach (PartTuningValue legacy
                 in legacyParts)
        {
            if (legacy == null)
                continue;

            result.Add(
                new BattleDebugPartTuning
                {
                    fallbackPartIndex =
                        legacy.partIndex,
                    currentHP =
                        legacy.currentHP,
                    maxHP =
                        legacy.maxHP,
                    state =
                        legacy.isBroken
                            ? BodyPartState.Broken
                            : legacy.isWeakened
                                ? BodyPartState.Weakened
                                : BodyPartState.Normal
                });
        }

        return result;
    }

    private void LogNotReady(
        string operation)
    {
        BattleDebugLog.Warning(
            BattleLogCategory.Event,
            "[BattleDebugTuner] 전투 초기화 전에는 실행할 수 없습니다. " +
            $"Operation={operation}",
            this);
    }
}