using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal interface ICharacterMechanicHudPresenter
{
    bool CanPresent(Character character);
    void Present(Character character);
    void Deactivate();
}

/// <summary>
/// CharacterMechanicHudUI가 소유한 공통 View를 캐릭터별 presenter에 노출한다.
/// 캐릭터 규칙은 알지 않고 텍스트/레이아웃 적용만 담당한다.
/// </summary>
internal sealed class CharacterMechanicHudPanelView
{
    private readonly RectTransform root;
    private readonly TMP_Text summaryText;
    private readonly TMP_Text panelTitleText;
    private readonly GameObject yujinControls;

    public CharacterMechanicHudPanelView(
        RectTransform root,
        TMP_Text summaryText,
        TMP_Text panelTitleText,
        GameObject yujinControls)
    {
        this.root = root;
        this.summaryText = summaryText;
        this.panelTitleText = panelTitleText;
        this.yujinControls = yujinControls;
    }

    public void SetPresentation(
        string title,
        bool showYujinControls)
    {
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
            panelTitleText.text = title ?? string.Empty;

        if (yujinControls != null &&
            yujinControls.activeSelf != showYujinControls)
        {
            yujinControls.SetActive(showYujinControls);
        }
    }

    public void SetSummary(string value)
    {
        if (summaryText != null)
            summaryText.text = value ?? string.Empty;
    }
}

