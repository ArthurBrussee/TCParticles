using System.Collections.Generic;
using UnityEngine;

public interface TCParticleForceManager
{
	/// <summary>
	/// Maxinum number of forces particles collide with.
	/// </summary>
	int MaxForces { get; set; }


	/// <summary>
	/// Total number of forces particles react to
	/// </summary>
	int NumForcesTotal { get; }


	/// <summary>
	/// On what layers to look for forces
	/// </summary>
	LayerMask ForceLayers { get; set; }


	/// <summary>
	/// A list of forces to link irregardles of distance or layer
	/// </summary>
	List<TCForce> BaseForces { get; }
}