Shader "Shader Graphs/SG_AirWallRipple"
{
    Properties
    {
        _RippleCount ("Ripple Count", Float) = 20
        _RingWidth ("Ring Width", Float) = 0.7
        _RippleColor ("Ripple Color", Color) = (0.72, 0.92, 1, 1)
        _Intensity ("Intensity", Float) = 1.2
        _Alpha ("Alpha", Range(0, 1)) = 0.6
        _Speed ("Speed", Float) = 1.5
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
            Name "AirWallRipple"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _RippleCount;
                float _RingWidth;
                float4 _RippleColor;
                float _Intensity;
                float _Alpha;
                float _Speed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv - 0.5;
                float distanceFromCenter = length(centeredUv) * 2.0;

                float animatedDistance = distanceFromCenter - frac(_Time.y * _Speed) * 0.35;
                float wave = sin(animatedDistance * _RippleCount);
                float ring = 1.0 - abs(wave);
                ring = smoothstep(1.0 - saturate(_RingWidth), 1.0, ring);

                float outerFade = 1.0 - smoothstep(0.82, 1.0, distanceFromCenter);
                float innerFade = smoothstep(0.04, 0.12, distanceFromCenter);
                float mask = ring * outerFade * innerFade;

                float3 color = _RippleColor.rgb * _Intensity * mask;
                float alpha = mask * _Alpha;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
