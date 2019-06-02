using System;
using System.Collections.Generic;
using System.ComponentModel;
using TC.Internal;
using UnityEngine;

namespace TC {
	/// <summary>
	/// Class containing settings and functions to manage colliders for a TC particle system
	/// </summary>
	[Serializable]
	public class ParticleColliderManager : ParticleComponent {
		///<summary>
		///The struct containing information for forces and colliders
		///</summary>
		struct Collider {
			public float Bounciness;
			public float LifeLoss;

			public Vector3 Position;
			public float Radius;
			public Vector3 BoxSize;

			public Vector3 AxisX;
			public Vector3 AxisY;
			public Vector3 AxisZ;

			public Vector3 Velocity;

			public uint Vtype;
			public uint IsInverse;

			public float Stickiness;
		}

		/// <summary>
		/// Maxinum number of colliders systems reacts to
		/// </summary>
		[SerializeField] int _maxColliders = 1;

		/// <summary>
		/// Maxinum number of colliders particles react to
		/// </summary>
		/// <remarks>
		/// If the number of colliders exceeds this amount, they are sorted by their distance size etc. to pick the most relevant ones.
		/// </remarks>
		public int MaxColliders {
			get => _maxColliders;
			set => _maxColliders = value;
		}

		/// <summary>
		/// Current number of colliders particles collide with in this system
		/// </summary>
		public int NumColliders => m_collidersList == null ? 0 : Mathf.Min(m_collidersList.Count, _maxColliders);

		[SerializeField, Range(0.0f, 1.0f)]  float _particleThickness = 0.25f;

		/// <summary>
		/// Global multiplier for particle size when calculating collisions. 1 means use the full quad size
		/// </summary>
		public float ParticleThickness {
			get => _particleThickness;
			set => _particleThickness = value;
		}

		/// <summary>
		/// Elasticity of the collision
		/// </summary>
		[SerializeField, Range(0.0f, 1.0f)]  float _bounciness = 0.3f;

		/// <summary>
		/// Elasticity of the collision (0 means particles, 1 is full bounce)
		/// </summary>
		public float Bounciness {
			get => _bounciness;
			set => _bounciness = Mathf.Clamp(value, 0.0f, 1.0f);
		}

		[SerializeField] bool overrideBounciness;

		/// <summary>
		/// Should this system override the colliders bounciness?
		/// </summary>
		public bool OverrideBounciness {
			get => overrideBounciness;
			set => overrideBounciness = value;
		}

		[SerializeField, Range(0.0f, 1.0f)]  float _stickiness = 0.3f;

		/// <summary>
		/// Stickiness of a collision when <see cref="OverrideStickiness"/> is true
		/// </summary>
		public float Stickiness {
			get => _stickiness;
			set => _stickiness = Mathf.Clamp(value, 0.0f, 1.0f);
		}

		[SerializeField] bool overrideStickiness;

		/// <summary>
		/// Should this system override the colliders stickiness?
		/// </summary>
		public bool OverrideStickiness {
			get => overrideStickiness;
			set => overrideStickiness = value;
		}

		Collider[] m_colliderStruct;
		TCCollider[] m_colliderReference;
		ComputeBuffer m_colliderBuffer;
		List<TCCollider> m_collidersList;

		[SerializeField] LayerMask _colliderLayers = -1;

		/// <summary>
		/// The layers in which the system looks for colliders
		/// </summary>
		public LayerMask ColliderLayers {
			get => _colliderLayers;
			set => _colliderLayers = value;
		}

		[SerializeField] List<TCCollider> _baseColliders = new List<TCCollider>();

		/// <summary>
		/// A list of colliders to link irregardles of distance or layer. Does count towards MaxColliders
		/// </summary>
		public List<TCCollider> BaseColliders => _baseColliders;

		Comparison<TCCollider> m_colliderSort;

		[EditorBrowsable(EditorBrowsableState.Never)]
		protected override void Initialize() {
			CreateBuffers();
		}

		void CreateBuffers() {
			m_colliderStruct = new Collider[MaxColliders];
			m_colliderReference = new TCCollider[MaxColliders];

			if (_maxColliders != 0) {
				m_colliderBuffer = new ComputeBuffer(_maxColliders, ColliderStride);
			}
		}

