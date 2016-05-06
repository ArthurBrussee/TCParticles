using System.Collections.Generic;
using TC;
using TC.Internal;
using UnityEngine;
using ParticleEmitter = TC.Internal.ParticleEmitter;

[ExecuteInEditMode]
[AddComponentMenu("TC Particles/Shape Emitter")]
public class TCShapeEmitter : MonoBehaviour {
	public static List<TCShapeEmitter> All = new List<TCShapeEmitter>();

	public ParticleEmitterShape ShapeData = new ParticleEmitterShape();
	public float EmissionRate;
	public ParticleEmitter.EmissionTypeEnum EmissionType;
	public bool Emit = true;
	
	private Vector3 m_prevPos;
	private Vector3 m_prevSpeed;
	
	private float m_femit;


	[SerializeField]
	private TCShapeEmitTag m_tag;
	
	private bool m_firstEmit;


	void Awake() {
		m_firstEmit = true;
	}

	private void OnEnable() {
		All.Add(this);
		var systs = TCParticleSystem.All;

		for (int i = 0; i < systs.Count; i++) {
			(systs[i].Emitter as ParticleEmitter).RegisterShape(this);

		}
	}


	private void OnDisable() {
		All.RemoveUnordered(this);
		var systs = TCParticleSystem.All;
		
		for (int i = 0; i < systs.Count; i++) {
			(systs[i].Emitter as ParticleEmitter).RemoveShape(this);
		}
	}


	public void BurstEmit(int particles) {
		if (!enabled) {
			return;
		}

		m_femit += particles;
	}


	private struct BurstToDo
	{
		public int Amount;
		public ParticleEmitter Emitter;

	}
	private List<BurstToDo> m_bursts; 

	public void BurstForSystem(int particles, TCParticleSystem system) {
		if (!enabled) {
			return;
		}

		if (m_bursts == null) {
			m_bursts = new List<BurstToDo>();
		}

		m_bursts.Add(new BurstToDo() {Amount = particles, Emitter = system.Emitter as ParticleEmitter});
	}



	public void InternalUpdateForEmitter(ParticleEmitter emitter, float deltaTime) {
		if (!Emit) {
			return;
		}

		if (!emitter.DoEmit) {
			return;
		}

		if (!enabled && m_femit == 0) {
			return;
		}


		if (m_firstEmit) {
			m_prevPos = emitter.GetEmitPos(transform);
			m_prevSpeed = Vector3.zero;

			m_firstEmit = false;
		}


		if (enabled) {
			if (m_bursts != null) {
				if (m_bursts.Exists(m => m.Emitter == emitter)) {
					int index = m_bursts.FindIndex(m => m.Emitter == emitter);

					int amount = m_bursts[index].Amount;
					m_femit += amount;

					m_bursts.RemoveAt(index);
				}
			}

			if (EmissionType == ParticleEmitter.EmissionTypeEnum.PerSecond) {
				m_femit += deltaTime * EmissionRate;
			}
			else {
				Vector3 pos = emitter.GetEmitPos(transform);
				Vector3 delta = pos - m_prevPos;
				m_femit += delta.magnitude * EmissionRate;
			}
		}
		
		int num = Mathf.FloorToInt(m_femit);
		m_femit -= num;
		
		//pdate shape
		emitter.SetShapeData(ShapeData, transform, ref m_prevPos, ref m_prevSpeed);
		emitter.CommitBuffer();
		
		//Set local data
		emitter.EmitNow(num);
	}



	private void OnDrawGizmosSelected() {
		Gizmos.DrawIcon(transform.position, "TCParticles.png", true);

		Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.localScale);
		Gizmos.matrix = rotationMatrix;

		if (ShapeData.shape == EmitShapes.Box) {
			Gizmos.DrawWireCube(Vector3.zero, ShapeData.cubeSize);
		}	
	}


	public bool HasTag(TCShapeEmitTag emitTag) {
		return emitTag == m_tag;
	}
}

