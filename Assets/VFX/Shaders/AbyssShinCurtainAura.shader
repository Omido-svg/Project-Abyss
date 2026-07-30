Shader "Project Abyss/VFX/Shin Curtain Aura"
{
    Properties
    {
        [HDR] _BaseColor ("Deep Amber", Color) = (1.0, 0.16, 0.006, 1.0)
        [HDR] _EdgeColor ("Golden Flame", Color) = (1.0, 0.62, 0.045, 1.0)
        [HDR] _HotColor ("Hot Yellow", Color) = (1.0, 0.98, 0.56, 1.0)
        _Emission ("Emission", Range(0.0, 24.0)) = 7.0
        _Opacity ("Opacity", Range(0.0, 1.0)) = 0.72
        _NoiseScale ("Coarse Noise Scale", Range(0.1, 20.0)) = 1.25
        _DetailNoiseScale ("Detail Noise Scale", Range(0.1, 30.0)) = 3.8
        _NoiseSpeed ("Vertical Flow Speed", Range(-5.0, 5.0)) = 0.48
        _FlameStretch ("Vertical Noise Stretch", Range(0.1, 8.0)) = 3.2
        _Cutoff ("Shape Cutoff", Range(0.0, 1.0)) = 0.34
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.35)) = 0.09
        _PulseSpeed ("Pulse Speed", Range(0.0, 8.0)) = 0.62
        _PulseAmount ("Pulse Amount", Range(0.0, 1.0)) = 0.06
        _LateralWobble ("Vertex Side Wobble", Range(0.0, 0.25)) = 0.035
        _DepthWobble ("Vertex Depth Wobble", Range(0.0, 0.25)) = 0.025
        _LobeSeparation ("Side Lobe Separation", Range(0.0, 0.9)) = 0.38
        _LobeWidth ("Flame Lobe Width", Range(0.05, 1.2)) = 0.48
        _TipVariation ("Tip Height Variation", Range(0.0, 0.6)) = 0.20
        _SideFade ("Side Fade", Range(0.01, 0.8)) = 0.18
        _BottomFade ("Bottom Fade", Range(0.001, 0.5)) = 0.07
        _LayerMode ("Layer Mode", Range(0.0, 1.0)) = 0.0
        _PhaseOffset ("Phase Offset", Float) = 0.0
        _IntensityScale ("Intensity Scale", Range(0.0, 2.0)) = 1.0
        _LayerIndex ("Layer Index", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent+3"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "ShinCurtainAura"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/VFX/Shaders/Includes/AbyssProceduralNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EdgeColor;
                float4 _HotColor;
                float _Emission;
                float _Opacity;
                float _NoiseScale;
                float _DetailNoiseScale;
                float _NoiseSpeed;
                float _FlameStretch;
                float _Cutoff;
                float _EdgeSoftness;
                float _PulseSpeed;
                float _PulseAmount;
                float _LateralWobble;
                float _DepthWobble;
                float _LobeSeparation;
                float _LobeWidth;
                float _TipVariation;
                float _SideFade;
                float _BottomFade;
                float _LayerMode;
                float _PhaseOffset;
                float _IntensityScale;
                float _LayerIndex;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 localPosition : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float GaussianLobe(float x, float center, float width)
            {
                float q = (x - center) / max(width, 0.001);
                return exp2(-2.35 * q * q);
            }

            float BuildThreeLobes(float x, float y, float timeValue, float phase)
            {
                float taper = lerp(1.10, 0.53, saturate(y));
                float width = max(0.025, _LobeWidth * taper);
                float slow = timeValue * 0.44;
                float center = sin(slow + phase * 0.73) * 0.055;
                float leftCenter = -_LobeSeparation + sin(slow * 0.83 + phase + 1.91) * 0.045;
                float rightCenter = _LobeSeparation + sin(slow * 0.91 + phase + 4.27) * 0.045;

                float middle = GaussianLobe(x, center, width * 1.10);
                float left = GaussianLobe(x, leftCenter, width * 0.78);
                float right = GaussianLobe(x, rightCenter, width * 0.78);
                return saturate(max(middle, max(left, right)));
            }

            float3 BuildFlowPosition(float x, float y, float scale, float phase)
            {
                return float3(
                    x * scale + phase * 0.37,
                    y * scale * _FlameStretch - _Time.y * _NoiseSpeed + phase,
                    phase * 0.61 + _LayerIndex * 1.13);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float2 uv = input.uv;
                float x = uv.x * 2.0 - 1.0;
                float y = saturate(uv.y);
                float phase = _PhaseOffset + _LayerIndex * 0.83;
                float3 flowPosition = BuildFlowPosition(x, y, _NoiseScale * 0.78, phase);
                float coarse = AbyssFbm3D(flowPosition);
                float detail = AbyssValueNoise3D(flowPosition * 2.17 + float3(7.1, 3.9, 11.7));

                float4 positionOS = input.positionOS;
                float upperWeight = smoothstep(0.08, 1.0, y);
                float sideWave = sin(y * 8.4 - _Time.y * (_NoiseSpeed * 2.3 + 0.35) + phase * 2.1);
                float randomWave = (coarse * 2.0 - 1.0) * 0.68 + (detail * 2.0 - 1.0) * 0.32;
                positionOS.x += (sideWave * 0.35 + randomWave * 0.65) * _LateralWobble * upperWeight;
                positionOS.z += randomWave * _DepthWobble * upperWeight;
                positionOS.y += (coarse - 0.5) * 0.025 * upperWeight;

                output.positionCS = TransformObjectToHClip(positionOS.xyz);
                output.uv = uv;
                output.localPosition = positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float x = input.uv.x * 2.0 - 1.0;
                float y = saturate(input.uv.y);
                float phase = _PhaseOffset + _LayerIndex * 0.83;
                float timeValue = _Time.y * max(abs(_NoiseSpeed), 0.05);
                float lobes = BuildThreeLobes(x, y, timeValue, phase);

                float3 coarsePosition = BuildFlowPosition(x, y, _NoiseScale, phase);
                float3 detailPosition = BuildFlowPosition(x, y, _DetailNoiseScale, phase + 9.31);
                float coarse = AbyssFbm3D(coarsePosition);
                float detail = AbyssFbm3D(detailPosition);
                float ridge = AbyssRidgedFbm3D(detailPosition * 0.73 + float3(4.3, 8.9, 2.1));

                float tipNoise = AbyssFbm3D(float3(
                    x * 2.7 + phase,
                    -_Time.y * 0.27 + phase * 0.41,
                    _LayerIndex * 1.7 + 5.2));
                float tipHeight = saturate(0.76 + lobes * 0.18 + (tipNoise - 0.5) * _TipVariation * 2.0);
                float tipMask = 1.0 - smoothstep(tipHeight, tipHeight + max(_EdgeSoftness, 0.01), y);
                float bottomMask = smoothstep(0.0, max(_BottomFade, 0.001), y);
                float sideMask = 1.0 - smoothstep(1.0 - max(_SideFade, 0.01), 1.0, abs(x));

                // The broad body stays persistent; noise mainly breaks the edge and makes vertical streaks.
                float body = lobes * bottomMask * tipMask * sideMask;
                float movingBody = body * lerp(0.72, 1.18, coarse);
                float verticalStreak = pow(saturate(ridge * 0.88 + detail * 0.28), 2.1) * body;

                float wideField = movingBody + verticalStreak * 0.22;
                float coreField = body * (0.28 + verticalStreak * 1.12 + ridge * 0.28);
                float field = lerp(wideField, coreField, saturate(_LayerMode));
                float mask = smoothstep(
                    _Cutoff - max(_EdgeSoftness, 0.001),
                    _Cutoff + max(_EdgeSoftness, 0.001),
                    field);

                float innerHot = saturate(verticalStreak * 1.15 + mask * 0.22);
                float edgeGold = saturate(mask * 0.72 + ridge * 0.36);
                float3 color = lerp(_BaseColor.rgb, _EdgeColor.rgb, edgeGold);
                color = lerp(color, _HotColor.rgb, innerHot);

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed + phase + coarse * 4.0) * _PulseAmount;
                float alpha = saturate(mask * _Opacity * _IntensityScale);
                float emission = _Emission * pulse * _IntensityScale;
                return half4(color * emission, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
