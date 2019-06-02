#if !defined(TC_FRAMEWORK_INC)
#define TC_FRAMEWORK_INC

#if defined(SHADER_API_D3D11) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PSSL) || defined(SHADER_API_XBOXONE) || SHADER_STAGE_COMPUTE
	#define TC_COMPUTES 1
#else
	#define TC_COMPUTES 0
#endif

#include "UnityCG.cginc"

uint _BufferOffset;
uint _MaxParticles;

uint _ParticleEmitCount;

struct Particle {
	float3 pos;
	float rotation;

	float3 velocity;
	float baseSize;

	float life;
	uint color;

	float random;
	float pad;
};


#if TC_COMPUTES
	RWStructuredBuffer<Particle> particles : register(u1);
	StructuredBuffer<Particle> particlesRead : register(t1);
#endif

//global parameters
struct SystemParameters {
	float3 constantForce;
	float angularVelocity;
	float damping;
	float maxSpeed;
};

#if TC_COMPUTES
	StructuredBuffer<SystemParameters> systemParameters : register(t2);
	StructuredBuffer<float3> _CustomNormalsBuffer : register(t3);
#endif

float4 _LifeMinMax;
float _DeltTime;
sampler2D _ColTex;

#if !SHADER_STAGE_COMPUTE	
	sampler2D _LifetimeTexture;
#else
	Texture2D _LifetimeTexture;
	SamplerState sampler_LifetimeTexture;
#endif
			
float4 _PixelMult;
float4 _Glow;

float _MaxSpeed;
float _ColorSpeedLerp;

float4x4 TC_MATRIX_M;
float4x4 TC_MATRIX_M_INV;

float _SpeedScale;
float _LengthScale;
float _BillboardFlip = 1.0f;

float4 _SpriteAnimSize;
float4 _SpriteAnimUv;
float _Cycles;
float _SpriteSheetBaseSpeed;
float _SpriteSheetRandomStart;
int _UvSpriteAnimation;

//===============================
//variables
//Groupsize is the number of threads in X direction in a thread group. Constraints:
//->Mutliple of 64 (nvidia warps are sized 64)
//->Must be smaller than 1024 (or 768 for DX10, if unity would support that...)
//->Memory is shared between groups
//->Driver must be able to handle sharing memory between the number of threads in the group
#define TCGroupSize 128

uint GetId(uint id) {
	return (id + _BufferOffset) % _MaxParticles;
}

