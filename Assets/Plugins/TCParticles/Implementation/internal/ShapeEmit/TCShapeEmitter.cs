using System;
using TC.Internal;
using UnityEngine;
using System.Linq;


namespace TC {
	/// <summary>
	/// Emitter shape that can be linked to a particle system to emit efficiently from lots of sources
	/// </summary>
	[ExecuteInEditMode]
	[AddComponentMenu("TC Particles/Shape Emitter")]
	public class TCShapeEmitter : MonoBehaviour, ITracked {
		int m_index = -1;

		/// <summary>
		/// Unique index in Tracker list
		/// </summary>
		public int Index {
			get { return m_index; }
			set { m_index = value; }
		}


		/// <summary>
		/// Holds all data about the curret emission shape
		/// </summary>
		public ParticleEmitterShape ShapeData = new ParticleEmitterShape();

		/// <summary>
		/// Rate of emission over time or unit
		/// </summary>
		public float EmissionRate;

		/// <summary>
		/// Whether particles should be emitted per second or per unit distance
		/// </summary>
		public ParticleEmitter.EmissionMethod EmissionType;

		/// <summary>
		/// Should particles be emitted from this shape emitter
		/// </summary>
		public bool Emit = true;


		//TODO: Can we make this private?
		[NonSerialized] public Vector3 PrevPos;
		[NonSerialized] public Vector3 PrevSpeed;

		[SerializeField] TCShapeEmitTag m_tag;

		/// <summary>
		/// The emission tag used to link this shape emitter to some particle emtiter in the scene
		/// </summary>
		public TCShapeEmitTag Tag {
			get { return m_tag; }
			set { m_tag = value; }
		}

		bool m_firstEmit;
		float m_femit;



		void Awake() {
			m_firstEmit = true;
		}

		void OnEnable() {
			Tracker<TCShapeEmitter>.Register(this);
		}

		void OnDisable() {
			Tracker<TCShapeEmitter>.Deregister(this);
		}

		/// <summary>
		/// Queue a number of particles to be emitted in a burst.
		/// </summary>
		/// <param name="particles">Nr. of particles to emit in current shape</param>
		public void BurstEmit(int particles) {
			if (!enabled) {
				return;
			}

			m_femit += particles;
		}


		/// <summary>
		/// Emit a given amount of particles with some initial starting positions
		/// </summary>
		/// <param name="positions">Starting positions of particles</param>
		public void BurstEmit(Vector3[] positions) {
			BurstEmit(positions.Select(pos => new ParticleProto { Position = pos }).ToArray(), false, false, false);
		}

		/// <summary>
		/// Emit a given amount of particles with some initial settings
		/// </summary>
		/// <param name="prototypes">List of particle prototypes</param>
		/// <param name="useColor">Whether the color of the prototypes should be applied</param>
		/// <param name="useSize">Whether the size of the prototypes should be applied</param>
		/// <param name="useVelocity">Whether the velocity of the prototypes should be applied</param>
		/// <param name="usePosition">Wether the position of the prototypes should be applied</param>
		public void BurstEmit(ParticleProto[] prototypes, bool useColor = true, bool useSize = true, bool useVelocity = true, bool usePosition = true) {
			BurstEmit(prototypes, prototypes.Length, useColor, useSize, useVelocity, usePosition);
		}

		public void BurstEmit(ParticleProto[] prototypes, int count, bool useColor = true, bool useSize = true, bool useVelocity = true, bool usePosition = true) {
			if (!enabled) {
				return;
			}

			ShapeData.SetPrototypeEmission(prototypes, count, useColor, useSize, useVelocity, usePosition);
			m_femit += count;
		}


		/// <summary>
		/// Whether this system links with a particular emit tag
		/// </summary>
		/// <param name="emitTag">The emit tag to check</param>
		/// <returns></returns>
		public bool LinksToTag(TCShapeEmitTag emitTag) {
			return emitTag == m_tag;
		}

		internal int TickEmission(TCParticleSystem system) {
			Vector3 curEmitPos = ParticleEmitter.GetEmitPos(system, transform);

			if (m_firstEmit) {
				PrevPos = curEmitPos;
				PrevSpeed = Vector3.zero;
				m_firstEmit = false;
			}

			if (EmissionType == ParticleEmitter.EmissionMethod.PerSecond) {
				m_femit += system.Manager.ParticleTimeDelta * EmissionRate;
			} else {
				Vector3 pos = curEmitPos;
				Vector3 delta = pos - PrevPos;
				m_femit += delta.magnitude * EmissionRate;
			}


			int num = Mathf.FloorToInt(m_femit);
			m_femit -= num;

			return num;
		}

		void OnDrawGizmosSelected() {
			Gizmos.DrawIcon(transform.position, "TCParticles.png", true);

			Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.localScale);
			Gizmos.matrix = rotationMatrix;

			if (ShapeData.shape == EmitShapes.Box) {
				Gizmos.DrawWireCube(Vector3.zero, ShapeData.cubeSize);
			}
		}

	}
}