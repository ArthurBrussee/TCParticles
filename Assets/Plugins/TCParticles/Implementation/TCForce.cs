using TC.Internal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace TC {
	/// <summary>
	/// Component to create a force that influences particle systems
	/// </summary>
	[AddComponentMenu("TC Particles/Force"), ExecuteInEditMode]
	public class TCForce : MonoBehaviour, ITracked {
		int m_index = -1;

		/// <summary>
		/// Unique index in Tracker list
		/// </summary>
		public int Index {
			get => m_index;
			set => m_index = value;
		}

		///<summary>
		/// The shape of the force
		/// </summary>
		public ForceShape shape;

		/// <summary>
		/// The max radius of the force. 
		/// </summary>
		public MinMax radius = MinMax.Constant(1);

		/// <summary>
		/// The height of the capsule when <see cref="shape"/> is <see cref="ForceShape.Capsule"/>
		/// </summary>
		public float height = 2.0f;

		/// <summary>
		/// The cube size of the force when <see cref="shape"/> is <see cref="ForceShape.Box"/>
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

		[Range(0, 1), SerializeField]  float _attenuation = 1.0f;

		/// <summary>
		/// amount this force fades from the center point to the radius ([0...1])
		/// </summary>
		public float Attenuation {
			get => _attenuation;
			set => _attenuation = Mathf.Clamp01(value);
		}

		/// <summary>
		///  Direction of force for ForceType.Vector
		/// </summary>
		public Vector3 forceDirection = Vector3.up;

		/// <summary>
		/// The coordinate space the force is calculated in
		/// </summary>
		public enum ForceSpace {
			/// <summary>
			/// Worldspace coordinates
			/// </summary>
			World,

			/// <summary>
			/// Localspace coordinates
			/// </summary>
			Local
		}

		/// <summary>
		/// The space to use for the force direction (world / local)
		/// </summary>
		public ForceSpace forceDirectionSpace = ForceSpace.World;

		/// <summary>
		/// Axis vortex force rotates around when <see cref="forceType"/> is <see cref="ForceType.Vortex"/>
		/// </summary>
		public Vector3 vortexAxis = Vector3.up;

		/// <summary>
		/// Strenght of an extra inward force for when <see cref="forceType"/> is <see cref="ForceType.Vortex"/>
		/// </summary>
		public float inwardForce;

		/// <summary>
		/// The height of the disc volume when <see cref="shape"/> is <see cref="ForceShape.Disc"/>
		/// </summary>
		public float discHeight;

		/// <summary>
		/// The rounding of the disc volume when <see cref="shape"/> is <see cref="ForceShape.Disc"/>
		/// </summary>
		public float discRounding;

		/// <summary>
		/// What type of disc is the volume when <see cref="shape"/> is <see cref="ForceShape.Disc"/>
		/// </summary>
		public DiscType discType;

		[SerializeField] bool globalShape;

		/// <summary>
		/// Is this force shape global for all attached forces?
		/// </summary>
		public bool IsGlobalShape => globalShape;

		/// <summary>
		/// Is this force the primary force attached? (first one in the component order)
		/// </summary>
		public bool IsPrimaryForce => GetComponent<TCForce>() == this;

		[Range(0, 1), SerializeField]  float _inheritVelocity;

		/// <summary>
		/// How much of the force's velocity should be inherited
		/// </summary>
		public float InheritVelocity {
			get => _inheritVelocity;
			set => _inheritVelocity = Mathf.Clamp(value, 0.0f, 1.0f);
		}

		/// <summary>
		/// The current velocity of this force
		/// </summary>
		public Vector3 Velocity {
			get {
				if (m_rigidbody != null) {
					return m_rigidbody.velocity;
				}

				return transform.position - m_lastPos;
			}
		}

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
		[Range(0, 256)] public int resolution = 32;

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
		/// The type of the noise when <see cref="forceType"/> is <see cref="ForceType.Turbulence"/>
		/// </summary>
		public NoiseType noiseType;

		NoiseType m_prevType;

		bool Changed(float cur, ref float prev) {
			if (cur != prev) {
				prev = cur;
				return true;
			}

			return false;
		}

		bool Changed(int cur, ref int prev) {
			if (cur != prev) {
				prev = cur;
				return true;
			}

			return false;
		}

		bool Changed(Vector3 cur, ref Vector3 prev) {
			if (cur != prev) {
				prev = cur;
				return true;
			}

			return false;
		}

		bool Changed(NoiseType cur, ref NoiseType prev) {
			if (cur != prev) {
				prev = cur;
				return true;
			}

			return false;
		}

		/// <summary>
		/// Get an up to date texture holding the current vector field data. Can be a render texture
		/// </summary>
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

				m_forceBaked = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32,
					RenderTextureReadWrite.Linear);
				m_forceBaked.dimension = TextureDimension.Tex3D;
				m_forceBaked.volumeDepth = resolution;
				m_forceBaked.enableRandomWrite = true;
				m_forceBaked.Create();
			}

			if (s_forceBaker == null) {
				s_forceBaker = Resources.Load<ComputeShader>("Compute/BakeForce");
			}

			int kernel = s_forceBaker.FindKernel("BakeForce");
			s_forceBaker.SetFloat("_Frequency", frequency);
			s_forceBaker.SetVector("_NoiseOffset", noiseOffset);
			s_forceBaker.SetFloat("_Resolution", resolution);
			s_forceBaker.SetFloat("_Lacunarity", lacunarity);
			s_forceBaker.SetFloat("_Persistence", persistence);
			s_forceBaker.SetInt("_OctaveCount", octaveCount);
			s_forceBaker.SetInt("_NoiseType", (int) noiseType);
			s_forceBaker.SetTexture(kernel, "_ForceTexture", m_forceBaked);

			int count = Mathf.CeilToInt(resolution / 4.0f);
			s_forceBaker.Dispatch(kernel, count, count, count);
			Profiler.EndSample();
		}

		void OnEnable() {
			Tracker<TCForce>.Register(this);
			resolution = Mathf.Clamp(resolution, 32, 512);
		}

		void OnDisable() {
			Tracker<TCForce>.Deregister(this);
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
			return x != 0 && (x & (x - 1)) == 0;
		}

		/// <summary>
		/// Generate a new texture based on existing vector field data
		/// </summary>
		/// <param name="values">Vector field</param>
		/// <param name="newTexture">Should a new texture be created to hold this data</param>
		public void GenerateTexture(Vector3[,,] values, bool newTexture = true) {
			int xres = values.GetLength(0);
			int yres = values.GetLength(1);
			int zres = values.GetLength(2);

			if (!IsPowerOfTwo(xres) || !IsPowerOfTwo(yres) || !IsPowerOfTwo(zres)) {
				Debug.Log("Can only generate a power of two sized turbulence texture!");
				return;
			}

			Color[] colors = new Color[xres * yres * zres];

			int id = 0;
			for (int z = 0; z < zres; ++z) {
				for (int y = 0; y < yres; ++y) {
					for (int x = 0; x < xres; ++x) {
						colors[id] = EncodeVector(values[x, y, z]);
						++id;
					}
				}
			}

			if (forceTexture == null || newTexture) {
				forceTexture = new Texture3D(xres, yres, zres, TextureFormat.RGB24, false)
					{anisoLevel = 0, wrapMode = TextureWrapMode.Repeat};
			}

			forceTexture.SetPixels(colors);
			forceTexture.Apply();
		}
	}
}