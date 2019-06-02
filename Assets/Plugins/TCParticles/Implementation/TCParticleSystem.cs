using System;
using System.ComponentModel;
using TC.Internal;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace TC {
	/// <summary>
	/// GPU Particle system. Provides basic API to control particles
	/// </summary>
	/// <remarks>
	/// To access more specific APIs use the  <see cref="Manager"/>, <see cref="Emitter"/>, <see cref="ForceManager"/> and <see cref="ColliderManager"/> properties.
	/// </remarks>
	[AddComponentMenu("TC Particles/TC Particle Manager"), ExecuteInEditMode]
	public class TCParticleSystem : MonoBehaviour, ITracked {
		int m_index = -1;

		/// <summary>
		/// Unique index in Tracker list
		/// </summary>
		public int Index {
			get => m_index;
			set => m_index = value;
		}

		[SerializeField] ParticleColliderManager _colliderManager = new ParticleColliderManager();
		[SerializeField] ParticleEmitter _emitter = new ParticleEmitter();
		[SerializeField] ParticleForceManager _forcesManager = new ParticleForceManager();
		[SerializeField] ParticleManager _manager = new ParticleManager();
		[SerializeField] ParticleRenderer _particleRenderer = new ParticleRenderer();

		bool m_doVisualize;

#if UNITY_EDITOR

		/// <summary>
		/// Should this system be visualized (editor only)
		/// </summary>
		public bool DoVisualize {
			get => m_doVisualize;
			set {
				if (value != m_doVisualize) {
					m_doVisualize = value;

					if (!m_doVisualize) {
						Clear();
					}
				}
			}
		}

#endif

		public static void UpdateCacheForEmitMesh(Mesh mesh) {
			for (int i = 0; i < 4; ++i) {
				TCParticleGlobalManager.UpdateCacheForEmitMesh(mesh, i);
			}
		}

		public static void ReleaseCacheForEmitMesh(Mesh mesh) {
			for (int i = 0; i < 4; ++i) {
				TCParticleGlobalManager.ReleaseCacheForEmitMesh(mesh, i);
			}
		}

		/// <summary>
		/// Contains collision properties
		/// </summary>
		public ParticleColliderManager ColliderManager => _colliderManager;

		/// <summary>
		/// Contains emission properties
		/// </summary>
		public ParticleEmitter Emitter => _emitter;

		/// <summary>
		/// Contains rendering properties
		/// </summary>
		public ParticleRenderer ParticleRenderer => _particleRenderer;

		/// <summary>
		/// Contains force properties
		/// </summary>
		public ParticleForceManager ForceManager => _forcesManager;

		/// <summary>
		/// Main properties
		/// </summary>
		public ParticleManager Manager => _manager;

		/// <summary>
		/// At what part of the loop is the system currently? Between 0...1
		/// </summary>
		public float Lifespan => _manager.SystemTime / _manager.Duration;

		/// <summary>
		/// The maximum number of particles in the particle buffer (Read Only)
		/// </summary>
		public int MaxParticles {
			get => _manager.MaxParticles;

			set => _manager.MaxParticles = value;
		}

		/// <summary>
		/// Current (approximate) particle count
		/// </summary>
		/// <remarks>
		/// This is only approximate as it is not known exactly when particles die off. All particles are assumed to live the maximum set lifetime
		/// </remarks>
		public int ParticleCount => _emitter.ParticleCount;

		/// <summary>
		/// Space in which the particles should be simulated
		/// </summary>
		public Space SimulationSpace {
			get => _manager.SimulationSpace;
			set => _manager.SimulationSpace = value;
		}

		/// <summary>
		/// Are the particles being simulated in world space?
		/// </summary>
		public bool IsWorldSpace => SimulationSpace == Space.World;

		/// <summary>
		/// The current time of the system ranging from 0...duration
		/// </summary>
		public float SystemTime => _manager.SystemTime;

		/// <summary>
		/// The duration of the system (or system life)
		/// </summary>
		public float Duration {
			get => _manager.Duration;
			set => _manager.Duration = value;
		}

		/// <summary>
		/// The initial delay of the particle system
		/// </summary>
		public float Delay => _manager.delay;

		/// <summary>
		/// Should the particle system start playing as soon as it's created?
		/// </summary>
		public bool PlayOnAwake => _manager.playOnAwake;

		/// <summary>
		/// Should the particle system loop?
		/// </summary>
		public bool Looping {
			get => _manager.looping;
			set => _manager.looping = value;
		}

		/// <summary>
		/// Should the particle system go through one loop when created?
		/// </summary>
		public bool Prewarm => _manager.prewarm;

		/// <summary>
		/// A multiplier for the speed of time
		/// </summary>
		public float PlaybackSpeed {
			get => _manager.playbackSpeed;
			set => _manager.playbackSpeed = value;
		}

		/// <summary>
		/// Is the particle system currently playing?
		/// </summary>
		public bool IsPlaying => _manager.IsPlaying;

		/// <summary>
		/// Has the particle system been stopped?
		/// </summary>
		public bool IsStopped => _manager.IsStopped;

		/// <summary>
		/// Is the particle system paused?
		/// </summary>
		public bool IsPaused => _manager.IsPaused;

		/// <summary>
		/// Constant force over lifetime
		/// </summary>
		public Vector3Curve ConstantForce {
			get => _manager.ConstantForce;
			set => _manager.ConstantForce = value;
		}

		/// <summary>
		/// Amount of particles emitted each second
		/// </summary>
		public float EmissionRate {
			get => _manager.EmissionRate;
			set => _manager.EmissionRate = value;
		}

		/// <summary>
		/// The amount of damping particles recieve each frame
		/// </summary>
		public float Damping {
			get => _manager.damping;
			set => _manager.damping = value;
		}

		/// <summary>
		/// What factor of your physics gravity should be applied?
		/// </summary>
		public float GravityMultiplier {
			get => _manager.gravityMultiplier;
			set => _manager.gravityMultiplier = value;
		}

		/// <summary>
		/// Delta time of particle simulation
		/// </summary>
		public float ParticleTimeDelta => _manager.ParticleTimeDelta;

		[NonSerialized] bool m_inited;

		void Init() {
			if (m_inited) {
				return;
			}

			Profiler.BeginSample("Init");

			m_inited = true;

			_manager.Awake(this);
			_emitter.Awake(this);
			_colliderManager.Awake(this);
			_forcesManager.Awake(this);
			_particleRenderer.Awake(this);

			Profiler.EndSample();
		}

		void Awake() {
			if (Application.isPlaying) {
				Init();
			}
		}

		void OnEnable() {
#if UNITY_EDITOR
			if (EditorUtility.IsPersistent(gameObject)) {
				return;
			}
#endif

			Tracker<TCParticleSystem>.Register(this);

#if UNITY_EDITOR
			if (!Application.isPlaying) {
				Init();
			}
#endif

			_manager.OnEnable();
			_emitter.OnEnable();
			_colliderManager.OnEnable();
			_forcesManager.OnEnable();
			_particleRenderer.OnEnable();
		}

		void OnDisable() {
#if UNITY_EDITOR
			if (EditorUtility.IsPersistent(gameObject)) {
				return;
			}
#endif

			_manager.OnDisable();
			_emitter.OnDisable();
			_colliderManager.OnDisable();
			_forcesManager.OnDisable();
			_particleRenderer.OnDisable();

			if (!Application.isPlaying) {
				OnDestroy();
			}

			Tracker<TCParticleSystem>.Deregister(this);
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public void EditorUpdate() {
			UpdateComponents();
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public void TCUpdate() {
			UpdateComponents();
		}

		void UpdateComponents() {
			_manager.Update();
			_emitter.Update();
			_colliderManager.Update();
			_forcesManager.Update();
			_particleRenderer.Update();
		}

		void OnDestroy() {
			_manager.OnDestroy();
			_emitter.OnDestroy();
			_colliderManager.OnDestroy();
			_forcesManager.OnDestroy();
			_particleRenderer.OnDestroy();
		}

		/// <summary>
		/// Pause the systems simulation
		/// </summary>
		/// <param name="withChildren">Should children be paused as well?</param>
		public void Pause(bool withChildren = true) {
			_manager.Pause(withChildren);
		}

		/// <summary>
		/// Resume the systems simulation
		/// </summary>
		/// <param name="withChildren">Should children resume playing as well?</param>
		public void Play(bool withChildren = true) {
			_manager.Play(withChildren);
		}

		/// <summary>
		/// Simulate the system for a given amout of time
		/// </summary>
		/// <param name="time">The time to simulate</param>
		/// <param name="withChildren">Should children be simulated as well?</param>
		/// <param name="restart">Should the system restart before simulating the given amount of time?</param>
		public void Simulate(float time, bool withChildren = false, bool restart = false) {
			_manager.Simulate(time, withChildren, restart);
		}

		/// <summary>
		/// Stop the system completely
		/// </summary>
		/// <param name="withChildren">Should the children be stopped as well?</param>
		public void Stop(bool withChildren = true) {
			_manager.Stop(withChildren);
		}

		/// <summary>
		/// Clear all particles in the system
		/// </summary>
		/// <param name="withChildren">Should the childrens particles be stopped as well?</param>
		public void Clear(bool withChildren = true) {
			_manager.Clear(withChildren);
		}

		/// <summary>
		/// Emit a number of particles
		/// </summary>
		/// <param name="count">Number of particles to emit</param>
		public void Emit(int count) {
			_emitter.Emit(count);
		}

		/// <summary>
		/// Emit a number of particles at some set positions
		/// </summary>
		/// <param name="positions">Positions of particles to emit</param>
		public void Emit(Vector3[] positions) {
			_emitter.Emit(positions);
		}

		/// <summary>
		/// Emit a number of particles based on a prototype
		/// </summary>
		/// <param name="proto">Prototypes of particles to emit</param>
		/// <param name="useColor">Use color from prototypes or default</param>
		/// <param name="useSize">Use size (multiplicative) from prototypes or default</param>
		/// <param name="useVelocity">Use velocity from prototypes or default</param>
		public void Emit(ParticleProto[] proto, bool useColor = true, bool useSize = true, bool useVelocity = true) {
			_emitter.Emit(proto, useColor, useSize, useVelocity);
		}

		/// <summary>
		/// Emit a number of particles based on a prototype
		/// </summary>
		/// <param name="proto">Prototypes of particles to emit</param>
		/// <param name="count">Number of particles to emit taken from the prototype array</param>
		/// <param name="useColor">Use color from prototypes or default</param>
		/// <param name="useSize">Use size (multiplicative) from prototypes or default</param>
		/// <param name="useVelocity">Use velocity from prototypes or default</param>
		public void Emit(ParticleProto[] proto, int count, bool useColor = true, bool useSize = true, bool useVelocity = true) {
			_emitter.Emit(proto, count, useColor, useSize, useVelocity);
		}

		void OnDrawGizmos() {
			Gizmos.DrawIcon(transform.position, "TCParticles.png", true);
		}

		void OnDrawGizmosSelected() {
			if (_emitter.Shape != EmitShapes.Box) {
				return;
			}

			var myTransform = transform;
			Matrix4x4 rotationMatrix = Matrix4x4.TRS(myTransform.position, myTransform.rotation, myTransform.localScale);
			Gizmos.matrix = rotationMatrix;
			Gizmos.DrawWireCube(Vector3.zero, _emitter.CubeSize);
		}
	}
}