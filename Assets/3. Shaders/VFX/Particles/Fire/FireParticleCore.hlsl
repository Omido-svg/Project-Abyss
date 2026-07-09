#ifndef PROJECT_ABYSS_FIRE_PARTICLE_CORE_INCLUDED
#define PROJECT_ABYSS_FIRE_PARTICLE_CORE_INCLUDED

float FireHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float FireValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);

    float a = FireHash21(i);
    float b = FireHash21(i + float2(1.0, 0.0));
    float c = FireHash21(i + float2(0.0, 1.0));
    float d = FireHash21(i + float2(1.0, 1.0));

    float2 u = f * f * (3.0 - 2.0 * f);

    return lerp(
        lerp(a, b, u.x),
        lerp(c, d, u.x),
        u.y);
}

float FireFbm(float2 p)
{
    float value = 0.0;
    float amplitude = 0.5;

    value += FireValueNoise(p) * amplitude;
    p *= 2.03;
    amplitude *= 0.5;

    value += FireValueNoise(p) * amplitude;
    p *= 2.01;
    amplitude *= 0.5;

    value += FireValueNoise(p) * amplitude;

    return value;
}

float4 FireParticleFragCore(
    float2 uv01,
    float4 vertexColor,
    float4 tintColor,
    float4 hotColor,
    float time,
    float noiseScale,
    float noiseSpeed,
    float corePower,
    float edgeSoftness,
    float distortion,
    float alphaPower,
    float intensity)
{
    float2 uv =
        uv01 * 2.0 - 1.0;

    float dist =
        length(uv);

    float2 noiseUv =
        uv * noiseScale +
        float2(
            time * 0.25,
            -time * noiseSpeed);

    float noise =
        FireFbm(noiseUv);

    float distortedDist =
        dist +
        (noise - 0.5) * distortion;

    float radial =
        1.0 - smoothstep(
            0.15,
            1.0,
            distortedDist);

    float core =
        1.0 - smoothstep(
            0.0,
            0.45,
            distortedDist);

    core =
        pow(
            saturate(core),
            corePower);

    float vertical =
        saturate(uv01.y);

    float upwardFade =
        1.0 - smoothstep(
            0.65,
            1.05,
            vertical + (noise - 0.5) * 0.25);

    float bottomFade =
        smoothstep(
            0.0,
            0.18,
            vertical);

    float flameMask =
        radial *
        upwardFade *
        bottomFade;

    float edge =
        1.0 - smoothstep(
            1.0 - edgeSoftness,
            1.0,
            dist);

    float alpha =
        flameMask *
        edge *
        vertexColor.a *
        tintColor.a;

    alpha =
        pow(
            saturate(alpha),
            alphaPower);

    alpha *=
        intensity;

    float hotAmount =
        saturate(
            core * 1.3 +
            noise * 0.35);

    float3 color =
        lerp(
            tintColor.rgb,
            hotColor.rgb,
            hotAmount);

    color *=
        vertexColor.rgb;

    color *=
        intensity;

    return float4(
        color,
        alpha);
}

#endif