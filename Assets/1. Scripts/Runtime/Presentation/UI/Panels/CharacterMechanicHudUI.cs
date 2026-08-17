using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 플레이어의 캐릭터 고유 자원을 표시한다.
/// 유진은 무기 선택, 살수의 감 자동 사용, 무기별 전환 연출을 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterMechanicHudUI : MonoBehaviour
{
    [Header("Legacy — retired by 2026-08-17 UI revision")]
    [SerializeField] private bool enableLegacyCharacterPanel = false;

    [SerializeField]
    private BattleManager battleManager;

    [SerializeField]
    private TMP_Text summaryText;

    [SerializeField]
    private GameObject yujinControls;

    [SerializeField]
    private Button baekuButton;

    [SerializeField]
    private Button jeokseolButton;

    [SerializeField]
    private Button nakilButton;

    [SerializeField]
    private Toggle autoSenseToggle;

    [Header("Yujin Weapon Presentation")]
    [SerializeField]
    private RectTransform weaponSelectionFrame;

    [SerializeField]
    private Image weaponSelectionFrameImage;

    [SerializeField]
    private RectTransform weaponAnnouncementRoot;

    [SerializeField]
    private CanvasGroup weaponAnnouncementGroup;

    [SerializeField]
    private Image weaponAnnouncementBackground;

    [SerializeField]
    private TMP_Text weaponAnnouncementText;

    [Header("Panel Presentation")]
    [SerializeField]
    private TMP_Text panelTitleText;

    [SerializeField]
    private TMP_Text autoSenseHelpText;

    [SerializeField]
    private Image panelHeaderImage;

    private readonly StringBuilder builder =
        new StringBuilder();

    private YujinMechanic boundYujinMechanic;
    private Sequence weaponSequence;
    private bool hasDisplayedWeapon;
    private YujinWeaponType displayedWeapon;

    private bool weaponButtonPositionsCaptured;
    private Vector2 baekuButtonHomePosition;
    private Vector2 jeokseolButtonHomePosition;
    private Vector2 nakilButtonHomePosition;

    private static readonly Color BaekuBase =
        new Color(0.16f, 0.28f, 0.42f, 0.96f);

    private static readonly Color BaekuAccent =
        new Color(0.62f, 0.86f, 1f, 1f);

    private static readonly Color JeokseolBase =
        new Color(0.42f, 0.10f, 0.14f, 0.96f);

    private static readonly Color JeokseolAccent =
        new Color(1f, 0.28f, 0.32f, 1f);

    private static readonly Color NakilBase =
        new Color(0.42f, 0.25f, 0.04f, 0.96f);

    private static readonly Color NakilAccent =
        new Color(1f, 0.70f, 0.12f, 1f);

    private void Awake()
    {
        if (!enableLegacyCharacterPanel)
        {
            gameObject.SetActive(false);
            return;
        }

        ResolveBattleManager();
        EnsureView();
        BindControls();
    }

    private void OnEnable()
    {
        ResolveBattleManager();
    }

    private void OnDisable()
    {
        UnbindYujinMechanic();
        KillWeaponAnimation();
    }

    private void OnDestroy()
    {
        UnbindYujinMechanic();
        KillWeaponAnimation();
    }

    private void Update()
    {
        Character player =
            battleManager?.BattleContext?.Player;

        if (player == null)
        {
            UnbindYujinMechanic();

            if (summaryText != null)
                summaryText.text = "캐릭터 고유 정보: 전투 준비 중";

            if (yujinControls != null)
                yujinControls.SetActive(false);

            SetWeaponAnnouncementVisible(false);
            return;
        }

        if (player is Olaf olaf)
        {
            UnbindYujinMechanic();
            DrawOlaf(olaf);

            if (yujinControls != null)
                yujinControls.SetActive(false);

            SetWeaponAnnouncementVisible(false);
            return;
        }

        if (player is Yujin yujin)
        {
            DrawYujin(yujin);

            if (yujinControls != null)
                yujinControls.SetActive(true);

            return;
        }

        UnbindYujinMechanic();

        if (summaryText != null)
        {
            summaryText.text =
                $"{player.Data?.CharacterName ?? player.name}\n" +
                "전용 HUD 없음";
        }

        if (yujinControls != null)
            yujinControls.SetActive(false);

        SetWeaponAnnouncementVisible(false);
    }

    private void DrawOlaf(
        Olaf olaf)
    {
        OlafMadnessMechanic mechanic =
            olaf.MadnessMechanic;

        SetPanelPresentation(
            "올라프  ·  광기 운용",
            false);

        builder.Clear();
        builder.Append("<color=#F2B35F><b>광기</b></color>  ");
        builder.Append(mechanic?.CurrentMadness ?? 0);
        builder.Append('/');
        builder.Append(mechanic?.MaxMadness ?? 10);

        if (mechanic?.IsBlooming == true)
        {
            builder.Append(
                "  <color=#FFD66B><b>만개</b></color>");
        }

        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine(
            "<color=#8DCBFF><b>내 부위 상태</b></color>");
        AppendOwnPartStates(olaf);
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine(
            "<color=#FF8B7B><b>적 혈상</b></color>");
        AppendEnemyPartValues(
            (enemy, part) =>
                enemy.GetPartStatus<Bleeding>(part)?.Stack ?? 0,
            "혈상");

        if (summaryText != null)
            summaryText.text = builder.ToString();
    }

    private void DrawYujin(
        Yujin yujin)
    {
        YujinMechanic mechanic =
            yujin.YujinMechanic;

        BindYujinMechanic(mechanic);

        SetPanelPresentation(
            "유진  ·  무기 운용",
            true);

        builder.Clear();
        builder.Append("<color=#8DCBFF><b>현재 무기</b></color>  ");
        builder.Append(
            GetWeaponDisplayName(
                mechanic?.CurrentWeapon ??
                YujinWeaponType.Baeku));
        builder.Append("    ");
        builder.Append("<color=#E8C875><b>살수의 감</b></color>  ");
        builder.Append(mechanic?.Sense ?? 0);
        builder.AppendLine();
        builder.Append("다음 각인·추격 감 사용  ");
        builder.Append(
            mechanic?.AutoUseSense == true
                ? "<color=#72E0A2><b>ON</b></color>"
                : "<color=#9AA6B2><b>OFF</b></color>");
        builder.AppendLine();
        builder.AppendLine(
            "<color=#A9B8C8>환형  빛 1 · 턴당 1회 · 행동 선택 전</color>");
        builder.AppendLine();
        builder.AppendLine(
            "<color=#8DCBFF><b>내 부위 상태</b></color>");
        AppendOwnPartStates(yujin);
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine(
            "<color=#F0C36E><b>적 표식</b></color>");
        AppendEnemyPartValues(
            (enemy, part) => mechanic?.GetMark(part) ?? 0,
            "표식");

        if (summaryText != null)
            summaryText.text = builder.ToString();

        if (autoSenseToggle != null)
        {
            autoSenseToggle.interactable =
                mechanic != null;

            if (mechanic != null &&
                autoSenseToggle.isOn != mechanic.AutoUseSense)
            {
                autoSenseToggle.SetIsOnWithoutNotify(
                    mechanic.AutoUseSense);
            }
        }

        if (mechanic != null)
        {
            UpdateWeaponButtonState(mechanic);

            if (!hasDisplayedWeapon)
            {
                displayedWeapon =
                    mechanic.CurrentWeapon;

                hasDisplayedWeapon = true;
                ApplyWeaponSelectionImmediate(
                    displayedWeapon);
            }
            else if (displayedWeapon !=
                     mechanic.CurrentWeapon)
            {
                // 자동계획이나 디버그 도구가 직접 무기를 바꾼 경우에도
                // 버튼 클릭과 같은 전환 연출을 재생한다.
                YujinWeaponType previous =
                    displayedWeapon;

                displayedWeapon =
                    mechanic.CurrentWeapon;

                PlayWeaponChangeAnimation(
                    previous,
                    displayedWeapon);
            }
        }
    }

    private void AppendOwnPartStates(
        Character character)
    {
        PartType[] order =
        {
            PartType.HEAD,
            PartType.RIGHT_HAND,
            PartType.LEFT_HAND,
            PartType.LEGS
        };

        int wrote = 0;

        for (int i = 0;
             i < order.Length;
             i++)
        {
            BodyPart part =
                FindBodyPart(
                    character,
                    order[i]);

            if (part == null)
                continue;

            if (wrote > 0)
            {
                builder.Append(
                    wrote % 2 == 0
                        ? "\n"
                        : "      ");
            }

            builder.Append(
                GetPartDisplayName(part.Type));
            builder.Append("  ");
            builder.Append(
                GetPartStateDisplayName(part.State));

            wrote++;
        }

        if (wrote == 0)
            builder.Append('-');
    }

    private void AppendEnemyPartValues(
        System.Func<Character, BodyPart, int> selector,
        string label)
    {
        if (selector == null ||
            battleManager?.BattleContext?.Enemies == null)
        {
            builder.Append('-');
            return;
        }

        bool wroteAnyValue = false;

        foreach (Character enemy in
                 battleManager.BattleContext.Enemies)
        {
            if (enemy == null ||
                enemy.BodyParts == null)
            {
                continue;
            }

            bool wroteEnemyName = false;
            bool wrotePart = false;

            foreach (BodyPart part in enemy.BodyParts)
            {
                if (part == null)
                    continue;

                int value =
                    Mathf.Max(
                        0,
                        selector(enemy, part));

                // 0인 값은 숨겨 패널이 길게 넘치지 않도록 한다.
                if (value <= 0)
                    continue;

                if (!wroteEnemyName)
                {
                    if (wroteAnyValue)
                        builder.AppendLine();

                    builder.Append(
                        enemy.Data?.CharacterName ?? enemy.name);
                    builder.Append("  ");
                    wroteEnemyName = true;
                }

                if (wrotePart)
                    builder.Append("  ·  ");

                builder.Append(
                    GetPartDisplayName(part.Type));
                builder.Append(' ');
                builder.Append(value);

                wrotePart = true;
                wroteAnyValue = true;
            }
        }

        if (!wroteAnyValue)
        {
            builder.Append(label);
            builder.Append(" 없음");
        }
    }

    private static BodyPart FindBodyPart(
        Character character,
        PartType type)
    {
        if (character?.BodyParts == null)
            return null;

        foreach (BodyPart part in character.BodyParts)
        {
            if (part != null &&
                part.Type == type)
            {
                return part;
            }
        }

        return null;
    }

    private static string GetPartDisplayName(
        PartType type)
    {
        return type switch
        {
            PartType.HEAD => "머리",
            PartType.RIGHT_HAND => "오른손",
            PartType.LEFT_HAND => "왼손",
            PartType.LEGS => "다리",
            _ => type.ToString()
        };
    }

    private static string GetPartStateDisplayName(
        BodyPartState state)
    {
        return state switch
        {
            BodyPartState.Normal =>
                "<color=#B9D4EA>정상</color>",

            BodyPartState.Weakened =>
                "<color=#F6C96A>약화</color>",

            BodyPartState.Broken =>
                "<color=#FF746C>파괴</color>",

            _ => state.ToString()
        };
    }

    private void BindControls()
    {
        baekuButton?.onClick.RemoveAllListeners();
        jeokseolButton?.onClick.RemoveAllListeners();
        nakilButton?.onClick.RemoveAllListeners();
        autoSenseToggle?.onValueChanged.RemoveAllListeners();

        baekuButton?.onClick.AddListener(
            () => SwitchWeapon(
                YujinWeaponType.Baeku));

        jeokseolButton?.onClick.AddListener(
            () => SwitchWeapon(
                YujinWeaponType.Jeokseol));

        nakilButton?.onClick.AddListener(
            () => SwitchWeapon(
                YujinWeaponType.Nakil));

        autoSenseToggle?.onValueChanged.AddListener(
            value =>
            {
                BattleCharacterPointerRouter
                    .BlockWorldInputForFrames(2);

                YujinMechanic mechanic =
                    GetYujinMechanic();

                if (mechanic != null)
                    mechanic.AutoUseSense = value;
            });
    }

    private void SwitchWeapon(
        YujinWeaponType weapon)
    {
        BattleCharacterPointerRouter
            .BlockWorldInputForFrames(2);

        YujinMechanic mechanic =
            GetYujinMechanic();

        if (mechanic == null)
            return;

        mechanic.TrySwitchWeapon(weapon);
    }

    private void BindYujinMechanic(
        YujinMechanic mechanic)
    {
        if (ReferenceEquals(
                boundYujinMechanic,
                mechanic))
        {
            return;
        }

        UnbindYujinMechanic();
        boundYujinMechanic = mechanic;
        hasDisplayedWeapon = false;

        if (boundYujinMechanic != null)
        {
            boundYujinMechanic.WeaponChanged +=
                OnWeaponChanged;
        }
    }

    private void UnbindYujinMechanic()
    {
        if (boundYujinMechanic != null)
        {
            boundYujinMechanic.WeaponChanged -=
                OnWeaponChanged;
        }

        boundYujinMechanic = null;
        hasDisplayedWeapon = false;
    }

    private void OnWeaponChanged(
        YujinWeaponType previous,
        YujinWeaponType current)
    {
        displayedWeapon = current;
        hasDisplayedWeapon = true;

        PlayWeaponChangeAnimation(
            previous,
            current);
    }

    private void UpdateWeaponButtonState(
        YujinMechanic mechanic)
    {
        UpdateWeaponButton(
            baekuButton,
            YujinWeaponType.Baeku,
            mechanic,
            BaekuBase,
            BaekuAccent);

        UpdateWeaponButton(
            jeokseolButton,
            YujinWeaponType.Jeokseol,
            mechanic,
            JeokseolBase,
            JeokseolAccent);

        UpdateWeaponButton(
            nakilButton,
            YujinWeaponType.Nakil,
            mechanic,
            NakilBase,
            NakilAccent);
    }

    private static void UpdateWeaponButton(
        Button button,
        YujinWeaponType weapon,
        YujinMechanic mechanic,
        Color baseColor,
        Color accentColor)
    {
        if (button == null ||
            mechanic == null)
        {
            return;
        }

        bool selected =
            mechanic.CurrentWeapon == weapon;

        button.interactable =
            mechanic.CanSwitchWeapon(weapon);

        Image image =
            button.GetComponent<Image>();

        if (image != null)
        {
            image.color = selected
                ? Color.Lerp(baseColor, accentColor, 0.52f)
                : baseColor;
        }
    }

    private void PlayWeaponChangeAnimation(
        YujinWeaponType previous,
        YujinWeaponType current)
    {
        EnsureWeaponPresentation();
        KillWeaponAnimation();
        RestoreWeaponButtonPositions();

        RectTransform target =
            GetWeaponButtonRect(current);

        Color accent =
            GetWeaponAccent(current);

        ApplyAnnouncementContent(current);

        weaponSequence = DOTween.Sequence()
            .SetUpdate(true);

        if (weaponSelectionFrame != null &&
            target != null)
        {
            weaponSelectionFrame.gameObject.SetActive(true);

            weaponSequence.Join(
                weaponSelectionFrame
                    .DOAnchorPos(
                        target.anchoredPosition,
                        0.20f)
                    .SetEase(Ease.OutBack));
        }

        if (weaponSelectionFrameImage != null)
        {
            weaponSequence.Join(
                weaponSelectionFrameImage
                    .DOColor(accent, 0.18f));
        }

        if (weaponAnnouncementGroup != null)
        {
            weaponAnnouncementRoot.gameObject.SetActive(true);
            weaponAnnouncementGroup.alpha = 0f;

            weaponSequence.Join(
                weaponAnnouncementGroup
                    .DOFade(1f, 0.12f));
        }

        if (weaponAnnouncementRoot != null)
        {
            weaponAnnouncementRoot.localScale =
                new Vector3(0.92f, 0.92f, 1f);

            weaponSequence.Join(
                weaponAnnouncementRoot
                    .DOScale(1f, 0.20f)
                    .SetEase(Ease.OutBack));
        }

        AppendWeaponSpecificMotion(
            weaponSequence,
            current,
            target);

        weaponSequence.AppendInterval(0.72f);

        if (weaponAnnouncementGroup != null)
        {
            weaponSequence.Append(
                weaponAnnouncementGroup
                    .DOFade(0f, 0.22f));
        }

        weaponSequence.OnComplete(() =>
        {
            if (weaponAnnouncementRoot != null)
                weaponAnnouncementRoot.gameObject.SetActive(false);

            weaponSequence = null;
        });
    }

    private static void AppendWeaponSpecificMotion(
        Sequence sequence,
        YujinWeaponType weapon,
        RectTransform target)
    {
        if (sequence == null ||
            target == null)
        {
            return;
        }

        target.DOKill(false);
        target.localScale = Vector3.one;
        target.localRotation = Quaternion.identity;

        switch (weapon)
        {
            case YujinWeaponType.Baeku:
                // 백우: 세 코인을 상징하는 짧고 안정적인 3연속 맥동.
                sequence.Append(
                    target.DOScale(1.12f, 0.08f));
                sequence.Append(
                    target.DOScale(0.98f, 0.07f));
                sequence.Append(
                    target.DOScale(1.08f, 0.07f));
                sequence.Append(
                    target.DOScale(1f, 0.08f));
                break;

            case YujinWeaponType.Jeokseol:
                // 적설: 위치를 직접 건드리지 않고 회전과 크기만 흔든다.
                // DOPunchAnchorPos는 Sequence가 중간에 Kill될 때 버튼의
                // anchoredPosition을 오염시킬 수 있으므로 사용하지 않는다.
                sequence.Append(
                    target.DOPunchRotation(
                        new Vector3(0f, 0f, -10f),
                        0.30f,
                        7,
                        0.48f));
                sequence.Join(
                    target.DOPunchScale(
                        new Vector3(0.11f, 0.05f, 0f),
                        0.30f,
                        5,
                        0.42f));
                break;

            case YujinWeaponType.Nakil:
                // 낙일: 단일 코인과 처형을 상징하는 한 번의 강한 낙하 충격.
                sequence.Append(
                    target.DOScale(1.24f, 0.12f)
                        .SetEase(Ease.OutExpo));
                sequence.Join(
                    target.DOLocalRotate(
                        new Vector3(0f, 0f, -8f),
                        0.12f));
                sequence.Append(
                    target.DOScale(1f, 0.18f)
                        .SetEase(Ease.OutBounce));
                sequence.Join(
                    target.DOLocalRotate(
                        Vector3.zero,
                        0.18f));
                break;
        }
    }

    private void ApplyWeaponSelectionImmediate(
        YujinWeaponType weapon)
    {
        EnsureWeaponPresentation();
        RestoreWeaponButtonPositions();

        RectTransform target =
            GetWeaponButtonRect(weapon);

        if (weaponSelectionFrame != null &&
            target != null)
        {
            weaponSelectionFrame.gameObject.SetActive(true);
            weaponSelectionFrame.anchoredPosition =
                target.anchoredPosition;
        }

        if (weaponSelectionFrameImage != null)
        {
            weaponSelectionFrameImage.color =
                GetWeaponAccent(weapon);
        }
    }

    private void ApplyAnnouncementContent(
        YujinWeaponType weapon)
    {
        if (weaponAnnouncementBackground != null)
        {
            weaponAnnouncementBackground.color =
                Color.Lerp(
                    GetWeaponBase(weapon),
                    Color.black,
                    0.14f);
        }

        if (weaponAnnouncementText == null)
            return;

        weaponAnnouncementText.text =
            weapon switch
            {
                YujinWeaponType.Baeku =>
                    "백우 전환  ·  3코인 / 안정형",

                YujinWeaponType.Jeokseol =>
                    "적설 전환  ·  2코인 / 다부위",

                YujinWeaponType.Nakil =>
                    "낙일 전환  ·  1코인 / 처형형",

                _ => weapon.ToString()
            };

        weaponAnnouncementText.color =
            GetWeaponAccent(weapon);
    }

    private void KillWeaponAnimation()
    {
        if (weaponSequence != null)
        {
            weaponSequence.Kill(false);
            weaponSequence = null;
        }

        ResetButtonTransform(
            baekuButton,
            baekuButtonHomePosition,
            weaponButtonPositionsCaptured);

        ResetButtonTransform(
            jeokseolButton,
            jeokseolButtonHomePosition,
            weaponButtonPositionsCaptured);

        ResetButtonTransform(
            nakilButton,
            nakilButtonHomePosition,
            weaponButtonPositionsCaptured);
    }

    private void CaptureWeaponButtonPositions(
        bool force = false)
    {
        if (weaponButtonPositionsCaptured &&
            !force)
        {
            return;
        }

        RectTransform baekuRect =
            baekuButton?.transform as RectTransform;

        RectTransform jeokseolRect =
            jeokseolButton?.transform as RectTransform;

        RectTransform nakilRect =
            nakilButton?.transform as RectTransform;

        if (baekuRect == null ||
            jeokseolRect == null ||
            nakilRect == null)
        {
            return;
        }

        baekuButtonHomePosition =
            baekuRect.anchoredPosition;

        jeokseolButtonHomePosition =
            jeokseolRect.anchoredPosition;

        nakilButtonHomePosition =
            nakilRect.anchoredPosition;

        weaponButtonPositionsCaptured = true;
    }

    private void RestoreWeaponButtonPositions()
    {
        CaptureWeaponButtonPositions();

        if (!weaponButtonPositionsCaptured)
            return;

        SetButtonAnchoredPosition(
            baekuButton,
            baekuButtonHomePosition);

        SetButtonAnchoredPosition(
            jeokseolButton,
            jeokseolButtonHomePosition);

        SetButtonAnchoredPosition(
            nakilButton,
            nakilButtonHomePosition);
    }

    private static void ResetButtonTransform(
        Button button,
        Vector2 homePosition,
        bool restorePosition)
    {
        RectTransform rect =
            button?.transform as RectTransform;

        if (rect == null)
            return;

        rect.DOKill(false);

        if (restorePosition)
            rect.anchoredPosition = homePosition;

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetButtonAnchoredPosition(
        Button button,
        Vector2 anchoredPosition)
    {
        RectTransform rect =
            button?.transform as RectTransform;

        if (rect != null)
            rect.anchoredPosition = anchoredPosition;
    }

    private YujinMechanic GetYujinMechanic()
    {
        return (
            battleManager?.BattleContext?.Player
            as Yujin)?.YujinMechanic;
    }

    private void ResolveBattleManager()
    {
        if (battleManager == null)
        {
            battleManager =
                FindFirstObjectByType<BattleManager>();
        }
    }

    private void EnsureView()
    {
        RectTransform root =
            transform as RectTransform;

        if (root == null)
        {
            Debug.LogError(
                "[CharacterMechanicHudUI] RectTransform이 필요합니다.",
                this);
            enabled = false;
            return;
        }

        // 화면 왼쪽 아래 고정 패널.
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.anchoredPosition = new Vector2(14f, 18f);
        root.sizeDelta = new Vector2(430f, 340f);

        Image background =
            GetComponent<Image>();

        if (background == null)
            background = gameObject.AddComponent<Image>();

        background.color =
            new Color(0.025f, 0.040f, 0.060f, 0.94f);
        background.raycastTarget = false;

        UnityEngine.UI.Outline outline =
            GetComponent<UnityEngine.UI.Outline>();

        if (outline == null)
        {
            outline =
                gameObject.AddComponent<UnityEngine.UI.Outline>();
        }

        outline.effectColor =
            new Color(0.20f, 0.56f, 0.82f, 0.58f);
        outline.effectDistance =
            new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        EnsurePanelHeader(root);
        EnsureSummary(root);
        EnsureYujinControls(root);
        ApplySharedFont();
        EnsureWeaponPresentation();

        CaptureWeaponButtonPositions(force: true);
        RestoreWeaponButtonPositions();
    }

    private void EnsurePanelHeader(
        RectTransform root)
    {
        Transform existing =
            root.Find("PanelHeader");

        RectTransform headerRect;

        if (existing == null)
        {
            GameObject header =
                new GameObject(
                    "PanelHeader",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

            headerRect =
                header.GetComponent<RectTransform>();

            headerRect.SetParent(root, false);
        }
        else
        {
            headerRect =
                existing as RectTransform;
        }

        headerRect.anchorMin =
            new Vector2(0f, 1f);
        headerRect.anchorMax =
            new Vector2(1f, 1f);
        headerRect.pivot =
            new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta =
            new Vector2(0f, 38f);

        panelHeaderImage =
            headerRect.GetComponent<Image>();

        panelHeaderImage.color =
            new Color(0.055f, 0.16f, 0.25f, 0.96f);
        panelHeaderImage.raycastTarget = false;

        Transform titleExisting =
            headerRect.Find("Title");

        if (titleExisting == null)
        {
            panelTitleText =
                CreateText(
                    "Title",
                    headerRect,
                    Vector2.zero,
                    Vector2.zero,
                    18f);
        }
        else
        {
            panelTitleText =
                titleExisting.GetComponent<TMP_Text>();
        }

        RectTransform titleRect =
            panelTitleText.transform as RectTransform;

        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.pivot =
            new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = Vector2.zero;

        panelTitleText.fontStyle =
            FontStyles.Bold;
        panelTitleText.alignment =
            TextAlignmentOptions.Left;
        panelTitleText.margin =
            new Vector4(14f, 0f, 8f, 0f);
        panelTitleText.color =
            new Color(0.90f, 0.96f, 1f, 1f);
        panelTitleText.textWrappingMode =
            TextWrappingModes.NoWrap;
        panelTitleText.raycastTarget = false;
    }

    private void EnsureSummary(
        RectTransform root)
    {
        if (summaryText == null)
        {
            Transform existing =
                root.Find("Summary");

            summaryText =
                existing != null
                    ? existing.GetComponent<TMP_Text>()
                    : CreateText(
                        "Summary",
                        root,
                        Vector2.zero,
                        Vector2.zero,
                        16f);
        }

        RectTransform summaryRect =
            summaryText.transform as RectTransform;

        summaryRect.anchorMin =
            new Vector2(0f, 1f);
        summaryRect.anchorMax =
            new Vector2(1f, 1f);
        summaryRect.pivot =
            new Vector2(0.5f, 1f);
        summaryRect.anchoredPosition =
            new Vector2(0f, -48f);
        summaryRect.sizeDelta =
            new Vector2(-24f, 174f);

        summaryText.fontSize = 16f;
        summaryText.enableAutoSizing = true;
        summaryText.fontSizeMin = 12f;
        summaryText.fontSizeMax = 16f;
        summaryText.richText = true;
        summaryText.textWrappingMode =
            TextWrappingModes.Normal;
        summaryText.overflowMode =
            TextOverflowModes.Ellipsis;
        summaryText.alignment =
            TextAlignmentOptions.TopLeft;
        summaryText.lineSpacing = -2f;
        summaryText.raycastTarget = false;
    }

    private void EnsureYujinControls(
        RectTransform root)
    {
        if (yujinControls == null)
        {
            yujinControls =
                new GameObject(
                    "YujinControls",
                    typeof(RectTransform));

            yujinControls.transform.SetParent(
                root,
                false);
        }

        RectTransform controls =
            yujinControls.transform as RectTransform;

        controls.anchorMin =
            new Vector2(0f, 0f);
        controls.anchorMax =
            new Vector2(1f, 0f);
        controls.pivot =
            new Vector2(0.5f, 0f);
        controls.anchoredPosition =
            new Vector2(0f, 12f);
        controls.sizeDelta =
            new Vector2(-24f, 108f);

        if (baekuButton == null)
        {
            baekuButton =
                CreateButton(
                    "BaekuButton",
                    "백우",
                    controls,
                    Vector2.zero,
                    BaekuBase);
        }

        if (jeokseolButton == null)
        {
            jeokseolButton =
                CreateButton(
                    "JeokseolButton",
                    "적설",
                    controls,
                    Vector2.zero,
                    JeokseolBase);
        }

        if (nakilButton == null)
        {
            nakilButton =
                CreateButton(
                    "NakilButton",
                    "낙일",
                    controls,
                    Vector2.zero,
                    NakilBase);
        }

        ConfigureWeaponButton(
            baekuButton,
            new Vector2(0f, 66f),
            BaekuBase);

        ConfigureWeaponButton(
            jeokseolButton,
            new Vector2(128f, 66f),
            JeokseolBase);

        ConfigureWeaponButton(
            nakilButton,
            new Vector2(256f, 66f),
            NakilBase);

        if (autoSenseToggle == null)
        {
            autoSenseToggle =
                CreateToggle(
                    controls,
                    Vector2.zero);
        }

        ConfigureAutoSenseToggle(
            controls);
    }

    private static void ConfigureWeaponButton(
        Button button,
        Vector2 anchoredPosition,
        Color baseColor)
    {
        RectTransform rect =
            button?.transform as RectTransform;

        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition =
            anchoredPosition;
        rect.sizeDelta =
            new Vector2(118f, 34f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image image =
            button.GetComponent<Image>();

        if (image != null)
            image.color = baseColor;

        TMP_Text label =
            button.GetComponentInChildren<TMP_Text>(
                true);

        if (label != null)
        {
            label.fontSize = 16f;
            label.fontStyle = FontStyles.Bold;
            label.alignment =
                TextAlignmentOptions.Center;
            label.textWrappingMode =
                TextWrappingModes.NoWrap;
            label.raycastTarget = false;
        }
    }

    private void ConfigureAutoSenseToggle(
        RectTransform controls)
    {
        if (autoSenseToggle == null)
            return;

        RectTransform toggleRect =
            autoSenseToggle.transform as RectTransform;

        toggleRect.anchorMin = Vector2.zero;
        toggleRect.anchorMax = Vector2.zero;
        toggleRect.pivot = Vector2.zero;
        toggleRect.anchoredPosition =
            new Vector2(0f, 18f);
        toggleRect.sizeDelta =
            new Vector2(224f, 36f);

        Image clickArea =
            autoSenseToggle.GetComponent<Image>();

        if (clickArea == null)
        {
            clickArea =
                autoSenseToggle.gameObject
                    .AddComponent<Image>();
        }

        clickArea.color =
            new Color(1f, 1f, 1f, 0.001f);
        clickArea.raycastTarget = true;

        Transform backgroundTransform =
            toggleRect.Find("Background");

        Image track =
            backgroundTransform != null
                ? backgroundTransform.GetComponent<Image>()
                : null;

        if (track != null)
        {
            RectTransform trackRect =
                track.transform as RectTransform;

            trackRect.anchorMin =
                new Vector2(0f, 0.5f);
            trackRect.anchorMax =
                new Vector2(0f, 0.5f);
            trackRect.pivot =
                new Vector2(0f, 0.5f);
            trackRect.anchoredPosition =
                new Vector2(2f, 0f);
            trackRect.sizeDelta =
                new Vector2(42f, 22f);

            track.color =
                new Color(0.12f, 0.18f, 0.23f, 1f);
            track.raycastTarget = false;
        }

        Image checkmark =
            backgroundTransform != null
                ? backgroundTransform
                    .Find("Checkmark")
                    ?.GetComponent<Image>()
                : null;

        if (checkmark != null)
        {
            RectTransform checkRect =
                checkmark.transform as RectTransform;

            checkRect.anchorMin =
                new Vector2(0.16f, 0.20f);
            checkRect.anchorMax =
                new Vector2(0.84f, 0.80f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            checkmark.color =
                new Color(0.36f, 0.90f, 0.64f, 1f);
            checkmark.raycastTarget = false;
        }

        TMP_Text label =
            toggleRect.Find("Label")
                ?.GetComponent<TMP_Text>();

        if (label != null)
        {
            RectTransform labelRect =
                label.transform as RectTransform;

            labelRect.anchorMin =
                new Vector2(0f, 0f);
            labelRect.anchorMax =
                new Vector2(0f, 1f);
            labelRect.pivot =
                new Vector2(0f, 0.5f);
            labelRect.anchoredPosition =
                new Vector2(52f, 0f);
            labelRect.sizeDelta =
                new Vector2(170f, 0f);

            label.text =
                "다음 각인·추격 감 사용";
            label.fontSize = 14f;
            label.fontStyle = FontStyles.Bold;
            label.alignment =
                TextAlignmentOptions.Left;
            label.textWrappingMode =
                TextWrappingModes.NoWrap;
            label.raycastTarget = false;
        }

        autoSenseToggle.targetGraphic =
            track != null
                ? track
                : clickArea;
        autoSenseToggle.graphic = checkmark;
        autoSenseToggle.transition =
            Selectable.Transition.ColorTint;

        Navigation navigation =
            autoSenseToggle.navigation;
        navigation.mode = Navigation.Mode.None;
        autoSenseToggle.navigation = navigation;

        Transform helpExisting =
            controls.Find("AutoSenseHelp");

        if (helpExisting == null)
        {
            autoSenseHelpText =
                CreateText(
                    "AutoSenseHelp",
                    controls,
                    Vector2.zero,
                    Vector2.zero,
                    12f);
        }
        else
        {
            autoSenseHelpText =
                helpExisting.GetComponent<TMP_Text>();
        }

        RectTransform helpRect =
            autoSenseHelpText.transform as RectTransform;

        helpRect.anchorMin = Vector2.zero;
        helpRect.anchorMax = Vector2.zero;
        helpRect.pivot = Vector2.zero;
        helpRect.anchoredPosition =
            new Vector2(230f, 10f);
        helpRect.sizeDelta =
            new Vector2(174f, 48f);

        autoSenseHelpText.text =
            "각인·추격 선택 전에 설정합니다.\n선택값은 해당 행동에 저장됩니다.";
        autoSenseHelpText.fontSize = 11.5f;
        autoSenseHelpText.color =
            new Color(0.68f, 0.76f, 0.82f, 1f);
        autoSenseHelpText.alignment =
            TextAlignmentOptions.BottomLeft;
        autoSenseHelpText.textWrappingMode =
            TextWrappingModes.Normal;
        autoSenseHelpText.raycastTarget = false;
    }

    private void SetPanelPresentation(
        string title,
        bool showYujinControls)
    {
        RectTransform root =
            transform as RectTransform;

        if (root != null)
        {
            root.sizeDelta =
                new Vector2(
                    430f,
                    showYujinControls
                        ? 340f
                        : 246f);
        }

        if (panelTitleText != null)
            panelTitleText.text = title;

        if (yujinControls != null)
            yujinControls.SetActive(showYujinControls);
    }

    private void ApplySharedFont()
    {
        TMP_FontAsset sharedFont =
            summaryText?.font;

        if (sharedFont == null)
        {
            sharedFont =
                baekuButton
                    ?.GetComponentInChildren<TMP_Text>(true)
                    ?.font;
        }

        if (sharedFont == null)
            return;

        if (panelTitleText != null)
            panelTitleText.font = sharedFont;

        if (autoSenseHelpText != null)
            autoSenseHelpText.font = sharedFont;

        TMP_Text toggleLabel =
            autoSenseToggle
                ?.GetComponentInChildren<TMP_Text>(true);

        if (toggleLabel != null)
            toggleLabel.font = sharedFont;

        TMP_Text baekuLabel =
            baekuButton
                ?.GetComponentInChildren<TMP_Text>(true);

        TMP_Text jeokseolLabel =
            jeokseolButton
                ?.GetComponentInChildren<TMP_Text>(true);

        TMP_Text nakilLabel =
            nakilButton
                ?.GetComponentInChildren<TMP_Text>(true);

        if (baekuLabel != null)
            baekuLabel.font = sharedFont;

        if (jeokseolLabel != null)
            jeokseolLabel.font = sharedFont;

        if (nakilLabel != null)
            nakilLabel.font = sharedFont;
    }

    private void EnsureWeaponPresentation()
    {
        RectTransform root =
            transform as RectTransform;

        RectTransform controls =
            yujinControls?.transform as RectTransform;

        if (root == null ||
            controls == null)
        {
            return;
        }

        if (weaponSelectionFrame == null)
        {
            GameObject frame =
                new GameObject(
                    "WeaponSelectionFrame",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

            weaponSelectionFrame =
                frame.GetComponent<RectTransform>();

            weaponSelectionFrame.SetParent(
                controls,
                false);

            weaponSelectionFrame.anchorMin =
                new Vector2(0f, 0f);
            weaponSelectionFrame.anchorMax =
                new Vector2(0f, 0f);
            weaponSelectionFrame.pivot =
                new Vector2(0f, 0f);
            weaponSelectionFrame.sizeDelta =
                new Vector2(96f, 30f);

            weaponSelectionFrameImage =
                frame.GetComponent<Image>();

            weaponSelectionFrameImage.color =
                BaekuAccent;

            weaponSelectionFrameImage.raycastTarget = false;
            frame.transform.SetAsFirstSibling();
        }
        else if (weaponSelectionFrameImage == null)
        {
            weaponSelectionFrameImage =
                weaponSelectionFrame.GetComponent<Image>();
        }

        if (weaponSelectionFrame != null)
        {
            weaponSelectionFrame.anchorMin = Vector2.zero;
            weaponSelectionFrame.anchorMax = Vector2.zero;
            weaponSelectionFrame.pivot = Vector2.zero;
            weaponSelectionFrame.sizeDelta =
                new Vector2(118f, 34f);
        }

        if (weaponSelectionFrameImage != null)
        {
            weaponSelectionFrameImage.color =
                new Color(
                    weaponSelectionFrameImage.color.r,
                    weaponSelectionFrameImage.color.g,
                    weaponSelectionFrameImage.color.b,
                    0.34f);
        }

        if (weaponAnnouncementRoot == null)
        {
            GameObject announcement =
                new GameObject(
                    "WeaponAnnouncement",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup));

            weaponAnnouncementRoot =
                announcement.GetComponent<RectTransform>();

            weaponAnnouncementRoot.SetParent(
                root,
                false);

            weaponAnnouncementRoot.anchorMin =
                new Vector2(0f, 1f);
            weaponAnnouncementRoot.anchorMax =
                new Vector2(0f, 1f);
            weaponAnnouncementRoot.pivot =
                new Vector2(0f, 1f);
            weaponAnnouncementRoot.anchoredPosition =
                new Vector2(12f, -218f);
            weaponAnnouncementRoot.sizeDelta =
                new Vector2(406f, 34f);

            weaponAnnouncementBackground =
                announcement.GetComponent<Image>();

            weaponAnnouncementGroup =
                announcement.GetComponent<CanvasGroup>();

            weaponAnnouncementText =
                CreateText(
                    "Label",
                    weaponAnnouncementRoot,
                    Vector2.zero,
                    weaponAnnouncementRoot.sizeDelta,
                    16f);

            RectTransform labelRect =
                weaponAnnouncementText.transform
                    as RectTransform;

            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot =
                new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;

            weaponAnnouncementText.alignment =
                TextAlignmentOptions.Center;

            weaponAnnouncementGroup.interactable = false;
            weaponAnnouncementGroup.blocksRaycasts = false;
            weaponAnnouncementGroup.alpha = 0f;
            announcement.SetActive(false);
        }
        else
        {
            if (weaponAnnouncementBackground == null)
            {
                weaponAnnouncementBackground =
                    weaponAnnouncementRoot.GetComponent<Image>();
            }

            if (weaponAnnouncementGroup == null)
            {
                weaponAnnouncementGroup =
                    weaponAnnouncementRoot.GetComponent<CanvasGroup>();
            }

            if (weaponAnnouncementText == null)
            {
                weaponAnnouncementText =
                    weaponAnnouncementRoot
                        .Find("Label")
                        ?.GetComponent<TMP_Text>();
            }
        }

        if (weaponAnnouncementRoot != null)
        {
            weaponAnnouncementRoot.anchorMin =
                new Vector2(0f, 1f);
            weaponAnnouncementRoot.anchorMax =
                new Vector2(0f, 1f);
            weaponAnnouncementRoot.pivot =
                new Vector2(0f, 1f);
            weaponAnnouncementRoot.anchoredPosition =
                new Vector2(12f, -218f);
            weaponAnnouncementRoot.sizeDelta =
                new Vector2(406f, 34f);
        }

        if (weaponAnnouncementText != null)
        {
            RectTransform labelRect =
                weaponAnnouncementText.transform
                    as RectTransform;

            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot =
                new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;
            weaponAnnouncementText.alignment =
                TextAlignmentOptions.Center;
        }
    }

    private void SetWeaponAnnouncementVisible(
        bool visible)
    {
        if (weaponAnnouncementRoot == null)
            return;

        if (!visible)
        {
            KillWeaponAnimation();
            weaponAnnouncementRoot.gameObject.SetActive(false);

            if (weaponAnnouncementGroup != null)
                weaponAnnouncementGroup.alpha = 0f;
        }
    }

    private RectTransform GetWeaponButtonRect(
        YujinWeaponType weapon)
    {
        Button button =
            weapon switch
            {
                YujinWeaponType.Baeku => baekuButton,
                YujinWeaponType.Jeokseol => jeokseolButton,
                YujinWeaponType.Nakil => nakilButton,
                _ => null
            };

        return button?.transform
            as RectTransform;
    }

    private static Color GetWeaponBase(
        YujinWeaponType weapon)
    {
        return weapon switch
        {
            YujinWeaponType.Baeku => BaekuBase,
            YujinWeaponType.Jeokseol => JeokseolBase,
            YujinWeaponType.Nakil => NakilBase,
            _ => Color.gray
        };
    }

    private static Color GetWeaponAccent(
        YujinWeaponType weapon)
    {
        return weapon switch
        {
            YujinWeaponType.Baeku => BaekuAccent,
            YujinWeaponType.Jeokseol => JeokseolAccent,
            YujinWeaponType.Nakil => NakilAccent,
            _ => Color.white
        };
    }

    private static string GetWeaponDisplayName(
        YujinWeaponType weapon)
    {
        return weapon switch
        {
            YujinWeaponType.Baeku => "백우",
            YujinWeaponType.Jeokseol => "적설",
            YujinWeaponType.Nakil => "낙일",
            _ => weapon.ToString()
        };
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize)
    {
        GameObject value =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        RectTransform rect =
            value.GetComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text =
            value.GetComponent<TextMeshProUGUI>();

        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment =
            TextAlignmentOptions.TopLeft;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;

        return text;
    }

    private static Button CreateButton(
        string objectName,
        string label,
        Transform parent,
        Vector2 anchoredPosition,
        Color baseColor)
    {
        GameObject value =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));

        RectTransform rect =
            value.GetComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(96f, 30f);

        value.GetComponent<Image>().color =
            baseColor;

        TMP_Text text =
            CreateText(
                "Label",
                rect,
                Vector2.zero,
                rect.sizeDelta,
                16f);

        RectTransform textRect =
            text.transform as RectTransform;

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        text.text = label;
        text.alignment =
            TextAlignmentOptions.Center;

        return value.GetComponent<Button>();
    }

    private static Toggle CreateToggle(
        Transform parent,
        Vector2 anchoredPosition)
    {
        GameObject value =
            new GameObject(
                "AutoSenseToggle",
                typeof(RectTransform),
                typeof(Image),
                typeof(Toggle));

        RectTransform rect =
            value.GetComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(224f, 36f);

        Image clickArea =
            value.GetComponent<Image>();

        clickArea.color =
            new Color(1f, 1f, 1f, 0.001f);
        clickArea.raycastTarget = true;

        GameObject background =
            new GameObject(
                "Background",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        RectTransform bgRect =
            background.GetComponent<RectTransform>();

        bgRect.SetParent(rect, false);
        bgRect.anchorMin = new Vector2(0f, 0.5f);
        bgRect.anchorMax = new Vector2(0f, 0.5f);
        bgRect.pivot = new Vector2(0f, 0.5f);
        bgRect.anchoredPosition = new Vector2(2f, 0f);
        bgRect.sizeDelta = new Vector2(42f, 22f);

        Image bgImage =
            background.GetComponent<Image>();

        bgImage.color =
            new Color(0.12f, 0.18f, 0.23f, 1f);
        bgImage.raycastTarget = false;

        GameObject checkmark =
            new GameObject(
                "Checkmark",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        RectTransform checkRect =
            checkmark.GetComponent<RectTransform>();

        checkRect.SetParent(bgRect, false);
        checkRect.anchorMin = new Vector2(0.16f, 0.20f);
        checkRect.anchorMax = new Vector2(0.84f, 0.80f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;

        Image checkImage =
            checkmark.GetComponent<Image>();

        checkImage.color =
            new Color(0.36f, 0.90f, 0.64f, 1f);
        checkImage.raycastTarget = false;

        TMP_Text label =
            CreateText(
                "Label",
                rect,
                Vector2.zero,
                Vector2.zero,
                14f);

        RectTransform labelRect =
            label.transform as RectTransform;

        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(52f, 0f);
        labelRect.sizeDelta = new Vector2(170f, 0f);

        label.text = "다음 각인·추격 감 사용";
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;

        Toggle toggle =
            value.GetComponent<Toggle>();

        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;

        return toggle;
    }

}
