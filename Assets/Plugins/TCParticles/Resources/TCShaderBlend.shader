Shader "TCParticles/Legacy/Blend" {
	Properties {
		_MainTex ("Texture", 2D) = "white" { }
		_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	}

	Category  {
		Tags { "Queue"="Transparent" "RenderType"="Transparent"}

		SubShader {
			Pass {
				Blend One One
				BlendOp RevSub

				CGPROGRAM
					#pragma target 4.5
					#pragma vertex TCDefaultVert
					#pragma fragment frag

					#pragma multi_compile TC_BILLBOARD TC_BILLBOARD_STRETCHED TC_MESH

					#pragma multi_compile_instancing
					#pragma instancing_options procedural:TCDefaultProc

					#include "TCFramework.cginc"
					#include "TCDefaultVert.cginc"

					float4 frag (TCFragment i) : SV_Target {
						float4 col = tex2Dbias(_MainTex, float4(i.uv, 0, -3)) * i.col;
						return col.r * 5.0f * col.a;
					}
				ENDCG
			}
			
			Pass  {
				Blend SrcAlpha One

				CGPROGRAM
				#pragma target 4.5
				#pragma vertex TCDefaultVert
				#pragma fragment frag

				#pragma multi_compile TC_BILLBOARD TC_BILLBOARD_STRETCHED TC_MESH

				#pragma multi_compile_instancing
				#pragma instancing_options procedural:TCDefaultProc

				#include "TCFramework.cginc"
				#include "TCDefaultVert.cginc"
			
				fixed4 frag (TCFragment i) : SV_Target {
					return tex2Dbias(_MainTex, float4(i.uv, 0, -3)) * i.col;
				}
				ENDCG
			}
		}
	}
}