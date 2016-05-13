Shader "Hidden/TCParticles/OffscreenComposite" 
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
			
			fixed4 frag (v2f_img i) : COLOR
			{
				fixed3 screen = tex2D(_MainTex, i.uv).rgb;
				float4 part = tex2D(_ParticleTex, i.uv);
				
				return float4(screen.rgb * (1 - part.a) + part.rgb * part.a, 1.0);
			}

			ENDCG
		}

	}
	Fallback "Diffuse"
}
