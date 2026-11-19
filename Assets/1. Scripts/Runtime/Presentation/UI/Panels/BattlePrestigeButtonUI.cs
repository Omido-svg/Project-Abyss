using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 11-19 임시 위세 발동 버튼 — Prefab 의존 없이 Battle UI Canvas에 런타임으로 붙는다.
/// 위세는 슬롯을 쓰지 않으므로(§9) 슬롯 스킬 목록에 나오지 않는다. 이 버튼이
/// BattleUIManager.TryExecutePrestigePlanning(슬롯리스 계획 명령)을 부른다.
/// 선택지가 있는 위세(히후미 속임수 — 시고로 / 대실패)는 BattlePlanningChoiceOverlay로 고른다.
///
/// 임시 UI — 모양 · 위치 · 연출은 코더 작업으로 대체한다.
/// 조준이 필요한 위세(빌야르 룬 새기기 등)의 대상 선택 UI는 아직 없다 —
/// 지금은 살아 있는 첫 적의 첫 생존 부위를 대상으로 넘긴다.
/// </summary>
public sealed class BattlePrestigeButtonUI : MonoBehaviour
{
    private BattleUIManager ui;
    private BattleManager battle;
    private BattlePlanningChoiceOverlay overlay;

    private GameObject root;
    private Button button;
    private Image buttonImage;
    private Text label;
    private string lastMessage = string.Empty;
    private float messageUntil;

    public void Bind(BattleUIManager owner)
    {
        ui = owner;
    }

    private void Update()
    {
        if (ui == null)
            return;

        battle ??= FindFirstObjectByType<BattleManager>();

        if (root == null && !TryBuild())
            return;

        Refresh();
    }

    private void Refresh()
    {
        Character player = battle != null && battle.IsInitialized
            ? battle.BattleContext?.Player
            : null;

        Skill skill = FirstPrestige();
        bool planning =
            player != null &&
            battle.TurnManager != null &&
            battle.TurnManager.IsBattleRunning &&
            !battle.TurnManager.IsResolving;

        bool usable =
            planning &&
            skill != null &&
            skill.CanUseByResource(player) &&
            !(skill.PrestigeUsePolicy == PrestigeUsePolicy.OncePerTurn && skill.UseCountThisTurn > 0);

        button.interactable = usable && (overlay == null || !overlay.IsOpen);
        buttonImage.color = usable
            ? new Color(0.62f, 0.40f, 0.08f, 0.95f)
            : new Color(0.18f, 0.18f, 0.20f, 0.85f);

        int gauge = player?.RuntimeStatus?.currentPrestige ?? 0;
        string text = skill == null
            ? "위세 없음"
            : $"위세 · {skill.SkillName}\n게이지 {gauge}" + (usable ? " · 발동 가능" : string.Empty);

        if (Time.unscaledTime < messageUntil && !string.IsNullOrEmpty(lastMessage))
            text = lastMessage;

        label.text = text;
    }

    private Skill FirstPrestige()
    {
        IReadOnlyList<Skill> skills = ui.GetSelectablePrestigeSkillsForPlanning();
        return skills != null && skills.Count > 0 ? skills[0] : null;
    }

    private void OnClick()
    {
        Skill skill = FirstPrestige();
        Character player = battle?.BattleContext?.Player;
        if (skill == null || player == null)
            return;

        var context = new ActionPlanningSkillContext(
            player,
            null,
            skill,
            -1,
            battle.ActionManager?.Slots);

        IReadOnlyList<ActionPlanningChoiceOption> choices =
            ActionPlanningMechanicPolicy.GetPlanningChoices(context);

        if (choices != null && choices.Count > 0)
        {
            overlay ??= root.GetComponent<BattlePlanningChoiceOverlay>() ??
                        root.AddComponent<BattlePlanningChoiceOverlay>();
            overlay.Show(
                $"{skill.SkillName} — 고정할 결과 선택",
                choices,
                choiceId => Execute(skill, choiceId));
            return;
        }

        Execute(skill, null);
    }

    private void Execute(Skill skill, string choiceId)
    {
        Character enemy = null;
        BodyPart part = null;
        if (battle?.BattleContext?.Enemies != null)
        {
            foreach (Character candidate in battle.BattleContext.Enemies)
            {
                if (candidate == null || candidate.IsDead)
                    continue;
                enemy = candidate;
                if (candidate.BodyParts != null)
                {
                    foreach (BodyPart p in candidate.BodyParts)
                    {
                        if (p != null && !p.IsBroken)
                        {
                            part = p;
                            break;
                        }
                    }
                }
                break;
            }
        }

        PrestigePlanningExecutionResult result =
            ui.TryExecutePrestigePlanning(skill, enemy, part, choiceId);

        if (result != null && result.Success)
        {
            ShowMessage($"{skill.SkillName} 발동" + (string.IsNullOrEmpty(choiceId) ? string.Empty : " · " + ChoiceLabel(choiceId)));
            ui.RefreshAllBodyPartButtons();
        }
        else
        {
            ShowMessage("위세 발동 실패\n" + (result?.FailureReason ?? string.Empty));
            Debug.LogWarning($"[PrestigeButton] {skill.SkillName} 실패: {result?.FailureReason}");
        }
    }

    private static string ChoiceLabel(string choiceId) =>
        choiceId == HifumiMechanic.TrickChoiceShigoro ? "시고로 고정"
        : choiceId == HifumiMechanic.TrickChoiceCatastrophe ? "대실패 고정"
        : choiceId;

    private void ShowMessage(string message)
    {
        lastMessage = message;
        messageUntil = Time.unscaledTime + 2.5f;
    }

    private bool TryBuild()
    {
        Canvas canvas = ResolveRootCanvas();
        if (canvas == null)
            return false;

        root = new GameObject(
            "PrestigeButton(Temp)",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        root.transform.SetParent(canvas.rootCanvas.transform, false);
        root.layer = canvas.gameObject.layer;

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-24f, 0f);
        rect.sizeDelta = new Vector2(260f, 72f);

        buttonImage = root.GetComponent<Image>();
        buttonImage.raycastTarget = true;

        button = root.GetComponent<Button>();
        button.onClick.AddListener(OnClick);

        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(root.transform, false);
        textGo.layer = root.layer;
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);

        label = textGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 20;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;

        root.transform.SetAsLastSibling();
        return true;
    }

    private Canvas ResolveRootCanvas()
    {
        BattleScreenModeController modeController =
            FindFirstObjectByType<BattleScreenModeController>(FindObjectsInactive.Include);
        Canvas modeCanvas = modeController != null
            ? modeController.GetComponentInParent<Canvas>()
            : null;
        if (modeCanvas != null)
            return modeCanvas.rootCanvas;

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (canvas != null && canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace)
                return canvas;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (root != null)
            Destroy(root);
    }
}
