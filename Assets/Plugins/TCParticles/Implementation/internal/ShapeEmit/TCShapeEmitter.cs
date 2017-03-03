using System;
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

	[NonSerialized]
	public Vector3 PrevPos;

	[NonSerialized]
	public Vector3 PrevSpeed;

	float m_femit;

	[SerializeField]
	TCShapeEmitTag m_tag;
	
	bool m_firstEmit;


	void Awake() {
		m_firstEmit = true;
	}

	void OnEnable() {
		All.Add(this);
		var systs = TCParticleSystem.All;

		for (int i = 0; i < systs.Count; i++) {
			(systs[i].Emitter as ParticleEmitter).RegisterShape(this);

		}
	}


	void OnDisable() {
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


	void OnDrawGizmosSelected() {
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

	public int TickEmission(ParticleEmitter emitter, float deltaTime) {
		if (m_firstEmit) {
			PrevPos = emitter.GetEmitPos(transform);
			PrevSpeed = Vector3.zero;
			m_firstEmit = false;
		}

		if (EmissionType == ParticleEmitter.EmissionTypeEnum.PerSecond) {
			m_femit += deltaTime * EmissionRate;
		} else {
			Vector3 pos = emitter.GetEmitPos(transform);
			Vector3 delta = pos - PrevPos;
			m_femit += delta.magnitude * EmissionRate;
		}


		int num = Mathf.FloorToInt(m_femit);
		m_femit -= num;

		return num;
	}
}

