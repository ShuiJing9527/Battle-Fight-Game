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
        _MistTex ("Mist Texture", 2D) = "gray" {}
        _MistColor ("Mist Color", Color) = (0.78, 0.9, 0.86, 1)
        _MistStrength ("Mist Strength", Range(0, 1)) = 0.2
        _MistScale ("Mist Scale", Range(0.05, 8)) = 0.25
        _MistFlowSpeed ("Mist Flow Speed", Range(0, 0.2)) = 0.015
        _MistDirection ("Mist Direction", Vector) = (0.8, 0.2, 0, 0)
        _DebugWetOnly ("Debug Wet Only", Range(0, 1)) = 0
        _DebugMistOnly ("Debug Mist Only", Range(0, 1)) = 0
        _GrassSwayStrength ("Grass Sway Strength", Range(0, 0.02)) = 0
        _GrassSwaySpeed ("Grass Sway Speed", Range(0, 3)) = 0.5
        _GrassSwayScale ("Grass Sway Scale", Range(0.1, 10)) = 3
        _GrassSwayDirection ("Grass Sway Direction", Vector) = (1, 0.3, 0, 0)
        _DebugSwayOnly ("Debug Sway Only", Range(0, 1)) = 0
        _LightStrength ("Light Strength", Range(0, 1)) = 0.25
        _Smoothness ("Smoothness", Range(0, 1)) = 0.12
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
            TEXTURE2D(_MistTex);
            SAMPLER(sampler_MistTex);

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
                half4 _MistColor;
                half _MistStrength;
                half _MistScale;
                half _MistFlowSpeed;
                half4 _MistDirection;
                half _DebugWetOnly;
                half _DebugMistOnly;
                half _GrassSwayStrength;
                half _GrassSwaySpeed;
                half _GrassSwayScale;
                half4 _GrassSwayDirection;
                half _DebugSwayOnly;
                half _LightStrength;
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
                half2 swayDir = normalize(_GrassSwayDirection.xy + half2(1e-4h, 1e-4h));
                half swayTime = _Time.y * _GrassSwaySpeed;
                half swayScale = max(_GrassSwayScale, 0.001);
                half2 swayBaseUV = input.positionWS.xz * swayScale;
                half swayNoiseA = sin(dot(swayBaseUV, swayDir) + swayTime);
                half swayNoiseB = sin(dot(swayBaseUV, half2(-swayDir.y, swayDir.x)) * 1.37h - swayTime * 0.81h);
                half swayNoise = (swayNoiseA * 0.65h + swayNoiseB * 0.35h);
                half swayNoise2 = sin((swayBaseUV.x + swayBaseUV.y) * 0.73h + swayTime * 1.19h);
                half2 swayOrtho = half2(-swayDir.y, swayDir.x);
                half2 swayVector = swayDir * swayNoise + swayOrtho * swayNoise2 * 0.45h;
                half2 swayOffset = swayVector * _GrassSwayStrength;
                half2 mainUV = input.uv + swayOffset;

                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV);
                half4 baseColor = sprite * input.color * _TintColor;

                half safeWetScale = max(_WetFlowScale, 0.001);
                half wetTime = _Time.y * _WetFlowSpeed;
                half2 wetFlowA = half2(0.23h, -0.17h) * wetTime;
                half2 wetFlowB = half2(-0.13h, 0.19h) * wetTime;
                half2 wetUV = input.positionWS.xz * safeWetScale;
                half wetA = WetNoise(wetUV + wetFlowA, wetTime * 1.25h);
                half wetB = WetNoise(wetUV * 1.63h + wetFlowB, -wetTime * 0.93h);
                half wetMask = saturate((wetA * 0.68h + wetB * 0.32h - 0.42h) * 1.75h);
                half wet = wetMask * _WetStrength;
                half pulse = 1.0h + sin(wetTime * 3.5h) * _LightPulseStrength;
                half depthMask = DepthNoise(input.positionWS.xz * max(_DepthScale, 0.001), _Time.y * _DepthFlowSpeed) * _DepthStrength;
                half2 mistDir = normalize(_MistDirection.xy + half2(1e-4h, 1e-4h));
                half mistTime = _Time.y * _MistFlowSpeed;
                half2 mistUV = input.positionWS.xz * max(_MistScale, 0.001) + mistDir * mistTime;
                half3 mistTex = SAMPLE_TEXTURE2D(_MistTex, sampler_MistTex, mistUV).rgb;
                half mistMask = saturate(dot(mistTex, half3(0.299h, 0.587h, 0.114h)));
                mistMask = smoothstep(0.2h, 0.85h, mistMask);

                half3 normalWS = input.normalWS;
                half normalLenSq = dot(normalWS, normalWS);
                normalWS = (normalLenSq > 1e-4h) ? normalize(normalWS) : half3(0.0h, 1.0h, 0.0h);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 wetColor = baseColor.rgb + _WetColor.rgb * wet;
                half3 layeredColor = lerp(wetColor, wetColor * _DepthColor.rgb, depthMask);
                half mistAmount = saturate(_MistStrength) * mistMask * 0.45h;
                half3 mistTint = _MistColor.rgb * lerp(0.75h, 1.1h, mistMask);
                layeredColor = lerp(layeredColor, layeredColor + mistTint, mistAmount);
                layeredColor += _WetColor.rgb * wetMask * (_WetStrength * 0.25h);
                half3 unlitColor = layeredColor * pulse;
                half3 ambient = SampleSH(normalWS);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lightColor = mainLight.color * mainLight.shadowAttenuation;
                half3 diffuse = lightColor * ndotl;
                half3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                half3 halfDir = normalize(mainLight.direction + viewDir);
                half specPower = lerp(8.0h, 64.0h, _Smoothness);
                half specular = pow(saturate(dot(normalWS, halfDir)), specPower) * _Smoothness;
                half3 litColor = unlitColor * (ambient + diffuse) + specular * lightColor;
                half3 finalColor = lerp(unlitColor, litColor, saturate(_LightStrength));

                if (_DebugWetOnly > 0.5h)
                {
                    half debugValue = wetMask;
                    return half4(debugValue.xxx, baseColor.a);
                }
                if (_DebugMistOnly > 0.5h)
                {
                    return half4(mistMask.xxx, baseColor.a);
                }
                if (_DebugSwayOnly > 0.5h)
                {
                    half2 debugOffset = swayOffset / max(_GrassSwayStrength, 1e-4h);
                    half3 debugSway = half3(debugOffset.x * 0.5h + 0.5h, debugOffset.y * 0.5h + 0.5h, swayNoise * 0.5h + 0.5h);
                    return half4(debugSway, baseColor.a);
                }

                return half4(finalColor, baseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
