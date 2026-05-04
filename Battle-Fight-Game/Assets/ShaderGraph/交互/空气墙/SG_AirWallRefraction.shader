Shader "Shader Graphs/SG_AirWallRefraction"
{
    Properties
    {
        _RippleColor ("Ripple Color", Color) = (0.65, 0.9, 1, 1)
        _DistortionStrength ("Distortion Strength", Range(0, 0.2)) = 0.12
        _BaseDistortionStrength ("Base Distortion Strength", Range(0, 0.2)) = 0.15
        _RingDistortionBoost ("Ring Distortion Boost", Range(0, 4)) = 1.35
        _RippleCount ("Ripple Count", Float) = 14
        _RingWidth ("Ring Width", Range(0.01, 1)) = 0.38
        _RippleStrength ("Ripple Strength", Range(0, 3)) = 1.4
        _MaskRadius ("Mask Radius", Range(0.1, 1.5)) = 1.08
        _EdgeFadeWidth ("Edge Fade Width", Range(0.05, 1)) = 0.6
        _OuterFadeStrength ("Outer Fade Strength", Range(0.1, 4)) = 1.45
        _DistortionFalloff ("Distortion Falloff", Range(0.1, 4)) = 0.35
        _Alpha ("Overall Alpha", Range(0, 1)) = 0.52
        _EdgeGlow ("Edge Glow", Range(0, 3)) = 0.3
        _Speed ("Ripple Speed", Float) = 0.85
        _ChromaticStrength ("Chromatic Strength", Range(0, 0.04)) = 0.008
        _IridescenceStrength ("Iridescence Strength", Range(0, 1)) = 0.18
        _IridescenceSpeed ("Iridescence Speed", Float) = 0.7
        _IridescenceScale ("Iridescence Scale", Float) = 3.2
        _TintIntensity ("Tint Intensity", Range(0, 1)) = 0.12
        _EmissionStrength ("Emission Strength", Range(0, 2)) = 0.22
        _EmissionColor ("Emission Color", Color) = (0.58, 0.9, 1, 1)
        _EmissionEdgeStrength ("Emission Edge Strength", Range(0, 2)) = 0.16
        _IridescenceEmissionStrength ("Iridescence Emission Strength", Range(0, 2)) = 0.28
        _HueOffset ("Hue Offset", Range(0, 1)) = 0
        _RainbowStrength ("Rainbow Strength", Range(0, 1)) = 0.42
        _RainbowSpeed ("Rainbow Speed", Range(0, 2)) = 0.18
        _RainbowSaturation ("Rainbow Saturation", Range(0, 1)) = 0.65
        _RainbowBrightness ("Rainbow Brightness", Range(0, 2)) = 1.08
        _RingColorIntensity ("Ring Color Intensity", Range(0, 3)) = 1.25
        _RingEmissionStrength ("Ring Emission Strength", Range(0, 4)) = 1.1
        _RingEmissionBoost ("Ring Emission Boost", Range(0, 4)) = 0.75
        _WhiteLineSuppression ("White Line Suppression", Range(0, 1)) = 0.92
        _NoiseStrength ("Noise Strength", Range(0, 0.25)) = 0.045
        _NoiseScale ("Noise Scale", Range(0.5, 12)) = 3.2
        _AngularWarpStrength ("Angular Warp Strength", Range(0, 0.25)) = 0.07
        _RingIrregularity ("Ring Irregularity", Range(0, 1)) = 0.28
        _SecondaryRippleStrength ("Secondary Ripple Strength", Range(0, 0.5)) = 0.12
        _SecondaryRippleScale ("Secondary Ripple Scale", Range(0.5, 8)) = 2.4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "AirWallRefraction"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPosition : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _RippleColor;
                float _DistortionStrength;
                float _BaseDistortionStrength;
                float _RingDistortionBoost;
                float _RippleCount;
                float _RingWidth;
                float _RippleStrength;
                float _MaskRadius;
                float _EdgeFadeWidth;
                float _OuterFadeStrength;
                float _DistortionFalloff;
                float _Alpha;
                float _EdgeGlow;
                float _Speed;
                float _ChromaticStrength;
                float _IridescenceStrength;
                float _IridescenceSpeed;
                float _IridescenceScale;
                float _TintIntensity;
                float _EmissionStrength;
                float4 _EmissionColor;
                float _EmissionEdgeStrength;
                float _IridescenceEmissionStrength;
                float _HueOffset;
                float _RainbowStrength;
                float _RainbowSpeed;
                float _RainbowSaturation;
                float _RainbowBrightness;
                float _RingColorIntensity;
                float _RingEmissionStrength;
                float _RingEmissionBoost;
                float _WhiteLineSuppression;
                float _NoiseStrength;
                float _NoiseScale;
                float _AngularWarpStrength;
                float _RingIrregularity;
                float _SecondaryRippleStrength;
                float _SecondaryRippleScale;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positions.positionCS;
                output.screenPosition = ComputeScreenPos(output.positionHCS);
                output.uv = input.uv;
                return output;
            }

            float3 HueToRgb(float hue)
            {
                float3 rgb = abs(frac(hue + float3(0.0, 0.6666667, 0.3333333)) * 6.0 - 3.0) - 1.0;
                return saturate(rgb);
            }

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 cell = floor(uv);
                float2 local = frac(uv);
                float2 blend = local * local * (3.0 - 2.0 * local);

                float bottomLeft = Hash21(cell);
                float bottomRight = Hash21(cell + float2(1.0, 0.0));
                float topLeft = Hash21(cell + float2(0.0, 1.0));
                float topRight = Hash21(cell + float2(1.0, 1.0));

                float bottom = lerp(bottomLeft, bottomRight, blend.x);
                float top = lerp(topLeft, topRight, blend.x);
                return lerp(bottom, top, blend.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv - 0.5;
                float radius = max(_MaskRadius, 0.001);
                float distanceFromCenter = length(centeredUv) * 2.0;
                float normalizedDistance = distanceFromCenter / radius;
                float2 radialDirection = distanceFromCenter > 0.0001 ? centeredUv / distanceFromCenter : float2(0, 0);

                float angle = atan2(centeredUv.y, centeredUv.x);
                float lowNoise = ValueNoise(centeredUv * _NoiseScale + _HueOffset * 17.31);
                float angularWarp = sin(angle * 3.0 + _HueOffset * 6.2831853) * 0.55 +
                    sin(angle * 5.0 - _Time.y * 0.13 + _HueOffset * 9.17) * 0.3 +
                    sin(angle * 8.0 + normalizedDistance * 3.0) * 0.15;
                float secondaryWarp = sin(normalizedDistance * _SecondaryRippleScale * 6.2831853 + angle * 2.0 - _Time.y * _Speed * 2.1);
                float warpedDistance = normalizedDistance;
                warpedDistance += (lowNoise - 0.5) * _NoiseStrength;
                warpedDistance += angularWarp * _AngularWarpStrength;
                warpedDistance += secondaryWarp * _SecondaryRippleStrength * 0.08;

                float ripplePhase = warpedDistance * _RippleCount - _Time.y * _Speed * 6.2831853;
                float wave = sin(ripplePhase);
                float ringWidthVariation = 1.0 + (lowNoise - 0.5) * _RingIrregularity * 0.7 + secondaryWarp * _RingIrregularity * 0.15;
                float effectiveRingWidth = saturate(_RingWidth * ringWidthVariation);
                float ringMask = smoothstep(1.0 - effectiveRingWidth, 1.0, 1.0 - abs(wave));
                float ringIntensityVariation = saturate(1.0 + (lowNoise - 0.5) * _RingIrregularity + secondaryWarp * _RingIrregularity * 0.2);
                ringMask *= lerp(1.0, ringIntensityVariation, _RingIrregularity);

                float edgeStart = saturate(1.0 - _EdgeFadeWidth);
                float edgeFade = 1.0 - smoothstep(edgeStart, 1.0, normalizedDistance);
                float outerFade = pow(saturate(1.0 - normalizedDistance), max(_OuterFadeStrength, 0.001));
                float distortionFalloff = pow(saturate(1.0 - normalizedDistance), max(_DistortionFalloff, 0.001));
                float centerFade = smoothstep(0.02, 0.1, normalizedDistance);
                float broadMask = saturate(edgeFade * outerFade * centerFade);
                float areaMask = saturate(edgeFade * distortionFalloff * centerFade);
                float distortionArea = areaMask;
                float softPulse = sin(ripplePhase * 0.35 + angularWarp) * 0.5 + 0.5;
                float baseDistortionMask = areaMask * (0.85 + softPulse * 0.22);
                float ringDistortionMask = ringMask * areaMask * _RingDistortionBoost * ringIntensityVariation;

                float2 screenUv = input.screenPosition.xy / input.screenPosition.w;
                float2 tangentDirection = float2(-radialDirection.y, radialDirection.x);
                float2 distortionDirection = radialDirection * wave + tangentDirection * cos(ripplePhase * 0.7) * 0.35;
                float slowMembraneWave = sin(normalizedDistance * 5.2 + angularWarp * 1.7 - _Time.y * _Speed * 1.4);
                float2 baseDriftDirection = radialDirection * (slowMembraneWave * 0.75 + (softPulse * 2.0 - 1.0) * 0.35) +
                    tangentDirection * sin(ripplePhase * 0.23 + angularWarp) * 0.55;
                float2 baseDistortion = baseDriftDirection * baseDistortionMask * _BaseDistortionStrength;
                float2 ringDistortion = distortionDirection * ringDistortionMask * _DistortionStrength;
                float2 distortion = baseDistortion + ringDistortion;
                float chromaticMask = saturate(areaMask * 0.35 + ringMask * areaMask * 0.9);
                float2 chromaticOffset = distortionDirection * chromaticMask * _ChromaticStrength;
                float3 refractedScene;
                refractedScene.r = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUv + distortion + chromaticOffset).r;
                refractedScene.g = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUv + distortion).g;
                refractedScene.b = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUv + distortion - chromaticOffset).b;

                float3 iridescenceA = float3(0.56, 0.9, 1.0);
                float3 iridescenceB = float3(0.78, 0.64, 1.0);
                float3 iridescenceC = float3(1.0, 0.64, 0.86);
                float colorFlow = sin((normalizedDistance * _IridescenceScale + wave * 0.25 + _Time.y * _IridescenceSpeed) * 6.2831853) * 0.5 + 0.5;
                float3 iridescence = lerp(iridescenceA, iridescenceB, smoothstep(0.0, 0.55, colorFlow));
                iridescence = lerp(iridescence, iridescenceC, smoothstep(0.55, 1.0, colorFlow) * 0.45);
                float hue = frac(_HueOffset + _Time.y * _RainbowSpeed + normalizedDistance * 0.18 + wave * 0.025);
                float3 rainbow = HueToRgb(hue);
                rainbow = lerp(float3(1.0, 1.0, 1.0), rainbow, _RainbowSaturation) * _RainbowBrightness;
                iridescence = lerp(iridescence, rainbow, _RainbowStrength);
                float activeColorMask = saturate((ringMask * 0.52 + distortionArea * 0.38) * _IridescenceStrength);
                float coloredRingMask = ringMask * broadMask;
                float ringVisibility = saturate(coloredRingMask * _RingColorIntensity);

                float fresnelEdge = smoothstep(0.45, 0.9, normalizedDistance) * broadMask;
                float whiteAssist = 1.0 - _WhiteLineSuppression;
                float glow = saturate(ringMask * 0.03 * whiteAssist + fresnelEdge * 0.18) * _EdgeGlow;
                float emissionMask = saturate(coloredRingMask * 0.9 + areaMask * 0.06);
                float edgeEmissionMask = saturate(fresnelEdge * edgeFade * _EmissionEdgeStrength);
                float3 emissionColor = lerp(_EmissionColor.rgb, iridescence, _RainbowStrength * 0.75);
                float3 coloredRipple = iridescence * ringVisibility;
                float3 emission = emissionColor * emissionMask * _EmissionStrength;
                emission += iridescence * coloredRingMask * (_RingEmissionStrength + ringVisibility * _RingEmissionBoost);
                emission += iridescence * activeColorMask * emissionMask * _IridescenceEmissionStrength;
                emission += lerp(_EmissionColor.rgb, iridescence, 0.55) * edgeEmissionMask;

                float3 color = refractedScene;
                color = lerp(color, color * iridescence, activeColorMask * _TintIntensity);
                color += coloredRipple;
                color += (_RippleColor.rgb * glow * whiteAssist + iridescence * activeColorMask * 0.04) * broadMask;
                color += emission * broadMask;
                float alpha = saturate(areaMask * _Alpha + coloredRingMask * max(_Alpha, 0.35) * 0.55 + fresnelEdge * _Alpha * 0.04);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
