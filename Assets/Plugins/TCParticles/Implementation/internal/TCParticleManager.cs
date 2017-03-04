using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace TC.Internal {
	[Serializable]
	public class ParticleManager : ParticleComponent, TCParticleManager {
		#region Implementation

		//Number of threads that should be dispatched in X
		public int DispatchCount {
			get { return Mathf.CeilToInt(ParticleCount / (float)GroupSize); }
		}

		public ComputeBuffer particles;
		
		//numParticles is clamped by MaxParticles
		public int NumParticles {
			get { return ParticleCount; }
			set { ParticleCount = value; }
		}

		[SerializeField] Space _simulationSpace = Space.Local;
		[SerializeField] bool m_noSimulation;

		bool UseShuriken {
			get { return !Supported && shurikenFallback != null; }
		}
		
		//Time that particle system is playing, in range of [0, duration]

		#endregion

		#region PublicApi

		[SerializeField] int _maxParticles = 10000;

		public int MaxParticles {
			get { return _maxParticles; }
			set { _maxParticles = value; }
		}

		public bool NoSimulation {
			get { return m_noSimulation; }
		}

		public int ParticleCount { get; private set; }
		public ParticleSystem shurikenFallback;

		public Space SimulationSpace {
			get { return _simulationSpace; }
			set {
				if (value != _simulationSpace) {
					_simulationSpace = value;
					Clear();
					PartEmitter.UpdateSpace();
				}
			}
		}

		public bool IsWorldSpace {
			get { return _simulationSpace == Space.World; }
		}

		/// <summary>
		/// Time particle system has been playing since it is playing
		/// </summary>
		public float RealTime { get; private set; }
		public float SystemTime { get; private set; }

		[SerializeField] float _duration = 3.0f;

		public float Duration {
			get { return _duration; }
			set { _duration = value; }
		}

		public float delay = 0.0f;
		public bool playOnAwake = true;
		public bool looping = true;
		public bool prewarm = false;

		[Range(0.0f, 2.0f)] public float playbackSpeed = 1.0f;

		public float gravityMultiplier;

		[Range(0.0f, 1.0f)] public float damping;
		public float MaxSpeed = -1.0f;


		[SerializeField] AnimationCurve dampingCurve;

		[SerializeField] bool dampingIsCurve = false;


		public float ParticleTimeDelta { get; private set; }
		/// <summary>
		/// Is the particle system paused?
		/// </summary>
		public bool IsPaused { get; private set; }
		/// <summary>
		/// Is the particle system paused?
		/// </summary>
		public bool IsStopped { get; private set; }

		bool m_isPlaying = true;

		/// <summary>
		/// Is the particle system playing?
		/// </summary>
		public bool IsPlaying {
			get { return m_isPlaying && !IsStopped && !IsPaused && SystemTime < _duration; }
		}

		/// <summary>
		/// Number of particles emitted each second
		/// </summary>
		public float EmissionRate {
			get { return PartEmitter.EmissionRate; }
			set { PartEmitter.EmissionRate = value; }
		}

		/// <summary>
		/// Gravity of the particle system in world space
		/// </summary>
		public Vector3Curve ConstantForce {
			get { return PartEmitter.ConstantForce; }
			set { PartEmitter.ConstantForce = value; }
		}

		#endregion

		struct SystemParameters {
			public Vector3 ConstantForce;
			public float AngularVelocity;
			public float Damping;
			public float MaxSpeed;


			public const int Stride = (3 + 1 + 1 + 1) * 4;
		}
		SystemParameters[] m_systArray = new SystemParameters[1];
		ComputeBuffer m_systemBuffer;

		[NonSerialized]
		public int Offset;

		List<TCParticleSystem> m_children;
		List<TCParticleSystem> Children {
			get {
				if (m_children == null) {
					m_children = new List<TCParticleSystem>(SystemComp.GetComponentsInChildren<TCParticleSystem>(true));
					m_children.Remove(SystemComp);
				}

				return m_children;
			}
		}

		public override void Initialize() {
			if (UseShuriken) {
				shurikenFallback.gameObject.SetActive(true);
				return;
			}

			if (!Supported) {
				return;
			}

			CreateParticleBuffer();
			m_systemBuffer = new ComputeBuffer(1, SystemParameters.Stride);
		}

		void CreateParticleBuffer() {
			if (particles != null) {
				particles.Release();
			}

			particles = new ComputeBuffer(MaxParticles, TCParticleSystem.ParticleStride);
		}

		public override void OnEnable() {
			//No support? Disable system, and let the shuriken system do it's work.
			if (!Supported) {
				return;
			}

			if (shurikenFallback != null) {
				shurikenFallback.gameObject.SetActive(false);
			}

			if (playOnAwake && Application.isPlaying) {
				Play(false);
			}
		}


		public void Update() {
			if (!Supported) {
				return;
			}

			if (ParticleCount > MaxParticles) {
				if (Application.isEditor) {
					Debug.LogError("Amount of particles (likely) exceeds max particles for TC Particle system!", Transform.gameObject);
				}
			}

			if (particles.count != MaxParticles) {
				CreateParticleBuffer();
			}

			if (IsPaused || IsStopped) {
				return;
			}

			Profiler.BeginSample("Update loop");
			if (m_isPlaying) {
				if (SystemTime > Duration && !looping) {
					m_isPlaying = false;
					return;
				}

				if (ShouldUpdate()) {
					float deltTime = (!IsPaused) ? Mathf.Min(SimulationDeltTIme, 0.1f) * playbackSpeed : 0.0f;


					UpdateParticles(deltTime);
				}
			}
			else {
				ParticleTimeDelta = Time.deltaTime;
				Dispatch(false);
			}
			Profiler.EndSample();
		}

		void UpdateParticles(float deltTime) {
			Profiler.BeginSample("Update particles");
			ParticleTimeDelta = deltTime;

			SystemTime += ParticleTimeDelta;
			RealTime += ParticleTimeDelta;

			if (SystemTime >= Duration) {
				if (looping) {
					SystemTime %= Duration;
					PartEmitter.PlayEvent();
					m_isPlaying = true;
				}
				else {
					m_isPlaying = false;
				}
			}

			Dispatch(true);
			Profiler.EndSample();
		}

		void Dispatch(bool emit) {
			Profiler.BeginSample("Dispatch loop");
			PartEmitter.UpdateParticleBursts();

			BindParticles();

			if (emit) {
				PartEmitter.UpdatePlayEvent();
			}

			if (!m_noSimulation) {
				//Dispatch the update kernel.
				SetPariclesToKernel(ComputeShader, UpdateAllKernel);

				Profiler.BeginSample("Main update");
				ComputeShader.Dispatch(UpdateAllKernel, DispatchCount, 1, 1);
				Profiler.EndSample();

				ColliderManager.Dispatch();
				ForceManager.Dispatch();
			}

			Profiler.EndSample();
		}

		public void SetPariclesToKernel(ComputeShader comp, int kernel) {
			comp.SetBuffer(kernel, "systemParameters", m_systemBuffer);
			comp.SetBuffer(kernel, "particles", particles);

			comp.SetFloat("_DeltTime", ParticleTimeDelta);
			comp.SetInt("_MaxParticles", MaxParticles);
			comp.SetInt("_BufferOffset", Offset);
		}

		public override void OnDisable() {
			Clear();
		}

		public override void OnDestroy() {
			Release(ref particles);
			Release(ref m_systemBuffer);
		}

		protected override void Bind() {
			float d = dampingIsCurve ? dampingCurve.Evaluate(SystemTime / Duration) : damping;

			var syst = m_systArray[0];
			syst.AngularVelocity = PartEmitter.AngularVelocity * Mathf.Deg2Rad * Manager.ParticleTimeDelta;
			syst.Damping = Mathf.Pow(Mathf.Abs(1.0f - d), ParticleTimeDelta);
			syst.MaxSpeed = MaxSpeed > 0 ? MaxSpeed : float.MaxValue;

			float actualThickness = ColliderManager.ParticleThickness * 0.5f;
			if (Renderer.isPixelSize) {
				actualThickness *= Mathf.Max(1.0f / Screen.width, 1.0f / Screen.height);
			}

			ComputeShader.SetFloat("_ParticleThickness", actualThickness);
			
			syst.ConstantForce = ConstantForce.Value(Manager.SystemTime / Manager.Duration) * ParticleTimeDelta +
			                       Physics.gravity * Manager.gravityMultiplier * ParticleTimeDelta * syst.Damping;

			m_systArray[0] = syst;
			m_systemBuffer.SetData(m_systArray);
		}

		void ResetPlayTime() {
			SystemTime = 0.0f;
			RealTime = 0.0f;
		}

		IEnumerator PlayRoutine() {
			if (delay != 0.0f) {
				yield return new WaitForSeconds(delay);
			}

			if (SystemTime >= Duration && !looping) {
				ResetPlayTime();
			}

			PartEmitter.PlayEvent();


			if (prewarm) {
				Simulate(Duration, false);
			}

			m_isPlaying = true;
		}

		static WaitForEndOfFrame s_waitFrame = new WaitForEndOfFrame();

		IEnumerator SimulateRoutine(float simTime) {
			yield return s_waitFrame;

			for (int i = 0; i < 75; ++i) {
				UpdateParticles(simTime / 75.0f);
			}
		}

		//Public API.
		//Calls map to shuriken fallback, if there is no support for compute shaders.
		public void Pause(bool withChildren = true) {
			if (UseShuriken) {
				shurikenFallback.Pause(withChildren);
				return;
			}

			IsPaused = true;

			if (withChildren) {
				Children.ForEach(s => s.Pause());
			}
		}

		public void Play(bool withChildren = true) {
			IsPaused = false;
			m_isPlaying = true;

			IsStopped = false;

			if (UseShuriken) {
				shurikenFallback.Play(withChildren);
				return;
			}

			if (!IsPaused || !looping && SystemTime >= Duration) {
				SystemComp.StartCoroutine(PlayRoutine());
			}

			if (withChildren) {
				Children.ForEach(s => s.Play());
			}
		}


		public void Simulate(float simulateTime, bool withChildren = true, bool restart = false) {
			if (simulateTime == 0.0f) {
				return;
			}

			if (UseShuriken) {
				shurikenFallback.Simulate(simulateTime, withChildren, restart);
				return;
			}

			if (restart) {
				Stop();
				Play();
			}

			SystemComp.StartCoroutine(SimulateRoutine(simulateTime));
			if (withChildren) {
				Children.ForEach(s => s.Simulate(simulateTime));
			}
		}

		public void Stop(bool withChildren = true) {
			if (UseShuriken) {
				shurikenFallback.Stop(withChildren);
				return;
			}

			Clear();
			IsStopped = true;
			IsPaused = false;
			m_isPlaying = false;
			SystemTime = 0.0f;

			if (withChildren) {
				Children.ForEach(s => s.Stop());
			}
		}


		public bool IsAlive() {
			if (UseShuriken) {
				return shurikenFallback.IsAlive();
			}

			return ParticleCount > 0;
		}

		public void Clear(bool withChildren = true) {
			if (UseShuriken) {
				shurikenFallback.Clear(withChildren);
				return;
			}

			PartEmitter.ClearAllEmittedParticles();

			if (withChildren) {
				Children.ForEach(s => s.Clear());
			}
		}

		public void Emit(int count) {
			if (UseShuriken) {
				shurikenFallback.Emit(count);
				return;
			}

			PartEmitter.Emit(count);
		}

		public void DispatchExtensionKernel(ComputeShader extension, int kern) {
			SetPariclesToKernel(extension, kern);

			uint x, y, z;
			extension.GetKernelThreadGroupSizes(kern, out x, out y, out z);
			int disp = Mathf.CeilToInt((float) ParticleCount / x);

			extension.Dispatch(kern, disp, 1, 1);
		}
	}
}