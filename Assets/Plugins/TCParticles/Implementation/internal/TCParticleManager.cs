using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Profiling;
using TC.Internal;
using UnityEngine.Rendering;

namespace TC {
	public struct Particle {
		public Vector3 Position;
		public float Rotation;

		public Vector3 Velocity;
		public float BaseSize;

		public float Life;
		public uint Color;

		public float Random;
		public float Pad;
	}

	
	/// <summary>
	/// Class containing main settings for the TC Particle system
	/// </summary>
	[Serializable]
	public class ParticleManager : ParticleComponent {
		/// <summary>
		/// Get the number of thread groups that should be dispatched in a TC Particles compute shader
		/// </summary>
		/// <returns>Numer of thread groups to dispatch</returns>
		/// <remarks>
		/// Only use in advanced use cases for the extension API.
		/// In most cases it is better to use the built in <see cref="DispatchExtensionKernel"/> method
		/// </remarks>
		public int DispatchCount {
			get {
				const int c_groupSize = 128;
				return Mathf.CeilToInt(Emitter.ParticleCount / (float)c_groupSize);
			}
		}

		/// <summary>
		/// Current (approximate) particle count
		/// </summary>
		/// <remarks>
		/// This is only approximate as it is not known exactly when particles die off. All particles are assumed to live the maximum set lifetime
		/// </remarks>
		public int ParticleCount { get { return Emitter.ParticleCount;  } }

		ComputeBuffer particles;

		
		/// <summary>
		/// Get the buffer containing the current particles data
		/// </summary>
		/// <returns>Buffer with particle data</returns>
		/// <remarks>
		/// Only use in advanced use cases for the extension API.
		/// In most cases it is better to use the built in <see cref="DispatchExtensionKernel"/> method
		/// </remarks>
		public ComputeBuffer GetParticlesBuffer() {
			return particles;
		}

		public Particle[] GetParticleData() {
			Particle[] ret = new Particle[ParticleCount];
			particles.GetData(ret);
			return ret;
		}

		[SerializeField] Space _simulationSpace = Space.Local;
		[SerializeField] bool m_noSimulation;

		[SerializeField] int _maxParticles = 10000;

		/// <summary>
		/// The maximum number of particles in the particle buffer (Read Only)
		/// </summary>
		public int MaxParticles {
			get { return _maxParticles; }
			set {
				if (Application.isPlaying) {
					Debug.LogError("Can't change max particles while playing!");
					return;
				}

				_maxParticles = value;
			}
		}

		/// <summary>
		/// Turn on/off no simulation. No simulation mode can be used for better performance of visualizations
		/// </summary>
		public bool NoSimulation {
			get { return m_noSimulation; }
			set { m_noSimulation = value; }
		}

		/// <summary>
		/// The space the simulation takes place in.
		/// </summary>
		/// <remarks>
		/// When setting this to a new space current particles are cleared
		/// </remarks>
		public Space SimulationSpace {
			get { return _simulationSpace; }
			set {
				if (value != _simulationSpace) {
					_simulationSpace = value;
					Clear();
					Emitter.UpdateSpace();
				}
			}
		}

		/// <summary>
		/// Is the current <see cref="SimulationSpace"/> set to <see cref="Space.World"/>
		/// </summary>
		public bool IsWorldSpace {
			get { return _simulationSpace == Space.World; }
		}

		/// <summary>
		/// Time particle system has been playing since it is playing
		/// </summary>
		public float RealTime { get; private set; }


		/// <summary>
		/// Current looped playback time
		/// </summary>
		public float SystemTime { get; private set; }
		float m_prevSystemTime;

		[SerializeField] float _duration = 3.0f;

		/// <summary>
		/// Total duration the system plays, before looping or stopping.
		/// </summary>
		public float Duration {
			get { return _duration; }
			set { _duration = value; }
		}

		/// <summary>
		/// Delay in seconds before systems starts playing when <see cref="playOnAwake"/> is true
		/// </summary>
		public float delay = 0.0f;

		/// <summary>
		/// Should the system start playing as soon as it wakes up
		/// </summary>
		public bool playOnAwake = true;

