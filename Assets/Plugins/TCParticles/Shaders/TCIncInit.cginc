#include "UnityCG.cginc"
#pragma target 5.0

StructuredBuffer<Particle> particles : register(cs_5_0, u[0]);

sampler2D _ColTex;
sampler2D _LifetimeTex;
float4 emitterPos;
float3 TCMainLightDir;

float4 _PixelMult;

fixed4 _Glow;

#ifdef TC_MESH
	StructuredBuffer<float2> uvs;
	StructuredBuffer<float3> bufNormals;
#endif

StructuredBuffer<float3> bufPoints;


#ifdef TC_BILLBOARD_STRETCHED
	float speedScale;
	float lengthScale;

	StructuredBuffer<float> stretchBuffer;
	float _BillboardFlip = 1.0f;
#endif


float _MaxSpeed;
float _TcColourSpeed;


float bufferOffset;
float maxParticles;


#ifdef TC_UV_SPRITE_ANIM
	float4 _SpriteAnimSize;

	float4 _SpriteAnimUv;

	float _Cycles;
#endif




float4x4 TC_MATRIX_P;
float4x4 TC_MATRIX_M;
float4x4 TC_MATRIX_V;


float4x4 TC_MATRIX_MVP;