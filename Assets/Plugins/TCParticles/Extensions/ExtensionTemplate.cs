using UnityEngine;

public class ExtensionTemplate : MonoBehaviour {
	public TCParticleSystem System;
	public ComputeShader Extension;

	[Range(0.0f, 10.0f)] public float AccelSpeed = 1.0f;

	// Update is called once per frame
	void Update() {
		//Bind own custom variables to compute shader
		Extension.SetFloat("AccelSpeed", AccelSpeed);

		//Then dispatch
		System.Manager.DispatchExtensionKernel(Extension, "MyExtensionKernel");
	}
}