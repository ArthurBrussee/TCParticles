using System;
using UnityEngine;

namespace TC {
	public delegate void OnParticleEvent(ComputeShader shader, int kern);

	[Serializable]
	public struct ParticleProto {
		public Vector3 Position;
		public Vector3 Velocity;

		public float Size;
		public Color Color;

		public ParticleProto(Vector3 pos) {
			Position = Vector3.zero;
			Velocity = Vector3.zero;
			Size = 1;
			Color = Color.white;
		}

		public const int Stride = 12 + 12 + 4 + 16;
	}

	public enum Space {
		World,
		Local,
		Parent,
		LocalWithScale
	}

	//Emit variables
	public enum EmitShapes {
		Sphere,
		Box,
		HemiSphere,
		Cone,
		Ring,
		Line,
		Mesh
	}

	public enum DiscType {
		Full,
		Half,
		Quarter
	}


	public enum AttenuationType {
		Linear,
		Divide,
		EaseInOut
	}

	public enum GeometryRenderMode {
		Billboard,
		StretchedBillboard,
		TailStretchBillboard,
		Mesh,
		Point
	}

	public enum ForceType {
		Radial,
		Vector,
		Drag,
		Vortex,
		Turbulence,
		TurbulenceTexture
	}

	public enum NoiseType {
		Billow,
		Perlin,
		Ridged
	}

	public enum ColliderShape {
		PhysxShape,
		Disc,
		Hemisphere,
		RoundedBox,
		Terrain
	}

	public enum ForceShape {
		Sphere,
		Capsule,
		Box,
		Disc,
		Hemisphere,
		Constant
	}

	public enum StartDirectionType {
		Vector,
		Normal,
		Random
	}

	public enum CulledSimulationMode {
		UpdateNormally,
		SlowSimulation,
		StopSimulation
	};

	public enum ParticleColourGradientMode {
		OverLifetime,
		Speed
	};
}