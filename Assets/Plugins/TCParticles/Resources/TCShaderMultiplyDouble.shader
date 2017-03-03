Shader "TCParticles/Multiply (Double)" 
{
	Properties 
	{
		_MainTex ("Texture", 2D) = "white" { }
		_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	}

	Category 
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "TCParticles"="True" }
		Blend DstColor SrcColor
		AlphaTest Greater .01
		ColorMask RGB
		ZWrite Off 

		SubShader 
		{
			Pass 
			{
				CGPROGRAM

				#pragma target 5.0
				#pragma multi_compile TC_BILLBOARD TC_BILLBOARD_STRETCHED TC_MESH
				#pragma multi_compile TC_UV_NORMAL TC_UV_SPRITE_ANIM

				#pragma vertex particle_vertex
				#pragma fragment frag

				#include "TCShaderInc.cginc"

				sampler2D _MainTex;

				float4 frag (particle_fragment i) : SV_Target
				{
					fixed4 col;
					fixed4 tex = tex2Dbias(_MainTex, float4(i.uv, 0, -3)) * i.col;
					col.rgb = tex.rgb * i.col.rgb * 2;
					col.a = i.col.a * tex.a;
					return lerp(fixed4(0.5f,0.5f,0.5f,0.5f), col, col.a);
				}

				ENDCG
			}
		}
	}

	Fallback "Diffuse"
}
