using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Run Flow Test Scene -> 실제 Battle Test Scene -> Run Flow Test Scene 왕복 중
/// 같은 RunProgressionState와 RunFlowTestSession 인스턴스를 보존하는 런타임 handoff.
/// 저장 시스템을 대신하지 않는다. Play session 안에서의 실제 통합 검증용이다.
/// </summary>
public static class RunFlowLiveHandoff
{
    public const string RunSceneName = "Run Flow Test";
    public const string BattleSceneName = "Battle Test Scene";

    private const string EncounterPreferenceKey =
        "ProjectAbyss.TestEncounter.Mode";
    private const string PlayerPreferenceKey =
        "ProjectAbyss.TestEncounter.Player";
    private const string EmotionPreferenceKey =
        "ProjectAbyss.TestEncounter.Emotion";

    public static RunFlowTestSession RetainedSession { get; private set; }
    public static RunFlowTestNodeType PendingNode { get; private set; }
    public static bool BattleInFlight { get; private set; }
    public static RunFlowLiveBattleResult PendingResult { get; private set; }
    public static RunFlowLiveTransferAudit TransferAudit { get; private set; }

    public static bool HasRetainedSession => RetainedSession != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        RetainedSession = null;
        PendingNode = default;
        BattleInFlight = false;
        PendingResult = null;
        TransferAudit = null;
    }

    public static bool BeginBattle(
        RunFlowTestSession session,
        RunFlowTestNodeType node,
        out string message)
    {
        message = string.Empty;

        if (session == null)
        {
            message = "Run session이 없습니다.";
            return false;
        }

        if (!IsBattleNode(node))
        {
            message = $"실전 전투로 넘길 수 없는 노드입니다: {node}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(session.SelectedCharacterKey))
        {
            message = "캐릭터를 먼저 선택하세요.";
            return false;
        }

        if (!session.SelectedEmotion.HasValue)
        {
            message = "시작 감정을 먼저 선택하세요.";
            return false;
        }

        if (BattleInFlight)
        {
            message = "이미 Live Battle handoff가 진행 중입니다.";
            return false;
        }

        RunFlowLiveContentRegistry registry =
            RunFlowLiveContentRegistry.Load();

        if (registry == null || registry.RunItemCatalog == null)
        {
            message =
                "RunFlowLiveContentRegistry/RunItemCatalog가 없습니다. " +
                "Tools > Project Abyss > Run Flow Test > Install Live Integration을 실행하세요.";
            return false;
        }

        if (registry.ResolvePlayerPrefab(session.SelectedCharacterKey) == null)
        {
            message = $"선택 캐릭터 prefab이 Registry에 없습니다: {session.SelectedCharacterKey}";
            return false;
        }

        RetainedSession = session;
        PendingNode = node;
        BattleInFlight = true;
        PendingResult = null;
        TransferAudit = new RunFlowLiveTransferAudit
        {
            CharacterKey = session.SelectedCharacterKey,
            Node = node,
            InventoryCount = session.Progression?.Inventory?.Owned?.Count ?? 0
        };

        WriteBattleTestPreferences(
            session.SelectedCharacterKey,
            node,
            session.SelectedEmotion.Value);

        message =
            $"LIVE Battle 준비 / Character={session.SelectedCharacterKey}, " +
            $"Node={RunFlowTestSession.GetNodeDisplayName(node)}, " +
            $"Items={TransferAudit.InventoryCount}";

        session.Log(message);
        return true;
    }

    public static bool OwnsSession(RunFlowTestSession session)
    {
        return session != null &&
               ReferenceEquals(RetainedSession, session);
    }

    public static void AbortBattleHandoff(string reason)
    {
        if (RetainedSession != null &&
            !string.IsNullOrWhiteSpace(reason))
        {
            RetainedSession.Log("LIVE Battle handoff 취소: " + reason);
        }

        BattleInFlight = false;
        PendingResult = null;
        RetainedSession = null;
        PendingNode = default;
        TransferAudit = null;
    }

    public static void MarkTransferAudit(
        bool sameRunProgression,
        bool sameSkillUpgrades,
        int expectedCombatItems,
        int equippedCombatItems,
        int expectedTempBalanceMechanics,
        int tempBalanceMechanics,
        int expectedPartSnapshots,
        int appliedPartSnapshots,
        string details)
    {
        TransferAudit ??= new RunFlowLiveTransferAudit();
        TransferAudit.SameRunProgression = sameRunProgression;
        TransferAudit.SameSkillUpgrades = sameSkillUpgrades;
        TransferAudit.ExpectedCombatItems = expectedCombatItems;
        TransferAudit.EquippedCombatItems = equippedCombatItems;
        TransferAudit.ExpectedTempBalanceMechanics = expectedTempBalanceMechanics;
        TransferAudit.TempBalanceMechanics = tempBalanceMechanics;
        TransferAudit.ExpectedPartSnapshots = Mathf.Max(0, expectedPartSnapshots);
        TransferAudit.AppliedPartSnapshots = Mathf.Max(0, appliedPartSnapshots);
        TransferAudit.Details = details ?? string.Empty;
    }

    public static void RecordBattleResult(RunFlowLiveBattleResult result)
    {
        if (result == null)
            return;

        result.Node = PendingNode;
        result.Audit = TransferAudit;
        PendingResult = result;
        BattleInFlight = false;
    }

    public static bool TryTakeReturn(
        out RunFlowTestSession session,
        out RunFlowLiveBattleResult result)
    {
        session = null;
        result = null;

        if (RetainedSession == null ||
            PendingResult == null ||
            BattleInFlight)
        {
            return false;
        }

        session = RetainedSession;
        result = PendingResult;

        RetainedSession = null;
        PendingResult = null;
        PendingNode = default;
        TransferAudit = null;
        return true;
    }

    public static bool IsBattleNode(RunFlowTestNodeType node)
    {
        return node == RunFlowTestNodeType.NormalBattle ||
               node == RunFlowTestNodeType.EliteBattle ||
               node == RunFlowTestNodeType.TurnLimitBattle ||
               node == RunFlowTestNodeType.BossBattle;
    }

    private static void WriteBattleTestPreferences(
        string characterKey,
        RunFlowTestNodeType node,
        EmotionType emotion)
    {
        PlayerPrefs.SetInt(
            PlayerPreferenceKey,
            ResolveBattleTestPlayer(characterKey));

        PlayerPrefs.SetInt(
            EncounterPreferenceKey,
            ResolveBattleTestEncounter(node));

        PlayerPrefs.SetInt(
            EmotionPreferenceKey,
            (int)emotion);

        PlayerPrefs.Save();
    }

    private static int ResolveBattleTestPlayer(string characterKey)
    {
        if (string.Equals(characterKey, "Olaf", StringComparison.OrdinalIgnoreCase))
            return 0;

        if (string.Equals(characterKey, "Hifumi", StringComparison.OrdinalIgnoreCase))
            return 2;

        // BattleTestPlayerMode.Yujin = 1
        return 1;
    }

    private static int ResolveBattleTestEncounter(RunFlowTestNodeType node)
    {
        return node switch
        {
            // BattleTestEncounterMode.MixedBattle에 Elite가 포함되어 있으므로
            // 현 Battle Test Scene에서 사용할 수 있는 가장 가까운 실제 fixture로 연결한다.
            RunFlowTestNodeType.EliteBattle => 1,
            RunFlowTestNodeType.BossBattle => 2,
            _ => 0
        };
    }
}

