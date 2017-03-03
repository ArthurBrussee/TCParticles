Shader "TCParticles/Blend" 
{
	Properties 
	{
		_MainTex ("Texture", 2D) = "white" { }
		_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	}

	Category 
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "TCParticles"="True" }
		
		
				
		ZWrite Off 

		SubShader 
		{
			Pass 
			{
				Blend One One
				BlendOp RevSub

				CGPROGRAM

				#pragma target 5.0
				#pragma multi_compile TC_BILLBOARD TC_BILLBOARD_STRETCHED TC_MESH
				#pragma multi_compile TC_UV_NORMAL TC_UV_SPRITE_ANIM

				#pragma vertex particle_vertex
				#pragma fragment frag

				#include "TCShaderInc.cginc"

				sampler2D _MainTex;

				fixed4 frag (particle_fragment i) : SV_Target
				{
					
					float4 col = tex2Dbias(_MainTex, float4(i.uv, 0, -3)) * i.col;
					return col.r * 5.0f * col.a;
				}
				
				ENDCG
			}
			
			Pass 
			{
				Blend SrcAlpha One

				CGPROGRAM

				#pragma target 5.0
				#pragma multi_compile TC_BILLBOARD TC_BILLBOARD_STRETCHED TC_BILLBOARD_TAILSTRETCH TC_MESH
				#pragma multi_compile TC_ALIGNED TC_ROTATE
				#pragma multi_compile TC_SIZE TC_PIXEL_SIZE 
				#pragma multi_compile TC_PERSPECTIVE TC_ORTHOGRAPHIC
				#pragma vertex particle_vertex
				#pragma fragment frag

				#include "TCShaderInc.cginc"

				sampler2D _MainTex;

				fixed4 frag (particle_fragment i) : SV_Target
				{
					return tex2Dbias(_MainTex, float4(i.uv, 0, -3)) * i.col;
				}
				
				ENDCG
			}
		}
	}
	Fallback "Diffuse"
}
