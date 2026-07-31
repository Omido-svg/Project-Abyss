Shader "Project Abyss/VFX/Procedural Screen Space Shin"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ProceduralScreenSpaceShin"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _AbyssAuraEnabled;
            float4 _AbyssAuraCenterUV;
            float4 _AbyssAuraSizeUV;
            float _AbyssAuraEyeDepth;
            float _AbyssAuraDepthBias;

            float4 _AbyssAuraBaseColor;
            float4 _AbyssAuraEdgeColor;
            float4 _AbyssAuraHotColor;
            float4 _AbyssAuraRuntimeTint;
            float _AbyssAuraIntensity;
            float _AbyssAuraOpacity;

            float _AbyssAuraLobeSpread;
            float _AbyssAuraLobeWidth;
            float4 _AbyssAuraLobeHeights;

            float _AbyssAuraNoiseScale;
            float _AbyssAuraDetailNoiseScale;
            float _AbyssAuraNoiseSpeed;
            float _AbyssAuraWarpStrength;
            float _AbyssAuraSwayStrength;
            float _AbyssAuraTipVariation;
            float _AbyssAuraCutoff;
            float _AbyssAuraSoftness;
            float _AbyssAuraCoreWidth;
            float4 _AbyssAuraPulse;
            float _AbyssAuraTimeScale;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float2 shift = float2(37.1, 17.7);

                [unroll]
                for (int octave = 0; octave < 5; octave++)
                {
                    value += ValueNoise(p) * amplitude;
                    p = p * 2.03 + shift;
                    amplitude *= 0.5;
                }

                return value;
            }

            float FlameLobe(
                float2 p,
                float center,
                float width,
                float height,
                float phase,
                float time,
                float flowNoise,
                out float signedCore)
            {
                float y = saturate(p.y);
                float rise = smoothstep(0.0, 0.09, y);
                float taper = pow(saturate(1.0 - y), 0.72);

                float sway = sin(
                    y * 8.5 +
                    time * (1.05 + phase * 0.07) +
                    phase) *
                    _AbyssAuraSwayStrength *
                    (0.15 + y * 0.85);

                float lowFrequencyWarp =
                    (Fbm(float2(
                        y * 2.4 + phase * 0.31,
                        time * 0.19 + phase)) - 0.5) *
                    _AbyssAuraWarpStrength *
                    (0.25 + y * 0.75);

                float x = p.x - center - sway - lowFrequencyWarp;
                float localWidth = max(
                    0.012,
                    width * (0.18 + taper * 0.82));

                float distanceToCenter = abs(x) / localWidth;
                signedCore = 1.0 - distanceToCenter;

                float horizontal = 1.0 - smoothstep(
                    1.0 - _AbyssAuraSoftness,
                    1.0 + _AbyssAuraSoftness,
                    distanceToCenter);

                float tipNoise =
                    (Fbm(float2(
                        center * 9.7 + phase,
                        time * 0.31 + p.x * 1.8)) - 0.5) * 2.0;
                tipNoise += (flowNoise - 0.5) * 0.85;

                float dynamicHeight =
                    height +
                    tipNoise * _AbyssAuraTipVariation;

                float vertical = 1.0 - smoothstep(
                    dynamicHeight - _AbyssAuraSoftness * 1.8,
                    dynamicHeight + _AbyssAuraSoftness * 1.8,
                    y);

                return horizontal * vertical * rise;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                half4 source = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv,
                    _BlitMipLevel);

                if (_AbyssAuraEnabled < 0.5)
                    return source;

                float2 safeSize = max(_AbyssAuraSizeUV.xy, float2(0.0001, 0.0001));
                float2 normalized = (uv - _AbyssAuraCenterUV.xy) / safeSize;

                // x: -1..1, y: 0..1 inside the target envelope.
                float2 p = float2(normalized.x * 2.0, normalized.y + 0.5);

                float envelope =
                    step(0.0, p.y) *
                    step(p.y, 1.08) *
                    step(abs(p.x), 1.25);

                if (envelope <= 0.0)
                    return source;

                float time = _Time.y * _AbyssAuraTimeScale;
                float2 flowingCoordinates = float2(
                    p.x * _AbyssAuraNoiseScale,
                    p.y * _AbyssAuraNoiseScale * 1.75 -
                    time * _AbyssAuraNoiseSpeed);
                float flowNoise = Fbm(flowingCoordinates);
                float detailNoise = Fbm(float2(
                    p.x * _AbyssAuraDetailNoiseScale + 13.7,
                    p.y * _AbyssAuraDetailNoiseScale * 1.45 -
                    time * _AbyssAuraNoiseSpeed * 1.73));

                float spread = _AbyssAuraLobeSpread;
                float width = _AbyssAuraLobeWidth;
                float coreSigned;
                float coreMaximum = -1.0;

                float mask = FlameLobe(
                    p, -spread * 3.0, width * 0.72,
                    _AbyssAuraLobeHeights.w, 0.7,
                    time, flowNoise, coreSigned);
                coreMaximum = max(coreMaximum, coreSigned);

                float lobe = FlameLobe(
                    p, -spread * 2.0, width * 0.86,
                    _AbyssAuraLobeHeights.z, 1.9,
                    time, flowNoise, coreSigned);
                mask = max(mask, lobe);
                coreMaximum = max(coreMaximum, coreSigned);

                lobe = FlameLobe(
                    p, -spread, width,
                    _AbyssAuraLobeHeights.y, 3.1,
                    time, flowNoise, coreSigned);
                mask = max(mask, lobe);
                coreMaximum = max(coreMaximum, coreSigned);

                lobe = FlameLobe(
                    p, 0.0, width * 1.12,
                    _AbyssAuraLobeHeights.x, 4.3,
                    time, flowNoise, coreSigned);
                mask = max(mask, lobe);
                coreMaximum = max(coreMaximum, coreSigned);

                lobe = FlameLobe(
                    p, spread, width,
                    _AbyssAuraLobeHeights.y, 5.5,
                    time, flowNoise, coreSigned);
                mask = max(mask, lobe);
                coreMaximum = max(coreMaximum, coreSigned);

                lobe = FlameLobe(
                    p, spread * 2.0, width * 0.86,
                    _AbyssAuraLobeHeights.z, 6.7,
                    time, flowNoise, coreSigned);
                mask = max(mask, lobe);
                coreMaximum = max(coreMaximum, coreSigned);

                lobe = FlameLobe(
                    p, spread * 3.0, width * 0.72,
                    _AbyssAuraLobeHeights.w, 7.9,
                    time, flowNoise, coreSigned);
                mask = max(mask, lobe);
                coreMaximum = max(coreMaximum, coreSigned);

                // Animated erosion: large noise preserves the broad silhouette,
                // detail noise cuts moving holes and separates the vertical tongues.
                float erosionSignal =
                    mask * 0.92 +
                    (flowNoise - 0.5) * 0.42 +
                    (detailNoise - 0.5) * 0.20;

                float alphaMask = smoothstep(
                    _AbyssAuraCutoff - _AbyssAuraSoftness,
                    _AbyssAuraCutoff + _AbyssAuraSoftness,
                    erosionSignal);
                alphaMask *= mask * envelope;

                float core = smoothstep(
                    _AbyssAuraCoreWidth,
                    1.0,
                    saturate(coreMaximum));
                core *= alphaMask;

                float edgeBand = saturate(alphaMask - core * 0.42);
                float heat = saturate(
                    core * 0.85 +
                    flowNoise * 0.35 +
                    detailNoise * 0.16);

                float3 color = lerp(
                    _AbyssAuraBaseColor.rgb,
                    _AbyssAuraEdgeColor.rgb,
                    saturate(edgeBand + flowNoise * 0.25));
                color = lerp(
                    color,
                    _AbyssAuraHotColor.rgb,
                    saturate(core * 0.92 + heat * 0.18));
                color *= _AbyssAuraRuntimeTint.rgb;

                float pulse = 1.0 +
                    sin(time * _AbyssAuraPulse.x) *
                    _AbyssAuraPulse.y;

                float rawDepth = SampleSceneDepth(uv);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float visibleBehindCharacter = step(
                    _AbyssAuraEyeDepth + _AbyssAuraDepthBias,
                    sceneEyeDepth);

                float alpha =
                    alphaMask *
                    _AbyssAuraOpacity *
                    visibleBehindCharacter;
                float3 emission =
                    color *
                    alpha *
                    _AbyssAuraIntensity *
                    pulse;

                // Screen-space additive composite. No authored VFX texture,
                // particle, billboard, or generated project mesh is used.
                return half4(source.rgb + emission, source.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