		/// <summary>
		/// Does the system loop
		/// </summary>
		public bool looping = true;

		/// <summary>
		/// Should the particles be prewarmed when starting playback
		/// </summary>
		public bool prewarm = false;


		[Range(0.0f, 2.0f)] public float playbackSpeed = 1.0f;

		/// <summary>
		/// The factor of gravity used as configured in the physics settings
		/// </summary>
		public float gravityMultiplier;


		/// <summary>
		/// Drag applied to particles. Specified in speed factor per second
		/// </summary>
		[Range(0.0f, 1.0f)] public float damping;

		public float MaxSpeed = -1.0f;

		[SerializeField] AnimationCurve dampingCurve;
		[SerializeField] bool dampingIsCurve;


		/// <summary>
		/// Delta time of particle simulation
		/// </summary>
		public float ParticleTimeDelta { get; private set; }

		/// <summary>
		/// Is the particle system currently paused?
		/// </summary>
		public bool IsPaused { get; private set; }

		/// <summary>
		/// Is the particle system currently stopped?
		/// </summary>
		/// <remarks>
		/// This is only false when calling <see cref="Stop" /> and not when pausing.
		/// </remarks>
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
			get { return Emitter.EmissionRate; }
			set { Emitter.EmissionRate = value; }
		}

		/// <summary>
		/// Gravity of the particle system in world space
		/// </summary>
		public Vector3Curve ConstantForce {
			get { return Emitter.ConstantForce; }
			set { Emitter.ConstantForce = value; }
		}

		struct SystemParameters {
			public Vector3 ConstantForce;
			public float AngularVelocity;
			public float Damping;
			public float MaxSpeed;
			public const int Stride = (3 + 1 + 1 + 1) * 4;
		}
		readonly SystemParameters[] m_systArray = new SystemParameters[1];
		ComputeBuffer m_systemBuffer;

		List<TCParticleSystem> m_children;
		List<TCParticleSystem> Children {
			get {
				if (m_children == null) {
					//Fill list
					m_children = new List<TCParticleSystem>();
					SystemComp.GetComponentsInChildren(m_children);
					m_children.Remove(SystemComp);
				}

				return m_children;
			}
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		protected override void Initialize() {
			CreateParticleBuffer();
			m_systemBuffer = new ComputeBuffer(1, SystemParameters.Stride);
		}

		void CreateParticleBuffer() {
			if (particles != null) {
				particles.Release();
			}

			particles = new ComputeBuffer(MaxParticles, ParticleStride);
		}

		internal override void OnEnable() {
			if (playOnAwake && Application.isPlaying) {
				Play(false);
			}
		}

		internal void Update() {
			if (Emitter.ParticleCount > MaxParticles) {
				if (Application.isEditor) {
					Debug.LogError("Amount of particles seems to exceed max particles for TC Particle system!", SystemComp.transform.gameObject);
				}
			}

			if (particles.count != MaxParticles) {
				CreateParticleBuffer();
			}

			if (IsPaused || IsStopped) {
				return;
			}

			Profiler.BeginSample("Update loop");
			if (ShouldUpdate()) {
				float deltTime = !IsPaused ? Mathf.Min(SimulationDeltTime, 0.1f) * playbackSpeed : 0.0f;
				UpdateParticles(deltTime);
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
					m_prevSystemTime = -1.0f;
					m_isPlaying = true;
				}
				else {
					m_isPlaying = false;
				}
			}


			Dispatch(m_isPlaying);

			Profiler.EndSample();
		}

		public delegate void SimulationCB();
		public SimulationCB OnPreSimulationCallback;
		public SimulationCB OnPostSimulationCallback;

		public delegate void GlobalSimulationCB(TCParticleSystem system);
		public static GlobalSimulationCB OnGlobalSimulationCallback;

