using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 기존 RunFlowTestHarness를 바꾸지 않고 실제 Phase E Item Catalog와
/// 실제 Battle Test Scene 왕복을 덧붙이는 개발자 통합 패널.
/// private session 접근은 테스트 harness에 한정된 adapter이며 전투 코어에는 reflection을 사용하지 않는다.
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class RunFlowLiveIntegrationOverlay : MonoBehaviour
{
    private static readonly FieldInfo HarnessSessionField =
        typeof(RunFlowTestHarness).GetField(
            "session",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo ItemOfferServiceField =
        typeof(RunFlowTestSession).GetField(
            "itemOfferService",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo AvatarCurrentHpField =
        typeof(RunFlowTestAvatarState).GetField(
            "<CurrentHp>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private RunFlowTestHarness harness;
    private RunFlowTestSession session;
    private RunFlowLiveContentRegistry registry;
    private Rect panelRect = new Rect(16f, 178f, 440f, 250f);
    private string lastStatus = "Live integration initializing...";
    private bool canonicalCatalogBound;

    private void Start()
    {
        registry = RunFlowLiveContentRegistry.Load();
        panelRect.x = Mathf.Max(16f, Screen.width - panelRect.width - 16f);
        panelRect.y = 16f;
        harness = FindFirstObjectByType<RunFlowTestHarness>();

        if (harness == null || HarnessSessionField == null)
        {
            lastStatus = "FAIL: RunFlowTestHarness/session field를 찾지 못했습니다.";
            Debug.LogError("[RunFlowLive] " + lastStatus, this);
            return;
        }

        RunFlowTestSession freshSession =
            GetHarnessSession();

        if (RunFlowLiveHandoff.TryTakeReturn(
                out RunFlowTestSession retained,
                out RunFlowLiveBattleResult result))
        {
            if (freshSession != null &&
                !ReferenceEquals(freshSession, retained))
            {
                freshSession.Dispose();
            }

            SetHarnessSession(retained);
            session = retained;
            BindCanonicalCatalog(session);
            ApplyBattleReturn(result);
        }
        else
        {
            session = freshSession;
            BindCanonicalCatalog(session);
        }

        LogRegistryState();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
            return;

        panelRect = GUILayout.Window(
            GetInstanceID(),
            panelRect,
            DrawWindow,
            "RUN FLOW ↔ LIVE BATTLE");
    }

    private void DrawWindow(int id)
    {
        if (session == null)
        {
            GUILayout.Label("Run session 없음");
            GUI.DragWindow();
            return;
        }

        int itemCount = registry?.CountValidRunItems() ?? 0;
        int linkedCount = registry?.CountCombatLinkedRunItems() ?? 0;

        GUILayout.Label(
            canonicalCatalogBound
                ? $"Phase E Catalog: {itemCount} items / Combat linked {linkedCount}"
                : "Phase E Catalog: NOT BOUND");

        GUILayout.Label(
            $"RunState: Stage {session.Progression.Stage} / " +
            $"Gold {session.Progression.Gold} / " +
            $"Inventory {session.Progression.Inventory.Owned.Count}");

        GUILayout.Label(
            $"Character={Display(session.SelectedCharacterKey)} / " +
            $"Emotion={(session.SelectedEmotion.HasValue ? session.SelectedEmotion.Value.ToString() : "-")}");

        GUILayout.Space(4f);
        GUILayout.Label(lastStatus);
        GUILayout.Space(6f);

        bool canEnter =
            session.ActiveNode.HasValue &&
            RunFlowLiveHandoff.IsBattleNode(session.ActiveNode.Value) &&
            !string.IsNullOrWhiteSpace(session.SelectedCharacterKey) &&
            session.SelectedEmotion.HasValue;

        GUI.enabled = canEnter;
        if (GUILayout.Button(
                canEnter
                    ? $"ENTER LIVE: {RunFlowTestSession.GetNodeDisplayName(session.ActiveNode.Value)}"
                    : "전투 노드에 들어가면 LIVE 진입 가능",
                GUILayout.Height(42f)))
        {
            TryEnterLiveBattle();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Rebind Phase E Catalog", GUILayout.Height(28f)))
        {
            registry = RunFlowLiveContentRegistry.Load();
            BindCanonicalCatalog(session);
            LogRegistryState();
        }

        GUILayout.Label(
            "LIVE 경로는 같은 RunProgressionState/SkillUpgradeState를 BattleContext에 전달하고, " +
            "Inventory의 RunItemDefinition.CombatItem을 실제 Player build에 주입합니다.");

        GUI.DragWindow();
    }

    private void TryEnterLiveBattle()
    {
        if (session == null || !session.ActiveNode.HasValue)
            return;

        RunFlowTestNodeType node = session.ActiveNode.Value;

        if (!RunFlowLiveHandoff.BeginBattle(
                session,
                node,
                out string message))
        {
            lastStatus = "FAIL: " + message;
            Debug.LogWarning("[RunFlowLive] " + lastStatus, this);
            return;
        }

        // Run Flow Scene unload 시 기존 Harness.OnDestroy가 session.Dispose()를 호출하지 않도록
        // 소유권을 static handoff로 넘기고 Harness의 private 참조만 비운다.
        SetHarnessSession(null);
        lastStatus = message;

        try
        {
            SceneManager.LoadScene(
                RunFlowLiveHandoff.BattleSceneName,
                LoadSceneMode.Single);
        }
        catch (Exception exception)
        {
            SetHarnessSession(session);
            RunFlowLiveHandoff.AbortBattleHandoff(exception.Message);
            lastStatus = "Scene load FAIL: " + exception.Message;
            Debug.LogException(exception, this);
        }
    }

    private void ApplyBattleReturn(RunFlowLiveBattleResult result)
    {
        if (session == null || result == null)
            return;

        ApplyAvatarResult(result);

        string audit = result.Audit == null
            ? "Audit 없음"
            : $"RunRef={result.Audit.SameRunProgression}, " +
              $"UpgradeRef={result.Audit.SameSkillUpgrades}, " +
              $"Items={result.Audit.EquippedCombatItems}/{result.Audit.ExpectedCombatItems}, " +
              $"TempMechanics={result.Audit.TempBalanceMechanics}/{result.Audit.ExpectedTempBalanceMechanics}, " +
              $"AuditPass={result.Audit.Passed}";

        session.Log(
            $"LIVE Battle 귀환 / Victory={result.Victory} / {audit}");

        if (!result.Victory)
        {
            session.Log(
                $"{RunFlowTestSession.GetNodeDisplayName(result.Node)} 패배: 보상 없음. " +
                "현재 테스트 shell 계약대로 Node Map으로 복귀합니다.");
            session.BackToNodeMap();
            lastStatus = "Returned: DEFEAT / " + audit;
            return;
        }

        switch (result.Node)
        {
            case RunFlowTestNodeType.NormalBattle:
            case RunFlowTestNodeType.TurnLimitBattle:
            {
                int gold = session.GrantBattleReward(result.Node);
                session.BackToNodeMap();
                lastStatus = $"Returned: VICTORY +{gold}G / {audit}";
                break;
            }

            case RunFlowTestNodeType.EliteBattle:
            {
                int gold = session.GrantBattleReward(result.Node);
                RunItemDefinition reward = session.GrantEliteMockItem();
                session.BackToNodeMap();
                lastStatus =
                    $"Returned: ELITE VICTORY +{gold}G + " +
                    $"{(reward != null ? reward.DisplayName : "item 없음")} / {audit}";
                break;
            }

            case RunFlowTestNodeType.BossBattle:
            {
                if (session.Progression.Stage < 3)
                {
                    session.PrepareBossClassOffer();
                    lastStatus =
                        $"Returned: BOSS VICTORY / 실제 Class Item 3택 생성 / {audit}";
                }
                else
                {
                    session.CompleteStageThreeRun();
                    lastStatus =
                        $"Returned: FINAL BOSS VICTORY / Run complete / {audit}";
                }
                break;
            }
        }

        Debug.Log("[RunFlowLive] " + lastStatus, this);
    }

    private void ApplyAvatarResult(RunFlowLiveBattleResult result)
    {
        RunFlowTestAvatarState avatar = session?.Avatar;
        if (avatar == null)
            return;

        if (AvatarCurrentHpField != null)
        {
            int hp = Mathf.Clamp(
                result.PlayerCurrentHp,
                1,
                Mathf.Max(1, avatar.MaximumHp));
            AvatarCurrentHpField.SetValue(avatar, hp);
        }

        if (result.Parts == null)
            return;

        for (int i = 0; i < result.Parts.Count; i++)
        {
            RunFlowLivePartSnapshot snapshot = result.Parts[i];
            if (snapshot == null)
                continue;

            int index = PartIndex(snapshot.Type);
            if (index < 0 || index >= avatar.Parts.Count)
                continue;

            avatar.SetPartState(
                index,
                snapshot.State switch
                {
                    BodyPartState.Broken => RunFlowTestPartState.Broken,
                    BodyPartState.Weakened => RunFlowTestPartState.Weakened,
                    _ => RunFlowTestPartState.Normal
                });
        }
    }

    private void BindCanonicalCatalog(RunFlowTestSession target)
    {
        canonicalCatalogBound = false;

        if (target == null ||
            registry?.RunItemCatalog == null ||
            ItemOfferServiceField == null)
        {
            return;
        }

        RunItemOfferService service =
            new RunItemOfferService(
                registry.RunItemCatalog,
                target.ItemEconomy);

        ItemOfferServiceField.SetValue(
            target,
            service);

        canonicalCatalogBound = true;
        target.Log(
            $"LIVE Integration: Phase E RunItemCatalog 연결 / " +
            $"Definitions={registry.CountValidRunItems()}, " +
            $"CombatLinked={registry.CountCombatLinkedRunItems()}");
    }

    private void LogRegistryState()
    {
        if (registry == null)
        {
            lastStatus = "Registry 없음 - Installer를 다시 실행하세요.";
            return;
        }

        int total = registry.CountValidRunItems();
        int linked = registry.CountCombatLinkedRunItems();
        lastStatus =
            $"Ready / Phase E Item {linked}/{total} combat-linked / " +
            $"Battle scene handoff available";
    }

    private RunFlowTestSession GetHarnessSession()
    {
        return harness == null || HarnessSessionField == null
            ? null
            : HarnessSessionField.GetValue(harness) as RunFlowTestSession;
    }

    private void SetHarnessSession(RunFlowTestSession value)
    {
        if (harness != null && HarnessSessionField != null)
            HarnessSessionField.SetValue(harness, value);
    }

    private static int PartIndex(PartType type)
    {
        return type switch
        {
            PartType.HEAD => 0,
            PartType.LEFT_HAND => 1,
            PartType.RIGHT_HAND => 2,
            PartType.LEGS => 3,
            _ => -1
        };
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value;
    }
}
