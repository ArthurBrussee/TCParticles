using UnityEngine;

namespace TC.Internal {
	public static class SID {
		public static int _UseEmitList = Shader.PropertyToID("_UseEmitList");
		public static int _UseVelSizeColorPos = Shader.PropertyToID("_UseVelSizeColorPos");

		public static int _EmitterMatrix = Shader.PropertyToID("_EmitterMatrix");
		public static int _EmitterRotationMatrix = Shader.PropertyToID("_EmitterRotationMatrix");
		public static int _EmitterStartRotationMatrix = Shader.PropertyToID("_EmitterStartRotationMatrix");

		public static int _Forces = Shader.PropertyToID("_Forces");
		public static int _TurbulenceForces = Shader.PropertyToID("_TurbulenceForces");

		public static int _TurbulenceRotation = Shader.PropertyToID("_TurbulenceRotation");
		public static int _TurbulenceRotationInv = Shader.PropertyToID("_TurbulenceRotationInv");
		public static int _TurbulenceTexture = Shader.PropertyToID("_TurbulenceTexture");

		public static int _TerrainTexture = Shader.PropertyToID("_TerrainTexture");
		public static int _Colliders = Shader.PropertyToID("_Colliders");

		//Global 
		public static int _MaxParticles = Shader.PropertyToID("_MaxParticles");
		public static int _BufferOffset = Shader.PropertyToID("_BufferOffset");
		public static int _DeltTime = Shader.PropertyToID("_DeltTime");

		// Can't rename because extension kernels rely on it :(
		public static int _Particles = Shader.PropertyToID("particles");
		public static int _SystemParameters = Shader.PropertyToID("systemParameters");

		public static int _LifetimeTexture = Shader.PropertyToID("_LifetimeTexture");

		public static int _ColTex = Shader.PropertyToID("_ColTex");
		public static int TC_MATRIX_M = Shader.PropertyToID("TC_MATRIX_M");
		public static int TC_MATRIX_M_INV = Shader.PropertyToID("TC_MATRIX_M_INV");

		public static int _MaxSpeed = Shader.PropertyToID("_MaxSpeed");
		public static int _ColorSpeedLerp = Shader.PropertyToID("_ColorSpeedLerp");

		public static int _Glow = Shader.PropertyToID("_Glow");
		public static int _PixelMult = Shader.PropertyToID("_PixelMult");
		public static int _SpeedScale = Shader.PropertyToID("_SpeedScale");
		public static int _LengthScale = Shader.PropertyToID("_LengthScale");
		public static int _ParticleThickness = Shader.PropertyToID("_ParticleThickness");

		public static int _ParticleEmitCount = Shader.PropertyToID("_ParticleEmitCount");
		public static int _LifeMinMax = Shader.PropertyToID("_LifeMinMax");
		public static int _Emitter = Shader.PropertyToID("_Emitter");

		public static int _SpriteAnimSize = Shader.PropertyToID("_SpriteAnimSize");
		public static int _Cycles = Shader.PropertyToID("_Cycles");

		public static int _SpriteSheetBaseSpeed = Shader.PropertyToID("_SpriteSheetBaseSpeed");
		public static int _SpriteSheetRandomStart = Shader.PropertyToID("_SpriteSheetRandomStart");
	}
}