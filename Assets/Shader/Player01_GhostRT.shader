Shader "Custom/Player01_GhostRT"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (0.45, 0.9, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.62
        _FlowColor ("Flow Color", Color) = (0.78, 0.96, 1, 1)
        _FlowIntensity ("Flow Intensity", Range(0, 5)) = 0.8
        _FlowSpeedX ("Flow Speed X", Float) = 0.12
        _FlowSpeedY ("Flow Speed Y", Float) = 0.04
        _FlowWidth ("Flow Width", Range(0.01, 1)) = 0.22
        _FlowSoftness ("Flow Softness", Range(0.01, 1)) = 0.28
        _FlowRepeat ("Flow Repeat", Float) = 1.0
        _FlowOffset ("Flow Offset", Float) = 0.0
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.06
        _ScanlineDensity ("Scanline Density", Float) = 170
        _ScanlineSpeed ("Scanline Speed", Float) = 0.85
        _RGBSplitStrength ("RGB Split Strength", Range(0, 2)) = 0.12
        _JitterStrength ("Jitter Strength", Range(0, 1)) = 0.05
        _JitterSpeed ("Jitter Speed", Float) = 0.65
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
            Name "Player01GhostRT"
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
                float4 _TintColor;
                float _Alpha;
                float4 _FlowColor;
                float _FlowIntensity;
                float _FlowSpeedX;
                float _FlowSpeedY;
                float _FlowWidth;
                float _FlowSoftness;
                float _FlowRepeat;
                float _FlowOffset;
                float _ScanlineIntensity;
                float _ScanlineDensity;
                float _ScanlineSpeed;
                float _RGBSplitStrength;
                float _JitterStrength;
                float _JitterSpeed;
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

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = input.uv;
                float4 baseTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                if (baseTex.a <= 0.0001)
                {
                    return half4(0, 0, 0, 0);
                }

                float time = _Time.y;
                float2 jitterCell = floor(uv * (64.0 + _JitterSpeed * 8.0) + time * _JitterSpeed);
                float jitterA = Hash12(jitterCell);
                float jitterB = Hash12(jitterCell + 17.31);
                float2 jitterOffset = (float2(jitterA, jitterB) - 0.5) * _JitterStrength * 0.003;

                float rgbPhase = sin(time * 0.72) * 0.5 + cos(time * 0.53) * 0.5;
                float2 rgbDir = normalize(float2(0.72, 0.41) + float2(rgbPhase, -rgbPhase) * 0.12);
                float2 rgbOffset = rgbDir * (_RGBSplitStrength * 0.0015);

                float4 texR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + jitterOffset + rgbOffset);
                float4 texG = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + jitterOffset);
                float4 texB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + jitterOffset - rgbOffset);
                float3 sampled = float3(texR.r, texG.g, texB.b);

                float scanPhase = uv.y * _ScanlineDensity + time * _ScanlineSpeed;
                float scanline = (sin(scanPhase * 6.2831853) * 0.5 + 0.5);
                scanline = smoothstep(0.55, 0.96, scanline) * _ScanlineIntensity;

                float2 flowDir = normalize(float2(cos(28.0 * 0.01745329252), sin(28.0 * 0.01745329252)));
                float flowCoord = dot(uv, flowDir) * _FlowRepeat + _FlowOffset + time * (_FlowSpeedX + _FlowSpeedY * 0.5);
                float flowPhase = frac(flowCoord);
                float flowCenterMask = 1.0 - smoothstep(_FlowWidth, _FlowWidth + _FlowSoftness, abs(flowPhase - 0.5));
                float flowBand = flowCenterMask * _FlowIntensity;

                float bodyAlpha = baseTex.a * _Alpha;
                float3 bodyColor = sampled * _TintColor.rgb;
                float3 glowColor = _FlowColor.rgb * (flowBand + scanline * 0.65);
                float3 finalColor = bodyColor + glowColor;
                finalColor *= input.color.rgb;
                bodyAlpha = saturate(bodyAlpha * input.color.a);

                return half4(finalColor, bodyAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
