Shader "TCParticles/Offscreen/Offscreen" 
{
	Properties 
	{
		_MainTex ("Texture", 2D) = "white" { }
		_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
		_InvFade ("Soft Particles Factor", Range(10, 80)) = 15
	}

	Category 
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "TCParticles"="True" "TCOffscreen"="True"}
		
		ZWrite Off 
		Blend One One

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

				uniform sampler2D _MainTex;
				uniform sampler2D _TCDepth;

				float2 _TCRes;
				float _InvFade;

				fixed4 frag (particle_fragment i) : SV_Target
				{
					float2 suv = float2(i.pos.x, i.pos.y) / (_TCRes);

				
					#if UNITY_UV_STARTS_AT_TOP
						suv.y = 1.0f - suv.y;
					#endif

					
					float z = Linear01Depth(i.pos.z);
					float sz = DecodeFloatRGBA(tex2D(_TCDepth, half2(suv.x, suv.y)).rgba);

					float fade = saturate (_InvFade * (sz - z));
					i.col *= fade;



					return tex2Dbias(_MainTex, float4(i.uv, 0, -3)) * i.col;
				}
				
				ENDCG
				
			}
		}
	}

	Fallback "Diffuse"
}
