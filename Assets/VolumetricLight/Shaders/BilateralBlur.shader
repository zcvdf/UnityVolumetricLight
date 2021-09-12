Shader "Hidden/BilateralBlur"
{
	Properties
	{
		[MainTexture] _MainTex("Texture", 2D) = "white"
	}
		SubShader
	{
		Cull Off ZWrite Off ZTest Always

		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
		//--------------------------------------------------------------------------------------------

		TEXTURE2D(_SampleCameraDepthTexture);
		TEXTURE2D(_RayMarchTex);
		TEXTURE2D(_MainTex);

		SAMPLER(sampler_SampleCameraDepthTexture);
		SAMPLER(sampler_RayMarchTex);
		SAMPLER(sampler_MainTex);

		float4 _SampleCameraDepthTexture_TexelSize;
		float4 _MainTex_TexelSize;
		float4 _MainTex_ST;

		int _DownSampleCout;

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


		struct v2fDownsample
		{
#if SHADER_TARGET > 40
			float2 uv : TEXCOORD0;
#else
			float2 uv00 : TEXCOORD0;
			float2 uv01 : TEXCOORD1;
			float2 uv10 : TEXCOORD2;
			float2 uv11 : TEXCOORD3;
#endif
			float4 positionHCS : SV_POSITION;
		};

		struct v2fUpsample
		{
			float2 uv : TEXCOORD0;
			float2 uv00 : TEXCOORD1;
			float2 uv01 : TEXCOORD2;
			float2 uv10 : TEXCOORD3;
			float2 uv11 : TEXCOORD4;
			float4 positionHCS : SV_POSITION;
		};

		Varyings vert(Attributes IN)
		{
			Varyings OUT;
			OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
			OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
			return OUT;
		}


		//-----------------------------------------------------------------------------------------
		// vertUpsample
		//-----------------------------------------------------------------------------------------
		v2fUpsample vertUpsample(Attributes IN, float2 texelSize)
		{
			v2fUpsample o;
			o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
			o.uv = IN.uv;

			o.uv00 = IN.uv - 0.5 * texelSize.xy;
			o.uv10 = o.uv00 + float2(texelSize.x, 0);
			o.uv01 = o.uv00 + float2(0, texelSize.y);
			o.uv11 = o.uv00 + texelSize.xy;
			return o;
		}

		//-----------------------------------------------------------------------------------------
		// BilateralUpsample
		//-----------------------------------------------------------------------------------------
		float4 BilateralUpsample(v2fUpsample input, Texture2D hiDepth, Texture2D loDepth, SamplerState pointSampler)
		{
			float4 highResDepth = LinearEyeDepth(SAMPLE_TEXTURE2D(hiDepth, pointSampler, input.uv), _ZBufferParams).xxxx;
			float4 lowResDepth;

			lowResDepth[0] = LinearEyeDepth(SAMPLE_TEXTURE2D(loDepth, pointSampler, input.uv00), _ZBufferParams);
			lowResDepth[1] = LinearEyeDepth(SAMPLE_TEXTURE2D(loDepth, pointSampler, input.uv10), _ZBufferParams);
			lowResDepth[2] = LinearEyeDepth(SAMPLE_TEXTURE2D(loDepth, pointSampler, input.uv01), _ZBufferParams);
			lowResDepth[3] = LinearEyeDepth(SAMPLE_TEXTURE2D(loDepth, pointSampler, input.uv11), _ZBufferParams);

			float4 depthDiff = abs(lowResDepth - highResDepth);

			float accumDiff = dot(depthDiff, float4(1, 1, 1, 1));
			[branch]
			if (accumDiff < 1.5) 
			{
				return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
			}
			// find nearest sample
			float minDepthDiff = depthDiff[0];
			float2 nearestUv = input.uv00;

			if (depthDiff[1] < minDepthDiff)
			{
				nearestUv = input.uv10;
				minDepthDiff = depthDiff[1];
			}

			if (depthDiff[2] < minDepthDiff)
			{
				nearestUv = input.uv01;
				minDepthDiff = depthDiff[2];
			}

			if (depthDiff[3] < minDepthDiff)
			{
				nearestUv = input.uv11;
				minDepthDiff = depthDiff[3];
			}

			return SAMPLE_TEXTURE2D(_MainTex, pointSampler, nearestUv);
		}
		static const float BLUR_DEPTH_FACTOR = 0.5;
		float CompareDepth(float3 d0, float3 d1)
		{
			float factor = abs(d0 - d1) * BLUR_DEPTH_FACTOR;
			return exp(-(factor * factor));
		}
		float4 BilateralBlur(Varyings input, int2 delta, Texture2D depth, SamplerState depthSampler)
		{
			float2 uv = input.uv;

			float3 d0 = depth.Sample(depthSampler, uv);
			float3 d1a = depth.Sample(depthSampler, uv, delta * -1);
			float3 d1b = depth.Sample(depthSampler, uv, delta * 1);
			float3 d2a = depth.Sample(depthSampler, uv, delta * -2);
			float3 d2b = depth.Sample(depthSampler, uv, delta * 2);
			float3 d3a = depth.Sample(depthSampler, uv, delta * -3);
			float3 d3b = depth.Sample(depthSampler, uv, delta * 3);
			float3 d4a = depth.Sample(depthSampler, uv, delta * -4);
			float3 d4b = depth.Sample(depthSampler, uv, delta * 4);

			float3 c0 = _MainTex.Sample(sampler_MainTex, uv);
			float3 c1a = _MainTex.Sample(sampler_MainTex, uv, delta * -1);
			float3 c1b = _MainTex.Sample(sampler_MainTex, uv, delta * 1);
			float3 c2a = _MainTex.Sample(sampler_MainTex, uv, delta * -2);
			float3 c2b = _MainTex.Sample(sampler_MainTex, uv, delta * 2);
			float3 c3a = _MainTex.Sample(sampler_MainTex, uv, delta * -3);
			float3 c3b = _MainTex.Sample(sampler_MainTex, uv, delta * 3);
			float3 c4a = _MainTex.Sample(sampler_MainTex, uv, delta * -4);
			float3 c4b = _MainTex.Sample(sampler_MainTex, uv, delta * 4);

			half w0 = 0.1994711;
			half w1a = CompareDepth(d0, d1a) * 0.1760327;
			half w1b = CompareDepth(d0, d1b) * 0.1760327;
			half w2a = CompareDepth(d0, d2a) * 0.1209854;
			half w2b = CompareDepth(d0, d2b) * 0.1209854;
			half w3a = CompareDepth(d0, d3a) * 0.0647588;
			half w3b = CompareDepth(d0, d3b) * 0.0647588;
			half w4a = CompareDepth(d0, d4a) * 0.02699548;
			half w4b = CompareDepth(d0, d4b) * 0.02699548;

			half3 result;
			result = w0 * c0.rgb;
			result += w1a * c1a.rgb;
			result += w1b * c1b.rgb;
			result += w2a * c2a.rgb;
			result += w2b * c2b.rgb;
			result += w3a * c3a.rgb;
			result += w3b * c3b.rgb;
			result += w4a * c4a.rgb;
			result += w4b * c4b.rgb;

			result /= (w0 + w1a + w1b + w2a + w2b + w3a + w3b + w4a + w4b);
			return half4(result, 1.0);
		}
		ENDHLSL

			// pass 0 - horizontal blur
			Pass
			{
				HLSLPROGRAM
				#pragma vertex vert
				#pragma fragment horizontalFrag
				#pragma target 4.0

				half4 horizontalFrag(Varyings IN) : SV_Target
				{
					return BilateralBlur(IN, int2(1, 0), _CameraDepthTexture, sampler_CameraDepthTexture);
				}

				ENDHLSL
			}

			// pass 1 - vertical blur
			Pass
			{
				HLSLPROGRAM
				#pragma vertex vert
				#pragma fragment verticalFrag
				#pragma target 4.0

				half4 verticalFrag(Varyings IN) : SV_Target
				{
					return BilateralBlur(IN, int2(0, 1), _CameraDepthTexture, sampler_CameraDepthTexture);
				}

				ENDHLSL
			}

					// pass 2 - bilateral upsample
					Pass
					{
						Blend One Zero

						HLSLPROGRAM
						#pragma vertex vertUpsampleToFull
						#pragma fragment frag		
						#pragma target 4.0

						v2fUpsample vertUpsampleToFull(Attributes IN)
						{
							return vertUpsample(IN, _SampleCameraDepthTexture_TexelSize);
						}
						float4 frag(v2fUpsample input) : SV_Target
						{
							return BilateralUpsample(input, _CameraDepthTexture, _SampleCameraDepthTexture, sampler_SampleCameraDepthTexture);
						}
						ENDHLSL
					}
	}
}
