using UnityEngine;
using UnityEngine.VFX;

public class VfxGraphEffectPlayer : MonoBehaviour, IBattleVfxPlayable
{
    [SerializeField] private VisualEffect visualEffect;

    [Header("Property Names")]
    [SerializeField] private string colorProperty = "Color";
    [SerializeField] private string intensityProperty = "Intensity";
    [SerializeField] private string radiusProperty = "Radius";

    private void Awake()
    {
        if (visualEffect == null)
            visualEffect = GetComponentInChildren<VisualEffect>(true);
    }

    public void Play(
        BattleVfxPlayData playData)
    {
        if (visualEffect == null)
            return;

        BattleVfxDefinition definition =
            playData.Definition;

        if (!string.IsNullOrEmpty(colorProperty) &&
            visualEffect.HasVector4(colorProperty))
        {
            visualEffect.SetVector4(
                colorProperty,
                playData.Color);
        }

        if (!string.IsNullOrEmpty(intensityProperty) &&
            visualEffect.HasFloat(intensityProperty))
        {
            visualEffect.SetFloat(
                intensityProperty,
                playData.Intensity);
        }

        if (!string.IsNullOrEmpty(radiusProperty) &&
            visualEffect.HasFloat(radiusProperty))
        {
            visualEffect.SetFloat(
                radiusProperty,
                playData.Radius);
        }

        if (definition != null &&
            !string.IsNullOrEmpty(definition.PlayEventName))
        {
            visualEffect.SendEvent(
                definition.PlayEventName);
        }
        else
        {
            visualEffect.Play();
        }
    }
}