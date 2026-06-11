Shader "Spine/PlayerLit"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)

        _LightInfluence ("Light Influence", Range(0, 2)) = 0.45
        _MinBrightness ("Min Brightness", Range(0, 2)) = 0.55
        _MaxBrightness ("Max Brightness", Range(0, 2)) = 1.15
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.45
        _LightColorInfluence ("Light Color Influence", Range(0, 1)) = 0.35
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
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
            #pragma target 3.0
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
                float4 color : COLOR;
                float fogFactor : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _LightInfluence;
                float _MinBrightness;
                float _MaxBrightness;
                float _ShadowStrength;
                float _LightColorInfluence;
                float _AmbientStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 spriteColor = texColor * IN.color * _Color;

                float3 normalWS = normalize(TransformObjectToWorldNormal(float3(0.0, 0.0, 1.0)));
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float lightLuma = dot(mainLight.color, float3(0.2126, 0.7152, 0.0722));
                float lightAmount = saturate(lightLuma * _LightInfluence);
                float brightness = lerp(_MinBrightness, _MaxBrightness, lightAmount);
                float shadowAttenuation = saturate(mainLight.shadowAttenuation);
                float shadowFactor = lerp(1.0 - _ShadowStrength, 1.0, shadowAttenuation);
                float ambientShadowFactor = lerp(1.0 - _ShadowStrength * 0.85, 1.0, shadowAttenuation);
                brightness = clamp(brightness * shadowFactor, _MinBrightness * 0.85, _MaxBrightness);

                half3 lightTint = lerp(half3(1.0, 1.0, 1.0), mainLight.color, _LightColorInfluence);
                half3 ambientProbe = max(SampleSH(normalWS), half3(0.16, 0.18, 0.21));
                half3 ambient = lerp(half3(1.0, 1.0, 1.0), ambientProbe, saturate(_AmbientStrength)) * ambientShadowFactor;

                half3 finalRgb = spriteColor.rgb * brightness;
                finalRgb = finalRgb * lightTint;
                finalRgb = finalRgb * ambient;
                finalRgb = min(finalRgb, half3(_MaxBrightness, _MaxBrightness, _MaxBrightness));
                finalRgb = MixFog(finalRgb, IN.fogFactor);

                return half4(finalRgb, spriteColor.a);
            }
            ENDHLSL
        }
    }
}
