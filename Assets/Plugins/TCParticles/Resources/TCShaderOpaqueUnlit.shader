Shader "TCParticles/Opaque unlit" 
{
	Properties 
	{
		_MainTex ("Texture", 2D) = "white" { }
		_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	}

	Category 
	{
		Tags { "Queue"="Geometry" "IgnoreProjector"="True" "TCParticles"="True" }
		ZWrite On
		Blend SrcAlpha OneMinusSrcAlpha
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
					return  tex2Dbias(_MainTex, float4(i.uv, 0, -3)) * i.col;
				}

				ENDCG
			}
		}
	}

	Fallback "Diffuse"
}
