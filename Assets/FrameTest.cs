using System.Collections;
using System.Collections.Generic;
using TC;
using UnityEngine;

public class FrameTest : MonoBehaviour {
	public int Count = 1000 * 1000;
	
	ParticleProto[] m_prototypes;

	TCParticleSystem m_system;
	
	// Use this for initialization
	void Awake () {
		m_prototypes = new ParticleProto[Count];

		for (int i = 0; i < m_prototypes.Length; ++i) {
			m_prototypes[i].Size = Random.value;
			m_prototypes[i].Color = Random.ColorHSV(0.0f, 1.0f);
			m_prototypes[i].Position = Random.insideUnitSphere * 20.0f;
			m_prototypes[i].Velocity = Vector3.zero;
		}

		m_system = GetComponent<TCParticleSystem>();
	}

	// Update is called once per frame
	void Update () {
		// Clear particles from previous frame
		m_system.Clear();
		
		// Add in new particles
		m_system.Emitter.Emit(m_prototypes);
	}
}