[Serializable]
public sealed class RunFlowLiveTransferAudit
{
    public string CharacterKey;
    public RunFlowTestNodeType Node;
    public int InventoryCount;
    public bool SameRunProgression;
    public bool SameSkillUpgrades;
    public int ExpectedCombatItems;
    public int EquippedCombatItems;
    public int ExpectedTempBalanceMechanics;
    public int TempBalanceMechanics;
    public int ExpectedPartSnapshots;
    public int AppliedPartSnapshots;
    public string Details;

    public bool Passed =>
        SameRunProgression &&
        SameSkillUpgrades &&
        EquippedCombatItems == ExpectedCombatItems &&
        TempBalanceMechanics == ExpectedTempBalanceMechanics &&
        AppliedPartSnapshots == ExpectedPartSnapshots;
}

[Serializable]
public sealed class RunFlowLivePartSnapshot
{
    public PartType Type;
    public BodyPartState State;
    public float CurrentHp;
    public float MaximumHp;
}

[Serializable]
public sealed class RunFlowLiveBattleResult
{
    public RunFlowTestNodeType Node;
    public bool Victory;
    public int PlayerCurrentHp;
    public int PlayerMaximumHp;
    public string Message;
    public RunFlowLiveTransferAudit Audit;
    public List<RunFlowLivePartSnapshot> Parts = new();
}
