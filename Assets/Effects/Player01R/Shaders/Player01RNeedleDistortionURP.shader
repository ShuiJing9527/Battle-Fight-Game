Shader "YY/Player01/R/NeedleDistortionURP"
{
    Properties
    {
        _NoiseTex("Noise Texture", 2D) = "gray" {}
        _DistortionStrength("Distortion Strength", Range(0, 0.1)) = 0.02
        _NoiseScale("Noise Scale", Range(0.1, 16)) = 4
        _NoiseSpeed("Noise Speed", Range(-10, 10)) = 2
        _EdgeFade("Edge Fade", Range(0.01, 1)) = 0.45
        _Opacity("Opacity", Range(0, 1)) = 0.65
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
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                half _DistortionStrength;
                half _NoiseScale;
                half _NoiseSpeed;
                half _EdgeFade;
                half _Opacity;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _NoiseTex);
                output.normalWS = normalize(normalInputs.normalWS);
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float radial = length(centeredUv);
                float edgeMask = 1.0 - smoothstep(saturate(1.0 - _EdgeFade), 1.0, radial);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float facingMask = saturate(abs(dot(normalWS, viewDirWS)));
                float finalMask = saturate(edgeMask * facingMask);

                float2 flowUv = float2(input.uv.x * _NoiseScale, input.uv.y * _NoiseScale + _Time.y * _NoiseSpeed);
                float2 noiseA = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flowUv).rg * 2.0 - 1.0;
                float2 noiseB = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flowUv * 0.73 + float2(0.17, -_Time.y * _NoiseSpeed * 0.35)).rg * 2.0 - 1.0;
                float2 distortion = (noiseA * 0.7 + noiseB * 0.3) * _DistortionStrength * finalMask;

                float2 screenUv = input.screenPos.xy / max(0.0001, input.screenPos.w);
                float2 distortedUv = saturate(screenUv + distortion);
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, distortedUv);
                sceneColor.a = finalMask * _Opacity;
                return sceneColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
