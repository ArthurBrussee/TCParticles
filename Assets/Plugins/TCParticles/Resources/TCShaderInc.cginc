/*
This file is for bacward-compatibility only
Normally, the following lines need to be added directly to the shader,
instear of:
#include "TCShaderInc.cginc"

This way, a user will be able to add some custom code between these parts.
*/

#include "TCFramework.cginc"
#include "UnityCG.cginc"

#pragma target 5.0

sampler2D _ColTex;
sampler2D _LifetimeTex;

float4 emitterPos;
float3 TCMainLightDir;

float4 _PixelMult;
float4 _Glow;

StructuredBuffer<float3> bufPoints;

#ifdef TC_MESH
StructuredBuffer<float2> uvs;
StructuredBuffer<float3> bufNormals;
#endif

#ifdef TC_BILLBOARD_STRETCHED
float speedScale;
float lengthScale;
StructuredBuffer<float> stretchBuffer;
float _BillboardFlip = 1.0f;
#endif

float _MaxSpeed;
float _TCColourSpeed;


#ifdef TC_UV_SPRITE_ANIM
float4 _SpriteAnimSize;
float4 _SpriteAnimUv;
float _Cycles;
#endif

float4x4 TC_MATRIX_P;
float4x4 TC_MATRIX_M;
float4x4 TC_MATRIX_V;

float4x4 TC_MATRIX_MVP;

/*
	To use custom data: 

	1. you need to define the respective compiler constant
	for this structure (like TC_USER_VERT_STRUCT above).
	2. you also need to do it BEFORE including this file.
	3. it doesn't matter where the structure itself is defined,
	just don't forget to do it :)

	So if you need to use some default structures in your custom one,
	you can write a code like this:
	1. define TC_USER_VERT_STRUCT
	2. include this file
	3. define your cusom user_vert_data
*/

#ifndef TC_V2F_STRUCT
#define TC_V2F_STRUCT
/*
The standard vertex-to-fragment passing datatype.
However, it contains an instance of user_v2f_data, which (if overriden)
allows to pass some custom user values from vertex to fragment shader.
*/
struct particle_fragment
{
	float4 pos : SV_POSITION;
	fixed4 col : COLOR0;

	float2 uv : TEXCOORD0;
	
#ifdef TC_OFFSCREEN
	float4 projPos : TEXCOORD1;
#endif

#ifdef TC_USER_V2F_STRUCT
	tc_user_v2f_data custom;
#endif
};
#endif


#include "TCIncVert.cginc"