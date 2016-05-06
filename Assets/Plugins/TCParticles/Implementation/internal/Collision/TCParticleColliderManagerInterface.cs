using System.Collections.Generic;
using UnityEngine;

public interface TCParticleColliderManager
{
	/// <summary>
	/// Maxinum number of colliders particles react to
	/// </summary>
	int MaxColliders { get; set; }

	/// <summary>
	/// Current number of colliders particles collide with
	/// </summary>
	int NumColliders { get; }

	/// <summary>
	/// Elasticity of the collision
	/// </summary>
	float Bounciness { get; set; }

	/// <summary>
	/// Should this system override the colliders bounciness?
	/// </summary>
	bool OverrideBounciness { get; set; }

	/// <summary>
	/// Stickiness of a collision if override stickiness is true
	/// </summary>
	float Stickiness { get; set; }

	/// <summary>
	/// Should this system override the colliders stickiness?
	/// </summary>
	bool OverrideStickiness { get; set; }

	/// <summary>
	/// Global multiplier for particle size when calculating collisions
	/// </summary>
	float ParticleThickness { get; set; }

	/// <summary>
	/// The layers in which the system looks for colliders
	/// </summary>
	LayerMask ColliderLayers { get; set; }

	/// <summary>
	/// A list of colliders to link irregardles of distance or layer
	/// </summary>
	List<TCCollider> BaseColliders { get; }
}