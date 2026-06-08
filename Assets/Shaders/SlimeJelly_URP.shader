Shader "UnderTheStars/Enemies/SlimeJelly_URP"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        _Color ("Sprite Color", Color) = (1,1,1,1)

        _TintColor ("TintColor", Color) = (0.80, 0.98, 0.94, 1.00)
        _BaseColorStrength ("BaseColorStrength", Range(0, 2)) = 1.00
        _LightStrength ("LightStrength", Range(0, 2)) = 0.80
        _ShadowStrength ("ShadowStrength", Range(0, 1)) = 0.45
        _AmbientStrength ("AmbientStrength", Range(0, 1)) = 0.35

        _HighlightColor ("HighlightColor", Color) = (0.93, 1.00, 0.99, 1.00)
        _HighlightStrength ("HighlightStrength", Range(0, 2)) = 0.50
        _HighlightPower ("HighlightPower", Range(4, 64)) = 24.0
        _RimColor ("RimColor", Color) = (0.86, 1.00, 0.98, 1.00)
        _RimStrength ("RimStrength", Range(0, 2)) = 0.30
        _RimPower ("RimPower", Range(0.5, 8)) = 3.0

        _InnerDarkColor ("InnerDarkColor", Color) = (0.09, 0.33, 0.29, 1.00)
        _InnerDarkStrength ("InnerDarkStrength", Range(0, 1)) = 0.25
        _AlphaStrength ("AlphaStrength", Range(0, 2)) = 1.0
        _EdgeAlphaStrength ("EdgeAlphaStrength", Range(0, 1)) = 0.35
        _InnerAlphaStrength ("InnerAlphaStrength", Range(0, 1)) = 0.85
        _AlphaSoftness ("AlphaSoftness", Range(0, 1)) = 0.5
        _JellyWobbleStrength ("JellyWobbleStrength", Range(0, 0.1)) = 0.012
        _JellyWobbleSpeed ("JellyWobbleSpeed", Range(0, 8)) = 2.8
        _Transparency ("Transparency", Range(0, 1)) = 0.85
        _ShadowAlphaCutoff("Shadow Alpha Cutoff", Range(0,1)) = 0.25
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.58
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 rightWS : TEXCOORD2;
                float3 upWS : TEXCOORD3;
                float3 forwardWS : TEXCOORD4;
                float4 color : COLOR;
                float fogCoord : TEXCOORD5;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _RendererColor;
                float4 _Color;
                float4 _TintColor;
                float4 _HighlightColor;
                float4 _RimColor;
                float4 _InnerDarkColor;
                float _BaseColorStrength;
                float _LightStrength;
                float _ShadowStrength;
                float _AmbientStrength;
                float _HighlightStrength;
                float _HighlightPower;
                float _RimStrength;
                float _RimPower;
                float _InnerDarkStrength;
                float _AlphaStrength;
                float _EdgeAlphaStrength;
                float _InnerAlphaStrength;
                float _AlphaSoftness;
                float _JellyWobbleStrength;
                float _JellyWobbleSpeed;
                float _Transparency;
                float _Metallic;
                float _Smoothness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = positionInputs.positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.positionWS = positionInputs.positionWS;
                OUT.rightWS = normalize(TransformObjectToWorldDir(float3(1.0, 0.0, 0.0)));
                OUT.upWS = normalize(TransformObjectToWorldDir(float3(0.0, 1.0, 0.0)));
                OUT.forwardWS = normalize(TransformObjectToWorldDir(float3(0.0, 0.0, 1.0)));
                OUT.color = IN.color;
                OUT.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float timePhase = _Time.y * _JellyWobbleSpeed;
                float wobbleWave = sin((uv.y * 11.0 + timePhase) * 1.1) * cos((uv.x * 9.0 + timePhase * 0.7));
                float2 wobbleOffset = float2(wobbleWave * _JellyWobbleStrength, 0.0);
                uv += wobbleOffset;

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                half4 spriteColor = tex * IN.color * _RendererColor * _Color;
                half3 baseColor = spriteColor.rgb * _TintColor.rgb * _BaseColorStrength;

                float2 centered = uv - 0.5;
                float radial = saturate(length(centered) / 0.7071);
                float sphereXY = saturate(1.0 - dot(centered * 2.0, centered * 2.0));
                float pseudoZ = sqrt(max(1e-4, sphereXY));
                float3 pseudoNormalTS = normalize(float3(centered.x * 2.0, centered.y * 2.0, pseudoZ));
                float3 normalWS = normalize(
                    pseudoNormalTS.x * IN.rightWS +
                    pseudoNormalTS.y * IN.upWS +
                    pseudoNormalTS.z * IN.forwardWS);

                float3 viewWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDirWS = normalize(mainLight.direction);

                float ndotl = saturate(dot(normalWS, lightDirWS));
                float shadowAtten = saturate(mainLight.shadowAttenuation);
                float directShadow = lerp(1.0 - _ShadowStrength, 1.0, shadowAtten);
                float ambientShadow = lerp(1.0 - _ShadowStrength * 0.45, 1.0, shadowAtten);

                half3 ambientTerm = SampleSH(normalWS) * _AmbientStrength * ambientShadow;
                half3 diffuseTerm = mainLight.color * ndotl * _LightStrength * directShadow;

                float3 halfDir = normalize(lightDirWS + viewWS);
                float ndoth = saturate(dot(normalWS, halfDir));
                float specPower = lerp(_HighlightPower * 0.45, _HighlightPower * 1.5, saturate(_Smoothness));
                float specular = pow(ndoth, specPower) * _HighlightStrength * directShadow;
                half3 specularTerm = _HighlightColor.rgb * specular;

                float fresnel = pow(saturate(1.0 - dot(normalWS, viewWS)), _RimPower);
                float rimLight = fresnel * _RimStrength * lerp(0.55, 1.0, shadowAtten);
                half3 rimTerm = _RimColor.rgb * rimLight;
                float backLight = pow(saturate(dot(-lightDirWS, normalWS)), 2.2) * _LightStrength * directShadow;
                half3 transmissionTerm = _TintColor.rgb * mainLight.color * backLight * 0.22;

                float centerMask = saturate(1.0 - radial);
                float bottomMask = smoothstep(0.28, 1.0, uv.y);
                float innerDarkMask = saturate(centerMask * 0.7 + (1.0 - bottomMask) * 0.5);
                half3 innerDarkened = lerp(baseColor, baseColor * _InnerDarkColor.rgb, _InnerDarkStrength * innerDarkMask);

                half3 lighting = ambientTerm + diffuseTerm + specularTerm + rimTerm + transmissionTerm;
                lighting = max(lighting, half3(0.05, 0.05, 0.05));

                half3 finalColor = innerDarkened * lighting;
                finalColor = lerp(finalColor, finalColor + _HighlightColor.rgb * 0.08, centerMask * 0.4);
                finalColor = min(finalColor, 1.35);
                finalColor = MixFog(finalColor, IN.fogCoord);

                float edgeStart = lerp(0.72, 0.52, _AlphaSoftness);
                float edgeMask = smoothstep(edgeStart, 1.0, radial);
                float coreMask = 1.0 - edgeMask;
                float bottomThickness = saturate(1.0 - uv.y);
                float coreAlpha = lerp(_InnerAlphaStrength * 0.92, min(1.0, _InnerAlphaStrength + 0.12), bottomThickness);
                float spatialAlpha = lerp(_EdgeAlphaStrength, coreAlpha, coreMask);
                spatialAlpha = lerp(spatialAlpha, spatialAlpha * (0.92 + 0.08 * centerMask), 0.5);
                half alpha = saturate(spriteColor.a * _TintColor.a * _Transparency * _AlphaStrength * spatialAlpha);
                alpha = max(alpha, spriteColor.a * 0.88);
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _RendererColor;
                float4 _Color;
                float4 _TintColor;
                float _AlphaStrength;
                float _EdgeAlphaStrength;
                float _InnerAlphaStrength;
                float _AlphaSoftness;
                float _JellyWobbleStrength;
                float _JellyWobbleSpeed;
                float _Transparency;
                float _ShadowAlphaCutoff;
            CBUFFER_END

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float timePhase = _Time.y * _JellyWobbleSpeed;
                float wobbleWave = sin((uv.y * 11.0 + timePhase) * 1.1) * cos((uv.x * 9.0 + timePhase * 0.7));
                float2 wobbleOffset = float2(wobbleWave * _JellyWobbleStrength, 0.0);
                uv += wobbleOffset;

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half4 spriteColor = tex * IN.color * _RendererColor * _Color;

                float2 centered = uv - 0.5;
                float radial = saturate(length(centered) / 0.7071);
                float edgeStart = lerp(0.72, 0.52, _AlphaSoftness);
                float edgeMask = smoothstep(edgeStart, 1.0, radial);
                float coreMask = 1.0 - edgeMask;
                float bottomThickness = saturate(1.0 - uv.y);
                float coreAlpha = lerp(_InnerAlphaStrength * 0.92, min(1.0, _InnerAlphaStrength + 0.12), bottomThickness);
                float spatialAlpha = lerp(_EdgeAlphaStrength, coreAlpha, coreMask);
                spatialAlpha = lerp(spatialAlpha, spatialAlpha * (0.92 + 0.08 * saturate(1.0 - radial)), 0.5);

                half shadowAlpha = saturate(spriteColor.a * _TintColor.a * _Transparency * _AlphaStrength * spatialAlpha);
                clip(shadowAlpha - _ShadowAlphaCutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}

