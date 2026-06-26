Shader "BattleFight/DeathDissolveURP"
{
    Properties
    {
        [PerRendererData][MainTexture] _MainTex("Main Tex", 2D) = "white" {}
        [MainColor] _Color("Color", Color) = (1,1,1,1)
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _RendererColor("Renderer Color", Color) = (1,1,1,1)
        _DissolveAmount("Dissolve Amount", Range(0,1)) = 0
        _DissolveNoise("Dissolve Noise", Float) = 12
        _EdgeColor("Edge Color", Color) = (1,0.6,0.2,1)
        _EdgeWidth("Edge Width", Range(0.001,0.5)) = 0.12
        _EmissionStrength("Emission Strength", Range(0,10)) = 2.8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
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
                float4 _Color;
                float4 _BaseColor;
                float4 _RendererColor;
                float _DissolveAmount;
                float _DissolveNoise;
                float4 _EdgeColor;
                float _EdgeWidth;
                float _EmissionStrength;
            CBUFFER_END

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float2 uv)
            {
                float2 cell = floor(uv);
                float2 local = frac(uv);
                float2 smooth = local * local * (3.0 - 2.0 * local);

                float a = Hash12(cell);
                float b = Hash12(cell + float2(1.0, 0.0));
                float c = Hash12(cell + float2(0.0, 1.0));
                float d = Hash12(cell + float2(1.0, 1.0));

                float ab = lerp(a, b, smooth.x);
                float cd = lerp(c, d, smooth.x);
                return lerp(ab, cd, smooth.y);
            }

            float FractalNoise(float2 uv)
            {
                float total = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                for (int octave = 0; octave < 4; octave++)
                {
                    total += ValueNoise(uv * frequency) * amplitude;
                    frequency *= 2.0;
                    amplitude *= 0.5;
                }
                return saturate(total);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 baseColor = tex * _Color * _BaseColor * _RendererColor * input.color;

                float2 dissolveUV = input.uv * max(_DissolveNoise, 0.01);
                dissolveUV += _Time.y * float2(0.14, 0.09);
                float noise = FractalNoise(dissolveUV);

                float edgeWidth = max(_EdgeWidth, 0.0001);
                float dissolveThreshold = 1.0 - saturate(_DissolveAmount);
                float visibleMask = 1.0 - smoothstep(dissolveThreshold - edgeWidth, dissolveThreshold + edgeWidth, noise);
                float edgeMask = saturate(1.0 - abs(noise - dissolveThreshold) / edgeWidth);

                baseColor.rgb *= visibleMask;
                baseColor.rgb += _EdgeColor.rgb * edgeMask * _EmissionStrength;
                baseColor.a = saturate(baseColor.a * visibleMask + edgeMask * 0.15 * _EdgeColor.a);
                clip(baseColor.a - 0.01);
                return baseColor;
            }
            ENDHLSL
        }
    }
}
