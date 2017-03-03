using System.Collections.Generic;
using TC;
using TC.Internal;
using UnityEngine;

public interface TCParticleEmitter
{
	/// <summary>
	/// The shape of the emitter - read only
	/// </summary>
	EmitShapes Shape { get; }

	/// <summary>
	/// Radius of the emitting sphere. Applies to EmitShapes.Sphere and EmitShaped.HemiSphere
	/// </summary>
	MinMax Radius { get; }

	/// <summary>
	/// Start speed of a particle, in the particle's direction
	/// </summary>
	MinMaxRandom Speed { get; }

	/// <summary>
	/// Time a particle is alive. Measured in seconds
	/// </summary>
	MinMaxRandom Energy { get; }

	/// <summary>
	/// Starting size of the particle
	/// </summary>
	MinMaxRandom Size { get; }

	/// <summary>
	/// Start rotation of a particle
	/// </summary>
	MinMaxRandom Rotation { get; }

	/// <summary>
	/// Angular velocity of a particle. Measured in degrees per second
	/// </summary>
	float AngularVelocity { get; set; }

	/// <summary>
	/// Cube size of the emitting cube. Applies to EmitShapes.Box
	/// </summary>
	Vector3 CubeSize { get; set; }

	/// <summary>
	/// Constant force on the particle system in world space
	/// </summary>
	Vector3Curve ConstantForce { get; set; }

	/// <summary>
	/// Number of particles emitted each second
	/// </summary>
	float EmissionRate { get; set; }

	/// <summary>
	/// Approximate number of particles in the system. Note: Not precise!
	/// </summary>
	int ParticleCount { get; }

	/// <summary>
	/// The mesh shape of the emitter
	/// </summary>
	Mesh EmitMesh { get; set; }

	/// <summary>
	/// Size over lifetime curve. Call UpdateSizeOverLifetime when changed. Clamped between [0-1]
	/// </summary>
	AnimationCurve SizeOverLifetime { get; set; }

	/// <summary>
	/// The first value of size over lifetime
	/// </summary>
	float StartSizeMultiplier { get; set; }

	/// <summary>
	/// Velocity over lifetime. Call UpdateVelocityOverLifetime when changed. 
	/// </summary>
	Vector3Curve VelocityOverLifetime { get; set; }

	/// <summary>
	/// How should the particle choose direction it should start in 
	/// </summary>
	StartDirectionType StartDirectionType { get; set; }

	/// <summary>
	/// starting direciton of particles for StartDirectionType.Vector
	/// </summary>
	Vector3 StartDirectionVector { get; set; }

	///<summay>
	/// Random angle of the starting direction (applies to StartDirectionType.Vector and StartDirectionType.Normal)
	/// </summay>
	float StartDirectionRandomAngle { get; set; }

	/// <summary>
	/// Should particles be emitted automatically each second?
	/// </summary>
	bool DoEmit { get; set; }

	/// <summary>
	/// The colour of the particles when they are emitted
	/// </summary>
	Color StartColor { get; }

	/// <summary>
	/// How much of the emitter's velocity should be inherited
	/// </summary>
	float InheritVelocity { get; set; }

	/// <summary>
	/// Angle of the cone shape. Read only
	/// </summary>
	float ConeAngle { get; set;  }

	/// <summary>
	/// Height of the cone shape. Read only
	/// </summary>
	float ConeHeight { get; set;  }

	/// <summary>
	/// Radius of the cone shape. Read only
	/// </summary>
	float ConeRadius { get; set;  }

	/// <summary>
	/// Outer radius of the ring shape. Read only
	/// </summary>
	float RingOuterRadius { get; set; }

	/// <summary>
	/// Radius of the ring shape. Read only
	/// </summary>
	float RingRadius { get; set;  }

	float LineLength { get; set; }

	/// <summary>
	/// Update size over lifetime, get's called automatically in most cases
	/// </summary>
	void UpdateSizeOverLifetime();

	/// <summary>
	/// Clear all particles
	/// </summary>
	void ClearParticles();

	/// <summary>
	/// Simulate the particle system for a give time
	/// </summary>
	void Simulate(float time);

	/// <summary>
	/// Emit a given amount of particles
	/// </summary>
	void Emit(int count);

	///<summary> 
	/// Emit a given amount of particles with some initial starting positions
	/// </summary>
	void Emit(List<Vector3> positions);

	///<summary> 
	/// Emit a given amount of particles with some initial starting positions
	/// </summary>
	void Emit(Vector3[] positions);

	///<summary> 
	/// Emit a given amount of particles with some initial settings
	/// </summary>
	void Emit(ParticleProto[] positions, bool useColor = true, bool useSize = true, bool useVelocity = true);
}