		void Dispatch(bool emit) {
			if (OnGlobalSimulationCallback != null) {
				OnGlobalSimulationCallback(SystemComp);
			}


			Profiler.BeginSample("Simulation loop");
			Emitter.UpdateForDispatch();

			BindParticles();

			if (emit) {
				Emitter.UpdatePlayEvent(m_prevSystemTime);
				m_prevSystemTime = SystemTime;
			}

			if (!NoSimulation) {
				Profiler.BeginSample("Pre Extenions update");
				if (OnPreSimulationCallback != null) {
					OnPreSimulationCallback();
				}
				Profiler.EndSample();

	
				Profiler.BeginSample("Main update");
				DispatchExtensionKernel(ComputeShader, UpdateAllKernel);
				Profiler.EndSample();

				Profiler.BeginSample("Post Extenions update");
				if (OnPostSimulationCallback != null) {
					OnPostSimulationCallback();
				}
				Profiler.EndSample();

				ColliderManager.Dispatch();
				ForceManager.Dispatch();
			}

			Profiler.EndSample();
		}

		internal override void OnDisable() {
			Clear();
		}

		internal override void OnDestroy() {
			Release(ref particles);
			Release(ref m_systemBuffer);
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		protected override void Bind() {
			float d = dampingIsCurve ? dampingCurve.Evaluate(SystemTime / Duration) : damping;

			var syst = m_systArray[0];
			syst.AngularVelocity = Emitter.AngularVelocity * Mathf.Deg2Rad * Manager.ParticleTimeDelta;
			syst.Damping = Mathf.Pow(Mathf.Abs(1.0f - d), ParticleTimeDelta);
			syst.MaxSpeed = MaxSpeed > 0 ? MaxSpeed : float.MaxValue;

			float actualThickness = ColliderManager.ParticleThickness * 0.5f;
			if (Renderer.isPixelSize) {
				actualThickness *= Mathf.Max(1.0f / Screen.width, 1.0f / Screen.height);
			}

			ComputeShader.SetFloat(SID._ParticleThickness, actualThickness);
			
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
			yield return new WaitForSeconds(delay);
			PlayInstant();
		}

		void PlayInstant() {
			if (SystemTime >= Duration && !looping) {
				ResetPlayTime();
			}

			m_prevSystemTime = -1.0f;

			if (prewarm) {
				Simulate(Duration, false);
			}

			m_isPlaying = true;
		}

		static readonly WaitForEndOfFrame WaitFrameYield = new WaitForEndOfFrame();

		IEnumerator SimulateRoutine(float simTime) {
			yield return WaitFrameYield;

			for (int i = 0; i < 50; ++i) {
				UpdateParticles(simTime / 50.0f);
			}
		}

		/// <summary>
		/// Pause current playback
		/// </summary>
		/// <param name="withChildren">Should children particle systems be paused too</param>
		public void Pause(bool withChildren = true) {
			IsPaused = true;

			if (withChildren) {
				Children.ForEach(s => s.Pause());
			}
		}

		/// <summary>
		/// Start playback of the current system
		/// </summary>
		/// <param name="withChildren">Should children system also start playback</param>
		public void Play(bool withChildren = true) {
			IsPaused = false;
			m_isPlaying = true;

			IsStopped = false;

			if (!IsPaused || !looping && SystemTime >= Duration) {
				if (delay > 0) {
					SystemComp.StartCoroutine(PlayRoutine());
				} else {
					PlayInstant();
				}
			}

			if (withChildren) {
				Children.ForEach(s => s.Play());
			}
		}

		/// <summary>
		/// Skip some time ahead by simulating the system for a given time
		/// </summary>
		/// <param name="simulateTime">Time to skip ahead</param>
		/// <param name="withChildren">Should children systems also be simulated with this system</param>
		/// <param name="restart">Should the system be restarted before simulating</param>
		public void Simulate(float simulateTime, bool withChildren = true, bool restart = false) {
			if (simulateTime <= 0.0f) {
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

		/// <summary>
		/// Stop playback of particle system
		/// </summary>
		/// <param name="withChildren">Should children systems also be stopped</param>
		public void Stop(bool withChildren = true) {
			Clear();
			IsStopped = true;
			IsPaused = false;
			m_isPlaying = false;
			SystemTime = 0.0f;

			if (withChildren) {
				Children.ForEach(s => s.Stop());
			}
		}

		/// <summary>
		/// Are there any particles alive in the current system
		/// </summary>
		/// <returns></returns>
		public bool IsAlive() {
			return Emitter.ParticleCount > 0;
		}

		/// <summary>
		/// Clear all currently emitted particles
		/// </summary>
		/// <param name="withChildren">Should children particle systems also be cleared</param>
		public void Clear(bool withChildren = true) {
			Emitter.ClearAllEmittedParticles();

			if (withChildren) {
				Children.ForEach(s => s.Clear());
			}
		}

		/// <summary>
		/// Binds all fields needed to interface with the TC Particles API to a certain Compute shader kernel
		/// </summary>
		/// <param name="comp"></param>
		/// <param name="kernel"></param>
		public void BindPariclesToKernel(CommandBuffer command, ComputeShader comp, int kernel)
		{
			command.SetComputeBufferParam(comp, kernel, SID._SystemParameters, m_systemBuffer);
			command.SetComputeBufferParam(comp, kernel, SID._Particles, particles);

			command.SetComputeFloatParam(comp, SID._DeltTime, ParticleTimeDelta);
			command.SetComputeIntParam(comp, SID._MaxParticles, MaxParticles);
			command.SetComputeIntParam(comp, SID._BufferOffset, Emitter.Offset);
		}

		/// <summary>
		/// Binds all fields needed to interface with the TC Particles API to a certain Compute shader kernel
		/// </summary>
		/// <param name="comp"></param>
		/// <param name="kernel"></param>
		public void BindPariclesToKernel(ComputeShader comp, int kernel) {
			if (comp == null || m_systemBuffer == null) {
				return;
			}

			comp.SetBuffer(kernel, SID._SystemParameters, m_systemBuffer);
			comp.SetBuffer(kernel, SID._Particles, particles);

			comp.SetFloat(SID._DeltTime, ParticleTimeDelta);
			comp.SetInt(SID._MaxParticles, MaxParticles);
			comp.SetInt(SID._BufferOffset, Emitter.Offset);
		}

		/// <summary>
		/// Launch a compute shader kernel with a thread for each current particle
		/// </summary>
		/// <param name="extension">The compute shader to dispatch</param>
		/// <param name="kernel">the kernel to dispatch in the compute shader</param>
		/// <remarks>
		/// The extension compute shader must adhere to certain guidelines:
		/// 
		/// 1. Have a groupsize of (TC_GROUP_SIZE, 1, 1)
		/// 2. Include TCFramework.cginc
		/// 3. To get a particle use particles[GetID(dispatchThreadID.x)]
		/// 4. Use the "_DeltTime" variable for calculations involving delta time
		/// 
		/// For more information see the additional comments in the extension kernel example
		/// </remarks>
		public void DispatchExtensionKernel(ComputeShader extension, int kernel) {
			BindPariclesToKernel(extension, kernel);

			uint x, y, z;
			extension.GetKernelThreadGroupSizes(kernel, out x, out y, out z);

			int disp = Mathf.CeilToInt((float) Emitter.ParticleCount / x);

			if (disp > 0) {
				extension.Dispatch(kernel, disp, 1, 1);
			}
		}

		/// <summary>
		/// Launch a compute shader kernel with a thread for each current particle
		/// </summary>
		/// <param name="extension">The compute shader to dispatch</param>
		/// <param name="kernel">the kernel to dispatch in the compute shader</param>
		/// <remarks>
		/// The extension compute shader must adhere to certain guidelines:
		/// 
		/// 1. Have a groupsize of (TC_GROUP_SIZE, 1, 1)
		/// 2. Include TCFramework.cginc
		/// 3. To get a particle use particles[GetID(dispatchThreadID.x)]
		/// 4. Use the "_DeltTime" variable for calculations involving delta time
		/// 
		/// For more information see the additional comments in the extension kernel example
		/// </remarks>
		public void DispatchExtensionKernel(CommandBuffer command, ComputeShader extension, int kernel)
		{
			BindPariclesToKernel(command, extension, kernel);

			uint x, y, z;
			extension.GetKernelThreadGroupSizes(kernel, out x, out y, out z);

			int disp = Mathf.CeilToInt((float)Emitter.ParticleCount / x);
			command.DispatchCompute(extension, kernel, disp, 1, 1);
		}
	}
}