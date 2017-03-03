/*
--------------------------------------------------------------------
------------------------ Default Functions -------------------------
--------------------------------------------------------------------
*/
/*
This is default color calculation function. It's here to avoid code duplication.
When user's color hook specified, it's not evaluated and instead user's code
is included directly from TCIncColorHook.cginc file
*/
#ifndef TC_HOOK_COLOR
	float4 particle_color(float life, float3 velocity, float4 partColor) {
		float4 tp = float4(lerp(life, length(velocity) * _MaxSpeed, _TCColourSpeed), 0.0f, 0.0f, 0.0f);
		return tex2Dlod(_ColTex, tp).rgba * _Glow * partColor;
	}
#endif

/*
--------------------------------------------------------------------
-------------------------- Vertex Functions ------------------------
--------------------------------------------------------------------
*/
#ifdef TC_MESH

particle_fragment particle_vertex(uint id : SV_VertexID, uint inst : SV_InstanceID) {
	particle_fragment o;

	inst = GetId(inst);

	Particles part = particlesRead[inst];

	float life = 1 - part.life / _LifeMinMax.y;
	float4 lifeTex = tex2Dlod(_LifetimeTex, life);
	float3 vel = part.velocity + lifeTex.xyz;

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
	o.col = particle_color(life, vel, UnpackColor(part.color));
#else
	// user's color calculation algorythm
	#include "TCIncHookColor.cginc"
#endif

	float angle = part.rotation;

	float2 cs = float2(cos(angle), sin(angle));
	float s = sin(angle);

	float3 vert = bufPoints[id];

	float3 cp = float3(vert.x * c + vert.z * s, vert.y, vert.z * c - vert.x * s);

	o.uv = uvs[id];
	
	float size = part.baseSize * lifeTex.a;
	o.pos = mul(UNITY_MATRIX_VP, mul(TC_MATRIX_M, float4(part.pos.xyz + emitterPos + cp * size, 1.0f)));
	o.pos.w *= step(0, life);


#ifdef TC_HOOK_OUTPUT
	// the hook performed at the very end of vertex calculation
	#include "TCIncHookOutput.cginc"
#endif

	return o;
}

#else

particle_fragment particle_vertex(uint id : SV_VertexID, uint inst : SV_InstanceID)
{
	particle_fragment o;


	inst = GetId(inst);

	Particle part = particlesRead[inst];

	float life = 1 - part.life / _LifeMinMax.y;
	float4 lifeTex = tex2Dlod(_LifetimeTex, life);

	float3 vel = part.velocity + lifeTex.xyz;

#ifdef TC_USER_VERT_STRUCT
	tc_user_vert_data vertData; // variable for passing user's values between hooks
#endif

#ifdef TC_HOOK_INPUT
	// the hook performed at the very beginning
	// it allows to change some input data
#include "TCIncHookInput.cginc"
#endif


	float4 pos = mul(UNITY_MATRIX_VP, mul(TC_MATRIX_M, float4(part.pos.xyz, 1.0f)));
	float mult = _PixelMult.x;
	mult *= lerp(1.0f, pos.w, _PixelMult.y);

	float size = part.baseSize * lifeTex.a;


#ifdef TC_BILLBOARD
	float angle = part.rotation;
	float c = cos(angle);
	float s = sin(angle);

	float2 buf = bufPoints[id] * (size * mult);
	float2 rot = float2(buf.x * c - buf.y * s, buf.x * s + buf.y * c);

	o.pos = pos + mul(UNITY_MATRIX_P, rot);
#elif TC_BILLBOARD_STRETCHED

	half3 sv = mul(UNITY_MATRIX_V, float4(vel, 0.0f)).xyz + float3(0.0001f, 0.0f, 0.0f);
	half l = length(sv);

	half3 vRight = sv / l;
	half3 vUp = float3(-_BillboardFlip * vRight.y, _BillboardFlip * vRight.x, 0.0f);


	float stretchFac = (1.0f + (lengthScale + stretchBuffer[id] * l * speedScale));


	o.pos = pos + mul(UNITY_MATRIX_P, float4(bufPoints[id].x * vRight * stretchFac + bufPoints[id].y * vUp, 1.0f) *  size * mult);
#else
	o.pos = float4(0.0f, 0.0f, 0.0f, 0.0f);
#endif
	
	//o.pos.w *= step(life, 0);

	// calculate color
#ifndef TC_HOOK_COLOR
	// default algorithm
	o.col = particle_color(part.life, vel, UnpackColor(part.color));
#else
	// user's color calculation algorythm
#include "TCIncHookColor.cginc"
#endif

#ifdef TC_UV_SPRITE_ANIM
	half2 uv = bufPoints[id] + 0.5f;
	uint sprite = (uint)(part.life * (_SpriteAnimSize.x * _SpriteAnimSize.y) * _Cycles);
	uint x = sprite % (uint)_SpriteAnimSize.x;
	uint y = (uint)(sprite / _SpriteAnimSize.x);
	o.uv = float2(x *_SpriteAnimSize.z, 1.0f - _SpriteAnimSize.w - y * _SpriteAnimSize.w) + uv * _SpriteAnimSize.zw;
#else
	o.uv = bufPoints[id] + 0.5f;
#endif

#ifdef TC_OFFSCREEN
	o.projPos = ComputeScreenPos(o.pos);
	o.projPos.z = -mul(UNITY_MATRIX_V, mul(TC_MATRIX_M, float4(part.pos, 0.0f))).z;
#endif

#ifdef TC_HOOK_OUTPUT
	// the hook performed at the very end of vertex calculation
	#include "TCIncHookOutput.cginc"
#endif

	return o;
}
#endif
