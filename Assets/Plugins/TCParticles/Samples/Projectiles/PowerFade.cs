using UnityEngine;
using TC;

public class PowerFade : MonoBehaviour {
	public AnimationCurve animationCurve = new AnimationCurve();
	public float duration = 5.0f;

	float m_startPower;
	float m_startTime;

	TCForce m_force;

	void Start() {
		m_force = GetComponent<TCForce>();
		m_startPower = m_force.power;
		m_startTime = Time.time;
	}

	void Update() {
		if (m_force != null){
			m_force.power = animationCurve.Evaluate(Mathf.Min(1.0f, (Time.time - m_startTime) / duration)) * m_startPower;
		}
	}
}
