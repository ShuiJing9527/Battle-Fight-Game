Shader "Hidden/AirWall/ScreenDistortion"
{
    Properties
    {
        _BaseDistortionStrength ("Base Distortion Strength", Range(0, 0.12)) = 0.04
        _RingDistortionBoost ("Ring Distortion Boost", Range(0, 4)) = 1.2
        _MaskRadius ("Mask Radius", Range(0.02, 0.6)) = 0.22
        _EdgeFadeWidth ("Edge Fade Width", Range(0.05, 1)) = 0.48
        _RippleCount ("Ripple Count", Float) = 11
        _Speed ("Ripple Speed", Float) = 0.55
        _RippleStrength ("Ripple Strength", Range(0, 3)) = 1.15
        _RingWidth ("Ring Width", Range(0.01, 1)) = 0.34
        _Alpha ("Overall Alpha", Range(0, 1)) = 0.72
        _EdgeGlow ("Edge Glow", Range(0, 3)) = 0.18
        _ChromaticStrength ("Chromatic Strength", Range(0, 0.04)) = 0.008
        _IridescenceStrength ("Iridescence Strength", Range(0, 1)) = 0.28
        _EmissionStrength ("Emission Strength", Range(0, 2)) = 0.22
        _RingEmissionStrength ("Ring Emission Strength", Range(0, 4)) = 0.95
        _RainbowStrength ("Rainbow Strength", Range(0, 1)) = 0.68
        _RainbowSpeed ("Rainbow Speed", Range(0, 2)) = 0.22
        _RainbowSaturation ("Rainbow Saturation", Range(0, 1)) = 0.78
        _RainbowBrightness ("Rainbow Brightness", Range(0, 2)) = 1.08
        _NoiseStrength ("Noise Strength", Range(0, 0.25)) = 0.02
        _NoiseScale ("Noise Scale", Range(0.5, 12)) = 2.4
        _AngularWarpStrength ("Angular Warp Strength", Range(0, 0.25)) = 0.03
        _RingIrregularity ("Ring Irregularity", Range(0, 1)) = 0.12
        _SecondaryRippleStrength ("Secondary Ripple Strength", Range(0, 0.5)) = 0.04
        _SecondaryRippleScale ("Secondary Ripple Scale", Range(0.5, 8)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "AirWallScreenDistortion"

            ZTest Always
            ZWrite Off
            Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);
            float4 _BlitTexture_TexelSize;
            float4 _BlitScaleBias;
            float _BlitMipLevel;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float _BaseDistortionStrength;
                float _RingDistortionBoost;
                float _MaskRadius;
                float _EdgeFadeWidth;
                float _RippleCount;
                float _Speed;
                float _RippleStrength;
                float _RingWidth;
                float _Alpha;
                float _EdgeGlow;
                float _ChromaticStrength;
                float _IridescenceStrength;
                float _EmissionStrength;
                float _RingEmissionStrength;
                float _RainbowStrength;
                float _RainbowSpeed;
                float _RainbowSaturation;
                float _RainbowBrightness;
                float _NoiseStrength;
                float _NoiseScale;
                float _AngularWarpStrength;
                float _RingIrregularity;
                float _SecondaryRippleStrength;
                float _SecondaryRippleScale;
            CBUFFER_END

            float4 _AirWallScreenData;
            float4 _AirWallTimeData;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.texcoord = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;

                #if UNITY_UV_STARTS_AT_TOP
                output.texcoord.y = 1.0 - output.texcoord.y;
                #endif

                return output;
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

            float3 HueToRgb(float hue)
            {
                float3 rgb = abs(frac(hue + float3(0.0, 0.6666667, 0.3333333)) * 6.0 - 3.0) - 1.0;
                return saturate(rgb);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                float3 original = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel).rgb;

                float visibility = _AirWallScreenData.z;
                if (visibility <= 0.0001)
                {
                    return half4(original, 1.0);
                }

                float2 center = _AirWallScreenData.xy;
                float hueOffset = _AirWallScreenData.w;
                float elapsed = _AirWallTimeData.x;
                float normalizedLife = _AirWallTimeData.z;

                float aspect = max(_ScreenParams.x / max(_ScreenParams.y, 1.0), 0.001);
                float2 delta = uv - center;
                float2 aspectDelta = float2(delta.x * aspect, delta.y);
                float distanceFromCenter = length(aspectDelta);
                float radius = max(_MaskRadius, 0.001);
                float normalizedDistance = distanceFromCenter / radius;

                float2 radialAspect = distanceFromCenter > 0.0001 ? aspectDelta / distanceFromCenter : float2(0.0, 1.0);
                float2 radialUv = normalize(float2(radialAspect.x / aspect, radialAspect.y));
                float2 tangentUv = float2(-radialUv.y, radialUv.x);
                float angle = atan2(aspectDelta.y, aspectDelta.x);

                float edgeStart = saturate(1.0 - _EdgeFadeWidth);
                float areaMask = (1.0 - smoothstep(edgeStart, 1.0, normalizedDistance)) * smoothstep(0.015, 0.08, normalizedDistance);
                areaMask = saturate(areaMask) * visibility;

                float lowNoise = ValueNoise(aspectDelta * _NoiseScale + hueOffset * 19.31);
                float angularWarp = sin(angle * 3.0 + hueOffset * 6.2831853) * 0.55 +
                    sin(angle * 5.0 - elapsed * 0.11 + hueOffset * 9.17) * 0.3 +
                    sin(angle * 8.0 + normalizedDistance * 3.0) * 0.15;
                float secondaryWarp = sin(normalizedDistance * _SecondaryRippleScale * 6.2831853 + angle * 2.0 - elapsed * _Speed * 2.1);
                float warpedDistance = normalizedDistance;
                warpedDistance += (lowNoise - 0.5) * _NoiseStrength;
                warpedDistance += angularWarp * _AngularWarpStrength;
                warpedDistance += secondaryWarp * _SecondaryRippleStrength * 0.08;

                float ripplePhase = warpedDistance * _RippleCount - elapsed * _Speed * 6.2831853;
                float wave = sin(ripplePhase);
                float widthVariation = 1.0 + (lowNoise - 0.5) * _RingIrregularity * 0.7 + secondaryWarp * _RingIrregularity * 0.15;
                float effectiveRingWidth = saturate(_RingWidth * widthVariation);
                float ringMask = smoothstep(1.0 - effectiveRingWidth, 1.0, 1.0 - abs(wave)) * areaMask;
                float ringIntensityVariation = saturate(1.0 + (lowNoise - 0.5) * _RingIrregularity + secondaryWarp * _RingIrregularity * 0.2);

                float membraneWave = sin(normalizedDistance * 5.0 + angularWarp * 1.65 - elapsed * _Speed * 1.35);
                float slowPulse = sin(ripplePhase * 0.32 + angularWarp) * 0.5 + 0.5;
                float2 baseDirection = radialUv * (membraneWave * 0.78 + (slowPulse * 2.0 - 1.0) * 0.35) +
                    tangentUv * sin(ripplePhase * 0.21 + angularWarp) * 0.58;
                float2 rippleDirection = radialUv * wave + tangentUv * cos(ripplePhase * 0.7) * 0.35;

                float2 baseDistortion = baseDirection * areaMask * (0.82 + slowPulse * 0.22) * _BaseDistortionStrength;
                float2 ringDistortion = rippleDirection * ringMask * _RippleStrength * _RingDistortionBoost * ringIntensityVariation * _BaseDistortionStrength;
                float2 finalDistortion = baseDistortion + ringDistortion;

                float chromaticMask = saturate(areaMask * 0.32 + ringMask * 0.92);
                float2 chromaticOffset = rippleDirection * chromaticMask * _ChromaticStrength;

                float3 refracted;
                refracted.r = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + finalDistortion + chromaticOffset, _BlitMipLevel).r;
                refracted.g = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + finalDistortion, _BlitMipLevel).g;
                refracted.b = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + finalDistortion - chromaticOffset, _BlitMipLevel).b;

                float hue = frac(hueOffset + elapsed * _RainbowSpeed + normalizedDistance * 0.18 + wave * 0.025);
                float3 rainbow = HueToRgb(hue);
                rainbow = lerp(float3(1.0, 1.0, 1.0), rainbow, _RainbowSaturation) * _RainbowBrightness;

                float3 iridescenceA = float3(0.56, 0.9, 1.0);
                float3 iridescenceB = float3(0.78, 0.64, 1.0);
                float3 iridescence = lerp(iridescenceA, rainbow, _RainbowStrength);
                float colorMask = saturate((ringMask * 0.9 + areaMask * 0.13) * _IridescenceStrength);
                float edgeGlow = smoothstep(0.45, 0.95, normalizedDistance) * areaMask * _EdgeGlow;

                float3 color = lerp(original, refracted, saturate(areaMask * _Alpha));
                color = lerp(color, color * iridescence, colorMask * 0.18);
                color += iridescence * ringMask * (_RingEmissionStrength + _EmissionStrength);
                color += iridescence * edgeGlow * 0.18;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
