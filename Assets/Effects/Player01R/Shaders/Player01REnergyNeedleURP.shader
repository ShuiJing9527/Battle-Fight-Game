Shader "YY/Player01/R/EnergyNeedleURP"
{
    Properties
    {
        _CoreColor("Core Color", Color) = (0.15, 0.95, 1.00, 1.0)
        _EdgeColor("Edge Color", Color) = (0.65, 0.95, 1.00, 1.0)
        _Opacity("Opacity", Range(0, 1)) = 0.8
        _EmissionIntensity("Emission Intensity", Range(0, 10)) = 2.5
        _FresnelPower("Fresnel Power", Range(0.25, 8)) = 2.5
        _FresnelIntensity("Fresnel Intensity", Range(0, 8)) = 1.5
        _NoiseScale("Noise Scale", Range(0.25, 24)) = 8
        _NoiseSpeed("Noise Speed", Range(-10, 10)) = 2.25
        _NoiseContrast("Noise Contrast", Range(0, 4)) = 1.35
        _TailFadeStart("Tail Fade Start", Range(0, 1)) = 0.16
        _TailFadeLength("Tail Fade Length", Range(0.05, 1)) = 0.68
        _TailFadePower("Tail Fade Power", Range(0.25, 8)) = 1.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _EdgeColor;
                half _Opacity;
                half _EmissionIntensity;
                half _FresnelPower;
                half _FresnelIntensity;
                half _NoiseScale;
                half _NoiseSpeed;
                half _NoiseContrast;
                half _TailFadeStart;
                half _TailFadeLength;
                half _TailFadePower;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 flowUv = float2(input.uv.x * _NoiseScale, input.uv.y * _NoiseScale * 0.28 + _Time.y * _NoiseSpeed);
                float noise = Noise2D(flowUv);
                noise = saturate(pow(noise * 1.18, max(0.01, _NoiseContrast)));

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDirWS)), max(0.01, _FresnelPower));

                float tailRamp = saturate((input.uv.y - _TailFadeStart) / max(0.0001, _TailFadeLength));
                float tail = smoothstep(0.0, 1.0, tailRamp);
                tail = pow(tail, max(0.01, _TailFadePower));

                half3 coreColor = _CoreColor.rgb;
                half3 edgeColor = _EdgeColor.rgb;
                half3 flowColor = lerp(coreColor, edgeColor, saturate(fresnel * 0.72 + noise * 0.28));
                half emission = _EmissionIntensity * (0.7h + noise * 0.22h) + fresnel * _FresnelIntensity;

                half alpha = _Opacity * tail * saturate(0.84h + noise * 0.18h + fresnel * 0.14h);
                half3 finalColor = flowColor * emission;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
