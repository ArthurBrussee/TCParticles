Shader "TCParticles/Legacy/Alpha cutout" {
	Properties {
		_MainTex ("Texture", 2D) = "white" { }
		_Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
		_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	}

	Category {
		Tags { "Queue"="AlphaTest" "RenderType"="Opaque" }

		SubShader {
			Pass  {
				Cull Off

				CGPROGRAM				
				#pragma target 4.5
				#pragma vertex TCDefaultVert
				#pragma fragment frag

				#pragma multi_compile TC_BILLBOARD TC_BILLBOARD_STRETCHED TC_MESH

				#pragma multi_compile_instancing
				#pragma instancing_options procedural:TCDefaultProc

				#include "TCFramework.cginc"
				#include "TCDefaultVert.cginc"

				float _Cutoff;

				float4 frag (TCFragment i) : SV_Target {
					half4 col =  tex2Dbias(_MainTex, float4(i.uv, 0, -4)) * i.col;

					if (col.a < _Cutoff){
						discard;
					}

					return col;
				}

				ENDCG
			}
		}
	}
}