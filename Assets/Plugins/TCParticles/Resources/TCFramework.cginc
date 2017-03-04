uint _BufferOffset;
uint _MaxParticles;

struct Particle {
	float3 pos;
	float rotation;

	float3 velocity;
	float baseSize;

	float life;
	float mass;

	uint color;
};
RWStructuredBuffer<Particle> particles;
StructuredBuffer<Particle> particlesRead;

uint GetEmitId(uint id, uint offset) {
	return (id + offset) % _MaxParticles;
}

uint GetId(uint id) {
	return GetEmitId(id, _BufferOffset);
}

//global parameters
struct SystemParameters
{
	float3 constantForce;
	float angularVelocity;
	float damping;
	float maxSpeed;
};
StructuredBuffer<SystemParameters> systemParameters;

float4 _LifeMinMax;
float _DeltTime;

//===============================
//variables
//Groupsize is the number of threads in X direction in a thread group. Constraints:
//->Mutliple of 64 (nvidia warps are sized 64)
//->Must be smaller than 1024 (or 768 for DX10, if unity would support that...)
//->Memory is shared between groups
//->Driver must be able to handle sharing memory between the number of threads in the group
#define TCGroupSize 128






//-----------------------------------------------------------------------------
// R8G8B8A8_UNORM <-> FLOAT4
//-----------------------------------------------------------------------------
uint D3DX_FLOAT_to_UINT(float _V, float _Scale)
{
	return (uint)floor(_V * _Scale + 0.5f);
}

float4 UnpackColor(uint packedInput) {
	float4 unpackedOutput;
	unpackedOutput.x = (float)(packedInput & 0x000000ff) / 255;
	unpackedOutput.y = (float)(((packedInput >> 8) & 0x000000ff)) / 255;
	unpackedOutput.z = (float)(((packedInput >> 16) & 0x000000ff)) / 255;
	unpackedOutput.w = (float)(packedInput >> 24) / 255;

	return unpackedOutput;
}

uint PackColor(float4 unpackedInput) {
	uint packedOutput;

	packedOutput = ((D3DX_FLOAT_to_UINT(saturate(unpackedInput.x), 255)) |
		(D3DX_FLOAT_to_UINT(saturate(unpackedInput.y), 255) << 8) |
		(D3DX_FLOAT_to_UINT(saturate(unpackedInput.z), 255) << 16) |
		(D3DX_FLOAT_to_UINT(saturate(unpackedInput.w), 255) << 24));

	return packedOutput;
}
