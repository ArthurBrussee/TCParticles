using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class FreeLookOnClick : MonoBehaviour {
	void Start() {
		CinemachineCore.GetInputAxis = GetAxisCustom;
	}

	public float GetAxisCustom(string axisName) {
		bool freeLook = Input.GetMouseButton(1) || Input.GetKey(KeyCode.LeftControl);

		if (axisName == "Mouse X") {
			if (freeLook) {
				return Input.GetAxis("Mouse X");
			} else {
				return 0;
			}
		} else if (axisName == "Mouse Y") {
			if (freeLook) {
				return Input.GetAxis("Mouse Y");
			} else {
				return 0;
			}
		}

		return Input.GetAxis(axisName);
	}
}