// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "WRL/ParallaxOffset"
{
	Properties
	{
		_MainTexture("视差纹理", 2D) = "white" {}
		_Color("折射颜色", Color) = (1,1,1,0)
		_shendu1("深度1", Float) = 0
		_shendu1Speed("深度1速度", Float) = 0
		_shendu2("深度2", Float) = 0
		_shendu2Speed("深度2速度", Float) = 0
		_Alpha("Alpha", Range( 0 , 1)) = 0.5
		_NormalTexture("屏幕扰动纹理", 2D) = "bump" {}
		_NormalSpeedX("屏幕扰动速度X", Float) = 0
		_NormalSpeedY("屏幕扰动速度Y", Float) = 0
		_NormalScale("屏幕扰动强度", Float) = 0
		[HDR]_FresnelColor("边缘光颜色", Color) = (0,0,0,0)
		_Fresnel("边缘光设置", Vector) = (0,0,0,0)
		_NoiseTexture("扰动纹理", 2D) = "white" {}
		_NoiseSpeedX("扰动X速度", Float) = 0
		_NoiseSpeedY("扰动Y速度", Float) = 0

	}
	
	SubShader
	{
		
		
		Tags { "RenderType"="Transparent" "Queue"="Geometry" }
	LOD 100

		CGINCLUDE
		#pragma target 3.0
		ENDCG
		Blend SrcAlpha OneMinusSrcAlpha
		AlphaToMask Off
		Cull Back
		ColorMask RGBA
		ZWrite On
		ZTest LEqual
		Offset 0 , 0
		
		
		GrabPass{ }

		Pass
		{
			Name "Unlit"
			Tags { "LightMode"="ForwardBase" }
			CGPROGRAM

			#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
			#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex);
			#else
			#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex)
			#endif


			#ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
			//only defining to not throw compilation error over Unity 5.5
			#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
			#endif
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"
			#include "UnityShaderVariables.cginc"
			#include "UnityStandardUtils.cginc"
			#include "UnityStandardBRDF.cginc"
			#define ASE_NEEDS_FRAG_WORLD_POSITION


			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_tangent : TANGENT;
				float3 ase_normal : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				float3 worldPos : TEXCOORD0;
				#endif
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			uniform float4 _Color;
			ASE_DECLARE_SCREENSPACE_TEXTURE( _GrabTexture )
			uniform sampler2D _NormalTexture;
			uniform float _NormalSpeedX;
			uniform float _NormalSpeedY;
			uniform float4 _NormalTexture_ST;
			uniform float _NormalScale;
			uniform sampler2D _MainTexture;
			uniform float4 _MainTexture_ST;
			uniform float _shendu1;
			uniform float _shendu1Speed;
			uniform float _shendu2;
			uniform float _shendu2Speed;
			uniform float4 _Fresnel;
			uniform float4 _FresnelColor;
			uniform sampler2D _NoiseTexture;
			uniform float _NoiseSpeedX;
			uniform float _NoiseSpeedY;
			uniform float4 _NoiseTexture_ST;
			uniform float _Alpha;

			
			v2f vert ( appdata v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				UNITY_TRANSFER_INSTANCE_ID(v, o);

				float4 ase_clipPos = UnityObjectToClipPos(v.vertex);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord1 = screenPos;
				float3 ase_worldTangent = UnityObjectToWorldDir(v.ase_tangent);
				o.ase_texcoord3.xyz = ase_worldTangent;
				float3 ase_worldNormal = UnityObjectToWorldNormal(v.ase_normal);
				o.ase_texcoord4.xyz = ase_worldNormal;
				float ase_vertexTangentSign = v.ase_tangent.w * unity_WorldTransformParams.w;
				float3 ase_worldBitangent = cross( ase_worldNormal, ase_worldTangent ) * ase_vertexTangentSign;
				o.ase_texcoord5.xyz = ase_worldBitangent;
				
				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord2.zw = 0;
				o.ase_texcoord3.w = 0;
				o.ase_texcoord4.w = 0;
				o.ase_texcoord5.w = 0;
				float3 vertexValue = float3(0, 0, 0);
				#if ASE_ABSOLUTE_VERTEX_POS
				vertexValue = v.vertex.xyz;
				#endif
				vertexValue = vertexValue;
				#if ASE_ABSOLUTE_VERTEX_POS
				v.vertex.xyz = vertexValue;
				#else
				v.vertex.xyz += vertexValue;
				#endif
				o.vertex = UnityObjectToClipPos(v.vertex);

				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				#endif
				return o;
			}
			
			fixed4 frag (v2f i ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				fixed4 finalColor;
				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				float3 WorldPosition = i.worldPos;
				#endif
				float4 screenPos = i.ase_texcoord1;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 appendResult119 = (float2(_NormalSpeedX , _NormalSpeedY));
				float2 uv_NormalTexture = i.ase_texcoord2.xy * _NormalTexture_ST.xy + _NormalTexture_ST.zw;
				float2 panner117 = ( 1.0 * _Time.y * appendResult119 + uv_NormalTexture);
				float4 screenColor210 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture,( ase_screenPosNorm + float4( UnpackScaleNormal( tex2D( _NormalTexture, panner117 ), _NormalScale ) , 0.0 ) ).xy);
				float2 uv_MainTexture = i.ase_texcoord2.xy * _MainTexture_ST.xy + _MainTexture_ST.zw;
				float3 ase_worldTangent = i.ase_texcoord3.xyz;
				float3 ase_worldNormal = i.ase_texcoord4.xyz;
				float3 ase_worldBitangent = i.ase_texcoord5.xyz;
				float3 tanToWorld0 = float3( ase_worldTangent.x, ase_worldBitangent.x, ase_worldNormal.x );
				float3 tanToWorld1 = float3( ase_worldTangent.y, ase_worldBitangent.y, ase_worldNormal.y );
				float3 tanToWorld2 = float3( ase_worldTangent.z, ase_worldBitangent.z, ase_worldNormal.z );
				float3 ase_worldViewDir = UnityWorldSpaceViewDir(WorldPosition);
				ase_worldViewDir = normalize(ase_worldViewDir);
				float3 ase_tanViewDir =  tanToWorld0 * ase_worldViewDir.x + tanToWorld1 * ase_worldViewDir.y  + tanToWorld2 * ase_worldViewDir.z;
				ase_tanViewDir = Unity_SafeNormalize( ase_tanViewDir );
				float2 Offset189 = ( ( _shendu1 - 1 ) * ase_tanViewDir.xy * 1.0 ) + uv_MainTexture;
				float2 Offset195 = ( ( _shendu2 - 1 ) * ase_tanViewDir.xy * 1.0 ) + uv_MainTexture;
				float fresnelNdotV203 = dot( ase_worldNormal, ase_worldViewDir );
				float fresnelNode203 = ( _Fresnel.x + _Fresnel.y * pow( max( 1.0 - fresnelNdotV203 , 0.0001 ), _Fresnel.z ) );
				float2 appendResult16 = (float2(_NoiseSpeedX , _NoiseSpeedY));
				float2 uv_NoiseTexture = i.ase_texcoord2.xy * _NoiseTexture_ST.xy + _NoiseTexture_ST.zw;
				float2 panner20 = ( 1.0 * _Time.y * appendResult16 + uv_NoiseTexture);
				float4 appendResult214 = (float4((( ( _Color * screenColor210 ) + tex2D( _MainTexture, ( Offset189 + ( _Time.y * _shendu1Speed ) ) ) + tex2D( _MainTexture, ( Offset195 + ( _Time.y * _shendu2Speed ) ) ) + ( saturate( fresnelNode203 ) * _FresnelColor * tex2D( _NoiseTexture, panner20 ).r ) )).rgb , _Alpha));
				
				
				finalColor = appendResult214;
				return finalColor;
			}
			ENDCG
		}
	}
	CustomEditor "ASEMaterialInspector"
	
	
}
/*ASEBEGIN
Version=18800
7;7;1906;1004;-1284.7;92.26599;1;True;False
Node;AmplifyShaderEditor.CommentaryNode;221;55.00067,-780.0104;Inherit;False;1791.849;896.3732;Comment;12;115;117;123;119;116;121;122;209;211;212;124;210;屏幕扰动/热扭曲/折射;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;122;133.8734,10.06273;Inherit;False;Property;_NormalSpeedY;屏幕扰动速度Y;9;0;Create;False;0;0;0;False;0;False;0;0.2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;121;136.0683,-119.9045;Inherit;False;Property;_NormalSpeedX;屏幕扰动速度X;8;0;Create;False;0;0;0;False;0;False;0;0.2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;116;101.4794,-285.9512;Inherit;False;0;115;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;119;366.0688,-87.90445;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;223;523.9556,917.2365;Inherit;False;1351.731;945.5244;Comment;11;21;20;17;16;18;19;204;203;205;207;225;边缘光;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;222;-2013.016,262.1425;Inherit;False;2246.452;1335.418;Comment;16;182;177;181;187;197;183;196;189;194;195;185;199;193;184;186;191;双层视差;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;123;610.7328,-140.4193;Inherit;False;Property;_NormalScale;屏幕扰动强度;10;0;Create;False;0;0;0;False;0;False;0;0.02;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;117;576.3957,-275.972;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;18;577.8323,1632.814;Inherit;False;Property;_NoiseSpeedX;扰动X速度;14;0;Create;False;0;0;0;False;0;False;0;-0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;19;589.8323,1740.814;Inherit;False;Property;_NoiseSpeedY;扰动Y速度;15;0;Create;False;0;0;0;False;0;False;0;-0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;199;-1523.672,1123.442;Inherit;False;Property;_shendu2;深度2;4;0;Create;False;0;0;0;False;0;False;0;0.89;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;191;-1353.901,1405.771;Inherit;False;Property;_shendu2Speed;深度2速度;5;0;Create;False;0;0;0;False;0;False;0;-0.01;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;182;-1583.281,922.6417;Inherit;False;Tangent;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;184;-887.3151,783.3989;Inherit;False;Property;_shendu1Speed;深度1速度;3;0;Create;False;0;0;0;False;0;False;0;0.02;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;186;-882.0753,623.469;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;193;-1375.69,1269.239;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;177;-1744.567,329.0492;Inherit;False;0;197;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ScreenPosInputsNode;209;857.5311,-556.9783;Float;False;0;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;16;796.8323,1693.814;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;17;564.8323,1462.814;Inherit;False;0;21;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector4Node;207;665.8224,1007.026;Inherit;False;Property;_Fresnel;边缘光设置;12;0;Create;False;0;0;0;False;0;False;0,0,0,0;0,2,6,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;115;857.7378,-309.151;Inherit;True;Property;_NormalTexture;屏幕扰动纹理;7;0;Create;False;0;0;0;False;0;False;-1;None;966ba28b8707b8b44bffe59b46a614aa;True;0;False;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;181;-1661.503,535.4695;Inherit;False;Property;_shendu1;深度1;2;0;Create;False;0;0;0;False;0;False;0;-0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;211;1194.096,-542.7363;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.FresnelNode;203;955.3088,989.0906;Inherit;False;Standard;WorldNormal;ViewDir;False;True;5;0;FLOAT3;0,0,1;False;4;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;20;954.8323,1471.814;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;185;-669.2682,621.5649;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ParallaxMappingNode;195;-1094.26,1034.325;Inherit;False;Normal;4;0;FLOAT2;0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ParallaxMappingNode;189;-1047.023,366.1762;Inherit;False;Normal;4;0;FLOAT2;0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;194;-1016.913,1264.26;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;183;-452.5133,402.8417;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ColorNode;124;1245.058,-734.3492;Inherit;False;Property;_Color;折射颜色;1;0;Create;False;0;0;0;False;0;False;1,1,1,0;0.5754716,0.5754716,0.5754716,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ScreenColorNode;210;1356.062,-549.3149;Inherit;False;Global;_GrabScreen0;Grab Screen 0;20;0;Create;True;0;0;0;False;0;False;Object;-1;False;False;1;0;FLOAT2;0,0;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;205;957.3452,1221.634;Inherit;False;Property;_FresnelColor;边缘光颜色;11;1;[HDR];Create;False;0;0;0;False;0;False;0,0,0,0;1.854524,1.949628,9.082411,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;225;1258.336,990.743;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;21;1236.859,1449.623;Inherit;True;Property;_NoiseTexture;扰动纹理;13;0;Create;False;0;0;0;False;0;False;-1;None;24886c3695d1440428d752317c49b00e;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;196;-718.3693,1053.601;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;204;1690.26,992.2684;Inherit;False;3;3;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;212;1671.998,-562.606;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;187;-197.4768,384.6709;Inherit;True;Property;_TextureSample0;Texture Sample 0;0;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;197;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;197;-566.0678,1017.901;Inherit;True;Property;_MainTexture;视差纹理;0;0;Create;False;0;0;0;False;0;False;-1;None;aa758e39208a3734db2b900833ae8583;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;217;2050.093,328.7576;Inherit;False;4;4;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ComponentMaskNode;216;2285.069,322.1665;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;45;2221.297,673.7411;Inherit;False;Property;_Alpha;Alpha;6;0;Create;True;0;0;0;False;0;False;0.5;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;214;2592.586,388.1055;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;213;2922.505,452.9919;Float;False;True;-1;2;ASEMaterialInspector;100;1;WRL/ParallaxOffset;0770190933193b94aaa3065e307002fa;True;Unlit;0;0;Unlit;2;True;2;5;False;-1;10;False;-1;0;1;False;-1;0;False;-1;True;0;False;-1;0;False;-1;False;False;False;False;False;False;True;0;False;-1;True;0;False;-1;True;True;True;True;True;0;False;-1;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;True;1;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;2;RenderType=Transparent=RenderType;Queue=Geometry=Queue=0;True;2;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=ForwardBase;False;0;;0;0;Standard;1;Vertex Position,InvertActionOnDeselection;1;0;1;True;False;;False;0
WireConnection;119;0;121;0
WireConnection;119;1;122;0
WireConnection;117;0;116;0
WireConnection;117;2;119;0
WireConnection;16;0;18;0
WireConnection;16;1;19;0
WireConnection;115;1;117;0
WireConnection;115;5;123;0
WireConnection;211;0;209;0
WireConnection;211;1;115;0
WireConnection;203;1;207;1
WireConnection;203;2;207;2
WireConnection;203;3;207;3
WireConnection;20;0;17;0
WireConnection;20;2;16;0
WireConnection;185;0;186;0
WireConnection;185;1;184;0
WireConnection;195;0;177;0
WireConnection;195;1;199;0
WireConnection;195;3;182;0
WireConnection;189;0;177;0
WireConnection;189;1;181;0
WireConnection;189;3;182;0
WireConnection;194;0;193;0
WireConnection;194;1;191;0
WireConnection;183;0;189;0
WireConnection;183;1;185;0
WireConnection;210;0;211;0
WireConnection;225;0;203;0
WireConnection;21;1;20;0
WireConnection;196;0;195;0
WireConnection;196;1;194;0
WireConnection;204;0;225;0
WireConnection;204;1;205;0
WireConnection;204;2;21;1
WireConnection;212;0;124;0
WireConnection;212;1;210;0
WireConnection;187;1;183;0
WireConnection;197;1;196;0
WireConnection;217;0;212;0
WireConnection;217;1;187;0
WireConnection;217;2;197;0
WireConnection;217;3;204;0
WireConnection;216;0;217;0
WireConnection;214;0;216;0
WireConnection;214;3;45;0
WireConnection;213;0;214;0
ASEEND*/
//CHKSM=8CE1229A0B3CE64808B67171EA92FFE5D841989D