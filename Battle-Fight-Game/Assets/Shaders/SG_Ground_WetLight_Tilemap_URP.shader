Shader "UnderTheStars/Tilemap/Ground Wet Light URP"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1, 1, 1, 1)
        _WetColor ("Wet Color", Color) = (0.65, 0.95, 0.8, 1)
        _WetStrength ("Wet Strength", Range(0, 0.2)) = 0.035
        _WetFlowSpeed ("Wet Flow Speed", Range(0, 1)) = 0.04
        _WetFlowScale ("Wet Flow Scale", Range(0.1, 16)) = 2.5
        _LightPulseStrength ("Light Pulse Strength", Range(0, 0.1)) = 0.01
        _DepthColor ("Depth Color", Color) = (0.28, 0.48, 0.28, 1)
        _DepthStrength ("Depth Strength", Range(0, 0.5)) = 0.08
        _DepthScale ("Depth Scale", Range(0.05, 8)) = 0.55
        _DepthFlowSpeed ("Depth Flow Speed", Range(0, 0.5)) = 0.01
        _Smoothness ("Smoothness", Range(0, 1)) = 0.22
        _Metallic ("Metallic", Range(0, 1)) = 0

        [HideInInspector] _Color ("Sprite Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                UNITY_SKINNED_VERTEX_INPUTS
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _TintColor;
                half4 _WetColor;
                half _WetStrength;
                half _WetFlowSpeed;
                half _WetFlowScale;
                half _LightPulseStrength;
                half4 _DepthColor;
                half _DepthStrength;
                half _DepthScale;
                half _DepthFlowSpeed;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END

            half WetNoise(float2 worldUV, half timeValue)
            {
                half waveA = sin(worldUV.x + timeValue);
                half waveB = sin(worldUV.y * 1.37 - timeValue * 0.82);
                half waveC = sin((worldUV.x + worldUV.y) * 0.43 + timeValue * 0.37);
                return saturate((waveA * waveB * 0.5 + waveC * 0.25) + 0.55);
            }

            half DepthNoise(float2 worldUV, half timeValue)
            {
                half waveA = sin(worldUV.x * 0.71 + timeValue);
                half waveB = sin(worldUV.y * 1.19 - timeValue * 0.43);
                half waveC = sin((worldUV.x - worldUV.y) * 0.37 + timeValue * 0.21);
                return saturate(waveA * 0.32 + waveB * 0.22 + waveC * 0.18 + 0.55);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                UNITY_SKINNED_VERTEX_COMPUTE(input);

                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 baseColor = sprite * input.color * _TintColor;

                half2 wetUV = input.positionWS.xz * max(_WetFlowScale, 0.001);
                half wet = WetNoise(wetUV, _Time.y * _WetFlowSpeed) * _WetStrength;
                half pulse = 1.0h + sin(_Time.y * _WetFlowSpeed) * _LightPulseStrength;
                half depthMask = DepthNoise(input.positionWS.xz * max(_DepthScale, 0.001), _Time.y * _DepthFlowSpeed) * _DepthStrength;

                half3 normalWS = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half mainLightAmount = saturate(abs(dot(normalWS, mainLight.direction))) * 0.75 + 0.25;
                half3 ambient = SampleSH(normalWS);
                half3 directLighting = mainLight.color * mainLightAmount * mainLight.shadowAttenuation;
                half3 lighting = max(ambient + directLighting, half3(0.22, 0.22, 0.22));

                half3 wetColor = baseColor.rgb + _WetColor.rgb * wet;
                half3 layeredColor = lerp(wetColor, wetColor * _DepthColor.rgb, depthMask);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half specPower = lerp(8.0h, 96.0h, _Smoothness);
                half specular = pow(saturate(abs(dot(normalWS, halfDir))), specPower) * _Smoothness * (wet + 0.02) * mainLight.shadowAttenuation;
                half3 litColor = layeredColor * lighting * pulse + mainLight.color * specular;

                return half4(litColor, baseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
