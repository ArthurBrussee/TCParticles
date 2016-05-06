namespace TC
{
	public enum Space
	{
		World,
		Local,
		Parent,
		LocalWithScale
	}

	//Emit variables
	public enum EmitShapes
	{
		Sphere,
		Box,
		HemiSphere,
		Cone,
		Ring,
		Line,
		Mesh
	}

	public enum DiscType
	{
		Full,
		Half,
		Quarter
	}


	public enum AttenuationType
	{
		Linear,
		Divide,
		EaseInOut
	}

	public enum GeometryRenderMode
	{
		Billboard,
		StretchedBillboard,
		TailStretchBillboard,
		Mesh,
		Point
	}

	public enum ForceType
	{
		Radial,
		Vector,
		Drag,
		Vortex,
		Turbulence,
		TurbulenceTexture
	}

	public enum NoiseType
	{
		Billow,
		Pink,
		Ridged,
		Voronoi
	}


	public enum ColliderShape
	{
		PhysxShape,
		Disc,
		Hemisphere,
		RoundedBox,
		Terrain
	}

	public enum ForceShape
	{
		Sphere,
		Capsule,
		Box,
		Disc,
		Hemisphere,
		Constant
	}

	public enum StartDirectionType
	{
		Vector,
		Normal,
		Random
	}

	public enum CulledSimulationMode
	{
		DoNothing,
		SlowSimulation,
		StopSimulation
	};

	public enum ParticleColourGradientMode
	{
		OverLifetime,
		Speed
	};
}