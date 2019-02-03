using TC;
using UnityEngine;

public class SpringParticles : MonoBehaviour {
	public ComputeShader SpringPosCompute;

	public bool DoSpring = true;

	public float SpringConstant = 0.1f;
	public float SpringDamping = 0.5f;

	//Float3 buffer storing initial positions
	ComputeBuffer m_initialPositions;
	TCParticleSystem m_system;

	//Kernels to be dispatched
	int m_updateKernel, m_writePosKernel;

	void OnEnable() {
		//Cache reference to
		m_system = GetComponent<TCParticleSystem>();

		//Count is max nr. of particles the system can have. Buffer contains float3 -> so a stride of 4 * 3 = 12 bytes
		m_initialPositions = new ComputeBuffer(m_system.MaxParticles, 12);

		//Whenever we emit new particles, we need to update our initial position buffer
		m_system.Emitter.OnEmissionCallback += OnEmissionCallback;

		//After emission we need to run our code applying the spring force
		m_system.Manager.OnPostSimulationCallback += OnPostSimulationCallback;

		//Find the kernels we'll dispatch later
		m_writePosKernel = SpringPosCompute.FindKernel("WriteInitialPos");
		m_updateKernel = SpringPosCompute.FindKernel("UpdateSpring");
	}

	void OnEmissionCallback(int emittedCount) {
		//Bind our buffer to write into
		SpringPosCompute.SetBuffer(m_writePosKernel, "_InitialPos", m_initialPositions);
		m_system.Emitter.DispatchEmitExtensionKernel(SpringPosCompute, m_writePosKernel);
	}

	void OnPostSimulationCallback() {
		if (DoSpring) {
			//Bind our buffer to read initial positions
			SpringPosCompute.SetBuffer(m_updateKernel, "_InitialPos", m_initialPositions);
			SpringPosCompute.SetFloat("_SpringConstant", SpringConstant);

			float dampingDt = Mathf.Pow(Mathf.Abs(1.0f - SpringDamping), m_system.ParticleTimeDelta);
			SpringPosCompute.SetFloat("_SpringDamping", dampingDt);

			m_system.Manager.DispatchExtensionKernel(SpringPosCompute, m_updateKernel);
		}
	}

	void OnDisable() {
		m_initialPositions.Dispose();

		//Deregister callbacks
		m_system.Emitter.OnEmissionCallback -= OnEmissionCallback;
		m_system.Manager.OnPostSimulationCallback -= OnPostSimulationCallback;
	}
}