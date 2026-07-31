#ifndef PROJECT_ABYSS_PARTICLE_NOISE_INCLUDED
#define PROJECT_ABYSS_PARTICLE_NOISE_INCLUDED

float AbyssHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float AbyssValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = AbyssHash21(i);
    float b = AbyssHash21(i + float2(1.0, 0.0));
    float c = AbyssHash21(i + float2(0.0, 1.0));
    float d = AbyssHash21(i + float2(1.0, 1.0));

    return lerp(
        lerp(a, b, u.x),
        lerp(c, d, u.x),
        u.y);
}

float AbyssFBM(float2 p)
{
    float value = 0.0;
    float amplitude = 0.5;

    [unroll]
    for (int octave = 0; octave < 4; octave++)
    {
        value += AbyssValueNoise(p) * amplitude;
        p = p * 2.03 + float2(17.17, 9.23);
        amplitude *= 0.5;
    }

    return value;
}

#endif
