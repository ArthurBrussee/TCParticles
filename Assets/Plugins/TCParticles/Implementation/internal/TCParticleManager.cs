using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TC.Internal {
	[Serializable]
	public class ParticleManager : ParticleComponent, TCParticleManager {
		#region Implementation

		//Number of threads that should be dispatched in X
		public int DispatchCount {
			get { return Mathf.CeilToInt(ParticleCount / GroupSize) + 1; }
		}

		public ComputeBuffer particles;
		//numParticles is clamped by MaxParticles

		public int NumParticles {
			get { return ParticleCount; }

			set { ParticleCount = value; }
		}

		[SerializeField] private Space _simulationSpace = Space.Local;

		[SerializeField] private bool m_noSimulation;



		private bool UseShuriken {
			get { return !Supported && shurikenFallback != null; }
		}

		//Time that particle system is playing, in range of [0, duration]

		#endregion

		#region PublicApi

		[SerializeField] private int _maxParticles = 10000;

		public int MaxParticles {
			get { return Mathf.RoundToInt(_maxParticles); }
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

		[SerializeField] private float _duration = 3.0f;

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


		[SerializeField] private AnimationCurve dampingCurve;

		[SerializeField] private bool dampingIsCurve = false;

		private ComputeBuffer m_systemBuffer;


		public float ParticleTimeDelta { get; private set; }

		/// <summary>
		/// Is the particle system paused?
		/// </summary>
		public bool IsPaused { get; private set; }

		/// <summary>
		/// Is the particle system paused?
		/// </summary>
		public bool IsStopped { get; private set; }

		private bool m_isPlaying = true;

		/// <summary>
		/// Is the particle system playing?
		/// </summary>
		public bool IsPlaying {
			get { return m_isPlaying && !IsStopped && !IsPaused && SystemTime < _duration; }
		}

		public int MaxParticlesBuffer {
			get { return Mathf.CeilToInt(_maxParticles * 1.15f / GroupSize) * (int) GroupSize; }
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

		private struct SystemParameters {
			public Vector3 ConstantForce;
			public float AngularVelocity;
			public float Damping;
			public float MaxSpeed;
			public float VelocitySampleScale;

			public float ParticleThickness;
			public float DeltTime;
			public uint Offset;
			public uint MaxParticles;
		}

		private List<TCParticleSystem> m_children;

		private List<TCParticleSystem> Children {
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
			m_systemBuffer = new ComputeBuffer(1, SystemParamsStride);
		}

		private void CreateParticleBuffer() {
			if (particles != null) {
				particles.Release();
			}

			particles = new ComputeBuffer(MaxParticlesBuffer, ParticleStructStride);
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

			if (particles.count != MaxParticlesBuffer) {
				CreateParticleBuffer();
			}


			Target = null;


			if (IsPaused || IsStopped) {
				return;
			}

			Profiler.BeginSample("Update loop");

			if (m_isPlaying) {
				if (SystemTime > Duration && !looping) {
					m_isPlaying = false;
					return;
				}

				if (DoUpdate()) {
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

		private void UpdateParticles(float deltTime) {

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

		private void Dispatch(bool emit) {

			Profiler.BeginSample("Dispatch loop");

			Profiler.BeginSample("Update bursts");
			PartEmitter.UpdateParticleBursts();
			Profiler.EndSample();



			SetParticles();


			if (emit) {
				PartEmitter.UpdatePlayEvent();
			}

			if (!m_noSimulation) {
				//Dispatch the update kernel.
				SetPariclesToKernel(ComputeShader, UpdateAllKernel);
				ComputeShader.Dispatch(UpdateAllKernel, DispatchCount, 1, 1);
				ColliderManager.Dispatch();
				ForceManager.Dispatch();
			}

			Profiler.EndSample();
		}

		public void SetPariclesToKernel(ComputeShader shader, int kernel) {
			Profiler.BeginSample("Set particle buffer");
			//for (int k = 0; k < NumKernelsTotal; ++k) {
			shader.SetBuffer(kernel, "systemParameters", m_systemBuffer);
			shader.SetBuffer(kernel, "particles", particles);
			//}

			Profiler.EndSample();
		}


		public override void OnDisable() {
			Clear();
		}

		public override void OnDestroy() {
			Release(ref particles);
			Release(ref m_systemBuffer);
		}



		private SystemParameters m_syst;
		private SystemParameters[] m_systArray = new SystemParameters[1];

		protected override void Set() {
			float d = dampingIsCurve ? dampingCurve.Evaluate(SystemTime / Duration) : damping;


			m_syst.AngularVelocity = PartEmitter.AngularVelocity * Mathf.Deg2Rad * Manager.ParticleTimeDelta;
			m_syst.Damping = Mathf.Pow(Mathf.Abs(1.0f - d), ParticleTimeDelta);
			m_syst.MaxSpeed = MaxSpeed > 0 ? MaxSpeed : float.MaxValue;



			m_syst.DeltTime = ParticleTimeDelta;
			m_syst.MaxParticles = (uint) MaxParticles;
			m_syst.Offset = (uint) PartEmitter.Offset;



			float actualThickness = ColliderManager.ParticleThickness * 0.5f;

			if (Renderer.isPixelSize) {
				actualThickness *= Mathf.Max(1.0f / Screen.width, 1.0f / Screen.height);
			}


			m_syst.ParticleThickness = actualThickness;
			m_syst.ConstantForce = ConstantForce.Value(Manager.SystemTime / Manager.Duration) * ParticleTimeDelta +
			                       Physics.gravity * Manager.gravityMultiplier * ParticleTimeDelta * m_syst.Damping;

			m_systArray[0] = m_syst;

			Profiler.BeginSample("Set data");
			m_systemBuffer.SetData(m_systArray);
			Profiler.EndSample();
		}


		private void PlayEvent() {
			SystemTime = 0.0f;
			RealTime = 0.0f;
		}

		private IEnumerator PlayRoutine() {
			if (delay != 0.0f) {
				yield return new WaitForSeconds(delay);
			}

			if (SystemTime >= Duration && !looping) {
				PlayEvent();
			}

			PartEmitter.PlayEvent();


			if (prewarm) {
				Simulate(Duration, false);
			}

			m_isPlaying = true;
		}

		private IEnumerator SimulateRoutine(float simTime) {
			yield return new WaitForEndOfFrame();

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

			SetParticles();
			PartEmitter.EmitterEmit(count);
		}

		//Extension handling --- for people who know what they are doing only
		private void BindParticleBufferToExtension(ComputeShader extension, string kernelName) {
			int kern = extension.FindKernel(kernelName);

			if (kern == -1) {
				Debug.Log("Can't find custom kernel! Make sure you have named it properly and added the #pragma");
				return;
			}

			if (particles == null || m_systemBuffer == null) {
				return;
			}

			SetPariclesToKernel(extension, kern);
		}

		public void DispatchExtensionKernel(ComputeShader extension, string kernelName) {
			int kern = extension.FindKernel(kernelName);

			if (kern == -1) {
				Debug.Log("Can't find custom kernel! Make sure you have named it properly and added the #pragma");
				return;
			}


			SetPariclesToKernel(extension, kern);
			extension.Dispatch(kern, DispatchCount, 1, 1);
		}
	}
}