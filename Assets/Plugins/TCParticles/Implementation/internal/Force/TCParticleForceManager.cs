using System;
using System.Collections.Generic;
using System.ComponentModel;
using TC.Internal;
using UnityEngine;
using UnityEngine.Profiling;

namespace TC {
	/// <summary>
	/// Class containing settings and functions to manage colliders for a TC particle system
	/// </summary>
	[Serializable]
	public class ParticleForceManager : ParticleComponent {
		struct Force {
			public uint type;
			public uint attenType;
			public float force;
			public Vector3 axis;
			public float attenuation;
			public float minRadius;
			public float inwardForce;
			public float enclosingRadius;
			public Vector3 pos;
			public float radius;
			public Vector3 boxSize;
			public Vector3 axisX;
			public Vector3 axisY;
			public Vector3 axisZ;
			public Vector3 velocity;
			public uint vtype;
			public float turbulencePosFac;
		}

		enum ForceTypeKernel {
			Normal,
			Turbulence,
			Count
		}

		const int ForceTypeKernelCount = (int)ForceTypeKernel.Count;


		[SerializeField] int _maxForces = 1;

		/// <summary>
		/// Maxinum number of forces particles collide with.
		/// </summary>
		/// <remarks>
		/// If the number of forces this system could link too exceeds this, the chosen forces are sorted by distance and size
		/// </remarks>
		public int MaxForces {
			get { return _maxForces; }
			set { _maxForces = value; }
		}

		/// <summary>
		/// Maxinum number of forces particles react to
		/// </summary>
		public int NumForcesTotal {
			get { return m_forcesList == null ? 0 : Mathf.Min(m_forcesList.Count, _maxForces); }
		}

		[SerializeField] List<TCForce> _baseForces = new List<TCForce>();

		/// <summary>
		/// A list of forces to link irregardles of distance or layer
		/// </summary>
		public List<TCForce> BaseForces {
			get { return _baseForces; }
		}

		/// <summary>
		/// Enable/disable the use of boids flocking particles
		/// </summary>
		public bool useBoidsFlocking;

		/// <summary>
		/// The strength the boids pulls the particles towards the center position
		/// </summary>
		[Range(0.0f, 2.0f)]
		public float boidsPositionStrength = 0.5f;

		/// <summary>
		/// The strength the boids forces the particles to the average velocity
		/// </summary>
		[Range(0.0f, 5.0f)]
		public float boidsVelocityStrength = 1.0f;

		/// <summary>
		/// The strength the boids pulls the particles towards this particle system
		/// </summary>
		[Range(0.0f, 5.0f)]
		public float boidsCenterStrength = 1.0f;


		Comparison<TCForce> m_forceSort;
		ComputeBuffer[] m_forcesBuffer;
		TCParticlesBoidsFlock m_boidsFlock;

		Force[][] m_forcesStruct;
		TCForce[][] m_forcesReference;
		int[] m_forcesCount;

		List<TCForce> m_forcesList;

		[SerializeField] LayerMask _forceLayers = -1;

		/// <summary>
		/// On what layers to look for forces
		/// </summary>
		public LayerMask ForceLayers {
			get { return _forceLayers; }
			set { _forceLayers = value; }
		}

