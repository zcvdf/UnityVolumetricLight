Shader "Hidden/Apply"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
		LOD 100

		HLSLINCLUDE
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct Attributes
			{
				float4 positionOS   : POSITION;
				float2 uv           : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionHCS  : SV_POSITION;
				float2 uv           : TEXCOORD0;
			};

			TEXTURE2D(_SourceTex);
			SAMPLER(sampler_SourceTex);
			TEXTURE2D(_RayMarchTex);
			SAMPLER(sampler_RayMarchTex);

			CBUFFER_START(UnityPerMaterial)
			float4 _SourceTex_ST;
			float4 _RayMarchTex_ST;
			CBUFFER_END

			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
				OUT.uv = TRANSFORM_TEX(IN.uv, _SourceTex);
				return OUT;
			}

			half4 frag(Varyings IN) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				half4 mainCol = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_SourceTex, IN.uv);
				half4 rayMarchCol = SAMPLE_TEXTURE2D_X(_RayMarchTex, sampler_RayMarchTex, IN.uv);
				half4 col = half4(mainCol.xyz + rayMarchCol.xyz, mainCol.w * rayMarchCol.w);
			 #ifdef _LINEAR_TO_SRGB_CONVERSION
				col = LinearToSRGB(col);
			 #endif
				return col;
			}
		ENDHLSL

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			ENDHLSL
		}
	}
}
