Shader "Project Abyss/VFX/Shin Surface Aura"
{
    Properties
    {
        [HDR] _BaseColor ("Inner Gold", Color) = (1.0, 0.18, 0.015, 1.0)
        [HDR] _EdgeColor ("Outer Gold", Color) = (1.0, 0.78, 0.12, 1.0)
        _Emission ("Emission", Range(0.0, 12.0)) = 4.5
        _Opacity ("Opacity", Range(0.0, 1.0)) = 0.72
        _FresnelPower ("Fresnel Power", Range(0.25, 8.0)) = 2.2
        _ShellWidth ("Shell Width", Range(0.0, 0.20)) = 0.02
        _NoiseScale ("Noise Scale", Range(0.1, 20.0)) = 4.6
        _NoiseSpeed ("Noise Rise Speed", Range(-5.0, 5.0)) = 0.85
        _FlameStretch ("Vertical Flame Stretch", Range(0.1, 8.0)) = 2.4
        _Cutoff ("Outer Flame Cutoff", Range(0.0, 1.0)) = 0.46
        _PulseSpeed ("Pulse Speed", Range(0.0, 8.0)) = 1.25
        _PulseAmount ("Pulse Amount", Range(0.0, 1.0)) = 0.16
        _OuterMode ("Outer Flame Mode", Range(0.0, 1.0)) = 0.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent+20"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "ShinSurfaceAura"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EdgeColor;
                float _Emission;
                float _Opacity;
                float _FresnelPower;
                float _ShellWidth;
                float _NoiseScale;
                float _NoiseSpeed;
                float _FlameStretch;
                float _Cutoff;
                float _PulseSpeed;
                float _PulseAmount;
                float _OuterMode;
                float _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

            float Fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                [unroll] for (int octave = 0; octave < 5; octave++)
                {
                    value += ValueNoise(p) * amplitude;
                    p = p * 2.03 + 17.17;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 local = input.positionOS.xyz;
                float3 flowPosition = local * (_NoiseScale * 0.55);
                flowPosition.y = flowPosition.y / max(0.1, _FlameStretch) - _Time.y * _NoiseSpeed;
                float noise = Fbm(flowPosition + float3(4.7, 1.3, 8.1));
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed + local.y * 6.0) * _PulseAmount;
                float widthVariation = lerp(1.0, lerp(0.62, 1.38, noise), saturate(_OuterMode));
                local += normalize(input.normalOS) * (_ShellWidth * pulse * widthVariation);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(local);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                float fresnel = pow(saturate(1.0 - abs(dot(normalWS, viewDirWS))), _FresnelPower);

                float3 p = input.positionOS * _NoiseScale;
                p.y = p.y / max(0.1, _FlameStretch) - _Time.y * _NoiseSpeed;
                float coarse = Fbm(p);
                float detail = Fbm(p * 2.35 + float3(19.1, 3.7, 6.3));
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed + coarse * 7.0) * _PulseAmount;

                float innerGlow = saturate(0.12 + fresnel * 1.05 + coarse * 0.20);
                float flame = coarse * 0.72 + detail * 0.28 + fresnel * 0.42;
                float outerMask = smoothstep(_Cutoff, 1.0, flame);
                if (_OuterMode > 0.5)
                    clip(flame - _Cutoff);

                float mask = lerp(innerGlow, outerMask, saturate(_OuterMode));
                float3 color = lerp(_BaseColor.rgb, _EdgeColor.rgb,
                    saturate(fresnel * 0.85 + coarse * 0.22 + _OuterMode * 0.28));
                return half4(color * (_Emission * mask * pulse * _Opacity), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
