Shader "Hidden/TCConvertDepth" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "" {}
	}
	
	// Shader code pasted into all further CGPROGRAM blocks
	CGINCLUDE
		
	#include "UnityCG.cginc"
	#pragma target 5.0
	
	struct v2f {
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
	};

	sampler2D _MainTex;
	sampler2D _CameraDepthTexture;

	v2f vert( appdata_img v ) {
		v2f o;
		o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
		o.uv =  v.texcoord.xy;
		return o;
	}
	
	half4 frag(v2f i) : SV_Target
	{
		float d = UNITY_SAMPLE_DEPTH( tex2D(_CameraDepthTexture, i.uv.xy) );
		d = Linear01Depth(d);
		
		return EncodeFloatRGBA(min(d, 0.99999f)); 
	}

	ENDCG
	
	Subshader {
	
	 Pass {
		  ZTest Always Cull Off ZWrite Off
		  Fog { Mode off }      

		  CGPROGRAM
		  #pragma fragmentoption ARB_precision_hint_fastest
		  #pragma vertex vert
		  #pragma fragment frag
		  ENDCG
	  }
	}

	Fallback "Diffuse"
} // shader
