Shader "Player02/HaloRainbowFlow"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        _ColorTint ("Color Tint", Color) = (1,1,1,1)
        _FlowSpeed ("Flow Speed", Float) = 0.4
        _RainbowStrength ("Rainbow Strength", Range(0, 1)) = 0.35
        _GlowStrength ("Glow Strength", Float) = 1.2
        _Alpha ("Alpha", Range(0, 1)) = 0.75
        _UseAdditive ("Use Additive", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _ColorTint;
                float _FlowSpeed;
                float _RainbowStrength;
                float _GlowStrength;
                float _Alpha;
                float _UseAdditive;
            CBUFFER_END

            static const float PI2 = 6.28318530718;

            float3 HSVToRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexPositionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexPositionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 tint = _ColorTint * input.color;
                float alpha = saturate(tex.a * tint.a * _Alpha);

                float2 centered = input.uv * 2.0 - 1.0;
                float angle01 = frac(atan2(centered.y, centered.x) / PI2 + _Time.y * _FlowSpeed);
                float3 rainbow = HSVToRGB(float3(angle01, saturate(_RainbowStrength), 1.0));

                float radial = saturate(1.0 - length(centered));
                float glow = pow(radial, 1.75) * _GlowStrength;

                float3 baseRgb = tex.rgb * tint.rgb;
                float3 flowRgb = lerp(baseRgb, rainbow, saturate(_RainbowStrength));
                flowRgb += rainbow * glow * 0.35;

                if (_UseAdditive > 0.5)
                {
                    flowRgb += rainbow * glow * 0.55;
                }

                flowRgb = max(flowRgb, 0.0);
                return half4(flowRgb, alpha);
            }
            ENDHLSL
        }
    }
}
