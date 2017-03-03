using System;
using System.Collections.Generic;
using TC;
using TC.Internal;
using UnityEngine;
using UnityEngine.Profiling;
using ParticleEmitter = TC.Internal.ParticleEmitter;
using ParticleRenderer = TC.Internal.ParticleRenderer;
using Space = TC.Space;

[AddComponentMenu("TC Particles/TC Particle Manager")]
[ExecuteInEditMode]
public class TCParticleSystem : MonoBehaviour {

	public static List<TCParticleSystem> All = new List<TCParticleSystem>();
	
	[SerializeField] private ParticleColliderManager _colliderManager = new ParticleColliderManager();
	[SerializeField] private ParticleEmitter _emitter = new ParticleEmitter();

	[SerializeField] private ParticleForceManager _forcesManager = new ParticleForceManager();
	[SerializeField] private ParticleManager _manager = new ParticleManager();

	[SerializeField] private ParticleRenderer _particleRenderer = new ParticleRenderer();
	
	private bool m_doVisualize;

	public bool DoVisualize {
		get { return m_doVisualize; }
		set {
			if (value != m_doVisualize) {
				m_doVisualize = value;

				if (!m_doVisualize) {
					Clear();
				}
			}
		}
	}


	/// <summary>
	/// Contains collision properties
	/// </summary>
	public TCParticleColliderManager ColliderManager {
		get { return _colliderManager; }
	}

	/// <summary>
	/// Contains emission properties
	/// </summary>
	public TCParticleEmitter Emitter {
		get { return _emitter; }
	}

	/// <summary>
	/// Contains rendering properties
	/// </summary>
	public TCParticleRenderer ParticleRenderer {
		get { return _particleRenderer; }
	}

	/// <summary>
	/// Contains force properties
	/// </summary>
	public TCParticleForceManager ForceManager {
		get { return _forcesManager; }
	}

	public TCParticleManager Manager {
		get { return _manager; }
	}

	/// <summary>
	/// At what part of the loop is the system currently? Between 0...1
	/// </summary>
	public float Lifespan {
		get { return _manager.SystemTime / _manager.Duration; }
	}


	#region ParticleSystemAPI

	/// <summary>
	/// The maximum number of particles simulated
	/// </summary>
	public int MaxParticles {
		get { return _manager.MaxParticles; }
		set { _manager.MaxParticles = value; }
	}

	/// <summary>
	/// Approximate number of particles in the system. Note: Can be much higher than actual count!
	/// </summary>
	public int ParticleCount {
		get { return _manager.ParticleCount; }
	}

	/// <summary>
	/// Shuriken system to use when DX11 is not supported
	/// </summary>
	public ParticleSystem ShurikenFallback {
		get { return _manager.shurikenFallback; }
		set { _manager.shurikenFallback = value; }
	}


	/// <summary>
	/// Space in which the particles should be simulated
	/// </summary>
	public Space SimulationSpace {
		get { return _manager.SimulationSpace; }
		set { _manager.SimulationSpace = value; }
	}


	/// <summary>
	/// Are the particles being simulated in world space?
	/// </summary>
	public bool IsWorldSpace {
		get { return SimulationSpace == Space.World; }
	}

	/// <summary>
	/// The current time of the system ranging from 0...duration
	/// </summary>
	public float SystemTime {
		get { return _manager.SystemTime; }
	}

	/// <summary>
	/// The duration of the system (or system life)
	/// </summary>
	public float Duration {
		get { return _manager.Duration; }
		set { _manager.Duration = value; }
	}

	/// <summary>
	/// The initial delay of the particle system
	/// </summary>
	public float Delay {
		get { return _manager.delay; }
	}


	/// <summary>
	/// Should the particle system start playing as soon as it's created?
	/// </summary>
	public bool PlayOnAwake {
		get { return _manager.playOnAwake; }
	}

	/// <summary>
	/// Should the particle system loop?
	/// </summary>
	public bool Looping {
		get { return _manager.looping; }
		set { _manager.looping = value; }
	}

	/// <summary>
	/// Should the particle system go through one loop when created?
	/// </summary>
	public bool Prewarm {
		get { return _manager.prewarm; }
	}


	/// <summary>
	/// A multiplier for the speed of time
	/// </summary>
	public float PlaybackSpeed {
		get { return _manager.playbackSpeed; }
		set { _manager.playbackSpeed = value; }
	}


	/// <summary>
	/// Is the particle system currently playing?
	/// </summary>
	public bool IsPlaying {
		get { return _manager.IsPlaying; }
	}

	/// <summary>
	/// Has the particle system been stopped?
	/// </summary>
	public bool IsStopped {
		get { return _manager.IsStopped; }
	}


	/// <summary>
	/// Is the particle system paused?
	/// </summary>
	public bool IsPaused {
		get { return _manager.IsPaused; }
	}

	/// <summary>
	/// Constant force over lifetime
	/// </summary>
	public Vector3Curve ConstantForce {
		get { return _manager.ConstantForce; }
		set { _manager.ConstantForce = value; }
	}


	/// <summary>
	/// Amount of particles emitted each second
	/// </summary>
	public float EmissionRate {
		get { return _manager.EmissionRate; }
		set { _manager.EmissionRate = value; }
	}

