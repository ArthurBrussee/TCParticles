using System;
using UnityEngine;

namespace TC.Internal {
	[Serializable]
	public class ParticleComponent {
		#region ComponentReferences
		[NonSerialized] protected ParticleColliderManager ColliderManager;
		[NonSerialized] protected ParticleEmitter PartEmitter;
		[NonSerialized] protected ParticleForceManager ForceManager;
		[NonSerialized] protected ParticleRenderer Renderer;
		[NonSerialized] protected ParticleManager Manager;

		[NonSerialized] public TCParticleSystem SystemComp;
		#endregion

		#region ForceTypeKernel enum
		public enum ForceTypeKernel {
			Normal,
			Turbulence,
			Count
		}
		#endregion

		protected const int ForceTypeKernelCount = (int) ForceTypeKernel.Count;

		protected int ClearKernel;
		protected int EmitKernel;

		protected int UpdateAllKernel;
		protected int UpdateCollidersKernel;

		protected int UpdateForcesKernel;
		protected int UpdateTurbulenceForcesKernel;

		protected const int ColliderStride = 96;
		protected const int ForcesStride = 124;

		protected ComputeShader ComputeShader;
		protected const int GroupSize = 128;


		float m_lastUpdate;
		protected float SimulationDeltTIme;

		public Transform Transform;

		protected bool Supported {
			get { return SystemInfo.supportsComputeShaders; }
		}

		protected Vector3 ParentPosition {
			get {
				if (Transform.parent == null) return Vector3.zero;
				return Transform.parent.position;
			}
		}

		float CurTime {
			get { return Application.isPlaying ? Time.time : Time.realtimeSinceStartup; }
		}

		protected bool ShouldUpdate() {
			if (Renderer.isVisible || Renderer.culledSimulationMode == CulledSimulationMode.UpdateNormally) {
				SimulationDeltTIme = CurTime - m_lastUpdate;
				m_lastUpdate = CurTime;
				return true;
			}

			switch (Renderer.culledSimulationMode) {
				case CulledSimulationMode.StopSimulation:
					SimulationDeltTIme = 0.0f;
					return false;

				case CulledSimulationMode.SlowSimulation:
					SimulationDeltTIme = CurTime - m_lastUpdate;
					if (SimulationDeltTIme > Renderer.cullSimulationDelta) {
						m_lastUpdate = CurTime;
						return true;
					}

					break;
			}

			return false;
		}

		public static int SizeOf<T>() {
			return System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
		}

		public void Awake(ParticleEmitter emitter, ParticleColliderManager colliderManager, ParticleRenderer renderer,
			ParticleForceManager forceManager, ParticleManager system) {
			//reference all components for easy acess

			PartEmitter = emitter;
			ColliderManager = colliderManager;
			Renderer = renderer;
			ForceManager = forceManager;
			Manager = system;

			//Load the compute shader reference
			if (system.ComputeShader == null) {
				ComputeShader = Resources.Load("MassParticle") as ComputeShader;
			}

			ComputeShader = system.ComputeShader;

			if (ComputeShader == null) {
				Debug.LogError("Failed to load compute shader!");
				return;
			}

			//find all kernels for quick acces.
			UpdateAllKernel = ComputeShader.FindKernel("UpdateAll");
			EmitKernel = ComputeShader.FindKernel("Emit");
			ClearKernel = ComputeShader.FindKernel("Clear");


			UpdateForcesKernel = ComputeShader.FindKernel("UpdateForces");
			UpdateTurbulenceForcesKernel = ComputeShader.FindKernel("UpdateTurbulenceForces");

			UpdateCollidersKernel = ComputeShader.FindKernel("UpdateColliders");
			Transform = SystemComp.transform;
		}


		public virtual void Initialize() {}
		public virtual void OnEnable() {}
		public virtual void OnDisable() {}
		public virtual void OnDestroy() {}
		protected virtual void Bind() {}

		//SetParticles does a global set, so you can be sure all kernels neccesary for updating use the right memory.
		protected void BindParticles() {
			Manager.Bind();
			PartEmitter.Bind();
			ForceManager.Bind();
			ColliderManager.Bind();
		}

		protected void Release(ref ComputeBuffer buf) {
			if (buf != null) {
				buf.Release();
				buf = null;
			}
		}
	}
}