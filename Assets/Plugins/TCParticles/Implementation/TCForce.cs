using System;
using System.Collections.Generic;
using TC;
using TC.Internal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

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
	public MinMax radius = MinMax.Constant(1);

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
	[Range(0, 1)] [SerializeField] float _attenuation = 1.0f;

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

	[SerializeField] bool globalShape = false;

	/// <summary>
	/// Is this force shape global for all attached forces?
	/// </summary>
	public bool IsGlobalShape {
		get { return globalShape; }
	}

	/// <summary>
	/// Is this force the primary force attached? (first one in the component order)
	/// </summary>
	public bool IsPrimaryForce {
		get { return GetComponent<TCForce>() == this; }
	}


	[Range(0, 1)] [SerializeField] float _inheritVelocity;

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

			return (transform.position - m_lastPos);
		}
	}

	//TODO: Real time force! Lift limitations! Hwuaah


	RenderTexture m_forceBaked;

	/// <summary>
	/// The force texture used in force type TurbulenceTexture
	/// </summary>
	public Texture3D forceTexture;



	/// <summary>
	/// The smoothness value 
	/// </summary>
	[Range(0.0f, 1.0f)] public float smoothness = 1.0f;


	/// <summary>
	/// The period in which the noise should be repeated
	/// </summary>
	public Vector3 noiseExtents = Vector3.one;



	//Noise properties - keep track of whether they have changed!

	/// <summary>
	/// The resolution of the force 
	/// </summary>
	[Range(0, 256)]
	public int resolution = 32;
	int m_prevResolution;

	/// <summary>
	/// The frequency of the selected noise type
	/// </summary>
	public float frequency = 1.0f;
	float m_prevFreq = -1.0f;

	/// <summary>
	/// The octave count for the selected noise type
	/// </summary>
	[Range(1.0f, 10.0f)] public int octaveCount = 4;
	int m_prevOct = -1;

	/// <summary>
	/// The lacunarity (chaos) for the selected noise type
	/// </summary>
	[Range(0.05f, 6.0f)] public float lacunarity = 2.17f;
	float m_prevLac = -1.0f;

	/// <summary>
	/// The persistence (strength of high frequency) for selected noise type
	/// </summary>
	[Range(0.0f, 1.0f)] public float persistence = 0.5f;
	float m_prevPersist = -1.0f;

	/// <summary>
	/// The seed of the noise
	/// </summary>
	public Vector3 noiseOffset;
	Vector3 m_prevNoiseOffset;

	/// <summary>
	/// The type of the noise
	/// </summary>
	public NoiseType noiseType;
	NoiseType m_prevType;

	
	bool Changed<T>(T cur, ref T prev) {
		if (!EqualityComparer<T>.Default.Equals(cur, prev)) {
			prev = cur;
			return true;
		}
		return false;
	}


	public Texture CurrentForceVolume {
		get {

			if (forceType == ForceType.Turbulence) {
				//Don't short circuit or! Make sure all prevs are set
				if (Changed(resolution, ref m_prevResolution) |
					Changed(frequency, ref m_prevFreq) |
					Changed(octaveCount, ref m_prevOct) |
					Changed(lacunarity, ref m_prevLac) |
					Changed(persistence, ref m_prevPersist) |
					Changed(noiseOffset, ref m_prevNoiseOffset) |
					Changed(noiseType, ref m_prevType) |
					(m_forceBaked == null)) {
					UpdateForceBake();
				}

				return m_forceBaked;
			}


			return forceTexture;
		}
	}


	Vector3 m_lastPos;
	Rigidbody m_rigidbody;

	static ComputeShader s_forceBaker;

	void LateUpdate() {
		m_lastPos = transform.position;
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
	}

	void UpdateForceBake() {
		Profiler.BeginSample("Bake force");
		if (m_forceBaked == null || m_forceBaked.width != resolution) {
			if (m_forceBaked != null) {
				DestroyImmediate(m_forceBaked);
			}

			m_forceBaked = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32);
			m_forceBaked.dimension = TextureDimension.Tex3D;
			m_forceBaked.volumeDepth = resolution;
			m_forceBaked.enableRandomWrite = true;
			m_forceBaked.Create();
		}

		if (s_forceBaker == null) {
			s_forceBaker = Resources.Load<ComputeShader>("BakeForce");
		}

		int kernel = s_forceBaker.FindKernel("BakeForce");
		s_forceBaker.SetFloat("_Frequency", frequency);
		s_forceBaker.SetVector("_NoiseOffset", noiseOffset);
		s_forceBaker.SetFloat("_Resolution", resolution);
		s_forceBaker.SetFloat("_Lacunarity", lacunarity);
		s_forceBaker.SetFloat("_Persistence", persistence);
		s_forceBaker.SetInt("_OctaveCount", octaveCount);
		s_forceBaker.SetInt("_NoiseType", (int)noiseType);
		s_forceBaker.SetTexture(kernel, "_ForceTexture", m_forceBaked);

		int count = Mathf.CeilToInt(resolution / 4.0f);
		s_forceBaker.Dispatch(kernel, count, count, count);
		Profiler.EndSample();
	}

	void OnEnable() {
		All.Add(this);

		var systs = TCParticleSystem.All;
		for (int i = 0; i < systs.Count; ++i) {
			systs[i].RegisterForce(this);
		}
		resolution = Mathf.Clamp(resolution, 32, 512);
	}

	void OnDisable() {
		var systs = TCParticleSystem.All;
		for (int i = 0; i < systs.Count; ++i) {
			systs[i].RemoveForce(this);
		}

		All.RemoveUnordered(this);
		DestroyImmediate(m_forceBaked);
		m_forceBaked = null;
	}

	void OnDrawGizmosSelected() {
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
	Color EncodeVector(Vector3 vec) {
		return new Color(vec.x * 0.5f + 0.5f,
		                 vec.y * 0.5f + 0.5f,
		                 vec.z * 0.5f + 0.5f);
	}

	bool IsPowerOfTwo(int x) {
		return (x != 0) && ((x & (x - 1)) == 0);
	}

	//TODO: Do this in a rendertexture too somehow?
	//Fill in UAV?
	public void GenerateTexture(Vector3[,,] values, bool newTexture = true) {
		int xres = values.GetLength(0);
		int yres = values.GetLength(1);
		int zres = values.GetLength(2);

		if (!IsPowerOfTwo(xres) || !IsPowerOfTwo(yres) || !IsPowerOfTwo(zres)) {
			Debug.Log("Can only generate a power of two sized turbulence texture!");
			return;
		}

		Color[]  colours = new Color[xres * yres * zres];

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