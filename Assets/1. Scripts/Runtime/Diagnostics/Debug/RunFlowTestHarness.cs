using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 외부 Run Flow를 검증하기 위한 개발자 전용 IMGUI Scene Harness.
/// 랜덤 노드 맵 생성이 목적이 아니라 모든 노드 타입의 진입/처리 계약을 손으로 반복 검증하는 것이 목적이다.
/// </summary>
public sealed class RunFlowTestHarness : MonoBehaviour
{
    private RunFlowTestSession session;
    private Vector2 mainScroll;
    private Vector2 logScroll;
    private Vector2 inventoryScroll;
    private Vector2 selfTestScroll;

    private readonly List<string> selfTestResults = new();
    private string lastAction = "Ready";
    private int maintenancePartIndex;

    private GUIStyle titleStyle;
    private GUIStyle headerStyle;
    private GUIStyle bodyStyle;
    private GUIStyle smallStyle;
    private GUIStyle warningStyle;
    private GUIStyle passStyle;
    private GUIStyle boxStyle;
    private bool stylesReady;

    private void Awake()
    {
        session = new RunFlowTestSession();
    }

    private void OnDestroy()
    {
        session?.Dispose();
        session = null;
    }

    private void OnGUI()
    {
        if (session == null)
            return;

        EnsureStyles();

        float width = Mathf.Max(960f, Screen.width - 24f);
        float height = Mathf.Max(640f, Screen.height - 24f);
        Rect area = new Rect(12f, 12f, width, height);

        GUILayout.BeginArea(area, GUIContent.none, boxStyle);
        DrawTopBar();
        GUILayout.Space(8f);

        mainScroll = GUILayout.BeginScrollView(mainScroll);
        switch (session.Phase)
        {
            case RunFlowTestPhase.CharacterSelect:
                DrawCharacterSelection();
                break;
            case RunFlowTestPhase.EmotionSelect:
                DrawEmotionSelection();
                break;
            case RunFlowTestPhase.NodeMap:
                DrawNodeMap();
                break;
            case RunFlowTestPhase.NodeDetail:
                DrawNodeDetail();
                break;
            case RunFlowTestPhase.RunComplete:
                DrawRunComplete();
                break;
        }
        GUILayout.EndScrollView();

        GUILayout.Space(8f);
        DrawLogPanel();
        GUILayout.EndArea();
    }

