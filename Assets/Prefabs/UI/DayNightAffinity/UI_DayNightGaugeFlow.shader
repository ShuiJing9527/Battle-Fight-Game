Shader "UI/DayNightGaugeFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
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
            fixed4 _TintColor;
            fixed4 _HighlightColor;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _FlowSpeed;
            float _FlowScale;
            float _DistortionStrength;
            float _HighlightStrength;
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
                fixed4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                float2 uv = IN.texcoord;
                float timeValue = _FlowTime;
                float2 noiseUv = float2(uv.x * _FlowScale + timeValue * _FlowSpeed, uv.y * 1.35);
                float2 noiseUv2 = float2(uv.x * (_FlowScale * 0.7) + timeValue * _FlowSpeed * 0.56 + 0.19, uv.y * 1.95);
                float noiseA = tex2D(_NoiseTex, noiseUv).r;
                float noiseB = tex2D(_NoiseTex, noiseUv2).r;
                float distortion = ((noiseA + noiseB) - 1.0) * _DistortionStrength;
                float2 sampleUv = float2(uv.x + distortion, uv.y);

                fixed4 sampledTex = tex2D(_MainTex, sampleUv) + _TextureSampleAdd;
                float highlightNoise = tex2D(_NoiseTex, float2(sampleUv.x * (_FlowScale * 1.2) + timeValue * _FlowSpeed * 1.4, sampleUv.y * 1.5)).r;
                float highlightBand = saturate(1.0 - abs(sampleUv.y * 2.0 - 1.0));
                float highlight = smoothstep(0.58, 0.92, highlightNoise) * highlightBand * _HighlightStrength;
                float energyBand = lerp(0.72, 1.28, noiseA * 0.55 + noiseB * 0.45);

                fixed3 baseColor = sampledTex.rgb * _TintColor.rgb * energyBand;
                fixed3 finalRgb = baseColor + (_HighlightColor.rgb * highlight);

                fixed4 color;
                color.rgb = finalRgb * IN.color.rgb;
                color.a = sampledTex.a * _TintColor.a * IN.color.a * _Alpha;

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
