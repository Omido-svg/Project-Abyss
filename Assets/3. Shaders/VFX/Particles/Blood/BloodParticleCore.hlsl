#ifndef PROJECT_ABYSS_BLOOD_PARTICLE_CORE_INCLUDED
#define PROJECT_ABYSS_BLOOD_PARTICLE_CORE_INCLUDED

float BloodHash21(float2 p)
{
    p = frac(p * float2(127.1, 311.7));
    p += dot(p, p + 74.7);
    return frac(p.x * p.y);
}

float BloodValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);

    float a = BloodHash21(i);
    float b = BloodHash21(i + float2(1.0, 0.0));
    float c = BloodHash21(i + float2(0.0, 1.0));
    float d = BloodHash21(i + float2(1.0, 1.0));

    float2 u = f * f * (3.0 - 2.0 * f);

    return lerp(
        lerp(a, b, u.x),
        lerp(c, d, u.x),
        u.y);
}

float BloodFbm(float2 p)
{
    float value = 0.0;
    float amplitude = 0.5;

    value += BloodValueNoise(p) * amplitude;
    p *= 2.17;
    amplitude *= 0.5;

    value += BloodValueNoise(p) * amplitude;
    p *= 2.11;
    amplitude *= 0.5;

    value += BloodValueNoise(p) * amplitude;

    return value;
}

float4 BloodParticleFragCore(
    float2 uv01,
    float4 vertexColor,
    float4 tintColor,
    float4 darkColor,
    float time,
    float noiseScale,
    float noiseAmount,
    float edgeSharpness,
    float alphaPower,
    float intensity)
{
    float2 uv =
        uv01 * 2.0 - 1.0;

    float dist =
        length(uv);

    float noise =
        BloodFbm(
            uv * noiseScale +
            float2(time * 0.2, time * 0.15));

    float distortedDist =
        dist +
        (noise - 0.5) * noiseAmount;

    float splatMask =
        1.0 - smoothstep(
            0.35,
            1.0,
            distortedDist);

    splatMask =
        pow(
            saturate(splatMask),
            edgeSharpness);

    float center =
        1.0 - smoothstep(
            0.0,
            0.45,
            dist);

    float grain =
        smoothstep(
            0.2,
            0.95,
            noise);

    float alpha =
        splatMask *
        lerp(0.75, 1.25, grain) *
        vertexColor.a *
        tintColor.a;

    alpha =
        pow(
            saturate(alpha),
            alphaPower);

    alpha *=
        intensity;

    float darkAmount =
        saturate(
            center * 0.55 +
            (1.0 - noise) * 0.25);

    float3 color =
        lerp(
            tintColor.rgb,
            darkColor.rgb,
            darkAmount);

    color *=
        vertexColor.rgb;

    return float4(
        color,
        alpha);
}

#endif