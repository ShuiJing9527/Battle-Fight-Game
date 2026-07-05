Shader "ShoreWave/Overlay URP"
{
    Properties
    {
        _FoamNoiseTex ("Foam Noise Tex", 2D) = "gray" {}
        _WaveBandTex ("Wave Band Tex", 2D) = "white" {}

        _TideSpeed ("Tide Speed", Range(0.01, 3.0)) = 0.65
        _MainWaveWidth ("Main Wave Width", Range(0.005, 0.35)) = 0.05
        _MidFoamWidth ("Mid Foam Width", Range(0.01, 0.5)) = 0.09
        _OuterFoamWidth ("Outer Foam Width", Range(0.02, 0.8)) = 0.16
        _MainWaveOpacity ("Main Wave Opacity", Range(0.0, 1.0)) = 0.95
        _MidFoamOpacity ("Mid Foam Opacity", Range(0.0, 1.0)) = 0.5
        _OuterFoamOpacity ("Outer Foam Opacity", Range(0.0, 1.0)) = 0.22
        _MainWaveOffset ("Main Wave Offset", Range(-1.5, 1.5)) = 0.46
        _MidFoamOffset ("Mid Foam Offset", Range(-1.5, 1.5)) = 0.33
        _OuterFoamOffset ("Outer Foam Offset", Range(-1.5, 1.5)) = 0.16
        _MainWavePhase ("Main Wave Phase", Range(-6.283, 6.283)) = 0
        _MidFoamPhase ("Mid Foam Phase", Range(-6.283, 6.283)) = 0.9
        _OuterFoamPhase ("Outer Foam Phase", Range(-6.283, 6.283)) = 1.8
        _FoamStrength ("Foam Strength", Range(0.0, 3.0)) = 1.1
        _FoamSoftness ("Foam Softness", Range(0.001, 0.5)) = 0.12
        _Tiling ("Tiling", Range(0.1, 8.0)) = 1.6
        _CornerMode ("Corner Mode", Range(0.0, 1.0)) = 0.0
        _CornerInner ("Corner Inner", Range(0.0, 1.0)) = 0.0
        _CornerBlend ("Corner Blend", Range(0.0, 1.0)) = 0.45
        _TideOffset ("Tide Offset", Range(-2.0, 2.0)) = 0.0
        _TideAmplitude ("Tide Amplitude", Range(0.0, 1.0)) = 0.12
        _EdgeNoiseStrength ("Edge Noise Strength", Range(0.0, 1.0)) = 0.08
        _WaveBandInfluence ("Wave Band Influence", Range(0.0, 1.0)) = 0.25
        _ReceiveShadows ("Receive Shadows", Range(0.0, 1.0)) = 1.0
        _LightingStrength ("Lighting Strength", Range(0.0, 1.5)) = 1.0
        _MinimumAmbient ("Minimum Ambient", Range(0.0, 1.0)) = 0.06

        [HDR]_MainFoamColor ("Main Foam Color", Color) = (1, 1, 1, 1)
        [HDR]_SecondaryFoamColor ("Secondary Foam Color", Color) = (0.78, 0.9, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
            };

            TEXTURE2D(_FoamNoiseTex);
            SAMPLER(sampler_FoamNoiseTex);
            TEXTURE2D(_WaveBandTex);
            SAMPLER(sampler_WaveBandTex);

            CBUFFER_START(UnityPerMaterial)
                float _TideSpeed;
                float _MainWaveWidth;
                float _MidFoamWidth;
                float _OuterFoamWidth;
                float _MainWaveOpacity;
                float _MidFoamOpacity;
                float _OuterFoamOpacity;
                float _MainWaveOffset;
                float _MidFoamOffset;
                float _OuterFoamOffset;
                float _MainWavePhase;
                float _MidFoamPhase;
                float _OuterFoamPhase;
                float _FoamStrength;
                float _FoamSoftness;
                float _Tiling;
                float _CornerMode;
                float _CornerInner;
                float _CornerBlend;
                float _TideOffset;
                float _TideAmplitude;
                float _EdgeNoiseStrength;
                float _WaveBandInfluence;
                float _ReceiveShadows;
                float _LightingStrength;
                float _MinimumAmbient;
                float4 _MainFoamColor;
                float4 _SecondaryFoamColor;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                value += ValueNoise(p) * amplitude;
                p = p * 2.02 + 13.17;
                amplitude *= 0.5;

                value += ValueNoise(p) * amplitude;
                p = p * 2.03 + 7.11;
                amplitude *= 0.5;

                value += ValueNoise(p) * amplitude;
                return value;
            }

            float ShoreWaveLuminance(float3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }

            float Band(float value, float center, float width, float softness)
            {
                float distanceToCenter = abs(value - center);
                return 1.0 - smoothstep(width, width + softness, distanceToCenter);
            }

            float LayerFront(float baseFront, float phase, float layerAmplitude, float timeValue)
            {
                float sinWave = sin(timeValue * _TideSpeed + phase);
                float cosWave = cos(timeValue * (_TideSpeed * 0.67) + phase * 1.37);
                return baseFront + sinWave * layerAmplitude + cosWave * layerAmplitude * 0.25;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv * max(_Tiling, 0.0001);
                float timeValue = _Time.y;
                float baseFront = _TideOffset + sin(timeValue * _TideSpeed) * _TideAmplitude;

                float2 noiseUV = uv * 1.45 + float2(timeValue * 0.03, -timeValue * 0.06);
                float proceduralNoise = Fbm(noiseUV * 2.2);
                float sampledNoise = ShoreWaveLuminance(SAMPLE_TEXTURE2D(_FoamNoiseTex, sampler_FoamNoiseTex, noiseUV).rgb);
                float edgeNoise = lerp(proceduralNoise, sampledNoise, 0.7);

                float waveBandSample = SAMPLE_TEXTURE2D(_WaveBandTex, sampler_WaveBandTex, float2(uv.x * 0.35, 0.5)).r;
                float waveBandMask = lerp(1.0, waveBandSample, saturate(_WaveBandInfluence));

                float linearShoreAxis = uv.y;
                float2 topRightEdgeDistance = saturate(float2(1.0 - input.uv.x, 1.0 - input.uv.y));
                float outerCornerMetric = lerp(
                    min(topRightEdgeDistance.x, topRightEdgeDistance.y),
                    saturate(length(topRightEdgeDistance) * 0.70710678),
                    saturate(_CornerBlend));
                float innerCornerMetric = saturate(length(topRightEdgeDistance) * 0.70710678);
                float outerCornerAxis = 1.0 - outerCornerMetric;
                float innerCornerAxis = 1.0 - innerCornerMetric;
                float cornerShoreAxis = lerp(outerCornerAxis, innerCornerAxis, saturate(_CornerInner));
                float shoreAxis = lerp(linearShoreAxis, cornerShoreAxis, saturate(_CornerMode));
                float distortedShore = shoreAxis + (edgeNoise - 0.5) * _EdgeNoiseStrength;

                float mainFront = LayerFront(baseFront + _MainWaveOffset, _MainWavePhase, _TideAmplitude * 0.18, timeValue);
                float midFront = LayerFront(baseFront + _MidFoamOffset, _MidFoamPhase, _TideAmplitude * 0.28, timeValue);
                float outerFront = LayerFront(baseFront + _OuterFoamOffset, _OuterFoamPhase, _TideAmplitude * 0.34, timeValue);

                float mainNoise = (edgeNoise - 0.5) * _EdgeNoiseStrength * 1.15;
                float midNoise = (proceduralNoise - 0.5) * _EdgeNoiseStrength * 1.5;
                float outerNoise = (sampledNoise - 0.5) * _EdgeNoiseStrength * 1.8;

                float mainBand = Band(distortedShore + mainNoise, mainFront, _MainWaveWidth, _FoamSoftness * 0.75);
                float midBand = Band(distortedShore + midNoise, midFront, _MidFoamWidth, _FoamSoftness * 1.05);
                float outerBand = Band(distortedShore + outerNoise, outerFront, _OuterFoamWidth, _FoamSoftness * 1.35);

                midBand *= 1.0 - mainBand * 0.45;
                outerBand *= 1.0 - saturate(mainBand * 0.35 + midBand * 0.28);

                float mainAlpha = mainBand * _MainWaveOpacity;
                float midAlpha = midBand * _MidFoamOpacity;
                float outerAlpha = outerBand * _OuterFoamOpacity;

                float combinedAlpha = saturate(mainAlpha + midAlpha + outerAlpha);
                combinedAlpha = saturate(combinedAlpha * _FoamStrength * waveBandMask);

                float3 foamColor = 0.0;
                foamColor += _MainFoamColor.rgb * mainAlpha;
                foamColor += lerp(_SecondaryFoamColor.rgb, _MainFoamColor.rgb, 0.35) * midAlpha;
                foamColor += _SecondaryFoamColor.rgb * outerAlpha;

                float colorWeight = max(mainAlpha + midAlpha + outerAlpha, 1e-4);
                foamColor /= colorWeight;
                foamColor *= saturate(0.84 + edgeNoise * 0.28);

                half3 normalWS = input.normalWS;
                half normalLenSq = dot(normalWS, normalWS);
                normalWS = (normalLenSq > 1e-4h) ? normalize(normalWS) : half3(0.0h, 1.0h, 0.0h);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half receiveShadows = saturate(_ReceiveShadows);
                half shadowAttenuation = lerp(1.0h, mainLight.shadowAttenuation, receiveShadows);
                half3 ambient = max(SampleSH(normalWS), half3(_MinimumAmbient, _MinimumAmbient, _MinimumAmbient));
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = mainLight.color * ndotl * shadowAttenuation * saturate(_LightingStrength);
                half3 finalColor = foamColor * (ambient + diffuse);

                return half4(finalColor, combinedAlpha);
            }
            ENDHLSL
        }
    }
}
