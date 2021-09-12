Shader "Hidden/RayMarchLight"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
		LOD 100

		HLSLINCLUDE

		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

		float _ShadowProjCoef;
		float _DensityCoef;
		float _FogHeightCoef;
		float _ExtinctionCoef;
		float3 _CameraForward;
		float4 _VolumetricLight;
		float4 _MieG;
		float4 _LightDir;
		float _MaxRayLength;
		int _SampleCount;

		float4 _FrustumCorners[4];
		struct Attributes
		{
			float4 positionOS   : POSITION;
			float2 uv           : TEXCOORD0;
		};

		struct Varyings
		{
			float4 positionHCS  : SV_POSITION;
			float2 uv           : TEXCOORD0;
			float3 wpos         : TEXCOORD1;
		};

		TEXTURE2D(_DitherTex);
		SAMPLER(sampler_DitherTex);
		float4 _DitherTex_TexelSize;


		half GetShadowAttenuation(float3 wpos)
		{
			float4 coords = TransformWorldToShadowCoord(wpos);
			half atten = MainLightRealtimeShadow(coords);
			return atten;
		}
		float GetDensity(float3 wpos)
		{
			float density = 1.0f;
			density *= 0.3f;
			density *= exp(min(-(wpos.y * _FogHeightCoef), 0));
			return density;
		}
		float4 MieScattering(float cosAngle, float4 g)
		{
			return g.w * (g.x / (pow(g.y - g.z * cosAngle, 1.5)));
		}
		float4 RayMarch(float2 screenPos, float3 rayStart, float3 rayDir, float rayLength)
		{
			float2 offsetUV = fmod(floor(screenPos.xy), 8.0f)/8.0f + float2(0.5f/8.0f, 0.5f/8.0f);
			float offset = SAMPLE_TEXTURE2D(_DitherTex, sampler_DitherTex, offsetUV).w;
			int stepCount = _SampleCount;

			float stepLen = rayLength / stepCount;
			float3 step = rayDir * stepLen;

			float3 stepWpos = rayStart + step * offset;
			float3 lightDir = _LightDir.xyz;
			float extinction = 0;
			float4 resultLight = 0;
			float transmittance = 1;
			[loop]
			for (int i = 0; i < stepCount; ++i)
			{
				float shadowAttenuation = GetShadowAttenuation(stepWpos);//阴影扣减
				float projectionDe = (1 - shadowAttenuation) * _ShadowProjCoef * stepLen * 0.1f;//投影增强
				float density = GetDensity(stepWpos) * _DensityCoef;//雾浓度
				float scattering = density  * stepLen;//散射
				extinction += stepLen * density * _ExtinctionCoef * 0.1f;
				transmittance *= exp(-extinction);
				float4 light = transmittance * shadowAttenuation * scattering - projectionDe;
				resultLight += light;
				stepWpos += step;
			}
			float cosAngle = dot(lightDir, -rayDir);
			resultLight *= MieScattering(cosAngle, _MieG);
			resultLight = max(0, resultLight);
			return resultLight;
		}

		ENDHLSL

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vertDir
			#pragma fragment fragDir
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN

			Varyings vertDir(Attributes IN)
			{
				Varyings OUT;
				OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
				OUT.uv.xy = UnityStereoTransformScreenSpaceTex(IN.uv);
				OUT.wpos = _FrustumCorners[IN.uv.x + IN.uv.y * 2];
				return OUT;
			}

			half4 fragDir(Varyings IN) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
#if UNITY_REVERSED_Z
				real depth = SampleSceneDepth(IN.uv.xy);
#else
				// Adjust Z to match NDC for OpenGL ([-1, 1])
				real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(IN.uv.xy));
#endif
				//float3 wpos = ComputeWorldSpacePosition(IN.uv.xy, depth, UNITY_MATRIX_I_VP);
				float3 wpos = IN.wpos;

				float3 rayDir = wpos - _WorldSpaceCameraPos;
				rayDir *= depth;
				float rayLength = length(rayDir);
				rayDir /= rayLength;

				float4 color = RayMarch(IN.positionHCS.xy, _WorldSpaceCameraPos, rayDir, rayLength);
				return color;
			}
			ENDHLSL
		}
	}
}
