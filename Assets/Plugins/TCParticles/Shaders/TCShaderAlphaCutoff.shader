Shader "TCParticles/Alpha cutout" 
{
	Properties 
	{
		_MainTex ("Texture", 2D) = "white" { }
		_Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
		_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	}

	Category 
	{
		Tags { "Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="Opaque" "TCParticles"="True" }


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
				float _Cutoff;

				float4 frag (particle_fragment i) : SV_Target
				{
					half4 col =  tex2Dbias(_MainTex, float4(i.uv, 0, -3)) * i.col;


					if (col.a < _Cutoff)
						discard;

					return col;
				}

				ENDCG
			}
		}
	}

	Fallback "Diffuse"
}

