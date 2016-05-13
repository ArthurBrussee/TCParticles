Shader "Hidden/TCParticles/OffscreenCompositeGradient" 
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
			
			fixed4 frag (v2f_img i) : COLOR
			{
				fixed3 screen = tex2D(_MainTex, i.uv).rgb;
				float parta = tex2D(_ParticleTex, i.uv).a;
				float4 gradient = tex2D(_Gradient, float2(parta * _GradientScale, 0.0f)).rgba;
				
				return float4(screen.rgb * (1 - gradient.a) + gradient.rgb * gradient.a, 1.0);
			}

			ENDCG
		}

	}
	Fallback "Diffuse"
}
