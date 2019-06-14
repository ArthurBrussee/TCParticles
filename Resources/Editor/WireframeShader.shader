Shader "Hidden/TCWireframeShader"
{
	Properties {
	}

	SubShader
	{
		Tags { "RenderType"="Transparent" }

		Blend SrcAlpha OneMinusSrcAlpha

		CGINCLUDE
					
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				return float4(1, 1, 1, 0.2f);
			}
		ENDCG

		Pass
		{
			Cull Front


			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
		
		Pass
		{
			Cull Back

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
	}
}
