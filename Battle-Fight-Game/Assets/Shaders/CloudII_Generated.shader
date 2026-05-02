Shader "Custom/CloudII_Generated"
{
    Properties
    {
        _("旋转投影", Vector, 4) = (1, 0, 0, 0)
        _Scale("Scale", Float) = 10
        __1("云流动速度", Float) = 0.1
        __2("云层高度", Float) = 100
        __3("云流动方向", Vector, 4) = (0, 1, -1, 1)
        [HDR]_Color("顶层云Color", Color) = (1, 1, 1, 1)
        [HDR]_Color_1("底层云Color", Color) = (0.1, 0.1, 0.1, 1)
        _Nome("Nome 起伏度", Float) = 0
        _Edge("Edge 起伏度", Float) = 0
        __4("蓬松度", Float) = 1
        __5("第二层级", Float) = 5
        __6("云层移动速度", Float) = 0.2
        __7("云层区域块面", Float) = 0
        __8("发光强度", Float) = 0
        __9("曲率半径", Float) = 0
        __10("光照角度", Float) = 1
        _Fresnel("Fresnel", Float) = 1
        __11("云密度", Float) = 100
        [HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "UniversalMaterialType" = "Unlit"
            "Queue"="Transparent"
            "DisableBatching"="False"
            "ShaderGraphShader"="true"
            "ShaderGraphTargetId"="UniversalUnlitSubTarget"
        }
        Pass
        {
            Name "Universal Forward"
            Tags
            {
                // LightMode: <None>
            }
        
        // Render State
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma multi_compile_instancing
        #pragma instancing_options renderinglayer
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma multi_compile _ LIGHTMAP_ON
        #pragma multi_compile _ DIRLIGHTMAP_COMBINED
        #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
        #pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING
        #pragma shader_feature _ _SAMPLE_GI
        #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
        #pragma multi_compile_fragment _ DEBUG_DISPLAY
        #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define GRAPH_VERTEX_USES_TIME_PARAMETERS_INPUT
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_UNLIT
        #define _FOG_FRAGMENT 1
        #define _SURFACE_TYPE_TRANSPARENT 1
        #define REQUIRE_DEPTH_TEXTURE
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 WorldSpaceViewDirection;
             float3 WorldSpacePosition;
             float4 ScreenPosition;
             float2 NDCPosition;
             float2 PixelPosition;
             float3 TimeParameters;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS : INTERP0;
             float3 normalWS : INTERP1;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _;
        float _Scale;
        float __1;
        float __2;
        float4 __3;
        float4 _Color;
        float4 _Color_1;
        float _Nome;
        float _Edge;
        float __4;
        float __5;
        float __6;
        float __7;
        float __8;
        float __9;
        float __10;
        float _Fresnel;
        float __11;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        
        // Graph Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl"
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Distance_float3(float3 A, float3 B, out float Out)
        {
            Out = distance(A, B);
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_Power_float(float A, float B, out float Out)
        {
            Out = pow(A, B);
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        void Unity_Rotate_About_Axis_Degrees_float(float3 In, float3 Axis, float Rotation, out float3 Out)
        {
            Rotation = radians(Rotation);
            float s, c;
            sincos(Rotation, s, c);
            Axis = normalize(Axis);
            Out = In * c + cross(Axis, In) * s + Axis * dot(Axis, In) * (1 - c);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
        {
            Out = UV * Tiling + Offset;
        }
        
        float2 Unity_GradientNoise_Deterministic_Dir_float(float2 p)
        {
            float x; Hash_Tchou_2_1_float(p, x);
            return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
        }
        
        void Unity_GradientNoise_Deterministic_float (float2 UV, float3 Scale, out float Out)
        {
            float2 p = UV * Scale.xy;
            float2 ip = floor(p);
            float2 fp = frac(p);
            float d00 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip), fp);
            float d01 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(0, 1)), fp - float2(0, 1));
            float d10 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 0)), fp - float2(1, 0));
            float d11 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 1)), fp - float2(1, 1));
            fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
            Out = lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Saturate_float(float In, out float Out)
        {
            Out = saturate(In);
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
        {
            Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
        }
        
        void Unity_Absolute_float(float In, out float Out)
        {
            Out = abs(In);
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
        {
            Out = lerp(A, B, T);
        }
        
        void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
        {
            Out = pow((1.0 - saturate(dot(normalize(Normal), ViewDir))), Power);
        }
        
        void Unity_Add_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A + B;
        }
        
        void Unity_SceneDepth_Eye_float(float4 UV, out float Out)
        {
            if (unity_OrthoParams.w == 1.0)
            {
                Out = LinearEyeDepth(ComputeWorldSpacePosition(UV.xy, SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), UNITY_MATRIX_I_VP), UNITY_MATRIX_V);
            }
            else
            {
                Out = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
            }
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float;
            Unity_Distance_float3(SHADERGRAPH_OBJECT_POSITION, IN.WorldSpacePosition, _Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float);
            float _Property_6e77d3e8ac5a45cb836f7d04202a85a7_Out_0_Float = __9;
            float _Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float;
            Unity_Divide_float(_Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float, _Property_6e77d3e8ac5a45cb836f7d04202a85a7_Out_0_Float, _Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float);
            float _Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float;
            Unity_Power_float(_Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float, float(3), _Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float);
            float3 _Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.WorldSpaceNormal, (_Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float.xxx), _Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3);
            float _Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float = _Nome;
            float _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float = _Edge;
            float4 _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4 = _;
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_R_1_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[0];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_G_2_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[1];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_B_3_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[2];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[3];
            float3 _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3;
            Unity_Rotate_About_Axis_Degrees_float(IN.WorldSpacePosition, (_Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4.xyz), _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float, _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3);
            float _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float = __1;
            float _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float, _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float);
            float2 _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float.xx), _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2);
            float _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float = _Scale;
            float _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float);
            float2 _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), float2 (0, 0), _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2);
            float _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float);
            float _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float;
            Unity_Add_float(_GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float, _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float);
            float _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float;
            Unity_Saturate_float(_Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float, _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float);
            float _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float;
            Unity_Divide_float(_Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float, float(2), _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float);
            float _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float = __4;
            float _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float;
            Unity_Power_float(_Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float, _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float, _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float);
            float4 _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4 = __3;
            float _Split_09487d18a83e485886a34ed861cc7b18_R_1_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[0];
            float _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[1];
            float _Split_09487d18a83e485886a34ed861cc7b18_B_3_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[2];
            float _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[3];
            float4 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4;
            float3 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3;
            float2 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_R_1_Float, _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float, float(0), float(0), _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2);
            float4 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4;
            float3 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3;
            float2 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_B_3_Float, _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float, float(0), float(0), _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2);
            float _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float;
            Unity_Remap_float(_Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2, _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float);
            float _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float;
            Unity_Absolute_float(_Remap_16660554acc34982b32fa3029894c38d_Out_3_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float);
            float _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float;
            Unity_Smoothstep_float(_Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float, _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float, _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float);
            float _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float = __6;
            float _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float, _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float);
            float2 _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float.xx), _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2);
            float _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float = __5;
            float _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2, _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float, _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float);
            float _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float = __7;
            float _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float;
            Unity_Multiply_float_float(_GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float, _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float);
            float _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float;
            Unity_Add_float(_Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float, _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float);
            float _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float;
            Unity_Add_float(float(1), _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float);
            float _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float;
            Unity_Divide_float(_Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float, _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float);
            float3 _Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.ObjectSpaceNormal, (_Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float.xxx), _Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3);
            float _Property_2825612abf5e4489b5821e858fe2ea4b_Out_0_Float = __2;
            float3 _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3;
            Unity_Multiply_float3_float3(_Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3, (_Property_2825612abf5e4489b5821e858fe2ea4b_Out_0_Float.xxx), _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3);
            float3 _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3;
            Unity_Add_float3(IN.ObjectSpacePosition, _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3, _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3);
            float3 _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3;
            Unity_Add_float3(_Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3, _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3, _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3);
            description.Position = _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float Alpha;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _Property_f2589fddf60243b8b845a860bbba81d9_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color_1) : _Color_1;
            float4 _Property_9bb80aef4a694edb99443da55a608468_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
            float _Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float = _Nome;
            float _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float = _Edge;
            float4 _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4 = _;
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_R_1_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[0];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_G_2_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[1];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_B_3_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[2];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[3];
            float3 _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3;
            Unity_Rotate_About_Axis_Degrees_float(IN.WorldSpacePosition, (_Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4.xyz), _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float, _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3);
            float _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float = __1;
            float _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float, _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float);
            float2 _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float.xx), _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2);
            float _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float = _Scale;
            float _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float);
            float2 _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), float2 (0, 0), _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2);
            float _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float);
            float _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float;
            Unity_Add_float(_GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float, _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float);
            float _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float;
            Unity_Saturate_float(_Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float, _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float);
            float _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float;
            Unity_Divide_float(_Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float, float(2), _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float);
            float _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float = __4;
            float _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float;
            Unity_Power_float(_Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float, _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float, _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float);
            float4 _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4 = __3;
            float _Split_09487d18a83e485886a34ed861cc7b18_R_1_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[0];
            float _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[1];
            float _Split_09487d18a83e485886a34ed861cc7b18_B_3_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[2];
            float _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[3];
            float4 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4;
            float3 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3;
            float2 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_R_1_Float, _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float, float(0), float(0), _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2);
            float4 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4;
            float3 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3;
            float2 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_B_3_Float, _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float, float(0), float(0), _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2);
            float _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float;
            Unity_Remap_float(_Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2, _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float);
            float _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float;
            Unity_Absolute_float(_Remap_16660554acc34982b32fa3029894c38d_Out_3_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float);
            float _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float;
            Unity_Smoothstep_float(_Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float, _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float, _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float);
            float _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float = __6;
            float _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float, _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float);
            float2 _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float.xx), _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2);
            float _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float = __5;
            float _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2, _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float, _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float);
            float _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float = __7;
            float _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float;
            Unity_Multiply_float_float(_GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float, _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float);
            float _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float;
            Unity_Add_float(_Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float, _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float);
            float _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float;
            Unity_Add_float(float(1), _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float);
            float _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float;
            Unity_Divide_float(_Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float, _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float);
            float4 _Lerp_e2a3e13e094c4f3eb442b0a9f18f31fc_Out_3_Vector4;
            Unity_Lerp_float4(_Property_f2589fddf60243b8b845a860bbba81d9_Out_0_Vector4, _Property_9bb80aef4a694edb99443da55a608468_Out_0_Vector4, (_Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float.xxxx), _Lerp_e2a3e13e094c4f3eb442b0a9f18f31fc_Out_3_Vector4);
            float _Property_f736fe6426514efd98c7663b4d6820f5_Out_0_Float = __10;
            float _FresnelEffect_09fa928a853b43b891edcdf74a7ce03f_Out_3_Float;
            Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_f736fe6426514efd98c7663b4d6820f5_Out_0_Float, _FresnelEffect_09fa928a853b43b891edcdf74a7ce03f_Out_3_Float);
            float _Multiply_a73906a05cc645f4b196a202187de6e5_Out_2_Float;
            Unity_Multiply_float_float(_Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float, _FresnelEffect_09fa928a853b43b891edcdf74a7ce03f_Out_3_Float, _Multiply_a73906a05cc645f4b196a202187de6e5_Out_2_Float);
            float _Property_e35af77fc1e04fac827732a570d397e4_Out_0_Float = _Fresnel;
            float _Multiply_6236f29104324d4b9b06b92acb03049d_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_a73906a05cc645f4b196a202187de6e5_Out_2_Float, _Property_e35af77fc1e04fac827732a570d397e4_Out_0_Float, _Multiply_6236f29104324d4b9b06b92acb03049d_Out_2_Float);
            float4 _Add_c0cdd70753804266aa9c0047174d2bd5_Out_2_Vector4;
            Unity_Add_float4(_Lerp_e2a3e13e094c4f3eb442b0a9f18f31fc_Out_3_Vector4, (_Multiply_6236f29104324d4b9b06b92acb03049d_Out_2_Float.xxxx), _Add_c0cdd70753804266aa9c0047174d2bd5_Out_2_Vector4);
            float _SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float;
            Unity_SceneDepth_Eye_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float);
            float4 _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4 = IN.ScreenPosition;
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_R_1_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[0];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_G_2_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[1];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_B_3_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[2];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_A_4_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[3];
            float _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float;
            Unity_Subtract_float(_Split_f132a71f4c3a46a3aaad77428bed17dc_A_4_Float, float(1), _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float);
            float _Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float;
            Unity_Subtract_float(_SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float, _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float, _Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float);
            float _Property_8bd450fff67f41e68636a90c818a9e85_Out_0_Float = __11;
            float _Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float;
            Unity_Divide_float(_Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float, _Property_8bd450fff67f41e68636a90c818a9e85_Out_0_Float, _Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float);
            float _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float;
            Unity_Saturate_float(_Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float, _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float);
            surface.BaseColor = (_Add_c0cdd70753804266aa9c0047174d2bd5_Out_2_Vector4.xyz);
            surface.Alpha = _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.TimeParameters =                             _TimeParameters.xyz;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
        
        
            output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
            output.WorldSpacePosition = input.positionWS;
            output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);
        
            #if UNITY_UV_STARTS_AT_TOP
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #else
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #endif
        
            output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
            output.NDCPosition.y = 1.0f - output.NDCPosition.y;
        
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
            output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/UnlitPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "MotionVectors"
            Tags
            {
                "LightMode" = "MotionVectors"
            }
        
        // Render State
        Cull Off
        ZTest LEqual
        ZWrite On
        ColorMask RG
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 3.5
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define GRAPH_VERTEX_USES_TIME_PARAMETERS_INPUT
        #define VARYINGS_NEED_POSITION_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_MOTION_VECTORS
        #define REQUIRE_DEPTH_TEXTURE
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpacePosition;
             float4 ScreenPosition;
             float2 NDCPosition;
             float2 PixelPosition;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS : INTERP0;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.positionWS.xyz = input.positionWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.positionWS = input.positionWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _;
        float _Scale;
        float __1;
        float __2;
        float4 __3;
        float4 _Color;
        float4 _Color_1;
        float _Nome;
        float _Edge;
        float __4;
        float __5;
        float __6;
        float __7;
        float __8;
        float __9;
        float __10;
        float _Fresnel;
        float __11;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        
        // Graph Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl"
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Distance_float3(float3 A, float3 B, out float Out)
        {
            Out = distance(A, B);
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_Power_float(float A, float B, out float Out)
        {
            Out = pow(A, B);
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        void Unity_Rotate_About_Axis_Degrees_float(float3 In, float3 Axis, float Rotation, out float3 Out)
        {
            Rotation = radians(Rotation);
            float s, c;
            sincos(Rotation, s, c);
            Axis = normalize(Axis);
            Out = In * c + cross(Axis, In) * s + Axis * dot(Axis, In) * (1 - c);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
        {
            Out = UV * Tiling + Offset;
        }
        
        float2 Unity_GradientNoise_Deterministic_Dir_float(float2 p)
        {
            float x; Hash_Tchou_2_1_float(p, x);
            return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
        }
        
        void Unity_GradientNoise_Deterministic_float (float2 UV, float3 Scale, out float Out)
        {
            float2 p = UV * Scale.xy;
            float2 ip = floor(p);
            float2 fp = frac(p);
            float d00 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip), fp);
            float d01 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(0, 1)), fp - float2(0, 1));
            float d10 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 0)), fp - float2(1, 0));
            float d11 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 1)), fp - float2(1, 1));
            fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
            Out = lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Saturate_float(float In, out float Out)
        {
            Out = saturate(In);
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
        {
            Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
        }
        
        void Unity_Absolute_float(float In, out float Out)
        {
            Out = abs(In);
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_SceneDepth_Eye_float(float4 UV, out float Out)
        {
            if (unity_OrthoParams.w == 1.0)
            {
                Out = LinearEyeDepth(ComputeWorldSpacePosition(UV.xy, SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), UNITY_MATRIX_I_VP), UNITY_MATRIX_V);
            }
            else
            {
                Out = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
            }
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float;
            Unity_Distance_float3(SHADERGRAPH_OBJECT_POSITION, IN.WorldSpacePosition, _Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float);
            float _Property_6e77d3e8ac5a45cb836f7d04202a85a7_Out_0_Float = __9;
            float _Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float;
            Unity_Divide_float(_Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float, _Property_6e77d3e8ac5a45cb836f7d04202a85a7_Out_0_Float, _Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float);
            float _Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float;
            Unity_Power_float(_Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float, float(3), _Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float);
            float3 _Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.WorldSpaceNormal, (_Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float.xxx), _Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3);
            float _Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float = _Nome;
            float _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float = _Edge;
            float4 _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4 = _;
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_R_1_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[0];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_G_2_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[1];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_B_3_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[2];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[3];
            float3 _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3;
            Unity_Rotate_About_Axis_Degrees_float(IN.WorldSpacePosition, (_Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4.xyz), _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float, _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3);
            float _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float = __1;
            float _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float, _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float);
            float2 _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float.xx), _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2);
            float _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float = _Scale;
            float _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float);
            float2 _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), float2 (0, 0), _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2);
            float _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float);
            float _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float;
            Unity_Add_float(_GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float, _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float);
            float _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float;
            Unity_Saturate_float(_Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float, _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float);
            float _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float;
            Unity_Divide_float(_Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float, float(2), _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float);
            float _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float = __4;
            float _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float;
            Unity_Power_float(_Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float, _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float, _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float);
            float4 _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4 = __3;
            float _Split_09487d18a83e485886a34ed861cc7b18_R_1_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[0];
            float _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[1];
            float _Split_09487d18a83e485886a34ed861cc7b18_B_3_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[2];
            float _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[3];
            float4 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4;
            float3 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3;
            float2 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_R_1_Float, _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float, float(0), float(0), _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2);
            float4 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4;
            float3 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3;
            float2 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_B_3_Float, _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float, float(0), float(0), _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2);
            float _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float;
            Unity_Remap_float(_Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2, _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float);
            float _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float;
            Unity_Absolute_float(_Remap_16660554acc34982b32fa3029894c38d_Out_3_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float);
            float _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float;
            Unity_Smoothstep_float(_Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float, _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float, _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float);
            float _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float = __6;
            float _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float, _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float);
            float2 _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float.xx), _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2);
            float _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float = __5;
            float _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2, _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float, _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float);
            float _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float = __7;
            float _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float;
            Unity_Multiply_float_float(_GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float, _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float);
            float _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float;
            Unity_Add_float(_Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float, _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float);
            float _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float;
            Unity_Add_float(float(1), _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float);
            float _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float;
            Unity_Divide_float(_Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float, _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float);
            float3 _Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.ObjectSpaceNormal, (_Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float.xxx), _Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3);
            float _Property_2825612abf5e4489b5821e858fe2ea4b_Out_0_Float = __2;
            float3 _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3;
            Unity_Multiply_float3_float3(_Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3, (_Property_2825612abf5e4489b5821e858fe2ea4b_Out_0_Float.xxx), _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3);
            float3 _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3;
            Unity_Add_float3(IN.ObjectSpacePosition, _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3, _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3);
            float3 _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3;
            Unity_Add_float3(_Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3, _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3, _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3);
            description.Position = _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float Alpha;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float _SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float;
            Unity_SceneDepth_Eye_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float);
            float4 _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4 = IN.ScreenPosition;
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_R_1_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[0];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_G_2_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[1];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_B_3_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[2];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_A_4_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[3];
            float _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float;
            Unity_Subtract_float(_Split_f132a71f4c3a46a3aaad77428bed17dc_A_4_Float, float(1), _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float);
            float _Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float;
            Unity_Subtract_float(_SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float, _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float, _Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float);
            float _Property_8bd450fff67f41e68636a90c818a9e85_Out_0_Float = __11;
            float _Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float;
            Unity_Divide_float(_Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float, _Property_8bd450fff67f41e68636a90c818a9e85_Out_0_Float, _Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float);
            float _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float;
            Unity_Saturate_float(_Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float, _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float);
            surface.Alpha = _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.TimeParameters =                             _TimeParameters.xyz;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
            output.WorldSpacePosition = input.positionWS;
            output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);
        
            #if UNITY_UV_STARTS_AT_TOP
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #else
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #endif
        
            output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
            output.NDCPosition.y = 1.0f - output.NDCPosition.y;
        
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/MotionVectorPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags
            {
                "LightMode" = "DepthNormalsOnly"
            }
        
        // Render State
        Cull Off
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define GRAPH_VERTEX_USES_TIME_PARAMETERS_INPUT
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHNORMALSONLY
        #define _SURFACE_TYPE_TRANSPARENT 1
        #define REQUIRE_DEPTH_TEXTURE
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpacePosition;
             float4 ScreenPosition;
             float2 NDCPosition;
             float2 PixelPosition;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS : INTERP0;
             float3 normalWS : INTERP1;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _;
        float _Scale;
        float __1;
        float __2;
        float4 __3;
        float4 _Color;
        float4 _Color_1;
        float _Nome;
        float _Edge;
        float __4;
        float __5;
        float __6;
        float __7;
        float __8;
        float __9;
        float __10;
        float _Fresnel;
        float __11;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        
        // Graph Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl"
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Distance_float3(float3 A, float3 B, out float Out)
        {
            Out = distance(A, B);
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_Power_float(float A, float B, out float Out)
        {
            Out = pow(A, B);
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        void Unity_Rotate_About_Axis_Degrees_float(float3 In, float3 Axis, float Rotation, out float3 Out)
        {
            Rotation = radians(Rotation);
            float s, c;
            sincos(Rotation, s, c);
            Axis = normalize(Axis);
            Out = In * c + cross(Axis, In) * s + Axis * dot(Axis, In) * (1 - c);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
        {
            Out = UV * Tiling + Offset;
        }
        
        float2 Unity_GradientNoise_Deterministic_Dir_float(float2 p)
        {
            float x; Hash_Tchou_2_1_float(p, x);
            return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
        }
        
        void Unity_GradientNoise_Deterministic_float (float2 UV, float3 Scale, out float Out)
        {
            float2 p = UV * Scale.xy;
            float2 ip = floor(p);
            float2 fp = frac(p);
            float d00 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip), fp);
            float d01 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(0, 1)), fp - float2(0, 1));
            float d10 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 0)), fp - float2(1, 0));
            float d11 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 1)), fp - float2(1, 1));
            fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
            Out = lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Saturate_float(float In, out float Out)
        {
            Out = saturate(In);
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
        {
            Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
        }
        
        void Unity_Absolute_float(float In, out float Out)
        {
            Out = abs(In);
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_SceneDepth_Eye_float(float4 UV, out float Out)
        {
            if (unity_OrthoParams.w == 1.0)
            {
                Out = LinearEyeDepth(ComputeWorldSpacePosition(UV.xy, SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), UNITY_MATRIX_I_VP), UNITY_MATRIX_V);
            }
            else
            {
                Out = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
            }
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float;
            Unity_Distance_float3(SHADERGRAPH_OBJECT_POSITION, IN.WorldSpacePosition, _Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float);
            float _Property_6e77d3e8ac5a45cb836f7d04202a85a7_Out_0_Float = __9;
            float _Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float;
            Unity_Divide_float(_Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float, _Property_6e77d3e8ac5a45cb836f7d04202a85a7_Out_0_Float, _Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float);
            float _Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float;
            Unity_Power_float(_Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float, float(3), _Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float);
            float3 _Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.WorldSpaceNormal, (_Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float.xxx), _Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3);
            float _Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float = _Nome;
            float _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float = _Edge;
            float4 _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4 = _;
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_R_1_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[0];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_G_2_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[1];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_B_3_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[2];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[3];
            float3 _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3;
            Unity_Rotate_About_Axis_Degrees_float(IN.WorldSpacePosition, (_Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4.xyz), _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float, _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3);
            float _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float = __1;
            float _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float, _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float);
            float2 _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float.xx), _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2);
            float _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float = _Scale;
            float _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float);
            float2 _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), float2 (0, 0), _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2);
            float _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float);
            float _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float;
            Unity_Add_float(_GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float, _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float);
            float _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float;
            Unity_Saturate_float(_Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float, _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float);
            float _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float;
            Unity_Divide_float(_Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float, float(2), _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float);
            float _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float = __4;
            float _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float;
            Unity_Power_float(_Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float, _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float, _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float);
            float4 _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4 = __3;
            float _Split_09487d18a83e485886a34ed861cc7b18_R_1_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[0];
            float _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[1];
            float _Split_09487d18a83e485886a34ed861cc7b18_B_3_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[2];
            float _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[3];
            float4 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4;
            float3 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3;
            float2 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_R_1_Float, _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float, float(0), float(0), _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2);
            float4 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4;
            float3 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3;
            float2 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_B_3_Float, _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float, float(0), float(0), _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2);
            float _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float;
            Unity_Remap_float(_Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2, _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float);
            float _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float;
            Unity_Absolute_float(_Remap_16660554acc34982b32fa3029894c38d_Out_3_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float);
            float _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float;
            Unity_Smoothstep_float(_Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float, _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float, _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float);
            float _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float = __6;
            float _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float, _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float);
            float2 _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float.xx), _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2);
            float _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float = __5;
            float _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2, _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float, _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float);
            float _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float = __7;
            float _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float;
            Unity_Multiply_float_float(_GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float, _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float);
            float _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float;
            Unity_Add_float(_Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float, _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float);
            float _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float;
            Unity_Add_float(float(1), _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float);
            float _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float;
            Unity_Divide_float(_Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float, _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float);
            float3 _Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.ObjectSpaceNormal, (_Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float.xxx), _Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3);
            float _Property_2825612abf5e4489b5821e858fe2ea4b_Out_0_Float = __2;
            float3 _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3;
            Unity_Multiply_float3_float3(_Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3, (_Property_2825612abf5e4489b5821e858fe2ea4b_Out_0_Float.xxx), _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3);
            float3 _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3;
            Unity_Add_float3(IN.ObjectSpacePosition, _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3, _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3);
            float3 _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3;
            Unity_Add_float3(_Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3, _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3, _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3);
            description.Position = _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float Alpha;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float _SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float;
            Unity_SceneDepth_Eye_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float);
            float4 _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4 = IN.ScreenPosition;
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_R_1_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[0];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_G_2_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[1];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_B_3_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[2];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_A_4_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[3];
            float _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float;
            Unity_Subtract_float(_Split_f132a71f4c3a46a3aaad77428bed17dc_A_4_Float, float(1), _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float);
            float _Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float;
            Unity_Subtract_float(_SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float, _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float, _Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float);
            float _Property_8bd450fff67f41e68636a90c818a9e85_Out_0_Float = __11;
            float _Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float;
            Unity_Divide_float(_Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float, _Property_8bd450fff67f41e68636a90c818a9e85_Out_0_Float, _Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float);
            float _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float;
            Unity_Saturate_float(_Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float, _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float);
            surface.Alpha = _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.TimeParameters =                             _TimeParameters.xyz;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
            output.WorldSpacePosition = input.positionWS;
            output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);
        
            #if UNITY_UV_STARTS_AT_TOP
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #else
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #endif
        
            output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
            output.NDCPosition.y = 1.0f - output.NDCPosition.y;
        
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthNormalsOnlyPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }
        
        // Render State
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZTest LEqual
        ZWrite Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 4.5
        #pragma exclude_renderers gles3 glcore
        #pragma multi_compile_instancing
        #pragma instancing_options renderinglayer
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
        #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
        #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
        #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
        #pragma multi_compile _ SHADOWS_SHADOWMASK
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define GRAPH_VERTEX_USES_TIME_PARAMETERS_INPUT
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_GBUFFER
        #define _SURFACE_TYPE_TRANSPARENT 1
        #define REQUIRE_DEPTH_TEXTURE
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
            #if !defined(LIGHTMAP_ON)
             float3 sh;
            #endif
            #if defined(USE_APV_PROBE_OCCLUSION)
             float4 probeOcclusion;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 WorldSpaceViewDirection;
             float3 WorldSpacePosition;
             float4 ScreenPosition;
             float2 NDCPosition;
             float2 PixelPosition;
             float3 TimeParameters;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if !defined(LIGHTMAP_ON)
             float3 sh : INTERP0;
            #endif
            #if defined(USE_APV_PROBE_OCCLUSION)
             float4 probeOcclusion : INTERP1;
            #endif
             float3 positionWS : INTERP2;
             float3 normalWS : INTERP3;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            #if defined(USE_APV_PROBE_OCCLUSION)
            output.probeOcclusion = input.probeOcclusion;
            #endif
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            #if defined(USE_APV_PROBE_OCCLUSION)
            output.probeOcclusion = input.probeOcclusion;
            #endif
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _;
        float _Scale;
        float __1;
        float __2;
        float4 __3;
        float4 _Color;
        float4 _Color_1;
        float _Nome;
        float _Edge;
        float __4;
        float __5;
        float __6;
        float __7;
        float __8;
        float __9;
        float __10;
        float _Fresnel;
        float __11;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        
        // Graph Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl"
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Distance_float3(float3 A, float3 B, out float Out)
        {
            Out = distance(A, B);
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_Power_float(float A, float B, out float Out)
        {
            Out = pow(A, B);
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        void Unity_Rotate_About_Axis_Degrees_float(float3 In, float3 Axis, float Rotation, out float3 Out)
        {
            Rotation = radians(Rotation);
            float s, c;
            sincos(Rotation, s, c);
            Axis = normalize(Axis);
            Out = In * c + cross(Axis, In) * s + Axis * dot(Axis, In) * (1 - c);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
        {
            Out = UV * Tiling + Offset;
        }
        
        float2 Unity_GradientNoise_Deterministic_Dir_float(float2 p)
        {
            float x; Hash_Tchou_2_1_float(p, x);
            return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
        }
        
        void Unity_GradientNoise_Deterministic_float (float2 UV, float3 Scale, out float Out)
        {
            float2 p = UV * Scale.xy;
            float2 ip = floor(p);
            float2 fp = frac(p);
            float d00 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip), fp);
            float d01 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(0, 1)), fp - float2(0, 1));
            float d10 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 0)), fp - float2(1, 0));
            float d11 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 1)), fp - float2(1, 1));
            fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
            Out = lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Saturate_float(float In, out float Out)
        {
            Out = saturate(In);
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
        {
            Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
        }
        
        void Unity_Absolute_float(float In, out float Out)
        {
            Out = abs(In);
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
        {
            Out = lerp(A, B, T);
        }
        
        void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
        {
            Out = pow((1.0 - saturate(dot(normalize(Normal), ViewDir))), Power);
        }
        
        void Unity_Add_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A + B;
        }
        
        void Unity_SceneDepth_Eye_float(float4 UV, out float Out)
        {
            if (unity_OrthoParams.w == 1.0)
            {
                Out = LinearEyeDepth(ComputeWorldSpacePosition(UV.xy, SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), UNITY_MATRIX_I_VP), UNITY_MATRIX_V);
            }
            else
            {
                Out = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
            }
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float;
            Unity_Distance_float3(SHADERGRAPH_OBJECT_POSITION, IN.WorldSpacePosition, _Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float);
            float _Property_6e77d3e8ac5a45cb836f7d04202a85a7_Out_0_Float = __9;
            float _Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float;
            Unity_Divide_float(_Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float, _Property_6e77d3e8ac5a45cb836f7d04202a85a7_Out_0_Float, _Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float);
            float _Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float;
            Unity_Power_float(_Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float, float(3), _Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float);
            float3 _Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.WorldSpaceNormal, (_Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float.xxx), _Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3);
            float _Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float = _Nome;
            float _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float = _Edge;
            float4 _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4 = _;
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_R_1_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[0];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_G_2_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[1];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_B_3_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[2];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[3];
            float3 _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3;
            Unity_Rotate_About_Axis_Degrees_float(IN.WorldSpacePosition, (_Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4.xyz), _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float, _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3);
            float _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float = __1;
            float _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float, _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float);
            float2 _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float.xx), _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2);
            float _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float = _Scale;
            float _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float);
            float2 _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), float2 (0, 0), _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2);
            float _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float);
            float _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float;
            Unity_Add_float(_GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float, _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float);
            float _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float;
            Unity_Saturate_float(_Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float, _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float);
            float _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float;
            Unity_Divide_float(_Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float, float(2), _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float);
            float _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float = __4;
            float _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float;
            Unity_Power_float(_Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float, _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float, _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float);
            float4 _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4 = __3;
            float _Split_09487d18a83e485886a34ed861cc7b18_R_1_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[0];
            float _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[1];
            float _Split_09487d18a83e485886a34ed861cc7b18_B_3_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[2];
            float _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[3];
            float4 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4;
            float3 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3;
            float2 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_R_1_Float, _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float, float(0), float(0), _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2);
            float4 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4;
            float3 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3;
            float2 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_B_3_Float, _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float, float(0), float(0), _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2);
            float _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float;
            Unity_Remap_float(_Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2, _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float);
            float _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float;
            Unity_Absolute_float(_Remap_16660554acc34982b32fa3029894c38d_Out_3_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float);
            float _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float;
            Unity_Smoothstep_float(_Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float, _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float, _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float);
            float _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float = __6;
            float _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float, _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float);
            float2 _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float.xx), _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2);
            float _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float = __5;
            float _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2, _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float, _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float);
            float _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float = __7;
            float _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float;
            Unity_Multiply_float_float(_GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float, _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float);
            float _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float;
            Unity_Add_float(_Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float, _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float);
            float _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float;
            Unity_Add_float(float(1), _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float);
            float _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float;
            Unity_Divide_float(_Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float, _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float);
            float3 _Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.ObjectSpaceNormal, (_Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float.xxx), _Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3);
            float _Property_2825612abf5e4489b5821e858fe2ea4b_Out_0_Float = __2;
            float3 _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3;
            Unity_Multiply_float3_float3(_Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3, (_Property_2825612abf5e4489b5821e858fe2ea4b_Out_0_Float.xxx), _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3);
            float3 _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3;
            Unity_Add_float3(IN.ObjectSpacePosition, _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3, _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3);
            float3 _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3;
            Unity_Add_float3(_Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3, _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3, _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3);
            description.Position = _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float Alpha;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _Property_f2589fddf60243b8b845a860bbba81d9_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color_1) : _Color_1;
            float4 _Property_9bb80aef4a694edb99443da55a608468_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
            float _Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float = _Nome;
            float _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float = _Edge;
            float4 _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4 = _;
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_R_1_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[0];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_G_2_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[1];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_B_3_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[2];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[3];
            float3 _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3;
            Unity_Rotate_About_Axis_Degrees_float(IN.WorldSpacePosition, (_Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4.xyz), _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float, _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3);
            float _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float = __1;
            float _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float, _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float);
            float2 _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float.xx), _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2);
            float _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float = _Scale;
            float _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float);
            float2 _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), float2 (0, 0), _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2);
            float _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float);
            float _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float;
            Unity_Add_float(_GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float, _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float);
            float _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float;
            Unity_Saturate_float(_Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float, _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float);
            float _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float;
            Unity_Divide_float(_Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float, float(2), _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float);
            float _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float = __4;
            float _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float;
            Unity_Power_float(_Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float, _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float, _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float);
            float4 _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4 = __3;
            float _Split_09487d18a83e485886a34ed861cc7b18_R_1_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[0];
            float _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[1];
            float _Split_09487d18a83e485886a34ed861cc7b18_B_3_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[2];
            float _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[3];
            float4 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4;
            float3 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3;
            float2 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_R_1_Float, _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float, float(0), float(0), _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2);
            float4 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4;
            float3 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3;
            float2 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_B_3_Float, _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float, float(0), float(0), _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2);
            float _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float;
            Unity_Remap_float(_Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2, _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float);
            float _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float;
            Unity_Absolute_float(_Remap_16660554acc34982b32fa3029894c38d_Out_3_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float);
            float _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float;
            Unity_Smoothstep_float(_Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float, _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float, _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float);
            float _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float = __6;
            float _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float, _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float);
            float2 _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float.xx), _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2);
            float _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float = __5;
            float _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2, _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float, _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float);
            float _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float = __7;
            float _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float;
            Unity_Multiply_float_float(_GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float, _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float);
            float _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float;
            Unity_Add_float(_Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float, _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float);
            float _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float;
            Unity_Add_float(float(1), _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float);
            float _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float;
            Unity_Divide_float(_Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float, _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float);
            float4 _Lerp_e2a3e13e094c4f3eb442b0a9f18f31fc_Out_3_Vector4;
            Unity_Lerp_float4(_Property_f2589fddf60243b8b845a860bbba81d9_Out_0_Vector4, _Property_9bb80aef4a694edb99443da55a608468_Out_0_Vector4, (_Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float.xxxx), _Lerp_e2a3e13e094c4f3eb442b0a9f18f31fc_Out_3_Vector4);
            float _Property_f736fe6426514efd98c7663b4d6820f5_Out_0_Float = __10;
            float _FresnelEffect_09fa928a853b43b891edcdf74a7ce03f_Out_3_Float;
            Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_f736fe6426514efd98c7663b4d6820f5_Out_0_Float, _FresnelEffect_09fa928a853b43b891edcdf74a7ce03f_Out_3_Float);
            float _Multiply_a73906a05cc645f4b196a202187de6e5_Out_2_Float;
            Unity_Multiply_float_float(_Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float, _FresnelEffect_09fa928a853b43b891edcdf74a7ce03f_Out_3_Float, _Multiply_a73906a05cc645f4b196a202187de6e5_Out_2_Float);
            float _Property_e35af77fc1e04fac827732a570d397e4_Out_0_Float = _Fresnel;
            float _Multiply_6236f29104324d4b9b06b92acb03049d_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_a73906a05cc645f4b196a202187de6e5_Out_2_Float, _Property_e35af77fc1e04fac827732a570d397e4_Out_0_Float, _Multiply_6236f29104324d4b9b06b92acb03049d_Out_2_Float);
            float4 _Add_c0cdd70753804266aa9c0047174d2bd5_Out_2_Vector4;
            Unity_Add_float4(_Lerp_e2a3e13e094c4f3eb442b0a9f18f31fc_Out_3_Vector4, (_Multiply_6236f29104324d4b9b06b92acb03049d_Out_2_Float.xxxx), _Add_c0cdd70753804266aa9c0047174d2bd5_Out_2_Vector4);
            float _SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float;
            Unity_SceneDepth_Eye_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float);
            float4 _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4 = IN.ScreenPosition;
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_R_1_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[0];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_G_2_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[1];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_B_3_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[2];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_A_4_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[3];
            float _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float;
            Unity_Subtract_float(_Split_f132a71f4c3a46a3aaad77428bed17dc_A_4_Float, float(1), _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float);
            float _Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float;
            Unity_Subtract_float(_SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float, _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float, _Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float);
            float _Property_8bd450fff67f41e68636a90c818a9e85_Out_0_Float = __11;
            float _Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float;
            Unity_Divide_float(_Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float, _Property_8bd450fff67f41e68636a90c818a9e85_Out_0_Float, _Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float);
            float _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float;
            Unity_Saturate_float(_Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float, _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float);
            surface.BaseColor = (_Add_c0cdd70753804266aa9c0047174d2bd5_Out_2_Vector4.xyz);
            surface.Alpha = _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.TimeParameters =                             _TimeParameters.xyz;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
        
        
            output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
            output.WorldSpacePosition = input.positionWS;
            output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);
        
            #if UNITY_UV_STARTS_AT_TOP
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #else
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #endif
        
            output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
            output.NDCPosition.y = 1.0f - output.NDCPosition.y;
        
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
            output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/UnlitGBufferPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "SceneSelectionPass"
            Tags
            {
                "LightMode" = "SceneSelectionPass"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define GRAPH_VERTEX_USES_TIME_PARAMETERS_INPUT
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_POSITION_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        #define SCENESELECTIONPASS 1
        #define ALPHA_CLIP_THRESHOLD 1
        #define REQUIRE_DEPTH_TEXTURE
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpacePosition;
             float4 ScreenPosition;
             float2 NDCPosition;
             float2 PixelPosition;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS : INTERP0;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.positionWS.xyz = input.positionWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.positionWS = input.positionWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _;
        float _Scale;
        float __1;
        float __2;
        float4 __3;
        float4 _Color;
        float4 _Color_1;
        float _Nome;
        float _Edge;
        float __4;
        float __5;
        float __6;
        float __7;
        float __8;
        float __9;
        float __10;
        float _Fresnel;
        float __11;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        
        // Graph Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl"
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Distance_float3(float3 A, float3 B, out float Out)
        {
            Out = distance(A, B);
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_Power_float(float A, float B, out float Out)
        {
            Out = pow(A, B);
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        void Unity_Rotate_About_Axis_Degrees_float(float3 In, float3 Axis, float Rotation, out float3 Out)
        {
            Rotation = radians(Rotation);
            float s, c;
            sincos(Rotation, s, c);
            Axis = normalize(Axis);
            Out = In * c + cross(Axis, In) * s + Axis * dot(Axis, In) * (1 - c);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
        {
            Out = UV * Tiling + Offset;
        }
        
        float2 Unity_GradientNoise_Deterministic_Dir_float(float2 p)
        {
            float x; Hash_Tchou_2_1_float(p, x);
            return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
        }
        
        void Unity_GradientNoise_Deterministic_float (float2 UV, float3 Scale, out float Out)
        {
            float2 p = UV * Scale.xy;
            float2 ip = floor(p);
            float2 fp = frac(p);
            float d00 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip), fp);
            float d01 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(0, 1)), fp - float2(0, 1));
            float d10 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 0)), fp - float2(1, 0));
            float d11 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 1)), fp - float2(1, 1));
            fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
            Out = lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Saturate_float(float In, out float Out)
        {
            Out = saturate(In);
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
        {
            Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
        }
        
        void Unity_Absolute_float(float In, out float Out)
        {
            Out = abs(In);
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_SceneDepth_Eye_float(float4 UV, out float Out)
        {
            if (unity_OrthoParams.w == 1.0)
            {
                Out = LinearEyeDepth(ComputeWorldSpacePosition(UV.xy, SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), UNITY_MATRIX_I_VP), UNITY_MATRIX_V);
            }
            else
            {
                Out = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
            }
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float;
            Unity_Distance_float3(SHADERGRAPH_OBJECT_POSITION, IN.WorldSpacePosition, _Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float);
            float _Property_6e77d3e8ac5a45cb836f7d04202a85a7_Out_0_Float = __9;
            float _Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float;
            Unity_Divide_float(_Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float, _Property_6e77d3e8ac5a45cb836f7d04202a85a7_Out_0_Float, _Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float);
            float _Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float;
            Unity_Power_float(_Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float, float(3), _Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float);
            float3 _Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.WorldSpaceNormal, (_Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float.xxx), _Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3);
            float _Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float = _Nome;
            float _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float = _Edge;
            float4 _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4 = _;
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_R_1_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[0];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_G_2_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[1];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_B_3_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[2];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[3];
            float3 _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3;
            Unity_Rotate_About_Axis_Degrees_float(IN.WorldSpacePosition, (_Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4.xyz), _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float, _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3);
            float _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float = __1;
            float _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float, _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float);
            float2 _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float.xx), _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2);
            float _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float = _Scale;
            float _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float);
            float2 _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), float2 (0, 0), _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2);
            float _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float);
            float _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float;
            Unity_Add_float(_GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float, _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float);
            float _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float;
            Unity_Saturate_float(_Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float, _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float);
            float _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float;
            Unity_Divide_float(_Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float, float(2), _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float);
            float _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float = __4;
            float _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float;
            Unity_Power_float(_Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float, _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float, _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float);
            float4 _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4 = __3;
            float _Split_09487d18a83e485886a34ed861cc7b18_R_1_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[0];
            float _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[1];
            float _Split_09487d18a83e485886a34ed861cc7b18_B_3_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[2];
            float _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[3];
            float4 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4;
            float3 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3;
            float2 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_R_1_Float, _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float, float(0), float(0), _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2);
            float4 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4;
            float3 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3;
            float2 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_B_3_Float, _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float, float(0), float(0), _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2);
            float _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float;
            Unity_Remap_float(_Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2, _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float);
            float _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float;
            Unity_Absolute_float(_Remap_16660554acc34982b32fa3029894c38d_Out_3_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float);
            float _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float;
            Unity_Smoothstep_float(_Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float, _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float, _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float);
            float _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float = __6;
            float _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float, _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float);
            float2 _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float.xx), _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2);
            float _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float = __5;
            float _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2, _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float, _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float);
            float _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float = __7;
            float _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float;
            Unity_Multiply_float_float(_GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float, _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float);
            float _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float;
            Unity_Add_float(_Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float, _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float);
            float _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float;
            Unity_Add_float(float(1), _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float);
            float _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float;
            Unity_Divide_float(_Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float, _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float);
            float3 _Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.ObjectSpaceNormal, (_Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float.xxx), _Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3);
            float _Property_2825612abf5e4489b5821e858fe2ea4b_Out_0_Float = __2;
            float3 _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3;
            Unity_Multiply_float3_float3(_Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3, (_Property_2825612abf5e4489b5821e858fe2ea4b_Out_0_Float.xxx), _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3);
            float3 _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3;
            Unity_Add_float3(IN.ObjectSpacePosition, _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3, _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3);
            float3 _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3;
            Unity_Add_float3(_Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3, _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3, _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3);
            description.Position = _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float Alpha;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float _SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float;
            Unity_SceneDepth_Eye_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float);
            float4 _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4 = IN.ScreenPosition;
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_R_1_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[0];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_G_2_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[1];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_B_3_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[2];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_A_4_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[3];
            float _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float;
            Unity_Subtract_float(_Split_f132a71f4c3a46a3aaad77428bed17dc_A_4_Float, float(1), _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float);
            float _Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float;
            Unity_Subtract_float(_SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float, _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float, _Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float);
            float _Property_8bd450fff67f41e68636a90c818a9e85_Out_0_Float = __11;
            float _Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float;
            Unity_Divide_float(_Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float, _Property_8bd450fff67f41e68636a90c818a9e85_Out_0_Float, _Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float);
            float _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float;
            Unity_Saturate_float(_Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float, _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float);
            surface.Alpha = _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.TimeParameters =                             _TimeParameters.xyz;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
            output.WorldSpacePosition = input.positionWS;
            output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);
        
            #if UNITY_UV_STARTS_AT_TOP
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #else
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #endif
        
            output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
            output.NDCPosition.y = 1.0f - output.NDCPosition.y;
        
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "ScenePickingPass"
            Tags
            {
                "LightMode" = "Picking"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define GRAPH_VERTEX_USES_TIME_PARAMETERS_INPUT
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        #define SCENEPICKINGPASS 1
        #define ALPHA_CLIP_THRESHOLD 1
        #define REQUIRE_DEPTH_TEXTURE
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 WorldSpaceViewDirection;
             float3 WorldSpacePosition;
             float4 ScreenPosition;
             float2 NDCPosition;
             float2 PixelPosition;
             float3 TimeParameters;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS : INTERP0;
             float3 normalWS : INTERP1;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _;
        float _Scale;
        float __1;
        float __2;
        float4 __3;
        float4 _Color;
        float4 _Color_1;
        float _Nome;
        float _Edge;
        float __4;
        float __5;
        float __6;
        float __7;
        float __8;
        float __9;
        float __10;
        float _Fresnel;
        float __11;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        
        // Graph Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl"
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Distance_float3(float3 A, float3 B, out float Out)
        {
            Out = distance(A, B);
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_Power_float(float A, float B, out float Out)
        {
            Out = pow(A, B);
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        void Unity_Rotate_About_Axis_Degrees_float(float3 In, float3 Axis, float Rotation, out float3 Out)
        {
            Rotation = radians(Rotation);
            float s, c;
            sincos(Rotation, s, c);
            Axis = normalize(Axis);
            Out = In * c + cross(Axis, In) * s + Axis * dot(Axis, In) * (1 - c);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
        {
            Out = UV * Tiling + Offset;
        }
        
        float2 Unity_GradientNoise_Deterministic_Dir_float(float2 p)
        {
            float x; Hash_Tchou_2_1_float(p, x);
            return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
        }
        
        void Unity_GradientNoise_Deterministic_float (float2 UV, float3 Scale, out float Out)
        {
            float2 p = UV * Scale.xy;
            float2 ip = floor(p);
            float2 fp = frac(p);
            float d00 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip), fp);
            float d01 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(0, 1)), fp - float2(0, 1));
            float d10 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 0)), fp - float2(1, 0));
            float d11 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 1)), fp - float2(1, 1));
            fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
            Out = lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Saturate_float(float In, out float Out)
        {
            Out = saturate(In);
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
        {
            Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
        }
        
        void Unity_Absolute_float(float In, out float Out)
        {
            Out = abs(In);
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
        {
            Out = lerp(A, B, T);
        }
        
        void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
        {
            Out = pow((1.0 - saturate(dot(normalize(Normal), ViewDir))), Power);
        }
        
        void Unity_Add_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A + B;
        }
        
        void Unity_SceneDepth_Eye_float(float4 UV, out float Out)
        {
            if (unity_OrthoParams.w == 1.0)
            {
                Out = LinearEyeDepth(ComputeWorldSpacePosition(UV.xy, SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), UNITY_MATRIX_I_VP), UNITY_MATRIX_V);
            }
            else
            {
                Out = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
            }
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float;
            Unity_Distance_float3(SHADERGRAPH_OBJECT_POSITION, IN.WorldSpacePosition, _Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float);
            float _Property_6e77d3e8ac5a45cb836f7d04202a85a7_Out_0_Float = __9;
            float _Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float;
            Unity_Divide_float(_Distance_0fdad9d1ea7644c89f0716e955406d87_Out_2_Float, _Property_6e77d3e8ac5a45cb836f7d04202a85a7_Out_0_Float, _Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float);
            float _Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float;
            Unity_Power_float(_Divide_abe60cfe1c70424d85ad66e7df3b5812_Out_2_Float, float(3), _Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float);
            float3 _Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.WorldSpaceNormal, (_Power_c357c6c4fd534fda9568bd4e2c875e56_Out_2_Float.xxx), _Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3);
            float _Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float = _Nome;
            float _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float = _Edge;
            float4 _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4 = _;
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_R_1_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[0];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_G_2_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[1];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_B_3_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[2];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[3];
            float3 _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3;
            Unity_Rotate_About_Axis_Degrees_float(IN.WorldSpacePosition, (_Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4.xyz), _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float, _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3);
            float _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float = __1;
            float _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float, _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float);
            float2 _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float.xx), _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2);
            float _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float = _Scale;
            float _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float);
            float2 _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), float2 (0, 0), _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2);
            float _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float);
            float _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float;
            Unity_Add_float(_GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float, _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float);
            float _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float;
            Unity_Saturate_float(_Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float, _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float);
            float _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float;
            Unity_Divide_float(_Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float, float(2), _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float);
            float _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float = __4;
            float _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float;
            Unity_Power_float(_Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float, _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float, _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float);
            float4 _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4 = __3;
            float _Split_09487d18a83e485886a34ed861cc7b18_R_1_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[0];
            float _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[1];
            float _Split_09487d18a83e485886a34ed861cc7b18_B_3_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[2];
            float _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[3];
            float4 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4;
            float3 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3;
            float2 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_R_1_Float, _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float, float(0), float(0), _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2);
            float4 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4;
            float3 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3;
            float2 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_B_3_Float, _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float, float(0), float(0), _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2);
            float _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float;
            Unity_Remap_float(_Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2, _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float);
            float _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float;
            Unity_Absolute_float(_Remap_16660554acc34982b32fa3029894c38d_Out_3_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float);
            float _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float;
            Unity_Smoothstep_float(_Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float, _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float, _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float);
            float _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float = __6;
            float _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float, _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float);
            float2 _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float.xx), _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2);
            float _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float = __5;
            float _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2, _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float, _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float);
            float _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float = __7;
            float _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float;
            Unity_Multiply_float_float(_GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float, _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float);
            float _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float;
            Unity_Add_float(_Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float, _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float);
            float _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float;
            Unity_Add_float(float(1), _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float);
            float _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float;
            Unity_Divide_float(_Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float, _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float);
            float3 _Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.ObjectSpaceNormal, (_Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float.xxx), _Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3);
            float _Property_2825612abf5e4489b5821e858fe2ea4b_Out_0_Float = __2;
            float3 _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3;
            Unity_Multiply_float3_float3(_Multiply_1aa3816931af4d8d9e843bd646885483_Out_2_Vector3, (_Property_2825612abf5e4489b5821e858fe2ea4b_Out_0_Float.xxx), _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3);
            float3 _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3;
            Unity_Add_float3(IN.ObjectSpacePosition, _Multiply_4ce320a4fc7a4273a3b4c0c3afbdc07e_Out_2_Vector3, _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3);
            float3 _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3;
            Unity_Add_float3(_Multiply_bbea90bb8b8346fcb23073673f94ea6c_Out_2_Vector3, _Add_9484feec886f4c02987b1754f21117db_Out_2_Vector3, _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3);
            description.Position = _Add_3649ea9512b747d88d5580ae45568307_Out_2_Vector3;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float Alpha;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _Property_f2589fddf60243b8b845a860bbba81d9_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color_1) : _Color_1;
            float4 _Property_9bb80aef4a694edb99443da55a608468_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
            float _Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float = _Nome;
            float _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float = _Edge;
            float4 _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4 = _;
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_R_1_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[0];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_G_2_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[1];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_B_3_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[2];
            float _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float = _Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4[3];
            float3 _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3;
            Unity_Rotate_About_Axis_Degrees_float(IN.WorldSpacePosition, (_Property_9be7ecdcce254294887e7aaa17764f68_Out_0_Vector4.xyz), _Split_2ee2a52587b049b8b2eb12217e86a96e_A_4_Float, _RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3);
            float _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float = __1;
            float _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_5ee944922a744210bdf7c2a3b3bf9f9b_Out_0_Float, _Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float);
            float2 _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_10e88b33070a4b16b3aded34fbbda9b6_Out_2_Float.xx), _TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2);
            float _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float = _Scale;
            float _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_9576faa4f3704d12ad88f057363e35fe_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float);
            float2 _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), float2 (0, 0), _TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2);
            float _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_b29a87efa2bc47e9b5e1ac37ab82e8f6_Out_3_Vector2, _Property_73f9360645d94a85b32bc0c43a530600_Out_0_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float);
            float _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float;
            Unity_Add_float(_GradientNoise_634970769f5d4df590d627be5c37a347_Out_2_Float, _GradientNoise_6debce6ec27341bda3a3062c8dacd1b8_Out_2_Float, _Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float);
            float _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float;
            Unity_Saturate_float(_Add_06f3c402614d4e9abffe77869eb44b5a_Out_2_Float, _Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float);
            float _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float;
            Unity_Divide_float(_Saturate_5d8d138b375749739bf21431820a8f66_Out_1_Float, float(2), _Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float);
            float _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float = __4;
            float _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float;
            Unity_Power_float(_Divide_94adae1e52e64009a181e3425b968ca7_Out_2_Float, _Property_dec3c7795f9e4673af58b5a90bb9f6b0_Out_0_Float, _Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float);
            float4 _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4 = __3;
            float _Split_09487d18a83e485886a34ed861cc7b18_R_1_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[0];
            float _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[1];
            float _Split_09487d18a83e485886a34ed861cc7b18_B_3_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[2];
            float _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float = _Property_d37845aa503f40cab0d67d9a19e479e8_Out_0_Vector4[3];
            float4 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4;
            float3 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3;
            float2 _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_R_1_Float, _Split_09487d18a83e485886a34ed861cc7b18_G_2_Float, float(0), float(0), _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGBA_4_Vector4, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RGB_5_Vector3, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2);
            float4 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4;
            float3 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3;
            float2 _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2;
            Unity_Combine_float(_Split_09487d18a83e485886a34ed861cc7b18_B_3_Float, _Split_09487d18a83e485886a34ed861cc7b18_A_4_Float, float(0), float(0), _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGBA_4_Vector4, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RGB_5_Vector3, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2);
            float _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float;
            Unity_Remap_float(_Power_beff927ec5484ee49c9401772b0fc7c9_Out_2_Float, _Combine_5df6c778cc164cfdb47b0ee9a54168f9_RG_6_Vector2, _Combine_8cd57a0eec6e4c1bbf05d6df44e98397_RG_6_Vector2, _Remap_16660554acc34982b32fa3029894c38d_Out_3_Float);
            float _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float;
            Unity_Absolute_float(_Remap_16660554acc34982b32fa3029894c38d_Out_3_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float);
            float _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float;
            Unity_Smoothstep_float(_Property_2b09296e007e4cad85bc43787ed6a0d3_Out_0_Float, _Property_5fcc310e13724fdfa3f255d36653c130_Out_0_Float, _Absolute_e6c69e1480dd4b658f5403f6fafa2ee6_Out_1_Float, _Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float);
            float _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float = __6;
            float _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_155129c7aa614972b82c80ab02218d58_Out_0_Float, _Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float);
            float2 _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2;
            Unity_TilingAndOffset_float((_RotateAboutAxis_f248bf0a59524383b6e3b52cd3851363_Out_3_Vector3.xy), float2 (1, 1), (_Multiply_7250b8f2544143efb56453dea1b9a570_Out_2_Float.xx), _TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2);
            float _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float = __5;
            float _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float;
            Unity_GradientNoise_Deterministic_float(_TilingAndOffset_277d71496adc495ba13ed7f073953039_Out_3_Vector2, _Property_4d01c71628d04aef97683faf53bebcc5_Out_0_Float, _GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float);
            float _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float = __7;
            float _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float;
            Unity_Multiply_float_float(_GradientNoise_a99328c99a6f47619551dcebf2d0f920_Out_2_Float, _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float);
            float _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float;
            Unity_Add_float(_Smoothstep_0f88831a2f19416da35791ed3650bb60_Out_3_Float, _Multiply_bdf5cff48fe349258a30ce974389ac43_Out_2_Float, _Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float);
            float _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float;
            Unity_Add_float(float(1), _Property_5c2105cd27c94e1cac77cbc11886acfa_Out_0_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float);
            float _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float;
            Unity_Divide_float(_Add_4eed771ff6be4798a1785cf3c5692c45_Out_2_Float, _Add_7e1ef21e5d89424390464f908dd8be88_Out_2_Float, _Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float);
            float4 _Lerp_e2a3e13e094c4f3eb442b0a9f18f31fc_Out_3_Vector4;
            Unity_Lerp_float4(_Property_f2589fddf60243b8b845a860bbba81d9_Out_0_Vector4, _Property_9bb80aef4a694edb99443da55a608468_Out_0_Vector4, (_Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float.xxxx), _Lerp_e2a3e13e094c4f3eb442b0a9f18f31fc_Out_3_Vector4);
            float _Property_f736fe6426514efd98c7663b4d6820f5_Out_0_Float = __10;
            float _FresnelEffect_09fa928a853b43b891edcdf74a7ce03f_Out_3_Float;
            Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_f736fe6426514efd98c7663b4d6820f5_Out_0_Float, _FresnelEffect_09fa928a853b43b891edcdf74a7ce03f_Out_3_Float);
            float _Multiply_a73906a05cc645f4b196a202187de6e5_Out_2_Float;
            Unity_Multiply_float_float(_Divide_d1caf9b4f74e4ed4b89929ebb7a23ed8_Out_2_Float, _FresnelEffect_09fa928a853b43b891edcdf74a7ce03f_Out_3_Float, _Multiply_a73906a05cc645f4b196a202187de6e5_Out_2_Float);
            float _Property_e35af77fc1e04fac827732a570d397e4_Out_0_Float = _Fresnel;
            float _Multiply_6236f29104324d4b9b06b92acb03049d_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_a73906a05cc645f4b196a202187de6e5_Out_2_Float, _Property_e35af77fc1e04fac827732a570d397e4_Out_0_Float, _Multiply_6236f29104324d4b9b06b92acb03049d_Out_2_Float);
            float4 _Add_c0cdd70753804266aa9c0047174d2bd5_Out_2_Vector4;
            Unity_Add_float4(_Lerp_e2a3e13e094c4f3eb442b0a9f18f31fc_Out_3_Vector4, (_Multiply_6236f29104324d4b9b06b92acb03049d_Out_2_Float.xxxx), _Add_c0cdd70753804266aa9c0047174d2bd5_Out_2_Vector4);
            float _SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float;
            Unity_SceneDepth_Eye_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float);
            float4 _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4 = IN.ScreenPosition;
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_R_1_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[0];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_G_2_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[1];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_B_3_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[2];
            float _Split_f132a71f4c3a46a3aaad77428bed17dc_A_4_Float = _ScreenPosition_cee8c4c7fc3741e68d21d8327a8889cb_Out_0_Vector4[3];
            float _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float;
            Unity_Subtract_float(_Split_f132a71f4c3a46a3aaad77428bed17dc_A_4_Float, float(1), _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float);
            float _Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float;
            Unity_Subtract_float(_SceneDepth_6f9de563a5164651832a966212a0181a_Out_1_Float, _Subtract_df78ae40314b4bb4b6cc9f4057f5bd81_Out_2_Float, _Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float);
            float _Property_8bd450fff67f41e68636a90c818a9e85_Out_0_Float = __11;
            float _Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float;
            Unity_Divide_float(_Subtract_e464c0fcf808463d82ee2f9803a8e2ba_Out_2_Float, _Property_8bd450fff67f41e68636a90c818a9e85_Out_0_Float, _Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float);
            float _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float;
            Unity_Saturate_float(_Divide_ceb8b760f52248debd1713b1b47ceef3_Out_2_Float, _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float);
            surface.BaseColor = (_Add_c0cdd70753804266aa9c0047174d2bd5_Out_2_Vector4.xyz);
            surface.Alpha = _Saturate_0024702821b24c60ab23fd93526be140_Out_1_Float;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.TimeParameters =                             _TimeParameters.xyz;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
        
        
            output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
            output.WorldSpacePosition = input.positionWS;
            output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);
        
            #if UNITY_UV_STARTS_AT_TOP
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #else
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #endif
        
            output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
            output.NDCPosition.y = 1.0f - output.NDCPosition.y;
        
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
            output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    CustomEditorForRenderPipeline "UnityEditor.ShaderGraphUnlitGUI" "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
    FallBack "Hidden/Shader Graph/FallbackError"
}