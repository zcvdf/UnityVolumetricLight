Shader "Hidden/SampleDepthTex"
{
	Properties
	{
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

		struct Attributes
		{
			float4 positionOS   : POSITION;
		};
		struct Varyings
		{
			float4 positionHCS  : SV_POSITION;
		};
		Varyings vert(Attributes IN)
		{
			Varyings OUT;
			OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
			return OUT;
		}
		half4 frag(Varyings IN) : SV_Target
		{
			float2 UV = IN.positionHCS.xy / _ScaledScreenParams.xy;

			#if UNITY_REVERSED_Z
				real depth = SampleSceneDepth(UV);
			#else
				// Adjust Z to match NDC for OpenGL ([-1, 1])
				real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
			#endif
			float depthValue = Linear01Depth(depth, _ZBufferParams);
			return half4(depthValue, depthValue, depthValue, 1);
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