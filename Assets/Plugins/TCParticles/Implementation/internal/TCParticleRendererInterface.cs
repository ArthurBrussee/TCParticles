using TC;
using UnityEngine;
using UnityEngine.Rendering;

public interface TCParticleRenderer
{
	/// <summary>
	/// The material to render the particles with
	/// </summary>
	Material Material { get; set; }

	/// <summary>
	/// What mode to render the particles in
	/// </summary>
	GeometryRenderMode RenderMode { get; set; }

	/// <summary>
	/// The length of particles for streched billboards
	/// </summary>
	float LengthScale { get; set; }


	/// <summary>
	/// The factor of speed of a particle that stretches it for stretched billboards
	/// </summary>
	float SpeedScale { get; set; }

	/// <summary>
	/// The mesh that particles are rendered with
	/// </summary>
	Mesh Mesh { get; set; }


	/// <summary>
	/// Should the system use frustum culling?
	/// </summary>
	bool UseFrustumCulling { get; set; }

	/// <summary>
	/// The bounds of the system used for frustum culling
	/// </summary>
	Bounds Bounds { get; set; }

	/// <summary>
	/// Colour over lifetime gradient. Call UpdateColourOverLifetime when changed
	/// </summary>
	Gradient ColourOverLifetime { get; set; }


	/// <summary>
	/// Update the colour over lifetime texture. Should be called whenever changing the colour over lifetime gradient
	/// </summary>
	void UpdateColourOverLifetime();

}