	/// <summary>
	/// The amount of damping particles recieve each frame
	/// </summary>
	public float Damping {
		get { return _manager.damping; }
		set { _manager.damping = value; }
	}

	/// <summary>
	/// What factor of your physics gravity should be applied?
	/// </summary>
	public float GravityMultiplier {
		get { return _manager.gravityMultiplier; }
		set { _manager.gravityMultiplier = value; }
	}

	public const int ParticleStride = 4;
	#endregion

	[NonSerialized]
	private bool m_inited;

	private void Init() {
		if (m_inited) {
			return;
		}

		Profiler.BeginSample("Init");

		m_inited = true;

		_manager.SystemComp = this;
		_emitter.SystemComp = this;
		_colliderManager.SystemComp = this;
		_forcesManager.SystemComp = this;
		_particleRenderer.SystemComp = this;

		_manager.Awake(_emitter, _colliderManager, _particleRenderer, _forcesManager, _manager);
		_emitter.Awake(_emitter, _colliderManager, _particleRenderer, _forcesManager, _manager);
		_colliderManager.Awake(_emitter, _colliderManager, _particleRenderer, _forcesManager, _manager);
		_forcesManager.Awake(_emitter, _colliderManager, _particleRenderer, _forcesManager, _manager);
		_particleRenderer.Awake(_emitter, _colliderManager, _particleRenderer, _forcesManager, _manager);
		_manager.Initialize();
		_emitter.Initialize();
		_colliderManager.Initialize();
		_forcesManager.Initialize();
		_particleRenderer.Initialize();

		Profiler.EndSample();
	}

	void Awake() {
		if (Application.isPlaying) {
			Init();
		}
	}

	public void OnEnable() {
		All.Add(this);

		if (!Application.isPlaying) {
			Init();
		}

		_manager.OnEnable();
		_emitter.OnEnable();
		_colliderManager.OnEnable();
		_forcesManager.OnEnable();
		_particleRenderer.OnEnable();

		for (int i = 0; i < TCForce.All.Count; ++i) {
			RegisterForce(TCForce.All[i]);
		}

		for (int i = 0; i < TCCollider.All.Count; ++i) {
			RegisterCollider(TCCollider.All[i]);
		}
		
		for (int i = 0; i < TCShapeEmitter.All.Count; ++i) {
			_emitter.RegisterShape(TCShapeEmitter.All[i]);
		}
	}


	public void OnDisable() {
		_manager.OnDisable();
		_emitter.OnDisable();
		_colliderManager.OnDisable();
		_forcesManager.OnDisable();
		_particleRenderer.OnDisable();

		for (int i = 0; i < TCForce.All.Count; ++i) {
			RemoveForce(TCForce.All[i]);
		}

		for (int i = 0; i < TCCollider.All.Count; ++i) {
			RemoveCollider(TCCollider.All[i]);
		}

		for (int i = 0; i < TCShapeEmitter.All.Count; ++i) {
			_emitter.RemoveShape(TCShapeEmitter.All[i]);
		}

		if (!Application.isPlaying) {
			OnDestroy();
		}

		All.RemoveUnordered(this);
	}

	public void EditorUpdate() {
		UpdateComponents();
	}

	void Update() {
		if (!Application.isPlaying) {
			return;
		}

		UpdateComponents();
	}

	void UpdateComponents() {
		_manager.Update();
		_emitter.Update();
		_colliderManager.Update();
		_forcesManager.Update();
	}

	void OnDestroy() {
		_manager.OnDestroy();
		_emitter.OnDestroy();
		_colliderManager.OnDestroy();
		_forcesManager.OnDestroy();
		_particleRenderer.OnDestroy();
	}

	//Public API

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
	/// Register a force for use. Doesn't have to be called manually usually
	/// </summary>
	/// <param name="force">The force to register</param>
	public void RegisterForce(TCForce force) {
		_forcesManager.RegisterForce(force);
	}

	/// <summary>.
	/// Remove a force from the system. Doesn't have to be called manually usually
	/// </summary>
	/// <param name="force">The force to remove</param>
	public void RemoveForce(TCForce force) {
		_forcesManager.RemoveForce(force);
	}

	/// <summary>
	/// Register a collider for use. Doesn't have to be called manually usually
	/// </summary>
	/// <param name="colliderReg">The collider to register</param>
	public void RegisterCollider(TCCollider colliderReg) {
		_colliderManager.RegisterCollider(colliderReg);
	}

	/// <summary>
	/// Remove a collider from the system. Doesn't have to be called manually usually
	/// </summary>
	/// <param name="colliderReg">The collider to remove</param>
	public void RemoveCollider(TCCollider colliderReg) {
		_colliderManager.RemoveCollider(colliderReg);
	}

	private void OnDrawGizmos() {
		Gizmos.DrawIcon(transform.position, "TCParticles.png", true);
	}

	private void OnDrawGizmosSelected() {
		if (_emitter.Shape == EmitShapes.Box) {
			Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.localScale);
			Gizmos.matrix = rotationMatrix;

			Gizmos.DrawWireCube(Vector3.zero, _emitter.CubeSize);
		}
	}
}