using TC;
using UnityEngine;
using UnityEngine.UI;

public class ForceEnable : MonoBehaviour {
	public Text CurrentForceText;

	public TCForce ForceTarget;
	public OrbitCam Cam;

	string m_startText;

	void OnEnable() {
		if (ForceTarget != null) {
			ForceTarget.enabled = false;
		}

		m_startText = CurrentForceText.text;

		ForceTarget.forceType = ForceType.Radial;
		ForceTarget.power = -60.0f;
	}

	void Update() {
		if (Input.GetMouseButton(0)) {
			ForceTarget.enabled = true;
			Ray ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
			Plane plane = new Plane(Vector3.Cross(transform.up, transform.right), Cam.Target.transform.position);

			float dist;
			if (plane.Raycast(ray, out dist)) {
				ForceTarget.transform.position = transform.position + ray.direction * dist;
			}
		} else {
			ForceTarget.enabled = false;
		}

		CurrentForceText.text = m_startText + "(Current Type: " + ForceTarget.forceType + ")";

		if (Input.GetKey(KeyCode.Alpha1)) {
			ForceTarget.forceType = ForceType.Radial;
			ForceTarget.power = -60.0f;
		} else if (Input.GetKey(KeyCode.Alpha2)) {
			ForceTarget.forceType = ForceType.Vortex;
			ForceTarget.power = 60.0f;
		} else if (Input.GetKey(KeyCode.Alpha3)) {
			ForceTarget.forceType = ForceType.Turbulence;
			ForceTarget.power = 60.0f;
		} else if (Input.GetKey(KeyCode.Alpha4)) {
			ForceTarget.forceType = ForceType.Vector;
			ForceTarget.power = 60.0f;
		} else if (Input.GetKey(KeyCode.Alpha5)) {
			ForceTarget.forceType = ForceType.Drag;
			ForceTarget.power = 60.0f;
		}	
	}
}
