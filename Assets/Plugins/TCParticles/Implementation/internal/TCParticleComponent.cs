using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TC.Internal {
	[Serializable]
	public class ParticleComponent {
		protected ParticleColliderManager ColliderManager => SystemComp.ColliderManager;
		protected ParticleEmitter Emitter => SystemComp.Emitter;
		protected ParticleForceManager ForceManager => SystemComp.ForceManager;
		protected ParticleRenderer Renderer => SystemComp.ParticleRenderer;
		protected ParticleManager Manager => SystemComp.Manager;

		protected TCParticleSystem SystemComp;

		protected int ClearKernel;
		protected int EmitKernel;

		public int UpdateAllKernel;
		protected int UpdateCollidersKernel;

		protected int UpdateForcesKernel;
		protected int UpdateTurbulenceForcesKernel;

		protected const int ColliderStride = 96;
		protected const int ForcesStride = 124;

		protected const int ParticleStride = 4 * (3 + 1 + 3 + 1 + 1 + 1 + 1 + 1);

		protected float SimulationDeltTime;

		float m_lastUpdate;

		public ComputeShader ComputeShader;

		protected Vector3 ParentPosition {
			get {
				if (SystemComp.transform.parent == null) {
					return Vector3.zero;
				}

				return SystemComp.transform.parent.position;
			}
		}

		float CurTime => Application.isPlaying ? Time.time : Time.realtimeSinceStartup;

		protected bool ShouldUpdate() {
			bool culled = Renderer.UseFrustumCulling && !Renderer.isVisible;

			if (!culled || Renderer.culledSimulationMode == CulledSimulationMode.UpdateNormally) {
				SimulationDeltTime = CurTime - m_lastUpdate;
				m_lastUpdate = CurTime;
				return true;
			}

			switch (Renderer.culledSimulationMode) {
				case CulledSimulationMode.StopSimulation:
					SimulationDeltTime = 0.0f;
					return false;

				case CulledSimulationMode.SlowSimulation:
					SimulationDeltTime = CurTime - m_lastUpdate;
					if (SimulationDeltTime > Renderer.cullSimulationDelta) {
						m_lastUpdate = CurTime;
						return true;
					}

					break;
			}

			return false;
		}

		public static int SizeOf<T>() {
			return Marshal.SizeOf(typeof(T));
		}

		internal void Awake(TCParticleSystem comp) {
			//reference all components for easy acess
			SystemComp = comp;
			ComputeShader = TCParticleGlobalManager.Instance.ComputeShader;

			//find all kernels for quick acces.
			UpdateAllKernel = ComputeShader.FindKernel("UpdateAll");
			EmitKernel = ComputeShader.FindKernel("Emit");
			ClearKernel = ComputeShader.FindKernel("Clear");

			UpdateForcesKernel = ComputeShader.FindKernel("UpdateForces");
			UpdateTurbulenceForcesKernel = ComputeShader.FindKernel("UpdateTurbulenceForces");

			UpdateCollidersKernel = ComputeShader.FindKernel("UpdateColliders");

			Initialize();
		}

		internal virtual void OnEnable() {
		}

		internal virtual void OnDisable() {
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		protected virtual void Bind() {
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		protected virtual void Initialize() {
		}

		//SetParticles does a global set, so you can be sure all kernels neccesary for updating use the right memory.
		protected void BindParticles() {
			Manager.Bind();
			Emitter.Bind();
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