using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleStatusRowUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    public void Configure(
        Image iconImage,
        TMP_Text textLabel)
    {
        icon = iconImage;
        label = textLabel;
    }

    public void Bind(StatusEffect effect)
    {
        if (label == null)
            return;

        if (effect == null)
        {
            label.text = string.Empty;

            if (icon != null)
                icon.enabled = false;

            return;
        }

        if (icon != null)
            icon.enabled = icon.sprite != null;

        string stack =
            effect.Stack > 0
                ? $" ×{effect.Stack}"
                : string.Empty;

        string duration =
            effect.IsPermanent
                ? "∞"
                : Mathf.Max(0, effect.Duration).ToString();

        string sourcePart =
            effect.SourcePart == null
                ? string.Empty
                : $" · 원천 {effect.SourcePart.Type}";

        string scope =
            effect.IsCharacterEffect
                ? "전체 슬롯"
                : effect.OwnerPart?.Type.ToString() ?? "부위";

        label.richText = true;
        label.text =
            $"<b>{effect.Name}</b>{stack}  " +
            $"<color=#C9D2E3>[{duration}]</color>\n" +
            $"적용 {scope}{sourcePart}";
    }
}
