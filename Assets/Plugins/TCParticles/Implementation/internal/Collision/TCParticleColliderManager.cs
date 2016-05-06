using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TC.Internal {
	[Serializable]
	public class ParticleColliderManager : ParticleComponent, TCParticleColliderManager {
		///<summary>
		///The struct containing information for forces and colliders
		///</summary>
		private struct Collider {
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
		/// Maxinum number of colliders particles react to
		/// </summary>
		[SerializeField] private int _maxColliders = 1;

		/// <summary>
		/// The maximum number of colliders system responds to. Sorted based on distance
		/// </summary>
		public int MaxColliders {
			get { return _maxColliders; }
			set { _maxColliders = value; }
		}

		/// <summary>
		/// Current number of colliders particles collide with
		/// </summary>
		public int NumColliders {
			get { return Mathf.Min(m_collidersList.Count, _maxColliders); }
		}


		[SerializeField] [Range(0.0f, 1.0f)] private float _particleThickness = 0.25f;


		/// <summary>
		/// The 'thickness' of a particle used in collision calculations
		/// </summary>
		public float ParticleThickness {
			get { return _particleThickness; }
			set { _particleThickness = value; }
		}

		/// <summary>
		/// Elasticity of the collision
		/// </summary>
		[SerializeField] [Range(0.0f, 1.0f)] private float _bounciness = 0.3f;

		/// <summary>
		/// Bounciness (elasticity) of a collision if override bounciness is true
		/// </summary>
		public float Bounciness {
			get { return _bounciness; }
			set { _bounciness = Mathf.Clamp(value, 0.0f, 1.0f); }
		}


		[SerializeField] private bool overrideBounciness;

		public bool OverrideBounciness {
			get { return overrideBounciness; }
			set { overrideBounciness = value; }
		}


		[SerializeField] [Range(0.0f, 1.0f)] private float _stickiness = 0.3f;


		public float Stickiness {
			get { return _stickiness; }
			set { _stickiness = Mathf.Clamp(value, 0.0f, 1.0f); }
		}


		[SerializeField] private bool overrideStickiness;

		public bool OverrideStickiness {
			get { return overrideStickiness; }
			set { overrideStickiness = value; }
		}

		private Collider[] m_colliderStruct;
		private TCCollider[] m_colliderReference;
		private TCCollider m_terrainReference;

		private int m_colliderCount;
		private ComputeBuffer m_colliderBuffer;

		private List<TCCollider> m_collidersList;

		[SerializeField] private LayerMask _colliderLayers = -1;

		public LayerMask ColliderLayers {
			get { return _colliderLayers; }
			set { _colliderLayers = value; }
		}

		[SerializeField] private List<TCCollider> _baseColliders = new List<TCCollider>();


		public List<TCCollider> BaseColliders {
			get { return _baseColliders; }
		}

		public override void Initialize() {
			if (m_collidersList == null) {
				m_collidersList = new List<TCCollider>();
			}

			CreateBuffers();

		}

		private void CreateBuffers() {
			m_colliderStruct = new Collider[MaxColliders];
			m_colliderReference = new TCCollider[MaxColliders];

			if (_maxColliders != 0) {
				m_colliderBuffer = new ComputeBuffer(_maxColliders, ColliderStride);
			}
		}

		//Functions to register and remove colliders / forces
		public void RegisterCollider(TCCollider collider) {
			if (m_collidersList == null) {
				m_collidersList = new List<TCCollider>();
			}

			if (m_collidersList.Contains(collider)) {
				return;
			}

			int objLayerMask = (1 << collider.gameObject.layer);

			if ((ColliderLayers.value & objLayerMask) <= 0) {
				return;
			}

			m_collidersList.Add(collider);
		}

		public void RemoveCollider(TCCollider collider) {
			m_collidersList.Remove(collider);
		}

		public void Update() {
			if (_maxColliders == 0) {
				return;
			}

			if (m_colliderStruct.Length != MaxColliders) {
				CreateBuffers();
			}

			SortColliders();
		}

		private void DistributeColliders() {
			if (m_colliderReference == null) {
				return;
			}

			m_colliderCount = 0;


			for (int i = 0; i < m_collidersList.Count; i++) {
				TCCollider c = m_collidersList[i];

				if (m_colliderCount >= MaxColliders) {
					break;
				}

				int objLayerMask = (1 << c.gameObject.layer);

				if ((ColliderLayers.value & objLayerMask) <= 0) {
					continue;
				}

				m_colliderReference[m_colliderCount] = c;
				m_colliderCount++;
			}
		}


		protected override void Set() {
			DistributeColliders();

			if (MaxColliders == 0 || m_collidersList.Count == 0) {
				return;
			}

			Vector3 parentPosition = Vector3.zero;
			if (Transform.parent != null) {
				parentPosition = Transform.parent.position;
			}



			for (int c = 0; c < m_colliderCount; ++c) {
				TCCollider col = m_colliderReference[c];

				var rc = new Collider {Position = col.Position, Radius = 0.0f, BoxSize = Vector3.zero};

				float xzscale = TCHelper.TransXzScale(col.transform);
				float scale = TCHelper.TransScale(col.transform);
				float yscale = col.transform.localScale.y;
				Vector3 lscale = col.transform.localScale;

				switch (col.shape) {
					case ColliderShape.PhysxShape:

						if (col.SphereCollider != null) {
							rc.Radius = col.SphereCollider.radius * scale;
							rc.BoxSize = Vector3.zero;
							rc.Vtype = 0;
						}
						else if (col.CapsuleCollider != null) {
							rc.Radius = col.CapsuleCollider.radius * xzscale;
							rc.BoxSize = new Vector3(0.0f,
								Mathf.Max(0, col.CapsuleCollider.height / 2.0f * yscale - rc.Radius * 2.0f),
								0.0f);
							rc.Vtype = 1;
						}
						else if (col.BoxCollider != null) {
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
						}
						else {
							Vector3 terrainScale = terrain.terrainData.heightmapScale;
							rc.BoxSize = new Vector3(terrainScale.x * terrain.terrainData.heightmapResolution, terrainScale.y,
								terrainScale.z * terrain.terrainData.heightmapResolution);
						}


						rc.Vtype = 7;
						break;

					default:
						rc.Vtype = 0;
						break;
				}


				rc.AxisX = col.transform.right;
				rc.AxisY = col.transform.up;
				rc.AxisZ = col.transform.forward;

				const float eps = 0.01f;
				float elasticity = 1.0f + eps +
				                   Mathf.Lerp(0.0f, 1.0f - eps * 2.0f, overrideBounciness ? Bounciness : col.Bounciness);


				rc.Bounciness = elasticity;
				rc.Velocity = col.Velocity * col.InheritVelocity * 0.5f;
				rc.LifeLoss = col.ParticleLifeLoss * PartEmitter.Energy.Max * Manager.ParticleTimeDelta;

				rc.IsInverse = (uint) (col.inverse ? 1 : 0);


				float s = overrideStickiness ? Stickiness : col.Stickiness;
				float deltaSticky = Mathf.Clamp01(1.0f - Mathf.Pow(s, 0.05f / Manager.ParticleTimeDelta));
				rc.Stickiness = deltaSticky;

				switch (Manager.SimulationSpace) {
					case Space.Local:
						rc.Position = Transform.InverseTransformPoint(rc.Position);
						rc.AxisX = Transform.InverseTransformDirection(rc.AxisX);
						rc.AxisY = Transform.InverseTransformDirection(rc.AxisY);
						rc.AxisZ = Transform.InverseTransformDirection(rc.AxisZ);
						rc.Velocity = Transform.InverseTransformDirection(rc.Velocity);
						break;

					case Space.Parent:
						rc.Position -= parentPosition;
						break;
				}

				m_colliderStruct[c] = rc;
			}
		}


		public void Dispatch() {
			if (MaxColliders == 0 || m_colliderCount == 0 || m_colliderReference == null) {
				return;
			}


			m_colliderBuffer.SetData(m_colliderStruct);
			ComputeShader.SetBuffer(UpdateCollidersKernel, "colliders", m_colliderBuffer);


			bool found = false;
			for (int i = 0; i < m_colliderCount; ++i) {
				if (m_colliderReference[i].shape != ColliderShape.Terrain) {
					continue;
				}

				if (!found) {
					ComputeShader.SetTexture(UpdateCollidersKernel, "terrainTexture", m_colliderReference[i].Heightmap);
					found = true;
				}
				else {
					Debug.Log("Can't have multiple terrain colliders acting on one particle system!");
					break;
				}
			}

			Manager.SetPariclesToKernel(ComputeShader, UpdateCollidersKernel);
			ComputeShader.Dispatch(UpdateCollidersKernel, Manager.DispatchCount, m_colliderCount, 1);
		}

		private float GetColliderPoints(TCCollider collider) {
			if (BaseColliders != null && BaseColliders.Contains(collider)) {
				return float.MaxValue;
			}

			if (!collider.enabled) {
				return int.MinValue;
			}


			float points = 0.0f;
			if (collider.transform.parent == Transform) {
				points += 10000;
			}

			if (collider.shape == ColliderShape.Terrain) {
				points += 500000;
			}

			points -= (collider.transform.position - Transform.position).sqrMagnitude * 100;
			return points;
		}

		private Comparison<TCCollider> m_colliderSort;

		//Choose nearest colliders and forces
		private void SortColliders() {
			if (MaxColliders == 0) {
				return;
			}

			if (m_collidersList.Count > MaxColliders) {
				if (m_colliderSort == null) {
					m_colliderSort = (c1, c2) => GetColliderPoints(c2).CompareTo(c1);
				}

				m_collidersList.Sort(m_colliderSort);
			}
		}


		public override void OnDestroy() {
			Release(ref m_colliderBuffer);
		}
	}
}