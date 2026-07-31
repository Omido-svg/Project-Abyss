Shader "Project Abyss/VFX/Procedural Particle Trail"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (1, 0.25, 0.01, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (1, 0.72, 0.06, 1)
        [HDR] _HotColor ("Hot Color", Color) = (1, 0.96, 0.58, 1)
        _Emission ("Emission", Range(0, 16)) = 2.2
        _Opacity ("Opacity", Range(0, 1)) = 0.72
        _EdgePower ("Edge Power", Range(0.1, 12)) = 1.7
        _NoiseScale ("Noise Scale", Range(0.01, 64)) = 5.2
        _NoiseSpeed ("Noise Speed", Range(-16, 16)) = 0.85
        _Breakup ("Breakup", Range(0, 1)) = 0.42
        _Softness ("Softness", Range(0.001, 0.5)) = 0.16
        _PulseSpeed ("Pulse Speed", Range(0, 16)) = 1.2
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.08
        [HideInInspector] _RuntimeTint ("Runtime Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RuntimeIntensity ("Runtime Intensity", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ProceduralTrail"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/VFX/Shaders/Includes/AbyssParticleNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EdgeColor;
                float4 _HotColor;
                float4 _RuntimeTint;
                float _Emission;
                float _Opacity;
                float _EdgePower;
                float _NoiseScale;
                float _NoiseSpeed;
                float _Breakup;
                float _Softness;
                float _PulseSpeed;
                float _PulseAmount;
                float _RuntimeIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS =
                    TransformObjectToWorld(input.positionOS.xyz);

                output.positionCS =
                    TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float across =
                    abs(input.uv.y * 2.0 - 1.0);
                float center =
                    pow(
                        saturate(1.0 - across),
                        max(0.1, _EdgePower));

                float2 noiseUV =
                    float2(
                        input.uv.x * _NoiseScale -
                        _Time.y * _NoiseSpeed,
                        input.uv.y * _NoiseScale +
                        input.positionWS.y * 0.17);

                float noise = AbyssFBM(noiseUV);
                float breakupMask =
                    smoothstep(
                        _Breakup - _Softness,
                        _Breakup + _Softness,
                        noise + center * 0.72);

                float alpha =
                    saturate(
                        input.color.a *
                        _Opacity *
                        center *
                        breakupMask);

                clip(alpha - 0.002);

                float hotMask =
                    pow(center, 3.0);
                float3 color =
                    lerp(
                        _BaseColor.rgb,
                        _EdgeColor.rgb,
                        center);
                color =
                    lerp(
                        color,
                        _HotColor.rgb,
                        hotMask);

                float pulse =
                    1.0 +
                    sin(
                        _Time.y * _PulseSpeed +
                        input.positionWS.y * 1.7) *
                    _PulseAmount;

                color *=
                    input.color.rgb *
                    _RuntimeTint.rgb *
                    _Emission *
                    _RuntimeIntensity *
                    pulse;

                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
