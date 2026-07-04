Shader "ShoreWave/Sand Fade URP"
{
    Properties
    {
        _BaseSandTex ("Base Sand Texture", 2D) = "white" {}
        _FadeNoiseTex ("Fade Noise Tex", 2D) = "gray" {}
        _GrassTransitionTex ("Grass Transition Texture", 2D) = "white" {}

        _SandTint ("Sand Tint", Color) = (1, 1, 1, 1)
        _Brightness ("Brightness", Range(0.0, 3.0)) = 1.0
        _TextureTiling ("Texture Tiling", Vector) = (1, 1, 0, 0)
        _EnableShoreFade ("Enable Shore Fade", Range(0.0, 1.0)) = 1.0

        _FadeStart ("Fade Start", Range(-0.5, 1.5)) = 0.58
        _FadeWidth ("Fade Width", Range(0.01, 1.5)) = 0.36
        _FadeSoftness ("Fade Softness", Range(0.001, 0.5)) = 0.1
        _FadeReverse ("Fade Reverse", Range(0.0, 1.0)) = 0.0
        _FadeNoiseStrength ("Fade Noise Strength", Range(0.0, 0.25)) = 0.035
        _WetSandStrength ("Wet Sand Strength", Range(0.0, 1.0)) = 0.18
        _WetSandColor ("Wet Sand Color", Color) = (0.72, 0.66, 0.52, 1)
        _AlphaMultiplier ("Alpha Multiplier", Range(0.0, 1.0)) = 1.0
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0
        _NormalStrength ("Normal Strength", Range(0.0, 2.0)) = 1.0
        _ReceiveShadows ("Receive Shadows", Range(0.0, 1.0)) = 1.0
        _LightingStrength ("Lighting Strength", Range(0.0, 1.5)) = 1.0
        _MinimumAmbient ("Minimum Ambient", Range(0.0, 1.0)) = 0.08
        _GrassBlendColor ("Grass Blend Color", Color) = (0.86, 0.84, 0.68, 1)
        _GrassBlendWidth ("Grass Blend Width", Range(0.0, 1.0)) = 0.0
        _GrassBlendStrength ("Grass Blend Strength", Range(0.0, 1.0)) = 0.0
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

            TEXTURE2D(_BaseSandTex);
            SAMPLER(sampler_BaseSandTex);
            TEXTURE2D(_FadeNoiseTex);
            SAMPLER(sampler_FadeNoiseTex);
            TEXTURE2D(_GrassTransitionTex);
            SAMPLER(sampler_GrassTransitionTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _SandTint;
                float _Brightness;
                float4 _TextureTiling;
                float _EnableShoreFade;
                float _FadeStart;
                float _FadeWidth;
                float _FadeSoftness;
                float _FadeReverse;
                float _FadeNoiseStrength;
                float _WetSandStrength;
                float4 _WetSandColor;
                float _AlphaMultiplier;
                float _Smoothness;
                float _Metallic;
                float _NormalStrength;
                float _ReceiveShadows;
                float _LightingStrength;
                float _MinimumAmbient;
                float4 _GrassBlendColor;
                float _GrassBlendWidth;
                float _GrassBlendStrength;
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

            half4 Frag(Varyings input) : SV_Target
            {
                float2 tiledUV = input.uv * max(_TextureTiling.xy, float2(0.0001, 0.0001)) + _TextureTiling.zw;
                half4 sand = SAMPLE_TEXTURE2D(_BaseSandTex, sampler_BaseSandTex, tiledUV);
                float noise = SAMPLE_TEXTURE2D(_FadeNoiseTex, sampler_FadeNoiseTex, tiledUV * 0.8).r;
                float shoreFadeEnabled = saturate(_EnableShoreFade);

                float fadeAxis = lerp(input.uv.y, 1.0 - input.uv.y, saturate(_FadeReverse));
                float fadeNoiseOffset = (noise - 0.5) * _FadeNoiseStrength;
                float distortedFadeAxis = saturate(fadeAxis + fadeNoiseOffset);

                float fadeStart = _FadeStart;
                float fadeEnd = _FadeStart + max(_FadeWidth, 0.0001);
                float fade = 1.0 - smoothstep(fadeStart, fadeEnd, distortedFadeAxis);
                fade = smoothstep(0.0, 1.0, fade);

                float edgeProximity = 1.0 - smoothstep(fadeStart - _FadeSoftness, fadeEnd + _FadeSoftness, distortedFadeAxis);
                float wetMask = saturate((1.0 - fade) * 1.15 + edgeProximity * 0.45) * shoreFadeEnabled;

                float3 surfaceColor = sand.rgb * _SandTint.rgb * _Brightness;
                float wetBlend = saturate(_WetSandStrength * wetMask);
                surfaceColor = lerp(surfaceColor, surfaceColor * _WetSandColor.rgb, wetBlend);

                float grassTransitionSample = SAMPLE_TEXTURE2D(_GrassTransitionTex, sampler_GrassTransitionTex, input.uv).r;
                float grassBlendEdge = 1.0 - smoothstep(
                    max(_GrassBlendWidth, 0.0001),
                    max(_GrassBlendWidth, 0.0001) + max(_FadeSoftness, 0.0001),
                    distortedFadeAxis);
                float grassBlendMask = saturate(grassTransitionSample * grassBlendEdge * _GrassBlendStrength);
                surfaceColor = lerp(surfaceColor, surfaceColor * _GrassBlendColor.rgb, grassBlendMask);

                float fullAlpha = saturate(sand.a * _SandTint.a * _AlphaMultiplier);
                float fadeAlpha = saturate(fade * sand.a * _SandTint.a * _AlphaMultiplier);
                fadeAlpha *= 1.0 - smoothstep(1.0, 1.0 + _FadeSoftness, distortedFadeAxis);
                float alpha = lerp(fullAlpha, fadeAlpha, shoreFadeEnabled);

                half3 normalWS = input.normalWS;
                half normalLenSq = dot(normalWS, normalWS);
                normalWS = (normalLenSq > 1e-4h) ? normalize(normalWS) : half3(0.0h, 1.0h, 0.0h);
                normalWS = normalize(lerp(half3(0.0h, 1.0h, 0.0h), normalWS, saturate(_NormalStrength)));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half receiveShadows = saturate(_ReceiveShadows);
                half shadowAttenuation = lerp(1.0h, mainLight.shadowAttenuation, receiveShadows);
                half3 ambient = SampleSH(normalWS);
                ambient = max(ambient, surfaceColor * _MinimumAmbient);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = mainLight.color * ndotl * shadowAttenuation * saturate(_LightingStrength);

                half3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                half3 halfDir = normalize(mainLight.direction + viewDir);
                half wetSmoothnessBoost = wetBlend * 0.08h;
                half effectiveSmoothness = saturate(_Smoothness + wetSmoothnessBoost);
                half effectiveMetallic = saturate(_Metallic);
                half specPower = lerp(8.0h, 64.0h, effectiveSmoothness);
                half specular = pow(saturate(dot(normalWS, halfDir)), specPower) * effectiveSmoothness;
                half3 specularColor = lerp(half3(0.02h, 0.02h, 0.02h), surfaceColor, effectiveMetallic);

                half3 finalColor = surfaceColor * (ambient + diffuse);
                finalColor += specularColor * specular * mainLight.color * shadowAttenuation * 0.08h;
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