internal sealed class OlafMechanicHudPresenter :
    ICharacterMechanicHudPresenter
{
    private readonly CharacterMechanicHudPanelView view;
    private readonly Func<BattleManager> battleManagerProvider;
    private readonly StringBuilder builder = new();

    public OlafMechanicHudPresenter(
        CharacterMechanicHudPanelView view,
        Func<BattleManager> battleManagerProvider)
    {
        this.view = view;
        this.battleManagerProvider = battleManagerProvider;
    }

    public bool CanPresent(Character character) =>
        character is Olaf;

    public void Present(Character character)
    {
        Olaf olaf = character as Olaf;

        if (olaf == null)
            return;

        OlafMadnessMechanic mechanic =
            olaf.MadnessMechanic;

        view?.SetPresentation(
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

        CharacterMechanicHudTextFormatter
            .AppendOwnPartStates(
                builder,
                olaf);

        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine(
            "<color=#FF8B7B><b>적 혈상</b></color>");

        CharacterMechanicHudTextFormatter
            .AppendEnemyPartValues(
                builder,
                battleManagerProvider?.Invoke()
                    ?.BattleContext?.Enemies,
                (enemy, part) =>
                    enemy.GetPartStatus<Bleeding>(part)
                        ?.Stack ?? 0,
                "혈상");

        view?.SetSummary(builder.ToString());
    }

    public void Deactivate()
    {
    }
}

internal sealed class YujinMechanicHudPresenter :
    ICharacterMechanicHudPresenter
{
    private readonly CharacterMechanicHudPanelView view;
    private readonly YujinWeaponHudView weaponView;
    private readonly Func<BattleManager> battleManagerProvider;
    private readonly StringBuilder builder = new();

    public YujinMechanicHudPresenter(
        CharacterMechanicHudPanelView view,
        YujinWeaponHudView weaponView,
        Func<BattleManager> battleManagerProvider)
    {
        this.view = view;
        this.weaponView = weaponView;
        this.battleManagerProvider = battleManagerProvider;
    }

    public bool CanPresent(Character character) =>
        character is Yujin;

    public void Present(Character character)
    {
        Yujin yujin = character as Yujin;

        if (yujin == null)
            return;

        YujinMechanic mechanic =
            yujin.YujinMechanic;

        view?.SetPresentation(
            "유진  ·  무기 운용",
            true);

        builder.Clear();
        builder.Append("<color=#8DCBFF><b>현재 무기</b></color>  ");
        builder.Append(
            YujinWeaponHudView.GetWeaponDisplayName(
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

        CharacterMechanicHudTextFormatter
            .AppendOwnPartStates(
                builder,
                yujin);

        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine(
            "<color=#F0C36E><b>적 표식</b></color>");

        CharacterMechanicHudTextFormatter
            .AppendEnemyPartValues(
                builder,
                battleManagerProvider?.Invoke()
                    ?.BattleContext?.Enemies,
                (enemy, part) =>
                    mechanic?.GetMark(part) ?? 0,
                "표식");

        view?.SetSummary(builder.ToString());
        weaponView?.Refresh(mechanic);
    }

    public void Deactivate()
    {
        weaponView?.Deactivate();
    }
}

internal static class CharacterMechanicHudTextFormatter
{
    private static readonly PartType[] OwnPartOrder =
    {
        PartType.HEAD,
        PartType.RIGHT_HAND,
        PartType.LEFT_HAND,
        PartType.LEGS
    };

    public static void AppendOwnPartStates(
        StringBuilder builder,
        Character character)
    {
        if (builder == null)
            return;

        int wrote = 0;

        foreach (PartType type in OwnPartOrder)
        {
            BodyPart part =
                FindBodyPart(
                    character,
                    type);

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

    public static void AppendEnemyPartValues(
        StringBuilder builder,
        IReadOnlyList<Character> enemies,
        Func<Character, BodyPart, int> selector,
        string label)
    {
        if (builder == null)
            return;

        if (selector == null || enemies == null)
        {
            builder.Append('-');
            return;
        }

        bool wroteAnyValue = false;

        foreach (Character enemy in enemies)
        {
            if (enemy?.BodyParts == null)
                continue;

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

                if (value <= 0)
                    continue;

                if (!wroteEnemyName)
                {
                    if (wroteAnyValue)
                        builder.AppendLine();

                    builder.Append(
                        enemy.Data?.CharacterName ??
                        enemy.name);
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
            if (part != null && part.Type == type)
                return part;
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
}

/// <summary>
/// 유진 전용 조작/애니메이션 View. 공통 HUD host는 무기 타입이나
/// YujinMechanic의 규칙을 알지 않는다.
/// </summary>
internal sealed class YujinWeaponHudView : IDisposable
{
    public static readonly Color BaekuBase =
        new(0.16f, 0.28f, 0.42f, 0.96f);
    public static readonly Color BaekuAccent =
        new(0.62f, 0.86f, 1f, 1f);
    public static readonly Color JeokseolBase =
        new(0.42f, 0.10f, 0.14f, 0.96f);
    public static readonly Color JeokseolAccent =
        new(1f, 0.28f, 0.32f, 1f);
    public static readonly Color NakilBase =
        new(0.42f, 0.25f, 0.04f, 0.96f);
    public static readonly Color NakilAccent =
        new(1f, 0.70f, 0.12f, 1f);

    private readonly Button baekuButton;
    private readonly Button jeokseolButton;
    private readonly Button nakilButton;
    private readonly Toggle autoSenseToggle;
    private readonly RectTransform weaponSelectionFrame;
    private readonly Image weaponSelectionFrameImage;
    private readonly RectTransform weaponAnnouncementRoot;
    private readonly CanvasGroup weaponAnnouncementGroup;
    private readonly Image weaponAnnouncementBackground;
    private readonly TMP_Text weaponAnnouncementText;

    private YujinMechanic boundMechanic;
    private Sequence weaponSequence;
    private bool hasDisplayedWeapon;
    private YujinWeaponType displayedWeapon;

    private bool positionsCaptured;
    private Vector2 baekuHomePosition;
    private Vector2 jeokseolHomePosition;
    private Vector2 nakilHomePosition;

    public YujinWeaponHudView(
        Button baekuButton,
        Button jeokseolButton,
        Button nakilButton,
        Toggle autoSenseToggle,
        RectTransform weaponSelectionFrame,
        Image weaponSelectionFrameImage,
        RectTransform weaponAnnouncementRoot,
        CanvasGroup weaponAnnouncementGroup,
        Image weaponAnnouncementBackground,
        TMP_Text weaponAnnouncementText)
    {
        this.baekuButton = baekuButton;
        this.jeokseolButton = jeokseolButton;
        this.nakilButton = nakilButton;
        this.autoSenseToggle = autoSenseToggle;
        this.weaponSelectionFrame = weaponSelectionFrame;
        this.weaponSelectionFrameImage = weaponSelectionFrameImage;
        this.weaponAnnouncementRoot = weaponAnnouncementRoot;
        this.weaponAnnouncementGroup = weaponAnnouncementGroup;
        this.weaponAnnouncementBackground = weaponAnnouncementBackground;
        this.weaponAnnouncementText = weaponAnnouncementText;

        CaptureButtonPositions(force: true);
        RestoreButtonPositions();
        BindControls();
    }

    public void Refresh(YujinMechanic mechanic)
    {
        BindMechanic(mechanic);

        if (autoSenseToggle != null)
        {
            autoSenseToggle.interactable = mechanic != null;

            if (mechanic != null &&
                autoSenseToggle.isOn != mechanic.AutoUseSense)
            {
                autoSenseToggle.SetIsOnWithoutNotify(
                    mechanic.AutoUseSense);
            }
        }

        if (mechanic == null)
            return;

        UpdateWeaponButtonState(mechanic);

        if (!hasDisplayedWeapon)
        {
            displayedWeapon = mechanic.CurrentWeapon;
            hasDisplayedWeapon = true;
            ApplyWeaponSelectionImmediate(displayedWeapon);
        }
        else if (displayedWeapon != mechanic.CurrentWeapon)
        {
            YujinWeaponType previous = displayedWeapon;
            displayedWeapon = mechanic.CurrentWeapon;
            PlayWeaponChangeAnimation(
                previous,
                displayedWeapon);
        }
    }

    public void Deactivate()
    {
        UnbindMechanic();
        KillWeaponAnimation();
        SetAnnouncementVisible(false);
    }

    public void Dispose()
    {
        Deactivate();
        baekuButton?.onClick.RemoveListener(OnBaekuClicked);
        jeokseolButton?.onClick.RemoveListener(OnJeokseolClicked);
        nakilButton?.onClick.RemoveListener(OnNakilClicked);
        autoSenseToggle?.onValueChanged.RemoveListener(OnAutoSenseChanged);
    }

    public static string GetWeaponDisplayName(
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

    private void BindControls()
    {
        baekuButton?.onClick.RemoveListener(OnBaekuClicked);
        jeokseolButton?.onClick.RemoveListener(OnJeokseolClicked);
        nakilButton?.onClick.RemoveListener(OnNakilClicked);
        autoSenseToggle?.onValueChanged.RemoveListener(OnAutoSenseChanged);

        baekuButton?.onClick.AddListener(OnBaekuClicked);
        jeokseolButton?.onClick.AddListener(OnJeokseolClicked);
        nakilButton?.onClick.AddListener(OnNakilClicked);
        autoSenseToggle?.onValueChanged.AddListener(OnAutoSenseChanged);
    }

    private void OnBaekuClicked() =>
        SwitchWeapon(YujinWeaponType.Baeku);

    private void OnJeokseolClicked() =>
        SwitchWeapon(YujinWeaponType.Jeokseol);

    private void OnNakilClicked() =>
        SwitchWeapon(YujinWeaponType.Nakil);

    private void OnAutoSenseChanged(bool value)
    {
        BattleCharacterPointerRouter
            .BlockWorldInputForFrames(2);

        if (boundMechanic != null)
            boundMechanic.AutoUseSense = value;
    }

    private void SwitchWeapon(YujinWeaponType weapon)
    {
        BattleCharacterPointerRouter
            .BlockWorldInputForFrames(2);

        boundMechanic?.TrySwitchWeapon(weapon);
    }

    private void BindMechanic(YujinMechanic mechanic)
    {
        if (ReferenceEquals(boundMechanic, mechanic))
            return;

        UnbindMechanic();
        boundMechanic = mechanic;
        hasDisplayedWeapon = false;

        if (boundMechanic != null)
            boundMechanic.WeaponChanged += OnWeaponChanged;
    }

    private void UnbindMechanic()
    {
        if (boundMechanic != null)
            boundMechanic.WeaponChanged -= OnWeaponChanged;

        boundMechanic = null;
        hasDisplayedWeapon = false;
    }

    private void OnWeaponChanged(
        YujinWeaponType previous,
        YujinWeaponType current)
    {
        displayedWeapon = current;
        hasDisplayedWeapon = true;
        PlayWeaponChangeAnimation(previous, current);
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
        if (button == null || mechanic == null)
            return;

        bool selected = mechanic.CurrentWeapon == weapon;
        button.interactable = mechanic.CanSwitchWeapon(weapon);

        Image image = button.GetComponent<Image>();

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
        KillWeaponAnimation();
        RestoreButtonPositions();

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

        if (weaponAnnouncementGroup != null &&
            weaponAnnouncementRoot != null)
        {
            weaponAnnouncementRoot.gameObject.SetActive(true);
            weaponAnnouncementGroup.alpha = 0f;
            weaponSequence.Join(
                weaponAnnouncementGroup.DOFade(1f, 0.12f));
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
            {
                weaponAnnouncementRoot.gameObject
                    .SetActive(false);
            }

            weaponSequence = null;
        });
    }

    private static void AppendWeaponSpecificMotion(
        Sequence sequence,
        YujinWeaponType weapon,
        RectTransform target)
    {
        if (sequence == null || target == null)
            return;

        target.DOKill(false);
        target.localScale = Vector3.one;
        target.localRotation = Quaternion.identity;

        switch (weapon)
        {
            case YujinWeaponType.Baeku:
                sequence.Append(target.DOScale(1.12f, 0.08f));
                sequence.Append(target.DOScale(0.98f, 0.07f));
                sequence.Append(target.DOScale(1.08f, 0.07f));
                sequence.Append(target.DOScale(1f, 0.08f));
                break;

            case YujinWeaponType.Jeokseol:
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
        RestoreButtonPositions();

        RectTransform target =
            GetWeaponButtonRect(weapon);

        if (weaponSelectionFrame != null && target != null)
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
            baekuHomePosition,
            positionsCaptured);

        ResetButtonTransform(
            jeokseolButton,
            jeokseolHomePosition,
            positionsCaptured);

        ResetButtonTransform(
            nakilButton,
            nakilHomePosition,
            positionsCaptured);
    }

    private void CaptureButtonPositions(bool force = false)
    {
        if (positionsCaptured && !force)
            return;

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

        baekuHomePosition = baekuRect.anchoredPosition;
        jeokseolHomePosition = jeokseolRect.anchoredPosition;
        nakilHomePosition = nakilRect.anchoredPosition;
        positionsCaptured = true;
    }

    private void RestoreButtonPositions()
    {
        CaptureButtonPositions();

        if (!positionsCaptured)
            return;

        SetButtonAnchoredPosition(
            baekuButton,
            baekuHomePosition);
        SetButtonAnchoredPosition(
            jeokseolButton,
            jeokseolHomePosition);
        SetButtonAnchoredPosition(
            nakilButton,
            nakilHomePosition);
    }

    private void SetAnnouncementVisible(bool visible)
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
        else
        {
            weaponAnnouncementRoot.gameObject.SetActive(true);
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

        return button?.transform as RectTransform;
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
}