		internal void Update() {
			if (_maxColliders == 0) {
				return;
			}

			if (m_colliderStruct.Length != MaxColliders) {
				CreateBuffers();
			}
		}

		void DistributeColliders() {
			if (m_colliderReference == null || MaxColliders == 0) {
				return;
			}

			if (m_collidersList == null) {
				m_collidersList = new List<TCCollider>();
			} else {
				m_collidersList.Clear();
			}

			for (int i = 0; i < Tracker<TCCollider>.Count; i++) {
				TCCollider c = Tracker<TCCollider>.All[i];

				if (ColliderLayers == (ColliderLayers | (1 << c.gameObject.layer))) {
					m_collidersList.Add(c);
				}
			}

			if (m_collidersList.Count > MaxColliders) {
				if (m_colliderSort == null) {
					m_colliderSort = (c1, c2) => GetColliderPoints(c2).CompareTo(GetColliderPoints(c1));
				}

				m_collidersList.Sort(m_colliderSort);
			}

			for (int i = 0; i < m_collidersList.Count; i++) {
				TCCollider c = m_collidersList[i];

				if (i >= MaxColliders) {
					break;
				}

				int objLayerMask = 1 << c.gameObject.layer;

				if ((ColliderLayers.value & objLayerMask) <= 0) {
					continue;
				}

				m_colliderReference[i] = c;
			}
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		protected override void Bind() {
			DistributeColliders();

			if (MaxColliders == 0 || m_collidersList.Count == 0) {
				return;
			}

			Vector3 parentPosition = Vector3.zero;
			Transform systTransform = SystemComp.transform;

			if (systTransform.parent != null) {
				parentPosition = systTransform.parent.position;
			}

			for (int c = 0; c < NumColliders; ++c) {
				TCCollider col = m_colliderReference[c];

				var rc = new Collider {Position = col.Position, Radius = 0.0f, BoxSize = Vector3.zero};

				Transform colTransform = col.transform;

				var lscale = colTransform.localScale;

				float xzscale = Mathf.Max(lscale.x, lscale.z);
				float scale = Mathf.Max(Mathf.Max(lscale.x, lscale.y), lscale.z);
				float yscale = lscale.y;

				switch (col.shape) {
					case ColliderShape.PhysxShape:

						if (col.SphereCollider != null) {
							rc.Radius = col.SphereCollider.radius * scale;
							rc.BoxSize = Vector3.zero;
							rc.Vtype = 0;
						} else if (col.CapsuleCollider != null) {
							rc.Radius = col.CapsuleCollider.radius * xzscale;
							rc.BoxSize = new Vector3(0.0f,
								Mathf.Max(0, col.CapsuleCollider.height / 2.0f * yscale - rc.Radius * 2.0f),
								0.0f);
							rc.Vtype = 1;
						} else if (col.BoxCollider != null) {
							rc.Radius = 0.5f;
							rc.BoxSize = Vector3.Scale(col.BoxCollider.size * 0.5f, lscale);

							rc.BoxSize = new Vector3(Mathf.Max(0, rc.BoxSize.x - rc.Radius),
								Mathf.Max(0, rc.BoxSize.y - rc.Radius),
								Mathf.Max(0, rc.BoxSize.z - rc.Radius));
							rc.Vtype = 2;
						}

						break;

					case ColliderShape.RoundedBox:
						rc.Radius = Mathf.Max(0.1f, col.rounding);
						rc.BoxSize = Vector3.Scale(col.boxSize * 0.5f, lscale);
						rc.BoxSize = new Vector3(Mathf.Max(0, rc.BoxSize.x - rc.Radius),
							Mathf.Max(0, rc.BoxSize.y - rc.Radius),
							Mathf.Max(0, rc.BoxSize.z - rc.Radius));
						rc.Vtype = 2;
						break;

					case ColliderShape.Hemisphere:
						rc.Radius = 0.05f;
						rc.BoxSize = new Vector3(col.radius.Max * scale, col.radius.Max * scale, col.radius.Max * scale);
						rc.Vtype = 3;
						break;

					case ColliderShape.Disc:
						float rMin = col.radius.IsConstant ? 0.0f : col.radius.Min * xzscale;
						float rMax = col.radius.Max * xzscale;

						rc.Radius = Mathf.Max(0.1f, col.rounding);
						rc.BoxSize = new Vector3(rMin, col.discHeight / 2.0f * yscale - col.rounding, rMax);
						switch (col.discType) {
							case DiscType.Full:
								rc.Vtype = 4;
								break;
							case DiscType.Half:
								rc.Vtype = 5;
								break;
							case DiscType.Quarter:
								rc.Vtype = 6;
								break;
						}

						break;

					case ColliderShape.Terrain:
						var terrain = col.GetComponent<Terrain>();

						if (terrain == null) {
							rc.BoxSize = Vector3.zero;
						} else {
							var terrainData = terrain.terrainData;
							Vector3 terrainScale = terrainData.heightmapScale;
							rc.BoxSize = new Vector3(terrainScale.x * terrainData.heightmapResolution, terrainScale.y,
								terrainScale.z * terrainData.heightmapResolution);
						}

						rc.Vtype = 7;
						break;

					default:
						rc.Vtype = 0;
						break;
				}

				rc.AxisX = colTransform.right;
				rc.AxisY = colTransform.up;
				rc.AxisZ = colTransform.forward;

				const float eps = 0.01f;
				float elasticity = 1.0f + eps + Mathf.Lerp(0.0f, 1.0f - eps * 2.0f, overrideBounciness ? Bounciness : col.Bounciness);

				rc.Bounciness = elasticity;
				rc.Velocity = col.InheritVelocity * 0.5f * col.Velocity;
				rc.LifeLoss = col.ParticleLifeLoss * Emitter.Lifetime.Max * Manager.ParticleTimeDelta;
				rc.IsInverse = (uint) (col.inverse ? 1 : 0);

				float s = overrideStickiness ? Stickiness : col.Stickiness;
				float deltaSticky = Mathf.Clamp01(1.0f - Mathf.Pow(s, 0.05f / Manager.ParticleTimeDelta));
				rc.Stickiness = deltaSticky;

				switch (Manager.SimulationSpace) {
					case Space.Local:
						rc.Position = systTransform.InverseTransformPoint(rc.Position);
						rc.AxisX = systTransform.InverseTransformDirection(rc.AxisX);
						rc.AxisY = systTransform.InverseTransformDirection(rc.AxisY);
						rc.AxisZ = systTransform.InverseTransformDirection(rc.AxisZ);
						rc.Velocity = systTransform.InverseTransformDirection(rc.Velocity);
						break;

					case Space.Parent:
						rc.Position -= parentPosition;
						break;
				}

				m_colliderStruct[c] = rc;
			}
		}

		internal void Dispatch() {
			if (NumColliders <= 0 || m_colliderReference == null) {
				return;
			}

			m_colliderBuffer.SetData(m_colliderStruct);
			ComputeShader.SetBuffer(UpdateCollidersKernel, SID._Colliders, m_colliderBuffer);
			ComputeShader.SetTexture(UpdateCollidersKernel, SID._TerrainTexture, Texture2D.whiteTexture);

			bool found = false;
			for (int i = 0; i < NumColliders; ++i) {
				if (m_colliderReference[i].shape != ColliderShape.Terrain) {
					continue;
				}

				if (!found) {
					ComputeShader.SetTexture(UpdateCollidersKernel, SID._TerrainTexture, m_colliderReference[i].Heightmap);
					found = true;
				} else {
					Debug.LogError("You currently Cannot have multiple terrain colliders act on a particle system!");
					break;
				}
			}

			Manager.BindPariclesToKernel(ComputeShader, UpdateCollidersKernel);

			if (Manager.DispatchCount > 0) {
				ComputeShader.Dispatch(UpdateCollidersKernel, Manager.DispatchCount, NumColliders, 1);
			}
		}

		float GetColliderPoints(TCCollider collider) {
			if (BaseColliders != null && BaseColliders.Contains(collider)) {
				return float.MaxValue;
			}

			if (!collider.enabled) {
				return float.MinValue;
			}

			float points = 0.0f;
			if (collider.transform.parent == SystemComp.transform) {
				points += 10000;
			}

			if (collider.shape == ColliderShape.Terrain) {
				points += 500000;
			}

			points -= (collider.transform.position - SystemComp.transform.position).sqrMagnitude * 100;
			return points;
		}

		internal virtual void OnDestroy() {
			Release(ref m_colliderBuffer);
		}
	}
}