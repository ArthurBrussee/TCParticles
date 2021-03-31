using UnityEngine;

public class OrbitCam : MonoBehaviour {
	public Transform Target;
	public float XSpeed = 120.0f;
	public float YSpeed = 120.0f;

	public float YMinLimit = -20f;
	public float YMaxLimit = 80f;

	public float Distance = 5.0f;
	public float DistanceMin = .5f;
	public float DistanceMax = 15f;

	float m_x;
	float m_y;

	void OnEnable() {
		Vector3 angles = transform.eulerAngles;
		m_x = angles.y;
		m_y = angles.x;

		// Make the rigid body not change rotation
		var rb = GetComponent<Rigidbody>();

		if (rb) {
			rb.freezeRotation = true;
		}
	}

	void LateUpdate() {
		m_x += Input.GetAxis("Mouse X") * XSpeed * 0.02f;
		m_y -= Input.GetAxis("Mouse Y") * YSpeed * 0.02f;
		m_y = ClampAngle(m_y, YMinLimit, YMaxLimit);

		Quaternion rotation = Quaternion.Euler(m_y, m_x, 0);
		Distance = Mathf.Clamp(Distance - Input.GetAxis("Mouse ScrollWheel") * 25, DistanceMin, DistanceMax);

		Vector3 negDistance = new Vector3(0.0f, 0.0f, -Distance);
		Vector3 position = rotation * negDistance + Target.position;

		transform.SetPositionAndRotation(position, rotation);
	}

	public static float ClampAngle(float angle, float min, float max) {
		if (angle < -360F) {
			angle += 360F;
		}

		if (angle > 360F) {
			angle -= 360F;
		}

		return Mathf.Clamp(angle, min, max);
	}
}