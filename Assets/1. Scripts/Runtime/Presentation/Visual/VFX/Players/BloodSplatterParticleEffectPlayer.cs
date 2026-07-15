using UnityEngine;

public class BloodSplatterParticleEffectPlayer : MonoBehaviour, IBattleVfxPlayable
{
    [SerializeField] private ParticleSystem targetParticleSystem;

    [Header("Emission")]
    [SerializeField] private int particleCount = 36;
    [SerializeField] private float minSpeed = 1.2f;
    [SerializeField] private float maxSpeed = 4.2f;
    [SerializeField] private float minLifetime = 0.22f;
    [SerializeField] private float maxLifetime = 0.75f;
    [SerializeField] private float minSize = 0.05f;
    [SerializeField] private float maxSize = 0.18f;

    [Header("Direction")]
    [SerializeField] private float forwardPower = 1.6f;
    [SerializeField] private float upwardPower = 0.75f;
    [SerializeField] private float sideSpread = 1.15f;
    [SerializeField] private float backwardChance = 0.18f;

    [Header("Color")]
    [SerializeField] private Color bloodColor = new Color(0.55f, 0.0f, 0.0f, 1f);
    [SerializeField] private Color darkBloodColor = new Color(0.16f, 0.0f, 0.0f, 1f);

    [Header("Debug")]
    [SerializeField] private bool logEmitPosition;

    private void Awake()
    {
        if (targetParticleSystem == null)
            targetParticleSystem = GetComponentInChildren<ParticleSystem>(true);
    }

    public void Play(BattleVfxPlayData playData)
    {
        if (targetParticleSystem == null)
            return;

        targetParticleSystem.Clear();

        Vector3 hitForward =
            GetHitForward(playData);

        Vector3 side =
            Vector3.Cross(
                Vector3.up,
                hitForward);

        if (side.sqrMagnitude <= 0.0001f)
            side = transform.right;

        side.Normalize();

        float radius =
            playData != null
                ? Mathf.Max(0.1f, playData.Radius)
                : 1f;

        Vector3 emitPosition =
            GetEmitPosition();

        if (logEmitPosition)
        {
            Debug.Log(
                $"[BloodSplatterParticleEffectPlayer] Emit Position / " +
                $"ParticleSystem={targetParticleSystem.name}, " +
                $"SimulationSpace={targetParticleSystem.main.simulationSpace}, " +
                $"VfxRoot={transform.position}, " +
                $"ParticleSystemPos={targetParticleSystem.transform.position}, " +
                $"EmitPosition={emitPosition}");
        }

        for (int i = 0; i < particleCount; i++)
        {
            ParticleSystem.EmitParams emitParams =
                new ParticleSystem.EmitParams();

            float sideAmount =
                Random.Range(
                    -sideSpread,
                    sideSpread);

            float upAmount =
                Random.Range(
                    0.05f,
                    upwardPower);

            float forwardAmount =
                Random.Range(
                    0.35f,
                    forwardPower);

            if (Random.value < backwardChance)
                forwardAmount *= -0.35f;

            Vector3 worldDirection =
                hitForward * forwardAmount +
                side * sideAmount +
                Vector3.up * upAmount;

            if (worldDirection.sqrMagnitude <= 0.0001f)
                worldDirection = Vector3.up;

            worldDirection.Normalize();

            float speed =
                Random.Range(
                    minSpeed,
                    maxSpeed);

            Vector3 worldVelocity =
                worldDirection * speed;

            emitParams.position =
                emitPosition;

            emitParams.velocity =
                GetEmitVelocity(
                    worldVelocity);

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
                    bloodColor,
                    darkBloodColor,
                    Random.Range(0f, 0.65f));

            emitParams.rotation3D =
                new Vector3(
                    0f,
                    0f,
                    Random.Range(0f, 360f));

            targetParticleSystem.Emit(
                emitParams,
                1);
        }
    }

    private Vector3 GetEmitPosition()
    {
        ParticleSystem.MainModule main =
            targetParticleSystem.main;

        if (main.simulationSpace == ParticleSystemSimulationSpace.World)
        {
            return targetParticleSystem.transform.position;
        }

        if (main.simulationSpace == ParticleSystemSimulationSpace.Local)
        {
            return Vector3.zero;
        }

        if (main.simulationSpace == ParticleSystemSimulationSpace.Custom)
        {
            if (main.customSimulationSpace != null)
            {
                return main.customSimulationSpace.InverseTransformPoint(
                    targetParticleSystem.transform.position);
            }

            return Vector3.zero;
        }

        return Vector3.zero;
    }

    private Vector3 GetEmitVelocity(
        Vector3 worldVelocity)
    {
        ParticleSystem.MainModule main =
            targetParticleSystem.main;

        if (main.simulationSpace == ParticleSystemSimulationSpace.World)
        {
            return worldVelocity;
        }

        if (main.simulationSpace == ParticleSystemSimulationSpace.Local)
        {
            return targetParticleSystem.transform.InverseTransformDirection(
                worldVelocity);
        }

        if (main.simulationSpace == ParticleSystemSimulationSpace.Custom)
        {
            if (main.customSimulationSpace != null)
            {
                return main.customSimulationSpace.InverseTransformDirection(
                    worldVelocity);
            }

            return worldVelocity;
        }

        return worldVelocity;
    }

    private Vector3 GetHitForward(
        BattleVfxPlayData playData)
    {
        if (playData != null &&
            playData.Context != null &&
            playData.Context.Attacker != null &&
            playData.Context.Target != null)
        {
            Vector3 fromAttackerToTarget =
                playData.Context.Target.transform.position -
                playData.Context.Attacker.transform.position;

            fromAttackerToTarget.y = 0f;

            if (fromAttackerToTarget.sqrMagnitude > 0.0001f)
                return fromAttackerToTarget.normalized;
        }

        return transform.forward;
    }
}