//-----------------------------------------------------------------------------
// R8G8B8A8_UNORM <-> FLOAT4
//-----------------------------------------------------------------------------
uint D3DX_FLOAT_to_UINT(float _V, float _Scale) {
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
	
// Generates an orthonormal (row-major) basis from a unit vector
// The resulting rotation matrix has the determinant of +1.
// Ref: 'ortho_basis_pixar_r2' from http://marc-b-reynolds.github.io/quaternions/2016/07/06/Orthonormal.html
void OrthonormalBasis(float3 localZ, out float3 localX, out float3 localY) {
    float x  = localZ.x;
    float y  = localZ.y;
    float z  = localZ.z;
    float sz = (z >= 0) ? 1.0 : -1.0;
    
    float a  = 1 / (sz + z);
    float ya = y * a;
    float b  = x * ya;
    float c  = x * sz;

    localX = float3(c * x * a - 1, sz * b, c);
    localY = float3(b, y * ya - sz, y);
}

struct TCFragment {
	float4 pos : SV_POSITION;
	float4 col : COLOR;
	half2 uv : TEXCOORD0;
};

//Instancing proc func override system	
#if !SHADER_STAGE_COMPUTE
	//Initialize override system
	//TODO: Little brittle - need to keep in sync with unity updates
	#if TC_COMPUTES && defined(PROCEDURAL_INSTANCING_ON)
		//Give access to original clip pos
		inline float4 UnityObjectToClipPosRaw(float4 vertex) {
			return UnityObjectToClipPos(vertex);
		}

		#define TC_DO_PARTICLE(input)      Particle tc_Particle; \
		{ \
			unity_InstanceID = UNITY_GET_INSTANCE_ID(input); \
			tc_Particle = particlesRead[GetId(unity_InstanceID)]; \
			TCParticleProc procIn = (TCParticleProc)0; \
			procIn.vertex = input.vertex; \
			procIn.normal = input.normal; \
			procIn.texcoord = input.texcoord; \
			procIn.texcoord1 = input.texcoord1; \
			TCDefaultProc(procIn, tc_Particle); \
			input.vertex = procIn.vertex; \
			input.texcoord.xy = procIn.texcoord; \
			input.texcoord1.xy = procIn.texcoord1; \
			input.color = procIn.color; \
			input.normal = procIn.normal; \
		} \

		//Override for unity surface shader
		#undef UNITY_TRANSFER_INSTANCE_ID
		#define UNITY_TRANSFER_INSTANCE_ID(input, output) TC_DO_PARTICLE(input)

		//Redefine transform for normal vertex shaders
		#if defined(TC_BILLBOARD) || defined(TC_BILLBOARD_STRETCHED)
			float4 TcObjectToClipPos(float4 inVert, float3 partPos) {
				float4 screenPos = UnityObjectToClipPosRaw(float4(partPos.xyz, 1.0f));
				float mult = _PixelMult.x * lerp(1.0f, screenPos.w, _PixelMult.y);
				inVert.xyz *= mult;
				return screenPos + mul(UNITY_MATRIX_P, float4(inVert.xy, 0.0f, 0.0f));
			}

			#define UnityObjectToClipPos(inVert) TcObjectToClipPos(inVert, tc_Particle.pos.xyz)

			#if defined(SHADOWS_CUBE) && !defined(SHADOWS_CUBE_IN_DEPTH_TEX)
				#undef TRANSFER_SHADOW_CASTER_NOPOS
				#define TRANSFER_SHADOW_CASTER_NOPOS(o,opos) o.vec = mul(unity_ObjectToWorld, tc_Particle.pos.xyz).xyz - _LightPositionRange.xyz; opos = TcObjectToClipPos(v.vertex, tc_Particle.pos.xyz);
			#else
				#undef TRANSFER_SHADOW_CASTER_NOPOS
				#define TRANSFER_SHADOW_CASTER_NOPOS(o,opos) \
					opos = TcObjectToClipPos(v.vertex, tc_Particle.pos.xyz); \
					opos = UnityApplyLinearShadowBias(opos);
			#endif
		#endif
	#else
		#define TC_DO_PARTICLE(input)

		//Give access to original clip pos			
		inline float4 UnityObjectToClipPosRaw(float4 vertex) {
			return UnityObjectToClipPos(vertex);
		}
	#endif
#endif

//Dummy function necesarry to compile
void TCDefaultProc(){}

#if !SHADER_STAGE_COMPUTE && defined(PROCEDURAL_INSTANCING_ON) && TC_COMPUTES
	struct TCParticleProc {
		float4 vertex;
		float2 texcoord;
		float2 texcoord1;
		float4 color;
		float3 normal;
	};

	void TCDefaultProc(inout TCParticleProc input, in Particle tc_Particle) {
		unity_ObjectToWorld = TC_MATRIX_M;
		unity_WorldToObject = TC_MATRIX_M_INV;

		float life = 1 - tc_Particle.life / _LifeMinMax.y;
		float4 lifeTex = tex2Dlod(_LifetimeTexture, float4(life, 0, 0, 0));
		float3 realVelocity = tc_Particle.velocity + lifeTex.xyz;
		float velLength = length(realVelocity);

		float4 partColor = UnpackColor(tc_Particle.color);

		// tc_Particle.baseSize *= saturate(saturate(partColor.a - 0.2f) / 0.8f + 0.4f);

		float totalSize = (tc_Particle.baseSize * lifeTex.a);
		input.vertex.xyz *= tc_Particle.life > 0 ? totalSize : 0;
		
        #ifdef TC_BILLBOARD
            //TODO: Do we include this in the actual rotation matrix?
            float angle = tc_Particle.rotation;
            
            float c = cos(angle);
            float s = sin(angle);
            
            input.vertex.xy = float2(input.vertex.x * c -  input.vertex.y * s,  input.vertex.x * s +  input.vertex.y * c);
            
            #if defined(TC_CUSTOM_NORMAL_ORIENT)
                input.normal = _CustomNormalsBuffer[GetId(unity_InstanceID)];
            #else
                input.normal = mul(UNITY_MATRIX_I_V, mul(unity_WorldToObject, float4(0.0f, 0.0f, 1.0f, 0.0f)));
            #endif
        #elif TC_BILLBOARD_STRETCHED
            float3 screenSpaceVelocity = mul(UNITY_MATRIX_V, float4(realVelocity, 0.0f)).xyz + float3(0.0001f, 0.0f, 0.0f);
            float3 vRight = screenSpaceVelocity / velLength;
            float3 vUp = float3(-vRight.y, vRight.x, 0.0f);

            float2 vertStretch = input.vertex.xy;

            float stretchScaleFac = input.texcoord1.x;
            float stretchFac = (1.0f + stretchScaleFac * (_LengthScale + velLength * _SpeedScale));
            input.vertex.xyz = vertStretch.x * vRight * stretchFac + vertStretch.y * vUp;
        #elif TC_MESH
            #if defined(TC_CUSTOM_NORMAL_ORIENT)
                input.normal = _CustomNormalsBuffer[GetId(unity_InstanceID)];;

                float3 localZ = -input.normal;
                float3 localX, localY;
                OrthonormalBasis(localZ, localX, localY);
                
                input.vertex.xyz = input.vertex.x * localX + input.vertex.y * localY + input.vertex.z * localZ;
            #endif
            
            input.vertex.xyz += tc_Particle.pos;
            
			// TODO: This is wrong?
			// float3 camFwd = _WorldSpaceCameraPos - tc_Particle.pos;
            // input.normal.xyz *= dot(input.normal, camFwd) < 0 ? -1 : 1;
        #endif

			float4 tp = float4(lerp(life, velLength * _MaxSpeed, _ColorSpeedLerp), 0.0f, 0.0f, 0.0f);
			input.color = tex2Dlod(_ColTex, tp) * _Glow * partColor;


        if (_UvSpriteAnimation > 0) {
            float2 uv = input.texcoord.xy;
            float spriteSheetTime = _LifeMinMax.y > 0 ? (tc_Particle.life / _LifeMinMax.y) : 0.0f;
            spriteSheetTime += frac(_Time.y * _SpriteSheetBaseSpeed);
            
            //TODO: Make adding random controllable
            spriteSheetTime += tc_Particle.random * _SpriteSheetRandomStart;
            
            //Normalize to 0-1
            spriteSheetTime = frac(spriteSheetTime);
    
            //Figure out sprite number
            uint sprite = (uint)(spriteSheetTime * (_SpriteAnimSize.x * _SpriteAnimSize.y));
            uint x = sprite % (uint)_SpriteAnimSize.x;
            uint y = sprite / (uint)_SpriteAnimSize.x;
            input.texcoord.xy = float2(x *_SpriteAnimSize.z, 1.0f - _SpriteAnimSize.w - y * _SpriteAnimSize.w) + uv * _SpriteAnimSize.zw;
        }
	}
#endif

#endif