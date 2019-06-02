using UnityEngine;

public class SmoothRandomRotate : MonoBehaviour {
	public float strength = 1.0f;

	float m_seed;
	Vector3 m_rotateSpeed;
	float m_friction = 0.99f;
	Rigidbody m_rigid;

	void OnEnable() {
		m_seed = Random.Range(0.0f, 100.0f);
		m_rigid = GetComponent<Rigidbody>();
	}

	float SmoothRand(float y) {
		return Mathf.PerlinNoise(Time.time + m_seed, y) * 2.0f - 1.0f;
	}

	void Update() {
		//Adjust speed with three random noise tracks
		m_rotateSpeed += new Vector3(SmoothRand(0.0f),
			               SmoothRand(1.0f),
			               SmoothRand(2.0f)) * strength * Time.deltaTime * 3.0f;

		m_rotateSpeed *= m_friction;

		if (m_rigid == null){
			transform.Rotate(m_rotateSpeed);
		}
	}

	void FixedUpdate() {
		if (m_rigid != null){
			m_rigid.MoveRotation(Quaternion.Euler(m_rotateSpeed));
		}
	}
}