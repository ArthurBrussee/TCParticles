using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace TC.Internal {
	[SuppressMessage("ReSharper", "InconsistentNaming")]
	public static class SID {
		public static readonly int _UseEmitList = Shader.PropertyToID("_UseEmitList");
		public static readonly int _UseVelSizeColorPos = Shader.PropertyToID("_UseVelSizeColorPos");

		public static readonly int _EmitterMatrix = Shader.PropertyToID("_EmitterMatrix");
		public static readonly int _EmitterRotationMatrix = Shader.PropertyToID("_EmitterRotationMatrix");
		public static readonly int _EmitterStartRotationMatrix = Shader.PropertyToID("_EmitterStartRotationMatrix");

		public static readonly int _Forces = Shader.PropertyToID("_Forces");
		public static readonly int _TurbulenceForces = Shader.PropertyToID("_TurbulenceForces");

		public static readonly int _TurbulenceRotation = Shader.PropertyToID("_TurbulenceRotation");
		public static readonly int _TurbulenceRotationInv = Shader.PropertyToID("_TurbulenceRotationInv");
		public static readonly int _TurbulenceTexture = Shader.PropertyToID("_TurbulenceTexture");

		public static readonly int _TerrainTexture = Shader.PropertyToID("_TerrainTexture");
		public static readonly int _Colliders = Shader.PropertyToID("_Colliders");

		//Global 
		public static readonly int _MaxParticles = Shader.PropertyToID("_MaxParticles");
		public static readonly int _BufferOffset = Shader.PropertyToID("_BufferOffset");
		public static readonly int _DeltTime = Shader.PropertyToID("_DeltTime");

		// Can't rename because extension kernels rely on it :(
		public static readonly int _Particles = Shader.PropertyToID("particles");
		public static readonly int _SystemParameters = Shader.PropertyToID("systemParameters");

		public static readonly int _LifetimeTexture = Shader.PropertyToID("_LifetimeTexture");

		public static readonly int _ColTex = Shader.PropertyToID("_ColTex");
		public static readonly int TC_MATRIX_M = Shader.PropertyToID("TC_MATRIX_M");
		public static readonly int TC_MATRIX_M_INV = Shader.PropertyToID("TC_MATRIX_M_INV");

		public static readonly int _MaxSpeed = Shader.PropertyToID("_MaxSpeed");
		public static readonly int _ColorSpeedLerp = Shader.PropertyToID("_ColorSpeedLerp");

		public static readonly int _Glow = Shader.PropertyToID("_Glow");
		public static readonly int _PixelMult = Shader.PropertyToID("_PixelMult");
		public static readonly int _SpeedScale = Shader.PropertyToID("_SpeedScale");
		public static readonly int _LengthScale = Shader.PropertyToID("_LengthScale");
		public static readonly int _ParticleThickness = Shader.PropertyToID("_ParticleThickness");

		public static readonly int _ParticleEmitCount = Shader.PropertyToID("_ParticleEmitCount");
		public static readonly int _LifeMinMax = Shader.PropertyToID("_LifeMinMax");
		public static readonly int _Emitter = Shader.PropertyToID("_Emitter");

		public static readonly int _SpriteAnimSize = Shader.PropertyToID("_SpriteAnimSize");
		public static readonly int _Cycles = Shader.PropertyToID("_Cycles");

		public static readonly int _SpriteSheetBaseSpeed = Shader.PropertyToID("_SpriteSheetBaseSpeed");
		public static readonly int _SpriteSheetRandomStart = Shader.PropertyToID("_SpriteSheetRandomStart");
	}
}