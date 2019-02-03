Shader "Unlit/TurbulenceVisualize" {
	Properties {
		_Color ("Color", Color) = (1, 1, 1, 1)
	}

	SubShader {
		Tags { "RenderType"="Opaque" }
		
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata {
				float4 vertex : POSITION;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
			};

			float4 _Color;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}
			
			float4 frag (v2f i) : SV_Target {
				return _Color;
			}
			ENDCG
		}

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog

			#pragma target 4.5
			
			#include "UnityCG.cginc"

			struct appdata {
				float4 vertex : POSITION;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float4 color : COLOR0;
			};

			uint _Resolution;

			float4 _Color;
			float _Slice;

			StructuredBuffer<float3> vertices;

			float4x4 _ModelMatrix;
			uint _PreviewMode;
			
			sampler3D _ForceTexture;

			Texture3D<float4> _ForceTextureRW;

			SamplerState sampler_ForceTextureRW;


			v2f vert (uint vert : SV_VertexID, uint inst : SV_InstanceID) {
				v2f o;

				//Get grid pos
				uint x = inst % _Resolution;
				uint y = inst / _Resolution;

				float3 uv = float3((x + 0.5f) / _Resolution, _Slice, (y + 0.5f) / _Resolution);
				
				float3 force = tex3Dlod(_ForceTexture, float4(uv, 0)).rgb * 2.0f - 1.0f;
				float3 dir = normalize(force);

				//Chose normal basis
				float3 xx = float3(dir.z, 0.0f, -dir.x);

				if (length(xx) < 0.01f) {
					xx = float3(0.0f, dir.z, -dir.y);
				}
				xx = normalize(xx);
				float3 zz = cross(xx, dir);
				
				float4 mpos = float4(vertices[vert], 1.0f);
				mpos.xyz = (mpos.x * xx + mpos.y * dir + mpos.z * zz) * length(force) / (_Resolution * 4) + force * 0.03f;

				//Move to grid pos
				mpos.xyz += uv - 0.5f;
				mpos = mul(_ModelMatrix, mpos);

				o.pos = UnityObjectToClipPos(float4(mpos.xyz / mpos.w, 1.0f));
				
				float3 uvX = uv + float3(1.0f / _Resolution, 0.0f, 0.0f);
				float3 uvY = uv + float3(0.0f, 1.0f / _Resolution, 0.0f);
				float3 uvZ = uv + float3(0.0f, 0.0f, 1.0f / _Resolution);

				float3 fx = (tex3Dlod(_ForceTexture, float4(uvX, 0)).rgb * 2.0f - 1.0f);
				float3 fy = (tex3Dlod(_ForceTexture, float4(uvY, 0)).rgb * 2.0f - 1.0f);
				float3 fz = (tex3Dlod(_ForceTexture, float4(uvZ, 0)).rgb * 2.0f - 1.0f);

				float3 grad = float3(fx.x - force.x, fy.y - force.y, fz.z - force.z) * _Resolution;
				float gradient = length(grad);

				float col = 0;
				switch (_PreviewMode) {
					case 0: col = gradient * 0.08f; break;
					case 1: col = abs(force.x); break;
					case 2: col = abs(force.y); break;
					case 3: col = abs(force.z); break;
				}
				o.color = float4(col * 2.1f, 2.0f - col * 2.0f, 0.0f, 1.0f);
				return o;
			}
			

			float4 frag (v2f i) : SV_Target {
				return i.color;
			}
			ENDCG
		}
	}
}