		void CreateBuffers() {
			if (MaxForces == 0) {
				return;
			}

			for (int t = 0; t < ForceTypeKernelCount; ++t) {
				if (m_forcesBuffer[t] != null) {
					m_forcesBuffer[t].Release();
				}

				m_forcesBuffer[t] = new ComputeBuffer(MaxForces, ForcesStride);
				m_forcesStruct[t] = new Force[MaxForces];
				m_forcesReference[t] = new TCForce[MaxForces];
			}
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		protected override void Initialize() {
			Profiler.BeginSample("Force buffers");
			m_forcesBuffer = new ComputeBuffer[ForceTypeKernelCount];
			m_forcesCount = new int[ForceTypeKernelCount];
			m_forcesStruct = new Force[ForceTypeKernelCount][];
			m_forcesReference = new TCForce[ForceTypeKernelCount][];
			Profiler.EndSample();

			CreateBuffers();

			if (useBoidsFlocking) {
				CreateBoids();
			}
		}

		internal void Update() {
			if (MaxForces == 0) {
				return;
			}

			if (m_forcesBuffer[0] != null && m_forcesBuffer[0].count != MaxForces || (m_forcesBuffer[1] != null && m_forcesBuffer[1].count != MaxForces)) {
				CreateBuffers();
			}
		}

		void CreateBoids() {
			m_boidsFlock = new TCParticlesBoidsFlock(Manager, this, ComputeShader);
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		protected override void Bind() {
			DistributeForces();

			if (MaxForces == 0 || NumForcesTotal == 0) {
				return;
			}

			Transform systTransform = SystemComp.transform;
			Vector3 parentPosition = Vector3.zero;

			if (systTransform.parent != null) {
				parentPosition = systTransform.parent.position;
			}

			for (int t = 0; t < ForceTypeKernelCount; ++t) {
				for (int f = 0; f < m_forcesCount[t]; ++f) {
					Force curForce = m_forcesStruct[t][f];
					TCForce force = m_forcesReference[t][f];

					Transform forceTransform = force.transform;
					Vector3 forcePos = forceTransform.position;

					curForce.type = (uint) force.forceType;
					
					curForce.axisX = forceTransform.right;
					curForce.axisY = forceTransform.up;
					curForce.axisZ = forceTransform.forward;

					curForce.minRadius = -1.0f;
					curForce.attenuation = force.Attenuation;

					switch (force.shape) {
						case ForceShape.Sphere:
							curForce.radius = force.radius.Max;

							if (force.radius.IsConstant) {
								curForce.minRadius = -1.0f;
							}
							else {
								curForce.minRadius = force.radius.Min * force.radius.Min;
							}

							curForce.enclosingRadius = force.radius.Max;
							curForce.boxSize = Vector3.zero;
							curForce.vtype = 0;
							break;

						case ForceShape.Capsule:
							float xzScale = Mathf.Max(force.transform.localScale.x, force.transform.localScale.z);

							float axisHeight = force.height / 2.0f * force.transform.localScale.y - force.radius.Max * xzScale;
							axisHeight = Mathf.Max(0, axisHeight);
							curForce.boxSize = new Vector3(0.0f, axisHeight * force.transform.localScale.y, 0.0f);
							curForce.radius = force.radius.Max;
							curForce.enclosingRadius = curForce.radius + force.height / 2.0f * force.transform.localScale.y;
							curForce.vtype = 1;
							break;

						case ForceShape.Box:
							curForce.radius = 0.1f;
							curForce.boxSize = force.boxSize * 0.5f - new Vector3(0.1f, 0.1f, 0.1f);
							curForce.boxSize = Vector3.Scale(curForce.boxSize, forceTransform.localScale);
							curForce.enclosingRadius = Mathf.Max(force.boxSize.x, force.boxSize.y, force.boxSize.z) * 0.5f;
							curForce.vtype = 2;
							curForce.attenuation = 0;
							break;
							
						case ForceShape.Hemisphere:
							curForce.radius = 0.1f;
							curForce.boxSize = Vector3.Scale(new Vector3(force.radius.Max, force.radius.Max, force.radius.Max),
							                                 force.transform.localScale);
							curForce.enclosingRadius = force.radius.Max;
							curForce.vtype = 3;
							break;
							
						case ForceShape.Disc:
							curForce.minRadius = -1.0f;
							curForce.vtype = (uint) force.discType;
							curForce.radius = Mathf.Max(0.1f, force.discRounding);

							float rMin = force.radius.IsConstant ? 0.0f : force.radius.Min;
							float rMax = force.radius.Max;

							curForce.boxSize = new Vector3(rMin, force.discHeight / 2.0f - force.discRounding, rMax);

							curForce.enclosingRadius = Mathf.Max(force.discHeight / 2.0f, force.radius.Max + force.discRounding);

							switch (force.discType) {
								case DiscType.Full:
									curForce.vtype = 4;
									break;
								case DiscType.Half:
									curForce.vtype = 5;
									break;
								case DiscType.Quarter:
									curForce.vtype = 6;
									break;
							}
							break;

						case ForceShape.Constant:
							curForce.radius = 0.1f;
							curForce.attenuation = 0.0f;
							curForce.enclosingRadius = -1.0f;
							curForce.vtype = 7;
							break;
					}

					curForce.force = force.power * Manager.ParticleTimeDelta;
					
					switch (force.forceType) {
						case ForceType.Vector:
							if (force.forceDirection != Vector3.zero) {
								if (force.forceDirectionSpace == TCForce.ForceSpace.World) {
									curForce.axis = Vector3.Normalize(force.forceDirection);
								}
								else {
									curForce.axis = force.transform.TransformDirection(Vector3.Normalize(force.forceDirection));
								}
							}
							break;

						case ForceType.Vortex:
							curForce.axis = force.vortexAxis;
							curForce.inwardForce = force.inwardForce / 1000.0f * Manager.ParticleTimeDelta * 60.0f;
							break;

						case ForceType.Turbulence:
						case ForceType.TurbulenceTexture:
							curForce.axis = force.noiseExtents;
							break;

						default:
							curForce.axis = Vector3.zero;
							break;
					}

					if (force.IsPrimaryForce) {
						curForce.velocity = force.Velocity * force.InheritVelocity;
					}
					else {
						curForce.velocity = Vector3.zero;
					}

					curForce.attenType = (uint) force.attenuationType;
					curForce.turbulencePosFac = 1.0f - force.smoothness;

					curForce.pos = forcePos;
					
					switch (Manager.SimulationSpace) {
						case Space.Local:
							curForce.pos = systTransform.InverseTransformPoint(curForce.pos);
							curForce.axisX = systTransform.InverseTransformDirection(curForce.axisX);
							curForce.axisY = systTransform.InverseTransformDirection(curForce.axisY);
							curForce.axisZ = systTransform.InverseTransformDirection(curForce.axisZ);
							break;
						
						case Space.Parent:
							curForce.pos = forcePos - parentPosition;
							break;
					}

					m_forcesStruct[t][f] = curForce;
				}
			}
		}

		void DistributeForces() {
			//easy fix for when forces get registered before system has initialized resources
			if (m_forcesBuffer == null || MaxForces == 0) {
				return;
			}

			if (m_forcesList == null) {
				m_forcesList = new List<TCForce>(32);
			}else{
				m_forcesList.Clear();
			}

			for (int i = 0; i < Tracker<TCForce>.Count; i++) {
				TCForce f = Tracker<TCForce>.All[i];

				if (ForceLayers == (ForceLayers | (1 << f.gameObject.layer))) {
					m_forcesList.Add(f);
				}
			}

			if (m_forcesList.Count > MaxForces) {
				if (m_forceSort == null) {
					m_forceSort = (f1, f2) => GetForcePoints(f2).CompareTo(GetForcePoints(f1));
				}

				m_forcesList.Sort(m_forceSort);
			}

			//Update data
			for (int t = 0; t < ForceTypeKernelCount; ++t) {
				m_forcesCount[t] = 0;
			}

			for (int i = 0; i < Mathf.Min(MaxForces, m_forcesList.Count); i++) {
				TCForce f = m_forcesList[i];
				int t = f.forceType == ForceType.Turbulence || f.forceType == ForceType.TurbulenceTexture ? 1 : 0;

				m_forcesReference[t][m_forcesCount[t]] = f;
				m_forcesCount[t]++;
			}
		}

		internal void Dispatch() {
			if (useBoidsFlocking) {
				if (m_boidsFlock == null) {
					CreateBoids();
				}

				m_boidsFlock.UpdateBoids(SystemComp.transform);
			}

			if (MaxForces == 0) {
				return;
			}

			if (m_forcesCount[0] > 0) {
				m_forcesBuffer[0].SetData(m_forcesStruct[0]);
				ComputeShader.SetBuffer(UpdateForcesKernel, SID._Forces, m_forcesBuffer[0]);
				Manager.BindPariclesToKernel(ComputeShader, UpdateForcesKernel);

				if (Manager.DispatchCount > 0) {
					ComputeShader.Dispatch(UpdateForcesKernel, Manager.DispatchCount, m_forcesCount[0], 1);
				}
			}

			if (m_forcesCount[1] > 0) {
				m_forcesBuffer[1].SetData(m_forcesStruct[1]);
				ComputeShader.SetBuffer(UpdateTurbulenceForcesKernel, SID._TurbulenceForces, m_forcesBuffer[1]);

				for (int k = 0; k < m_forcesCount[1]; ++k) {
					TCForce force = m_forcesReference[1][k];

					if (force.CurrentForceVolume == null) {
						continue;
					}

					ComputeShader.SetTexture(UpdateTurbulenceForcesKernel, SID._TurbulenceTexture, force.CurrentForceVolume);

					Matrix4x4 rotation = Matrix4x4.TRS(Vector3.zero, force.transform.rotation, Vector3.one);
					ComputeShader.SetMatrix(SID._TurbulenceRotation, rotation);
					ComputeShader.SetMatrix(SID._TurbulenceRotationInv, rotation.inverse);

					//TODO: Just bind one force?
					ComputeShader.SetInt("turbulenceKernelOffset", k);

					Manager.BindPariclesToKernel(ComputeShader, UpdateTurbulenceForcesKernel);

					if (Manager.DispatchCount > 0) {
						ComputeShader.Dispatch(UpdateTurbulenceForcesKernel, Manager.DispatchCount, 1, 1);
					}
				}
			}
		}

		float GetForcePoints(TCForce force) {
			if (BaseForces != null && BaseForces.Contains(force)) {
				return float.MaxValue;
			}

			if (!force.enabled) {
				return float.MinValue;
			}

			if (force.transform.parent == SystemComp.transform) {
				return float.MaxValue;
			}

			float projectedForce = force.power;
			float dist = (force.transform.position - SystemComp.transform.position).magnitude;

			switch (force.attenuationType) {
				case AttenuationType.Linear:
				case AttenuationType.EaseInOut:
					projectedForce = projectedForce * (1.0f - dist / force.radius.Max * force.Attenuation);
					break;


				case AttenuationType.Divide:
					projectedForce = Mathf.Lerp(projectedForce, projectedForce / dist, force.Attenuation);
					break;
			}


			var points = Mathf.Abs(projectedForce);
			return points;
		}


		internal override void OnDestroy() {
			for (int t = 0; t < ForceTypeKernelCount; ++t) {
				if (m_forcesBuffer != null && m_forcesBuffer[t] != null){
					Release(ref m_forcesBuffer[t]);
				}
			}

			if (m_boidsFlock != null) {
				m_boidsFlock.ReleaseBuffers();
			}
		}
	}
}