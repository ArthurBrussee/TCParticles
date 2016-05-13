Shader "Hidden/TCParticles/OffscreenCompositeDistort" 
{
	Properties {
		_MainTex ("Screen", 2D) = "" {}
		_ColourGradient("Colour Gradient", 2D) = "" {}
	}

	SubShader 
	{
		Pass
		{
			ZTest Always
			Cull Off
			ZWrite Off
			Fog { Mode Off }
			Blend One Zero

			CGPROGRAM
		
			#pragma vertex vert_img
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#include "UnityCG.cginc"
			
			uniform sampler2D _MainTex;
			uniform sampler2D _ParticleTex;
			uniform sampler2D _Gradient;
			uniform float _GradientScale;

			uniform float _PxSizeX;
			uniform float _PxSizeY;

			uniform float _DistortStrength;
			

			fixed4 frag (v2f_img i) : COLOR
			{


				float part = tex2D(_ParticleTex, i.uv).r;

				float right = tex2D(_ParticleTex, i.uv.xy + half2(_PxSizeX, 0.0f)).r - part;
				float up = tex2D(_ParticleTex, i.uv.xy + half2(0.0f, _PxSizeY)).r - part;

				float3 normal = cross(float3(_PxSizeX, 0.0f, right), float3(0.0f, _PxSizeY, up));
			
				fixed3 screen = tex2D(_MainTex, i.uv.xy + normal.xy * _DistortStrength).rgb;
				
				return fixed4(screen, 1.0f);
			}

			ENDCG
		}

	}
	Fallback "Diffuse"
}
