using System.Collections.Generic;
using System.Collections.ObjectModel;
using TC;
using TC.Internal;
using UnityEngine;

[AddComponentMenu("TC Particles/Collider")]
[ExecuteInEditMode]
public class TCCollider : MonoBehaviour {

	public static List<TCCollider> All = new List<TCCollider>();


	/// <summary>
	/// Sphere collider, if attached
	/// </summary>
	public SphereCollider SphereCollider { get; private set; }

	/// <summary>
	/// Box collider, if attached
	/// </summary>
	public BoxCollider BoxCollider { get; private set; }

	/// <summary>
	/// Capsule collider, if attached
	/// </summary>
	public CapsuleCollider CapsuleCollider { get; private set; }

	[SerializeField] [Range(0.0f, 1.0f)] private float _bounciness;

	/// <summary>
	/// Elasticity of the collision.
	/// </summary>
	public float Bounciness {
		get { return _bounciness; }
		set { _bounciness = value; }
	}

	[SerializeField] [Range(0.0f, 1.0f)] private float _stickiness;

	/// <summary>
	/// Stickiness of the collision
	/// </summary>
	public float Stickiness {
		get { return _stickiness; }
		set { _stickiness = value; }
	}

	[SerializeField] [Range(0.0f, 1.0f)] private float _inheritVelocity;

	/// <summary>
	/// How much of the velocity should be transfered to the particles
	/// </summary>
	public float InheritVelocity {
		get { return _inheritVelocity; }
		set { _inheritVelocity = value; }
	}

	/// <summary>
	/// The current velocity of this collider
	/// </summary>
	public Vector3 Velocity {
		get {
			if (r != null) {
				return r.velocity;
			}
			if (controller != null) {
				return controller.velocity;
			}
			return (transform.position - lastPos) / Time.deltaTime;
		}
	}


	[Range(0.0f, 1.0f)] [SerializeField] private float _particleLifeLoss;

	/// <summary>
	/// Amount in seconds that particles loses when colliding
	/// </summary>
	public float ParticleLifeLoss {
		get { return _particleLifeLoss; }
		set { _particleLifeLoss = value; }
	}

	/// <summary>
	/// The actual position of the attached collider
	/// </summary>
	public Vector3 Position {
		get {
			if (shape == ColliderShape.PhysxShape && !noCollider) {
				return GetComponent<Collider>().bounds.center;
			}
			return transform.position;
		}
	}

	/// <summary>
	/// The disc radius of the collider
	/// </summary>
	public MinMax radius;

	/// <summary>
	/// The height of the disc
	/// </summary>
	public float discHeight;

	/// <summary>
	/// 
	/// </summary>
	public DiscType discType;

	/// <summary>
	/// The rounding of a rounded box or of a disc 
	/// </summary>
	public float rounding;

	/// <summary>
	/// The current shape of this collider
	/// </summary>
	public ColliderShape shape;

	/// <summary>
	/// Should the collider be inverse?
	/// </summary>
	public bool inverse;

	/// <summary>
	/// The size of a rounded box
	/// </summary>
	public Vector3 boxSize;

	[SerializeField] private Texture2D heightmap;
	private bool hasGeneratedHeightmap;

	/// <summary>
	/// The heightmap for a terrain collider
	/// </summary>
	public Texture2D Heightmap {
		get {
			if (shape != ColliderShape.Terrain) {
				return null;
			}

			if (!hasGeneratedHeightmap) {
				GenHeightmap();
			}

			return heightmap;
		}
	}


	private float Scale {
		get { return TCHelper.TransScale(transform); }
	}

	private float Xzscale {
		get { return TCHelper.TransXzScale(transform); }
	}

	private Vector3 VectorAbs(Vector3 vec) {
		return new Vector3(Mathf.Abs(vec.x), Mathf.Abs(vec.y), Mathf.Abs(vec.z));
	}

	private bool noCollider;

	//Keep track if colliders / forces need to be re-sorted
	private Vector3 lastPos;
	private CharacterController controller;
	private Rigidbody r;

	private void GenHeightmap() {
		var t = GetComponent<Terrain>();

		if (t == null) {
			return;
		}

		int res = t.terrainData.heightmapResolution;

		heightmap = new Texture2D(res, res, TextureFormat.RGB24, false, true);

		//heightmap.
		var heightCols = new Color[res * res];
		float[,] heights = t.terrainData.GetHeights(0, 0, res, res);

		for (int x = 0; x < res; ++x) {
			for (int y = 0; y < res; ++y) {
				float h = heights[y, x];

				float hr = heights[Mathf.Min(res - 1, y + 1), x];
				float hf = heights[y, Mathf.Min(x + 1, res - 1)];

				heightCols[x + y * res] = new Color(h, hf, hr);
			}
		}

		heightmap.SetPixels(heightCols);
		heightmap.Apply();
		hasGeneratedHeightmap = true;
	}

	private void Awake(){
		SphereCollider = GetComponent<SphereCollider>();
		BoxCollider = GetComponent<BoxCollider>();
		CapsuleCollider = GetComponent<CapsuleCollider>();

		controller = GetComponent<CharacterController>();
		r = GetComponent<Rigidbody>();

		GenHeightmap();



		if (shape != ColliderShape.PhysxShape || SphereCollider != null || BoxCollider != null || CapsuleCollider != null) {
			return;
		}

		noCollider = true;
		Debug.Log("To use a physx shape, please attach a physx collider! (Sphere, capsule, or box collider. Mesh is not supported currently)");
	}

	private void OnSystemCreated(TCParticleSystem system) {
		system.RegisterCollider(this);
	}
	
	private void LateUpdate() {
		lastPos = transform.position;
	}

	private void OnEnable() {
		All.Add(this);

		var systs = TCParticleSystem.All;
		for (int i = 0; i < systs.Count; ++i) {
			systs[i].RegisterCollider(this);
		}
	}


	private void OnDisable() {
		var systs = TCParticleSystem.All;
		for (int i = 0; i < systs.Count; ++i) {
			systs[i].RemoveCollider(this);
		}

		All.RemoveUnordered(this);
	}
}