#ifndef PROJECT_ABYSS_PROCEDURAL_NOISE_INCLUDED
#define PROJECT_ABYSS_PROCEDURAL_NOISE_INCLUDED

float AbyssHash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float AbyssValueNoise3D(float3 p)
{
    float3 cell = floor(p);
    float3 local = frac(p);
    local = local * local * (3.0 - 2.0 * local);

    float n000 = AbyssHash31(cell + float3(0, 0, 0));
    float n100 = AbyssHash31(cell + float3(1, 0, 0));
    float n010 = AbyssHash31(cell + float3(0, 1, 0));
    float n110 = AbyssHash31(cell + float3(1, 1, 0));
    float n001 = AbyssHash31(cell + float3(0, 0, 1));
    float n101 = AbyssHash31(cell + float3(1, 0, 1));
    float n011 = AbyssHash31(cell + float3(0, 1, 1));
    float n111 = AbyssHash31(cell + float3(1, 1, 1));

    float nx00 = lerp(n000, n100, local.x);
    float nx10 = lerp(n010, n110, local.x);
    float nx01 = lerp(n001, n101, local.x);
    float nx11 = lerp(n011, n111, local.x);
    return lerp(lerp(nx00, nx10, local.y), lerp(nx01, nx11, local.y), local.z);
}

float AbyssFbm3D(float3 p)
{
    float value = 0.0;
    float amplitude = 0.5;
    [unroll] for (int octave = 0; octave < 5; octave++)
    {
        value += AbyssValueNoise3D(p) * amplitude;
        p = p * 2.03 + float3(17.17, 9.31, 13.73);
        amplitude *= 0.5;
    }
    return value;
}

float AbyssRidgedFbm3D(float3 p)
{
    float value = 0.0;
    float amplitude = 0.55;
    [unroll] for (int octave = 0; octave < 4; octave++)
    {
        float noise = AbyssValueNoise3D(p);
        float ridge = 1.0 - abs(noise * 2.0 - 1.0);
        value += ridge * ridge * amplitude;
        p = p * 2.11 + float3(7.93, 15.17, 4.37);
        amplitude *= 0.52;
    }
    return saturate(value);
}

#endif
