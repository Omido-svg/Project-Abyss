using UnityEngine;

public class ScriptedParticleEffectPlayer : MonoBehaviour, IBattleVfxPlayable
{
    [SerializeField] private ParticleSystem targetParticleSystem;

    [Header("Emission")]
    [SerializeField] private int particleCount = 24;
    [SerializeField] private float minSpeed = 0.6f;
    [SerializeField] private float maxSpeed = 2.0f;
    [SerializeField] private float minLifetime = 0.25f;
    [SerializeField] private float maxLifetime = 0.65f;
    [SerializeField] private float minSize = 0.08f;
    [SerializeField] private float maxSize = 0.22f;

    [Header("Shape")]
    [SerializeField] private float horizontalSpread = 1.0f;
    [SerializeField] private float upwardBias = 1.6f;
    [SerializeField] private float forwardBias = 0.2f;

    [Header("Color")]
    [SerializeField] private Color startColor = new Color(1f, 0.25f, 0.02f, 1f);
    [SerializeField] private Color hotColor = new Color(1f, 0.85f, 0.15f, 1f);

    private void Awake()
    {
        if (targetParticleSystem == null)
        {
            targetParticleSystem =
                GetComponentInChildren<ParticleSystem>(true);
        }
    }

    public void Play(BattleVfxPlayData playData)
    {
        if (targetParticleSystem == null)
            return;

        targetParticleSystem.Clear();

        Color baseColor =
            playData != null
                ? playData.Color
                : startColor;

        if (baseColor.a <= 0.01f)
            baseColor = startColor;

        float radius =
            playData != null
                ? Mathf.Max(0.1f, playData.Radius)
                : 1f;

        for (int i = 0; i < particleCount; i++)
        {
            ParticleSystem.EmitParams emitParams =
                new ParticleSystem.EmitParams();

            Vector3 randomDir =
                new Vector3(
                    Random.Range(-horizontalSpread, horizontalSpread),
                    Random.Range(0.2f, upwardBias),
                    Random.Range(-horizontalSpread, horizontalSpread) + forwardBias);

            randomDir.Normalize();

            float speed =
                Random.Range(
                    minSpeed,
                    maxSpeed);

            emitParams.position =
                Vector3.zero;

            emitParams.velocity =
                transform.TransformDirection(randomDir) * speed;

            emitParams.startLifetime =
                Random.Range(
                    minLifetime,
                    maxLifetime);

            emitParams.startSize =
                Random.Range(
                    minSize,
                    maxSize) * radius;

            emitParams.startColor =
                Color.Lerp(
                    baseColor,
                    hotColor,
                    Random.Range(0.25f, 1f));

            emitParams.startColor =
                Color.Lerp(
                    baseColor,
                    hotColor,
                    Random.Range(0.25f, 1f));

            targetParticleSystem.Emit(
                emitParams,
                1);
        }
    }
}