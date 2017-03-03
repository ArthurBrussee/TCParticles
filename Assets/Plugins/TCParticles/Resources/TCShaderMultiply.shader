Shader "TCParticles/Multiply" 
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" { }
		_TintColor("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	}

	Category {
		Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "TCParticles" = "True" }
		Blend Zero SrcColor
		AlphaTest Greater .01
		ColorMask RGB
		ZWrite Off

		SubShader {
			Pass {
				CGPROGRAM

				#pragma target 5.0
				#pragma multi_compile TC_BILLBOARD TC_BILLBOARD_STRETCHED TC_MESH
				#pragma multi_compile TC_UV_NORMAL TC_UV_SPRITE_ANIM

				#pragma vertex particle_vertex
				#pragma fragment frag

				#include "TCShaderInc.cginc"

				sampler2D _MainTex;

				float4 frag(particle_fragment i) : SV_Target
				{
					half4 prev = tex2Dbias(_MainTex, float4(i.uv, 0, -3)) * i.col;
					return lerp(half4(1,1,1,1), prev, prev.a);
				}

				ENDCG
			}
		}
	}

	Fallback "Diffuse"
}