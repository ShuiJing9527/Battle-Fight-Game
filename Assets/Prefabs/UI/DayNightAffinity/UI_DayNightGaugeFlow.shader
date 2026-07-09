Shader "UI/DayNightGaugeFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        _DeepColor ("Deep Color", Color) = (0.2,0.2,0.3,1)
        _TintColor ("Tint Color", Color) = (1,1,1,1)
        _HighlightColor ("Highlight Color", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        _FlowSpeed ("Flow Speed", Float) = 0.2
        _FlowScale ("Flow Scale", Float) = 3.2
        _DistortionStrength ("Distortion Strength", Range(0,0.2)) = 0.03
        _HighlightStrength ("Highlight Strength", Range(0,1)) = 0.22
        _HighlightWidth ("Highlight Width", Range(0.01,0.4)) = 0.12
        _GradientDirection ("Gradient Direction", Float) = 1
        _GradientPower ("Gradient Power", Range(0.5,4)) = 1.8
        _EdgeFade ("Edge Fade", Range(0,1)) = 0.8
        _EdgeShade ("Edge Shade", Range(0,1)) = 0.24
        _CoreGlow ("Core Glow", Range(0,2)) = 0.36
        _VerticalGlow ("Vertical Glow", Range(0,2)) = 0.28
        _FlowTime ("Flow Time", Float) = 0
        _Alpha ("Alpha", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            fixed4 _DeepColor;
            fixed4 _TintColor;
            fixed4 _HighlightColor;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _FlowSpeed;
            float _FlowScale;
            float _DistortionStrength;
            float _HighlightStrength;
            float _HighlightWidth;
            float _GradientDirection;
            float _GradientPower;
            float _EdgeFade;
            float _EdgeShade;
            float _CoreGlow;
            float _VerticalGlow;
            float _FlowTime;
            float _Alpha;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float timeValue = max(_FlowTime, _Time.y);
                float2 noiseUvA = float2(uv.x * (_FlowScale * 0.6) + timeValue * _FlowSpeed * 0.18, uv.y * 1.25 + timeValue * 0.04);
                float2 noiseUvB = float2(uv.x * (_FlowScale * 0.38) - timeValue * _FlowSpeed * 0.11 + 0.37, uv.y * 1.8 - timeValue * 0.03);
                float2 noiseSample = tex2D(_NoiseTex, noiseUvA).rg * 2.0 - 1.0;
                float2 noiseSampleSecondary = tex2D(_NoiseTex, noiseUvB).rg * 2.0 - 1.0;
                float2 distortionVector = (noiseSample * 0.75 + noiseSampleSecondary * 0.25) * _DistortionStrength;
                float2 sampleUv = uv + float2(distortionVector.x * 0.65, distortionVector.y * 0.1);

                fixed4 sampledTex = tex2D(_MainTex, sampleUv) + _TextureSampleAdd;

                float baseShimmerNoise = tex2D(_NoiseTex, float2(sampleUv.x * (_FlowScale * 0.48) + timeValue * _FlowSpeed * 0.09, sampleUv.y * 1.1)).r;
                float baseShimmer = lerp(0.96, 1.06, baseShimmerNoise);

                float flowNoiseA = tex2D(_NoiseTex, float2(sampleUv.x * (_FlowScale * 0.75) + timeValue * _FlowSpeed * 0.32, sampleUv.y * 1.45)).r;
                float flowNoiseB = tex2D(_NoiseTex, float2(sampleUv.x * (_FlowScale * 0.52) + timeValue * _FlowSpeed * 0.21 + 0.43, sampleUv.y * 1.9)).r;

                float flowCoordA = frac(sampleUv.x * _FlowScale - timeValue * _FlowSpeed + flowNoiseA * 0.16);
                float flowCoordB = frac(sampleUv.x * (_FlowScale * 0.62) - timeValue * _FlowSpeed * 0.72 + flowNoiseB * 0.21 + 0.35);
                float halfWidth = max(0.01, _HighlightWidth);

                float highlightA = 1.0 - smoothstep(0.0, halfWidth, abs(flowCoordA - 0.5));
                float highlightB = 1.0 - smoothstep(0.0, halfWidth * 1.35, abs(flowCoordB - 0.5));
                float edgeSoftBand = smoothstep(0.02, 0.28, uv.y) * (1.0 - smoothstep(0.72, 0.98, uv.y));
                float highlight = saturate((highlightA * 0.9 + highlightB * 0.45) * edgeSoftBand) * _HighlightStrength;

                float gradientPower = max(0.5, _GradientPower);
                float directionalUv = uv.x;
                float longitudinalGlow = smoothstep(0.0, 0.78, directionalUv);
                float membraneBand = 1.0 - abs(uv.y * 2.0 - 1.0);
                membraneBand = smoothstep(0.0, 1.0, membraneBand);
                float verticalGlowMask = lerp(0.72, 1.0, membraneBand);

                float toneMask;
                float alphaMask;
                if (_GradientDirection < 0.5)
                {
                    float leftFade = pow(saturate(1.0 - uv.x), gradientPower);
                    toneMask = saturate(0.18 + leftFade * 0.82);
                    alphaMask = saturate(leftFade * _EdgeFade) * verticalGlowMask;
                }
                else if (_GradientDirection < 1.5)
                {
                    float rightFade = pow(saturate(uv.x), gradientPower);
                    toneMask = saturate(0.18 + rightFade * 0.82);
                    alphaMask = saturate(rightFade * _EdgeFade) * verticalGlowMask;
                }
                else
                {
                    float coverLongitudinal = pow(saturate(0.35 + uv.x * 0.65), max(0.75, gradientPower * 0.6));
                    toneMask = saturate(0.28 + coverLongitudinal * 0.34 + membraneBand * 0.58);
                    alphaMask = lerp(saturate(_EdgeFade), 1.0, membraneBand * 0.58 + coverLongitudinal * 0.42);
                }

                fixed3 layeredTint = lerp(_DeepColor.rgb, _TintColor.rgb, toneMask);
                float edgeToInner = lerp(1.0 - _EdgeShade, 1.0 + (_CoreGlow * 0.4), toneMask);
                float centerLift = 1.0 + membraneBand * _VerticalGlow;

                fixed3 baseColor = sampledTex.rgb * layeredTint * baseShimmer * edgeToInner * centerLift;
                fixed3 finalRgb = baseColor + (_HighlightColor.rgb * highlight);

                fixed4 color;
                color.rgb = finalRgb * IN.color.rgb;
                color.a = sampledTex.a * _TintColor.a * IN.color.a * _Alpha * alphaMask;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
