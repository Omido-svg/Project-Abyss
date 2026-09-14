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

        label.richText = true;
        label.text = BattleStatusUiText.BuildListLabel(effect, effect.Owner);
    }
}
