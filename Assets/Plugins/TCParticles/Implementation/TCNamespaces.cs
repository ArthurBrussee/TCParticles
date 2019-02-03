using System;
using System.ComponentModel;
using UnityEngine;

namespace TC {
	/// <summary>
	/// Prototype data of particle to be emitted
	/// </summary>
	[Serializable]
	public struct ParticleProto {
		/// <summary>
		/// The starting position of the prototype
		/// </summary>
		public Vector3 Position;

		/// <summary>
		/// The start velocity of the prototype
		/// </summary>
		public Vector3 Velocity;

		/// <summary>
		/// The start size of the prototype
		/// </summary>
		public float Size;

		/// <summary>
		/// The start color of the prototype
		/// </summary>
		/// <remarks>
		/// Note while this has full float precision, particles only have 32 bit color precision
		/// </remarks>
		public Color Color;

		[EditorBrowsable(EditorBrowsableState.Never)]
		public const int Stride = 12 + 12 + 4 + 16;
	}

	/// <summary>
	/// The simulation space of the particle system
	/// </summary>
	public enum Space {
		/// <summary>
		/// World space simulation space
		/// </summary>
		World,
		/// <summary>
		/// Local space simulation space. Particles move and rotate with the system but don't scale
		/// </summary>
		Local,
		/// <summary>
		/// Parent simulation space. Particles move and rotate with the parent transform but don't scale with the parent transform
		/// </summary>
		Parent,
		/// <summary>
		/// Local space simulation space, with scale. Particles move, rotate and scale with the parent system
		/// </summary>
		LocalWithScale
	}

	public struct BurstEmission {
		public float Time;
		public int Amount;
	}

	/// <summary>
	/// Shape of the emitter
	/// </summary>
	public enum EmitShapes {
		Sphere,
		Box,
		HemiSphere,
		Cone,
		Ring,
		Line,
		Mesh,
		PointCloud
	}

	/// <summary>
	/// Disc shape of a force / collider shape
	/// </summary>
	public enum DiscType {
		Full,
		Half,
		Quarter
	}

	/// <summary>
	/// Attenuation faloff of force
	/// </summary>
	public enum AttenuationType {
		/// <summary>
		/// Linear faloff (1 - x)
		/// </summary>
		Linear,

		/// <summary>
		/// 1 / x faloff
		/// </summary>
		Divide,

		/// <summary>
		/// Smooth faloff (uses curve from smoothstep which approximately is x*x*(3 - 2*x) )
		/// </summary>
		EaseInOut
	}

	/// <summary>
	/// Render geometry mode for particles. 
	/// </summary>
	public enum GeometryRenderMode {
		/// <summary>
		/// Normal camera oriented billboard
		/// </summary>
		Billboard,
		/// <summary>
		/// Camera oriented billboard that is stretched in the direction the particle is moving
		/// </summary>
		StretchedBillboard,
		/// <summary>
		/// Camera oriented billobard, where the lower part is stretched in the direction the particle is moving
		/// </summary>
		TailStretchBillboard,
		/// <summary>
		/// Mesh for each particle. Rotates around z-axis with particle rotation
		/// </summary>
		Mesh,
		/// <summary>
		/// Renders 1 pixel points for each particle
		/// </summary>
		Point
	}

	/// <summary>
	/// Type of force to be applied to particles in range
	/// </summary>
	public enum ForceType {
		/// <summary>
		/// Apply force to push/pull particles in range away/to center of force.
		/// </summary>
		Radial,


		/// <summary>
		/// Apply force in constant direction to particles in range.
		/// </summary>
		Vector,

		/// <summary>
		/// Apply a force slowing down particles to particles in range.
		/// </summary>
		Drag,

		/// <summary>
		/// Apply a force rotating particles in range around a particular axis
		/// </summary>
		Vortex,

		/// <summary>
		/// Apply a chaotic noise force to particles in range
		/// </summary>
		Turbulence,

		/// <summary>
		/// Apply a chaotic noise force to particles in range based on vector data in a texture
		/// </summary>
		TurbulenceTexture
	}

	/// <summary>
	/// Type of noise to be used when force is in <see cref="ForceType.Turbulence"/>
	/// </summary>
	public enum NoiseType {
		/// <summary>
		/// Simple fractal perlin noise
		/// </summary>
		Billow,

		/// <summary>
		/// Pure perlin noise
		/// </summary>
		Perlin,

		/// <summary>
		/// Complex fractal perlin noise with deep troches / trenches
		/// </summary>
		Ridged
	}

	/// <summary>
	/// Shape of collider to be used
	/// </summary>
	public enum ColliderShape {
		/// <summary>
		/// Use the shape of the PhysX component attached to this object. Oonly cube / sphere / capsule are supported
		/// </summary>
		PhysxShape,
		/// <summary>
		/// Colliser has a disc shape. Can have rounded edges and a hole in the middle
		/// </summary>
		Disc,

		/// <summary>
		/// Collider has a hemispherical shape. Can have rounded edges
		/// </summary>
		Hemisphere,

		/// <summary>
		/// Collider has a cubibcal shape. Can have rounded edges
		/// </summary>
		RoundedBox,

		/// <summary>
		/// Use heightmap attached to this object or custom heightmap from script
		/// </summary>
		Terrain
	}

	/// <summary>
	/// Shape of force to be used
	/// </summary>
	public enum ForceShape {
		/// <summary>
		/// Apply force in spherical region
		/// </summary>
		Sphere,

		/// <summary>
		/// Apply force in capsule region
		/// </summary>
		Capsule,

		/// <summary>
		/// Apply force in cubical region. Can have rounded edges
		/// </summary>
		Box,

		/// <summary>
		/// Apply forc in disc shaped region. Can have rounded edges
		/// </summary>
		Disc,

		/// <summary>
		/// Apply force in hemispherical region. Can have rounded edges
		/// </summary>
		Hemisphere,

		/// <summary>
		/// Force that is global to the world
		/// </summary>
		Constant
	}

	/// <summary>
	/// The way a particle picks it's initial direction
	/// </summary>
	public enum StartDirection {
		/// <summary>
		/// Fixed direction
		/// </summary>
		Vector,

		/// <summary>
		/// Normal direction of the current emission shape
		/// </summary>
		Normal,

		/// <summary>
		/// Spherical random direction
		/// </summary>
		Random
	}

	/// <summary>
	/// The way to simulate a particle system when it is culled
	/// </summary>
	public enum CulledSimulationMode {
		/// <summary>
		/// Simulate like normal when culled
		/// </summary>
		UpdateNormally,

		/// <summary>
		/// Simulate less frequently when culled
		/// </summary>
		SlowSimulation,

		/// <summary>
		/// Don't simulate at all when culled
		/// </summary>
		StopSimulation
	};

	public enum ParticleColourGradientMode {
		/// <summary>
		/// Change colour of particles over their lifetime
		/// </summary>
		OverLifetime,

		/// <summary>
		/// Change colour of particles based on their speed.
		/// </summary>
		Speed
	};
}