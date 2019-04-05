Shader "TCParticles/Legacy/Additive (soft)" {
	Properties {
		_MainTex ("Texture", 2D) = "white" { }
		_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	}

	Category  {
		Tags { "Queue"="Transparent" "RenderType"="Transparent"}

		SubShader {
			Pass {
				Blend OneMinusDstColor One
				ZWrite Off 

				CGPROGRAM
					#pragma target 4.5
					#pragma vertex TCDefaultVert
					#pragma fragment frag
					
					#pragma multi_compile_instancing
					#pragma instancing_options procedural:TCDefaultProc

					#pragma multi_compile TC_BILLBOARD TC_BILLBOARD_STRETCHED TC_MESH

					#include "TCFramework.cginc"
					#include "TCDefaultVert.cginc"

					float4 frag (TCFragment i) : SV_Target {
						float4 col = tex2Dbias(_MainTex, float4(i.uv, 0, -3));
						return float4(min(float3(1.0f, 1.0, 1.0f), col.rgb * i.col.rgb * i.col.a * col.a), 1.0f);
					}
				ENDCG
			}
		}
	}
}