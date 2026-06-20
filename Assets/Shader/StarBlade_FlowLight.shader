Shader "Custom/StarBlade_FlowLight"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 1

        _FlowColor ("Flow Light Color", Color) = (0.82, 0.97, 1, 1)
        _FlowIntensity ("Flow Intensity", Range(0, 5)) = 1.5
        _FlowWidth ("Flow Width", Range(0.01, 1)) = 0.18
        _FlowSoftness ("Flow Softness", Range(0.01, 1)) = 0.25
        _FlowSpeed ("Flow Speed", Float) = 0.8
        _FlowAngle ("Flow Angle", Range(0, 360)) = 25
        _FlowRepeat ("Flow Repeat", Float) = 1
        _FlowOffset ("Flow Offset", Float) = 0

        _InnerGlowColor ("Inner Glow Color", Color) = (0.92, 0.98, 1, 1)
        _InnerGlowIntensity ("Inner Glow Intensity", Range(0, 3)) = 0.4
        _EdgeLightIntensity ("Edge Light Intensity", Range(0, 3)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "StarBladeFlowLight"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _BaseColor;
                float _BaseAlpha;
                float4 _FlowColor;
                float _FlowIntensity;
                float _FlowWidth;
                float _FlowSoftness;
                float _FlowSpeed;
                float _FlowAngle;
                float _FlowRepeat;
                float _FlowOffset;
                float4 _InnerGlowColor;
                float _InnerGlowIntensity;
                float _EdgeLightIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            float SmoothBand(float x, float width, float softness)
            {
                width = max(width, 0.0001);
                softness = max(softness, 0.0001);
                float dist = abs(x);
                float inner = smoothstep(width, max(width + softness, width + 0.0001), dist);
                return saturate(1.0 - inner);
            }

            float SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = input.uv;
                float4 baseTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float mainAlpha = baseTex.a;
                if (mainAlpha <= 0.0001)
                {
                    return half4(0, 0, 0, 0);
                }

                float2 dir = float2(cos(radians(_FlowAngle)), sin(radians(_FlowAngle)));
                dir = normalize(dir);
                float flowCoord = dot(uv - 0.5, dir) * max(_FlowRepeat, 0.0001);
                float flowPhase = frac(flowCoord - _Time.y * _FlowSpeed + _FlowOffset);
                float flowDist = abs(flowPhase - 0.5);
                float flowMask = SmoothBand(flowDist, _FlowWidth, _FlowSoftness) * mainAlpha;

                float2 texel = _MainTex_TexelSize.xy;
                float alphaL = SampleAlpha(uv - float2(texel.x, 0));
                float alphaR = SampleAlpha(uv + float2(texel.x, 0));
                float alphaD = SampleAlpha(uv - float2(0, texel.y));
                float alphaU = SampleAlpha(uv + float2(0, texel.y));
                float neighborMin = min(min(alphaL, alphaR), min(alphaD, alphaU));
                float innerEdgeMask = saturate((mainAlpha - neighborMin) * 12.0);
                innerEdgeMask = smoothstep(0.05, 0.9, innerEdgeMask) * mainAlpha;

                float3 bodyColor = baseTex.rgb * _BaseColor.rgb;
                float3 flowGlow = _FlowColor.rgb * (flowMask * _FlowIntensity);
                float3 innerGlow = _InnerGlowColor.rgb * (innerEdgeMask * _InnerGlowIntensity);
                float3 edgeLight = _FlowColor.rgb * (innerEdgeMask * _EdgeLightIntensity * 0.35);

                float3 finalColor = bodyColor + flowGlow + innerGlow + edgeLight;
                float finalAlpha = saturate(mainAlpha * _BaseAlpha * input.color.a);
                finalColor *= input.color.rgb;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
