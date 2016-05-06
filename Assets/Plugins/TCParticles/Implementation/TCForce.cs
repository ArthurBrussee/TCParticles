using System.Collections.Generic;
using System.Collections.ObjectModel;
using TC;
using TC.Internal;
using UnityEngine;

[AddComponentMenu("TC Particles/Force")]
[ExecuteInEditMode]
public class TCForce : MonoBehaviour {


	public static List<TCForce> All = new List<TCForce>();


	///<summary>
	/// The shape of the force
	/// </summary>
	public ForceShape shape;

	/// <summary>
	/// The max radius of the force. 
	/// </summary>
	public MinMax radius;

	/// <summary>
	/// The height of the capsule for ForceShape.Capsule
	/// </summary>
	public float height = 2.0f;

	/// <summary>
	/// The cube size of the force for ForceShape.FanBox
	/// </summary>
	public Vector3 boxSize = Vector3.one;

	/// <summary>
	/// The type of force
	/// </summary>
	public ForceType forceType;

	/// <summary>
	/// What falloff function to use for the force
	/// </summary>
	public AttenuationType attenuationType;

	/// <summary>
	/// The strength of the force.
	/// </summary>
	public float power = 1.0f;

	/// <summary>
	/// amount this force fades from the center point to the radius ([0...1])
	/// </summary>
	[Range(0, 1)] [SerializeField] private float _attenuation = 1.0f;

	public float Attenuation {
		get { return _attenuation; }
		set { _attenuation = Mathf.Clamp(value, 0.0f, 1.0f); }
	}

	/// <summary>
	///  Direction of force for ForceType.Vector
	/// </summary>
	public Vector3 forceDirection = Vector3.up;

	public enum ForceSpace
	{
		World,
		Local
	};

	/// <summary>
	/// The space to use for the force direction (world / local)
	/// </summary>
	public ForceSpace forceDirectionSpace = ForceSpace.World;

	/// <summary>
	/// Axis vortex force rotates around
	/// </summary>
	public Vector3 vortexAxis = Vector3.up;


	/// <summary>
	/// Strenght of an extra inward force for the vortex force
	/// </summary>
	public float inwardForce;

	/// <summary>
	/// The height of the disc volume
	/// </summary>
	public float discHeight;



	/// <summary>
	/// The rounding of the disc volume
	/// </summary>
	public float discRounding;

	/// <summary>
	/// What type of disc is the volume
	/// </summary>
	public DiscType discType;

	[SerializeField] private bool globalShape = false;

	/// <summary>
	/// Is this force shape global for all attached forces?
	/// </summary>
	public bool IsGlobalShape {
		get { return globalShape; }
	}

	[SerializeField] private bool primary = false;

	/// <summary>
	/// Is this force the primary force attached? (first one in the component order)
	/// </summary>
	public bool IsPrimaryForce {
		get { return primary; }
	}


	[Range(0, 1)] [SerializeField] private float _inheritVelocity;

	/// <summary>
	/// How much of the force's velocity should be inherited
	/// </summary>
	public float InheritVelocity {
		get { return _inheritVelocity; }
		set { _inheritVelocity = Mathf.Clamp(value, 0.0f, 1.0f); }
	}

	/// <summary>
	/// The current velocity of this force
	/// </summary>
	public Vector3 Velocity {
		get {
			if (m_rigidbody != null) {
				return m_rigidbody.velocity;
			}

			return (transform.position - lastPos);
		}
	}

	/// <summary>
	/// The baked force texture
	/// </summary>
	public Texture3D forceTexture;

	public enum PowerOfTwo
	{
		Eight = 8,
		Sixteen = 16,
		ThirtyTwo = 32,
		SixtyFour = 64,
		OneHundredTwentyEight = 128
	}

	/// <summary>
	/// The resolution of the force 
	/// </summary>
	public PowerOfTwo resolution = PowerOfTwo.ThirtyTwo;

	/// <summary>
	/// The frequency of an overlay of chaotic turublence for the baked noise
	/// </summary>
	public float turbulenceFrequency = 3.0f;

	/// <summary>
	/// The power of an overlay of chaotic turbulence for the baked noises
	/// </summary>
	public float turbulencePower = 0.01f;


