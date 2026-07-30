Shader "ProjectAbyss/VFX/Procedural3D/EnergyURP"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,0.65,0.18,0.45)
        _EdgeColor ("Edge Color", Color) = (1,0.95,0.7,1)
        _Emission ("Emission", Float) = 4.5
        _PulseSpeed ("Pulse Speed", Float) = 2
        _PulseAmount ("Pulse Amount", Float) = 0.08
        _NoiseFrequency ("Noise Frequency", Float) = 4
        _NoiseAmplitude ("Noise Amplitude", Float) = 0.04
        _FlowSpeed ("Flow Speed", Float) = 0.5
        _Alpha ("Alpha", Range(0,1)) = 0.35
        _VerticalFade ("Vertical Fade", Range(0,1)) = 1
        _FresnelPower ("Fresnel Power", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                float _Emission;
                float _PulseSpeed;
                float _PulseAmount;
                float _NoiseFrequency;
                float _NoiseAmplitude;
                float _FlowSpeed;
                float _Alpha;
                float _VerticalFade;
                float _FresnelPower;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionOS : TEXCOORD2; };
            float hash31(float3 p){ p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
            float noise3(float3 p)
            {
                float3 i = floor(p); float3 f = frac(p); f = f * f * (3.0 - 2.0 * f);
                float n000 = hash31(i + float3(0,0,0)); float n100 = hash31(i + float3(1,0,0));
                float n010 = hash31(i + float3(0,1,0)); float n110 = hash31(i + float3(1,1,0));
                float n001 = hash31(i + float3(0,0,1)); float n101 = hash31(i + float3(1,0,1));
                float n011 = hash31(i + float3(0,1,1)); float n111 = hash31(i + float3(1,1,1));
                float nx00 = lerp(n000, n100, f.x); float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x); float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y); float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }
            Varyings vert(Attributes IN)
            {
                Varyings OUT; float t = _Time.y; float pulse = 1.0 + sin(t * _PulseSpeed) * _PulseAmount;
                float n = noise3(IN.positionOS.xyz * _NoiseFrequency + float3(0, t * _FlowSpeed, 0));
                float disp = (n - 0.5) * 2.0 * _NoiseAmplitude;
                float3 positionOS = IN.positionOS.xyz * pulse + normalize(IN.normalOS) * disp;
                float4 positionWS = mul(GetObjectToWorldMatrix(), float4(positionOS, 1.0));
                OUT.positionHCS = TransformWorldToHClip(positionWS.xyz); OUT.positionWS = positionWS.xyz;
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS)); OUT.positionOS = positionOS; return OUT;
            }
            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDirWS = SafeNormalize(GetCameraPositionWS() - IN.positionWS);
                float fresnel = pow(saturate(1.0 - dot(IN.normalWS, viewDirWS)), _FresnelPower);
                float t = _Time.y; float n = noise3(IN.positionOS * _NoiseFrequency + float3(0, t * _FlowSpeed, 0));
                float pulse = 0.5 + 0.5 * sin(t * _PulseSpeed + IN.positionOS.y * 3.5);
                float vertical = _VerticalFade > 0.5 ? saturate((IN.positionOS.y + 0.7) * 0.9) : 1.0;
                float coreMask = saturate(n * 0.65 + pulse * 0.35);
                float alpha = saturate((_Alpha * coreMask + fresnel * 0.55) * vertical);
                float3 color = lerp(_BaseColor.rgb, _EdgeColor.rgb, saturate(fresnel * 0.8 + pulse * 0.2));
                color *= _Emission * (0.45 + coreMask * 0.55 + fresnel * 0.75);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
