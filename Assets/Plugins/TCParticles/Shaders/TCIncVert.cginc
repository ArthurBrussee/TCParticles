/*
--------------------------------------------------------------------
------------------------ Default Functions -------------------------
--------------------------------------------------------------------
*/

uint GetId(uint id)
{
	return (uint)((id + bufferOffset) % maxParticles + 0.5f);
		// 0.5 added for correct float->uint rounding on AMD graphic cards
}



#ifndef TC_HOOK_COLOR
/*
This is default color calculation function. It's here to avoid code duplication.
When user's color hook specified, it's not evaluated and instead user's code
is included directly from TCIncColorHook.cginc file
*/
fixed4 particle_color(float life, float3 velocity) {
	float4 tp = float4(lerp(life, length(velocity) * _MaxSpeed, _TcColourSpeed), 0.0f, 0.0f, 0.0f);
	return tex2Dlod(_ColTex, tp).rgba * _Glow;
}
#endif



/*
--------------------------------------------------------------------
-------------------------- Vertex Functions ------------------------
--------------------------------------------------------------------
*/

#ifdef TC_MESH

	particle_fragment particle_vertex(uint id : SV_VertexID, uint inst : SV_InstanceID)
	{
		particle_fragment o;
		
		inst = GetId(inst);

		float life = particles[inst].life;
		float4 lifeTex = tex2Dlod(_LifetimeTex, life);
		float3 vel = particles[inst].velocity + lifeTex.xyz;

		//Particle particles[inst] = particles[inst];
			// store the current particle's data to writeble variable
			// to let the hooks modify it's values
		
		#ifdef TC_USER_VERT_STRUCT
			tc_user_vert_data vertData; // variable for passing user's values between hooks
		#endif

		#ifdef TC_HOOK_INPUT
			// the hook performed at the very beginning
			// it allows to change some input data
			#include "TCIncHookInput.cginc"
		#endif
		
		// calculate color
		#ifndef TC_HOOK_COLOR
			// default algorithm
			o.col = particle_color(particles[inst].life, vel);
		#else
			// user's color calculation algorythm
			#include "TCIncHookColor.cginc"
		#endif
		
		float angle =particles[inst].rotation;

		float c = cos(angle);
		float s = sin(angle);

		float3 vert = bufPoints[id];
		float3 cp = float3(vert.x * c + vert.z * s, vert.y, -vert.x * s + vert.z * c);



		o.uv = uvs[id];

				
		float size = particles[inst].baseSize * step(life, 1) * lifeTex.a;


		o.pos = mul(UNITY_MATRIX_VP, mul(TC_MATRIX_M, float4(particles[inst].pos + emitterPos + cp * size, 1.0f)));

		#ifdef TC_HOOK_OUTPUT
			// the hook performed at the very end of vertex calculation
			#include "TCIncHookOutput.cginc"
		#endif


		#ifdef TC_VERT_CUSTOM
			o = TCVertCustom(o, id, inst);
		#endif
		
		return o;
	}

#else

	particle_fragment particle_vertex (uint id : SV_VertexID, uint inst : SV_InstanceID)
	{
		particle_fragment o;

		
		inst = GetId(inst);
		
		float life = particles[inst].life;
		float4 lifeTex = tex2Dlod(_LifetimeTex, life);
		float3 vel = particles[inst].velocity + lifeTex.xyz;

		//Particle particles[inst] = particles[inst];
			// store the current particle's data to writeble variable
			// to let the hooks modify it's values
		
		#ifdef TC_USER_VERT_STRUCT
			tc_user_vert_data vertData; // variable for passing user's values between hooks
		#endif

		#ifdef TC_HOOK_INPUT
			// the hook performed at the very beginning
			// it allows to change some input data
			#include "TCIncHookInput.cginc"
		#endif
	

		float4 pos = mul(UNITY_MATRIX_VP, mul(TC_MATRIX_M, float4(particles[inst].pos, 1.0f)));
		float mult = _PixelMult.x;
		

		mult *= lerp(1.0f, pos.w, _PixelMult.y);

		

		float size = particles[inst].baseSize * step(life, 1) * lifeTex.a;


		
		#ifdef TC_BILLBOARD
			float angle = particles[inst].rotation;

			float c = cos(angle);
			float s = sin(angle);

			float2 buf = bufPoints[id] * size * mult;
			float2 rot = float2(buf.x * c - buf.y * s, buf.x * s + buf.y * c);
			

			o.pos = pos + mul(UNITY_MATRIX_P, rot);
		#elif TC_BILLBOARD_STRETCHED

			half3 sv = mul(UNITY_MATRIX_V, float4(vel, 0.0f)).xyz + float3(0.0001f, 0.0f, 0.0f);
			half l = length(sv);

			half3 vRight = sv / l;
			half3 vUp = float3(-_BillboardFlip * vRight.y, _BillboardFlip * vRight.x, 0.0f);


			float stretchFac = (1.0f + (lengthScale + stretchBuffer[id] * l * speedScale));
					

			o.pos = pos + mul(UNITY_MATRIX_P, float4(bufPoints[id].x * vRight * stretchFac + bufPoints[id].y * vUp, 1.0f) *  size * mult );
		#else
			o.pos = float4(0.0f, 0.0f, 0.0f, 0.0f);
		#endif

		// calculate color
		#ifndef TC_HOOK_COLOR
			// default algorithm
			o.col = particle_color(particles[inst].life, vel);
		#else
			// user's color calculation algorythm
			#include "TCIncHookColor.cginc"
		#endif

		#ifdef TC_UV_SPRITE_ANIM
			half2 uv = bufPoints[id] + 0.5f;
			uint sprite = (uint)(particles[inst].life * (_SpriteAnimSize.x * _SpriteAnimSize.y) * _Cycles);
			uint x = sprite % (uint)_SpriteAnimSize.x;
			uint y = (uint)(sprite / _SpriteAnimSize.x);

			o.uv = float2(x *_SpriteAnimSize.z, 1.0f -  _SpriteAnimSize.w - y * _SpriteAnimSize.w) + uv * _SpriteAnimSize.zw;
		#else
			o.uv = bufPoints[id] + 0.5f;
		#endif

		#ifdef TC_OFFSCREEN
			o.projPos = ComputeScreenPos(o.pos);
			o.projPos.z = -mul( UNITY_MATRIX_V, mul(TC_MATRIX_M, float4(particles[inst].pos, 0.0f))).z;
		#endif
		
		#ifdef TC_HOOK_OUTPUT
			// the hook performed at the very end of vertex calculation
			#include "TCIncHookOutput.cginc"
		#endif
		
		#ifdef TC_VERT_CUSTOM
			o = TCVertCustom(o, id, inst);
		#endif

		return o;
	}
#endif