	/// <summary>
	/// An extra vector that is added to the turbulence force
	/// </summary>
	public Vector3 VectorBias;

	/// <summary>
	/// The smoothness value 
	/// </summary>
	[Range(0.0f, 1.0f)] public float smoothness = 0.5f;

	/// <summary>
	/// The frequency of the selected noise type
	/// </summary>
	public float frequency = 1.0f;

	/// <summary>
	/// The octave count for the selected noise type
	/// </summary>
	[Range(1.0f, 10.0f)] public int octaveCount = 4;

	/// <summary>
	/// The lacunarity (chaos) for the selected noise type
	/// </summary>
	[Range(0.05f, 6.0f)] public float lacunarity = 2.17f;

	/// <summary>
	/// The type of the noise
	/// </summary>
	public NoiseType noiseType;

	/// <summary>
	/// The seed of the noise
	/// </summary>
	public int seed;


	/// <summary>
	/// The period in which the noise should be repeated
	/// </summary>
	public Vector3 noiseExtents = Vector3.one;

	private Vector3 lastPos;
	private Rigidbody m_rigidbody;


	private void LateUpdate() {
		lastPos = transform.position;
	}

	void Awake() {
		TCForce[] forces = GetComponents<TCForce>();

		if (this != forces[0]) {
			if (forces[0].IsGlobalShape) {
				shape = forces[0].shape;
				radius = forces[0].radius;
				boxSize = forces[0].boxSize;
				height = forces[0].height;
				discHeight = forces[0].discHeight;
				discRounding = forces[0].discRounding;
				discType = forces[0].discType;
			}
		}



		m_rigidbody = GetComponent<Rigidbody>();

		if (forceType == ForceType.Turbulence && forceTexture == null) {
			Debug.Log("Turbulence has not been generated! Make sure you do so in the editor!");
		}
	}


	private void OnEnable() {
		All.Add(this);
		var systs = TCParticleSystem.All;

		for (int i = 0; i < systs.Count; ++i) {
			systs[i].RegisterForce(this);
		}
	}

	private void OnDisable() {
		var systs = TCParticleSystem.All;
		for (int i = 0; i < systs.Count; ++i) {
			systs[i].RemoveForce(this);
		}

		All.RemoveUnordered(this);
	}

	private void OnDrawGizmosSelected() {
		if (shape != ForceShape.Box && forceType != ForceType.Turbulence && forceType != ForceType.TurbulenceTexture) {
			return;
		}

		Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.localScale);
		Gizmos.matrix = rotationMatrix;


		if (shape == ForceShape.Box) {
			Gizmos.DrawWireCube(Vector3.zero, boxSize);
		}

		if (forceType == ForceType.Turbulence || forceType == ForceType.TurbulenceTexture) {
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireCube(Vector3.zero, noiseExtents);
			Gizmos.color = Color.white;
		}
	}

	//Noise force API
	private Color EncodeVector(Vector3 vec) {
		return new Color((vec.x * 0.5f + 0.5f),
		                 (vec.y * 0.5f + 0.5f),
		                 (vec.z * 0.5f + 0.5f));
	}

	private Color[] colours;


	private bool IsPowerOfTwo(int x) {
		return (x != 0) && ((x & (x - 1)) == 0);
	}

	public void GenerateTexture(Vector3[,,] values, bool newTexture = true) {
		int xres = values.GetLength(0);
		int yres = values.GetLength(1);
		int zres = values.GetLength(2);

		if (!IsPowerOfTwo(xres) || !IsPowerOfTwo(yres) || !IsPowerOfTwo(zres)) {
			Debug.Log("Can only generate a power of two sized turbulence texture!");
			return;
		}

		colours = new Color[xres * yres * zres];

		int id = 0;
		for (int z = 0; z < zres; ++z) {
			for (int y = 0; y < yres; ++y) {
				for (int x = 0; x < xres; ++x) {
					colours[id] = EncodeVector(values[x, y, z]);

					++id;
				}
			}
		}

		if (forceTexture == null || newTexture) {
			forceTexture = new Texture3D(xres, yres, zres, TextureFormat.RGB24, false)
			               {anisoLevel = 0, wrapMode = TextureWrapMode.Repeat};
		}

		forceTexture.SetPixels(colours);
		forceTexture.Apply();
	}
}