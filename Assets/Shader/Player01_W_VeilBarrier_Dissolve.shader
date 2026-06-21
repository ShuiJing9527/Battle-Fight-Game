Shader "Effects/Player01/WVeilBarrierDissolve"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _Color("Tint Color", Color) = (1,1,1,1)
        _Alpha("Alpha", Range(0,1)) = 0.55

        _ParallaxMap("Parallax Map", 2D) = "gray" {}
        _ParallaxStrength("Parallax Strength", Range(0,0.2)) = 0.03
        _ParallaxCenter("Parallax Center", Range(0,1)) = 0.5

        _DistortionTex("Distortion Texture", 2D) = "gray" {}
        _DistortionStrength("Distortion Strength", Range(0,0.2)) = 0.02
        _DistortionSpeedX("Distortion Speed X", Float) = 0.08
        _DistortionSpeedY("Distortion Speed Y", Float) = 0.04
        _MainTexSpeedX("Main Tex Speed X", Float) = 0.015
        _MainTexSpeedY("Main Tex Speed Y", Float) = 0.005
        _MainTexFlowStrength("Main Tex Flow Strength", Float) = 1
        _ParallaxSpeedX("Parallax Speed X", Float) = -0.01
        _ParallaxSpeedY("Parallax Speed Y", Float) = 0.008
        _SurfaceDistortionStrength("Surface Distortion Strength", Float) = 0.18
        _SurfaceDistortionTiling("Surface Distortion Tiling", Float) = 4
        _SurfaceDistortionSpeedX("Surface Distortion Speed X", Float) = 0.08
        _SurfaceDistortionSpeedY("Surface Distortion Speed Y", Float) = 0.05
        _DetailDistortionTex("Detail Distortion Tex", 2D) = "gray" {}
        _DetailDistortionStrength("Detail Distortion Strength", Float) = 0.08
        _DetailDistortionTiling("Detail Distortion Tiling", Float) = 9
        _DetailDistortionSpeedX("Detail Distortion Speed X", Float) = -0.12
        _DetailDistortionSpeedY("Detail Distortion Speed Y", Float) = 0.10

        _SceneBlend("Scene Blend", Range(0,1)) = 0.4
        _RefractionStrength("Refraction Strength", Range(0,1)) = 0.08
        _RefractionTint("Refraction Tint", Color) = (1,1,1,1)
        _TintStrength("Tint Strength", Range(0,1)) = 0.12
        _RimColor("Rim Color", Color) = (0.48,0.55,1,1)
        _RimIntensity("Rim Intensity", Range(0,5)) = 2.2
        _RimPower("Rim Power", Range(0.5,8)) = 3
        _RimDistortionBoost("Rim Distortion Boost", Float) = 1.5
        _CenterDistortionFade("Center Distortion Fade", Range(0,1)) = 0.5
        _PulseStrength("Pulse Strength", Range(0,0.2)) = 0.05
        _PulseSpeed("Pulse Speed", Float) = 1.2

        _DissolveTex("Dissolve Texture", 2D) = "gray" {}
        _DissolveAmount("Dissolve Amount", Range(0,1)) = 0
        _DissolveWidth("Dissolve Width", Range(0,1)) = 0.06
        _DissolveSoftness("Dissolve Softness", Range(0,1)) = 0.02
        _DissolveEdgeColor("Dissolve Edge Color", Color) = (0.65,0.95,1,1)
        _DissolveEdgeIntensity("Dissolve Edge Intensity", Range(0,5)) = 2.5
        _OpacityMultiplier("Opacity Multiplier", Range(0,1)) = 1
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
            Name "WVeilBarrierDissolve"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_ParallaxMap);
            SAMPLER(sampler_ParallaxMap);
            TEXTURE2D(_DistortionTex);
            SAMPLER(sampler_DistortionTex);
            TEXTURE2D(_DetailDistortionTex);
            SAMPLER(sampler_DetailDistortionTex);
            TEXTURE2D(_DissolveTex);
            SAMPLER(sampler_DissolveTex);
            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Alpha;
                float4 _ParallaxMap_ST;
                float _ParallaxStrength;
                float _ParallaxCenter;
                float4 _DistortionTex_ST;
                float _DistortionStrength;
                float _DistortionSpeedX;
                float _DistortionSpeedY;
                float _MainTexSpeedX;
                float _MainTexSpeedY;
                float _MainTexFlowStrength;
                float _ParallaxSpeedX;
                float _ParallaxSpeedY;
                float _SurfaceDistortionStrength;
                float _SurfaceDistortionTiling;
                float _SurfaceDistortionSpeedX;
                float _SurfaceDistortionSpeedY;
                float4 _DetailDistortionTex_ST;
                float _DetailDistortionStrength;
                float _DetailDistortionTiling;
                float _DetailDistortionSpeedX;
                float _DetailDistortionSpeedY;
                float _SceneBlend;
                float _RefractionStrength;
                float4 _RefractionTint;
                float _TintStrength;
                float4 _RimColor;
                float _RimIntensity;
                float _RimPower;
                float _RimDistortionBoost;
                float _CenterDistortionFade;
                float _PulseStrength;
                float _PulseSpeed;
                float4 _DissolveTex_ST;
                float _DissolveAmount;
                float _DissolveWidth;
                float _DissolveSoftness;
                float4 _DissolveEdgeColor;
                float _DissolveEdgeIntensity;
                float _OpacityMultiplier;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float3 tangentWS : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 SafeNormalize3(float3 v)
            {
                return v / max(length(v), 1e-5);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionHCS = positionInputs.positionCS;
                output.screenPos = ComputeScreenPos(output.positionHCS);
                output.uv = input.uv;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = SafeNormalize3(normalInputs.normalWS);
                output.tangentWS = SafeNormalize3(normalInputs.tangentWS);
                if (length(output.tangentWS) < 1e-5)
                {
                    float3 helperAxis = abs(output.normalWS.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
                    output.tangentWS = SafeNormalize3(cross(helperAxis, output.normalWS));
                }
                output.bitangentWS = SafeNormalize3(cross(output.normalWS, output.tangentWS));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 mainUV = TRANSFORM_TEX(input.uv, _MainTex);
                float2 parallaxUV = TRANSFORM_TEX(input.uv, _ParallaxMap);
                float2 distortionUV = TRANSFORM_TEX(input.uv, _DistortionTex);
                float2 detailDistortionUV = TRANSFORM_TEX(input.uv, _DetailDistortionTex);
                float2 dissolveUV = TRANSFORM_TEX(input.uv, _DissolveTex);

                mainUV += float2(_MainTexSpeedX, _MainTexSpeedY) * _Time.y * _MainTexFlowStrength;
                parallaxUV += float2(_ParallaxSpeedX, _ParallaxSpeedY) * _Time.y;
                distortionUV += _Time.y * float2(_DistortionSpeedX, _DistortionSpeedY);
                distortionUV *= _SurfaceDistortionTiling;
                distortionUV += _Time.y * float2(_SurfaceDistortionSpeedX, _SurfaceDistortionSpeedY);
                detailDistortionUV *= _DetailDistortionTiling;
                detailDistortionUV += _Time.y * float2(_DetailDistortionSpeedX, _DetailDistortionSpeedY);

                float3 viewDirWS = SafeNormalize3(GetWorldSpaceViewDir(input.positionWS));
                float3 viewDirTS = float3(
                    dot(viewDirWS, input.tangentWS),
                    dot(viewDirWS, input.bitangentWS),
                    dot(viewDirWS, input.normalWS)
                );
                float viewZ = max(abs(viewDirTS.z), 0.2);

                float height = SAMPLE_TEXTURE2D(_ParallaxMap, sampler_ParallaxMap, parallaxUV).r;
                float parallaxAmount = (height - _ParallaxCenter) * _ParallaxStrength;
                mainUV += (viewDirTS.xy / viewZ) * parallaxAmount;

                float4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV);
                float mainAlpha = saturate(mainTex.a * _Alpha);

                float2 surfaceSample = SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, distortionUV).rg * 2.0 - 1.0;
                float2 detailSample = SAMPLE_TEXTURE2D(_DetailDistortionTex, sampler_DetailDistortionTex, detailDistortionUV).rg * 2.0 - 1.0;
                float2 combinedDistort = surfaceSample * _DistortionStrength + detailSample * _DetailDistortionStrength;

                float rimMask = pow(1.0 - saturate(abs(dot(viewDirWS, input.normalWS))), max(_RimPower, 0.001));
                float centerFade = saturate(1.0 - rimMask * (1.0 - _CenterDistortionFade));
                float rimBoost = lerp(1.0, _RimDistortionBoost, rimMask);
                combinedDistort *= rimBoost * lerp(_CenterDistortionFade, 1.0, rimMask);

                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                mainUV += combinedDistort * (0.05 + rimMask * 0.02);
                parallaxUV += combinedDistort * (0.07 + rimMask * 0.03);
                screenUV += combinedDistort * _RefractionStrength;
                float3 sceneColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV).rgb;

                float3 baseColor = mainTex.rgb * _Color.rgb;
                float sceneMix = saturate(_SceneBlend * mainAlpha * _RefractionStrength);
                float3 color = lerp(baseColor, sceneColor * _RefractionTint.rgb, sceneMix);

                float tintPulse = (surfaceSample.x + surfaceSample.y + detailSample.x + detailSample.y) * 0.25;
                color += baseColor * _TintStrength * tintPulse * mainAlpha;

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;
                float rim = rimMask;
                color += _RimColor.rgb * rim * _RimIntensity * pulse;

                float dissolveNoise = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, dissolveUV).r;
                float visibleMask = smoothstep(_DissolveAmount - _DissolveSoftness, _DissolveAmount + _DissolveSoftness, dissolveNoise);
                float edgeMask = smoothstep(_DissolveAmount, _DissolveAmount + _DissolveWidth, dissolveNoise) - visibleMask;
                edgeMask = saturate(edgeMask);

                float alpha = saturate(mainAlpha * visibleMask * _OpacityMultiplier);
                color += _DissolveEdgeColor.rgb * edgeMask * _DissolveEdgeIntensity;
                alpha = saturate(alpha + edgeMask * 0.08);

                color += sceneColor * rim * 0.08;
                alpha = saturate(alpha + rim * 0.08);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