    private void DrawTopBar()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("PROJECT ABYSS · RUN FLOW INTEGRATION TEST", titleStyle, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Reset Run", GUILayout.Width(110f), GUILayout.Height(28f)))
        {
            session.ResetAll();
            selfTestResults.Clear();
            lastAction = "Run reset";
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Character: {DisplayOrDash(session.SelectedCharacterKey)}", bodyStyle, GUILayout.Width(190f));
        GUILayout.Label($"Emotion: {(session.SelectedEmotion.HasValue ? RunFlowTestSession.GetEmotionDisplayName(session.SelectedEmotion.Value) : "-")}", bodyStyle, GUILayout.Width(170f));
        GUILayout.Label($"Stage: {session.Progression.Stage}", bodyStyle, GUILayout.Width(100f));
        GUILayout.Label($"Gold: {session.Progression.Gold}", bodyStyle, GUILayout.Width(120f));
        GUILayout.Label($"HP: {session.Avatar.CurrentHp}/{session.Avatar.MaximumHp}", bodyStyle, GUILayout.Width(170f));
        GUILayout.Label($"Items: {session.Progression.Inventory.Owned.Count}", bodyStyle, GUILayout.Width(110f));
        GUILayout.Label($"Mock Skills: {session.MockSkillCount}", bodyStyle, GUILayout.Width(130f));
        GUILayout.EndHorizontal();

        GUILayout.Label($"Last Action: {lastAction}", smallStyle);
    }

    private void DrawCharacterSelection()
    {
        GUILayout.Label("1. 캐릭터 선택", headerStyle);
        GUILayout.Label("테스트 Run Context의 Character Key를 고릅니다. 실제 Character Prefab 로딩은 Phase D/E와 별개입니다.", bodyStyle);
        GUILayout.Space(12f);

        GUILayout.BeginHorizontal();
        DrawLargeSelectButton("OLAF", "올라프", () => session.SelectCharacter("Olaf"));
        DrawLargeSelectButton("YUJIN", "유진", () => session.SelectCharacter("Yujin"));
        DrawLargeSelectButton("HIFUMI", "히후미", () => session.SelectCharacter("Hifumi"));
        GUILayout.EndHorizontal();
    }

    private void DrawEmotionSelection()
    {
        GUILayout.Label("2. 시작 감정 선택", headerStyle);
        GUILayout.Label("0916의 7감정 중 하나를 Run 시작 감정으로 고릅니다. 전투 중 Tier 증강 선택은 기존 EmotionAugment 시스템이 담당합니다.", bodyStyle);
        GUILayout.Space(10f);

        EmotionType[] emotions = (EmotionType[])Enum.GetValues(typeof(EmotionType));
        for (int i = 0; i < emotions.Length; i += 4)
        {
            GUILayout.BeginHorizontal();
            for (int j = 0; j < 4 && i + j < emotions.Length; j++)
            {
                EmotionType emotion = emotions[i + j];
                if (GUILayout.Button(
                        $"{RunFlowTestSession.GetEmotionDisplayName(emotion)}\n({emotion})",
                        GUILayout.Height(70f),
                        GUILayout.MinWidth(170f)))
                {
                    session.SelectEmotion(emotion);
                    lastAction = $"Emotion = {RunFlowTestSession.GetEmotionDisplayName(emotion)}";
                }
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawNodeMap()
    {
        GUILayout.Label("3. Node Integration Map — 모든 타입 직접 진입", headerStyle);
        GUILayout.Label(
            "랜덤 맵 생성 검증이 아니라 노드 처리 계약 검증용입니다. 0916 정본이 우선이며, 구 XLSM 전용 요소는 Legacy/Test로 표시합니다.",
            bodyStyle);

        GUILayout.Space(8f);
        DrawStageControls();
        GUILayout.Space(8f);

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(560f, Screen.width * 0.61f)));
        DrawNodeGroup("BATTLE", new[]
        {
            RunFlowTestNodeType.NormalBattle,
            RunFlowTestNodeType.EliteBattle,
            RunFlowTestNodeType.TurnLimitBattle,
            RunFlowTestNodeType.BossBattle
        });
        DrawNodeGroup("ECONOMY / RECOVERY", new[]
        {
            RunFlowTestNodeType.GoldNode,
            RunFlowTestNodeType.GoldShop,
            RunFlowTestNodeType.HealthShop,
            RunFlowTestNodeType.Maintenance
        });
        DrawNodeGroup("CONTENT / ROUTER", new[]
        {
            RunFlowTestNodeType.Encounter,
            RunFlowTestNodeType.MysteryLegacyRouter
        });
        GUILayout.EndVertical();

        GUILayout.Space(10f);
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        DrawRunStateDebug();
        DrawInventory();
        DrawSelfTest();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private void DrawStageControls()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Stage Test Controls", headerStyle);
        GUILayout.BeginHorizontal();
        for (int stage = 1; stage <= 3; stage++)
        {
            int captured = stage;
            string label = session.Progression.Stage == stage ? $"[Stage {stage}]" : $"Stage {stage}";
            if (GUILayout.Button(label, GUILayout.Height(32f)))
            {
                session.SetStage(captured);
                lastAction = $"Stage -> {captured}";
            }
        }
        GUILayout.Label($"n = {session.Economy.GetStageBaseGold(session.Progression.Stage)}", bodyStyle, GUILayout.Width(120f));
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawNodeGroup(string label, RunFlowTestNodeType[] nodes)
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label(label, headerStyle);
        for (int i = 0; i < nodes.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            for (int j = 0; j < 2 && i + j < nodes.Length; j++)
            {
                RunFlowTestNodeType node = nodes[i + j];
                if (GUILayout.Button(GetNodeButtonLabel(node), GUILayout.Height(64f), GUILayout.MinWidth(260f)))
                {
                    session.EnterNode(node);
                    lastAction = $"Enter {RunFlowTestSession.GetNodeDisplayName(node)}";
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    private void DrawNodeDetail()
    {
        if (!session.ActiveNode.HasValue)
        {
            session.BackToNodeMap();
            return;
        }

        RunFlowTestNodeType node = session.ActiveNode.Value;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("← Node Map", GUILayout.Width(140f), GUILayout.Height(34f)))
        {
            session.BackToNodeMap();
            return;
        }
        GUILayout.Label(RunFlowTestSession.GetNodeDisplayName(node), titleStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(600f, Screen.width * 0.63f)));
        DrawNodeCanonicalNote(node);
        GUILayout.Space(8f);

        switch (node)
        {
            case RunFlowTestNodeType.NormalBattle:
                DrawBattleNode(node, false, false);
                break;
            case RunFlowTestNodeType.EliteBattle:
                DrawBattleNode(node, true, false);
                break;
            case RunFlowTestNodeType.TurnLimitBattle:
                DrawBattleNode(node, false, true);
                break;
            case RunFlowTestNodeType.GoldNode:
                DrawGoldNode();
                break;
            case RunFlowTestNodeType.Encounter:
                DrawEncounterNode();
                break;
            case RunFlowTestNodeType.MysteryLegacyRouter:
                DrawMysteryNode();
                break;
            case RunFlowTestNodeType.GoldShop:
                DrawGoldShopNode();
                break;
            case RunFlowTestNodeType.HealthShop:
                DrawHealthShopNode();
                break;
            case RunFlowTestNodeType.Maintenance:
                DrawMaintenanceNode();
                break;
            case RunFlowTestNodeType.BossBattle:
                DrawBossNode();
                break;
        }

        GUILayout.EndVertical();
        GUILayout.Space(10f);
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        DrawRunStateDebug();
        DrawInventory();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private void DrawBattleNode(RunFlowTestNodeType node, bool elite, bool turnLimit)
    {
        int reward = PreviewBattleReward(node);
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Battle Handoff Test", headerStyle);
        GUILayout.Label(
            "이 Scene은 실제 전투 Scene을 로드하지 않습니다. 현재 목적은 '노드 진입 → 전투 결과 반환 → Run State 반영' 계약을 검증하는 것입니다.",
            bodyStyle);
        GUILayout.Label($"현재 보상 Preview: {reward}G (variance {session.RewardVariance:0.0})", bodyStyle);

        DrawVarianceControl();

        if (turnLimit)
        {
            GUILayout.Label(
                "턴 제한 수치 자체는 0916 정본에서 확정값이 없습니다. 구 XLSM의 5턴은 UI 설명용 Legacy 테스트값으로만 취급합니다.",
                warningStyle);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Simulate Victory", GUILayout.Height(44f)))
        {
            int gained = session.GrantBattleReward(node);
            if (elite)
                session.GrantEliteMockItem();
            session.BackToNodeMap();
            lastAction = $"Victory: +{gained}G" + (elite ? " + Mock Item" : string.Empty);
        }

        if (turnLimit && GUILayout.Button("Timeout / Retreat", GUILayout.Height(44f)))
        {
            session.Log("턴제한 전투 실패/후퇴: 보상 없음");
            session.BackToNodeMap();
            lastAction = "Turn-limit timeout: no reward";
        }
        else if (!turnLimit && GUILayout.Button("Simulate Defeat (No Reward)", GUILayout.Height(44f)))
        {
            session.Log($"{RunFlowTestSession.GetNodeDisplayName(node)} 패배 처리: 테스트 shell에서는 보상 없이 Node Map 복귀");
            session.BackToNodeMap();
            lastAction = "Defeat: no reward";
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawGoldNode()
    {
        int preview = session.Economy.CalculateNodeGold(
            session.Progression.Stage,
            RunNodeRewardKind.GoldNode,
            session.RewardVariance);

        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label($"Gold Reward Preview: {preview}G", headerStyle);
        DrawVarianceControl();
        if (GUILayout.Button("Resolve Gold Node", GUILayout.Height(46f)))
        {
            int gained = session.ResolveGoldNode();
            lastAction = $"Gold node +{gained}G";
        }
        GUILayout.EndVertical();
    }

    private void DrawEncounterNode()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Encounter Content Scaffold", headerStyle);
        GUILayout.Label(
            "0916은 스킬 획득 경로에 '조우'를 명시하지만 조우 이벤트의 세부 선택지/보상표는 현재 정본에 없습니다. 따라서 여기서는 값을 발명하지 않고 처리 경로만 테스트합니다.",
            warningStyle);

        if (GUILayout.Button("Resolve: Mock Skill Acquired", GUILayout.Height(42f)))
        {
            session.ResolveEncounterMockSkill();
            lastAction = "Encounter -> mock skill token";
        }
        if (GUILayout.Button("Resolve: No Reward / Event Closed", GUILayout.Height(42f)))
        {
            session.ResolveEncounterNoReward();
            lastAction = "Encounter -> no reward";
        }
        GUILayout.EndVertical();
    }

    private void DrawMysteryNode()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Legacy XLSM Router", headerStyle);
        GUILayout.Label(
            "구 XLSM의 '물음표 → 조우 또는 골드' 구조를 노드 라우팅 테스트용으로만 남겼습니다. 0916 정본의 확정 노드 규칙으로 취급하지 않습니다.",
            warningStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Route → Gold Node", GUILayout.Height(46f)))
        {
            session.RouteMysteryTo(RunFlowTestNodeType.GoldNode);
            lastAction = "Mystery -> Gold";
        }
        if (GUILayout.Button("Route → Encounter", GUILayout.Height(46f)))
        {
            session.RouteMysteryTo(RunFlowTestNodeType.Encounter);
            lastAction = "Mystery -> Encounter";
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawGoldShopNode()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Gold Shop — transient mock items", headerStyle);
        GUILayout.Label(
            "가격/판매율/5~6칸/리롤 x1.5는 Phase C 런 경제를 그대로 사용합니다. 아이템 효과는 Phase E 전이므로 Mock입니다.",
            bodyStyle);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Reroll base cost: {session.TestRerollBaseCost}G", bodyStyle, GUILayout.Width(220f));
        if (GUILayout.Button("-25", GUILayout.Width(60f))) session.TestRerollBaseCost = Mathf.Max(0, session.TestRerollBaseCost - 25);
        if (GUILayout.Button("+25", GUILayout.Width(60f))) session.TestRerollBaseCost += 25;
        GUILayout.Label("(TEST INPUT: 정본에 base reroll 비용 없음)", warningStyle);
        GUILayout.EndHorizontal();

        GUILayout.Label($"Next Reroll Cost: {session.GetNextRerollCost()}G / Count {session.GoldShopRerollCount}", bodyStyle);
        if (GUILayout.Button("Pay & Reroll", GUILayout.Height(36f)))
        {
            if (session.TryRerollGoldShop(out string message))
                lastAction = message;
            else
                lastAction = message;
        }

        GUILayout.Space(8f);
        IReadOnlyList<RunItemDefinition> offer = session.CurrentShopOffer;
        for (int i = 0; i < offer.Count; i++)
        {
            RunItemDefinition item = offer[i];
            if (item == null) continue;
            int price = session.ItemEconomy.GetPrice(item.Tier);
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"[{item.Tier}] {item.DisplayName}", bodyStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label($"{price}G", bodyStyle, GUILayout.Width(80f));
            int index = i;
            if (GUILayout.Button("Buy", GUILayout.Width(90f)))
            {
                session.TryBuyShopItem(index, out string message);
                lastAction = message;
                break;
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    private void DrawHealthShopNode()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Health Shop", headerStyle);
        GUILayout.Label(
            "확정: MaxHP +1 비용 = CurrentHP 3.  미정: CurrentHP 1 → Gold 2.5는 기본적으로 비활성(Unset).",
            bodyStyle);

        GUILayout.Label($"Current HP {session.Avatar.CurrentHp}/{session.Avatar.MaximumHp}", bodyStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Buy MaxHP +5 (HP -15)", GUILayout.Height(40f)))
        {
            session.Avatar.TryBuyMaximumHp(session.Progression, session.Economy, 5, out string message);
            lastAction = message;
            session.Log(message);
        }
        if (GUILayout.Button("Buy MaxHP +10 (HP -30)", GUILayout.Height(40f)))
        {
            session.Avatar.TryBuyMaximumHp(session.Progression, session.Economy, 10, out string message);
            lastAction = message;
            session.Log(message);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        bool testRateOn = session.EconomySettings.HasCurrentHpToGoldRate;
        if (GUILayout.Button(
                testRateOn
                    ? "Disable TEST CurrentHP→Gold Rate"
                    : "Enable TEST CurrentHP→Gold Rate = 2.5 (UNSET)",
                GUILayout.Height(38f)))
        {
            session.EnableTestOnlyCurrentHpGoldRate(!testRateOn);
            lastAction = !testRateOn ? "TEST rate enabled" : "TEST rate disabled";
        }

        GUI.enabled = session.EconomySettings.HasCurrentHpToGoldRate;
        if (GUILayout.Button("Exchange CurrentHP 20 → Gold", GUILayout.Height(38f)))
        {
            session.Avatar.TryExchangeCurrentHpForGoldTestOnly(session.Progression, session.Economy, 20, out string message);
            lastAction = message;
            session.Log(message);
        }
        GUI.enabled = true;
        GUILayout.EndVertical();
    }

    private void DrawMaintenanceNode()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Maintenance — 75G package", headerStyle);
        GUILayout.Label("부위 상태 1곳 회복 + 최대HP 25% 회복. 스킬 강화는 실제 SkillDefinition 데이터가 Phase E에서 채워진 뒤 같은 정비 화면에 연결합니다.", bodyStyle);

        GUILayout.Label($"Cost: {session.EconomySettings.MaintenanceCost}G / Heal: {session.EconomySettings.MaintenanceHealMaxHpRatio:P0}", bodyStyle);
        for (int i = 0; i < session.Avatar.Parts.Count; i++)
        {
            RunFlowTestPart part = session.Avatar.Parts[i];
            bool selected = maintenancePartIndex == i;
            if (GUILayout.Button($"{(selected ? "▶ " : string.Empty)}{part.Name}: {part.State}", GUILayout.Height(32f)))
                maintenancePartIndex = i;
        }

        if (GUILayout.Button("Pay 75G & Restore Selected Part", GUILayout.Height(46f)))
        {
            session.Avatar.TryMaintenanceRestore(
                session.Progression,
                session.EconomySettings,
                maintenancePartIndex,
                out string message);
            lastAction = message;
            session.Log(message);
        }

        GUILayout.Space(10f);
        GUILayout.Label("Skill Upgrade Test (transient definitions)", headerStyle);
        IReadOnlyList<SkillDefinition> skills = session.MockUpgradeSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillDefinition skill = skills[i];
            int level = session.Progression.SkillUpgrades.GetLevel(skill);
            int target = level + 1;
            bool hasQuote = SkillUpgradeService.TryGetUpgradeCost(skill, target, out int cost);

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"{skill.SkillName} [{skill.ActionType}] Lv{level}/2", bodyStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label(hasQuote ? $"Next {cost}G" : "Next: UNSET", hasQuote ? bodyStyle : warningStyle, GUILayout.Width(120f));
            GUI.enabled = hasQuote && level < 2;
            int index = i;
            if (GUILayout.Button("Upgrade", GUILayout.Width(90f)))
            {
                session.TryUpgradeMockSkill(index, out string message);
                lastAction = message;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    private void DrawBossNode()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label($"Boss Node — Stage {session.Progression.Stage}", headerStyle);
        GUILayout.Label(
            "Stage 1·2 보스 클리어: 직업 아이템 3택1 + 스테이지 클리어 전회복. Stage 3 최종 구조는 구 맵 XLSM을 테스트 shell의 흐름 종료 기준으로만 사용합니다.",
            bodyStyle);

        if (session.Progression.Stage < 3)
        {
            if (session.CurrentBossOffer.Count == 0)
            {
                if (GUILayout.Button("Simulate Boss Victory → Build Class Item 3-Choice", GUILayout.Height(48f)))
                {
                    session.PrepareBossClassOffer();
                    lastAction = "Boss victory -> class item offer";
                }
            }
            else
            {
                GUILayout.Label("Choose 1 Class Mock Item", headerStyle);
                for (int i = 0; i < session.CurrentBossOffer.Count; i++)
                {
                    RunItemDefinition item = session.CurrentBossOffer[i];
                    int index = i;
                    if (GUILayout.Button(item.DisplayName, GUILayout.Height(42f)))
                    {
                        session.ChooseBossClassItemAndCompleteStage(index, out string message);
                        lastAction = message;
                        break;
                    }
                }
            }
        }
        else
        {
            if (GUILayout.Button("Simulate Final Boss Victory → Finish Test Run", GUILayout.Height(52f)))
            {
                session.CompleteStageThreeRun();
                lastAction = "Run complete";
            }
        }
        GUILayout.EndVertical();
    }

    private void DrawRunStateDebug()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Run Debug State", headerStyle);
        GUILayout.Label($"Gold: {session.Progression.Gold}", bodyStyle);
        GUILayout.Label($"HP: {session.Avatar.CurrentHp}/{session.Avatar.MaximumHp}", bodyStyle);
        GUILayout.Label($"MaxHP Bonus: +{session.Progression.MaximumHpBonus}", bodyStyle);
        GUILayout.Label($"Elite Mock Rewards: {session.MockEliteBonusRewardCount}", bodyStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+500G"))
        {
            session.Progression.AddGold(500);
            session.Log("DEBUG +500G");
            lastAction = "+500G";
        }
        if (GUILayout.Button("Damage 100"))
        {
            session.Avatar.Damage(100);
            session.Log("DEBUG HP -100");
            lastAction = "HP -100";
        }
        if (GUILayout.Button("Full Heal"))
        {
            session.Avatar.FullHeal();
            session.Log("DEBUG Full Heal");
            lastAction = "Full Heal";
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("Body Parts", smallStyle);
        for (int i = 0; i < session.Avatar.Parts.Count; i++)
        {
            RunFlowTestPart part = session.Avatar.Parts[i];
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{part.Name}: {part.State}", smallStyle, GUILayout.Width(150f));
            int index = i;
            if (GUILayout.Button("Normal", GUILayout.Width(70f))) session.Avatar.SetPartState(index, RunFlowTestPartState.Normal);
            if (GUILayout.Button("Weak", GUILayout.Width(70f))) session.Avatar.SetPartState(index, RunFlowTestPartState.Weakened);
            if (GUILayout.Button("Broken", GUILayout.Width(70f))) session.Avatar.SetPartState(index, RunFlowTestPartState.Broken);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    private void DrawInventory()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Inventory (Mock Content)", headerStyle);
        inventoryScroll = GUILayout.BeginScrollView(inventoryScroll, GUILayout.Height(150f));
        IReadOnlyList<RunItemDefinition> owned = session.Progression.Inventory.Owned;
        if (owned.Count == 0)
        {
            GUILayout.Label("(empty)", smallStyle);
        }
        else
        {
            for (int i = 0; i < owned.Count; i++)
            {
                RunItemDefinition item = owned[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{i + 1}. [{item.Tier}] {item.DisplayName}", smallStyle, GUILayout.ExpandWidth(true));
                int index = i;
                if (GUILayout.Button($"Sell +{session.ItemEconomy.GetSellPrice(item)}G", GUILayout.Width(110f)))
                {
                    session.TrySellOwnedItem(index, out string message);
                    lastAction = message;
                    break;
                }
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawSelfTest()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Run Shell Self Test", headerStyle);
        if (GUILayout.Button("Run 15 Contract Checks", GUILayout.Height(34f)))
        {
            selfTestResults.Clear();
            selfTestResults.AddRange(session.RunSelfTest());
            int fail = 0;
            for (int i = 0; i < selfTestResults.Count; i++)
            {
                if (selfTestResults[i].StartsWith("FAIL", StringComparison.Ordinal))
                    fail++;
            }
            lastAction = $"Self Test: {selfTestResults.Count - fail} PASS / {fail} FAIL";
            session.Log(lastAction);
        }

        selfTestScroll = GUILayout.BeginScrollView(selfTestScroll, GUILayout.Height(180f));
        for (int i = 0; i < selfTestResults.Count; i++)
        {
            string row = selfTestResults[i];
            GUILayout.Label(row, row.StartsWith("PASS", StringComparison.Ordinal) ? passStyle : warningStyle);
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawRunComplete()
    {
        GUILayout.Label("TEST RUN COMPLETE", titleStyle);
        GUILayout.Label("Stage 3 종료까지 Run shell 흐름을 밟았습니다. 실제 맵 생성/이벤트 콘텐츠/최종 UI는 이 테스트 Scene의 책임이 아닙니다.", bodyStyle);
        GUILayout.Space(12f);
        DrawInventory();
        GUILayout.Space(8f);
        if (GUILayout.Button("Start New Test Run", GUILayout.Height(50f)))
        {
            session.ResetAll();
            selfTestResults.Clear();
            lastAction = "New run";
        }
    }

    private void DrawLogPanel()
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(155f));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Event Log", headerStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label("Scene-only developer UI", smallStyle);
        GUILayout.EndHorizontal();

        logScroll = GUILayout.BeginScrollView(logScroll, GUILayout.Height(110f));
        IReadOnlyList<string> history = session.History;
        for (int i = history.Count - 1; i >= 0; i--)
            GUILayout.Label(history[i], smallStyle);
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawVarianceControl()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Reward variance", bodyStyle, GUILayout.Width(150f));
        if (GUILayout.Button("0.9", GUILayout.Width(55f))) session.RewardVariance = 0.9f;
        if (GUILayout.Button("1.0", GUILayout.Width(55f))) session.RewardVariance = 1f;
        if (GUILayout.Button("1.1", GUILayout.Width(55f))) session.RewardVariance = 1.1f;
        GUILayout.Label($"Current {session.RewardVariance:0.0}", bodyStyle);
        GUILayout.EndHorizontal();
    }

    private int PreviewBattleReward(RunFlowTestNodeType node)
    {
        RunNodeRewardKind kind = node switch
        {
            RunFlowTestNodeType.EliteBattle => RunNodeRewardKind.EliteBattle,
            RunFlowTestNodeType.TurnLimitBattle => RunNodeRewardKind.TurnLimitBattle,
            _ => RunNodeRewardKind.NormalBattle
        };
        return session.Economy.CalculateNodeGold(session.Progression.Stage, kind, session.RewardVariance);
    }

    private void DrawNodeCanonicalNote(RunFlowTestNodeType node)
    {
        string text = node switch
        {
            RunFlowTestNodeType.NormalBattle => "0916 CURRENT: 골드 n. 일반전투는 3마리/4마리 조우 구성의 실제 전투 데이터가 Phase E에서 연결됩니다.",
            RunFlowTestNodeType.EliteBattle => "0916 CURRENT: 골드 1.5n×0.9~1.1 + 무작위 아이템 1개.",
            RunFlowTestNodeType.TurnLimitBattle => "0916 CURRENT: 골드 1.5n×0.9~1.1 + 각 노드 보상. 제한 턴 숫자는 현 정본 확정값 아님.",
            RunFlowTestNodeType.GoldNode => "0916 CURRENT: 골드 2.5n×0.9~1.1.",
            RunFlowTestNodeType.Encounter => "CURRENT SCAFFOLD: 조우는 스킬 획득 경로로 존재하지만 세부 이벤트/보상표가 현재 MD에 없음.",
            RunFlowTestNodeType.MysteryLegacyRouter => "LEGACY ONLY: 구 XLSM의 물음표 분기. 현재 정본 규칙으로 하드코딩하지 않음.",
            RunFlowTestNodeType.GoldShop => "0916 CURRENT: 매대 5~6칸, 전 티어, 골드 구매, 판매 33%, 리롤마다 비용 x1.5.",
            RunFlowTestNodeType.HealthShop => "0916 CURRENT: 별도 체력 상점. MaxHP +1 = CurrentHP 3. CurrentHP→Gold 환율은 (미정).",
            RunFlowTestNodeType.Maintenance => "0916 CURRENT: 75G = 부위 1곳 상태 회복 + 최대HP 25% 회복 + 스킬 강화 진입점.",
            RunFlowTestNodeType.BossBattle => "0916 CURRENT: Stage 1·2 보스는 직업 아이템 3택1, 스테이지 클리어 시 HP 전회복. Stage 3 종료 흐름은 구 맵 기획을 테스트 shell에만 사용.",
            _ => string.Empty
        };

        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Contract", headerStyle);
        GUILayout.Label(text, node == RunFlowTestNodeType.MysteryLegacyRouter ? warningStyle : bodyStyle);
        GUILayout.EndVertical();
    }

    private string GetNodeButtonLabel(RunFlowTestNodeType node)
    {
        string suffix = node switch
        {
            RunFlowTestNodeType.MysteryLegacyRouter => "\n[LEGACY TEST]",
            RunFlowTestNodeType.Encounter => "\n[SCAFFOLD]",
            RunFlowTestNodeType.HealthShop => "\n[UNSET RATE SAFE]",
            _ => string.Empty
        };
        return RunFlowTestSession.GetNodeDisplayName(node) + suffix;
    }

    private void DrawLargeSelectButton(string key, string korean, Action select)
    {
        if (GUILayout.Button($"{korean}\n{key}", GUILayout.Height(100f), GUILayout.MinWidth(240f)))
        {
            select?.Invoke();
            lastAction = $"Character = {key}";
        }
    }

    private static string DisplayOrDash(string value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true
        };
        smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            wordWrap = true
        };
        warningStyle = new GUIStyle(bodyStyle)
        {
            fontStyle = FontStyle.Italic
        };
        passStyle = new GUIStyle(smallStyle)
        {
            fontStyle = FontStyle.Bold
        };
        boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 10, 10)
        };
        stylesReady = true;
    }
}
