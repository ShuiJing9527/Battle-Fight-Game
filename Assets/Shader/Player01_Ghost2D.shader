Shader "Custom/Player01_Ghost2D"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _BodyTintColor ("Body Tint Color", Color) = (0.35, 0.85, 1, 1)
        _BodyAlpha ("Body Alpha", Range(0, 1)) = 0.35
        _FlowColor ("Flow Color", Color) = (0.82, 0.97, 1, 1)
        _FlowIntensity ("Flow Intensity", Range(0, 5)) = 1.35
        _FlowSpeedX ("Flow Speed X", Float) = 0.35
        _FlowSpeedY ("Flow Speed Y", Float) = 0.08
        _FlowWidth ("Flow Width", Range(0.01, 1)) = 0.22
        _ScanPatternTex ("Scan / Flow Pattern Texture", 2D) = "black" {}
        _ScanPatternStrength ("Scan Pattern Strength", Range(0, 1)) = 0.25
        _ScanPatternSpeedX ("Scan Pattern Speed X", Float) = 0.05
        _ScanPatternSpeedY ("Scan Pattern Speed Y", Float) = 0.15
        _ScanPatternTilingX ("Scan Pattern Tiling X", Float) = 1
        _ScanPatternTilingY ("Scan Pattern Tiling Y", Float) = 1
        _ScanPatternColor ("Scan Pattern Color", Color) = (0.82, 0.97, 1, 1)
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.08
        _ScanlineDensity ("Scanline Density", Float) = 180
        _ScanlineSpeed ("Scanline Speed", Float) = 1.2
        _RGBSplitStrength ("RGB Split Strength", Range(0, 2)) = 0.25
        _JitterStrength ("Jitter Strength", Range(0, 1)) = 0.15
        _JitterSpeed ("Jitter Speed", Float) = 2.2
        _HideNoiseStrength ("Hide Noise Strength", Range(0, 1)) = 0.05
        _HideNoiseSpeed ("Hide Noise Speed", Float) = 0.8
        _ShadowAlpha ("Shadow Alpha", Range(0, 1)) = 0.18
        _ShadowTintColor ("Shadow Tint Color", Color) = (0.18, 0.45, 0.82, 1)
        _ShadowOffsetX ("Shadow Offset X", Float) = 0.06
        _ShadowOffsetY ("Shadow Offset Y", Float) = -0.04
        _ShadowNoiseStrength ("Shadow Noise Strength", Range(0, 1)) = 0.08
        _ShadowFlowStrength ("Shadow Flow Strength", Range(0, 5)) = 0.65
        _ShadowRGBSplitStrength ("Shadow RGB Split Strength", Range(0, 2)) = 0.35
        _ShadowJitterStrength ("Shadow Jitter Strength", Range(0, 1)) = 0.28
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Player01Ghost2D"
            Tags { "LightMode"="UniversalForward" }

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
            TEXTURE2D(_ScanPatternTex);
            SAMPLER(sampler_ScanPatternTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _BodyTintColor;
                float _BodyAlpha;
                float4 _FlowColor;
                float _FlowIntensity;
                float _FlowSpeedX;
                float _FlowSpeedY;
                float _FlowWidth;
                float _ScanPatternStrength;
                float _ScanPatternSpeedX;
                float _ScanPatternSpeedY;
                float _ScanPatternTilingX;
                float _ScanPatternTilingY;
                float4 _ScanPatternColor;
                float _ScanlineIntensity;
                float _ScanlineDensity;
                float _ScanlineSpeed;
                float _RGBSplitStrength;
                float _JitterStrength;
                float _JitterSpeed;
                float _HideNoiseStrength;
                float _HideNoiseSpeed;
                float _ShadowAlpha;
                float4 _ShadowTintColor;
                float _ShadowOffsetX;
                float _ShadowOffsetY;
                float _ShadowNoiseStrength;
                float _ShadowFlowStrength;
                float _ShadowRGBSplitStrength;
                float _ShadowJitterStrength;
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

            float SmoothBand(float x, float width)
            {
                return saturate(1.0 - smoothstep(width, width + width * 0.75, abs(x)));
            }

            float SampleScanPattern(float2 uv, float2 timeOffset)
            {
                float2 tiling = max(float2(_ScanPatternTilingX, _ScanPatternTilingY), float2(0.001, 0.001));
                float2 patternUV = uv * tiling + timeOffset;
                float4 patternSample = SAMPLE_TEXTURE2D(_ScanPatternTex, sampler_ScanPatternTex, patternUV);
                float patternValue = max(patternSample.a, dot(patternSample.rgb, float3(0.3333333, 0.3333333, 0.3333333)));
                return saturate(patternValue);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = input.uv;
                float4 baseTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float alphaMask = baseTex.a;
                if (alphaMask <= 0.0001)
                {
                    return half4(0, 0, 0, 0);
                }

                float2 jitterCell = floor(uv * (64.0 + _JitterSpeed * 8.0) + _Time.y * _JitterSpeed);
                float jitterA = Hash12(jitterCell);
                float jitterB = Hash12(jitterCell + 17.31);
                float2 jitter = (float2(jitterA, jitterB) - 0.5) * _JitterStrength * 0.005;

                float2 bodyUV = uv + jitter;
                float splitBase = _RGBSplitStrength * 0.0015;
                float2 splitDir = normalize(float2(0.75, 0.35) + float2(sin(_Time.y * 0.7), cos(_Time.y * 0.43)) * 0.1);
                float2 splitOffset = splitDir * splitBase;

                float4 texR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, bodyUV + splitOffset);
                float4 texG = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, bodyUV);
                float4 texB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, bodyUV - splitOffset);
                float3 sampled = float3(texR.r, texG.g, texB.b);

                float scanPhase = uv.y * _ScanlineDensity + _Time.y * _ScanlineSpeed;
                float scanline = (sin(scanPhase * 6.2831853) * 0.5 + 0.5);
                scanline = smoothstep(0.45, 0.92, scanline) * _ScanlineIntensity;
                scanline *= alphaMask * lerp(1.0, 0.4, saturate(_ScanPatternStrength));

                float2 scanPatternOffset = _Time.y * float2(_ScanPatternSpeedX, _ScanPatternSpeedY);
                float scanPatternValue = SampleScanPattern(uv, scanPatternOffset);
                scanPatternValue = smoothstep(0.08, 0.9, scanPatternValue) * _ScanPatternStrength * alphaMask;
                float3 scanPatternGlow = _ScanPatternColor.rgb * scanPatternValue;

                float flowPhase = frac(uv.x * 0.85 + uv.y * 0.2 + _Time.y * (_FlowSpeedX + _FlowSpeedY * 0.5));
                float flowBand = SmoothBand(flowPhase - 0.5, max(0.01, _FlowWidth * 0.5));
                flowBand *= alphaMask;

                float hideWave = (sin((uv.x * 23.17 + uv.y * 31.41 + _Time.y * _HideNoiseSpeed) * 6.2831853) * 0.5 + 0.5);
                hideWave = smoothstep(0.35, 0.8, hideWave);
                float hideMask = lerp(1.0, hideWave, _HideNoiseStrength);

                float bodyAlpha = alphaMask * _BodyAlpha * hideMask;

                float3 bodyColor = sampled * _BodyTintColor.rgb;
                float3 flowGlow = _FlowColor.rgb * (flowBand * _FlowIntensity + scanline);
                float3 finalBodyColor = bodyColor + flowGlow;

                float2 shadowUV = uv + float2(_ShadowOffsetX, _ShadowOffsetY);
                float2 shadowJitterCell = floor(shadowUV * (52.0 + _JitterSpeed * 6.0) + _Time.y * (_JitterSpeed + 0.75));
                float shadowJA = Hash12(shadowJitterCell);
                float shadowJB = Hash12(shadowJitterCell + 11.17);
                float2 shadowJitter = (float2(shadowJA, shadowJB) - 0.5) * _ShadowJitterStrength * 0.006;
                shadowUV += shadowJitter;

                float shadowSplitBase = _ShadowRGBSplitStrength * 0.0016;
                float2 shadowSplitDir = normalize(float2(-0.68, 0.42) + float2(sin(_Time.y * 0.61), cos(_Time.y * 0.27)) * 0.1);
                float2 shadowSplitOffset = shadowSplitDir * shadowSplitBase;

                float4 shadowR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, shadowUV + shadowSplitOffset);
                float4 shadowG = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, shadowUV);
                float4 shadowB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, shadowUV - shadowSplitOffset);
                float3 shadowSample = float3(shadowR.r, shadowG.g, shadowB.b);

                float shadowScanPhase = shadowUV.y * (_ScanlineDensity * 0.92) + _Time.y * (_ScanlineSpeed * 0.8);
                float shadowScan = (sin(shadowScanPhase * 6.2831853) * 0.5 + 0.5);
                shadowScan = smoothstep(0.45, 0.92, shadowScan) * (0.05 + _ShadowNoiseStrength * 0.15);
                shadowScan *= shadowG.a * lerp(1.0, 0.4, saturate(_ScanPatternStrength));

                float2 shadowPatternOffset = _Time.y * float2(_ScanPatternSpeedX, _ScanPatternSpeedY) * 0.85;
                float shadowPatternValue = SampleScanPattern(shadowUV, shadowPatternOffset);
                shadowPatternValue = smoothstep(0.08, 0.9, shadowPatternValue) * _ScanPatternStrength * shadowG.a;
                float3 shadowPatternGlow = _ScanPatternColor.rgb * shadowPatternValue * 0.85;

                float shadowFlowPhase = frac(shadowUV.x * 0.78 + shadowUV.y * 0.18 + _Time.y * ((_FlowSpeedX + _FlowSpeedY) * 0.65));
                float shadowFlowBand = SmoothBand(shadowFlowPhase - 0.5, max(0.01, _FlowWidth * 0.7));
                shadowFlowBand *= shadowG.a;

                float shadowHideWave = (sin((shadowUV.x * 21.07 + shadowUV.y * 28.61 + _Time.y * _HideNoiseSpeed * 0.85) * 6.2831853) * 0.5 + 0.5);
                shadowHideWave = smoothstep(0.3, 0.82, shadowHideWave);
                float shadowHideMask = lerp(1.0, shadowHideWave, _ShadowNoiseStrength);

                float shadowAlpha = shadowG.a * _ShadowAlpha * shadowHideMask;
                float3 shadowColor = shadowSample * _ShadowTintColor.rgb;
                shadowColor += _FlowColor.rgb * (shadowFlowBand * _ShadowFlowStrength + shadowScan);
                shadowColor += shadowPatternGlow;
                shadowColor *= 0.75;

                float3 finalColor = finalBodyColor + scanPatternGlow + shadowColor * shadowAlpha;

                finalColor *= input.color.rgb;
                bodyAlpha = saturate(bodyAlpha * input.color.a);
                shadowAlpha = saturate(shadowAlpha * input.color.a);

                float finalAlpha = saturate(max(bodyAlpha, shadowAlpha));
                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
